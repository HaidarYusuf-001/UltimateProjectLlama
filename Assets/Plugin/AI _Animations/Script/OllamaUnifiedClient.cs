using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
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
    private Stopwatch stopwatch;
    private StringBuilder currentResponse = new StringBuilder();
    private Dictionary<string, string> responseCache = new Dictionary<string, string>();
    private bool isProcessing = false;

    void Start()
    {
        // Auto Assign Komponen
        if (animationController == null)
            animationController = GetComponent<Animator>() ?? FindAnyObjectByType<Animator>();

        if (audioPlayer == null)
            audioPlayer = GetComponent<LMNTAudioPlayer>() ?? FindAnyObjectByType<LMNTAudioPlayer>();

        if (audioPlayer != null)
            audioPlayer.OnAudioPlaybackComplete += HandleAudioPlaybackComplete;
        else
            Debug.LogWarning("LMNTAudioPlayer tidak ditemukan!");

        // Inisialisasi WebSocket
        websocket = new WebSocket(websocketUrl);

        websocket.OnOpen += () => Debug.Log("WebSocket connected");
        websocket.OnError += (e) => Debug.LogError("WebSocket error: " + e);
        websocket.OnClose += (e) =>
        {
            Debug.Log("WebSocket closed");
            if (isProcessing && currentResponse.Length == 0)
                ReturnToIdle();
        };

        websocket.OnMessage += (bytes) =>
        {
            string msg = Encoding.UTF8.GetString(bytes);
            try
            {
                OllamaResponse chunk = JsonUtility.FromJson<OllamaResponse>(msg);
                currentResponse.Append(chunk.response);
                outputText.text = currentResponse.ToString();

                if (chunk.done)
                {
                    stopwatch.Stop();
                    float latency = (float)stopwatch.Elapsed.TotalSeconds;
                    Debug.Log($"Response complete. Latency: {latency:F2}s");

                    UpdateCache(inputField.text, currentResponse.ToString());
                    FinalizeResponse(currentResponse.ToString());
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Gagal parsing JSON: {msg}\n{ex.Message}");
            }
        };

        _ = websocket.Connect();
    }

    public void OnSendButtonPressed()
    {
        string userInput = inputField.text.Trim();
        if (string.IsNullOrEmpty(userInput)) return;

        if (isProcessing)
        {
            Debug.LogWarning("Masih memproses permintaan sebelumnya");
            return;
        }

        if (responseCache.ContainsKey(userInput))
        {
            string cached = responseCache[userInput];
            outputText.text = cached;
            FinalizeResponse(cached, useAudio: false);
            return;
        }

        SendMessageToServer(userInput);
    }

    public async void SendMessageToServer(string message)
    {
        isProcessing = true;
        currentResponse.Clear();
        outputText.text = "";
        stopwatch = Stopwatch.StartNew();

        animationController?.Play("Wave");

        if (websocket.State != WebSocketState.Open)
        {
            Debug.Log("Menunggu koneksi WebSocket...");
            await websocket.Connect();
        }

        try
        {
            await websocket.SendText(message);
            StartCoroutine(CheckTimeout());
        }
        catch (Exception ex)
        {
            Debug.LogError("Gagal kirim ke WebSocket: " + ex.Message);
            ReturnToIdle();
        }
    }

    private IEnumerator CheckTimeout()
    {
        float startTime = Time.time;
        while (isProcessing && Time.time - startTime < responseTimeout)
            yield return null;

        if (isProcessing)
        {
            Debug.LogError("Timeout WebSocket setelah " + responseTimeout + " detik");
            ReturnToIdle();
        }
    }

    private void FinalizeResponse(string finalText, bool useAudio = true)
    {
        string clean = finalText.Trim();
        if (!string.IsNullOrEmpty(clean))
        {
            if (useAudio && audioPlayer != null)
            {
                animationController?.Play("Talking");
                audioPlayer.PlayText(clean);
            }
            else
            {
                animationController?.Play("Idle");
            }
        }
        else
        {
            Debug.LogWarning("Respons kosong");
            ReturnToIdle();
        }

        isProcessing = false;
    }

    private void ReturnToIdle()
    {
        animationController?.Play("Idle");
        isProcessing = false;
    }

    private void HandleAudioPlaybackComplete()
    {
        Debug.Log("Audio selesai, kembali ke idle");
        animationController?.Play("Idle");
    }

    private void UpdateCache(string question, string response)
    {
        if (!responseCache.ContainsKey(question))
        {
            responseCache[question] = response;
            Debug.Log("Cache disimpan untuk pertanyaan: " + question);
        }
    }

    private void OnDestroy()
    {
        if (audioPlayer != null)
            audioPlayer.OnAudioPlaybackComplete -= HandleAudioPlaybackComplete;

        _ = websocket?.Close();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    [Serializable]
    private class OllamaResponse
    {
        public string response;
        public bool done;
    }
}
