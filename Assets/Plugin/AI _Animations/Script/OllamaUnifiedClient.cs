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
    private Stopwatch stopwatch;
    private StringBuilder sentenceBuilder = new StringBuilder();
    private List<string> allSentences = new List<string>();
    private Dictionary<string, string> responseCache = new Dictionary<string, string>();

    private bool isProcessing = false;
    private bool isReceivingResponse = false;
    private bool waitingForAudio = false;
    private Coroutine timeoutCoroutine;

    private Queue<string> sentenceQueue = new Queue<string>();
    private Coroutine sentencePlayerCoroutine = null;
    private Queue<string> sentenceDisplayQueue = new Queue<string>();

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

        websocket = new WebSocket(websocketUrl);

        websocket.OnOpen += () => Debug.Log("WebSocket connected");
        websocket.OnError += (e) => Debug.LogError("WebSocket error: " + e);
        websocket.OnClose += (e) =>
        {
            Debug.Log("WebSocket closed");
            if (isProcessing) ReturnToIdle();
        };

        websocket.OnMessage += (bytes) =>
        {
            string msg = Encoding.UTF8.GetString(bytes);
            try
            {
                OllamaResponse chunk = JsonUtility.FromJson<OllamaResponse>(msg);

                bool isFinal = chunk.done;
                HandleIncomingText(chunk.response, isFinal);

                if (isFinal)
                {
                    stopwatch.Stop();
                    Debug.Log($"Response complete. Latency: {stopwatch.Elapsed.TotalSeconds:F2}s");

                    string finalText = string.Join(" ", allSentences);
                    UpdateCache(inputField.text, finalText);
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

        if (isProcessing || waitingForAudio)
        {
            Debug.LogWarning("Masih memproses permintaan sebelumnya");
            return;
        }

        // Cek jika pertanyaannya adalah "siapa kamu"
        string userInputLower = userInput.ToLower();

        if (string.IsNullOrEmpty(userInput)) return;

        if (isProcessing || waitingForAudio)
        {
            Debug.LogWarning("Masih memproses permintaan sebelumnya");
            return;
        }

        // Deteksi pertanyaan "siapa kamu" dalam berbagai bentuk
        if (userInputLower.Contains("siapa") && userInputLower.Contains("kamu"))
        {
            string customResponse = "Halo! Saya adalah virtual assistant yang dibuat oleh prodi Informatika UMM untuk membantu menjawab pertanyaan Anda. Apa yang bisa saya bantu?";
            outputText.text = customResponse;
            FinalizeResponse(customResponse, useAudio: true);
            return;
        }

        else if ((userInputLower.Contains("informatika") || userInputLower.Contains("informatica")) && userInputLower.Contains("umm"))
        {
            string[] customResponses = new string[]
            {
        "Program Studi Informatika UMM memiliki visi menjadi program studi terkemuka dalam pengembangan ilmu pengetahuan, teknologi, rekayasa dan seni di bidang rekayasa perangkat lunak, sistem dan keamanan jaringan, sains data, dan game cerdas yang berlandaskan pada nilai-nilai Islam.",
        "Misinya adalah menyelenggarakan pendidikan dan pembelajaran secara profesional dan islami, melakukan penelitian yang inovatif dan bermutu, mengabdi kepada masyarakat melalui teknologi informasi, serta menjalin kerja sama dengan berbagai lembaga.",
        "Tujuannya adalah menghasilkan lulusan yang kompeten dan berjiwa wirausaha, menghasilkan karya penelitian yang mendukung pendidikan, serta menjalin kerja sama untuk kemajuan pendidikan dan pengabdian masyarakat."
            };

            outputText.text = "";
            sentenceQueue.Clear();
            sentenceDisplayQueue.Clear();
            foreach (string sentence in customResponses)
            {
                sentenceQueue.Enqueue(sentence);
                sentenceDisplayQueue.Enqueue(sentence);
            }

            if (sentencePlayerCoroutine == null)
                sentencePlayerCoroutine = StartCoroutine(PlaySentencesSequentially());

            return;
        }



        // Cek cache
        if (responseCache.ContainsKey(userInput))
        {
            string cached = responseCache[userInput];
            outputText.text = cached;
            FinalizeResponse(cached, useAudio: false);
            return;
        }

        // Lanjut kirim ke server jika tidak termasuk kasus khusus
        SendMessageToServer(userInput);
    }


    public async void SendMessageToServer(string message)
    {
        isProcessing = true;
        isReceivingResponse = true;
        waitingForAudio = false;
        sentenceBuilder.Clear();
        allSentences.Clear();
        sentenceQueue.Clear();
        sentenceDisplayQueue.Clear();
        outputText.text = "";
        stopwatch = Stopwatch.StartNew();

        if (websocket.State != WebSocketState.Open)
        {
            Debug.Log("Menunggu koneksi WebSocket...");
            await websocket.Connect();
        }

        try
        {
            await websocket.SendText(message);
            timeoutCoroutine = StartCoroutine(CheckTimeout());
        }
        catch (Exception ex)
        {
            Debug.LogError("Gagal kirim ke WebSocket: " + ex.Message);
            ReturnToIdle();
        }
    }

    private void HandleIncomingText(string chunk, bool isFinal)
    {
        foreach (char c in chunk)
        {
            sentenceBuilder.Append(c);
            if (c == '.')
            {
                string sentence = sentenceBuilder.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(sentence))
                {
                    allSentences.Add(sentence);
                    sentenceQueue.Enqueue(sentence);
                    if (sentencePlayerCoroutine == null)
                        sentencePlayerCoroutine = StartCoroutine(PlaySentencesSequentially());
                }
                sentenceBuilder.Clear();
            }
        }

        if (isFinal && sentenceBuilder.Length > 0)
        {
            string leftover = sentenceBuilder.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(leftover))
            {
                allSentences.Add(leftover);
                sentenceQueue.Enqueue(leftover);
                if (sentencePlayerCoroutine == null)
                    sentencePlayerCoroutine = StartCoroutine(PlaySentencesSequentially());
            }
            sentenceBuilder.Clear();
        }
    }

    private void OnAudioStartHandler()
    {
        TriggerSpeakingAnimation();
    }

    private IEnumerator PlaySentencesSequentially()
    {
        while (sentenceQueue.Count > 0)
        {
            waitingForAudio = true;

            string sentence = sentenceQueue.Dequeue();
            Debug.Log("Sending TTS request for sentence: " + sentence);
            sentenceDisplayQueue.Enqueue(sentence);
            audioPlayer.PlayText(sentence);

            yield return new WaitUntil(() => !waitingForAudio);
        }

        sentencePlayerCoroutine = null;
        isProcessing = false;
    }

    private void TriggerSpeakingAnimation()
    {
        animationController?.SetBool("isTalking", true);
        Debug.Log("Speaking animation triggered");

        if (sentenceDisplayQueue.Count > 0)
        {
            string sentenceToDisplay = sentenceDisplayQueue.Dequeue();
            outputText.text += sentenceToDisplay + "\n";
        }
        else
        {
            Debug.LogWarning("Tidak ada kalimat yang tersedia untuk ditampilkan.");
        }
    }

    private void StopSpeakingAnimation()
    {
        animationController?.SetBool("isTalking", false);
        Debug.Log("Speaking animation stopped");
        waitingForAudio = false;
        isProcessing = false;
    }

    private IEnumerator CheckTimeout()
    {
        float startTime = Time.time;
        while (isReceivingResponse && Time.time - startTime < responseTimeout)
            yield return null;

        if (isReceivingResponse)
        {
            ReturnToIdle();
        }
    }

    private void FinalizeResponse(string finalText, bool useAudio = true)
    {
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        string clean = finalText.Trim();
        if (!string.IsNullOrEmpty(clean))
        {
            if (useAudio && audioPlayer != null)
            {
                audioPlayer.PlayText(clean);
                animationController?.SetBool("isTalking", true);
                waitingForAudio = true;
            }
            else
            {
                animationController?.SetBool("isTalking", false);
                ReturnToIdle();
            }
        }
        else
        {
            Debug.LogWarning("Respons kosong");
            ReturnToIdle();
        }
    }

    private void ReturnToIdle()
    {
        isProcessing = false;
        isReceivingResponse = false;
        waitingForAudio = false;
        animationController?.SetBool("isTalking", false);
    }

    private void HandleAudioPlaybackComplete()
    {
        Debug.Log("Kalimat selesai diputar");
        waitingForAudio = false;
        animationController?.SetBool("isTalking", false);
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
        {
            audioPlayer.OnAudioStart -= OnAudioStartHandler;
            audioPlayer.OnAudioPlaybackComplete -= HandleAudioPlaybackComplete;
        }

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
