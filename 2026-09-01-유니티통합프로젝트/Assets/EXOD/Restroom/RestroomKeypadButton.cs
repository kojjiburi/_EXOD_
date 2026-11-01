using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화장실 금고 키패드(RRPass)의 숫자 버튼 하나 (Pass1~9).
/// 눌리면 명도가 20 낮아지고(100 → 80), 키패드를 닫았다 다시 열 때까지 다시 누를 수 없습니다.
/// RestroomGimmick이 실행 중에 자동으로 붙입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class RestroomKeypadButton : MonoBehaviour, IPointerClickHandler
{
    private const float PressedBrightness = 0.8f;

    private RestroomGimmick owner;
    private Image image;
    private int number;
    private bool isPressed;

    public int Number => number;
    public bool IsPressed => isPressed;

    public void Setup(RestroomGimmick gimmick, int buttonNumber)
    {
        owner = gimmick;
        number = buttonNumber;
        image = GetComponent<Image>();
        ResetState();
    }

    public void ResetState()
    {
        isPressed = false;
        if (image != null)
            image.color = Color.white;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || isPressed || owner == null)
            return;

        isPressed = true;
        if (image != null)
            image.color = new Color(PressedBrightness, PressedBrightness, PressedBrightness, 1f);

        owner.OnKeypadPressed(number);
    }
}
