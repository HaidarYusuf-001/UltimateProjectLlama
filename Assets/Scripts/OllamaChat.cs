using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class OllamaChat : MonoBehaviour
{
    private const string apiUrl = "http://localhost:11434/api/generate";
    private const string modelName = "sampri-custom:latest";

    public void SendMessageToOllama(string userInput)
    {
        StartCoroutine(SendRequest(userInput));
    }

    private IEnumerator SendRequest(string userInput)
    {
        string jsonBody = "{\"model\":\"" + modelName + "\",\"prompt\":\"" + userInput + "\",\"stream\":true}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                StartCoroutine(HandleStreamingResponse(request.downloadHandler.text));
            }
            else
            {
                Debug.LogError("Error: " + request.error);
            }
        }
    }

    private IEnumerator HandleStreamingResponse(string responseText)
    {
        StringBuilder fullResponse = new StringBuilder();
        string[] responseLines = responseText.Split('\n');

        foreach (string line in responseLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                OllamaResponse responseChunk = JsonUtility.FromJson<OllamaResponse>(line);
                fullResponse.Append(responseChunk.response);

                if (responseChunk.done)
                {
                    break;
                }
            }
            catch
            {
                Debug.LogWarning("Gagal parse JSON: " + line);
            }

            yield return null;
        }

        Debug.Log("Final Response: " + fullResponse.ToString());

        ChatUI chatUI = FindObjectOfType<ChatUI>();
        if (chatUI != null)
        {
            chatUI.HandleFinalResponse(fullResponse.ToString());
        }
        else
        {
            Debug.LogError("ChatUI tidak ditemukan di scene!");
        }
    }

    [System.Serializable]
    private class OllamaResponse
    {
        public string response;
        public bool done;
    }
}
