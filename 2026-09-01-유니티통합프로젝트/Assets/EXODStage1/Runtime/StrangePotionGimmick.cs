using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 거실 '이상한 물약' 기믹 — 머릿속 생각 연타로 날리기
/// - 물약을 마시면 플레이어의 머릿속 화면이 뜨고, '탈출 하지 말까' 생각이 하나둘 떠오릅니다.
/// - 생각을 여러 번 클릭(연타)하면 흔들리다가 날아가 사라집니다.
/// - 제한 시간 안에 모든 생각을 날리면 성공, 시간이 다 되면 실패입니다.
/// - 결과(성공 true / 실패 false)는 Begin()에 넘긴 onFinished로 알려 줍니다.
///   대사·하트 처리는 Stage1StoryController가 맡습니다.
/// 난이도는 아래 Inspector 값(제한 시간, 생각 개수, 연타 횟수)으로 조절합니다.
/// </summary>
public sealed class StrangePotionGimmick : MonoBehaviour
{
    public static StrangePotionGimmick Instance { get; private set; }

    /// <summary>머릿속 화면이 떠 있는지 여부 (이때는 일차 시간이 멈춥니다)</summary>
    public static bool IsOpen => Instance != null && Instance.isRunning;

    [Header("=== 난이도 ===")]
    [Tooltip("제한 시간(초)")]
    public float timeLimit = 15f;

    [Tooltip("날려야 하는 생각의 총 개수")]
    public int totalThoughts = 8;

    [Tooltip("생각 하나를 날리는 데 필요한 클릭 수")]
    public int hitsPerThought = 5;

    [Tooltip("시작할 때 바로 떠 있는 생각 수")]
    public int startThoughts = 3;

    [Tooltip("화면에 동시에 떠 있을 수 있는 최대 생각 수")]
    public int maxOnScreen = 5;

    [Tooltip("새 생각이 떠오르는 간격(초)")]
    public float spawnInterval = 1.1f;

    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private static readonly Vector2 HeadSize = new Vector2(1200f, 720f);
    private static readonly Vector2 BubbleSize = new Vector2(330f, 110f);

    private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.78f);
    private static readonly Color HeadColor = new Color(0.16f, 0.07f, 0.14f, 0.96f);
    private static readonly Color HeadBorderColor = new Color(0.62f, 0.24f, 0.42f, 1f);
    private static readonly Color BubbleColor = new Color(0.96f, 0.80f, 0.88f, 1f);
    private static readonly Color BubbleHitColor = new Color(1f, 0.45f, 0.55f, 1f);
    private static readonly Color BubbleTextColor = new Color(0.30f, 0.06f, 0.16f, 1f);
    private static readonly Color TimerColor = new Color(0.95f, 0.55f, 0.70f, 1f);
    private static readonly Color TimerLowColor = new Color(1f, 0.25f, 0.30f, 1f);

    private sealed class Thought
    {
        public RectTransform rect;
        public Image image;
        public TextMeshProUGUI label;
        public Vector2 home;
        public int hitsLeft;
        public float shake;      // 클릭 직후 흔들림 세기 (점점 줄어듦)
        public float flash;      // 클릭 직후 붉게 번쩍임 (점점 줄어듦)
        public float phase;      // 둥실둥실 움직임 시작점
        public bool isLeaving;   // 날아가는 중
        public float leaveTime;
        public Vector2 leaveDirection;
    }

    private readonly List<Thought> thoughts = new List<Thought>();
    private GameObject root;
    private RectTransform headArea;
    private Image timerFill;
    private TextMeshProUGUI countText;
    private Action<bool> onFinished;
    private bool isRunning;
    private float timeLeft;
    private float nextSpawnTime;
    private int spawnedCount;
    private int clearedCount;

    // ═══════════════════════════════════════════════════
    //  시작
    // ═══════════════════════════════════════════════════

    public static StrangePotionGimmick GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        StrangePotionGimmick found = FindFirstObjectByType<StrangePotionGimmick>();
        if (found != null)
            return found;

        return new GameObject("이상한 물약 기믹").AddComponent<StrangePotionGimmick>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (root != null)
            Destroy(root);

        if (Instance == this)
            Instance = null;
    }

    /// <summary>머릿속 연타 기믹을 시작합니다. 끝나면 onFinished(성공 여부)를 호출합니다.</summary>
    public void Begin(Action<bool> onFinished)
    {
        if (isRunning)
            return;

        this.onFinished = onFinished;
        BuildUi();

        thoughts.Clear();
        thoughtsShown = 0;
        spawnedCount = 0;
        clearedCount = 0;
        timeLeft = timeLimit;
        isRunning = true;

        for (int i = 0; i < Mathf.Min(startThoughts, totalThoughts); i++)
            SpawnThought();

        nextSpawnTime = Time.unscaledTime + spawnInterval;
        UpdateCountText();
        EXODSound.Play("glitch", 0.7f);
        EXODSound.PlayHeartbeat(0.55f, 1.15f);
    }

    // ═══════════════════════════════════════════════════
    //  진행
    // ═══════════════════════════════════════════════════

    private void Update()
    {
        if (!isRunning)
            return;

        float dt = Time.unscaledDeltaTime;

        // 기절·게임 오버 연출이 끼어들면 시간을 멈춥니다.
        bool isPaused = PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy;
        if (!isPaused)
        {
            timeLeft -= dt;

            bool canSpawn = spawnedCount < totalThoughts && CountAlive() < maxOnScreen;
            if (canSpawn && Time.unscaledTime >= nextSpawnTime)
            {
                SpawnThought();
                nextSpawnTime = Time.unscaledTime + spawnInterval;
            }
        }

        AnimateThoughts(dt);
        UpdateTimer();

        if (clearedCount >= totalThoughts)
        {
            Finish(true);
            return;
        }

        if (timeLeft <= 0f)
            Finish(false);
    }

    private void OnThoughtClicked(Thought thought)
    {
        if (!isRunning || thought.isLeaving)
            return;

        thought.hitsLeft--;
        thought.shake = 1f;
        thought.flash = 1f;
        EXODSound.Play("keypad_press", 0.6f);

        if (thought.hitsLeft > 0)
            return;

        // 날려 보냅니다: 머릿속 가운데에서 바깥쪽으로 튕겨 나갑니다.
        thought.isLeaving = true;
        thought.leaveTime = 0f;
        thought.leaveDirection = thought.home.sqrMagnitude > 1f ? thought.home.normalized : Vector2.up;
        thought.image.raycastTarget = false;
        clearedCount++;
        UpdateCountText();
        EXODSound.Play("type_tick", 0.9f);
    }

    private void AnimateThoughts(float dt)
    {
        float time = Time.unscaledTime;

        for (int i = thoughts.Count - 1; i >= 0; i--)
        {
            Thought thought = thoughts[i];

            if (thought.isLeaving)
            {
                thought.leaveTime += dt;
                float t = thought.leaveTime / 0.35f;
                thought.rect.anchoredPosition = thought.home + thought.leaveDirection * (900f * t * t);
                thought.rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t);
                SetThoughtAlpha(thought, 1f - t);

                if (t >= 1f)
                {
                    Destroy(thought.rect.gameObject);
                    thoughts.RemoveAt(i);
                }

                continue;
            }

            // 둥실둥실 + 클릭할 때 덜덜 흔들림
            Vector2 drift = new Vector2(Mathf.Sin(time * 1.3f + thought.phase), Mathf.Cos(time * 1.7f + thought.phase)) * 10f;
            Vector2 shake = UnityEngine.Random.insideUnitCircle * (18f * thought.shake);
            thought.rect.anchoredPosition = thought.home + drift + shake;

            // 맞을수록 작아지고, 오래 남아 있을수록 숨 쉬듯 커졌다 작아집니다.
            float damage = 1f - (float)thought.hitsLeft / Mathf.Max(1, hitsPerThought);
            float pulse = 1f + Mathf.Sin(time * 4f + thought.phase) * 0.04f;
            thought.rect.localScale = Vector3.one * (Mathf.Lerp(1f, 0.7f, damage) * pulse + thought.shake * 0.08f);

            thought.image.color = Color.Lerp(BubbleColor, BubbleHitColor, thought.flash);
            thought.shake = Mathf.Max(0f, thought.shake - dt * 6f);
            thought.flash = Mathf.Max(0f, thought.flash - dt * 5f);
        }
    }

    private void Finish(bool isCleared)
    {
        isRunning = false;
        EXODSound.StopHeartbeat();
        EXODSound.Play(isCleared ? "key_unlock" : "glitch");

        if (root != null)
            Destroy(root);

        root = null;
        thoughts.Clear();

        Action<bool> callback = onFinished;
        onFinished = null;
        callback?.Invoke(isCleared);
    }

    // ═══════════════════════════════════════════════════
    //  생각 만들기
    // ═══════════════════════════════════════════════════

    // 4가지 문구를 한 번씩 섞어서 먼저 다 보여 준 뒤, 그다음부터는 무작위
    private readonly List<string> thoughtDeck = new List<string>();
    private int thoughtsShown;

    private string NextThoughtText()
    {
        string[] all = Stage1Dialogues.PotionThoughts;
        if (thoughtsShown == 0)
        {
            thoughtDeck.Clear();
            thoughtDeck.AddRange(all);
            for (int i = thoughtDeck.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (thoughtDeck[i], thoughtDeck[j]) = (thoughtDeck[j], thoughtDeck[i]);
            }
        }

        string text = thoughtsShown < thoughtDeck.Count
            ? thoughtDeck[thoughtsShown]
            : all[UnityEngine.Random.Range(0, all.Length)];
        thoughtsShown++;
        return text;
    }

    private void SpawnThought()
    {
        Vector2 home = FindFreeSpot();

        GameObject go = new GameObject("생각 " + (spawnedCount + 1), typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(headArea, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = BubbleSize;
        rect.anchoredPosition = home;

        Image image = go.GetComponent<Image>();
        image.color = BubbleColor;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = HeadBorderColor;
        outline.effectDistance = new Vector2(4f, -4f);

        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        TextMeshProUGUI label = CreateText("글자", rect, NextThoughtText(), 40f, BubbleTextColor);
        Stretch(label.rectTransform);
        // 문구 길이가 달라서 칸에 맞게 줄어들도록
        label.enableAutoSizing = true;
        label.fontSizeMin = 22f;
        label.fontSizeMax = 32f;
        label.margin = new Vector4(12f, 6f, 12f, 6f);
        label.raycastTarget = false;

        Thought thought = new Thought
        {
            rect = rect,
            image = image,
            label = label,
            home = home,
            hitsLeft = Mathf.Max(1, hitsPerThought),
            phase = UnityEngine.Random.Range(0f, 10f),
        };

        button.onClick.AddListener(() => OnThoughtClicked(thought));
        thoughts.Add(thought);
        spawnedCount++;

        // 새로 떠오를 때 살짝 튀어나오는 느낌
        thought.shake = 0.6f;
        EXODSound.Play("type_tick_low", 0.6f);
    }

    // 다른 생각과 너무 겹치지 않는 자리를 고릅니다.
    private Vector2 FindFreeSpot()
    {
        float halfX = HeadSize.x * 0.5f - BubbleSize.x * 0.5f - 30f;
        float minY = -HeadSize.y * 0.5f + BubbleSize.y * 0.5f + 40f;
        float maxY = HeadSize.y * 0.5f - BubbleSize.y * 0.5f - 90f; // 위쪽은 제목·시간 막대 자리

        Vector2 best = Vector2.zero;
        float bestDistance = -1f;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 candidate = new Vector2(UnityEngine.Random.Range(-halfX, halfX), UnityEngine.Random.Range(minY, maxY));
            float nearest = float.MaxValue;

            foreach (Thought other in thoughts)
            {
                if (!other.isLeaving)
                    nearest = Mathf.Min(nearest, Vector2.Distance(candidate, other.home));
            }

            if (nearest > BubbleSize.x * 1.05f)
                return candidate;

            if (nearest > bestDistance)
            {
                bestDistance = nearest;
                best = candidate;
            }
        }

        return best;
    }

    private int CountAlive()
    {
        int count = 0;
        foreach (Thought thought in thoughts)
        {
            if (!thought.isLeaving)
                count++;
        }

        return count;
    }

    // ═══════════════════════════════════════════════════
    //  화면 (머릿속)
    // ═══════════════════════════════════════════════════

    private void BuildUi()
    {
        if (root != null)
            Destroy(root);

        root = new GameObject("머릿속 화면 (이상한 물약)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        // 퀵슬롯·대화창보다 위, 기절·게임 오버 화면(1000)보다는 아래에 그립니다.
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        // 뒤쪽 화면 클릭을 막는 어두운 배경
        Image backdrop = CreateImage("어두운 배경", root.transform, BackdropColor);
        Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;

        // 머릿속 판 (테두리 → 안쪽)
        Image border = CreateImage("머릿속 테두리", root.transform, HeadBorderColor);
        border.rectTransform.anchorMin = border.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        border.rectTransform.sizeDelta = HeadSize + new Vector2(16f, 16f);
        border.rectTransform.anchoredPosition = new Vector2(0f, 20f);

        Image head = CreateImage("머릿속", border.transform, HeadColor);
        headArea = head.rectTransform;
        headArea.anchorMin = headArea.anchorMax = new Vector2(0.5f, 0.5f);
        headArea.sizeDelta = HeadSize;
        headArea.anchoredPosition = Vector2.zero;

        TextMeshProUGUI title = CreateText("제목", headArea, "- 머릿속 -", 34f, HeadBorderColor);
        PlaceTop(title.rectTransform, 22f, 50f);

        // 남은 시간 막대
        Image timerBack = CreateImage("시간 막대 배경", headArea, new Color(0f, 0f, 0f, 0.5f));
        timerBack.rectTransform.anchorMin = new Vector2(0.1f, 1f);
        timerBack.rectTransform.anchorMax = new Vector2(0.9f, 1f);
        timerBack.rectTransform.pivot = new Vector2(0.5f, 1f);
        timerBack.rectTransform.sizeDelta = new Vector2(0f, 14f);
        timerBack.rectTransform.anchoredPosition = new Vector2(0f, -78f);

        timerFill = CreateImage("시간 막대", timerBack.transform, TimerColor);
        timerFill.rectTransform.anchorMin = Vector2.zero;
        timerFill.rectTransform.anchorMax = Vector2.one;
        timerFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        timerFill.rectTransform.offsetMin = Vector2.zero;
        timerFill.rectTransform.offsetMax = Vector2.zero;

        // 안내 문구 (판 아래)
        TextMeshProUGUI guide = CreateText("안내", root.transform, "생각을 연타해서 머릿속에서 날려 버리자!", 34f, Color.white);
        guide.rectTransform.anchorMin = guide.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        guide.rectTransform.sizeDelta = new Vector2(1400f, 60f);
        guide.rectTransform.anchoredPosition = new Vector2(0f, -HeadSize.y * 0.5f - 30f);

        countText = CreateText("남은 생각", root.transform, string.Empty, 28f, new Color(1f, 0.8f, 0.88f, 1f));
        countText.rectTransform.anchorMin = countText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        countText.rectTransform.sizeDelta = new Vector2(800f, 50f);
        countText.rectTransform.anchoredPosition = new Vector2(0f, -HeadSize.y * 0.5f - 80f);
    }

    private void UpdateTimer()
    {
        if (timerFill == null)
            return;

        float ratio = Mathf.Clamp01(timeLeft / Mathf.Max(0.01f, timeLimit));
        timerFill.rectTransform.localScale = new Vector3(ratio, 1f, 1f);

        // 3초 남으면 붉게 깜빡입니다.
        bool isLow = timeLeft <= 3f;
        float blink = isLow ? (Mathf.Sin(Time.unscaledTime * 12f) + 1f) * 0.5f : 0f;
        timerFill.color = isLow ? Color.Lerp(TimerColor, TimerLowColor, blink) : TimerColor;
    }

    private void UpdateCountText()
    {
        if (countText != null)
            countText.text = "남은 생각 " + Mathf.Max(0, totalThoughts - clearedCount) + "개";
    }

    private static void SetThoughtAlpha(Thought thought, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        Color imageColor = thought.image.color;
        imageColor.a = alpha;
        thought.image.color = imageColor;
        thought.label.alpha = alpha;
    }

    // ─── UI 도우미 ─────────────────────────────────────

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();

        TMP_FontAsset font = EXODUiAssets.Font;
        if (font != null)
            label.font = font;

        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void PlaceTop(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, height);
        rect.anchoredPosition = new Vector2(0f, -top);
    }
}
