using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// UI 숨김 키 (Tab)
/// - Tab을 누르면 상단 UI(DAY·하트·얀 얼굴)와 퀵슬롯을 숨기고, 다시 누르면 보입니다.
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
    private const string StatusRootName = "EXOD_PlayerStatusSystem"; // DAY·하트·얀 얼굴이 있는 상단 UI
    private const string DayObjectName = "DAY";
    private const string QuickSlotCanvasName = "QuickSlotCanvas";   // 퀵슬롯 (씬을 넘어가도 유지됨)

    // 씬을 다시 불러와도 숨김 상태를 유지합니다.
    private static bool isHidden;

    private readonly List<CanvasGroup> hudGroups = new();
    private float nextRefreshTime;

    public static bool IsHidden => isHidden;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        isHidden = false;
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
            isHidden = !isHidden;
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

    public void SetHidden(bool hidden)
    {
        isHidden = hidden;
        RefreshTargets();
        Apply();
    }

    private void Apply()
    {
        foreach (CanvasGroup group in hudGroups)
        {
            if (group == null)
                continue;

            group.alpha = isHidden ? 0f : 1f;
            group.blocksRaycasts = !isHidden;
            group.interactable = !isHidden;
        }
    }

    private void RefreshTargets()
    {
        hudGroups.RemoveAll(group => group == null);

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool isStatusCanvas = IsUnderStatusRoot(canvas.transform);
            bool isQuickSlotCanvas = canvas.name == QuickSlotCanvasName;
            if (!isStatusCanvas && !isQuickSlotCanvas)
                continue;

            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            if (group == null)
                group = canvas.gameObject.AddComponent<CanvasGroup>();

            if (!hudGroups.Contains(group))
                hudGroups.Add(group);

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
        float hintSize = Mathf.Max(8f, dayText.fontSize * 0.42f);

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
        // DAY 글자를 감싼 흰 상자 테두리 아래로 내립니다.
        rect.anchoredPosition = new Vector2(bounds.center.x, bounds.min.y - dayText.fontSize * 0.4f);
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
