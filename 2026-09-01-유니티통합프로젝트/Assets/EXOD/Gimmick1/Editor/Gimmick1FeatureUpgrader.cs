#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class Gimmick1FeatureUpgrader
{
    static Gimmick1FeatureUpgrader()
    {
        EditorApplication.delayCall += Upgrade;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += Upgrade;
        };
    }

    [MenuItem("EXOD/기믹 1 기능 갱신")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != "Assets/Scenes/SampleScene.unity")
            return;

        GameObject root = GameObject.Find("EXOD_기믹1");
        if (root == null)
            return;

        UpdateInspectableItems();
        UpdateInspectionBackButton(root.transform);
        EnsurePasswordPanel(root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static void UpdateInspectableItems()
    {
        InspectableItem[] items = Object.FindObjectsByType<InspectableItem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (InspectableItem item in items)
        {
            SerializedObject data = new SerializedObject(item);
            data.FindProperty("showHoverHighlight").boolValue = false;
            data.FindProperty("hoverAlpha").floatValue = 0f;

            SerializedProperty password = data.FindProperty("openPasswordPanelOnExamine");
            password.boolValue = item.ItemId == "bedroom_drawer";
            data.ApplyModifiedPropertiesWithoutUndo();

            RectTransform rect = item.GetComponent<RectTransform>();
            switch (item.ItemId)
            {
                case "bedroom_calendar":
                    SetNormalizedRect(rect, new Vector2(0.02f, 0.56f), new Vector2(0.23f, 0.97f));
                    break;
                case "bedroom_photo":
                    SetNormalizedRect(rect, new Vector2(0.34f, 0.43f), new Vector2(0.66f, 0.82f));
                    break;
                case "bedroom_memo":
                    SetNormalizedRect(rect, new Vector2(0.66f, 0.48f), new Vector2(0.90f, 0.88f));
                    break;
                case "bedroom_drawer":
                    SetNormalizedRect(rect, new Vector2(0.66f, 0.12f), new Vector2(0.93f, 0.58f));
                    break;
            }
        }
    }

    private static void SetNormalizedRect(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        if (rect == null) return;
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void UpdateInspectionBackButton(Transform root)
    {
        Transform buttonTransform = FindDescendantContaining(root, "닫기 버튼");
        if (buttonTransform == null)
            buttonTransform = FindDescendantContaining(root, "뒤로 가기 버튼");
        if (buttonTransform == null)
            return;

        buttonTransform.name = "뒤로 가기 버튼";
        RectTransform rect = buttonTransform as RectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(190f, 64f);
        rect.anchoredPosition = new Vector2(38f, -38f);

        TextMeshProUGUI label = buttonTransform.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = "← 뒤로";
            label.fontSize = 27f;
        }
    }

    private static void EnsurePasswordPanel(Transform root)
    {
        if (FindDescendantContaining(root, "비밀번호 입력 화면") != null)
            return;

        Transform controllerTransform = FindDescendantContaining(root, "조사 화면 관리자");
        if (controllerTransform == null)
            controllerTransform = root;

        GameObject controllerObject = CreateUi("비밀번호 시스템", controllerTransform);
        PasswordDrawer controller = controllerObject.AddComponent<PasswordDrawer>();

        GameObject panel = CreateUi("비밀번호 입력 화면", root);
        Stretch(panel.GetComponent<RectTransform>());
        Image dim = panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.88f);
        dim.raycastTarget = true;

        GameObject box = CreateUi("비밀번호 입력 상자", panel.transform);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(720f, 450f);
        boxRect.anchoredPosition = Vector2.zero;
        Image boxImage = box.AddComponent<Image>();
        boxImage.color = new Color(0.11f, 0.075f, 0.07f, 0.98f);

        CreateText(box.transform, "제목", "서랍 비밀번호", 38f,
            new Vector2(0.5f, 1f), new Vector2(620f, 65f), new Vector2(0f, -62f), Color.white);

        TextMeshProUGUI result = CreateText(box.transform, "안내 문구",
            "달력, 사진, 메모에서 찾은 숫자를 입력하자.", 24f,
            new Vector2(0.5f, 1f), new Vector2(620f, 60f), new Vector2(0f, -135f),
            new Color(0.88f, 0.80f, 0.72f));

        TMP_InputField input = CreateInputField(box.transform);
        Button confirm = CreateButton(box.transform, "확인 버튼", "확인",
            new Vector2(0.5f, 0f), new Vector2(210f, 66f), new Vector2(-125f, 48f));
        Button back = CreateButton(box.transform, "입력 취소 버튼", "뒤로",
            new Vector2(0.5f, 0f), new Vector2(210f, 66f), new Vector2(125f, 48f));

        Sprite openedDrawer = LoadSprite("Assets/EXOD/Gimmick1/Art/05_열린_서랍_열쇠.png");
        controller.Configure(panel, input, result, confirm, back, openedDrawer);
        panel.SetActive(false);
    }

    private static TMP_InputField CreateInputField(Transform parent)
    {
        GameObject fieldObject = CreateUi("세 자리 입력칸", parent);
        RectTransform rect = fieldObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 92f);
        rect.anchoredPosition = new Vector2(0f, -15f);

        Image background = fieldObject.AddComponent<Image>();
        background.color = new Color(0.92f, 0.88f, 0.80f, 1f);

        TMP_InputField input = fieldObject.AddComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 3;
        input.textViewport = rect;

        TextMeshProUGUI placeholder = CreateText(fieldObject.transform, "자리 표시", "000", 42f,
            new Vector2(0.5f, 0.5f), new Vector2(380f, 75f), Vector2.zero,
            new Color(0.35f, 0.30f, 0.28f, 0.45f));
        TextMeshProUGUI text = CreateText(fieldObject.transform, "입력 글자", string.Empty, 42f,
            new Vector2(0.5f, 0.5f), new Vector2(380f, 75f), Vector2.zero,
            new Color(0.18f, 0.06f, 0.05f));

        input.placeholder = placeholder;
        input.textComponent = text;
        input.caretColor = new Color(0.45f, 0.08f, 0.06f);
        input.selectionColor = new Color(0.55f, 0.18f, 0.14f, 0.35f);
        return input;
    }

    private static Button CreateButton(
        Transform parent, string name, string label,
        Vector2 anchor, Vector2 size, Vector2 position)
    {
        GameObject buttonObject = CreateUi(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.38f, 0.10f, 0.09f, 1f);
        Button button = buttonObject.AddComponent<Button>();

        TextMeshProUGUI text = CreateText(buttonObject.transform, "글자", label, 28f,
            new Vector2(0.5f, 0.5f), size, Vector2.zero, Color.white);
        text.raycastTarget = false;
        return button;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent, string name, string value, float fontSize,
        Vector2 anchor, Vector2 size, Vector2 position, Color color)
    {
        GameObject textObject = CreateUi(name, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.enableWordWrapping = true;
        return text;
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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

    private static Transform FindDescendantContaining(Transform root, string text)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains(text))
                return child;
        }
        return null;
    }
}
#endif
