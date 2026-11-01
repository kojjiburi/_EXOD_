using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// UI 숨김 키 (Tab)
/// - Tab을 누를 때마다 단계가 바뀝니다.
///   1번: 상단 UI(DAY·하트·얀 얼굴)만 숨김 → 2번: 퀵슬롯까지 숨김 → 3번: 모두 다시 표시 → 다시 1번부터
/// - 숨긴 동안에는 클릭도 통과해서 그 아래에 있는 물건(예: 화장실 수건)을 누를 수 있습니다.
/// - 대화창, 조사 화면, 키패드 같은 기믹 UI는 숨기지 않습니다.
/// - DAY 아래에 "UI숨김버튼-Tab" 안내 문구를 작게 붙이며, 이 문구도 상단 UI와 함께 숨겨집니다.
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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey) && !IsTypingInInputField())
        {
            hideLevel = (hideLevel + 1) % HideLevelCount;
            RefreshTargets();
            Apply();
        }

        // 퀵슬롯·상단 UI가 늦게 만들어지는 경우를 위해 주기적으로 확인합니다.
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 0.5f;
            RefreshTargets();
            Apply();
        }
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
