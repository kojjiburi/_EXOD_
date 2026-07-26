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

    private readonly string[] _allRooms =
        { "침실", "거실", "복도", "화장실", "마당", "집밖" };
    private readonly System.Collections.Generic.List<string> _unlockedRooms = new();
    private int _currentIndex;

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
        if (leftButton != null)
            leftButton.onClick.AddListener(PrevRoom);
        if (rightButton != null)
            rightButton.onClick.AddListener(NextRoom);

        // 이미 해금된 상태로 저장된 경우에는 시작하자마자 버튼을 숨기지 않습니다.
        if (sliderPanel != null)
            sliderPanel.SetActive(isUnlocked);

        UpdateSlider();
    }

    public void UnlockSlider()
    {
        isUnlocked = true;
        if (sliderPanel != null)
            sliderPanel.SetActive(true);
        UpdateSlider();
    }

    public void UpdateSlider()
    {
        if (!isUnlocked)
            return;

        _unlockedRooms.Clear();

        foreach (string room in _allRooms)
        {
            if (RoomManager.Instance != null && RoomManager.Instance.IsUnlocked(room))
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
    }

    private void PrevRoom()
    {
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
        if (_unlockedRooms.Count == 0)
            return;

        _currentIndex++;
        if (_currentIndex >= _unlockedRooms.Count)
            _currentIndex = 0;

        RefreshUI();
        MoveToRoom();
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
        Debug.Log($"{_unlockedRooms[_currentIndex]} 으로 이동!");
        // TODO: CameraManager가 완성되면 이곳에서 실제 방 이동을 연결합니다.
    }

    public string CurrentRoom =>
        _unlockedRooms.Count > 0 ? _unlockedRooms[_currentIndex] : string.Empty;
}
