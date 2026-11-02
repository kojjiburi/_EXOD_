using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 일차 끝 점검에 걸렸을 때(제자리에 없는 물건이 있을 때) 기믹으로 얻은 물건까지 모두 압수 (사용자 결정 2026-11-03)
/// - ItemPositionManager가 점검 대상 물건(황금 열쇠·수건·손전등·종이 등)을 처음 자리로 돌린 뒤(ItemsResetForNewDay) 이어서:
///   · 곰돌이 팔다리(아직 안 돌려준 것) → 원래 자리
///   · 전화 부스 열쇠 → 거울 미로 안 선반 (미로는 깬 채로, 열쇠만 다시 주우면 됨)
///   · 부스 부품(테이프·버튼 1·2) → 산책 길 원래 자리 / 주소지 → 가로등 번호판을 다시 확인해야 함
///   · 현관문 → 다시 잠김 (비밀번호부터 다시)
/// - 그대로 두는 것: 곰돌이 메모(냉장고 비밀번호 쪽지), 열린 서랍·금고·냉장고·전화 부스(문·수리 상태)
/// - 경찰 신고에 성공한 뒤에는 점검 자체를 안 함 (ItemPositionManager.SkipDayEndCheck)
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
public sealed class Stage1ConfiscateOnFail : MonoBehaviour
{
    private static readonly string[] WalkPartIds =
    {
        "stage2_insulating_tape",
        "stage2_dial_button_1",
        "stage2_dial_button_2",
    };
    private const string AddressNoteId = "stage2_address_note";

    private ItemPositionManager subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;
        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null && slider.GetComponent<Stage1ConfiscateOnFail>() == null)
            slider.gameObject.AddComponent<Stage1ConfiscateOnFail>();
    }

    private void Update()
    {
        if (subscribed == null && ItemPositionManager.Instance != null)
        {
            subscribed = ItemPositionManager.Instance;
            subscribed.ItemsResetForNewDay += Confiscate;
        }
    }

    private void OnDestroy()
    {
        if (subscribed != null)
            subscribed.ItemsResetForNewDay -= Confiscate;
    }

    private void Confiscate()
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance;

        // 곰돌이 팔다리
        BearLimbGimmickManager bear = FindFirstObjectByType<BearLimbGimmickManager>();
        if (bear != null)
            bear.ConfiscateHeldLimbs();

        // 전화 부스 열쇠 → 거울 미로 안 선반으로
        if (RemoveFromInventory(inventory, Stage1Dialogues.BoothKeyItemId))
        {
            SetStatic(typeof(MirrorMazeGimmick), "RewardTaken", false);
            Stage1MirrorBoothKey mirror = FindFirstObjectByType<Stage1MirrorBoothKey>();
            if (mirror != null)
            {
                FieldInfo keyGiven = typeof(Stage1MirrorBoothKey).GetField("keyGiven", BindingFlags.NonPublic | BindingFlags.Instance);
                keyGiven?.SetValue(mirror, false);
            }
        }

        // 부스 부품 → 산책 길에서 다시 주울 수 있게
        foreach (string id in WalkPartIds)
        {
            ItemData item = FindItem(inventory, id);
            if (item != null && RemoveFromInventory(inventory, id))
                EXOD.Walk.WalkItemPickup.Forget(item);
        }

        // 주소지 → 가로등 번호판을 다시 확인해야 받음
        if (RemoveFromInventory(inventory, AddressNoteId))
        {
            EXOD.Walk.PhoneBoothProgress.AddressObtained = false;
            EXOD.Walk.PhoneBoothProgress.LampsChecked.Clear();
        }

        // 현관문 다시 잠김
        Stage1FrontDoorGimmick door = FindFirstObjectByType<Stage1FrontDoorGimmick>();
        if (door != null)
            door.Relock();

        Debug.Log("[Confiscate] 점검에 걸림 → 기믹 물건까지 압수, 현관문 다시 잠김 (곰돌이 메모는 그대로)");
    }

    private static ItemData FindItem(QuickSlotInventory inventory, string id)
    {
        if (inventory == null)
            return null;
        QuickSlotEntry entry = inventory.Entries.FirstOrDefault(e => e != null && e.ItemData != null && e.ItemData.ItemId == id);
        return entry != null ? entry.ItemData : null;
    }

    private static bool RemoveFromInventory(QuickSlotInventory inventory, string id)
    {
        if (inventory == null)
            return false;
        bool removed = false;
        foreach (QuickSlotEntry entry in inventory.Entries.ToArray())
        {
            if (entry != null && entry.ItemData != null && entry.ItemData.ItemId == id)
                removed |= inventory.TryRemove(entry);
        }
        return removed;
    }

    private static void SetStatic(System.Type type, string property, object value)
    {
        PropertyInfo prop = type.GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo setter = prop != null ? prop.GetSetMethod(true) : null;
        setter?.Invoke(null, new[] { value });
    }
}
