using EXOD.Walk;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 산책(2스테이지) 중 퀵슬롯 아이템을 화면에 끌어다 놓으면, 그 자리를 "그 아이템을 들고 클릭"한 것으로 산책 기믹에 알립니다.
/// (전화 부스: 열쇠 → 부스, 테이프·다이얼 버튼 → 부스 안 전화기, 주소지 → 수화기)
/// 팀 퀵슬롯 코드는 고치지 않고, 칸마다 이 컴포넌트를 덧붙여서 끌기 시작/끝을 함께 받습니다.
/// 퀵슬롯 칸은 나중에 만들어지기도 해서 Stage2HouseLink가 산책 중에 주기적으로 붙입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WalkQuickSlotDropBridge : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    private ItemData draggingItem;

    public void OnBeginDrag(PointerEventData eventData)
    {
        draggingItem = null;
        if (!Stage2HouseLink.IsInStage2)
            return;

        // 팀 OnEndDrag가 먼저 칸을 새로 그릴 수 있으므로, 끌기 시작할 때 아이템을 기억해 둡니다.
        QuickSlotSlotView slot = GetComponent<QuickSlotSlotView>();
        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        if (slot == null || inventory == null || slot.SlotIndex < 0 || slot.SlotIndex >= inventory.Count)
            return;

        QuickSlotEntry entry = inventory.Entries[slot.SlotIndex];
        draggingItem = entry != null ? entry.ItemData : null;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ItemData item = draggingItem;
        draggingItem = null;
        if (item == null || !Stage2HouseLink.IsInStage2)
            return;

        // 퀵슬롯 바 위에 다시 놓았으면 무시 (끌고 있는 아이콘 그림은 판정에서 빼려고 바 영역만 봅니다)
        QuickSlotController bar = GetComponentInParent<QuickSlotController>();
        if (bar != null)
        {
            Canvas canvas = bar.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)bar.transform, eventData.position, uiCamera))
                return;
        }

        WalkGameManager manager = WalkGameManager.Current;
        if (manager != null)
            manager.HandleItemDrop(item, eventData.position);
    }

    /// <summary>열려 있는 퀵슬롯 칸 모두에 붙입니다.</summary>
    public static void AttachToAllSlots()
    {
        foreach (QuickSlotSlotView slot in FindObjectsByType<QuickSlotSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (slot.GetComponent<WalkQuickSlotDropBridge>() == null)
                slot.gameObject.AddComponent<WalkQuickSlotDropBridge>();
        }
    }
}
