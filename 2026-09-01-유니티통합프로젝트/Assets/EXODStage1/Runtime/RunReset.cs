using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// [다시 시작]/[로비로] 때 이번 판의 진행 상황을 모두 지웁니다. (엔딩 수집 기록 EndingCollection만 남김, 사용자 요청 2026-11-03)
/// 씬을 다시 불러와도 static 값은 남아 있어서 "일부 아이템이 사라진 채로 다시 안 나오는" 문제가 있었음.
/// 여러 팀원 파일의 private static 값을 이름으로 찾아 지우므로, 이름이 바뀌어도 에러 없이 건너뜁니다.
/// PlayerFaintController.ResetRunState가 부릅니다.
/// 남기는 것: 엔딩 수집(PlayerPrefs), 음량, 행동 요령 접힘, Tab UI 숨김 단계 같은 사용자 설정
/// </summary>
public static class RunReset
{
    public static void ResetAll()
    {
        // 조사·산책·전화 부스 진행
        InspectionState.ResetAll();
        EXOD.Walk.PhoneBoothProgress.ResetAll();

        // 산책 길에서 주운 아이템 기록(한 번 주우면 다시 안 나오던 것), 산책 상태
        ClearCollection("EXOD.Walk.WalkItemPickup", "PickedNames");
        ClearCollection("EXOD.Walk.WalkItemPickup", "pickedKeys");
        ClearCollection("EXOD.Walk.WalkItems", "fallbackOwned");
        Set("EXOD.Walk.DayClock", "lockedDay", int.MinValue);
        Set("EXOD.Walk.WalkGameManager", "firstIntroShown", false);
        Set("EXOD.Walk.PhoneBoothInterior", "firstEnterShown", false);
        Set("EXOD.Walk.PhoneBooth", "testKeyGiven", false);
        Set("EXOD.Walk.WalkSession", "LastResult", EXOD.Walk.WalkResult.None);

        // 거울 미로 (깬 기록·열쇠 주운 기록)
        Set("MirrorMazeGimmick", "IsSolved", false);
        Set("MirrorMazeGimmick", "RewardTaken", false);
        Set("MirrorMazeGimmick", "LastClue", null);
        ClearCollection("MirrorMazeReward", "Owned");

        // 청혼 엔딩·일차 끝 대사·대화창 그림 상태
        Set("ProposalEnding", "HasPlayed", false);
        Set("DayEndReport", "pendingReport", null);
        Set("DayEndReport", "healPending", false);
        Set("DayEndReport", "BearGiftDelivered", false);
        Set("DialoguePortrait", "sweetRequested", false);
        Set("DialoguePortrait", "sweetActive", false);
        Set("DialoguePortrait", "centerRequested", false);
        Set("DialoguePortrait", "HideGirlfriend", false);
        Set("DialogueDimmer", "Suppressed", false);
        Set("Stage1FridgeGimmick", "IsAnyInsideShown", false);

        // 퀵슬롯 비우기
        if (QuickSlotInventory.Instance != null)
            QuickSlotInventory.Instance.Clear();
    }

    private static Type FindType(string fullName)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(fullName, false);
            if (t != null)
                return t;
        }
        return null;
    }

    private const BindingFlags StaticAll = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static void Set(string typeName, string member, object value)
    {
        Type t = FindType(typeName);
        if (t == null)
            return;

        FieldInfo field = t.GetField(member, StaticAll);
        if (field != null && !field.IsInitOnly)
        {
            field.SetValue(null, value);
            return;
        }

        PropertyInfo prop = t.GetProperty(member, StaticAll);
        MethodInfo setter = prop != null ? prop.GetSetMethod(true) : null;
        if (setter != null)
        {
            setter.Invoke(null, new[] { value });
            return;
        }

        // 자동 속성의 숨은 필드
        FieldInfo backing = t.GetField("<" + member + ">k__BackingField", StaticAll);
        backing?.SetValue(null, value);
    }

    private static void ClearCollection(string typeName, string member)
    {
        Type t = FindType(typeName);
        FieldInfo field = t != null ? t.GetField(member, StaticAll) : null;
        if (field?.GetValue(null) is IList list)
            list.Clear();
        else if (field?.GetValue(null) is object set && set.GetType().GetMethod("Clear") is MethodInfo clear)
            clear.Invoke(set, null);
    }
}
