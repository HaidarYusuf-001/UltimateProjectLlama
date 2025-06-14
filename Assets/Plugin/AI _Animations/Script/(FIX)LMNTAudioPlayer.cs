using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.Collections.Generic;

public class LMNTAudioPlayer : MonoBehaviour
{
    [Header("LMNT API Settings")]
    [SerializeField] private string apiKey = "YOUR_LMNT_API_KEY"; // Ganti dengan API Key Anda
    [SerializeField] private string voiceId = "james";
    [SerializeField] private AudioSource audioSource;

    public delegate void AudioEventHandler();
    public event AudioEventHandler OnAudioStart;
    public event AudioEventHandler OnAudioPlaybackComplete; // Event ini sekarang punya arti baru

    private Queue<AudioClip> playbackQueue = new Queue<AudioClip>();
    private bool isPlaying = false;

    // Properti ini tetap berguna untuk skrip lain jika diperlukan, jadi kita biarkan saja.
    public bool HasQueuedAudio => playbackQueue.Count > 0;

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }
        if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_LMNT_API_KEY")
            Debug.LogError("LMNT API Key not set!");
    }

    public void PlayText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        StartCoroutine(RequestAndQueueAudio(text));
    }

    private IEnumerator RequestAndQueueAudio(string text)
    {
        // ... (Fungsi ini tidak perlu diubah, tugasnya hanya download dan enqueue)
        // Untuk singkatnya, kode request ke API tidak ditampilkan lagi karena sama.
        // Anggap saja setelah sukses, dia akan memanggil:
        // playbackQueue.Enqueue(clip);
        // if (!isPlaying) { StartCoroutine(PlayNextInQueue()); }

        string apiUrl = "https://api.lmnt.com/v1/ai/speech/bytes";
        string jsonBody = $"{{\"voice\": \"{voiceId}\", \"text\": \"{EscapeJson(text)}\", \"model\": \"blizzard\", \"language\": \"en\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-API-Key", apiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"LMNT TTS Error: {request.responseCode} - {request.error}");
                yield break;
            }

            byte[] audioData = request.downloadHandler.data;

            // Proses konversi dari byte[] ke AudioClip
            string tempPath = Application.persistentDataPath + "/" + System.Guid.NewGuid().ToString() + ".mp3";
            System.IO.File.WriteAllBytes(tempPath, audioData);

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
            {
                yield return www.SendWebRequest();
                System.IO.File.Delete(tempPath);

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Failed to load audio clip from file: " + www.error);
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    playbackQueue.Enqueue(clip);
                    Debug.Log($"Audio clip ready and enqueued. Queue size: {playbackQueue.Count}");

                    if (!isPlaying)
                    {
                        StartCoroutine(PlayNextInQueue());
                    }
                }
            }
        }
    }

    // =======================================================================
    // == PERUBAHAN UTAMA DI SINI ==
    // =======================================================================
    private IEnumerator PlayNextInQueue()
    {
        isPlaying = true;

        // Terus berputar selama masih ada audio di dalam antrian
        while (playbackQueue.Count > 0)
        {
            AudioClip clip = playbackQueue.Dequeue();
            audioSource.clip = clip;

            yield return new WaitUntil(() => clip.loadState == AudioDataLoadState.Loaded);

            // OnAudioStart dipicu untuk SETIAP kalimat, ini yang akan menampilkan teks
            // dan memastikan animasi 'isTalking' tetap true.
            OnAudioStart?.Invoke();

            audioSource.Play();

            // Tunggu sampai klip ini selesai
            yield return new WaitWhile(() => audioSource.isPlaying);

            Destroy(clip);
        }

        // Setelah loop selesai (artinya antrian kosong), baru kita beri tahu bahwa
        // RANGKAIAN pemutaran audio telah selesai.
        isPlaying = false;
        Debug.Log("Playback queue is empty. Firing OnAudioPlaybackComplete.");
        OnAudioPlaybackComplete?.Invoke();
    }

    private string EscapeJson(string str)
    {
        return string.IsNullOrEmpty(str) ? "" : str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
}