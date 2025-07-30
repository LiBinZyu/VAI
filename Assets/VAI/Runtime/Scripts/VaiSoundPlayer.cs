using UnityEngine;

public class VaiSoundPlayer : MonoBehaviour
{
    [Header("One-Shot Sounds")]
    public AudioClip functionCalledSound;
    public AudioClip invalidSound;
    public AudioClip listeningForCommandSound;
    public AudioClip idleSound;

    [Header("Looping Sounds")]
    public AudioClip processingCommandLoop;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }


    public void PlayFunctionCalledSound()
    {
        if (functionCalledSound != null)
        {
            // PlayOneShot适合播放一次性音效，不会打断其他声音
            audioSource.PlayOneShot(functionCalledSound);
        }
    }

    public void PlayInvalidSound()
    {
        if (invalidSound != null)
        {
            audioSource.PlayOneShot(invalidSound);
        }
    }

    public void PlayIdleSound()
    {
        if (invalidSound != null)
        {
            audioSource.PlayOneShot(idleSound);
        }
    }

    public void PlayListeningSound()
    {
        if (listeningForCommandSound != null)
        {
            audioSource.PlayOneShot(listeningForCommandSound);
        }
    }

    public void StartProcessingSound()
    {
        if (processingCommandLoop != null)
        {
            audioSource.clip = processingCommandLoop;
            audioSource.loop = true; // 设置为循环
            audioSource.Play();
        }
    }

    public void StopProcessingSound()
    {
        // 只停止循环音效，不影响可能在播放的一次性音效
        if (audioSource.clip == processingCommandLoop && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
        }
    }
}
