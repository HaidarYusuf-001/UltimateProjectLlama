using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NativeWebSocket;
using Debug = UnityEngine.Debug;

public class OllamaUnifiedClient : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField inputField;
    public TMP_Text outputText;

    [Header("WebSocket")]
    public string websocketUrl = "ws://localhost:8765";
    public float responseTimeout = 30f;

    [Header("Komponen")]
    public LMNTAudioPlayer audioPlayer;
    public Animator animationController;

    private WebSocket websocket;
    private StringBuilder sentenceBuilder = new StringBuilder();
    private Queue<string> sentenceQueueForAudio = new Queue<string>();
    private Queue<string> sentenceQueueForDisplay = new Queue<string>();

    private bool isAwaitingResponse = false;
    private Coroutine audioRequestCoroutine = null;

    void Start()
    {
        if (animationController == null)
            animationController = GetComponent<Animator>() ?? FindAnyObjectByType<Animator>();
        if (audioPlayer == null)
            audioPlayer = GetComponent<LMNTAudioPlayer>() ?? FindAnyObjectByType<LMNTAudioPlayer>();

        if (audioPlayer != null)
        {
            audioPlayer.OnAudioStart += OnAudioStartHandler;
            audioPlayer.OnAudioPlaybackComplete += HandleAudioPlaybackComplete;
        }

        ConnectWebSocket();
    }

    private async void ConnectWebSocket()
    {
        websocket = new WebSocket(websocketUrl);

        websocket.OnOpen += () => Debug.Log("WebSocket connected");
        websocket.OnError += (e) => Debug.LogError("WebSocket error: " + e);
        websocket.OnClose += (e) => {
            Debug.Log("WebSocket closed");
            if (isAwaitingResponse) ReturnToIdle();
        };

        websocket.OnMessage += (bytes) => {
            string msg = Encoding.UTF8.GetString(bytes);
            try
            {
                OllamaResponse chunk = JsonUtility.FromJson<OllamaResponse>(msg);
                HandleIncomingText(chunk.response, chunk.done);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Gagal parsing JSON: {msg}\n{ex.Message}");
            }
        };

        await websocket.Connect();
    }

    public void OnSendButtonPressed()
    {
        string userInput = inputField.text.Trim();
        if (string.IsNullOrEmpty(userInput) || isAwaitingResponse) return;

        ResetState();
        animationController?.SetBool("isThinking", true);
        isAwaitingResponse = true;

        // Logika custom response bisa ditaruh di sini
        if (userInput.ToLower().Contains("siapa") && userInput.ToLower().Contains("kamu"))
        {
            string customResponse = "Halo! Saya adalah virtual assistant yang dapat berjalan secara offline yang dibuat oleh prodi Informatika UMM untuk membantu menjawab pertanyaan Anda. Apa yang bisa saya bantu?";
            ProcessSingleSentence(customResponse);
            isAwaitingResponse = false;
            return;
        }

        SendMessageToServer(userInput);
    }

    private void ResetState()
    {
        sentenceBuilder.Clear();
        sentenceQueueForAudio.Clear();
        sentenceQueueForDisplay.Clear();
        outputText.text = "";

        if (audioRequestCoroutine != null)
        {
            StopCoroutine(audioRequestCoroutine);
            audioRequestCoroutine = null;
        }
    }

    public async void SendMessageToServer(string message)
    {
        if (websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("WebSocket not connected. Attempting to reconnect...");
            await websocket.Connect();
        }

        if (websocket.State == WebSocketState.Open)
        {
            await websocket.SendText(message);
        }
        else
        {
            Debug.LogError("Failed to send message, WebSocket is not open.");
            ReturnToIdle();
        }
    }

    private void HandleIncomingText(string chunk, bool isFinal)
    {
        sentenceBuilder.Append(chunk);

        while (true)
        {
            string text = sentenceBuilder.ToString();
            int sentenceEnd = text.IndexOfAny(new char[] { '.', '?', '!' });

            if (sentenceEnd == -1) break;

            string sentence = text.Substring(0, sentenceEnd + 1).Trim();
            sentenceBuilder.Remove(0, sentenceEnd + 1);

            if (!string.IsNullOrWhiteSpace(sentence))
            {
                ProcessSingleSentence(sentence);
            }
        }

        if (isFinal)
        {
            string leftover = sentenceBuilder.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(leftover))
            {
                ProcessSingleSentence(leftover);
            }
            sentenceBuilder.Clear();
            isAwaitingResponse = false;
        }
    }

    private void ProcessSingleSentence(string sentence)
    {
        sentenceQueueForAudio.Enqueue(sentence);
        sentenceQueueForDisplay.Enqueue(sentence);

        if (audioRequestCoroutine == null)
        {
            audioRequestCoroutine = StartCoroutine(RequestAudioSequentially());
        }
    }

    private IEnumerator RequestAudioSequentially()
    {
        while (sentenceQueueForAudio.Count > 0)
        {
            string sentence = sentenceQueueForAudio.Dequeue();
            audioPlayer.PlayText(sentence);
            yield return new WaitForSeconds(0.1f);
        }
        audioRequestCoroutine = null;
    }

    private void OnAudioStartHandler()
    {
        // Transisi: Thinking -> Talking atau tetap di Talking
        animationController?.SetBool("isThinking", false);
        animationController?.SetBool("isTalking", true);

        if (sentenceQueueForDisplay.Count > 0)
        {
            string sentenceToDisplay = sentenceQueueForDisplay.Dequeue();
            outputText.text += sentenceToDisplay + " ";
        }
    }

    private void HandleAudioPlaybackComplete()
    {
        // Fungsi ini HANYA dipanggil ketika antrian audio di LMNTAudioPlayer habis.
        Debug.Log("Rangkaian audio telah selesai diputar.");

        // Langkah 1: Hentikan animasi berbicara.
        animationController?.SetBool("isTalking", false);

        // Langkah 2: Tentukan state selanjutnya.
        // Apakah kita masih menunggu kalimat baru dari server atau masih ada antrian teks?
        if (isAwaitingResponse || sentenceQueueForAudio.Count > 0)
        {
            // Jika ya, masuk ke mode Thinking.
            // Transisi: Talking -> Thinking
            Debug.Log("Masih menunggu data, masuk ke mode Thinking.");
            animationController?.SetBool("isThinking", true);
        }
        else
        {
            // Jika tidak, semua proses sudah selesai. Kembali ke Idle.
            // Transisi: Talking -> Idle
            Debug.Log("Semua proses selesai, kembali ke Idle.");
            ReturnToIdle(); // Ini akan mengatur isThinking ke false.
        }
    }

    private void ReturnToIdle()
    {
        isAwaitingResponse = false;
        animationController?.SetBool("isTalking", false);
        animationController?.SetBool("isThinking", false);
        Debug.Log("System is now Idle.");
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    void Update()
    {
        websocket?.DispatchMessageQueue();
    }
#endif

    private void OnDestroy()
    {
        if (audioPlayer != null)
        {
            audioPlayer.OnAudioStart -= OnAudioStartHandler;
            audioPlayer.OnAudioPlaybackComplete -= HandleAudioPlaybackComplete;
        }
        _ = websocket?.Close();
    }

    [Serializable]
    private class OllamaResponse
    {
        public string response;
        public bool done;
    }
}