using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UI 숨김 키 (Tab)
/// - Tab을 누를 때마다 단계가 바뀝니다.
///   1번: 상단 UI(DAY·하트·얀 얼굴)만 숨김 → 2번: 퀵슬롯까지 숨김 → 3번: 모두 다시 표시 → 다시 1번부터
/// - 숨긴 동안에는 클릭도 통과해서 그 아래에 있는 물건(예: 화장실 수건)을 누를 수 있습니다.
/// - 대화창, 조사 화면, 키패드 같은 기믹 UI는 숨기지 않습니다.
/// - DAY 아래에 "UI숨김버튼-Tab" 안내 문구를 작게 붙이며, 이 문구도 상단 UI와 함께 숨겨집니다.
/// - [터치] 상단 UI 왼쪽 아래에 "UI 금지" 그림 버튼을 둡니다. 누르면 Tab과 같습니다.
///   이 버튼은 따로 만든 캔버스에 있어서 UI를 숨겨도 숨겨지지 않습니다.
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
[DefaultExecutionOrder(220)]
public sealed class HudToggle : MonoBehaviour
{
    public static HudToggle Instance { get; private set; }

    public const KeyCode ToggleKey = KeyCode.Tab;

    private const string HintText = "UI숨김버튼-Tab";
    private const string HintObjectName = "UI숨김 안내";
    private const float HintGap = 1f; // DAY 글자와 안내 문구 사이 간격 (DAY 상자 안에 들어가도록)
    private const string StatusRootName = "EXOD_PlayerStatusSystem"; // DAY·하트·얀 얼굴이 있는 상단 UI
    private const string DayObjectName = "DAY";
    private const string QuickSlotCanvasName = "QuickSlotCanvas";   // 퀵슬롯 (씬을 넘어가도 유지됨)

    // 0: 모두 표시, 1: 상단 UI만 숨김, 2: 상단 UI + 퀵슬롯 숨김
    // 씬을 다시 불러와도 숨김 단계를 유지합니다.
    private const int HideLevelCount = 3;
    private static int hideLevel;

    private readonly List<CanvasGroup> statusGroups = new();
    private readonly List<CanvasGroup> quickSlotGroups = new();
    private float nextRefreshTime;

    // [터치] UI 숨김 버튼 (상단 UI 왼쪽 아래, 숨겨지지 않음)
    private const float ButtonSize = 64f;
    private const float ButtonGap = 10f;   // 상단 UI 아래와 버튼 사이 (캔버스 단위)
    private Canvas buttonCanvas;
    private RectTransform buttonRect;
    private static Sprite ringSprite;

    public static int HideLevel => hideLevel;
    public static bool IsStatusHidden => hideLevel >= 1;
    public static bool IsQuickSlotHidden => hideLevel >= 2;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        hideLevel = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<HudToggle>() != null)
            return;

        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null)
            slider.gameObject.AddComponent<HudToggle>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        BuildToggleButton();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (buttonCanvas != null)
            Destroy(buttonCanvas.gameObject);
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey) && !IsTypingInInputField())
            Cycle();

        // 퀵슬롯·상단 UI가 늦게 만들어지는 경우를 위해 주기적으로 확인합니다.
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 0.5f;
            RefreshTargets();
            Apply();
        }
    }

    /// <summary>다음 숨김 단계로 (Tab 또는 화면 버튼)</summary>
    public void Cycle()
    {
        hideLevel = (hideLevel + 1) % HideLevelCount;
        EXODSound.Play("ui_hover");
        RefreshTargets();
        Apply();
    }

    private void LateUpdate()
    {
        PlaceToggleButton();
    }

    /// <summary>0: 모두 표시, 1: 상단 UI만 숨김, 2: 상단 UI + 퀵슬롯 숨김</summary>
    public void SetHideLevel(int level)
    {
        hideLevel = Mathf.Clamp(level, 0, HideLevelCount - 1);
        RefreshTargets();
        Apply();
    }

    private void Apply()
    {
        SetVisible(statusGroups, !IsStatusHidden);
        SetVisible(quickSlotGroups, !IsQuickSlotHidden);
    }

    private static void SetVisible(List<CanvasGroup> groups, bool visible)
    {
        foreach (CanvasGroup group in groups)
        {
            if (group == null)
                continue;

            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }
    }

    private void RefreshTargets()
    {
        statusGroups.RemoveAll(group => group == null);
        quickSlotGroups.RemoveAll(group => group == null);

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool isStatusCanvas = IsUnderStatusRoot(canvas.transform);
            bool isQuickSlotCanvas = canvas.name == QuickSlotCanvasName;
            if (!isStatusCanvas && !isQuickSlotCanvas)
                continue;

            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            if (group == null)
                group = canvas.gameObject.AddComponent<CanvasGroup>();

            List<CanvasGroup> groups = isStatusCanvas ? statusGroups : quickSlotGroups;
            if (!groups.Contains(group))
                groups.Add(group);

            if (isStatusCanvas)
                EnsureHint(canvas.transform);
        }
    }

    private static bool IsUnderStatusRoot(Transform transform)
    {
        Transform root = transform.root;
        return root != null && root.name == StatusRootName;
    }

    // DAY 글자 바로 아래에 안내 문구를 작게 붙입니다. (상단 UI 캔버스 안이라 함께 숨겨집니다)
    private static void EnsureHint(Transform statusCanvas)
    {
        Transform day = FindChildRecursive(statusCanvas, DayObjectName);
        if (day == null || day.Find(HintObjectName) != null)
            return;

        TMP_Text dayText = day.GetComponent<TMP_Text>();
        if (dayText == null)
            return;

        dayText.ForceMeshUpdate();
        Bounds bounds = dayText.textBounds;
        float hintSize = Mathf.Max(8f, dayText.fontSize * 0.34f);

        GameObject hintObject = new GameObject(
            HintObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        hintObject.transform.SetParent(day, false);

        TextMeshProUGUI hint = hintObject.GetComponent<TextMeshProUGUI>();
        hint.text = HintText;
        hint.font = dayText.font;
        hint.fontSize = hintSize;
        hint.fontStyle = FontStyles.Normal;
        hint.alignment = TextAlignmentOptions.Top;
        hint.color = Color.white;
        hint.raycastTarget = false;
        hint.enableWordWrapping = false;
        hint.outlineWidth = 0.12f;
        hint.outlineColor = new Color32(0, 0, 0, 255);

        RectTransform rect = hint.rectTransform;
        rect.anchorMin = rect.anchorMax = ((RectTransform)day).pivot;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(Mathf.Max(bounds.size.x * 2f, hintSize * 12f), hintSize * 1.6f);
        // DAY 글자 바로 아래, DAY 상자 안에 넣습니다.
        rect.anchoredPosition = new Vector2(bounds.center.x, bounds.min.y - HintGap);
    }

    // ─── [터치] UI 숨김 버튼 ───

    private void BuildToggleButton()
    {
        GameObject root = new GameObject("UI숨김 버튼 (터치)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        buttonCanvas = root.GetComponent<Canvas>();
        buttonCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        buttonCanvas.sortingOrder = 640;   // 대화 기록(720)·키패드(700) 아래
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject go = new GameObject("버튼", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(root.transform, false);
        buttonRect = (RectTransform)go.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = Vector2.zero;
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

        Image bg = go.GetComponent<Image>();
        bg.color = Color.white;
        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.06f, 0.05f, 0.06f, 0.75f);
        colors.highlightedColor = new Color(0.40f, 0.12f, 0.16f, 0.9f);
        colors.pressedColor = new Color(0.55f, 0.16f, 0.22f, 0.95f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(Cycle);

        // "UI" 글자
        GameObject label = new GameObject("UI", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        StretchFull((RectTransform)label.transform);
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.text = "UI";
        text.fontSize = 26f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        // 금지 표시: 빨간 동그라미 + 사선
        Color red = new Color(0.92f, 0.18f, 0.20f, 1f);
        GameObject ring = new GameObject("금지 동그라미", typeof(RectTransform), typeof(Image));
        ring.transform.SetParent(go.transform, false);
        RectTransform ringRect = (RectTransform)ring.transform;
        ringRect.anchorMin = ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        ringRect.sizeDelta = new Vector2(ButtonSize - 10f, ButtonSize - 10f);
        Image ringImage = ring.GetComponent<Image>();
        ringImage.sprite = GetRingSprite();
        ringImage.color = red;
        ringImage.raycastTarget = false;

        GameObject slash = new GameObject("금지 사선", typeof(RectTransform), typeof(Image));
        slash.transform.SetParent(go.transform, false);
        RectTransform slashRect = (RectTransform)slash.transform;
        slashRect.anchorMin = slashRect.anchorMax = new Vector2(0.5f, 0.5f);
        slashRect.sizeDelta = new Vector2(ButtonSize - 14f, 5f);
        slashRect.localRotation = Quaternion.Euler(0f, 0f, -45f);
        Image slashImage = slash.GetComponent<Image>();
        slashImage.color = red;
        slashImage.raycastTarget = false;

        PlaceToggleButton();
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null)
            return ringSprite;
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float c = (size - 1) * 0.5f, outer = size * 0.5f - 1f, inner = outer - size * 0.09f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }

    // 상단 UI(DAY·하트·얀 얼굴)가 차지하는 화면 영역의 왼쪽 아래 바로 밑에 버튼을 둡니다.
    // 상단 UI가 없는 씬에서는 버튼을 숨깁니다.
    private readonly Vector3[] cornerBuffer = new Vector3[4];

    private void PlaceToggleButton()
    {
        if (buttonRect == null)
            return;

        bool found = false;
        float minX = float.MaxValue, minY = float.MaxValue;
        float screenW = Screen.width, screenH = Screen.height;
        foreach (CanvasGroup group in statusGroups)
        {
            if (group == null || !group.gameObject.activeInHierarchy)
                continue;
            Canvas canvas = group.GetComponent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            foreach (Graphic graphic in group.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.enabled || graphic.color.a < 0.05f || graphic.name == HintObjectName)
                    continue;
                graphic.rectTransform.GetWorldCorners(cornerBuffer);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, cornerBuffer[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, cornerBuffer[2]);
                // 화면 전체를 덮는 배경 같은 것은 제외
                if (b.x - a.x > screenW * 0.8f || b.y - a.y > screenH * 0.8f)
                    continue;
                minX = Mathf.Min(minX, a.x);
                minY = Mathf.Min(minY, a.y);
                found = true;
            }
        }

        if (buttonCanvas.gameObject.activeSelf != found)
            buttonCanvas.gameObject.SetActive(found);
        if (!found)
            return;

        float scale = buttonCanvas.scaleFactor > 0f ? buttonCanvas.scaleFactor : 1f;
        buttonRect.anchoredPosition = new Vector2(Mathf.Max(minX, 0f) / scale, minY / scale - ButtonGap);
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }

        return null;
    }

    // 서랍 비밀번호처럼 글자를 입력하는 중에는 Tab으로 UI를 숨기지 않습니다.
    private static bool IsTypingInInputField()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null)
            return false;

        TMP_InputField tmpInput = selected.GetComponent<TMP_InputField>();
        if (tmpInput != null && tmpInput.isFocused)
            return true;

        UnityEngine.UI.InputField legacyInput = selected.GetComponent<UnityEngine.UI.InputField>();
        return legacyInput != null && legacyInput.isFocused;
    }
}
