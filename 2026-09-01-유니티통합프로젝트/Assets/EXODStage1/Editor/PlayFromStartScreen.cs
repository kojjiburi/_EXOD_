using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 편집기에서 어떤 씬을 열어 두든 Play를 누르면 시작 화면(StartScenes)부터 실행합니다.
/// 실제 빌드와 똑같은 순서로 테스트하기 위한 설정입니다.
/// </summary>
[InitializeOnLoad]
public static class PlayFromStartScreen
{
    private const string StartScenePath = "Assets/EXOD/TeamMerge/Scenes/StartScenes.unity";

    static PlayFromStartScreen()
    {
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(StartScenePath);
    }
}
