using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 퀵슬롯 칸을 클릭했을 때 손전등 기믹에 알려 줍니다.
/// FlashlightGimmick이 실행 중에 각 QuickSlotSlotView에 자동으로 붙입니다.
/// (드래그로 아이템을 옮길 때는 클릭으로 처리되지 않습니다.)
/// </summary>
[DisallowMultipleComponent]
public sealed class FlashlightSlotClickHandler : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        FlashlightGimmick gimmick = FlashlightGimmick.Instance;
        if (gimmick == null)
            return;

        // 우클릭: 비출 물건 고르기를 취소합니다.
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            gimmick.CancelTargetSelection();
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        QuickSlotSlotView slotView = GetComponent<QuickSlotSlotView>();
        if (slotView == null)
            return;

        gimmick.OnQuickSlotClicked(FlashlightGimmick.GetEntryAtSlot(slotView.SlotIndex));
    }
}
