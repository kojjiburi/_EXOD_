using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 방 배경 위의 투명한 조사 영역에 붙입니다.
/// 클릭하면 물건 확대 화면과 조사 대사를 표시합니다.
/// </summary>
[RequireComponent(typeof(Image))]
public class InspectableItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("물건 정보")]
    [SerializeField] private string itemId = "bedroom_item";
    [SerializeField] private string itemName = "물건";
    [SerializeField] private Sprite detailSprite;

    [Header("대사")]
    [TextArea(2, 5)]
    [SerializeField] private string[] openingTexts = { "자세히 살펴보자." };
    [TextArea(2, 5)]
    [SerializeField] private string[] firstExamineTexts = { "특별한 점을 발견했다." };
    [TextArea(2, 5)]
    [SerializeField] private string[] repeatExamineTexts = { "더 이상 특별한 것은 없다." };

    [Header("화면 표시")]
    [Tooltip("포인터를 올렸을 때 조사 영역을 희미하게 표시합니다.")]
    [SerializeField] private bool showHoverHighlight = false;
    [Range(0f, 1f)]
    [SerializeField] private float hoverAlpha = 0.12f;

    [Header("조사 이벤트")]
    [Tooltip("이 물건을 처음 자세히 조사했을 때 한 번 실행됩니다.")]
    [SerializeField] private UnityEvent onFirstExamine;
    [Tooltip("자세히 조사할 때마다 실행됩니다.")]
    [SerializeField] private UnityEvent onEveryExamine;

    [Header("연결 기믹")]
    [Tooltip("자세히 조사했을 때 비밀번호 입력창을 엽니다.")]
    [SerializeField] private bool openPasswordPanelOnExamine;

    private Image clickAreaImage;

    public string ItemId => itemId;
    public string ItemName => itemName;
    public Sprite DetailSprite => detailSprite;
    public string[] OpeningTexts => openingTexts;

    private void Awake()
    {
        clickAreaImage = GetComponent<Image>();
        clickAreaImage.raycastTarget = true;
        SetAreaAlpha(0f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (InspectionManager.Instance == null)
        {
            Debug.LogError("씬에 InspectionManager가 없습니다.", this);
            return;
        }

        InspectionManager.Instance.Open(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (showHoverHighlight)
            SetAreaAlpha(hoverAlpha);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetAreaAlpha(0f);
    }

    public void Examine()
    {
        bool isFirstExamine = InspectionState.MarkExamined(itemId);
        string[] selectedTexts = isFirstExamine ? firstExamineTexts : repeatExamineTexts;

        ShowDialogue(selectedTexts);
        onEveryExamine?.Invoke();

        if (isFirstExamine)
            onFirstExamine?.Invoke();

        if (openPasswordPanelOnExamine)
        {
            PasswordDrawer passwordDrawer = FindFirstObjectByType<PasswordDrawer>(FindObjectsInactive.Include);
            if (passwordDrawer != null)
                passwordDrawer.Open();
        }
    }

    private void ShowDialogue(string[] texts)
    {
        if (texts == null || texts.Length == 0)
            return;

        if (DialogueManager.Instance == null)
        {
            Debug.LogError("씬에 DialogueManager가 없습니다.", this);
            return;
        }

        DialogueManager.Instance.StartInteractionDialogue(itemName, texts);
    }

    private void SetAreaAlpha(float alpha)
    {
        if (clickAreaImage == null)
            return;

        Color color = clickAreaImage.color;
        color.a = alpha;
        clickAreaImage.color = color;
    }
}
