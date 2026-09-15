using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class QuickSlotSlotView : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [SerializeField] private int slotIndex;
    [SerializeField] private Image iconImage;
    [SerializeField] private QuickSlotController controller;

    private QuickSlotEntry entry;
    private bool isDragging;

    public int SlotIndex => slotIndex;
    public Image IconImage => iconImage;

    public void Configure(QuickSlotController quickSlotController, int index, Image itemIcon)
    {
        controller = quickSlotController;
        slotIndex = index;
        iconImage = itemIcon;
    }

    public void Bind(QuickSlotEntry quickSlotEntry)
    {
        entry = quickSlotEntry;

        if (iconImage == null)
            return;

        Sprite icon = entry == null || entry.ItemData == null ? null : entry.ItemData.Icon;
        iconImage.sprite = icon;
        iconImage.enabled = icon != null && !isDragging;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            entry == null ||
            entry.ItemData == null ||
            entry.ItemData.Icon == null ||
            controller == null)
        {
            return;
        }

        Vector2 sourceSize = ((RectTransform)transform).rect.size;
        isDragging = controller.BeginDrag(entry, entry.ItemData.Icon, sourceSize, eventData);

        if (isDragging && iconImage != null)
            iconImage.enabled = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging)
            controller.UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;
        controller.CompleteDrag(entry, eventData.position);
    }

    private void OnDisable()
    {
        if (!isDragging)
            return;

        isDragging = false;
        controller?.CancelDragVisual();
    }
}
