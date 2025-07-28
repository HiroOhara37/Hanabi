using UnityEngine;

public class AudioPlayer : MonoBehaviour
{
    public static AudioPlayer Instance; // シングルトン化（どこからでも呼べる）

    private AudioSource audioSource;
    public AudioClip finishPlayng; //   手番終了の効果音

    private void Awake()
    {
        // シングルトン
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
    }

    // 効果音を鳴らす
    public void PlaySE(AudioClip clip)
    {
        if (clip != null)
            audioSource.PlayOneShot(clip);
    }
}
