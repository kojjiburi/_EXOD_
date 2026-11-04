using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 실행 중 발견한 조사 단서를 기억합니다.
/// 씬을 다시 시작하면 초기화됩니다.
/// </summary>
public static class InspectionState
{
    private static readonly HashSet<string> examinedIds = new HashSet<string>();
    private static readonly HashSet<string> openingShownIds = new HashSet<string>();

    /// <summary>처음 열 때 나오는 안내 대사를 이번에 처음 보여 주는 거면 true.</summary>
    public static bool MarkOpeningShown(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && openingShownIds.Add(itemId);
    }

    public static bool IsExamined(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && examinedIds.Contains(itemId);
    }

    public static bool MarkExamined(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogWarning("조사 물건의 식별자가 비어 있습니다.");
            return false;
        }

        return examinedIds.Add(itemId);
    }

    public static void ResetAll()
    {
        examinedIds.Clear();
        openingShownIds.Clear();
    }
}
