using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1스테이지(침실 씬)의 효과음과 화면 효과를 맡습니다.
/// - 배경: 낮게 웅웅거리는 앰비언스를 계속 깝니다.
/// - 대사: 글자가 찍힐 때 "틱" 소리 (여자친구는 낮은 소리),
///         "똑", "철컥", "꿀꺽"처럼 소리를 묘사하는 대사에 맞는 효과음
/// - 장소 이동: 발소리 / 조사 창 열기: 부스럭
/// - 하트(HP)가 줄면: 깨지는 소리 + 화면 가장자리가 붉게 번쩍임
/// </summary>
[DefaultExecutionOrder(250)]
public sealed class Stage1SoundEffects : MonoBehaviour
{
    // 대사에 이 글자가 있으면 해당 효과음을 재생합니다. (위에서부터 먼저 찾은 하나만)
    private static readonly string[,] NarrationSounds =
    {
        { "똑", "knock" },
        { "꿀꺽", "gulp" },
        { "삐용", "alarm" },
        { "삐리릭", "doorlock_beep" },
        { "끼익", "door_creak" },
        { "철컥", "door_lock" },
    };

    [Header("=== 음량 ===")]
    [SerializeField] private float ambienceVolume = 0.45f;
    [SerializeField] private float typingVolume = 0.35f;

    [Header("=== HP 감소 효과 ===")]
    [SerializeField] private Color damageColor = new Color(0.75f, 0f, 0.05f, 0.85f);
    [SerializeField] private float damageFlashDuration = 0.9f;

    private HeartManager hearts;
    private int lastHp = -1;
    private bool wasInspecting;
    private bool girlfriendSpeaking;
    private int typedCount;
    private Image damageVignette;
    private Coroutine damageCoroutine;

    private void Start()
    {
        EXODSound.StopHeartbeat();
        EXODSound.PlayAmbience("ambience_drone", ambienceVolume, 3f);

        hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
        if (hearts != null)
            lastHp = hearts.HP;

        DialogueManager dm = DialogueManager.Instance;
        if (dm != null)
        {
            dm.LineShown += OnLineShown;
            dm.CharacterTyped += OnCharacterTyped;
        }

        if (SliderManager.Instance != null)
            SliderManager.Instance.RoomChanged += OnRoomChanged;

        BuildDamageVignette();
    }

    private void OnDestroy()
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm != null)
        {
            dm.LineShown -= OnLineShown;
            dm.CharacterTyped -= OnCharacterTyped;
        }

        if (SliderManager.Instance != null)
            SliderManager.Instance.RoomChanged -= OnRoomChanged;
    }

    private void Update()
    {
        bool isInspecting = InspectionManager.Instance != null && InspectionManager.Instance.IsOpen;
        if (isInspecting && !wasInspecting)
            EXODSound.Play("inspect");
        wasInspecting = isInspecting;

        if (hearts != null)
        {
            if (lastHp >= 0 && hearts.HP < lastHp)
                OnHpLost();
            lastHp = hearts.HP;
        }
    }

    // ═══════════════════════════════════════════════════
    //  대사
    // ═══════════════════════════════════════════════════

    private void OnLineShown(DialogueLine line)
    {
        string speaker = line.speakerName != null ? line.speakerName.Trim() : string.Empty;
        girlfriendSpeaking = speaker == Stage1Dialogues.Girlfriend || speaker == Stage1Dialogues.Unknown
            || speaker == "얀데레" || speaker == "여주";
        typedCount = 0;

        // 이름 없이 나오는 설명 줄에서만 소리를 찾습니다. (등장인물 대사 속 단어는 무시)
        if (!string.IsNullOrEmpty(speaker) || string.IsNullOrEmpty(line.text))
            return;

        for (int i = 0; i < NarrationSounds.GetLength(0); i++)
        {
            if (line.text.Contains(NarrationSounds[i, 0]))
            {
                EXODSound.Play(NarrationSounds[i, 1]);
                return;
            }
        }
    }

    private void OnCharacterTyped(char c)
    {
        if (char.IsWhiteSpace(c))
            return;

        // 두 글자마다 한 번씩 울려 너무 시끄럽지 않게 합니다.
        if (typedCount++ % 2 == 0)
            EXODSound.Play(girlfriendSpeaking ? "type_tick_low" : "type_tick", typingVolume);
    }

    private void OnRoomChanged(string roomName)
    {
        EXODSound.Play("footsteps", 0.8f);
    }

    // ═══════════════════════════════════════════════════
    //  HP 감소
    // ═══════════════════════════════════════════════════

    private void OnHpLost()
    {
        EXODSound.Play("hp_lost");

        if (damageVignette == null)
            return;

        if (damageCoroutine != null)
            StopCoroutine(damageCoroutine);
        damageCoroutine = StartCoroutine(FlashDamage());
    }

    private IEnumerator FlashDamage()
    {
        damageVignette.gameObject.SetActive(true);

        // 두 번 맥박치듯 번쩍인 뒤 사라집니다.
        float elapsed = 0f;
        while (elapsed < damageFlashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / damageFlashDuration);
            float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * (1f - t);
            Color color = damageColor;
            color.a *= pulse;
            damageVignette.color = color;
            yield return null;
        }

        damageVignette.gameObject.SetActive(false);
        damageCoroutine = null;
    }

    private void BuildDamageVignette()
    {
        GameObject root = new GameObject("HP 감소 효과", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;  // 기절·게임 오버 화면(1000) 바로 아래

        GameObject go = new GameObject("붉은 가장자리", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(root.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        damageVignette = go.GetComponent<Image>();
        damageVignette.sprite = CreateVignetteSprite();
        damageVignette.raycastTarget = false;
        go.SetActive(false);
    }

    // 가운데는 투명하고 가장자리로 갈수록 진해지는 그림을 만듭니다.
    private static Sprite CreateVignetteSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
