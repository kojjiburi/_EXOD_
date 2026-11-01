using UnityEngine;
using UnityEngine.UI;

namespace EXOD.Walk
{
    /// <summary>
    /// 1스테이지(팀 UI)와 같은 모양의 화면 요소를 만드는 도우미.
    /// 팀 대화창 설정을 그대로 따른다: 1920×1080 기준, Galmuri9, 대화 글자 42 / 이름 36,
    /// 대화 글자색 짙은 빨강, 이름 글자색 빨강.
    /// </summary>
    public static class WalkUi
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        public static readonly Color DialogueTextColor = new Color(0.4716981f, 0.17710924f, 0.17710924f, 1f);
        public static readonly Color NameTextColor = new Color(1f, 0f, 0f, 1f);
        public static readonly Vector2 NameBoxSize = new Vector2(260f, 72f);

        static WalkUiTheme theme;
        static bool themeLoaded;
        static Font fallbackFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            theme = null;
            themeLoaded = false;
        }

        public static WalkUiTheme Theme
        {
            get
            {
                if (!themeLoaded)
                {
                    themeLoaded = true;
                    theme = Resources.Load<WalkUiTheme>("WalkUiTheme");
                    if (theme == null)
                        Debug.LogWarning("[Walk] Resources/WalkUiTheme 를 찾지 못해 기본 모양으로 표시합니다.");
                }
                return theme;
            }
        }

        public static Font Font
        {
            get
            {
                if (Theme != null && Theme.font != null) return Theme.font;
                if (fallbackFont == null) fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return fallbackFont;
            }
        }

        public static Sprite DialogueBoxSprite => Theme != null ? Theme.dialogueBox : null;
        public static Sprite NameBoxSprite => Theme != null ? Theme.nameBox : null;

        /// <summary>화면 전체를 덮는 오버레이 캔버스 (1920×1080 기준으로 크기 조절)</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color fallbackColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackColor;
            image.raycastTarget = false;
            return image;
        }

        public static Text CreateText(string name, Transform parent, int size, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>어두운 배경 위에서 읽기 쉽게 흰 글자 + 검은 테두리</summary>
        public static Text CreateOutlinedText(string name, Transform parent, int size, TextAnchor alignment)
        {
            var text = CreateText(name, parent, size, Color.white, alignment);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        /// <summary>팀 이름표 모양 상자 + 가운데 글자</summary>
        public static Text CreateNameBox(string name, Transform parent, out RectTransform box, int fontSize = 36, Color? textColor = null)
        {
            var image = CreateImage(name, parent, NameBoxSprite, new Color(0.82f, 0.82f, 0.82f, 1f));
            box = image.rectTransform;
            box.sizeDelta = NameBoxSize;
            var text = CreateText("글자", box, fontSize, textColor ?? NameTextColor, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, new Vector2(-28f, -12f));
            return text;
        }

        public static void Stretch(RectTransform rt, Vector2 sizeDelta = default)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
