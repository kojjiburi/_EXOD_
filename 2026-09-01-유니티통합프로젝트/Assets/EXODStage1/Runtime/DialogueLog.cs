using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지나간 대사를 다시 볼 수 있는 대화 기록 창입니다.
/// - 대화창에 나온 대사를 순서대로 모아 둡니다. (최근 MaxEntries줄)
/// - L 키 또는 화면 위쪽 [대화 기록 (L)] 버튼으로 열고, L·Esc·X로 닫습니다.
/// - 이름이 있는 대사는 "이름  대사", 생각·설명은 회색으로 표시합니다.
/// </summary>
[DefaultExecutionOrder(275)]
public sealed class DialogueLog : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.L;
    private const int MaxEntries = 200;

    private static readonly Color PanelColor = new Color(0.07f, 0.05f, 0.06f, 0.97f);
    private static readonly Color NameColor = new Color(0.86f, 0.30f, 0.40f, 1f);
    private static readonly Color LineColor = new Color(0.90f, 0.87f, 0.88f, 1f);
    private static readonly Color ThoughtColor = new Color(0.62f, 0.60f, 0.62f, 1f);

    private struct Entry
    {
        public string Speaker;
        public string Text;
    }

    private readonly List<Entry> entries = new List<Entry>();

    private GameObject window;
    private RectTransform content;
    private ScrollRect scrollRect;
    private CanvasGroup buttonGroup;
    private TextMeshProUGUI emptyText;

    public static DialogueLog Instance { get; private set; }
    public bool IsOpen => window != null && window.activeSelf;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Build();

        if (DialogueManager.Instance != null)
            DialogueManager.Instance.LineShown += OnLineShown;
    }

    private void OnDestroy()
    {
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.LineShown -= OnLineShown;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey) || (IsOpen && Input.GetKeyDown(KeyCode.Escape)))
            Toggle();

        // 버튼은 상단 UI를 Tab으로 숨겼을 때, 아이템 확대 화면·키패드·기믹 창이 열려 있을 때 숨깁니다.
        bool showButton = !HudToggle.IsStatusHidden && !IsOpen && !Stage1HelpBar.IsCloseUpOpen();
        buttonGroup.alpha = showButton ? 1f : 0f;
        buttonGroup.blocksRaycasts = showButton;
    }

    private void OnLineShown(DialogueLine line)
    {
        if (line == null || string.IsNullOrEmpty(line.text))
            return;

        string speaker = line.speakerName != null ? line.speakerName.Trim() : string.Empty;
        if (speaker == "얀데레" || speaker == "여주")
            speaker = Stage1Dialogues.Girlfriend;

        entries.Add(new Entry { Speaker = speaker, Text = line.text });
        if (entries.Count > MaxEntries)
            entries.RemoveAt(0);
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            window.SetActive(false);
            return;
        }

        EXODSound.Play("inspect", 0.7f);
        Rebuild();
        window.SetActive(true);
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;  // 가장 최근 대사가 보이게
    }

    private void Rebuild()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        emptyText.gameObject.SetActive(entries.Count == 0);

        foreach (Entry entry in entries)
        {
            string text = string.IsNullOrEmpty(entry.Speaker)
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(ThoughtColor)}>{entry.Text}</color>"
                : $"<color=#{ColorUtility.ToHtmlStringRGB(NameColor)}>{entry.Speaker}</color>   {entry.Text}";

            TextMeshProUGUI line = CreateText("기록", content, text, 28f, LineColor, TextAlignmentOptions.TopLeft);
            line.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    // ═══════════════════════════════════════════════════
    //  UI 만들기
    // ═══════════════════════════════════════════════════

    private void Build()
    {
        GameObject root = new GameObject("대화 기록", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 720;  // 키패드(700) 위, HP 감소 효과(900)·기절 화면(1000) 아래

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildOpenButton(root.transform);
        BuildWindow(root.transform);
    }

    // 행동 요령 띠 바로 아래의 작은 버튼
    private void BuildOpenButton(Transform parent)
    {
        GameObject go = new GameObject("대화 기록 버튼", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Button), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-190f, -162f);
        rect.sizeDelta = new Vector2(190f, 44f);

        go.GetComponent<Image>().color = Color.white;
        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.06f, 0.05f, 0.06f, 0.72f);
        colors.highlightedColor = new Color(0.40f, 0.12f, 0.16f, 0.9f);
        colors.pressedColor = new Color(0.55f, 0.16f, 0.22f, 0.95f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(Toggle);

        buttonGroup = go.GetComponent<CanvasGroup>();

        TextMeshProUGUI label = CreateText("글자", go.transform, "대화 기록 (L)", 22f, LineColor, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
    }

    private void BuildWindow(Transform parent)
    {
        window = new GameObject("대화 기록 창", typeof(RectTransform));
        window.transform.SetParent(parent, false);
        Stretch(window.GetComponent<RectTransform>());

        Image dim = CreateImage("어둡게", window.transform, new Color(0f, 0f, 0f, 0.6f));
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Toggle);  // 바깥을 누르면 닫힘

        Image panel = CreateImage("창", window.transform, PanelColor);
        Place(panel.rectTransform, Vector2.zero, new Vector2(1180f, 800f));
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.06f, 0.10f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        TextMeshProUGUI title = CreateText("제목", panel.transform, "대화 기록", 40f, NameColor, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0f, 352f), new Vector2(600f, 60f));

        TextMeshProUGUI hint = CreateText("안내", panel.transform, "L · Esc 로 닫기", 22f, ThoughtColor, TextAlignmentOptions.Center);
        Place(hint.rectTransform, new Vector2(0f, -368f), new Vector2(600f, 40f));

        Image close = CreateImage("닫기", panel.transform, new Color(0.20f, 0.14f, 0.14f, 1f));
        Place(close.rectTransform, new Vector2(548f, 358f), new Vector2(56f, 56f));
        close.gameObject.AddComponent<Button>().onClick.AddListener(Toggle);
        EXODSound.AttachUiSounds(close.gameObject);
        TextMeshProUGUI closeLabel = CreateText("글자", close.transform, "X", 32f, new Color(0.95f, 0.45f, 0.45f, 1f), TextAlignmentOptions.Center);
        Stretch(closeLabel.rectTransform);

        // 스크롤 영역
        GameObject scrollObject = new GameObject("스크롤", typeof(RectTransform), typeof(ScrollRect));
        scrollObject.transform.SetParent(panel.transform, false);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        Place(scrollRectTransform, new Vector2(0f, -6f), new Vector2(1100f, 640f));

        GameObject viewport = new GameObject("보이는 영역", typeof(RectTransform), typeof(RectMask2D), typeof(CanvasRenderer), typeof(Image));
        viewport.transform.SetParent(scrollObject.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);  // 스크롤 휠을 받기 위한 투명 바탕

        GameObject contentObject = new GameObject("내용", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport.transform, false);
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 8, 8);
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.content = content;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        emptyText = CreateText("비어 있음", panel.transform, "아직 기록된 대사가 없어요.", 28f, ThoughtColor, TextAlignmentOptions.Center);
        Place(emptyText.rectTransform, Vector2.zero, new Vector2(800f, 60f));

        window.SetActive(false);
    }

    // ═══════════════════════════════════════════════════
    //  도우미
    // ═══════════════════════════════════════════════════

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (EXODUiAssets.Font != null)
            text.font = EXODUiAssets.Font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
