using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine.EventSystems;
using TMPro;

public class SpeechToText : MonoBehaviour
{
    public Button talkButton; // Tidak digunakan lagi untuk onClick
    public TMP_InputField resultInputField;
    public Text buttonText;
    public OllamaUnifiedClient ollamaClient;


    private AudioClip audioClip;
    private string micDevice;
    private const int sampleRate = 16000;
    private const string serverIp = "127.0.0.1";
    private const int serverPort = 5000;

    private bool isHolding = false;

    void Start()
    {
        micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;

        if (micDevice == null)
        {
            Debug.LogError("No microphone found!");
            talkButton.interactable = false;
        }

        // Pastikan teks button menunjukkan instruksi hold
        buttonText.text = "Hold to Talk";
    }

    public void StartRecordingFromUI()
    {
        if (!isHolding)
        {
            isHolding = true;
            StartRecording();
            buttonText.text = "Listening...";
        }
    }

    public void StopRecordingAndSendFromUI()
    {
        if (isHolding)
        {
            isHolding = false;
            StopRecordingAndSend();
            buttonText.text = "Hold to Talk";
        }
    }


    void StartRecording()
    {
        audioClip = Microphone.Start(micDevice, false, 5, sampleRate);
        Debug.Log("Recording started...");
    }

    async void StopRecordingAndSend()
    {
        Microphone.End(micDevice);
        Debug.Log("Recording stopped.");

        byte[] wavData = ConvertClipToWav(audioClip);
        resultInputField.text = "Processing...";

        string transcription = await SendToWhisperAsync(wavData);
        string correctedTranscription = SpeechPostProcessing.CorrectNames(transcription);

        resultInputField.text = correctedTranscription;
        ollamaClient.OnSendButtonPressed();

        Debug.Log("Transcription: " + transcription);
        Debug.Log("Setelah Post-Processing: " + correctedTranscription);
    }

    byte[] ConvertClipToWav(AudioClip clip)
    {
        float[] samples = new float[clip.samples];
        clip.GetData(samples, 0);

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + samples.Length * 2); // PCM 16bit
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16); // Subchunk1Size
            writer.Write((short)1); // PCM format
            writer.Write((short)1); // Mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2); // Byte rate
            writer.Write((short)2); // Block align
            writer.Write((short)16); // Bits per sample
            writer.Write("data".ToCharArray());
            writer.Write(samples.Length * 2);

            foreach (float sample in samples)
            {
                short pcmSample = (short)(sample * 32767);
                writer.Write(pcmSample);
            }

            return stream.ToArray();
        }
    }

    async Task<string> SendToWhisperAsync(byte[] wavData)
    {
        try
        {
            using (var client = new TcpClient(serverIp, serverPort))
            using (var stream = client.GetStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(wavData.Length);
                writer.Write(wavData);
                writer.Flush();

                using (var reader = new StreamReader(stream))
                {
                    string response = await reader.ReadLineAsync();
                    return response ?? "No response from server.";
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error sending audio: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }
}
