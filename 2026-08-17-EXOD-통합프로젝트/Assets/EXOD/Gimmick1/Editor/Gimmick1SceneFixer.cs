#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Gimmick1SceneFixer
{
    private static bool isChangingPlayMode;

    static Gimmick1SceneFixer()
    {
        isChangingPlayMode =
            EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode;
        EditorApplication.delayCall += FixCurrentSampleScene;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            isChangingPlayMode = false;
            EditorApplication.delayCall += FixCurrentSampleScene;
        }
        else
        {
            isChangingPlayMode = true;
        }
    }

    [MenuItem("EXOD/기믹 1 화면 순서 고치기 _F8")]
    public static void FixCurrentSampleScene()
    {
        if (isChangingPlayMode ||
            EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != "Assets/Scenes/SampleScene.unity")
            return;

        GameObject root = GameObject.Find("EXOD_기믹1");
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (root == null || canvas == null)
            return;

        StretchToParent(root.GetComponent<RectTransform>());

        Transform oldBackground = FindDirectChildContaining(canvas.transform, "배경");
        if (oldBackground != null && !oldBackground.IsChildOf(root.transform))
            oldBackground.gameObject.SetActive(false);

        root.transform.SetAsLastSibling();

        Transform character = FindDirectChildContaining(canvas.transform, "캐릭터");
        if (character != null)
            character.gameObject.SetActive(false);

        Transform dialogue = FindDirectChildContaining(canvas.transform, "대화창");
        if (dialogue != null)
            dialogue.SetAsLastSibling();

        Transform namePanel = FindDirectChildContaining(canvas.transform, "이름표");
        if (namePanel != null)
            namePanel.SetAsLastSibling();

        Transform slider = FindDirectChildContaining(canvas.transform, "SliderPanel");
        if (slider != null)
            slider.SetAsLastSibling();

        // 지연 호출 사이에 재생 전환이 시작될 수 있으므로 저장 직전에 다시 확인합니다.
        if (isChangingPlayMode ||
            EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;
        }
        return null;
    }

    private static Transform FindDirectChildContaining(Transform parent, string text)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.Contains(text))
                return child;
        }
        return null;
    }

    private static void StretchToParent(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}
#endif
