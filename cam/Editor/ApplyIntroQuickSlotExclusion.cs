#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ApplyIntroQuickSlotExclusion
{
    private const string ScriptPath = "Assets/Code/UI/QuickSlotPersistentRoot.cs";
    private const string OriginalCondition = "if (scene.name == ExcludedSceneName)";
    private const string UpdatedCondition = "if (scene.name == ExcludedSceneName || scene.name == \"Intro\")";

    static ApplyIntroQuickSlotExclusion()
    {
        EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        if (!File.Exists(ScriptPath))
        {
            Debug.LogError("[QuickSlot] 제외 설정 대상 스크립트를 찾지 못했습니다.");
            return;
        }

        string source = File.ReadAllText(ScriptPath);
        if (source.Contains(UpdatedCondition))
            return;

        if (!source.Contains(OriginalCondition))
        {
            Debug.LogError("[QuickSlot] Intro 제외 조건을 적용할 위치를 찾지 못했습니다.");
            return;
        }

        File.WriteAllText(ScriptPath, source.Replace(OriginalCondition, UpdatedCondition));
        AssetDatabase.ImportAsset(ScriptPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[QuickSlot] Intro 씬을 퀵슬롯 제외 목록에 추가했습니다.");
    }
}
#endif
