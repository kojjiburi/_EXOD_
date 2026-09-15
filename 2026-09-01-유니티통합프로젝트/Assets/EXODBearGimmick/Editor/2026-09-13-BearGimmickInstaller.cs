#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

internal static class BearGimmickInstaller
{
    [MenuItem("EXOD/곰돌이 기믹/설치 상태 확인")]
    private static void CheckInstallation()
    {
        bool hasSlider = Object.FindFirstObjectByType<SliderManager>() != null;
        bool hasInventory = Object.FindFirstObjectByType<QuickSlotInventory>() != null;
        bool hasDialogue = Object.FindFirstObjectByType<DialogueManager>() != null;

        string message =
            $"SliderManager: {(hasSlider ? "정상" : "없음")}\n" +
            $"QuickSlotInventory: {(hasInventory ? "정상" : "없음")}\n" +
            $"DialogueManager: {(hasDialogue ? "정상" : "없음")}\n\n" +
            "세 항목이 모두 정상이면 재생 시 곰돌이 기믹이 자동 생성됩니다.";

        EditorUtility.DisplayDialog("곰돌이 기믹 설치 상태", message, "확인");
    }
}
#endif
