using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Karakter Sesleri")]
    public AudioClip footstepWalk;
    public AudioClip footstepRun;
    public AudioClip hurtSound;
    public AudioClip deathSound;

    [Header("Eşya Sesleri")]
    public AudioClip pickupSound;
    public AudioClip bandageWrap;
    public AudioClip bandageRip;
    public AudioClip canOpen;
    public AudioClip canSwallow;

    [Header("Kapı / Soru Sesleri")]
    public AudioClip doorOpen;
    public AudioClip correctAnswer;
    public AudioClip wrongAnswer;
    public AudioClip paperOpen;

    private AudioSource sfxSource;
    private AudioSource footstepSource;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        sfxSource             = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        footstepSource             = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.loop        = false;
    }

    public void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayFootstep(bool isRunning)
    {
        if (footstepSource == null) return;
        AudioClip clip = isRunning ? footstepRun : footstepWalk;
        if (clip == null || footstepSource.isPlaying) return;

        footstepSource.Stop();
        footstepSource.clip   = clip;
        footstepSource.volume = isRunning ? 0.8f : 0.5f;
        footstepSource.Play();
    }

    public void StopFootstep()
    {
        if (footstepSource != null && footstepSource.isPlaying)
            footstepSource.Stop();
    }

    public void PlayBandage()
    {
        Play(bandageRip, 0.8f);
        float delay = bandageRip != null ? bandageRip.length : 0.3f;
        Invoke(nameof(PlayBandageWrap), delay);
    }

    void PlayBandageWrap() => Play(bandageWrap, 0.8f);

    public void PlayEnergyDrink()
    {
        Play(canOpen, 0.9f);
        float delay = canOpen != null ? canOpen.length : 0.5f;
        Invoke(nameof(PlaySwallow), delay);
    }

    void PlaySwallow() => Play(canSwallow, 0.9f);

    public void PlayHurt()      => Play(hurtSound,     0.9f);
    public void PlayDeath()     => Play(deathSound,    1f);
    public void PlayPickup()    => Play(pickupSound,   0.7f);
    public void PlayDoorOpen()  => Play(doorOpen,      1f);
    public void PlayCorrect()   => Play(correctAnswer, 1f);
    public void PlayWrong()     => Play(wrongAnswer,   1f);
    public void PlayPaperOpen() => Play(paperOpen,     1f);
}