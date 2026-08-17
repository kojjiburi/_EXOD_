#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class Gimmick1GameplayUpgrader
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string GalmuriFontPath = "Assets/TextMesh Pro/Fonts/Galmuri9 SDFALL.asset";
    private const string OpenedDrawerPath = "Assets/EXOD/Gimmick1/Art/05_열린_서랍_열쇠.png";
    private const string EmptyDrawerPath = "Assets/EXOD/Gimmick1/Art/07_열린_서랍_빈.png";
    private const string TemporaryGoldenKeyAsset =
        "Assets/EXOD/Gimmick1/Items/GoldenKey.asset";

    static Gimmick1GameplayUpgrader()
    {
        EditorApplication.delayCall += StopPlayModeForTemporaryQuickSlotCleanup;
        QueueUpgrade();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                QueueUpgrade();
        };
    }

    [MenuItem("EXOD/Gimmick 1/Install Gameplay Features")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return;

        DialogueManager dialogueManager =
            Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        PasswordDrawer passwordDrawer =
            Object.FindFirstObjectByType<PasswordDrawer>(FindObjectsInactive.Include);

        if (dialogueManager == null || passwordDrawer == null)
            return;

        RemoveTemporaryQuickSlot();

        TMP_FontAsset galmuri =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);
        EnsureDialogueNextButton(dialogueManager, galmuri);
        ConfigurePasswordDrawer(passwordDrawer);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("EXOD: next button, key pickup, and persistent drawer installed.");
    }

    [MenuItem("EXOD/Gimmick 1/Reset Drawer Test Save")]
    public static void ResetDrawerTestSave()
    {
        PlayerPrefs.DeleteKey("EXOD.BedroomDrawer.Unlocked");
        PlayerPrefs.DeleteKey("EXOD.BedroomDrawer.KeyCollected");
        PlayerPrefs.DeleteKey("EXOD.Inventory.golden_key");
        PlayerPrefs.Save();
        Debug.Log("EXOD: drawer and golden key test save reset.");
    }

    private static void QueueUpgrade()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += Upgrade;
    }

    private static void StopPlayModeForTemporaryQuickSlotCleanup()
    {
        if (EditorApplication.isPlaying && GameObject.Find("EXOD_QuickSlot") != null)
            EditorApplication.isPlaying = false;
    }

    private static void RemoveTemporaryQuickSlot()
    {
        GameObject quickSlot = GameObject.Find("EXOD_QuickSlot");
        if (quickSlot != null)
            Object.DestroyImmediate(quickSlot);

        // 이전 작업에서 임시로 만든 자산만 제거합니다.
        // 다른 팀원이 만들 퀵슬롯이나 아이템 파일에는 접근하지 않습니다.
        if (AssetDatabase.LoadMainAssetAtPath(TemporaryGoldenKeyAsset) != null)
            AssetDatabase.DeleteAsset(TemporaryGoldenKeyAsset);
    }

    private static void EnsureDialogueNextButton(
        DialogueManager dialogueManager,
        TMP_FontAsset font)
    {
        SerializedObject managerData = new SerializedObject(dialogueManager);
        GameObject dialoguePanel =
            managerData.FindProperty("dialoguePanel").objectReferenceValue as GameObject;
        if (dialoguePanel == null)
            return;

        Transform existing = dialoguePanel.transform.Find("Dialogue Next Button");
        GameObject buttonObject = existing != null
            ? existing.gameObject
            : CreateUi("Dialogue Next Button", dialoguePanel.transform);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.sizeDelta = new Vector2(250f, 68f);
        rect.anchoredPosition = new Vector2(-18f, -18f);

        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
            image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.45f, 0.065f, 0.055f, 1f);
        image.raycastTarget = true;

        if (buttonObject.GetComponent<Button>() == null)
            buttonObject.AddComponent<Button>();
        if (buttonObject.GetComponent<DialogueNextButton>() == null)
            buttonObject.AddComponent<DialogueNextButton>();

        TextMeshProUGUI label =
            buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null)
            label = CreateText(buttonObject.transform, "Label", "다음", 30f, font);

        label.text = "다음";
        label.font = font;
        label.fontSize = 30f;
        label.fontStyle = FontStyles.Normal;
        label.fontWeight = FontWeight.Regular;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        Stretch(label.rectTransform);

        buttonObject.transform.SetAsLastSibling();
        dialoguePanel.transform.SetAsLastSibling();
    }

    private static void ConfigurePasswordDrawer(PasswordDrawer drawer)
    {
        SerializedObject data = new SerializedObject(drawer);
        data.FindProperty("openedDrawerSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(OpenedDrawerPath);
        data.FindProperty("emptyOpenedDrawerSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(EmptyDrawerPath);
        data.FindProperty("keyItemId").stringValue = "golden_key";
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(drawer);
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string value,
        float fontSize,
        TMP_FontAsset font)
    {
        GameObject textObject = CreateUi(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private static GameObject CreateUi(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
#endif
