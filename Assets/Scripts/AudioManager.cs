using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Karakter Sesleri")]
    public AudioClip footstepWalk;
    public AudioClip footstepRun;

    [Header("Kapı / Soru Sesleri")]
    public AudioClip doorOpen;
    public AudioClip correctAnswer;
    public AudioClip wrongAnswer;
    public AudioClip paperOpen;
    public AudioClip drawerOpen; // Çekmece açılma sesi eklendi

    private AudioSource sfxSource;
    private AudioSource footstepSource;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.loop = false;
    }

    // Oyun durduğunda (soru veya ipucu açıkken) çalan ayak sesini anında susturur
    void Update()
    {
        if (Time.timeScale == 0)
        {
            StopFootstep();
        }
    }

    // Normal kısa sesler için
    public void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    // KAPI SESİ GÜNCELLEMESİ: Sesin üst üste binip uzamasını engeller
    public void PlayDoorOpen()
    {
        if (doorOpen == null || sfxSource == null) return;
        sfxSource.Stop();
        sfxSource.clip = doorOpen; 
        sfxSource.Play();
    }

    public void PlayFootstep(bool isRunning)
    {
        // Panel açıkken veya oyun duraklatıldığında yeni ayak sesi çalınmasını engeller
        if (Time.timeScale == 0) return;

        if (footstepSource == null) return;
        AudioClip clip = isRunning ? footstepRun : footstepWalk;
        if (clip == null || footstepSource.isPlaying) return;

        footstepSource.Stop();
        footstepSource.clip = clip;
        footstepSource.volume = isRunning ? 0.8f : 0.5f;
        footstepSource.Play();
    }

    public void StopFootstep()
    {
        if (footstepSource != null && footstepSource.isPlaying)
            footstepSource.Stop();
    }

    public void PlayCorrect()    => Play(correctAnswer, 1f);
    public void PlayWrong()      => Play(wrongAnswer,   1f);
    public void PlayPaperOpen()  => Play(paperOpen,     1f);
    public void PlayDrawerOpen() => Play(drawerOpen,    1f); // Çekmece sesi tetikleyicisi
}