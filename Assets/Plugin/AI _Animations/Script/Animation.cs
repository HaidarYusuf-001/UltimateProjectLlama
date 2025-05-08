using UnityEngine;

public class Animation : MonoBehaviour
{
    private Animator animator;
    private bool isTalking = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator tidak ditemukan di GameObject ini atau child objects");
            }
            else
            {
                Debug.Log("Animator ditemukan di child object");
            }
        }

        // Default to idle animation at start
        PlayIdleAnimation();
    }

    public void PlayWaveAnimation()
    {
        if (animator == null) return;

        Debug.Log("Playing wave animation");
        animator.SetTrigger("WaveTrigger");

        // Auto transition to idle after wave
        CancelInvoke();
        Invoke("PlayIdleAnimation", 2f);
    }

    public void PlayIdleAnimation()
    {
        if (animator == null) return;

        Debug.Log("Playing idle animation");
        animator.SetTrigger("IdleBool");
        isTalking = false;
    }

    public void PlayTalkingAnimation()
    {
        if (animator == null) return;

        // Cancel any pending transitions to idle
        CancelInvoke();

        Debug.Log("Playing talking animation");
        animator.SetTrigger("TalkingBool");
        isTalking = true;

        // Don't auto-transition to idle when talking animation starts
        // Will be transitioned when audio playback completes via OllamaChat
    }

    public void ReturnToIdle()
    {
        if (animator == null) return;

        if (isTalking)
        {
            Debug.Log("Returning to idle from talking");
            PlayIdleAnimation();
        }
    }

    // Utility method to check if currently talking
    public bool IsTalking()
    {
        return isTalking;
    }
}