using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 플레이어 기절과 게임 오버를 맡습니다.
/// - Faint(): 화면 암전 → "플레이어가 기절했다." → 하트 1개 감소 → 다시 밝아짐
/// - 하트(HP)가 0이 되면 이유와 상관없이 게임 오버 창을 띄웁니다.
/// - 게임 오버: 암전 → 여자친구 "우리는 평생 함께야... ♡" → GAME OVER 창
/// - 게임 오버·엔딩 창: [다시 시작] 지금 씬을 처음부터, [로비로] 시작 화면으로, [게임 종료]
/// UI는 실행 중에 자동으로 만들어집니다.
/// </summary>
[DefaultExecutionOrder(230)]
public sealed class PlayerFaintController : MonoBehaviour
{
    public static PlayerFaintController Instance { get; private set; }

    private const string LobbySceneName = "StartScenes";

    [Header("=== 기절 연출 ===")]
    [SerializeField] private float fadeDuration = 0.8f;   // 암전·밝아짐에 걸리는 시간
    [SerializeField] private float holdDuration = 1.6f;   // 암전 상태로 문구를 보여주는 시간

    [Header("=== 게임 오버 연출 ===")]
    [SerializeField] private float gameOverTypingSpeed = 0.12f; // 여자친구 대사 타이핑 속도 (초/글자)
    [SerializeField] private float gameOverLineHold = 1.8f;     // 대사를 다 보여준 뒤 기다리는 시간

    private HeartManager hearts;
    private GameObject overlayRoot;
    private Image blackout;
    private TextMeshProUGUI faintText;
    private EndingScreen endingScreen;
    private GameObject gameOverLineRoot;
    private CanvasGroup gameOverLineGroup;
    private Image gameOverPortrait;
    private TextMeshProUGUI gameOverLineText;
    private TextMeshProUGUI gameOverNameText;
    private Image gameOverLineBox;
    private Image gameOverNameBox;
    private string pendingTitle;
    private string pendingSubtitle;
    private EndingStyle pendingStyle;
    private bool isFainting;
    private bool isGameOver;

    /// <summary>기절 연출 중이거나 게임 오버 상태인지 여부</summary>
    public bool IsBusy => isFainting || isGameOver;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
        if (hearts == null)
            Debug.LogWarning("[PlayerFaint] HeartManager를 찾을 수 없어 HP가 줄지 않습니다.", this);

        BuildUi();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // 얀 등장처럼 다른 이유로 HP가 0이 되어도 게임 오버를 띄웁니다.
        if (!isGameOver && !isFainting && hearts != null && hearts.HP <= 0)
            ShowGameOver();
    }

    // ═══════════════════════════════════════════════════
    //  기절
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 기절 연출을 재생합니다. 깨어난 뒤(HP가 남아 있을 때) onWokeUp을 호출합니다.
    /// </summary>
    public void Faint(Action onWokeUp = null)
    {
        if (IsBusy)
            return;

        StartCoroutine(FaintRoutine(onWokeUp));
    }

    private IEnumerator FaintRoutine(Action onWokeUp)
    {
        isFainting = true;
        CloseOtherUi();

        overlayRoot.SetActive(true);
        endingScreen.Hide();
        faintText.gameObject.SetActive(true);
        faintText.text = Stage1Dialogues.FaintText;
        SetAlpha(faintText, 0f);

        EXODSound.Play("faint");
        yield return Fade(blackout, 0f, 1f, fadeDuration);
        yield return Fade(faintText, 0f, 1f, fadeDuration * 0.5f);
        yield return new WaitForSecondsRealtime(holdDuration);

        if (hearts != null)
            hearts.HPM();

        if (hearts != null && hearts.HP <= 0)
        {
            isFainting = false;
            ShowGameOver();
            yield break;
        }

        yield return Fade(faintText, 1f, 0f, fadeDuration * 0.5f);
        yield return Fade(blackout, 1f, 0f, fadeDuration);

        faintText.gameObject.SetActive(false);
        overlayRoot.SetActive(false);
        isFainting = false;

        onWokeUp?.Invoke();
    }

    // ═══════════════════════════════════════════════════
    //  게임 오버
    // ═══════════════════════════════════════════════════

    // 하트 0 → GAME OVER 대신 간호 엔딩 (사용자 결정 2026-11-04 'A안')
    //  암전 → 검은 화면 위에 간호 대사(볼 빨간 여자친구 그림) → 마지막 한마디 → 엔딩 화면 "간호 엔딩 : 아프지 마, 자기야"
    private const int NursingDialogueOrder = 1001;   // 기절 암전(1000) 위에 대화창

    /// <summary>[이지] "여기까지 하시겠습니까?" [예] → 하트가 남아 있어도 간호 엔딩 (EasyGiveUpPrompt)</summary>
    public void ShowNursingEnding()
    {
        ShowGameOver();
    }

    private void ShowGameOver()
    {
        if (isGameOver)
            return;

        isFainting = false;
        isGameOver = true;
        StopAllCoroutines();
        CloseOtherUi();

        overlayRoot.SetActive(true);
        faintText.gameObject.SetActive(false);
        endingScreen.Hide();
        StartCoroutine(NursingRoutine());
    }

    private IEnumerator NursingRoutine()
    {
        if (blackout.color.a < 0.99f)
            yield return Fade(blackout, blackout.color.a, 1f, 0.8f);
        EXODSound.StopAmbience(1.5f);
        EXODSound.PlayHeartbeat(0.5f, 0.6f, 1.5f);   // 느린 심장 소리 (앓는 중)
        yield return new WaitForSecondsRealtime(1.5f);

        DialogueManager dm = DialogueManager.Instance;
        if (dm != null)
        {
            DialogueOnTop.Raise(NursingDialogueOrder);
            DialoguePortrait.UseSweetForNextDialogue(false);
            dm.StartDialogue(Stage1Dialogues.NursingCutscene());
            yield return null;
            while (dm.IsDialogueOpen)
                yield return null;
            DialogueOnTop.Lower(NursingDialogueOrder);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        EXODSound.StopHeartbeat(0.5f);
        ShowEndScreen(Stage1Dialogues.NursingEndingLine, Stage1Dialogues.NursingEndingTitle,
            Stage1Dialogues.NursingEndingSubtitle, EndingStyle.GameOver);
    }

    /// <summary>
    /// 엔딩 화면을 띄웁니다. (게임 오버와 같은 연출: 암전 → 여자친구 마지막 대사 → 제목·설명 + [다시 시작]/[로비로]/[게임 종료])
    /// 예: 거실 물약 기믹 실패 → 순응 엔딩
    /// </summary>
    /// girlfriendLine이 비어 있으면 대사 없이 바로 엔딩 화면이 나옵니다. (전화 부스 탈출 엔딩)
    public void ShowEnding(string girlfriendLine, string title, string subtitle, EndingStyle style = EndingStyle.Submission)
    {
        if (isGameOver)
            return;

        ShowEndScreen(girlfriendLine, title, subtitle, style);
    }

    private void ShowEndScreen(string line, string title, string subtitle, EndingStyle style)
    {
        isFainting = false;
        pendingTitle = title;
        pendingSubtitle = subtitle;
        pendingStyle = style;

        // 시작 화면 [엔딩 목록]에 본 엔딩으로 기록
        EndingCollection.MarkSeen(style);

        isGameOver = true;
        StopAllCoroutines();
        CloseOtherUi();

        overlayRoot.SetActive(true);
        faintText.gameObject.SetActive(false);
        SetAlpha(blackout, 1f);
        endingScreen.Hide();
        StartCoroutine(GameOverRoutine(line));
    }

    // 여자친구의 마지막 대사를 보여준 뒤 GAME OVER 창을 띄웁니다. (도중에 넘길 수 없음)
    private IEnumerator GameOverRoutine(string line)
    {
        EXODSound.StopAmbience(1f);
        if (string.IsNullOrEmpty(line))
        {
            endingScreen.Show(pendingTitle, pendingSubtitle, pendingStyle);
            yield break;
        }

        gameOverLineRoot.SetActive(true);
        MatchTeamDialogueBox();
        if (gameOverPortrait != null)
        {
            Sprite face = pendingStyle == EndingStyle.Proposal ? DialoguePortrait.LoadSweetPortrait() : null;
            gameOverPortrait.sprite = face != null ? face : DialoguePortrait.LoadPortrait();
            float h = gameOverPortrait.rectTransform.sizeDelta.y;
            gameOverPortrait.rectTransform.sizeDelta = new Vector2(h * gameOverPortrait.sprite.rect.width / gameOverPortrait.sprite.rect.height, h);
        }
        gameOverLineText.text = line;
        gameOverLineText.maxVisibleCharacters = 0;

        EXODSound.StopAmbience(1f);
        EXODSound.PlayHeartbeat(0.9f, 0.8f, 0.5f);

        RectTransform portraitRect = gameOverPortrait != null ? gameOverPortrait.rectTransform : null;
        Vector2 portraitEnd = portraitRect != null ? portraitRect.anchoredPosition : Vector2.zero;

        yield return new WaitForSecondsRealtime(0.6f);

        // 초상화가 아래에서 천천히 다가오며 나타납니다.
        float elapsed = 0f;
        const float appearDuration = 1.2f;
        while (elapsed < appearDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / appearDuration));
            gameOverLineGroup.alpha = t;
            if (portraitRect != null)
                portraitRect.anchoredPosition = portraitEnd + new Vector2(0f, -60f * (1f - t));
            yield return null;
        }

        gameOverLineText.ForceMeshUpdate();
        int length = gameOverLineText.textInfo.characterCount;
        for (int i = 1; i <= length; i++)
        {
            gameOverLineText.maxVisibleCharacters = i;
            if (!char.IsWhiteSpace(gameOverLineText.textInfo.characterInfo[i - 1].character))
                EXODSound.Play("type_tick_low", 0.5f);
            yield return new WaitForSecondsRealtime(gameOverTypingSpeed);
        }

        yield return new WaitForSecondsRealtime(gameOverLineHold);

        yield return FadeGroup(gameOverLineGroup, 1f, 0f, 0.8f);
        gameOverLineRoot.SetActive(false);
        if (portraitRect != null)
            portraitRect.anchoredPosition = portraitEnd;

        EXODSound.StopHeartbeat(0.3f);
        endingScreen.Show(pendingTitle, pendingSubtitle, pendingStyle);
    }

    // ═══════════════════════════════════════════════════
    //  부스 운영 모드: 제한시간 초과 (사용자 요청 2026-11-03)
    //  암전 + 트럼펫 → 대화창에 두 줄 → "Game Over" 뒤에 1초마다 점 하나씩(5초) → 대화창 위 [타이틀 화면으로 가기]
    // ═══════════════════════════════════════════════════

    private const string TimeUpLine = "당신은 탈출하지도, 다른 결말을 찾지도 못했습니다.";

    public void ShowBoothTimeUp()
    {
        if (isGameOver)
            return;

        isFainting = false;
        isGameOver = true;
        StopAllCoroutines();
        CloseOtherUi();

        overlayRoot.SetActive(true);
        overlayRoot.GetComponent<Canvas>().sortingOrder = 31000;   // 산책 화면·들킴 연출보다도 위
        faintText.gameObject.SetActive(false);
        endingScreen.Hide();
        StartCoroutine(BoothTimeUpRoutine());
    }

    private IEnumerator BoothTimeUpRoutine()
    {
        EXODSound.StopAmbience(0.5f);
        EXODSound.StopHeartbeat(0.3f);
        EXODSound.Play("keypad_error");   // 제한시간 초과 삑삑 (사용자 요청 2026-11-03)

        // 이미 볼 수 있는 엔딩이 있으면 게임 오버 대신 '운명의 갈림길' (사용자 요청 2026-11-03)
        List<BoothMode.FateEnding> fates = BoothMode.AvailableEndings();
        if (fates.Count > 0)
        {
            EXODSound.Play("heaven_choir");
            yield return Fade(blackout, 0f, 1f, 0.8f);
            yield return FateRoutine(fates);
            yield break;
        }

        EXODSound.Play("proposal_trumpet");
        yield return Fade(blackout, 0f, 1f, 0.8f);

        ShowLineBox(null);
        yield return FadeGroup(gameOverLineGroup, 0f, 1f, 0.5f);
        yield return TypeLine(TimeUpLine);
        yield return new WaitForSecondsRealtime(1.6f);

        // Game Over + 1초마다 점 하나 (5초)
        gameOverLineText.maxVisibleCharacters = 99999;
        gameOverLineText.text = "<b>Game Over</b>";
        for (int dots = 1; dots <= 5; dots++)
        {
            yield return new WaitForSecondsRealtime(1f);
            gameOverLineText.text = "<b>Game Over</b>" + new string('.', dots);
        }

        yield return WaitChoice(new[] { "타이틀 화면으로 가기" });
        BackToLobby();
    }

    // ─── 대화창(게임 오버용 복제) 도우미 ───────────────────────────────

    // speaker가 비어 있으면 이름표 없음. 초상화는 항상 숨김
    private void ShowLineBox(string speaker)
    {
        gameOverLineRoot.SetActive(true);
        MatchTeamDialogueBox();
        if (gameOverPortrait != null)
            gameOverPortrait.gameObject.SetActive(false);
        bool named = !string.IsNullOrEmpty(speaker);
        gameOverNameBox.gameObject.SetActive(named);
        if (named)
            gameOverNameText.text = speaker;
    }

    // 다른 엔딩 연출에 넘기기 전 원래 모양으로
    private void RestoreLineBox()
    {
        if (gameOverPortrait != null)
            gameOverPortrait.gameObject.SetActive(true);
        gameOverNameBox.gameObject.SetActive(true);
        gameOverNameText.text = Stage1Dialogues.Girlfriend;
        gameOverLineRoot.SetActive(false);
        gameOverLineGroup.alpha = 0f;
    }

    private IEnumerator TypeLine(string text, float charDelay = 0.05f)
    {
        gameOverLineText.text = text;
        gameOverLineText.maxVisibleCharacters = 0;
        gameOverLineText.ForceMeshUpdate();
        int length = gameOverLineText.textInfo.characterCount;
        for (int i = 1; i <= length; i++)
        {
            gameOverLineText.maxVisibleCharacters = i;
            if (!char.IsWhiteSpace(gameOverLineText.textInfo.characterInfo[i - 1].character))
                EXODSound.Play("type_tick", 0.5f);
            yield return new WaitForSecondsRealtime(charDelay);
        }
        gameOverLineText.maxVisibleCharacters = 99999;
    }

    private IEnumerator Say(string speaker, string text, float hold = 1.4f)
    {
        ShowLineBox(speaker);
        gameOverLineGroup.alpha = 1f;
        yield return TypeLine(text);
        yield return new WaitForSecondsRealtime(hold);
    }

    // 대화창 바로 위 선택지 (위에서부터). autoPick >= 0이면 잠시 뒤 저절로 눌림
    private readonly List<GameObject> choiceButtons = new List<GameObject>();
    private int pickedChoice = -1;

    private IEnumerator WaitChoice(string[] labels, int autoPick = -1, float autoDelay = 1.1f)
    {
        ClearChoices();
        pickedChoice = -1;
        List<Image> images = new List<Image>();
        for (int i = 0; i < labels.Length; i++)
            images.Add(BuildChoice(labels[i], i, labels.Length, autoPick < 0));

        if (autoPick >= 0)
        {
            yield return new WaitForSecondsRealtime(autoDelay);
            images[autoPick].color = new Color(0.45f, 0.16f, 0.20f, 0.95f);   // 눌리는 것처럼
            EXODSound.Play("ui_click");
            yield return new WaitForSecondsRealtime(0.35f);
            pickedChoice = autoPick;
        }

        while (pickedChoice < 0)
            yield return null;
        ClearChoices();
    }

    private void ClearChoices()
    {
        foreach (GameObject go in choiceButtons)
            if (go != null)
                Destroy(go);
        choiceButtons.Clear();
    }

    // clickable=false: 눌리지도, 마우스에 반응하지도 않음 (저절로 눌리는 선택지)
    private Image BuildChoice(string text, int index, int count, bool clickable = true)
    {
        GameObject go = new GameObject("선택지 " + text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(gameOverLineRoot.transform, false);
        choiceButtons.Add(go);
        RectTransform box = gameOverLineBox.rectTransform;
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = box.anchorMin;
        rect.anchorMax = box.anchorMax;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(560f, 76f);
        float top = box.anchoredPosition.y + box.sizeDelta.y * (1f - box.pivot.y) + 26f;
        rect.anchoredPosition = new Vector2(box.anchoredPosition.x, top + (count - 1 - index) * 90f);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.12f, 0.08f, 0.09f, 0.95f);
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.04f, 0.08f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        Button button = go.GetComponent<Button>();
        if (clickable)
        {
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.75f, 0.8f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            int captured = index;
            button.onClick.AddListener(() => pickedChoice = captured);
            EXODSound.AttachUiSounds(go);
        }
        else
        {
            button.transition = Selectable.Transition.None;
            button.interactable = false;
            image.raycastTarget = false;
        }

        TextMeshProUGUI label = CreateText("글자", go.transform, text, 34f, Color.white);
        Stretch(label.rectTransform);
        return image;
    }

    // ═══════════════════════════════════════════════════
    //  운명의 갈림길: 이미 볼 수 있는 엔딩 고르기 (부스 운영 모드 제한시간 초과)
    // ═══════════════════════════════════════════════════

    private const string FateLine = "운명의 갈림길에 섰다 어떤 길을 고를까?\n"
        + "<size=72%><color=#3C3C3C>볼 수 있는 엔딩이 뜹니다. 게임 오버가 아니라서 부스 체험 보상을 받을 수 있어요</color></size>";

    private IEnumerator FateRoutine(List<BoothMode.FateEnding> fates)
    {
        ShowLineBox(null);
        yield return FadeGroup(gameOverLineGroup, 0f, 1f, 0.5f);

        while (true)
        {
            yield return TypeLine(FateLine, 0.04f);
            string[] labels = new string[fates.Count];
            for (int i = 0; i < fates.Count; i++)
                labels[i] = FateLabel(fates[i]);
            yield return WaitChoice(labels);
            BoothMode.FateEnding fate = fates[pickedChoice];

            foreach (string line in FateIntro(fate))
                yield return Say(null, line);
            yield return TypeLine(FateQuestion(fate));
            yield return WaitChoice(new[] { FateLabel(fate), "다시 생각해본다" });
            if (pickedChoice != 0)
                continue;   // 다시 생각해본다 → 갈림길로

            BoothMode.ForcingEnding = true;
            if (fate == BoothMode.FateEnding.Submission)
                yield return BedEndingRoutine();
            else if (fate == BoothMode.FateEnding.Proposal)
                yield return RingEndingRoutine();
            else
                yield return AddressEndingRoutine();
            yield break;
        }
    }

    private static string FateLabel(BoothMode.FateEnding fate) =>
        fate == BoothMode.FateEnding.Submission ? "침대를 고른다"
        : fate == BoothMode.FateEnding.Proposal ? "반지를 고른다" : "주소지를 고른다";

    private static string FateQuestion(BoothMode.FateEnding fate) =>
        fate == BoothMode.FateEnding.Submission ? "침대를 고를까?"
        : fate == BoothMode.FateEnding.Proposal ? "반지를 고를까?" : "주소지를 고를까?";

    private static string[] FateIntro(BoothMode.FateEnding fate) =>
        fate == BoothMode.FateEnding.Submission ? new[] { "침대다. 포근하고 안락하다. 평생 여기에 있고싶어진다." }
        : fate == BoothMode.FateEnding.Proposal ? new[] { "반지다. 어딘가 꺼림칙한 기분이 든다." }
        : new[] { "주소지다. 내가 가지고있던 것이다.", "이거 얻으려고 고생했는데.." };

    // 산책 중이었으면 집으로 (연출 없이)
    private IEnumerator LeaveWalk()
    {
        bool done = false;
        Stage2HouseLink.ForceLeaveWalk(() => done = true);
        while (!done)
            yield return null;
    }

    // 다른 엔딩 연출에 넘기기 (검은 막·대화창 치우고 게임 오버 상태 풀기)
    private void HandOff()
    {
        ClearChoices();
        RestoreLineBox();
        faintText.gameObject.SetActive(false);
        SetAlpha(blackout, 0f);
        overlayRoot.GetComponent<Canvas>().sortingOrder = 1000;
        overlayRoot.SetActive(false);
        isGameOver = false;
    }

    // ─── 1. 침대 → 순응 엔딩: 침실로 끌려가 저절로 5번 연속으로 잠 ───────────────

    private static readonly string[] SleepLines =
    {
        null, null,
        "자고 바로 또자서그런가? 몸이 약간 찌뿌둥하다",
        "... 다시누울까?",
        ".. 여기서 계속 사는것도 좋을지도?",
        "여기서 살자.",
    };

    private IEnumerator BedEndingRoutine()
    {
        gameOverLineGroup.alpha = 0f;
        yield return LeaveWalk();

        ItemPositionManager items = ItemPositionManager.GetOrCreate();
        if (items != null)
            items.ResetItemsForNewDay();   // 아이템은 혹시 모르니 초기화
        if (SliderManager.Instance != null)
            SliderManager.Instance.GoToRoom("침실");

        int day = GManager.Instance != null ? GManager.Instance.Day : 1;
        for (int slept = 0; ; slept++)
        {
            faintText.gameObject.SetActive(false);
            yield return Fade(blackout, 1f, 0f, 0.7f);

            if (slept >= 2)
                yield return Say(null, SleepLines[slept], 1.6f);
            if (slept >= 5)
                break;

            // 침대를 누르고 [넘어간다]가 저절로 눌림
            ShowLineBox(null);
            gameOverLineGroup.alpha = 1f;
            yield return TypeLine("다음 일차로 넘어갈까?", 0.03f);
            yield return WaitChoice(new[] { "넘어간다", "아직 아니다" }, 0);
            gameOverLineGroup.alpha = 0f;

            yield return Fade(blackout, 0f, 1f, 0.6f);
            day++;
            faintText.text = "DAY " + day;
            SetHudDay(day);   // 화면 위쪽 DAY 표시도 같이 (실제 일차 값은 그대로)
            faintText.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(1.1f);
        }

        HandOff();
        ShowEnding(Stage1Dialogues.PotionEndingLine, Stage1Dialogues.PotionEndingTitle,
            Stage1Dialogues.PotionEndingSubtitle, EndingStyle.Submission);
    }

    private static void SetHudDay(int day)
    {
        foreach (DayText hud in FindObjectsByType<DayText>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (hud != null && hud.DAY != null)
                hud.DAY.text = "DAY " + day;
    }

    // ─── 2. 반지 → 청혼 엔딩: 화장실, 금고 열린 채로 반지 (집지 않는다 없음) ───────────────

    private IEnumerator RingEndingRoutine()
    {
        gameOverLineGroup.alpha = 0f;
        yield return LeaveWalk();
        if (SliderManager.Instance != null)
            SliderManager.Instance.GoToRoom("화장실");
        yield return new WaitForSecondsRealtime(0.3f);

        RestroomGimmick restroom = FindFirstObjectByType<RestroomGimmick>(FindObjectsInactive.Include);
        HandOff();
        if (restroom != null)
            restroom.ForceRingForEnding();
        else
            ProposalEnding.Trigger();
    }

    // ─── 3. 주소지 → 탈출 엔딩: 실제 산책 → 부스가 화면 가운데에 오면 부스 창 → 1·1·9 자동 → 119 통화 → 탈출 엔딩 ───────────────

    private IEnumerator AddressEndingRoutine()
    {
        gameOverLineGroup.alpha = 0f;
        HandOff();   // 산책이 돌아가도록 게임 오버 상태를 풂 (하루 시간·위험도는 BoothMode.ForcingEnding으로 멈춤)
        BoothEscapeDriver.Run();
        yield break;
    }

    // [다시 시작]: 이지/하드를 다시 고른 뒤 처음부터 (사용자 요청 2026-11-03)
    public void Restart()
    {
        DifficultyPicker.Show(easy =>
        {
            GameDifficulty.IsEasy = easy;
            ResetRunState();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        });
    }

    public void BackToLobby()
    {
        ResetRunState();
        SceneManager.LoadScene(LobbySceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 씬을 다시 불러와도 남아 있는 진행 상황을 지웁니다.
    private static void ResetRunState()
    {
        // 엔딩 수집 기록만 남기고 이번 판 진행을 모두 지움 (RunReset, 2026-11-03)
        RunReset.ResetAll();
    }

    private static void CloseOtherUi()
    {
        if (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen)
            InspectionManager.Instance.Close();

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            DialogueManager.Instance.ForceCloseDialogue();
    }

    // ═══════════════════════════════════════════════════
    //  UI 만들기
    // ═══════════════════════════════════════════════════

    private void BuildUi()
    {
        overlayRoot = new GameObject(
            "기절·게임오버 화면",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        // 하트 HUD, 퀵슬롯, 대화창보다 위에 그립니다.
        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = overlayRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 전체 화면 검은 막 (뒤쪽 클릭도 막습니다)
        blackout = CreateImage("암전", overlayRoot.transform, Color.black);
        Stretch(blackout.rectTransform);
        SetAlpha(blackout, 0f);

        faintText = CreateText("기절 문구", overlayRoot.transform, Stage1Dialogues.FaintText, 44f,
            new Color(0.85f, 0.85f, 0.85f, 1f));
        Stretch(faintText.rectTransform);

        BuildGameOverLine();
        BuildGameOverPanel();

        faintText.gameObject.SetActive(false);
        overlayRoot.SetActive(false);
    }

    private void BuildGameOverLine()
    {
        gameOverLineRoot = new GameObject("게임 오버 대사", typeof(RectTransform), typeof(CanvasGroup));
        gameOverLineRoot.transform.SetParent(overlayRoot.transform, false);
        Stretch(gameOverLineRoot.GetComponent<RectTransform>());
        gameOverLineGroup = gameOverLineRoot.GetComponent<CanvasGroup>();
        gameOverLineGroup.alpha = 0f;

        Sprite sprite = DialoguePortrait.LoadPortrait();
        if (sprite != null)
        {
            gameOverPortrait = CreateImage("여자친구", gameOverLineRoot.transform, Color.white);
            gameOverPortrait.sprite = sprite;
            gameOverPortrait.preserveAspect = true;
            gameOverPortrait.raycastTarget = false;

            RectTransform rect = gameOverPortrait.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            float height = 820f;
            rect.sizeDelta = new Vector2(height * sprite.rect.width / sprite.rect.height, height);
            rect.anchoredPosition = new Vector2(0f, 120f);
        }

        // 초상화 아래쪽을 아래로 갈수록 어둡게 덮어 글자가 잘 보이게 합니다.
        Image shade = CreateImage("글자 배경", gameOverLineRoot.transform, new Color(0f, 0f, 0f, 0.92f));
        shade.sprite = CreateFadeUpSprite();
        shade.raycastTarget = false;
        RectTransform shadeRect = shade.rectTransform;
        shadeRect.anchorMin = new Vector2(0f, 0f);
        shadeRect.anchorMax = new Vector2(1f, 0f);
        shadeRect.pivot = new Vector2(0.5f, 0f);
        shadeRect.sizeDelta = new Vector2(0f, 420f);
        shadeRect.anchoredPosition = Vector2.zero;

        // 게임 대화창과 같은 상자 (그림·위치는 띄울 때 MatchTeamDialogueBox가 실제 대화창에 맞춤)
        gameOverLineBox = CreateImage("대화창", gameOverLineRoot.transform, new Color(0.95f, 0.93f, 0.92f, 1f));
        gameOverLineBox.raycastTarget = false;
        RectTransform boxRect = gameOverLineBox.rectTransform;
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0f);
        boxRect.sizeDelta = new Vector2(1500f, 240f);
        boxRect.anchoredPosition = new Vector2(0f, 150f);

        gameOverNameBox = CreateImage("이름표", gameOverLineRoot.transform, new Color(0.95f, 0.93f, 0.92f, 1f));
        gameOverNameBox.raycastTarget = false;
        RectTransform nameBoxRect = gameOverNameBox.rectTransform;
        nameBoxRect.anchorMin = nameBoxRect.anchorMax = new Vector2(0.5f, 0f);
        nameBoxRect.sizeDelta = new Vector2(260f, 72f);
        nameBoxRect.anchoredPosition = new Vector2(-600f, 290f);

        gameOverNameText = CreateText("이름", gameOverNameBox.transform, Stage1Dialogues.Girlfriend, 38f,
            new Color(1f, 0f, 0f, 1f));
        Stretch(gameOverNameText.rectTransform);

        gameOverLineText = CreateText("대사", gameOverLineBox.transform, Stage1Dialogues.GameOverLine, 52f,
            new Color(0.47f, 0.18f, 0.18f, 1f));
        Stretch(gameOverLineText.rectTransform);
        gameOverLineText.margin = new Vector4(70f, 30f, 70f, 30f);

        gameOverLineRoot.SetActive(false);
    }

    private void BuildGameOverPanel()
    {
        // 화려한 엔딩 화면 (EndingScreen.cs): 제목·설명·[다시 시작]/[로비로]/[게임 종료]
        endingScreen = EndingScreen.Create(overlayRoot.transform, Restart, BackToLobby, QuitGame);
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    // 실제 게임 대화창(DialogueManager)의 그림·위치·글자색을 그대로 가져옵니다. 없으면 기본 모양을 씁니다.
    private void MatchTeamDialogueBox()
    {
        DialogueManager dialogue = DialogueManager.Instance;
        if (dialogue == null)
            return;

        CopyBox(dialogue.DialoguePanelRect, gameOverLineBox);
        CopyBox(dialogue.NamePanelRect, gameOverNameBox);

        System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        TextMeshProUGUI teamLine = typeof(DialogueManager).GetField("dialogueText", flags)?.GetValue(dialogue) as TextMeshProUGUI;
        TextMeshProUGUI teamName = typeof(DialogueManager).GetField("nameText", flags)?.GetValue(dialogue) as TextMeshProUGUI;
        if (teamLine != null)
        {
            gameOverLineText.color = teamLine.color;
            gameOverLineText.font = teamLine.font;
        }
        if (teamName != null)
        {
            gameOverNameText.color = teamName.color;
            gameOverNameText.font = teamName.font;
        }
    }

    private static void CopyBox(RectTransform source, Image target)
    {
        if (source == null || target == null)
            return;

        Image sourceImage = source.GetComponent<Image>();
        if (sourceImage != null && sourceImage.sprite != null)
        {
            target.sprite = sourceImage.sprite;
            target.type = sourceImage.type;
            target.color = new Color(sourceImage.color.r, sourceImage.color.g, sourceImage.color.b, 1f);
            target.preserveAspect = sourceImage.preserveAspect;
        }

        // 같은 화면 자리에 놓기 (두 캔버스 모두 화면 덮개 방식)
        Vector3[] corners = new Vector3[4];
        source.GetWorldCorners(corners);
        RectTransform parent = (RectTransform)target.rectTransform.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, corners[0], null, out Vector2 min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, corners[2], null, out Vector2 max);
        if ((max - min).sqrMagnitude < 1f)
            return;

        RectTransform rect = target.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (min + max) * 0.5f - parent.rect.center;
        rect.sizeDelta = max - min;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;

        // 방 이름 글자와 같은 폰트(Galmuri9)를 사용합니다.
        TextMeshProUGUI roomText = SliderManager.Instance != null ? SliderManager.Instance.roomNameText : null;
        if (roomText != null && roomText.font != null)
            text.font = roomText.font;

        return text;
    }

    // 아래는 불투명하고 위로 갈수록 투명해지는 세로 그라데이션
    private static Sprite CreateFadeUpSprite()
    {
        const int height = 64;
        Texture2D texture = new Texture2D(1, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < height; y++)
        {
            float alpha = Mathf.SmoothStep(1f, 0f, y / (height - 1f));
            texture.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f));
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
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private static IEnumerator Fade(Graphic graphic, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(graphic, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(graphic, to);
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = to;
    }

    // Play 중 Inspector의 ⋮ 메뉴에서 실행해 기절 연출을 확인합니다.
    [ContextMenu("기절 테스트")]
    private void TestFaint()
    {
        Faint();
    }

    [ContextMenu("게임 오버 테스트")]
    private void TestGameOver()
    {
        ShowGameOver();
    }
}
