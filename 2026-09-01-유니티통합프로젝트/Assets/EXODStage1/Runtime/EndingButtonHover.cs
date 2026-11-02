using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>엔딩 화면 버튼: 마우스를 올리면 살짝 커지고 엔딩 색으로 빛나며, 누르면 눌린 듯 작아집니다.</summary>
public sealed class EndingButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private static readonly Color NormalColor = new Color(0.015f, 0.005f, 0.01f, 1f);

    private Image background;
    private Outline outline;
    private Image accent;
    private Func<Color> themeColor;
    private bool hovered;
    private bool pressed;
    private float amount;

    public void Setup(Image backgroundImage, Outline border, Image accentBar, Func<Color> theme)
    {
        background = backgroundImage;
        outline = border;
        accent = accentBar;
        themeColor = theme;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;

    private void OnDisable()
    {
        hovered = pressed = false;
        amount = 0f;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        amount = Mathf.MoveTowards(amount, hovered ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        Color theme = themeColor != null ? themeColor() : Color.white;

        float scale = 1f + 0.07f * amount - (pressed ? 0.05f : 0f);
        transform.localScale = Vector3.one * scale;

        if (background != null)
            background.color = Color.Lerp(NormalColor, new Color(theme.r * 0.25f, theme.g * 0.25f, theme.b * 0.25f, 1f), amount);
        if (outline != null)
            outline.effectColor = Color.Lerp(new Color(1f, 1f, 1f, 0.35f), new Color(theme.r, theme.g, theme.b, 0.95f), amount);
        if (accent != null)
        {
            accent.color = Color.Lerp(new Color(1f, 1f, 1f, 0.8f), theme, amount);
            accent.rectTransform.sizeDelta = new Vector2(6f + 10f * amount, 0f);
        }
    }
}
