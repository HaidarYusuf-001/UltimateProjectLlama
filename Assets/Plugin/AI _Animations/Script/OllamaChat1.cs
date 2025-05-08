using System;
using System.Collections;
using System.Text;
using UnityEngine;
using NativeWebSocket;
using Debug = UnityEngine.Debug;
using System.Diagnostics;

public class OllamaChat : MonoBehaviour
{
    [Header("WebSocket Settings")]
    [SerializeField] private string websocketUrl = "ws://localhost:8765";
    [SerializeField] private float responseTimeout = 30f;

    [Header("Components")]
    [SerializeField] private LMNTAudioPlayer audioPlayer;
    [SerializeField] private Animator animationController;  // Perubahan ke Animator

    private WebSocket websocket;
    private Stopwatch stopwatch;
    private StringBuilder fullResponse;
    private bool isProcessing = false;

    void Start()
    {
        if (audioPlayer != null)
        {
            audioPlayer.OnAudioPlaybackComplete += HandleAudioPlaybackComplete;
        }
    }

    void OnDestroy()
    {
        if (audioPlayer != null)
        {
            audioPlayer.OnAudioPlaybackComplete -= HandleAudioPlaybackComplete;
        }

        CloseWebSocket();
    }

    private void HandleAudioPlaybackComplete()
    {
        animationController?.SetTrigger("Idle");
        Debug.Log("Animation: Returning to idle animation after audio playback");
    }

    public async void SendMessageToOllama(string userInput)
    {
        if (string.IsNullOrEmpty(userInput))
        {
            Debug.LogWarning("User input kosong, tidak ada yang dikirim");
            return;
        }

        if (isProcessing)
        {
            Debug.LogWarning("Masih memproses permintaan sebelumnya");
            return;
        }

        isProcessing = true;
        animationController?.SetTrigger("Talk");

        stopwatch = Stopwatch.StartNew();
        fullResponse = new StringBuilder();

        CloseWebSocket();
        websocket = new WebSocket(websocketUrl);

        websocket.OnOpen += () =>
        {
            Debug.Log("WebSocket opened");
            websocket.SendText(userInput);
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError("WebSocket Error: " + e);
            animationController?.SetTrigger("Idle");
            isProcessing = false;
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("WebSocket closed with code: " + e);
            if (isProcessing && fullResponse.Length == 0)
            {
                animationController?.SetTrigger("Idle");
                isProcessing = false;
            }
        };

        websocket.OnMessage += (bytes) =>
        {
            string msg = Encoding.UTF8.GetString(bytes);
            try
            {
                OllamaResponse chunk = JsonUtility.FromJson<OllamaResponse>(msg);
                fullResponse.Append(chunk.response);
                UpdateUIIncrementally(fullResponse.ToString());

                if (chunk.done)
                {
                    FinalizeResponse();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Gagal parse JSON: " + msg + "\nError: " + ex.Message);
            }
        };

        try
        {
            await websocket.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to connect to WebSocket: " + ex.Message);
            animationController?.SetTrigger("Idle");
            isProcessing = false;
        }

        // Implement timeout
        StartCoroutine(CheckTimeout());
    }

    private IEnumerator CheckTimeout()
    {
        float startTime = Time.time;

        while (isProcessing && Time.time - startTime < responseTimeout)
        {
            yield return null;
        }

        if (isProcessing && Time.time - startTime >= responseTimeout)
        {
            Debug.LogError("Request timeout after " + responseTimeout + " seconds");
            CloseWebSocket();
            animationController?.SetTrigger("Idle");
            isProcessing = false;
        }
    }

    private void CloseWebSocket()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            try
            {
                websocket.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError("Error closing WebSocket: " + ex.Message);
            }
        }
    }

    private void FinalizeResponse()
    {
        string finalText = fullResponse.ToString().Trim();
        Debug.Log("Final Response: " + finalText);

        if (!string.IsNullOrEmpty(finalText))
        {
            animationController?.SetTrigger("Talk");

            if (audioPlayer != null)
            {
                try
                {
                    audioPlayer.PlayText(finalText);
                    Debug.Log("Audio: Playing speech from text");
                }
                catch (Exception ex)
                {
                    Debug.LogError("TTS Error: " + ex.Message);
                    animationController?.SetTrigger("Idle");
                }
            }
            else
            {
                Debug.LogError("LMNTAudioPlayer tidak ditemukan!");
                animationController?.SetTrigger("Idle");
            }

            UpdateUIWithFinalResponse(finalText);
        }
        else
        {
            Debug.LogWarning("Response kosong dari Ollama");
            animationController?.SetTrigger("Idle");
        }

        stopwatch.Stop();
        float latency = (float)stopwatch.Elapsed.TotalSeconds;
        Debug.Log("Total Latency: " + latency + " seconds");

        CloseWebSocket();
        isProcessing = false;
    }

    private void UpdateUIIncrementally(string currentText)
    {
        ChatUI chatUI = FindAnyObjectByType<ChatUI>();
        if (chatUI != null)
        {
            try
            {
                var method = chatUI.GetType().GetMethod("UpdateResponseInProgress");
                method?.Invoke(chatUI, new object[] { currentText });
            }
            catch { }
        }
    }

    private void UpdateUIWithFinalResponse(string finalText)
    {
        ChatUI chatUI = FindAnyObjectByType<ChatUI>();
        if (chatUI != null)
        {
            try
            {
                chatUI.HandleFinalResponse(finalText);
            }
            catch (Exception e)
            {
                Debug.LogError("Error updating UI: " + e.Message);
            }
        }
    }

    [System.Serializable]
    private class OllamaResponse
    {
        public string response;
        public bool done;
    }
}
