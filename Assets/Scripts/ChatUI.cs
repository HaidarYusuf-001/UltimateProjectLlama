using UnityEngine;
using TMPro;
using System.Collections;

public class ChatUI : MonoBehaviour
{
    public TMP_InputField inputField;
    public TMP_Text outputText;
    private OllamaChat ollamaChat;

    void Start()
    {
        ollamaChat = FindObjectOfType<OllamaChat>();
        outputText.gameObject.SetActive(true);
        outputText.color = Color.black;
    }

    void UpdateChat(string message)
    {
        StartCoroutine(UpdateUIText(message));
    }

    IEnumerator UpdateUIText(string message)
    {
        yield return new WaitForEndOfFrame();
        outputText.text = message;
    }

    public void HandleFinalResponse(string response)
    {
        
        UpdateChat(response);
    }

    public void SendMessage()
    {
        string userInput = inputField.text;
        if (!string.IsNullOrEmpty(userInput))
        {
            ollamaChat.SendMessageToOllama(userInput);
        }
    }
}
