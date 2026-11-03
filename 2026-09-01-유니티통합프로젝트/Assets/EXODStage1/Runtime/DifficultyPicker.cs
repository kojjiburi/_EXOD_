using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [다시 시작]을 눌렀을 때 이지/하드를 다시 고르는 창 (사용자 요청 2026-11-03)
/// 엔딩·게임 오버 화면 위에 뜹니다. [뒤로]를 누르면 닫히고 엔딩 화면으로 돌아갑니다.
/// PlayerFaintController.Restart가 부릅니다.
/// </summary>
public static class DifficultyPicker
{
    private static readonly Color TextColor = new Color(0.78f, 0.74f, 0.76f, 1f);
    private static readonly Color TitleColor = new Color(0.62f, 0.04f, 0.08f, 1f);

    private static GameObject root;

    public static bool IsOpen => root != null && root.activeSelf;

    public static void Show(Action<bool> onPicked)
    {
        if (root != null)
            UnityEngine.Object.Destroy(root);

        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        root = new GameObject("난이도 다시 고르기", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32500;   // 엔딩·일차 넘김 검은 화면(32000) 위
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform rootRect = root.GetComponent<RectTransform>();

        Image dim = Img("어둡게", rootRect, new Color(0f, 0f, 0f, 0.82f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        bool booth = BoothMode.IsOn;
        Image window = Img("창", dim.rectTransform, new Color(0.07f, 0.04f, 0.05f, 1f));
        Place(window.rectTransform, booth ? BoothMode.BoothWindowSize : new Vector2(1200f, 600f), Vector2.zero);
        Outline outline = window.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.06f, 0.1f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        TextMeshProUGUI heading = Txt("제목", window.rectTransform, "난이도 선택", 52f, TitleColor);
        Place(heading.rectTransform, new Vector2(800f, 90f), new Vector2(0f, booth ? 290f : 230f));

        if (booth)
        {
            Card(window.rectTransform, new Vector2(-290f, 5f), "이지", null,
                new Color(0.55f, 0.85f, 0.65f, 1f), () => Pick(onPicked, true), true);
            Card(window.rectTransform, new Vector2(290f, 5f), "하드", null,
                new Color(0.88f, 0.12f, 0.18f, 1f), () => Pick(onPicked, false), false);
        }
        else
        {
            Card(window.rectTransform, new Vector2(-270f, -10f), "이지", "처음 하는 사람용\n짧게 끝낼 수 있어요",
                new Color(0.55f, 0.85f, 0.65f, 1f), () => Pick(onPicked, true));
            Card(window.rectTransform, new Vector2(270f, -10f), "하드", "원래 난이도\n모든 단서를 직접 찾아야 해요",
                new Color(0.88f, 0.12f, 0.18f, 1f), () => Pick(onPicked, false));
        }

        Button back = Btn(window.rectTransform, "뒤로", new Vector2(200f, 64f), new Vector2(0f, booth ? -305f : -240f));
        back.onClick.AddListener(Close);
    }

    public static void Close()
    {
        if (root != null)
            UnityEngine.Object.Destroy(root);
        root = null;
    }

    private static void Pick(Action<bool> onPicked, bool easy)
    {
        Close();
        BoothMode.StartTimer(easy);   // 부스 운영 모드면 제한시간 다시 시작
        onPicked?.Invoke(easy);
    }

    private static void Card(RectTransform parent, Vector2 pos, string name, string desc, Color accent, UnityEngine.Events.UnityAction onClick,
        bool? boothEasy = null)
    {
        Image card = Img(name, parent, new Color(0.13f, 0.08f, 0.10f, 1f));
        Place(card.rectTransform, boothEasy.HasValue ? BoothMode.BoothCardSize : new Vector2(460f, 260f), pos);
        Outline o = card.gameObject.AddComponent<Outline>();
        o.effectColor = accent;
        o.effectDistance = new Vector2(3f, -3f);
        Button b = card.gameObject.AddComponent<Button>();
        ColorBlock c = b.colors;
        c.highlightedColor = new Color(1f, 0.85f, 0.9f, 1f);
        c.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = c;
        b.onClick.AddListener(onClick);
        EXODSound.AttachUiSounds(card.gameObject);

        if (boothEasy.HasValue)
        {
            BoothMode.FillDifficultyCard(card.rectTransform, boothEasy.Value, accent, EXODUiAssets.Font);
            return;
        }

        TextMeshProUGUI t = Txt("이름", card.rectTransform, name, 64f, accent);
        Place(t.rectTransform, new Vector2(420f, 100f), new Vector2(0f, 60f));
        TextMeshProUGUI d = Txt("설명", card.rectTransform, desc, 28f, TextColor);
        Place(d.rectTransform, new Vector2(420f, 110f), new Vector2(0f, -55f));
    }

    private static Button Btn(RectTransform parent, string label, Vector2 size, Vector2 pos)
    {
        Image img = Img(label, parent, Color.white);
        Place(img.rectTransform, size, pos);
        Button b = img.gameObject.AddComponent<Button>();
        ColorBlock c = b.colors;
        c.normalColor = new Color(0.20f, 0.14f, 0.14f, 1f);
        c.highlightedColor = new Color(0.45f, 0.16f, 0.20f, 1f);
        c.pressedColor = new Color(0.62f, 0.20f, 0.26f, 1f);
        c.selectedColor = c.normalColor;
        b.colors = c;
        EXODSound.AttachUiSounds(img.gameObject);
        TextMeshProUGUI t = Txt("글자", img.rectTransform, label, 30f, Color.white);
        Stretch(t.rectTransform);
        return b;
    }

    private static Image Img(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image i = go.GetComponent<Image>();
        i.color = color;
        return i;
    }

    private static TextMeshProUGUI Txt(string name, Transform parent, string value, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (EXODUiAssets.Font != null)
            t.font = EXODUiAssets.Font;
        t.text = value;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    private static void Place(RectTransform r, Vector2 size, Vector2 pos)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
