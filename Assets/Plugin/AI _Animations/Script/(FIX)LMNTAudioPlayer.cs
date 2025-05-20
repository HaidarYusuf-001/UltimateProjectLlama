using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.Collections.Generic;

public class LMNTAudioPlayer : MonoBehaviour
{
    [Header("LMNT API Settings")]
    [SerializeField] private string apiKey = "0aff645103c7492186268222abbaaa95";
    [SerializeField] private string voiceId = "james";
    [SerializeField] private AudioSource audioSource;

    public delegate void AudioEventHandler();
    public event AudioEventHandler OnAudioStart;
    public event AudioEventHandler OnAudioPlaybackComplete;

    private Queue<AudioClip> playbackQueue = new Queue<AudioClip>();
    private bool isPlaying = false;

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            Debug.Log("AudioSource added automatically to LMNTAudioPlayer");
        }

        if (string.IsNullOrEmpty(apiKey))
            Debug.LogError("LMNT API Key not set! Please set your API key in the Inspector.");
    }

    public void PlayText(string text)
    {
        Debug.Log($"[LMNTAudioPlayer] Requesting LMNT TTS for: {text}");
        RequestAndCacheAudio(text);
    }

    public void RequestAndCacheAudio(string text)
    {
        StartCoroutine(RequestAudio(text));
    }

    private IEnumerator RequestAudio(string text)
    {
        string apiUrl = "https://api.lmnt.com/v1/ai/speech/bytes";
        string jsonBody = $"{{\"voice\": \"{voiceId}\", \"text\": \"{EscapeJson(text)}\", \"model\": \"blizzard\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(apiUrl, ""))
        {
            request.method = "POST";
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-API-Key", apiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"LMNT TTS Error: {request.responseCode} - {request.error}");
                OnAudioPlaybackComplete?.Invoke();
                yield break;
            }

            byte[] audioData = request.downloadHandler.data;

            string tempPath = Application.persistentDataPath + "/lmnt_temp.mp3";
            System.IO.File.WriteAllBytes(tempPath, audioData);
            Debug.Log("Audio file saved to: " + tempPath);

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Failed to load audio clip from file: " + www.error);
                    OnAudioPlaybackComplete?.Invoke();
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip == null)
                {
                    Debug.LogError("Clip is null after parsing file!");
                    OnAudioPlaybackComplete?.Invoke();
                    yield break;
                }

                playbackQueue.Enqueue(clip);
                if (!isPlaying)
                    StartCoroutine(PlayNextInQueue());
            }
        }
    }

    private IEnumerator PlayNextInQueue()
    {
        while (playbackQueue.Count > 0)
        {
            isPlaying = true;
            AudioClip clip = playbackQueue.Dequeue();
            audioSource.clip = clip;

            yield return new WaitUntil(() => clip.loadState == AudioDataLoadState.Loaded);

            // Trigger start event
            Debug.Log("LMNT TTS audio is playing");
            OnAudioStart?.Invoke();

            audioSource.Play();
            yield return new WaitWhile(() => audioSource.isPlaying);

            Debug.Log("LMNT TTS audio playback completed");
            OnAudioPlaybackComplete?.Invoke();
        }

        isPlaying = false;
    }

    private string EscapeJson(string str)
    {
        return string.IsNullOrEmpty(str) ? "" :
            str.Replace("\\", "\\\\").Replace("\"", "\\\"")
               .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
}
