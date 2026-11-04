using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 게임 전체 효과음과 배경 소리를 맡습니다. (씬이 바뀌어도 유지)
/// - 소리 파일: Assets/Resources/EXODStage1/Sfx/이름.wav
/// - EXODSound.Play("knock") 처럼 파일 이름으로 재생합니다.
/// - 배경 소리(앰비언스)와 심장 박동은 따로 반복 재생됩니다.
/// - 전체 음량은 시작 화면 [설정]에서 바꾸고, 다음 실행에도 기억합니다.
/// </summary>
public sealed class EXODSound : MonoBehaviour
{
    private const string ClipFolder = "EXODStage1/Sfx/";
    private const string VolumeKey = "EXOD_MasterVolume";
    private const float DefaultVolume = 0.8f;

    private static EXODSound instance;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private AudioSource sfxSource;
    private AudioSource ambienceSource;
    private AudioSource heartbeatSource;
    private Coroutine ambienceFade;
    private Coroutine heartbeatFade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedVolume()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
    }

    /// <summary>전체 음량 (0~1). 바꾸면 바로 저장됩니다.</summary>
    public static float MasterVolume
    {
        get => AudioListener.volume;
        set
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
        }
    }

    private static EXODSound Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("EXOD 사운드");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<EXODSound>();
            }

            return instance;
        }
    }

    private void Awake()
    {
        sfxSource = CreateSource(false);
        ambienceSource = CreateSource(true);
        heartbeatSource = CreateSource(true);
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    // ═══════════════════════════════════════════════════
    //  효과음
    // ═══════════════════════════════════════════════════

    /// <summary>효과음을 한 번 재생합니다.</summary>
    public static void Play(string clipName, float volume = 1f)
    {
        AudioClip clip = Instance.GetClip(clipName);
        if (clip != null)
            Instance.sfxSource.PlayOneShot(clip, volume);
    }

    /// <summary>버튼에 마우스를 올리거나 누를 때 소리가 나게 합니다.</summary>
    public static EventTrigger AttachUiSounds(GameObject target)
    {
        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = target.AddComponent<EventTrigger>();

        AddTrigger(trigger, EventTriggerType.PointerEnter, () => Play("ui_hover", 0.6f));
        AddTrigger(trigger, EventTriggerType.PointerClick, () => Play("ui_click", 0.8f));
        return trigger;
    }

    public static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    // ═══════════════════════════════════════════════════
    //  반복 소리 (앰비언스, 심장 박동)
    // ═══════════════════════════════════════════════════

    public static void PlayAmbience(string clipName, float volume, float fadeTime = 1.5f)
    {
        EXODSound sound = Instance;
        sound.StartLoop(sound.ambienceSource, ref sound.ambienceFade, clipName, volume, fadeTime);
    }

    public static void StopAmbience(float fadeTime = 1.5f)
    {
        if (instance != null)
            instance.StartLoop(instance.ambienceSource, ref instance.ambienceFade, null, 0f, fadeTime);
    }

    public static void PlayHeartbeat(float volume, float pitch = 1f, float fadeTime = 1f)
    {
        EXODSound sound = Instance;
        sound.heartbeatSource.pitch = pitch;
        sound.StartLoop(sound.heartbeatSource, ref sound.heartbeatFade, "heartbeat", volume, fadeTime);
    }

    public static void StopHeartbeat(float fadeTime = 1f)
    {
        if (instance != null)
            instance.StartLoop(instance.heartbeatSource, ref instance.heartbeatFade, null, 0f, fadeTime);
    }

    // clipName이 null이면 서서히 줄인 뒤 멈춥니다. 같은 소리가 이미 나오고 있으면 음량만 맞춥니다.
    private void StartLoop(AudioSource source, ref Coroutine fade, string clipName, float volume, float fadeTime)
    {
        if (fade != null)
            StopCoroutine(fade);

        if (clipName != null)
        {
            AudioClip clip = GetClip(clipName);
            if (clip == null)
                return;

            if (source.clip != clip || !source.isPlaying)
            {
                source.clip = clip;
                source.volume = 0f;
                source.Play();
            }
        }

        fade = StartCoroutine(FadeSource(source, volume, fadeTime, clipName == null));
    }

    private static IEnumerator FadeSource(AudioSource source, float target, float duration, bool stopAtEnd)
    {
        float start = source.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        source.volume = target;
        if (stopAtEnd)
        {
            source.Stop();
            source.clip = null;
        }
    }

    private AudioClip GetClip(string clipName)
    {
        if (clips.TryGetValue(clipName, out AudioClip clip))
            return clip;

        clip = Resources.Load<AudioClip>(ClipFolder + clipName);
        if (clip == null)
            Debug.LogWarning($"[EXODSound] 소리 파일이 없습니다: Resources/{ClipFolder}{clipName}");

        clips[clipName] = clip;
        return clip;
    }
}
