using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>엔딩 종류별 분위기 (색·떠다니는 빛·소리)</summary>
public enum EndingStyle
{
    GameOver,   // 검붉은 배경, 떠오르는 불씨
    Submission, // 순응 엔딩: 짙은 분홍, 둥둥 떠다니는 빛 방울, 오르골
    Escape,     // 탈출 엔딩: 남색, 경찰차 빨강·파랑 불빛
    Proposal,   // 청혼 엔딩: 결혼식 분위기, 흩날리는 분홍 꽃잎·하트, 리본, 오르골
}

/// <summary>
/// 모든 엔딩·게임 오버의 마지막 화면 (제목·설명·[다시 시작]/[로비로]/[게임 종료]).
/// 배경 숨쉬기 + 가장자리 어둡게 + 떠다니는 빛 + 장식선 펼치기 + 제목 한 글자씩·글리치 + 버튼 차례로 올라오기.
/// PlayerFaintController가 만들고 띄웁니다. (전화 부스 탈출 엔딩도 통합 프로젝트에서는 이 화면을 씁니다)
/// </summary>
public sealed class EndingScreen : MonoBehaviour
{
    private const int ParticleCount = 46;

    private CanvasGroup group;
    private Image background;
    private Image glow;
    private Image flashLeft;
    private Image flashRight;
    private RectTransform particleRoot;
    private readonly List<RectTransform> particles = new List<RectTransform>();
    private readonly List<Vector2> particleSpeed = new List<Vector2>();
    private Image lineTop;
    private Image lineBottom;
    private TextMeshProUGUI title;
    private TextMeshProUGUI ghostRed;
    private TextMeshProUGUI ghostCyan;
    private TextMeshProUGUI subtitle;
    private readonly List<CanvasGroup> buttons = new List<CanvasGroup>();
    private readonly List<Image> particleImages = new List<Image>();
    private Image titleBand;
    private readonly List<TextMeshProUGUI> titleOutline = new List<TextMeshProUGUI>();
    private readonly List<Vector2> titleOutlineOffsets = new List<Vector2>();
    private Image heartLeft;
    private Image heartRight;
    private Image bandAidA;
    private Image bandAidB;
    private Sprite dotSprite;
    private Sprite heartSprite;
    private Sprite brokenHeartSprite;
    private Sprite petalSprite;
    private Sprite bandAidSprite;
    private Sprite ribbonSprite;

    private EndingStyle style;
    private Color baseColor;
    private Color glowColor;
    private Color particleColor;
    private bool idle;
    private float nextGlitchTime;

    public bool IsShowing => gameObject.activeSelf;

    // ═══════════════════════════════════════════════════
    //  만들기
    // ═══════════════════════════════════════════════════

    public static EndingScreen Create(Transform parent, Action restart, Action lobby, Action quit)
    {
        GameObject root = new GameObject("엔딩 화면", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        Stretch((RectTransform)root.transform);
        EndingScreen screen = root.AddComponent<EndingScreen>();
        screen.Build(restart, lobby, quit);
        root.SetActive(false);
        return screen;
    }

    private void Build(Action restart, Action lobby, Action quit)
    {
        group = GetComponent<CanvasGroup>();

        background = CreateImage("배경", transform, Color.black);
        Stretch(background.rectTransform);
        background.raycastTarget = true;   // 뒤쪽 클릭 막기

        // 가운데에서 퍼지는 빛 (숨쉬듯 밝아졌다 어두워짐)
        glow = CreateImage("가운데 빛", transform, Color.white);
        glow.sprite = RadialSprite(false);
        glow.raycastTarget = false;
        RectTransform glowRect = glow.rectTransform;
        glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.55f);
        glowRect.sizeDelta = new Vector2(2200f, 1500f);

        // 경찰차 불빛 (탈출 엔딩)
        flashLeft = CreateSideFlash("왼쪽 불빛", 0f);
        flashRight = CreateSideFlash("오른쪽 불빛", 1f);

        particleRoot = new GameObject("떠다니는 빛", typeof(RectTransform)).GetComponent<RectTransform>();
        particleRoot.SetParent(transform, false);
        Stretch(particleRoot);
        dotSprite = RadialSprite(true);
        heartSprite = HeartSprite(false);
        brokenHeartSprite = HeartSprite(true);
        petalSprite = PetalSprite();
        ribbonSprite = RibbonSprite();
        for (int i = 0; i < ParticleCount; i++)
        {
            Image p = CreateImage("빛 " + i, particleRoot, Color.white);
            p.sprite = dotSprite;
            p.raycastTarget = false;
            particles.Add(p.rectTransform);
            particleImages.Add(p);
            particleSpeed.Add(Vector2.zero);
        }

        // 가장자리 어둡게
        Image vignette = CreateImage("가장자리 어둡게", transform, Color.black);
        vignette.sprite = RadialSprite(false, inverted: true);
        vignette.raycastTarget = false;
        Stretch(vignette.rectTransform);

        lineTop = CreateLine("장식선 위", 255f);
        lineBottom = CreateLine("장식선 아래", -10f);

        // 제목 뒤 어두운 띠 (글씨가 배경·빛에 묻히지 않게)
        titleBand = CreateImage("제목 띠", transform, new Color(0f, 0f, 0f, 0.55f));
        titleBand.raycastTarget = false;
        Place(titleBand.rectTransform, new Vector2(0f, 150f), new Vector2(1920f, 170f));

        ghostRed = CreateText("제목 잔상 빨강", 96f, new Color(1f, 0.1f, 0.2f, 0f));
        ghostCyan = CreateText("제목 잔상 청록", 96f, new Color(0.2f, 0.9f, 1f, 0f));
        // 검은 테두리: 같은 글씨를 검정으로 8방향에 조금씩 비켜 깔아 둠 (픽셀 글꼴이 뭉개지지 않게)
        Vector2[] offsets =
        {
            new Vector2(-4f, 0f), new Vector2(4f, 0f), new Vector2(0f, -4f), new Vector2(0f, 4f),
            new Vector2(-4f, -4f), new Vector2(4f, -4f), new Vector2(-4f, 4f), new Vector2(4f, 4f),
        };
        foreach (Vector2 offset in offsets)
        {
            TextMeshProUGUI copy = CreateText("제목 테두리", 96f, Color.black);
            titleOutline.Add(copy);
            titleOutlineOffsets.Add(offset);
        }

        title = CreateText("제목", 96f, Color.white);
        foreach (TextMeshProUGUI t in new[] { ghostRed, ghostCyan, title })
            Place(t.rectTransform, new Vector2(0f, 150f), new Vector2(1800f, 170f));
        foreach (TextMeshProUGUI copy in titleOutline)
            Place(copy.rectTransform, new Vector2(0f, 150f), new Vector2(1800f, 170f));

        // 제목 양옆에서 두근거리는 하트
        heartLeft = CreateImage("하트 왼쪽", transform, Color.white);
        heartRight = CreateImage("하트 오른쪽", transform, Color.white);
        foreach (Image h in new[] { heartLeft, heartRight })
        {
            h.sprite = heartSprite;
            h.raycastTarget = false;
            h.preserveAspect = true;
        }

        // 제목 모서리에 비스듬히 붙은 반창고 스티커
        Sprite bandAid = BandAidSprite();
        bandAidSprite = bandAid;
        bandAidA = CreateImage("반창고 1", transform, Color.white);
        bandAidB = CreateImage("반창고 2", transform, Color.white);
        foreach (Image b in new[] { bandAidA, bandAidB })
        {
            b.sprite = bandAid;
            b.raycastTarget = false;
        }

        subtitle = CreateText("설명", 40f, new Color(0.88f, 0.85f, 0.87f, 1f));
        Place(subtitle.rectTransform, new Vector2(0f, 40f), new Vector2(1600f, 70f));

        CreateButton("다시 시작", new Vector2(-400f, -140f), restart);
        CreateButton("로비로", new Vector2(0f, -140f), lobby);
        CreateButton("게임 종료", new Vector2(400f, -140f), quit);
    }

    // ═══════════════════════════════════════════════════
    //  보여 주기
    // ═══════════════════════════════════════════════════

    public void Show(string titleText, string subtitleText, EndingStyle endingStyle)
    {
        style = endingStyle;
        switch (style)
        {
            case EndingStyle.Submission:
                baseColor = new Color(0.09f, 0.02f, 0.08f);
                glowColor = new Color(0.95f, 0.40f, 0.70f, 0.60f);
                particleColor = new Color(1f, 0.62f, 0.82f, 1f);
                title.color = new Color(1f, 0.45f, 0.72f);
                break;
            case EndingStyle.Proposal:
                baseColor = new Color(0.11f, 0.03f, 0.08f);
                glowColor = new Color(1f, 0.70f, 0.86f, 0.40f);
                particleColor = new Color(1f, 0.78f, 0.88f, 1f);
                title.color = new Color(1f, 0.45f, 0.72f);
                break;
            case EndingStyle.Escape:
                baseColor = new Color(0.02f, 0.03f, 0.08f);
                glowColor = new Color(0.55f, 0.70f, 1f, 0.45f);
                particleColor = new Color(0.80f, 0.90f, 1f, 1f);
                title.color = new Color(0.92f, 0.95f, 1f);
                break;
            default:
                baseColor = new Color(0.06f, 0.0f, 0.01f);
                glowColor = new Color(0.75f, 0.05f, 0.08f, 0.55f);
                particleColor = new Color(0.75f, 0.06f, 0.14f, 1f);
                title.color = new Color(1f, 0.45f, 0.72f);
                break;
        }

        background.color = baseColor;

        // 멘헤라 장식: 순응 엔딩·게임 오버는 하트(게임 오버는 금 간 하트), 탈출 엔딩은 빛 방울만
        bool hearts = style != EndingStyle.Escape;
        Sprite particleSprite = style == EndingStyle.GameOver ? brokenHeartSprite : (hearts ? heartSprite : dotSprite);
        for (int i = 0; i < particleImages.Count; i++)
        {
            if (style == EndingStyle.Proposal)
                particleImages[i].sprite = i % 3 == 2 ? heartSprite : petalSprite;   // 꽃잎 2 : 하트 1
            else
                particleImages[i].sprite = hearts && i % 3 == 2 ? dotSprite : particleSprite;
        }
        heartLeft.sprite = heartRight.sprite = style == EndingStyle.GameOver ? brokenHeartSprite : heartSprite;
        heartLeft.gameObject.SetActive(hearts);
        heartRight.gameObject.SetActive(hearts);
        bandAidA.gameObject.SetActive(hearts);
        bandAidB.gameObject.SetActive(style == EndingStyle.Submission || style == EndingStyle.Proposal);
        // 청혼 엔딩은 반창고 대신 리본
        bandAidA.sprite = bandAidB.sprite = style == EndingStyle.Proposal ? ribbonSprite : bandAidSprite;
        Color heartColor = style == EndingStyle.GameOver ? new Color(0.55f, 0.02f, 0.08f) : new Color(1f, 0.45f, 0.70f);
        heartLeft.color = heartRight.color = heartColor;
        bandAidA.color = bandAidB.color = style == EndingStyle.GameOver ? new Color(0.85f, 0.72f, 0.65f)
            : style == EndingStyle.Proposal ? Color.white : new Color(1f, 0.82f, 0.88f);
        lineTop.color = lineBottom.color = new Color(title.color.r, title.color.g, title.color.b, 0.9f);
        flashLeft.gameObject.SetActive(style == EndingStyle.Escape);
        flashRight.gameObject.SetActive(style == EndingStyle.Escape);

        title.text = ghostRed.text = ghostCyan.text = titleText;
        title.fontSize = ghostRed.fontSize = ghostCyan.fontSize = titleText.Length > 10 ? 88f : 120f;
        foreach (TextMeshProUGUI copy in titleOutline)
        {
            copy.text = titleText;
            copy.fontSize = title.fontSize;
        }
        title.ForceMeshUpdate();
        float halfWidth = Mathf.Min(title.preferredWidth * 0.5f, 860f);
        Place(heartLeft.rectTransform, new Vector2(-halfWidth - 70f, 155f), new Vector2(84f, 84f));
        Place(heartRight.rectTransform, new Vector2(halfWidth + 70f, 155f), new Vector2(84f, 84f));
        bool ribbon = style == EndingStyle.Proposal;
        Place(bandAidA.rectTransform, new Vector2(-halfWidth + 40f, 215f), ribbon ? new Vector2(120f, 80f) : new Vector2(150f, 52f));
        bandAidA.rectTransform.localEulerAngles = new Vector3(0f, 0f, ribbon ? 12f : 24f);
        Place(bandAidB.rectTransform, new Vector2(halfWidth - 30f, 92f), ribbon ? new Vector2(110f, 74f) : new Vector2(130f, 46f));
        bandAidB.rectTransform.localEulerAngles = new Vector3(0f, 0f, ribbon ? -10f : -18f);
        subtitle.text = subtitleText ?? string.Empty;
        subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitleText));

        for (int i = 0; i < particles.Count; i++)
            ResetParticle(i, true);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        StopAllCoroutines();
        StartCoroutine(Intro());
    }

    public void Hide()
    {
        StopAllCoroutines();
        idle = false;
        if (title != null) title.rectTransform.localEulerAngles = Vector3.zero;
        gameObject.SetActive(false);
    }

    private IEnumerator Intro()
    {
        idle = false;
        group.alpha = 0f;
        title.maxVisibleCharacters = ghostRed.maxVisibleCharacters = ghostCyan.maxVisibleCharacters = 0;
        SetAlpha(subtitle, 0f);
        SetAlpha(titleBand, 0f);
        SetLineWidth(0f);
        foreach (Image deco in new[] { heartLeft, heartRight, bandAidA, bandAidB })
            SetAlpha(deco, 0f);
        foreach (CanvasGroup b in buttons)
        {
            b.alpha = 0f;
            b.interactable = false;
            b.blocksRaycasts = false;
        }

        PlayStyleSound();

        // 배경이 서서히 나타남
        yield return Animate(0.7f, t => group.alpha = t);

        // 장식선이 가운데에서 양옆으로 펼쳐짐
        idle = true;   // 이때부터 배경·빛이 움직임
        yield return Animate(0.5f, t =>
        {
            SetLineWidth(Mathf.SmoothStep(0f, 1f, t));
            SetAlpha(titleBand, 0.55f * t);
        });

        // 제목 한 글자씩 + 글리치
        title.ForceMeshUpdate();
        int count = title.textInfo.characterCount;
        for (int i = 1; i <= count; i++)
        {
            title.maxVisibleCharacters = ghostRed.maxVisibleCharacters = ghostCyan.maxVisibleCharacters = i;
            if (!char.IsWhiteSpace(title.textInfo.characterInfo[i - 1].character))
                EXODSound.Play("type_tick_low", 0.45f);
            Glitch(0.6f);
            yield return new WaitForSecondsRealtime(0.07f);
        }
        ClearGlitch();
        StartCoroutine(Punch(title.rectTransform, 1.12f, 0.25f));

        // 하트·반창고가 '탁' 붙음
        foreach (Image deco in new[] { heartLeft, heartRight, bandAidA, bandAidB })
        {
            if (!deco.gameObject.activeSelf) continue;
            SetAlpha(deco, 1f);
            StartCoroutine(Punch(deco.rectTransform, 1.35f, 0.22f));
            EXODSound.Play("ui_click", 0.4f);
            yield return new WaitForSecondsRealtime(0.08f);
        }

        // 설명
        yield return Animate(0.6f, t => SetAlpha(subtitle, t));

        // 버튼이 하나씩 아래에서 올라옴
        for (int i = 0; i < buttons.Count; i++)
        {
            StartCoroutine(RiseButton(buttons[i]));
            yield return new WaitForSecondsRealtime(0.12f);
        }

        nextGlitchTime = Time.unscaledTime + UnityEngine.Random.Range(1.5f, 3.5f);
    }

    private void PlayStyleSound()
    {
        switch (style)
        {
            case EndingStyle.Submission:
            case EndingStyle.Proposal:
                EXODSound.PlayAmbience("music_box", 0.45f, 2f);
                break;
            case EndingStyle.Escape:
                EXODSound.Play("door_creak", 0.5f);
                break;
            default:
                EXODSound.Play("gameover_boom");
                break;
        }
    }

    // ═══════════════════════════════════════════════════
    //  매 프레임 (배경 숨쉬기·떠다니는 빛·경찰 불빛·가끔 글리치)
    // ═══════════════════════════════════════════════════

    private void LateUpdate()
    {
        if (title == null)
            return;
        RectTransform tr = title.rectTransform;
        for (int i = 0; i < titleOutline.Count; i++)
        {
            TextMeshProUGUI copy = titleOutline[i];
            copy.maxVisibleCharacters = title.maxVisibleCharacters;
            copy.enabled = title.enabled;
            RectTransform cr = copy.rectTransform;
            cr.anchoredPosition = tr.anchoredPosition + titleOutlineOffsets[i];
            cr.localEulerAngles = tr.localEulerAngles;
            cr.localScale = tr.localScale;
        }
    }

    private void Update()
    {
        if (!idle)
            return;

        float time = Time.unscaledTime;
        float breathe = 0.5f + 0.5f * Mathf.Sin(time * 1.3f);
        glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * (0.55f + 0.45f * breathe));
        glow.rectTransform.localScale = Vector3.one * (1f + 0.04f * breathe);

        if (style == EndingStyle.Escape)
        {
            // 경찰차 불빛: 빨강·파랑이 번갈아 번쩍
            bool redTurn = Mathf.FloorToInt(time * 2.2f) % 2 == 0;
            float pulse = Mathf.Abs(Mathf.Sin(time * Mathf.PI * 2.2f));
            flashLeft.color = new Color(1f, 0.1f, 0.15f, redTurn ? 0.55f * pulse : 0.05f);
            flashRight.color = new Color(0.15f, 0.35f, 1f, redTurn ? 0.05f : 0.55f * pulse);
        }

        // 하트 두근두근 (쿵-쿵 두 번 뛰고 쉼)
        float beatPhase = time % 1.1f;
        float beat = Mathf.Exp(-Mathf.Pow((beatPhase - 0.08f) * 18f, 2f)) + 0.7f * Mathf.Exp(-Mathf.Pow((beatPhase - 0.3f) * 18f, 2f));
        heartLeft.rectTransform.localScale = heartRight.rectTransform.localScale = Vector3.one * (1f + 0.22f * beat);

        // 순응 엔딩 제목은 불안하게 살짝 흔들림
        if (style == EndingStyle.Submission)
            title.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(time * 2.3f) * 1.2f + Mathf.Sin(time * 7.1f) * 0.3f);

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < particles.Count; i++)
        {
            RectTransform p = particles[i];
            Vector2 pos = p.anchoredPosition + particleSpeed[i] * dt;
            pos.x += Mathf.Sin(time * 0.8f + i) * 12f * dt;
            p.anchoredPosition = pos;
            p.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(time * 1.2f + i * 0.7f) * 18f);
            if (pos.y > 600f || pos.y < -600f)
                ResetParticle(i, false);

            Image image = p.GetComponent<Image>();
            float twinkle = 0.55f + 0.45f * Mathf.Sin(time * (1.5f + i % 5 * 0.4f) + i);
            image.color = new Color(particleColor.r, particleColor.g, particleColor.b, 0.75f * twinkle);
        }

        // 가끔 제목이 지직
        if (time >= nextGlitchTime && nextGlitchTime > 0f)
        {
            StartCoroutine(GlitchBurst());
            nextGlitchTime = time + UnityEngine.Random.Range(2f, 4.5f);
        }
    }

    private void ResetParticle(int i, bool anywhere)
    {
        RectTransform p = particles[i];
        bool rising = style == EndingStyle.Proposal ? false : (style != EndingStyle.Submission || i % 2 == 0);
        float x = UnityEngine.Random.Range(-980f, 980f);
        float y = anywhere ? UnityEngine.Random.Range(-560f, 560f) : (rising ? -580f : 580f);
        p.anchoredPosition = new Vector2(x, y);
        float size = style == EndingStyle.Submission || style == EndingStyle.Proposal
            ? UnityEngine.Random.Range(14f, 46f) : UnityEngine.Random.Range(6f, 22f);
        p.sizeDelta = new Vector2(size, size);
        float speed = UnityEngine.Random.Range(25f, 85f) * (style == EndingStyle.GameOver ? 1.4f : 1f);
        particleSpeed[i] = new Vector2(UnityEngine.Random.Range(-12f, 12f), rising ? speed : -speed * 0.6f);
    }

    // ═══════════════════════════════════════════════════
    //  연출 도우미
    // ═══════════════════════════════════════════════════

    private void Glitch(float strength)
    {
        Vector2 home = new Vector2(0f, 150f);
        ghostRed.rectTransform.anchoredPosition = home + new Vector2(UnityEngine.Random.Range(-14f, -4f) * strength, UnityEngine.Random.Range(-3f, 3f));
        ghostCyan.rectTransform.anchoredPosition = home + new Vector2(UnityEngine.Random.Range(4f, 14f) * strength, UnityEngine.Random.Range(-3f, 3f));
        SetAlpha(ghostRed, 0.65f * strength);
        SetAlpha(ghostCyan, 0.5f * strength);
        title.rectTransform.anchoredPosition = home + new Vector2(UnityEngine.Random.Range(-6f, 6f) * strength, 0f);
    }

    private void ClearGlitch()
    {
        Vector2 home = new Vector2(0f, 150f);
        title.rectTransform.anchoredPosition = home;
        SetAlpha(ghostRed, 0f);
        SetAlpha(ghostCyan, 0f);
        ghostRed.rectTransform.anchoredPosition = home + new Vector2(-2f, 0f);
        ghostCyan.rectTransform.anchoredPosition = home + new Vector2(2f, 0f);
    }

    private IEnumerator GlitchBurst()
    {
        EXODSound.Play("glitch", 0.25f);
        float end = Time.unscaledTime + UnityEngine.Random.Range(0.12f, 0.25f);
        while (Time.unscaledTime < end)
        {
            Glitch(1f);
            title.enabled = UnityEngine.Random.value > 0.12f;
            yield return new WaitForSecondsRealtime(0.03f);
        }
        title.enabled = true;
        ClearGlitch();
    }

    private IEnumerator RiseButton(CanvasGroup button)
    {
        RectTransform rect = (RectTransform)button.transform;
        Vector2 end = rect.anchoredPosition;
        Vector2 start = end + new Vector2(0f, -40f);
        yield return Animate(0.35f, t =>
        {
            float e = 1f - (1f - t) * (1f - t);
            rect.anchoredPosition = Vector2.LerpUnclamped(start, end, e);
            button.alpha = t;
        });
        button.interactable = true;
        button.blocksRaycasts = true;
    }

    private static IEnumerator Punch(RectTransform rect, float peak, float duration)
    {
        float half = duration * 0.5f;
        yield return Animate(half, t => rect.localScale = Vector3.one * Mathf.Lerp(1f, peak, t));
        yield return Animate(half, t => rect.localScale = Vector3.one * Mathf.Lerp(peak, 1f, t));
    }

    private static IEnumerator Animate(float duration, Action<float> step)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            step(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        step(1f);
    }

    private void SetLineWidth(float t)
    {
        lineTop.rectTransform.sizeDelta = new Vector2(1100f * t, 3f);
        lineBottom.rectTransform.sizeDelta = new Vector2(1100f * t, 3f);
    }

    // ═══════════════════════════════════════════════════
    //  UI 만들기 도우미
    // ═══════════════════════════════════════════════════

    private void CreateButton(string label, Vector2 position, Action onClick)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)go.transform;
        Place(rect, position, new Vector2(340f, 86f));

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.015f, 0.005f, 0.01f, 1f);
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);

        // 왼쪽 강조 막대
        Image accent = CreateImage("강조", go.transform, new Color(1f, 1f, 1f, 0.8f));
        accent.raycastTarget = false;
        RectTransform accentRect = accent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(6f, 0f);

        TextMeshProUGUI text = CreateText("글자", 34f, Color.white, go.transform);
        Stretch(text.rectTransform);
        text.text = label;

        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick?.Invoke());
        EXODSound.AttachUiSounds(go);
        EndingButtonHover hover = go.AddComponent<EndingButtonHover>();
        hover.Setup(image, outline, accent, () => title != null ? title.color : Color.white);

        buttons.Add(go.GetComponent<CanvasGroup>());
    }

    private Image CreateSideFlash(string name, float side)
    {
        Image image = CreateImage(name, transform, Color.clear);
        image.sprite = RadialSprite(false);
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(side, 0.5f);
        rect.sizeDelta = new Vector2(1400f, 1600f);
        image.gameObject.SetActive(false);
        return image;
    }

    private Image CreateLine(string name, float y)
    {
        Image line = CreateImage(name, transform, Color.white);
        line.raycastTarget = false;
        Place(line.rectTransform, new Vector2(0f, y), new Vector2(0f, 3f));
        return line;
    }

    private TextMeshProUGUI CreateText(string name, float size, Color color, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent != null ? parent : transform, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (EXODUiAssets.Font != null)
            text.font = EXODUiAssets.Font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
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

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    // 하트 모양 (broken이면 가운데에 지그재그로 금이 감)
    private static Sprite HeartSprite(bool broken)
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 둥근 봉우리 두 개(원) + 아래로 뾰족한 삼각형
                float fx = (x - size * 0.5f) / (size * 0.5f);   // -1 ~ 1
                float fy = (y - size * 0.5f) / (size * 0.5f);   // -1(아래) ~ 1(위)
                float lobeL = 0.52f - Mathf.Sqrt((fx + 0.45f) * (fx + 0.45f) + (fy - 0.38f) * (fy - 0.38f));
                float lobeR = 0.52f - Mathf.Sqrt((fx - 0.45f) * (fx - 0.45f) + (fy - 0.38f) * (fy - 0.38f));
                // 삼각형: 위쪽 너비 ±0.93(fy=0.3) → 아래 꼭짓점(0, -0.92)
                float tri = fy > 0.32f ? -1f : Mathf.Min(0.32f - fy, (0.93f * (fy + 0.92f) / 1.22f) - Mathf.Abs(fx));
                float dist = Mathf.Max(lobeL, Mathf.Max(lobeR, tri));
                float a = Mathf.Clamp01(dist * size * 0.5f + 0.5f);
                float shine = Mathf.Clamp01(1f - Mathf.Sqrt((fx + 0.45f) * (fx + 0.45f) + (fy - 0.45f) * (fy - 0.45f)) / 0.22f);
                Color c = Color.Lerp(new Color(0.92f, 0.92f, 0.92f), Color.white, shine);
                if (broken)
                {
                    float crackX = Mathf.Abs(Mathf.Repeat(fy * 3f, 1f) - 0.5f) * 0.35f - 0.08f;
                    if (Mathf.Abs(fx - crackX) < 0.07f) a = 0f;
                }
                c.a = a;
                texture.SetPixel(x, y, c);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // 꽃잎 (끝이 뾰족한 타원, 가운데가 조금 진함)
    private static Sprite PetalSprite()
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = (x - size * 0.5f) / (size * 0.5f);
                float fy = (y - size * 0.5f) / (size * 0.5f);
                // 위아래로 뾰족한 렌즈 모양
                float width = 0.55f * Mathf.Sqrt(Mathf.Max(0f, 1f - fy * fy)) * (1f - 0.25f * Mathf.Abs(fy));
                float d = Mathf.Abs(fx) - width;
                float a = Mathf.Clamp01(-d * size * 0.25f + 0.5f) * (Mathf.Abs(fy) < 0.98f ? 1f : 0f);
                float shade = 0.85f + 0.15f * (1f - Mathf.Abs(fx) / Mathf.Max(0.01f, width));
                texture.SetPixel(x, y, new Color(shade, shade, shade, a));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // 리본 (양쪽 고리 + 가운데 매듭 + 늘어진 꼬리), 검은 테두리
    private static Sprite RibbonSprite()
    {
        const int w = 180, h = 120;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color pink = new Color(1f, 0.55f, 0.75f, 1f);
        Color dark = new Color(0.85f, 0.35f, 0.55f, 1f);
        Color line = new Color(0.12f, 0.03f, 0.08f, 1f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float fx = (x - w * 0.5f) / (h * 0.5f);   // 세로 기준 단위
                float fy = (y - h * 0.58f) / (h * 0.5f);
                // 양쪽 고리: 가운데로 갈수록 좁아지는 삼각형 모양 타원
                float ax = Mathf.Abs(fx);
                float loop = (ax > 0.15f && ax < 1.45f) ? (Mathf.Abs(fy) - (0.18f + 0.42f * Mathf.Sin(Mathf.Clamp01((ax - 0.15f) / 1.3f) * Mathf.PI * 0.85f))) : 1f;
                // 꼬리 두 개
                float tailL = Mathf.Abs((fx + 0.25f) + (fy + 0.15f) * 0.45f) - 0.12f;
                float tailR = Mathf.Abs((fx - 0.25f) - (fy + 0.15f) * 0.45f) - 0.12f;
                bool tailRange = fy < -0.1f && fy > -0.95f;
                float tail = tailRange ? Mathf.Min(tailL, tailR) : 1f;
                // 가운데 매듭
                float knot = Mathf.Max(ax - 0.2f, Mathf.Abs(fy) - 0.24f);
                float dist = Mathf.Min(Mathf.Min(loop, tail), knot);
                Color c;
                if (dist > 0.0f) c = new Color(0f, 0f, 0f, 0f);
                else if (dist > -0.06f) c = line;
                else c = knot <= 0f ? dark : pink;
                texture.SetPixel(x, y, c);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    // 반창고 (둥근 막대 + 가운데 패드 + 작은 구멍)
    private static Sprite BandAidSprite()
    {
        const int w = 192, h = 64;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float r = h * 0.5f - 2f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x, r + 2f, w - r - 2f);
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - h * 0.5f) * (y - h * 0.5f));
                float a = Mathf.Clamp01(r - d + 1f);
                Color c = new Color(1f, 1f, 1f, a);
                bool pad = Mathf.Abs(x - w * 0.5f) < w * 0.16f && Mathf.Abs(y - h * 0.5f) < h * 0.32f;
                if (pad)
                    c = new Color(0.86f, 0.78f, 0.80f, a);
                else if (x % 10 == 4 && y % 10 == 4)
                    c = new Color(0.75f, 0.6f, 0.65f, a);   // 작은 구멍 점
                if (a > 0f && d > r - 2.5f)
                    c = new Color(0.25f, 0.08f, 0.15f, a);   // 테두리 선
                texture.SetPixel(x, y, c);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    // 가운데가 밝고 바깥으로 갈수록 투명한 원 (inverted면 반대로 가장자리가 진함)
    private static Sprite RadialSprite(bool hard, bool inverted = false)
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float a = hard ? Mathf.Clamp01(1f - d) : Mathf.Clamp01(1f - d * d);
                if (hard) a = a * a;
                if (inverted)
                {
                    float dx = Mathf.Abs(x - half) / half, dy = Mathf.Abs(y - half) / half;
                    float edge = Mathf.Clamp01(Mathf.Max(dx * 0.9f, dy) - 0.35f) / 0.65f;
                    a = Mathf.SmoothStep(0f, 0.92f, edge);
                }
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
