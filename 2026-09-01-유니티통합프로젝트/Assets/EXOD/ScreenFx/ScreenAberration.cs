using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 여자친구한테 들켰을 때 화면 색수차 연출.
/// 지금 화면을 찍어서 그 위에 빨강·파랑이 갈라진 그림을 덮습니다. 가운데는 거의 그대로, 가장자리로 갈수록 크게 갈라집니다.
/// 처음에 세게 터졌다가 조금 줄어든 채로 계속 떨립니다. 대화창은 그 위에 또렷하게 그려집니다.
///  - 1스테이지: 침실 밖에서 들킨 대사(VisitCaught) 동안 (DangerLevel)
///  - 2스테이지: 산책 중 들킨 뒤 대사가 끝날 때까지 (DangerLevel)
/// 이 동안 대화창의 여자친구 초상화는 띄우지 않습니다 (DialoguePortrait.HideGirlfriend).
/// [컷신 이미지 자리] 아래 파일을 넣으면 색수차 화면 위·대화창 아래에 화면 가득(비율 유지) 천천히 나타나고, 다 나타난 뒤 대사가 시작됩니다. 없으면 안 보임.
///  - 1스테이지 방 밖에서 들킴: Assets/EXOD/ScreenFx/Resources/EXODScreenFx/CaughtCutscene_House.png (넣음)
///  - 2스테이지 산책 중 들킴:  Assets/EXOD/ScreenFx/Resources/EXODScreenFx/CaughtCutscene_Walk.png
///  (Texture Type은 Sprite (2D and UI)로)
/// 셰이더: Assets/EXOD/ScreenFx/Resources/EXODScreenFx/ChromaticAberrationUI.shader
/// </summary>
public sealed class ScreenAberration : MonoBehaviour
{
    private const int SortingOrder = 30000;        // 모든 UI(하트·퀵슬롯·종료 버튼 등) 위, 일차 넘김 검은 화면(32000) 아래. 대화창만 그 위에 그림
    private const int DialogueOrder = SortingOrder + 1;
    private const string ShaderPath = "EXODScreenFx/ChromaticAberrationUI";
    public const string HouseCutscene = "EXODScreenFx/CaughtCutscene_House";
    public const string WalkCutscene = "EXODScreenFx/CaughtCutscene_Walk";

    private const float PeakStrength = 0.30f;      // 처음 터질 때 (화면 모서리에서 화면 폭의 약 15%만큼 갈라짐)
    private const float HoldStrength = 0.16f;      // 그 뒤 유지
    private const float PeakSeconds = 0.6f;        // 처음 세기에서 유지 세기로 줄어드는 시간
    private const float WobbleAmount = 0.3f;       // 유지 중 출렁임 (유지 세기의 ±30%)
    private const float WobbleSpeed = 9f;
    private const float JitterAmount = 0.012f;     // 가끔 화면이 툭 튀는 정도
    private const float JitterChancePerSecond = 6f;
    private const float FadeInSeconds = 0.08f;
    private const float FadeOutSeconds = 0.35f;
    private const float CutsceneFadeSeconds = 1.2f;   // 컷신 이미지가 천천히 나타나는 시간 (다 나타난 뒤 대사 시작)
    private const float WaitDialogueOpenSeconds = 5f;
    private const float SafetySeconds = 40f;

    private static ScreenAberration instance;

    private Canvas canvas;
    private CanvasGroup group;
    private RawImage image;
    private Image cutsceneImage;
    private Material material;
    private Material cutsceneMaterial;
    private Texture2D shot;
    private Coroutine routine;
    private bool stopRequested;

    private static readonly int StrengthId = Shader.PropertyToID("_Strength");
    private static readonly int JitterId = Shader.PropertyToID("_Jitter");

    public static bool IsPlaying => instance != null && instance.routine != null;

    /// <summary>
    /// 화면을 찍고 색수차를 켭니다. 찍은 뒤 afterCapture를 부릅니다 (여기서 대사를 시작하면 찍힌 화면에 대사창이 안 들어감).
    /// stopWhenDialogueCloses가 true면 대사가 열렸다가 닫힐 때 저절로 꺼지고, false면 Stop()을 불러야 꺼집니다.
    /// </summary>
    public static void Play(Action afterCapture, bool stopWhenDialogueCloses, string cutsceneResource = null)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("들킴 색수차 효과");
            instance = go.AddComponent<ScreenAberration>();
        }

        instance.Begin(afterCapture, stopWhenDialogueCloses, cutsceneResource);
    }

    public static void Stop()
    {
        if (instance != null)
            instance.stopRequested = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        DialogueOnTop.Lower(DialogueOrder);
        DialoguePortrait.HideGirlfriend = false;
        if (shot != null)
            Destroy(shot);
        if (material != null)
            Destroy(material);
        if (cutsceneMaterial != null)
            Destroy(cutsceneMaterial);
    }

    private void Begin(Action afterCapture, bool stopWhenDialogueCloses, string cutsceneResource)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        stopRequested = false;
        routine = StartCoroutine(Run(afterCapture, stopWhenDialogueCloses, cutsceneResource));
    }

    private IEnumerator Run(Action afterCapture, bool stopWhenDialogueCloses, string cutsceneResource)
    {
        EnsureOverlay();
        DialoguePortrait.HideGirlfriend = true;

        // 이번 프레임이 다 그려진 뒤 화면을 찍음
        yield return new WaitForEndOfFrame();
        if (shot != null)
            Destroy(shot);
        shot = ScreenCapture.CaptureScreenshotAsTexture();
        shot.wrapMode = TextureWrapMode.Clamp;
        image.texture = shot;
        image.enabled = material != null;

        // 컷신 이미지 (넣어 두었으면)
        Sprite cutscene = string.IsNullOrEmpty(cutsceneResource) ? null : Resources.Load<Sprite>(cutsceneResource);
        cutsceneImage.sprite = cutscene;
        cutsceneImage.enabled = cutscene != null;
        cutsceneImage.color = new Color(1f, 1f, 1f, 0f);

        canvas.gameObject.SetActive(true);
        group.alpha = 0f;
        DialogueOnTop.Raise(DialogueOrder);

        float t = 0f;
        Vector2 jitter = Vector2.zero;

        // 방 밖에서 들킴·산책 중 들킴 컷신 효과음 (1/4 볼륨)
        if (cutsceneResource == HouseCutscene || cutsceneResource == WalkCutscene)
            EXODSound.Play("do_not_open", 0.25f);

        // 컷신 이미지가 천천히 다 나타난 뒤에 대사 시작
        if (cutscene != null)
        {
            float fadeIn = 0f;
            while (fadeIn < CutsceneFadeSeconds && !stopRequested)
            {
                group.alpha = Mathf.Clamp01(t / FadeInSeconds);
                cutsceneImage.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, fadeIn / CutsceneFadeSeconds));
                Animate(t, ref jitter);
                fadeIn += Time.unscaledDeltaTime;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            cutsceneImage.color = Color.white;
        }

        try
        {
            afterCapture?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        bool dialogueOpened = false;
        float waitStart = t;
        while (!stopRequested)
        {
            bool open = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen;
            if (open)
                dialogueOpened = true;
            if (stopWhenDialogueCloses && ((dialogueOpened && !open) || (!dialogueOpened && t - waitStart > WaitDialogueOpenSeconds)))
                break;
            if (t > SafetySeconds)
                break;

            group.alpha = Mathf.Clamp01(t / FadeInSeconds);
            Animate(t, ref jitter);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // 천천히 사라짐
        float fade = 0f;
        float startAlpha = group.alpha;
        while (fade < FadeOutSeconds)
        {
            fade += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(startAlpha, 0f, fade / FadeOutSeconds);
            Animate(t + fade, ref jitter);
            yield return null;
        }

        group.alpha = 0f;
        canvas.gameObject.SetActive(false);
        DialogueOnTop.Lower(DialogueOrder);
        DialoguePortrait.HideGirlfriend = false;
        image.texture = null;
        cutsceneImage.sprite = null;
        if (shot != null)
        {
            Destroy(shot);
            shot = null;
        }

        routine = null;
    }

    private void Animate(float t, ref Vector2 jitter)
    {
        if (material == null)
            return;

        float strength = t < PeakSeconds
            ? Mathf.Lerp(PeakStrength, HoldStrength, Mathf.SmoothStep(0f, 1f, t / PeakSeconds))
            : HoldStrength * (1f + WobbleAmount * Mathf.Sin(t * WobbleSpeed));

        // 가끔 툭 튀었다가 금방 돌아옴
        if (UnityEngine.Random.value < JitterChancePerSecond * Time.unscaledDeltaTime)
            jitter = UnityEngine.Random.insideUnitCircle * JitterAmount;
        else
            jitter = Vector2.Lerp(jitter, Vector2.zero, 12f * Time.unscaledDeltaTime);

        material.SetFloat(StrengthId, strength);
        material.SetVector(JitterId, new Vector4(jitter.x, jitter.y, 0f, 0f));
        if (cutsceneMaterial != null)
        {
            cutsceneMaterial.SetFloat(StrengthId, strength);
            cutsceneMaterial.SetVector(JitterId, new Vector4(jitter.x, jitter.y, 0f, 0f));
        }
    }

    private void EnsureOverlay()
    {
        if (canvas != null)
            return;

        GameObject canvasObject = new GameObject("색수차 화면", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        group = canvasObject.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject imageObject = new GameObject("찍은 화면", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        image = imageObject.GetComponent<RawImage>();
        image.raycastTarget = false;

        // 컷신 이미지 자리 (색수차 화면 위, 대화창 아래)
        GameObject cutsceneObject = new GameObject("컷신 이미지", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cutsceneObject.transform.SetParent(canvasObject.transform, false);
        RectTransform cutsceneRect = cutsceneObject.GetComponent<RectTransform>();
        cutsceneRect.anchorMin = Vector2.zero;
        cutsceneRect.anchorMax = Vector2.one;
        cutsceneRect.offsetMin = Vector2.zero;
        cutsceneRect.offsetMax = Vector2.zero;
        cutsceneImage = cutsceneObject.GetComponent<Image>();
        cutsceneImage.preserveAspect = true;
        cutsceneImage.raycastTarget = false;
        cutsceneImage.enabled = false;

        Shader shader = Resources.Load<Shader>(ShaderPath);
        if (shader != null)
        {
            material = new Material(shader);
            image.material = material;
            // 컷신 이미지에도 같은 색수차 → 컷신 동안 계속 갈라져 보임 (2스테이지처럼, 사용자 요청 2026-11-03)
            cutsceneMaterial = new Material(shader);
            cutsceneMaterial.SetFloat("_IsScreenshot", 0f);
            cutsceneImage.material = cutsceneMaterial;
        }
        else
        {
            Debug.LogWarning("[ScreenAberration] 색수차 셰이더를 찾지 못했습니다: Resources/" + ShaderPath);
        }

        canvasObject.SetActive(false);
    }
}
