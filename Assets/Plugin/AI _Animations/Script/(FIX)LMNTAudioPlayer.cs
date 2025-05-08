using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class LMNTAudioPlayer : MonoBehaviour
{
    [Header("LMNT API Settings")]
    [SerializeField] private string apiKey = "eced998660014e82821eeb392d3cce0b";
    [SerializeField] private string voiceId = "james";
    [SerializeField] private AudioSource audioSource;

    public delegate void AudioPlaybackCompleteHandler();
    public event AudioPlaybackCompleteHandler OnAudioPlaybackComplete;

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                Debug.Log("AudioSource added automatically to LMNTAudioPlayer");
            }
        }

        if (apiKey == "YOUR_API_KEY_HERE" || string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("LMNT API Key not set! Please set your API key in the Inspector.");
        }
    }

    public void PlayText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("Cannot play empty text");
            OnAudioPlaybackComplete?.Invoke();
            return;
        }

        StartCoroutine(RequestAndPlayAudio(text));
    }

    private IEnumerator RequestAndPlayAudio(string text)
    {
        string apiUrl = "https://api.lmnt.com/v1/ai/speech/bytes";
        string jsonBody = $"{{\"voice\": \"{voiceId}\", \"text\": \"{EscapeJson(text)}\", \"model\": \"blizzard\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);

            // TEMP: Pakai buffer biar bisa debug respon error
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.downloadHandler = new DownloadHandlerAudioClip(apiUrl, AudioType.MPEG);
            request.SetRequestHeader("X-API-Key", apiKey);

            Debug.Log("Sending TTS request to LMNT API");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log(" Request sukses tapi belum putar audio karena ini mode debug.");
            }
            else
            {
                Debug.LogError($"LMNT TTS Error: {request.responseCode} - {request.error}");

                // Ini aman karena pakai DownloadHandlerBuffer
                Debug.LogError("Response Text: " + request.downloadHandler.text);
            }

            OnAudioPlaybackComplete?.Invoke();

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip != null)
            {
                Debug.Log($"Clip loaded: {clip.length} seconds");
                audioSource.clip = clip;
                audioSource.Play();
                Debug.Log("LMNT TTS audio is playing");

                yield return new WaitWhile(() => audioSource.isPlaying);
                Debug.Log("LMNT TTS audio playback completed");
                OnAudioPlaybackComplete?.Invoke();
            }
            else
            {
                Debug.LogError("Clip is null! Mungkin format audio gak cocok.");
                OnAudioPlaybackComplete?.Invoke();
            }

        }
    }


    private string EscapeJson(string str)
    {
        if (string.IsNullOrEmpty(str))
            return "";

        return str
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
