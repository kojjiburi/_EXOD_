using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 숫자 키패드 창 (냉장고, 현관 도어락이 함께 씁니다). 처음 쓸 때 자동으로 만들어집니다.
/// - 마우스로 누르거나 키보드 숫자로 입력, Backspace 지우기, Esc·X 닫기
/// - 자릿수를 다 채우면 자동으로 확인합니다.
/// </summary>
public sealed class Stage1Keypad : MonoBehaviour
{
    private static readonly Color DeviceColor = new Color(0.10f, 0.10f, 0.11f, 1f);
    private static readonly Color KeyColor = new Color(0.26f, 0.26f, 0.28f, 1f);
    private static readonly Color ScreenColor = new Color(0.10f, 0.17f, 0.12f, 1f);
    private static readonly Color ScreenTextColor = new Color(0.55f, 0.95f, 0.60f, 1f);
    private static readonly Color LedLocked = new Color(0.85f, 0.12f, 0.12f, 1f);
    private static readonly Color LedOpen = new Color(0.20f, 0.90f, 0.30f, 1f);

    private static Stage1Keypad instance;

    private GameObject root;
    private TextMeshProUGUI screenText;
    private Image led;
    private Sprite roundedSprite;

    private string password;
    private string entered = string.Empty;
    private bool isChecking;
    private Action onCorrect;
    private Action onWrong;

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    /// <summary>
    /// 키패드를 엽니다. 맞으면 창을 닫고 onCorrect를 호출합니다.
    /// 틀렸을 때 onWrong이 있으면 창을 닫고 onWrong을 호출하고, 없으면 다시 입력받습니다.
    /// </summary>
    public static void Show(string password, Action onCorrect, Action onWrong = null)
    {
        // [위험도] 틀리면 100이 될 상황이면 경고만 하고 이번엔 열지 않음
        if (DangerLevel.WarnBeforeGimmick())
            return;

        if (instance == null)
            instance = new GameObject("키패드").AddComponent<Stage1Keypad>();

        instance.Open(password, onCorrect, onWrong);
    }

    private void Awake()
    {
        roundedSprite = CreateRoundedSprite();
        Build();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (!root.activeSelf || isChecking)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
            PressBack();

        foreach (char c in Input.inputString)
        {
            if (c >= '0' && c <= '9')
                PressDigit(c);
        }
    }

    private void Open(string newPassword, Action correct, Action wrong)
    {
        password = newPassword;
        onCorrect = correct;
        onWrong = wrong;
        entered = string.Empty;
        isChecking = false;
        led.color = LedLocked;
        RefreshScreen();
        root.SetActive(true);
    }

    private void Close()
    {
        StopAllCoroutines();
        isChecking = false;
        root.SetActive(false);
    }

    private void PressDigit(char digit)
    {
        if (isChecking || entered.Length >= password.Length)
            return;

        EXODSound.Play("keypad_press");
        entered += digit;
        RefreshScreen();

        if (entered.Length == password.Length)
            StartCoroutine(Check());
    }

    private void PressBack()
    {
        if (isChecking || entered.Length == 0)
            return;

        EXODSound.Play("keypad_press", 0.6f);
        entered = entered.Substring(0, entered.Length - 1);
        RefreshScreen();
    }

    private IEnumerator Check()
    {
        isChecking = true;
        yield return new WaitForSecondsRealtime(0.25f);

        if (entered == password)
        {
            EXODSound.Play("doorlock_beep");
            led.color = LedOpen;
            screenText.text = "OPEN";
            yield return new WaitForSecondsRealtime(0.8f);

            Action callback = onCorrect;
            Close();
            DangerLevel.GimmickSolved();
            callback?.Invoke();
            yield break;
        }

        EXODSound.Play("keypad_error");
        DangerLevel.GimmickFailed();
        for (int i = 0; i < 3; i++)
        {
            screenText.text = "ERROR";
            yield return new WaitForSecondsRealtime(0.15f);
            screenText.text = string.Empty;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        if (onWrong != null)
        {
            Action callback = onWrong;
            Close();
            callback();
            yield break;
        }

        entered = string.Empty;
        RefreshScreen();
        isChecking = false;
    }

    private void RefreshScreen()
    {
        string text = string.Empty;
        for (int i = 0; i < password.Length; i++)
        {
            text += i < entered.Length ? entered[i].ToString() : "_";
            if (i < password.Length - 1)
                text += " ";
        }

        screenText.text = text;
    }

    // ═══════════════════════════════════════════════════
    //  UI 만들기
    // ═══════════════════════════════════════════════════

    private void Build()
    {
        root = new GameObject("키패드 화면", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;  // 퀵슬롯·하트 HUD 위, HP 감소 효과·기절 화면 아래

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image dim = CreateImage("어둡게", root.transform, new Color(0f, 0f, 0f, 0.65f));
        Stretch(dim.rectTransform);

        Image body = CreateRounded("키패드 본체", root.transform, DeviceColor);
        Place(body.rectTransform, Vector2.zero, new Vector2(440f, 640f));
        Outline outline = body.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.32f, 0.32f, 0.34f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        Image screen = CreateRounded("화면", body.transform, ScreenColor);
        Place(screen.rectTransform, new Vector2(0f, 222f), new Vector2(360f, 100f));
        screenText = CreateText("화면 글자", screen.transform, string.Empty, 58f, ScreenTextColor);
        Stretch(screenText.rectTransform);

        led = CreateRounded("상태 불빛", body.transform, LedLocked);
        led.type = Image.Type.Simple;
        Place(led.rectTransform, new Vector2(150f, 290f), new Vector2(18f, 18f));

        string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "<", "0", "X" };
        for (int i = 0; i < labels.Length; i++)
        {
            int row = i / 3;
            int column = i % 3;
            Vector2 position = new Vector2(-115f + column * 115f, 105f - row * 105f);
            string label = labels[i];
            UnityEngine.Events.UnityAction action =
                label == "<" ? PressBack :
                label == "X" ? Close :
                (UnityEngine.Events.UnityAction)(() => PressDigit(label[0]));
            CreateKey(body.transform, label, position, action);
        }

        root.SetActive(false);
    }

    private void CreateKey(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        Image key = CreateRounded(label, parent, Color.white);
        Place(key.rectTransform, position, new Vector2(96f, 86f));

        Button button = key.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = KeyColor;
        colors.highlightedColor = new Color(0.36f, 0.36f, 0.39f, 1f);
        colors.pressedColor = new Color(0.55f, 0.55f, 0.58f, 1f);
        colors.selectedColor = KeyColor;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        Color textColor = label == "X" ? new Color(0.95f, 0.45f, 0.45f, 1f) : new Color(0.92f, 0.92f, 0.92f, 1f);
        TextMeshProUGUI text = CreateText("글자", key.transform, label, 44f, textColor);
        Stretch(text.rectTransform);
    }

    private Image CreateRounded(string name, Transform parent, Color color)
    {
        Image image = CreateImage(name, parent, color);
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        return image;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (EXODUiAssets.Font != null)
            text.font = EXODUiAssets.Font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 모서리가 둥근 사각형 (9분할로 늘려 써도 모서리 모양이 유지됩니다)
    private static Sprite CreateRoundedSprite()
    {
        const int size = 32;
        const float radius = 10f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius), 0f);
                float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius), 0f);
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
