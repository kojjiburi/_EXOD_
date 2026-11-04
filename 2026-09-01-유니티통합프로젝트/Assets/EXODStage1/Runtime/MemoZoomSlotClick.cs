using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>퀵슬롯 칸 클릭을 MemoZoom에 알려 줍니다. (드래그는 클릭으로 처리되지 않습니다)</summary>
[DisallowMultipleComponent]
public sealed class MemoZoomSlotClick : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging)
            return;

        QuickSlotSlotView slot = GetComponent<QuickSlotSlotView>();
        if (slot != null && MemoZoom.Instance != null)
            MemoZoom.Instance.OnSlotClicked(slot.SlotIndex);
    }
}
