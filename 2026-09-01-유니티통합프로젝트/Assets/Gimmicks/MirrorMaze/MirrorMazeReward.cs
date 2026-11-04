// 거울 미로 보상(전화부스 열쇠)을 팀 퀵슬롯에 넣어 주는 연결 코드.
//
// - 팀 통합 프로젝트: 팀 코드(ItemData, QuickSlotInventory)를 이름으로 찾아서
//   QuickSlotInventory.Instance.TryAdd(열쇠 ItemData) 를 호출한다 → 아래 퀵슬롯에 열쇠가 들어간다.
// - EXOD_3처럼 팀 코드가 없는 프로젝트: 얻었다는 기록만 남긴다 (Owned 목록).
// 팀 코드를 직접 참조하지 않으므로 어느 프로젝트에서도 컴파일 에러가 나지 않는다.
//
// 열쇠 ItemData 고르는 순서
//  1) 인스펙터에서 연결한 ItemData 에셋 (예: 스테이지2의 PhoneBooth_Key)
//  2) 이미 메모리에 올라와 있는 ItemData 중 ItemId 가 같은 것
//  3) 없으면 같은 ItemId·이름·아이콘으로 즉석에서 만든 ItemData

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class MirrorMazeReward
{
    /// <summary>팀 퀵슬롯이 없을 때 얻은 아이템 ID 기록</summary>
    public static readonly List<string> Owned = new List<string>();

    static readonly Dictionary<string, UnityEngine.Object> createdItems = new Dictionary<string, UnityEngine.Object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Owned.Clear(); createdItems.Clear(); }

    public enum Result { AddedToQuickSlot, RecordedOnly, InventoryFull }

    /// <summary>아이템을 준다. InventoryFull 이면 주지 못한 것.</summary>
    public static Result Give(string itemId, string displayName, Sprite icon, UnityEngine.Object itemAssetOverride)
    {
        Type itemType = FindType("ItemData");
        Type invType = FindType("QuickSlotInventory");

        if (itemType != null && invType != null)
        {
            object inventory = GetInventory(invType);
            if (inventory != null)
            {
                UnityEngine.Object item = ResolveItem(itemType, itemId, displayName, icon, itemAssetOverride);
                MethodInfo tryAdd = invType.GetMethod("TryAdd", BindingFlags.Public | BindingFlags.Instance, null, new[] { itemType }, null);
                if (item != null && tryAdd != null)
                {
                    bool ok = (bool)tryAdd.Invoke(inventory, new object[] { item });
                    if (!ok) return Result.InventoryFull;
                    Debug.Log($"[MirrorMazeReward] 퀵슬롯에 '{displayName}' ({itemId}) 추가");
                    return Result.AddedToQuickSlot;
                }
            }
            else Debug.LogWarning("[MirrorMazeReward] 팀 QuickSlotInventory 가 씬에 없어 기록만 남깁니다.");
        }

        if (!Owned.Contains(itemId)) Owned.Add(itemId);
        Debug.Log($"[MirrorMazeReward] '{displayName}' ({itemId}) 획득 (팀 퀵슬롯 없음 → 기록만)");
        return Result.RecordedOnly;
    }

    static object GetInventory(Type invType)
    {
        object inv = invType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (inv == null) inv = UnityEngine.Object.FindFirstObjectByType(invType);
        return inv;
    }

    static UnityEngine.Object ResolveItem(Type itemType, string itemId, string displayName, Sprite icon, UnityEngine.Object overrideAsset)
    {
        if (overrideAsset != null && itemType.IsInstanceOfType(overrideAsset)) return overrideAsset;

        PropertyInfo idProp = itemType.GetProperty("ItemId", BindingFlags.Public | BindingFlags.Instance);
        if (idProp != null)
            foreach (var o in Resources.FindObjectsOfTypeAll(itemType))
                if (string.Equals(idProp.GetValue(o) as string, itemId, StringComparison.Ordinal)) return o;

        if (createdItems.TryGetValue(itemId, out var cached) && cached != null) return cached;

        var created = ScriptableObject.CreateInstance(itemType);
        created.name = itemId;
        SetField(itemType, created, "itemId", itemId);
        SetField(itemType, created, "displayName", displayName);
        SetField(itemType, created, "icon", icon);
        SetField(itemType, created, "consumeOnSuccessfulUse", true);
        createdItems[itemId] = created;
        return created;
    }

    static void SetField(Type t, object target, string field, object value)
    {
        FieldInfo f = t.GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (f != null) f.SetValue(target, value);
    }

    static Type FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name, false);
            if (t != null) return t;
        }
        return null;
    }
}
