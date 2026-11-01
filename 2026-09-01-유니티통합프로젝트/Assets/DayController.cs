using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 각 Day 씬에 붙이는 컨트롤러
/// Day1, Day2, Day3, Day4 씬에 각각 붙이기
/// </summary>
public class DayController : MonoBehaviour
{
    [Header("=== 일차 정보 ===")]
    public int dayNumber = 1;           // 이 씬이 몇 일차인지

    [Header("=== 대화 데이터 ===")]
    public DialogueData openingDialogue;  // 씬 시작 대화
    public DialogueData endingDialogue;   // 씬 종료 대화

    [Header("=== UI ===")]
    public GameObject dayTitleUI;         // "DAY-1" 텍스트 UI (임시)

    [Header("=== 미니게임 오브젝트 ===")]
    public GameObject miniGameObject;     // 미니게임 오브젝트 (임시 박스)

    [Header("=== 일차 넘어갈 때 페이드 ===")]
    [Tooltip("화면이 천천히 검어지는 시간(초)")]
    public float fadeToBlackSeconds = 1.5f;
    [Tooltip("검은 화면에 'DAY-n'을 보여주는 시간(초)")]
    public float dayTitleHoldSeconds = 1.5f;
    [Tooltip("다시 천천히 투명해지는 시간(초)")]
    public float fadeFromBlackSeconds = 1.5f;
    public string dayTitleFormat = "DAY-{0}";

    private bool _dayCleared = false;

    /// <summary>일차 넘김 페이드가 진행 중인지 (이 동안 3분 타이머·침대 클릭이 멈춤)</summary>
    public static bool IsTransitioning { get; private set; }
    private static DayController _instance;

    private CanvasGroup _fadeGroup;
    private TextMeshProUGUI _fadeTitle;

    void Awake()
    {
        _instance = this;
        IsTransitioning = false;
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            IsTransitioning = false;
        }
    }

    /// <summary>
    /// 일차 넘김 페이드: 천천히 검어짐 → (검은 화면에서) advance 실행 + "DAY-n" 표시 → 천천히 투명해짐.
    /// 씬에 DayController가 없으면 false를 돌려주고 아무것도 하지 않습니다.
    /// </summary>
    public static bool TryPlayDayTransition(Action advance)
    {
        if (_instance == null || !_instance.isActiveAndEnabled)
            return false;
        if (IsTransitioning)
            return true;

        _instance.StartCoroutine(_instance.DayTransitionRoutine(advance));
        return true;
    }

    private IEnumerator DayTransitionRoutine(Action advance)
    {
        IsTransitioning = true;
        EnsureFadeOverlay();
        _fadeGroup.gameObject.SetActive(true);
        _fadeGroup.blocksRaycasts = true;
        _fadeTitle.alpha = 0f;

        yield return FadeOverlay(0f, 1f, fadeToBlackSeconds);

        // 화면이 완전히 검을 때 일차를 넘깁니다. (아이템 제자리 점검·초기화가 검은 화면 뒤에서 일어남)
        advance?.Invoke();

        int day = GManager.Instance != null ? GManager.Instance.Day : dayNumber + 1;
        _fadeTitle.text = string.Format(dayTitleFormat, day);
        yield return FadeTitle(0f, 1f, 0.4f);
        yield return new WaitForSecondsRealtime(dayTitleHoldSeconds);
        yield return FadeTitle(1f, 0f, 0.4f);

        yield return FadeOverlay(1f, 0f, fadeFromBlackSeconds);

        _fadeGroup.blocksRaycasts = false;
        _fadeGroup.gameObject.SetActive(false);
        IsTransitioning = false;
    }

    private IEnumerator FadeOverlay(float from, float to, float seconds)
    {
        float t = 0f;
        seconds = Mathf.Max(0.01f, seconds);
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            _fadeGroup.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        _fadeGroup.alpha = to;
    }

    private IEnumerator FadeTitle(float from, float to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            _fadeTitle.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        _fadeTitle.alpha = to;
    }

    // 검은 화면은 코드로 만듭니다 (모든 UI 위에 덮임)
    private void EnsureFadeOverlay()
    {
        if (_fadeGroup != null)
            return;

        GameObject canvasObject = new GameObject("일차 넘김 페이드", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _fadeGroup = canvasObject.GetComponent<CanvasGroup>();
        _fadeGroup.alpha = 0f;

        GameObject black = new GameObject("검은 화면", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        black.transform.SetParent(canvasObject.transform, false);
        RectTransform blackRect = black.GetComponent<RectTransform>();
        blackRect.anchorMin = Vector2.zero;
        blackRect.anchorMax = Vector2.one;
        blackRect.offsetMin = Vector2.zero;
        blackRect.offsetMax = Vector2.zero;
        Image image = black.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;

        GameObject title = new GameObject("DAY 표시", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        title.transform.SetParent(canvasObject.transform, false);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(1200f, 240f);
        _fadeTitle = title.GetComponent<TextMeshProUGUI>();
        _fadeTitle.fontSize = 120f;
        _fadeTitle.alignment = TextAlignmentOptions.Center;
        _fadeTitle.color = Color.white;
        _fadeTitle.raycastTarget = false;

        // 방 이름 글자와 같은 폰트(Galmuri9)
        TextMeshProUGUI roomText = SliderManager.Instance != null ? SliderManager.Instance.roomNameText : null;
        if (roomText != null && roomText.font != null)
            _fadeTitle.font = roomText.font;

        canvasObject.SetActive(false);
    }

    void Start()
    {
        // 일차 타이틀 표시
        ShowDayTitle();

        // 시작 대화 실행
        if (openingDialogue != null)
        {
            DialogueManager.Instance.StartDialogue(openingDialogue);
            DialogueManager.Instance.onDialogueEnd.AddListener(OnOpeningEnd);
        }
    }

    private void ShowDayTitle()
    {
        if (dayTitleUI != null)
            dayTitleUI.SetActive(true);

        // 2초 후 숨기기
        Invoke(nameof(HideDayTitle), 2f);
    }

    private void HideDayTitle()
    {
        if (dayTitleUI != null)
            dayTitleUI.SetActive(false);
    }

    // 오프닝 대화 끝나면 호출
    private void OnOpeningEnd()
    {
        DialogueManager.Instance.onDialogueEnd.RemoveListener(OnOpeningEnd);
        // 미니게임 활성화
        if (miniGameObject != null)
            miniGameObject.SetActive(true);
    }

    /// <summary>미니게임 클리어 시 호출</summary>
    public void OnMiniGameClear()
    {
        if (_dayCleared) return;
        _dayCleared = true;

        // 종료 대화 후 다음 씬으로
        if (endingDialogue != null)
        {
            DialogueManager.Instance.StartDialogue(endingDialogue);
            DialogueManager.Instance.onDialogueEnd.AddListener(OnEndingEnd);
        }
        else
        {
            GoNextDay();
        }
    }

    private void OnEndingEnd()
    {
        DialogueManager.Instance.onDialogueEnd.RemoveListener(OnEndingEnd);
        GoNextDay();
    }

    private void GoNextDay()
    {
        if (StageManager.Instance != null)
            StageManager.Instance.NextDay();
    }
}
