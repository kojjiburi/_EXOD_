using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 장소 이동 슬라이더 (캐러셀 방식)
/// 해금된 방만 표시합니다.
/// </summary>
public class SliderManager : MonoBehaviour
{
    public static SliderManager Instance { get; private set; }

    [Header("=== UI ===")]
    public GameObject sliderPanel;
    public TextMeshProUGUI roomNameText;
    public Button leftButton;
    public Button rightButton;

    [Header("=== 슬라이더 해금 조건 ===")]
    public bool isUnlocked = false;

    [Tooltip("침실 방문이 열리기 전에는 좌우 버튼을 눌러도 이동하지 않습니다.")]
    [SerializeField] private bool startLocked = true;

    [Header("=== 임시 단색 방 배경 ===")]
    [SerializeField] private Color livingRoomColor = new(0.42f, 0.36f, 0.31f, 1f);
    [SerializeField] private Color hallwayColor = new(0.25f, 0.28f, 0.33f, 1f);
    [SerializeField] private Color bathroomColor = new(0.29f, 0.32f, 0.41f, 1f);
    [SerializeField] private Color outdoorColor = new(0.20f, 0.29f, 0.24f, 1f);

    [Header("=== 잠금 경고 ===")]
    [Tooltip("비워 두면 실행 중 Canvas 중앙에 자동으로 생성합니다.")]
    [SerializeField] private GameObject lockedWarningPanel;
    [Tooltip("비워 두면 실행 중 Galmuri9가 적용된 문구를 자동으로 생성합니다.")]
    [SerializeField] private TextMeshProUGUI lockedWarningText;
    [SerializeField] private string lockedWarningMessage = "아직 탈출할 수 없음";
    [Min(0.1f)]
    [SerializeField] private float lockedWarningDuration = 1.5f;
    [Min(0.1f)]
    [SerializeField] private float lockedWarningFadeDuration = 0.35f;

    private readonly string[] _allRooms =
        { "침실", "거실", "복도", "화장실", "야외" };
    private readonly List<string> _unlockedRooms = new();
    private readonly Dictionary<string, GameObject> _roomViews = new();
    private int _currentIndex;
    private CanvasGroup _lockedWarningCanvasGroup;
    private Coroutine _lockedWarningCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        InitializeRoomViews();

        if (leftButton != null)
            leftButton.onClick.AddListener(PrevRoom);
        if (rightButton != null)
            rightButton.onClick.AddListener(NextRoom);

        if (startLocked)
            isUnlocked = false;

        // 잠긴 상태에서도 좌우 버튼을 눌러 경고를 확인할 수 있도록 항상 표시합니다.
        if (sliderPanel != null)
            sliderPanel.SetActive(true);

        EnsureLockedWarningUi();

        UpdateSlider();
        ShowRoom("침실");
    }

    private void OnDestroy()
    {
        if (leftButton != null)
            leftButton.onClick.RemoveListener(PrevRoom);
        if (rightButton != null)
            rightButton.onClick.RemoveListener(NextRoom);

        if (Instance == this)
            Instance = null;
    }

    public void UnlockSlider()
    {
        isUnlocked = true;
        if (sliderPanel != null)
            sliderPanel.SetActive(true);

        HideLockedWarning();
        UpdateSlider();
    }

    /// <summary>
    /// 침실 방문이 실제로 열린 순간 Button의 OnClick 또는 UnityEvent에 연결합니다.
    /// </summary>
    public void OnBedroomDoorOpened()
    {
        UnlockSlider();
    }

    public void UpdateSlider()
    {
        if (!isUnlocked)
        {
            // 비활성화하면 클릭 자체가 들어오지 않으므로 잠긴 동안에는 버튼을 활성화합니다.
            if (leftButton != null)
                leftButton.interactable = true;
            if (rightButton != null)
                rightButton.interactable = true;

            if (roomNameText != null)
                roomNameText.text = "침실";

            ShowRoom("침실");

            return;
        }

        _unlockedRooms.Clear();

        foreach (string room in _allRooms)
        {
            string unlockName = room == "야외" ? "마당" : room;
            if (RoomManager.Instance != null && RoomManager.Instance.IsUnlocked(unlockName))
                _unlockedRooms.Add(room);
        }

        if (_unlockedRooms.Count == 0)
        {
            if (roomNameText != null)
                roomNameText.text = string.Empty;
            if (leftButton != null)
                leftButton.interactable = false;
            if (rightButton != null)
                rightButton.interactable = false;
            return;
        }

        if (_currentIndex < 0 || _currentIndex >= _unlockedRooms.Count)
            _currentIndex = 0;

        RefreshUI();
        ShowRoom(_unlockedRooms[_currentIndex]);
    }

    private void PrevRoom()
    {
        if (!isUnlocked)
        {
            if (TryOpenBedroomDoorWithKey())
                return;

            ShowLockedWarning();
            return;
        }

        if (_unlockedRooms.Count == 0)
            return;

        _currentIndex--;
        if (_currentIndex < 0)
            _currentIndex = _unlockedRooms.Count - 1;

        RefreshUI();
        MoveToRoom();
    }

    private void NextRoom()
    {
        if (!isUnlocked)
        {
            if (TryOpenBedroomDoorWithKey())
                return;

            ShowLockedWarning();
            return;
        }

        if (_unlockedRooms.Count == 0)
            return;

        _currentIndex++;
        if (_currentIndex >= _unlockedRooms.Count)
            _currentIndex = 0;

        RefreshUI();
        MoveToRoom();
    }

    private bool TryOpenBedroomDoorWithKey()
    {
        PasswordDrawer drawer = FindFirstObjectByType<PasswordDrawer>(FindObjectsInactive.Include);
        if (drawer == null || !drawer.IsKeyCollected)
            return false;

        UnlockSlider();

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartInteractionDialogue(
                "문",
                new[] { "열쇠로 문을 열었다." });
        }

        return true;
    }

    private void RefreshUI()
    {
        if (_unlockedRooms.Count == 0)
            return;

        if (roomNameText != null)
            roomNameText.text = _unlockedRooms[_currentIndex];

        bool hasMultipleRooms = _unlockedRooms.Count > 1;
        if (leftButton != null)
            leftButton.interactable = hasMultipleRooms;
        if (rightButton != null)
            rightButton.interactable = hasMultipleRooms;
    }

    private void MoveToRoom()
    {
        if (_unlockedRooms.Count == 0)
            return;

        if (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen)
            InspectionManager.Instance.Close();

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            DialogueManager.Instance.ForceCloseDialogue();

        string roomName = _unlockedRooms[_currentIndex];
        ShowRoom(roomName);
        Debug.Log($"{roomName}으로 이동!");
    }

    private void InitializeRoomViews()
    {
        _roomViews.Clear();

        RectTransform bedroomTransform = FindRectTransform("침실 기믹 화면");
        Canvas canvas = sliderPanel != null
            ? sliderPanel.GetComponentInParent<Canvas>()
            : FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("Canvas를 찾을 수 없어 단색 방 화면을 만들지 못했습니다.");
            return;
        }

        Transform roomParent = bedroomTransform != null
            ? bedroomTransform.parent
            : canvas.transform;
        int roomSiblingIndex = bedroomTransform != null
            ? bedroomTransform.GetSiblingIndex()
            : 0;

        if (bedroomTransform != null)
        {
            _roomViews["침실"] = bedroomTransform.gameObject;
        }
        else
        {
            _roomViews["침실"] = CreateSolidRoomView(
                "침실", new Color(0.38f, 0.39f, 0.37f, 1f), roomParent, roomSiblingIndex);
        }

        _roomViews["거실"] = CreateSolidRoomView(
            "거실", livingRoomColor, roomParent, roomSiblingIndex);
        _roomViews["복도"] = CreateSolidRoomView(
            "복도", hallwayColor, roomParent, roomSiblingIndex);
        _roomViews["화장실"] = CreateSolidRoomView(
            "화장실", bathroomColor, roomParent, roomSiblingIndex);
        _roomViews["야외"] = CreateSolidRoomView(
            "야외", outdoorColor, roomParent, roomSiblingIndex);

        foreach (KeyValuePair<string, GameObject> pair in _roomViews)
        {
            if (pair.Value != null)
                pair.Value.SetActive(pair.Key == "침실");
        }
    }

    private static GameObject CreateSolidRoomView(
        string roomName,
        Color color,
        Transform parent,
        int siblingIndex)
    {
        GameObject roomObject = new(
            $"{roomName} 단색 화면",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        roomObject.transform.SetParent(parent, false);
        roomObject.transform.SetSiblingIndex(siblingIndex);

        RectTransform rect = roomObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = roomObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        roomObject.SetActive(false);
        return roomObject;
    }

    private void ShowRoom(string roomName)
    {
        foreach (KeyValuePair<string, GameObject> pair in _roomViews)
        {
            if (pair.Value != null)
                pair.Value.SetActive(pair.Key == roomName);
        }
    }

    private static RectTransform FindRectTransform(string objectName)
    {
        RectTransform[] transforms = FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (RectTransform rectTransform in transforms)
        {
            if (rectTransform.name == objectName)
                return rectTransform;
        }

        return null;
    }

    private void ShowLockedWarning()
    {
        EnsureLockedWarningUi();

        if (lockedWarningPanel == null || lockedWarningText == null)
        {
            Debug.LogWarning(lockedWarningMessage);
            return;
        }

        lockedWarningText.text = lockedWarningMessage;
        lockedWarningPanel.SetActive(true);

        if (_lockedWarningCoroutine != null)
            StopCoroutine(_lockedWarningCoroutine);

        _lockedWarningCoroutine = StartCoroutine(PlayLockedWarning());
    }

    private IEnumerator PlayLockedWarning()
    {
        if (_lockedWarningCanvasGroup != null)
            _lockedWarningCanvasGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(lockedWarningDuration);

        float elapsed = 0f;
        while (elapsed < lockedWarningFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_lockedWarningCanvasGroup != null)
            {
                _lockedWarningCanvasGroup.alpha =
                    1f - Mathf.Clamp01(elapsed / lockedWarningFadeDuration);
            }

            yield return null;
        }

        if (lockedWarningPanel != null)
            lockedWarningPanel.SetActive(false);

        _lockedWarningCoroutine = null;
    }

    private void HideLockedWarning()
    {
        if (_lockedWarningCoroutine != null)
        {
            StopCoroutine(_lockedWarningCoroutine);
            _lockedWarningCoroutine = null;
        }

        if (_lockedWarningCanvasGroup != null)
            _lockedWarningCanvasGroup.alpha = 0f;

        if (lockedWarningPanel != null)
            lockedWarningPanel.SetActive(false);
    }

    private void EnsureLockedWarningUi()
    {
        if (lockedWarningPanel != null && lockedWarningText != null)
        {
            _lockedWarningCanvasGroup = lockedWarningPanel.GetComponent<CanvasGroup>();
            if (_lockedWarningCanvasGroup == null)
                _lockedWarningCanvasGroup = lockedWarningPanel.AddComponent<CanvasGroup>();

            lockedWarningText.fontStyle = FontStyles.Normal;
            lockedWarningPanel.SetActive(false);
            return;
        }

        Canvas canvas = null;
        if (sliderPanel != null)
            canvas = sliderPanel.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        lockedWarningPanel = new GameObject(
            "잠금 경고",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        lockedWarningPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = lockedWarningPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(520f, 90f);

        Image panelImage = lockedWarningPanel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelImage.raycastTarget = false;

        _lockedWarningCanvasGroup = lockedWarningPanel.GetComponent<CanvasGroup>();
        _lockedWarningCanvasGroup.alpha = 0f;
        _lockedWarningCanvasGroup.blocksRaycasts = false;
        _lockedWarningCanvasGroup.interactable = false;

        GameObject textObject = new GameObject(
            "잠금 경고 문구",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(lockedWarningPanel.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 8f);
        textRect.offsetMax = new Vector2(-24f, -8f);

        lockedWarningText = textObject.GetComponent<TextMeshProUGUI>();
        lockedWarningText.text = lockedWarningMessage;
        lockedWarningText.alignment = TextAlignmentOptions.Center;
        lockedWarningText.fontSize = 32f;
        lockedWarningText.color = Color.white;
        lockedWarningText.fontStyle = FontStyles.Normal;
        lockedWarningText.raycastTarget = false;

        // 프로젝트의 Galmuri9가 적용된 RoomNameText와 같은 폰트를 사용합니다.
        if (roomNameText != null && roomNameText.font != null)
            lockedWarningText.font = roomNameText.font;

        lockedWarningPanel.SetActive(false);
    }

    public string CurrentRoom =>
        _unlockedRooms.Count > 0 ? _unlockedRooms[_currentIndex] : string.Empty;
}
