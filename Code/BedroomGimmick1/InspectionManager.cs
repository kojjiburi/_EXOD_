using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 물건 확대 화면을 열고 닫으며 현재 선택된 물건의 자세한 조사를 처리합니다.
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

    public bool IsOpen => inspectionPanel != null && inspectionPanel.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DisableHiddenUiBackgroundRaycasts();

        if (detailButton != null)
            detailButton.onClick.AddListener(ExamineCurrentItem);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (inspectionPanel != null)
            inspectionPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        RestoreOtherUi();

        if (detailButton != null)
            detailButton.onClick.RemoveListener(ExamineCurrentItem);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    public void Open(InspectableItem item)
    {
        if (item == null || inspectionPanel == null || detailImage == null)
            return;

        currentItem = item;
        detailImage.sprite = item.DetailSprite;
        detailImage.preserveAspect = true;
        inspectionPanel.SetActive(true);
        HideOtherUi();

        if (item.ItemId == "bedroom_drawer")
        {
            PasswordDrawer drawer = FindFirstObjectByType<PasswordDrawer>(FindObjectsInactive.Include);
            if (drawer != null)
                drawer.ApplyPersistentVisual();
        }

        if (item.OpeningTexts != null && item.OpeningTexts.Length > 0 && DialogueManager.Instance != null)
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

        if (currentItem != null)
            currentItem.Examine();
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
