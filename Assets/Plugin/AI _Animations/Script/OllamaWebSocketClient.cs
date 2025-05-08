using System;
using System.Text;
using UnityEngine;
using NativeWebSocket;
using TMPro;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

public class OllamaWebSocketClient : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField inputField;
    public TMP_Text outputText;

    [Header("External Components")]
    [SerializeField] private LMNTAudioPlayer audioPlayer;
    [SerializeField] private Animator animationController;

    private WebSocket websocket;
    private StringBuilder currentResponse = new StringBuilder();
    private Stopwatch stopwatch;
    private Dictionary<string, string> responseCache = new Dictionary<string, string>();
    private bool isProcessing = false;

    async void Start()
    {
        await InitializeWebSocket();

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
        // Pastikan parameter "Idle" ada dalam Animator
        if (animationController != null)
        {
            animationController.SetTrigger("Idle"); // Cek apakah parameter "Idle" ada
        }
        UnityEngine.Debug.Log("Animation: Returning to idle animation after audio playback");
    }

    private async Task InitializeWebSocket()
    {
        websocket = new WebSocket("ws://localhost:8765");

        websocket.OnOpen += () => UnityEngine.Debug.Log("WebSocket connected");

        websocket.OnError += (e) =>
        {
            UnityEngine.Debug.LogError("WebSocket error: " + e);
            isProcessing = false;
        };

        websocket.OnClose += (e) =>
        {
            UnityEngine.Debug.Log("WebSocket closed");
            if (isProcessing && currentResponse.Length == 0)
            {
                UnityEngine.Debug.Log("Empty response, going idle...");
                if (animationController != null)
                {
                    animationController.SetTrigger("Idle");
                }
                isProcessing = false;
            }
        };

        websocket.OnMessage += (bytes) =>
        {
            string message = Encoding.UTF8.GetString(bytes);
            UnityEngine.Debug.Log("Message received: " + message);

            if (string.IsNullOrWhiteSpace(message) || message == "[DONE]")
            {
                UnityEngine.Debug.LogWarning("Received empty or invalid message: " + message);
                return;
            }

            try
            {
                OllamaResponse chunk = JsonUtility.FromJson<OllamaResponse>(message);
                UnityEngine.Debug.Log("Parsed chunk: " + chunk.response);

                if (chunk != null && !string.IsNullOrEmpty(chunk.response))
                {
                    currentResponse.Append(chunk.response);
                    UnityEngine.Debug.Log("Updated current response: " + currentResponse.ToString());
                    outputText.text = currentResponse.ToString();
                    UpdateUIIncrementally(currentResponse.ToString());

                    if (chunk.done)
                    {
                        FinalizeResponse();
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("Failed to parse JSON: " + message + "\n" + ex.Message);
            }
        };

        await websocket.Connect();
    }

    public async void SendMessageToServer()
    {
        UnityEngine.Debug.Log("SendMessageToServer CALLED");

        string userInput = inputField.text;
        if (string.IsNullOrEmpty(userInput)) return;

        if (responseCache.ContainsKey(userInput))
        {
            outputText.text = responseCache[userInput];
            UnityEngine.Debug.Log("Response from cache: " + responseCache[userInput]);
            return;
        }

        if (isProcessing) return;

        isProcessing = true;

        currentResponse.Clear();
        outputText.text = "";

        stopwatch = Stopwatch.StartNew();

        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            await InitializeWebSocket();
        }

        await websocket.SendText(userInput);
    }

    private void FinalizeResponse()
    {
        string finalText = currentResponse.ToString().Trim();
        UnityEngine.Debug.Log("Final response text: " + finalText);

        if (!string.IsNullOrEmpty(finalText))
        {
            if (audioPlayer != null)
            {
                audioPlayer.PlayText(finalText);
            }

            if (animationController != null)
            {
                animationController.SetTrigger("TalkBool");
            }

            responseCache[inputField.text] = finalText;
        }
        else
        {
            UnityEngine.Debug.LogWarning("No final response to display.");
        }
    }

    private void CloseWebSocket()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            websocket.Close();
        }
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
                UnityEngine.Debug.LogError("Error updating UI: " + e.Message);
            }
        }
    }

    [System.Serializable]
    public class OllamaResponse
    {
        public string response;
        public bool done;
    }
}
