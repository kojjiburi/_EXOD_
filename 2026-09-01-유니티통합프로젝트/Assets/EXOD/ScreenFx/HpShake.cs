using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하트(HP)가 깎일 때 화면 흔들림 (사용자 요청 2026-11-05)
/// 한 번 크게 쾅 → 왼쪽 → 오른쪽 → 느려지면서 왼쪽으로 조금 → 원래 자리.
/// 화면에 깔린 모든 UI 캔버스(방 화면·대화창·퀵슬롯 등)와 메인 카메라를 같이 옮깁니다.
/// 다른 코드가 같은 물체를 움직여도 망가지지 않게, 매 프레임 '이번에 더 움직일 만큼'만 더하고 끝나면 정확히 되돌립니다.
/// Stage1SoundEffects.OnHpLost에서 부릅니다.
/// </summary>
public sealed class HpShake : MonoBehaviour
{
    // (시간 초, 가로 위치) — 1920 기준 화면 픽셀. 시간 간격이 길어질수록 느려짐
    private static readonly Vector2[] Keys =
    {
        new Vector2(0f, 0f),
        new Vector2(0.05f, -46f),   // 쾅! 왼쪽으로 크게
        new Vector2(0.15f, 38f),    // 오른쪽으로
        new Vector2(0.36f, -14f),   // 느려지면서 왼쪽으로 조금
        new Vector2(0.60f, 0f),     // 원래 자리
    };
    private const float ReferenceWidth = 1920f;

    private static HpShake current;

    private readonly List<(Transform target, float unitsPerRefPixel)> targets = new List<(Transform, float)>();
    private float applied;   // 지금까지 더한 만큼 (1920 기준 픽셀)

    public static void Play()
    {
        if (current != null)
            current.Finish();
        GameObject go = new GameObject("[HP 화면 흔들림]");
        current = go.AddComponent<HpShake>();
        current.Begin();
    }

    private void Begin()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas == null || !canvas.isRootCanvas || !canvas.isActiveAndEnabled || canvas.renderMode == RenderMode.WorldSpace)
                continue;
            RectTransform root = (RectTransform)canvas.transform;
            float perPixel = root.rect.width > 1f ? root.rect.width / ReferenceWidth : 1f;
            foreach (Transform child in root)
                targets.Add((child, perPixel));
        }

        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
            targets.Add((cam.transform, cam.orthographicSize * 2f * cam.aspect / ReferenceWidth));

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        float t = 0f;
        float end = Keys[Keys.Length - 1].x;
        while (t < end)
        {
            t += Time.unscaledDeltaTime;
            MoveTo(Evaluate(t));
            yield return null;
        }
        Finish();
    }

    private static float Evaluate(float t)
    {
        for (int i = 1; i < Keys.Length; i++)
        {
            if (t <= Keys[i].x)
            {
                float k = Mathf.InverseLerp(Keys[i - 1].x, Keys[i].x, t);
                return Mathf.Lerp(Keys[i - 1].y, Keys[i].y, Mathf.SmoothStep(0f, 1f, k));
            }
        }
        return 0f;
    }

    private void MoveTo(float offset)
    {
        float delta = offset - applied;
        applied = offset;
        if (Mathf.Approximately(delta, 0f))
            return;
        foreach (var (target, perPixel) in targets)
        {
            if (target != null)
                target.localPosition += new Vector3(delta * perPixel, 0f, 0f);
        }
    }

    private void Finish()
    {
        MoveTo(0f);
        targets.Clear();
        if (current == this)
            current = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // 씬이 바뀌며 중간에 사라져도 제자리로
        if (applied != 0f)
            MoveTo(0f);
        if (current == this)
            current = null;
    }
}
