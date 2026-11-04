using System.Linq;
using UnityEditor;

/// <summary>
/// 2스테이지 산책 씬(Stage2_Walk)이 Build Settings 씬 목록에 없으면 자동으로 추가합니다.
/// (1스테이지 현관문에서 산책 씬을 Additive로 열려면 목록에 있어야 함)
/// </summary>
[InitializeOnLoad]
internal static class Stage2BuildSceneRegistrar
{
    private const string WalkScenePath = "Assets/EXODStage2Walk/Scenes/Stage2_Walk.unity";

    static Stage2BuildSceneRegistrar()
    {
        EditorApplication.delayCall += EnsureRegistered;
    }

    private static void EnsureRegistered()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WalkScenePath) == null)
            return;

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == WalkScenePath))
            return;

        EditorBuildSettings.scenes = scenes
            .Append(new EditorBuildSettingsScene(WalkScenePath, true))
            .ToArray();
        UnityEngine.Debug.Log("[Stage2] Build Settings 씬 목록에 Stage2_Walk를 추가했습니다.");
    }
}
