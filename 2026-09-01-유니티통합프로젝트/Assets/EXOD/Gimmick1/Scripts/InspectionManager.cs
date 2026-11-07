using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 물건 확대 화면을 열고 닫으며 현재 선택된 물건의 자세한 조사를 처리합니다.
/// [터치] 확대 화면에서 누른 곳에 따라:
///   - 물건 그림(실제로 그려진 부분)을 누르면 → 조사 (대사 중이면 다음 줄)
///   - 대화창·버튼·퀵슬롯 등 다른 UI를 누르면 → 그 UI가 처리 (여기서는 아무것도 안 함)
///   - 그 밖의 빈 곳을 누르면 → [뒤로가기]와 같이 닫기 (선택지가 떠 있을 때는 닫지 않음)
/// </summary>
public class InspectionManager : MonoBehaviour
{
    public static InspectionManager Instance { get; private set; }

    [Header("자세히 보기 화면")]
    [SerializeField] private GameObject inspectionPanel;
    [SerializeField] private Image detailImage;
    [SerializeField] private Button detailButton;
    [SerializeField] private Button closeButton;

    [Header("조사 중 잠시 숨길 화면")]
    [Tooltip("장소 이동용 SliderPanel 등을 연결합니다.")]
    [SerializeField] private GameObject[] hideWhileInspecting;

    private InspectableItem currentItem;
    private bool[] hiddenUiPreviousStates;
    private bool hasStoredUiStates;
    private bool pressStartedWhileOpen;   // [터치] 확대 화면이 열린 뒤에 누르기 시작한 클릭만 처리
    private readonly List<RaycastResult> raycastHits = new List<RaycastResult>();
    private const string DialogueDimName = "대화 배경 어둡게";   // DialogueDimmer가 만드는 대사 중 배경 막

    private static readonly Vector2 DefaultDetailSize = new Vector2(760f, 540f);
    private static readonly Vector2 CalendarDetailSize = new Vector2(780f, 780f);

    public bool IsOpen => inspectionPanel != null && inspectionPanel.activeSelf;
    public string CurrentItemId => currentItem != null ? currentItem.ItemId : string.Empty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DisableHiddenUiBackgroundRaycasts();

        // [터치] 물건 그림 클릭은 Update의 HandlePointer가 직접 처리합니다 (그림의 투명한 여백·대사 중 배경 막 때문)
        if (detailButton != null)
            detailButton.transition = Selectable.Transition.None;

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (inspectionPanel != null)
            inspectionPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        RestoreOtherUi();

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();

        HandlePointer();
    }

    // [터치] 누른 곳(마우스·손가락)에 따라 조사 / 닫기. 손을 뗄 때 처리합니다 (UI 버튼과 같은 때).
    private void HandlePointer()
    {
        if (!IsOpen)
        {
            pressStartedWhileOpen = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
            pressStartedWhileOpen = true;
        if (!Input.GetMouseButtonUp(0) || !pressStartedWhileOpen)
            return;
        pressStartedWhileOpen = false;

        Vector2 screen = Input.mousePosition;

        // 맨 위에 있는 UI가 대화창·버튼·비밀번호 창 같은 다른 UI면 그쪽에 맡김
        EventSystem es = EventSystem.current;
        if (es != null)
        {
            raycastHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screen }, raycastHits);
            if (raycastHits.Count > 0 && !IsInspectionBackground(raycastHits[0].gameObject))
                return;
        }

        // 대화창(글자 부분처럼 클릭을 받지 않는 곳 포함)을 누른 건 닫기로 치지 않음
        if (IsPointOnDialogueBox(screen))
            return;

        if (IsPointOnObject(screen))
        {
            ExamineCurrentItem();
            return;
        }

        // 선택지가 떠 있으면 빈 곳을 눌러도 닫지 않음 (골라야 함)
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsChoosing)
            return;

        EXODSound.Play("ui_click", 0.5f);
        Close();
    }

    // 확대 화면의 어두운 바탕, 물건 그림, 대사 중 배경 막 → 이 화면이 직접 처리하는 곳
    private bool IsInspectionBackground(GameObject go)
    {
        if (go == null)
            return true;
        if (inspectionPanel != null && go == inspectionPanel)
            return true;
        if (detailImage != null && go == detailImage.gameObject)
            return true;
        return go.name == DialogueDimName;
    }

    private static bool IsPointOnDialogueBox(Vector2 screen)
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm == null || !dm.IsDialogueOpen)
            return false;
        return ContainsScreenPoint(dm.DialoguePanelRect, screen) || ContainsScreenPoint(dm.NamePanelRect, screen);
    }

    private static bool ContainsScreenPoint(RectTransform rect, Vector2 screen)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
            return false;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screen, cam);
    }

    // 물건 그림이 실제로 그려진 영역(preserveAspect로 생기는 투명한 여백 제외)을 눌렀는지
    private bool IsPointOnObject(Vector2 screen)
    {
        if (detailImage == null || !detailImage.gameObject.activeInHierarchy)
            return false;

        RectTransform rect = detailImage.rectTransform;
        Canvas canvas = detailImage.canvas;
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, cam, out Vector2 local))
            return false;

        Rect r = rect.rect;
        float w = r.width, h = r.height;
        if (detailImage.sprite != null && detailImage.preserveAspect)
        {
            float spriteAspect = detailImage.sprite.rect.width / detailImage.sprite.rect.height;
            if (w / h > spriteAspect) w = h * spriteAspect; else h = w / spriteAspect;
        }
        return Mathf.Abs(local.x - r.center.x) <= w * 0.5f && Mathf.Abs(local.y - r.center.y) <= h * 0.5f;
    }

    public void Open(InspectableItem item)
    {
        if (item == null || inspectionPanel == null || detailImage == null)
            return;

        currentItem = item;
        detailImage.sprite = item.DetailSprite;
        detailImage.preserveAspect = true;
        ApplyDetailLayout(item);
        inspectionPanel.SetActive(true);
        HideOtherUi();

        if (item.ItemId == "bedroom_drawer")
        {
            PasswordDrawer drawer = FindFirstObjectByType<PasswordDrawer>(FindObjectsInactive.Include);
            if (drawer != null)
                drawer.ApplyPersistentVisual();
        }

        // 서랍장 안내 대사("첫 번째 서랍에 잠금장치가…")는 처음 한 번만 보여 줍니다.
        bool showOpening = item.ItemId != "bedroom_drawer" || InspectionState.MarkOpeningShown(item.ItemId);

        if (showOpening && item.OpeningTexts != null && item.OpeningTexts.Length > 0 && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartInteractionDialogue(item.ItemName, item.OpeningTexts);
        }
    }

    public void ExamineCurrentItem()
    {
        // 조사 그림, 대화창의 '다음' 버튼, Space 키를 모두 사용할 수 있게 합니다.
        // 대사가 열려 있으면 그림 클릭은 현재 문장을 완성하거나 다음 줄로 진행합니다.
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
        {
            DialogueManager.Instance.AdvanceDialogue();
            return;
        }

        if (currentItem == null)
            return;

        // 서랍장은 화면 아무 데나 눌러도 열쇠를 다시 줍던 문제 → 맨 위 서랍 부분을 눌렀을 때만 (사용자 요청 2026-11-03)
        if (currentItem.ItemId == "bedroom_drawer" && !IsPointerOnDrawer())
            return;

        currentItem.Examine();
    }

    // 서랍장 그림(488x488) 안 맨 위 서랍 자리: 가로 21%~93%, 위에서 26%~62%
    private static readonly Rect DrawerAreaNormalized = new Rect(0.21f, 0.26f, 0.72f, 0.36f);

    private bool IsPointerOnDrawer()
    {
        if (detailImage == null || detailImage.sprite == null)
            return true;

        RectTransform rect = detailImage.rectTransform;
        Canvas canvas = detailImage.canvas;
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, cam, out Vector2 local))
            return false;

        // preserveAspect로 실제 그림이 그려진 영역
        Rect r = rect.rect;
        float spriteAspect = detailImage.sprite.rect.width / detailImage.sprite.rect.height;
        float w = r.width, h = r.height;
        if (w / h > spriteAspect) w = h * spriteAspect; else h = w / spriteAspect;
        float x0 = r.center.x - w * 0.5f, yTop = r.center.y + h * 0.5f;

        float nx = (local.x - x0) / w;
        float ny = (yTop - local.y) / h;   // 위에서부터
        return DrawerAreaNormalized.Contains(new Vector2(nx, ny));
    }

    public void Close()
    {
        if (inspectionPanel != null)
            inspectionPanel.SetActive(false);

        // 물건 설명 대화가 남아 있으면 대화창의 불투명 배경이
        // 방 화면 위에서 클릭을 계속 막으므로 조사 화면과 함께 닫습니다.
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            DialogueManager.Instance.ForceCloseDialogue();

        currentItem = null;
        RestoreOtherUi();
    }

    public void SetCurrentDetailSprite(Sprite sprite)
    {
        if (detailImage == null || sprite == null)
            return;

        detailImage.sprite = sprite;
        detailImage.preserveAspect = true;
    }

    private void ApplyDetailLayout(InspectableItem item)
    {
        RectTransform detailRect = detailImage.rectTransform;
        bool isCalendar = item.ItemId == "bedroom_calendar";

        detailRect.sizeDelta = isCalendar ? CalendarDetailSize : DefaultDetailSize;
        detailRect.anchoredPosition = isCalendar ? new Vector2(0f, 35f) : Vector2.zero;
    }

    private void HideOtherUi()
    {
        if (hideWhileInspecting == null || hasStoredUiStates)
            return;

        hiddenUiPreviousStates = new bool[hideWhileInspecting.Length];

        for (int i = 0; i < hideWhileInspecting.Length; i++)
        {
            GameObject target = hideWhileInspecting[i];
            if (target != null)
            {
                hiddenUiPreviousStates[i] = target.activeSelf;
                target.SetActive(false);
            }
        }

        hasStoredUiStates = true;
    }

    private void RestoreOtherUi()
    {
        if (!hasStoredUiStates || hideWhileInspecting == null || hiddenUiPreviousStates == null)
            return;

        int count = Mathf.Min(hideWhileInspecting.Length, hiddenUiPreviousStates.Length);
        for (int i = 0; i < count; i++)
        {
            GameObject target = hideWhileInspecting[i];
            if (target != null)
                target.SetActive(hiddenUiPreviousStates[i]);
        }

        hiddenUiPreviousStates = null;
        hasStoredUiStates = false;
    }

    private void DisableHiddenUiBackgroundRaycasts()
    {
        if (hideWhileInspecting == null)
            return;

        foreach (GameObject target in hideWhileInspecting)
        {
            if (target == null)
                continue;

            // SliderPanel처럼 화면 전체를 덮는 부모 이미지는 장식용입니다.
            // 부모만 클릭을 무시하고 자식 Button의 클릭은 그대로 유지합니다.
            Image background = target.GetComponent<Image>();
            if (background != null)
            {
                background.raycastTarget = false;
                Color color = background.color;
                color.a = 0f;
                background.color = color;
            }
        }
    }
}
