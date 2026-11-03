using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀵슬롯의 쪽지·종이(메모) 아이템을 클릭하면 크게 확대해서 보여 줍니다.
/// - 대상: 냉장고 비밀번호 쪽지, 종이 A~E (paper_)
/// - 아무 곳이나 클릭하거나 Esc로 닫습니다.
/// - 손전등으로 비출 물건을 고르는 중에는 손전등 기믹이 클릭을 쓰므로 열지 않습니다.
/// 퀵슬롯 칸마다 MemoZoomSlotClick을 자동으로 붙입니다.
/// </summary>
[DefaultExecutionOrder(275)]
public sealed class MemoZoom : MonoBehaviour
{
    private const string PaperItemPrefix = "paper_";

    private static readonly HashSet<string> MemoItemIds = new HashSet<string>
    {
        Stage1Dialogues.FridgeNoteItemId,
    };

    private GameObject viewer;
    private Image memoImage;
    private TextMeshProUGUI nameText;
    private float nextAttachTime;

    public static MemoZoom Instance { get; private set; }
    public bool IsOpen => viewer != null && viewer.activeSelf;

    public static bool IsMemo(ItemData item)
    {
        if (item == null)
            return false;

        string id = item.ItemId;
        return MemoItemIds.Contains(id) || id.StartsWith(PaperItemPrefix);
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();

        // 퀵슬롯 칸은 씬을 넘어가도 유지되거나 나중에 만들어지므로 가끔 확인해서 붙입니다.
        if (Time.unscaledTime >= nextAttachTime)
        {
            nextAttachTime = Time.unscaledTime + 1f;
            foreach (QuickSlotSlotView slot in FindObjectsByType<QuickSlotSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (slot.GetComponent<MemoZoomSlotClick>() == null)
                    slot.gameObject.AddComponent<MemoZoomSlotClick>();
            }
        }
    }

    /// <summary>퀵슬롯 칸이 클릭됐을 때 (MemoZoomSlotClick)</summary>
    public void OnSlotClicked(int slotIndex)
    {
        if (IsOpen || IsBusy())
            return;

        QuickSlotEntry entry = GetEntryAtSlot(slotIndex);
        if (entry == null || !IsMemo(entry.ItemData) || entry.ItemData.Icon == null)
            return;

        Open(entry.ItemData);
    }

    private static bool IsBusy()
    {
        FlashlightGimmick flashlight = FlashlightGimmick.Instance;
        return (flashlight != null && (flashlight.IsSelectingTarget || flashlight.IsViewerOpen))
            || (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            || (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy)
            || Stage1Keypad.IsOpen;
    }

    // 퀵슬롯은 얻은 순서대로 칸에 표시됩니다. (QuickSlotController.Refresh와 같은 순서)
    private static QuickSlotEntry GetEntryAtSlot(int slotIndex)
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance != null
            ? QuickSlotInventory.Instance
            : FindFirstObjectByType<QuickSlotInventory>();
        if (inventory == null || slotIndex < 0)
            return null;

        return inventory.Entries
            .Where(entry => entry != null)
            .OrderBy(entry => entry.AcquiredSequence)
            .ElementAtOrDefault(slotIndex);
    }

    private void Open(ItemData item)
    {
        Sprite sprite = item.Icon;
        memoImage.sprite = sprite;

        // 퀵슬롯 위 공간(최대 1200x620)에 비율을 지키며 최대한 크게
        float scale = Mathf.Min(1200f / sprite.rect.width, 620f / sprite.rect.height);
        memoImage.rectTransform.sizeDelta = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);

        nameText.text = item.DisplayName;
        EXODSound.Play("inspect");
        viewer.SetActive(true);
    }

    // 여자친구 등장 등으로 강제로 닫을 때도 씀 (GimmickInterrupt)
    public void Close()
    {
        viewer.SetActive(false);
    }

    // ═══════════════════════════════════════════════════
    //  UI 만들기
    // ═══════════════════════════════════════════════════

    private void Build()
    {
        GameObject root = new GameObject("메모 확대", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 720;  // 퀵슬롯(500)·키패드(700) 위, 기절 화면(1000) 아래

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        viewer = new GameObject("확대 화면", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        viewer.transform.SetParent(root.transform, false);
        RectTransform viewerRect = viewer.GetComponent<RectTransform>();
        viewerRect.anchorMin = Vector2.zero;
        viewerRect.anchorMax = Vector2.one;
        viewerRect.offsetMin = Vector2.zero;
        viewerRect.offsetMax = Vector2.zero;
        viewer.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.92f);
        viewer.GetComponent<Button>().onClick.AddListener(Close);  // 아무 곳이나 누르면 닫힘

        GameObject imageObject = new GameObject("메모", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(viewer.transform, false);
        memoImage = imageObject.GetComponent<Image>();
        memoImage.preserveAspect = true;
        memoImage.raycastTarget = false;
        RectTransform imageRect = memoImage.rectTransform;
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = new Vector2(0f, 110f);

        nameText = CreateText("이름", viewer.transform, string.Empty, 34f, new Color(0.90f, 0.87f, 0.88f, 1f));
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0f);
        nameRect.anchoredPosition = new Vector2(0f, 285f);
        nameRect.sizeDelta = new Vector2(1000f, 50f);

        TextMeshProUGUI hint = CreateText("안내", viewer.transform, "아무 곳이나 누르거나 Esc로 닫기", 22f, new Color(0.62f, 0.60f, 0.62f, 1f));
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 245f);
        hintRect.sizeDelta = new Vector2(1000f, 40f);

        viewer.SetActive(false);
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (EXODUiAssets.Font != null)
            text.font = EXODUiAssets.Font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }
}
