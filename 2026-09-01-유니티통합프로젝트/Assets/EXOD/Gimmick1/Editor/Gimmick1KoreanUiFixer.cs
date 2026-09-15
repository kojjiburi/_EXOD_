#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gimmick 1 UI repair pass.
/// Uses the same Korean TMP font as the existing dialogue UI and keeps the
/// inspection back button large and visible at the upper-right corner.
/// </summary>
[InitializeOnLoad]
public static class Gimmick1KoreanUiFixer
{
    private const string TargetScene = "Assets/Scenes/SampleScene.unity";
    private const string Galmuri9FontPath =
        "Assets/TextMesh Pro/Fonts/Galmuri9 SDFALL.asset";
    private const string RepairVersionKey =
        "EXOD.Gimmick1.UiRepair.20260726.4";

    static Gimmick1KoreanUiFixer()
    {
        // 이번 UI 갱신이 재생 중 들어온 경우에만 재생을 한 번 종료합니다.
        // Repair가 완료되면 버전 키가 저장되므로 이후 재생은 절대 자동 종료하지 않습니다.
        if (EditorApplication.isPlaying && !EditorPrefs.GetBool(RepairVersionKey, false))
        {
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        }
        else
        {
            QueueRepair();
        }

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("EXOD/Gimmick 1/Repair Korean UI")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != TargetScene)
            return;

        InspectionManager manager = Object.FindFirstObjectByType<InspectionManager>(FindObjectsInactive.Include);
        if (manager == null)
            return;

        Transform gimmickRoot = manager.transform.parent != null
            ? manager.transform.parent
            : manager.transform;

        // 다음 버튼이 없으면 먼저 만든 뒤 최종 디자인을 적용합니다.
        Gimmick1GameplayUpgrader.Upgrade();
        ApplyGalmuri9Font(gimmickRoot);
        RemoveBoldFromAllText();
        RepairBackButton(manager);
        RepairPasswordButtons(gimmickRoot);
        RepairNavigationButtons();
        RepairDialogueLayout();
        RepairHiddenUiBackgrounds(manager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetBool(RepairVersionKey, true);
        Debug.Log("EXOD: 조사 화면 입력과 UI 디자인을 갱신했습니다.");
    }

    private static void QueueRepair()
    {
        // Run after the original installer/upgrader has finished its own pass.
        EditorApplication.delayCall += () => EditorApplication.delayCall += Repair;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            QueueRepair();
    }

    private static void ApplyGalmuri9Font(Transform root)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Galmuri9FontPath);
        if (font == null)
        {
            Debug.LogWarning("EXOD: Galmuri9 TMP font asset was not found: " + Galmuri9FontPath);
            return;
        }

        TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI label in labels)
        {
            label.font = font;
            EditorUtility.SetDirty(label);
        }
    }

    private static void RemoveBoldFromAllText()
    {
        TextMeshProUGUI[] labels = Object.FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (TextMeshProUGUI label in labels)
        {
            label.fontStyle = FontStyles.Normal;
            label.fontWeight = FontWeight.Regular;
            EditorUtility.SetDirty(label);
        }
    }

    private static void RepairBackButton(InspectionManager manager)
    {
        SerializedObject managerData = new SerializedObject(manager);
        SerializedProperty closeButtonProperty = managerData.FindProperty("closeButton");
        Button button = closeButtonProperty != null
            ? closeButtonProperty.objectReferenceValue as Button
            : null;

        if (button == null)
            return;

        button.gameObject.name = "Inspection Back Button";
        button.transform.SetAsLastSibling();

        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(300f, 96f);
            rect.anchoredPosition = new Vector2(-34f, -34f);
            rect.localScale = Vector3.one;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.44f, 0.06f, 0.055f, 0.98f);
            image.raycastTarget = true;
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            // "뒤로가기" written as escapes to make the source encoding independent.
            label.text = "\uB4A4\uB85C\uAC00\uAE30";
            label.fontSize = 38f;
            label.fontStyle = FontStyles.Normal;
            label.fontWeight = FontWeight.Regular;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        EditorUtility.SetDirty(button);
    }

    private static void RepairPasswordButtons(Transform root)
    {
        PasswordDrawer drawer = root.GetComponentInChildren<PasswordDrawer>(true);
        if (drawer == null)
            return;

        SerializedObject drawerData = new SerializedObject(drawer);
        SetButtonLabel(drawerData.FindProperty("confirmButton"), "\uD655\uC778");
        SetButtonLabel(drawerData.FindProperty("closeButton"), "\uB4A4\uB85C");
    }

    private static void RepairHiddenUiBackgrounds(InspectionManager manager)
    {
        SerializedObject managerData = new SerializedObject(manager);
        SerializedProperty targets = managerData.FindProperty("hideWhileInspecting");
        if (targets == null || !targets.isArray)
            return;

        for (int i = 0; i < targets.arraySize; i++)
        {
            GameObject target = targets.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            if (target == null)
                continue;

            Image background = target.GetComponent<Image>();
            if (background == null)
                continue;

            Color color = background.color;
            color.a = 0f;
            background.color = color;
            background.raycastTarget = false;
            EditorUtility.SetDirty(background);
        }
    }

    private static void SetButtonLabel(SerializedProperty property, string text)
    {
        Button button = property != null ? property.objectReferenceValue as Button : null;
        if (button == null)
            return;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null)
            return;

        label.text = text;
        label.fontSize = 32f;
        label.fontStyle = FontStyles.Normal;
        label.fontWeight = FontWeight.Regular;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        EditorUtility.SetDirty(label);
    }

    private static void RepairNavigationButtons()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Galmuri9FontPath);

        SliderManager sliderManager =
            Object.FindFirstObjectByType<SliderManager>(FindObjectsInactive.Include);
        if (sliderManager != null)
        {
            sliderManager.isUnlocked = true;
            if (sliderManager.sliderPanel != null)
                sliderManager.sliderPanel.SetActive(true);
            EditorUtility.SetDirty(sliderManager);
        }

        ConfigureNavigationButton(GameObject.Find("LeftButton"), true, font);
        ConfigureNavigationButton(GameObject.Find("RightButton"), false, font);
    }

    private static void ConfigureNavigationButton(
        GameObject buttonObject,
        bool isLeft,
        TMP_FontAsset font)
    {
        if (buttonObject == null)
            return;

        RectTransform rect = buttonObject.transform as RectTransform;
        if (rect != null)
        {
            Vector2 anchor = new Vector2(isLeft ? 0f : 1f, 0.5f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(110f, 220f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        // 기존 반원 스프라이트는 그대로 사용하고 검은 반투명 배경으로 보이게 합니다.
        Image background = buttonObject.GetComponent<Image>();
        if (background != null)
        {
            background.color = new Color(0f, 0f, 0f, 0.62f);
            background.raycastTarget = true;
        }

        Transform glyphTransform = buttonObject.transform.Find("Navigation Arrow");
        GameObject glyphObject = glyphTransform != null
            ? glyphTransform.gameObject
            : new GameObject("Navigation Arrow", typeof(RectTransform));

        if (glyphTransform == null)
            glyphObject.transform.SetParent(buttonObject.transform, false);

        TextMeshProUGUI glyph = glyphObject.GetComponent<TextMeshProUGUI>();
        if (glyph == null)
            glyph = glyphObject.AddComponent<TextMeshProUGUI>();

        glyph.text = isLeft ? "<" : ">";
        glyph.font = font;
        glyph.fontSize = 66f;
        glyph.fontStyle = FontStyles.Normal;
        glyph.fontWeight = FontWeight.Regular;
        glyph.alignment = TextAlignmentOptions.Center;
        glyph.color = Color.white;
        glyph.raycastTarget = false;

        RectTransform glyphRect = glyph.rectTransform;
        glyphRect.anchorMin = Vector2.zero;
        glyphRect.anchorMax = Vector2.one;
        glyphRect.offsetMin = Vector2.zero;
        glyphRect.offsetMax = Vector2.zero;

        EditorUtility.SetDirty(buttonObject);
        EditorUtility.SetDirty(glyph);
    }

    private static void RepairDialogueLayout()
    {
        DialogueManager dialogueManager =
            Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        if (dialogueManager == null)
            return;

        SerializedObject data = new SerializedObject(dialogueManager);
        GameObject panel = data.FindProperty("dialoguePanel").objectReferenceValue as GameObject;
        TextMeshProUGUI dialogueText =
            data.FindProperty("dialogueText").objectReferenceValue as TextMeshProUGUI;
        GameObject namePanel =
            data.FindProperty("namePanel").objectReferenceValue as GameObject;
        TextMeshProUGUI nameText =
            data.FindProperty("nameText").objectReferenceValue as TextMeshProUGUI;
        if (panel == null)
            return;

        RectTransform panelRect = panel.transform as RectTransform;
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(-420f, 240f);
            panelRect.anchoredPosition = new Vector2(0f, 150f);
            panelRect.localScale = Vector3.one;
        }

        if (dialogueText != null)
        {
            RectTransform textRect = dialogueText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(36f, 24f);
            textRect.offsetMax = new Vector2(-292f, -24f);
            dialogueText.fontStyle = FontStyles.Normal;
            dialogueText.fontWeight = FontWeight.Regular;
            dialogueText.fontSize = 42f;
            dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
            dialogueText.raycastTarget = false;
            EditorUtility.SetDirty(dialogueText);
        }

        // 이름표의 아래쪽 모서리를 줄어든 대사창의 왼쪽 위에 정확히 붙입니다.
        if (namePanel != null)
        {
            RectTransform nameRect = namePanel.transform as RectTransform;
            if (nameRect != null)
            {
                nameRect.anchorMin = Vector2.zero;
                nameRect.anchorMax = Vector2.zero;
                nameRect.pivot = Vector2.zero;
                nameRect.sizeDelta = new Vector2(260f, 72f);
                nameRect.anchoredPosition = new Vector2(210f, 270f);
                nameRect.localScale = Vector3.one;
                EditorUtility.SetDirty(nameRect);
            }

            EditorUtility.SetDirty(namePanel);
        }

        if (nameText != null)
        {
            RectTransform nameTextRect = nameText.rectTransform;
            nameTextRect.anchorMin = Vector2.zero;
            nameTextRect.anchorMax = Vector2.one;
            nameTextRect.offsetMin = new Vector2(14f, 6f);
            nameTextRect.offsetMax = new Vector2(-14f, -6f);
            EditorUtility.SetDirty(nameTextRect);
            nameText.fontSize = 36f;
            nameText.fontStyle = FontStyles.Normal;
            nameText.fontWeight = FontWeight.Regular;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.raycastTarget = false;
            EditorUtility.SetDirty(nameText);
        }

        Transform nextTransform = panel.transform.Find("Dialogue Next Button");
        if (nextTransform != null)
        {
            RectTransform nextRect = nextTransform as RectTransform;
            nextRect.anchorMin = Vector2.one;
            nextRect.anchorMax = Vector2.one;
            nextRect.pivot = Vector2.one;
            nextRect.sizeDelta = new Vector2(250f, 68f);
            nextRect.anchoredPosition = new Vector2(-18f, -18f);

            Image image = nextTransform.GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.45f, 0.065f, 0.055f, 1f);

            TextMeshProUGUI label =
                nextTransform.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = "\uB2E4\uC74C";
                label.fontSize = 30f;
                label.fontStyle = FontStyles.Normal;
                label.fontWeight = FontWeight.Regular;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                EditorUtility.SetDirty(label);
            }
        }

        EditorUtility.SetDirty(panel);
    }
}
#endif
