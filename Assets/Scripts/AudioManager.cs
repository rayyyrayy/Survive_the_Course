using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource bgmSource;
    public AudioClip bgmClip;

    public static AudioManager instance;
    void Awake() 
    {
        if (instance == null) 
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        } 
        else 
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        if (bgmSource == null) return;
        if (PlayerPrefs.HasKey("MusicVolume"))
            bgmSource.volume = PlayerPrefs.GetFloat("MusicVolume");
        else
            bgmSource.volume = 0.5f;
        if (bgmClip != null)
            bgmSource.clip = bgmClip;
        bgmSource.loop = true;
        if (!bgmSource.isPlaying)
            bgmSource.Play();
    }

    public void SetVolume(float volume)
    {
        if (bgmSource != null)
            bgmSource.volume = volume;
        PlayerPrefs.SetFloat("MusicVolume", volume);
    }
}
