using UnityEngine;

/// <summary>글자를 계속 덜덜 떨게 함 (부스 모드 하드 카드 경고)</summary>
public sealed class BoothShake : MonoBehaviour
{
    private RectTransform rect;
    private Vector2 basePos;
    private float next;

    private void Start()
    {
        rect = (RectTransform)transform;
        basePos = rect.anchoredPosition;
    }

    private void Update()
    {
        if (rect == null || Time.unscaledTime < next)
            return;
        next = Time.unscaledTime + 0.04f;
        rect.anchoredPosition = basePos + new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(-1.8f, 1.8f));
    }
}
