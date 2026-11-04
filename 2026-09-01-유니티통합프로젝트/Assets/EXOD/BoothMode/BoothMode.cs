using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 부스 운영 모드 (사용자 요청 2026-11-03)
/// - 타이틀 화면 왼쪽 아래 스위치로 켜고 끔 (StartScreen). 껐다 켜도 유지(PlayerPrefs).
/// - 켜져 있으면 난이도 선택 카드 설명이 부스용으로 바뀜 (StartScreen / DifficultyPicker → FillDifficultyCard)
/// - 게임을 시작하면 제한시간 타이머가 돌고 (이지 5분 / 하드 10분), 행동 요령 제목 옆에 표시됨 (Stage1HelpBar)
/// </summary>
public static class BoothMode
{
    private const string PrefKey = "EXOD_BoothMode";
    private const string TitleSceneName = "StartScenes";
    public const float EasyLimitSeconds = 5f * 60f;
    public const float HardLimitSeconds = 10f * 60f;

    private static bool loaded;
    private static bool isOn;

    private static bool timerRunning;
    private static float pausedSeconds;      // 엔딩 중에 멈춰 있던 시간
    private static float lastTickTime;
    private static bool timeUpFired;

    // ─── 제한시간 초과 때 '이미 볼 수 있는 엔딩' (사용자 요청 2026-11-03) ───
    public enum FateEnding { Submission, Proposal, Escape }

    /// <summary>엔딩을 강제로 진행 중 (하루 시간·위험도·여자친구 등장 멈춤)</summary>
    public static bool ForcingEnding;
    /// <summary>[이지] '여기까지 하시겠습니까?'에서 [아니요]를 고른 적 있음 → 순응 엔딩</summary>
    public static bool GiveUpDeclined;
    /// <summary>손전등 + 종이 A~E(이지는 A)를 모두 가진 채 화장실 금고를 누른 적 있음 → 청혼 엔딩</summary>
    public static bool SafeTriedWithClues;
    /// <summary>현관문을 처음으로 열고 나간 적 있음 (Stage1FrontDoorGimmick.LeaveHouse) → [이지] 순응 엔딩 조건에 추가 (사용자 요청 2026-11-04)</summary>
    public static bool FrontDoorOpened;

    private static void ResetRunFlags()
    {
        timeUpFired = false;
        ForcingEnding = false;
        GiveUpDeclined = false;
        SafeTriedWithClues = false;
        FrontDoorOpened = false;
    }

    /// <summary>화장실 금고를 누를 때 (RestroomGimmick.OpenKeypad)</summary>
    public static void NoteSafeInteraction()
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        if (inventory == null)
            return;
        HashSet<string> ids = new HashSet<string>(inventory.Entries
            .Where(e => e != null && e.ItemData != null).Select(e => e.ItemData.ItemId));
        string[] papers = GameDifficulty.IsEasy ? new[] { "paper_a" } : new[] { "paper_a", "paper_b", "paper_c", "paper_d", "paper_e" };
        if (ids.Contains(FlashlightGimmick.FlashlightItemId) && papers.All(ids.Contains))
            SafeTriedWithClues = true;
    }

    /// <summary>손전등 + 종이 A~E(이지는 종이 A만)를 지금 퀵슬롯에 가지고 있는지</summary>
    public static bool HasFlashlightAndPapers()
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        if (inventory == null)
            return false;
        HashSet<string> ids = new HashSet<string>(inventory.Entries
            .Where(e => e != null && e.ItemData != null).Select(e => e.ItemData.ItemId));
        string[] papers = GameDifficulty.IsEasy ? new[] { "paper_a" } : new[] { "paper_a", "paper_b", "paper_c", "paper_d", "paper_e" };
        return ids.Contains(FlashlightGimmick.FlashlightItemId) && papers.All(ids.Contains);
    }

    public static List<FateEnding> AvailableEndings()
    {
        List<FateEnding> list = new List<FateEnding>();
        if (GameDifficulty.IsEasy && GiveUpDeclined && FrontDoorOpened)
            list.Add(FateEnding.Submission);
        // 청혼: 손전등 + 종이 A~E(이지는 A)를 지금 가지고 있으면 (2026-11-04 사용자 요청: 금고를 눌러 봤는지는 안 봄)
        if ((SafeTriedWithClues || HasFlashlightAndPapers()) && !ProposalEnding.HasPlayed)
            list.Add(FateEnding.Proposal);
        bool repaired = GameDifficulty.IsEasy
            ? EXOD.Walk.PhoneBoothProgress.PhoneRepaired
            : EXOD.Walk.PhoneBoothProgress.CordFixed && EXOD.Walk.PhoneBoothProgress.Button1Installed;   // 하드는 선 + [1]만
        if (EXOD.Walk.PhoneBoothProgress.AddressObtained && repaired)
            list.Add(FateEnding.Escape);
        return list;
    }
    private static float timerStart;
    private static float timerLimit;

    public static bool IsOn
    {
        get
        {
            if (!loaded)
            {
                isOn = PlayerPrefs.GetInt(PrefKey, 0) == 1;
                loaded = true;
            }
            return isOn;
        }
        set
        {
            isOn = value;
            loaded = true;
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        timerRunning = false;
        ResetRunFlags();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 타이틀로 돌아가면 타이머 멈춤
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == TitleSceneName)
        {
            timerRunning = false;
            ResetRunFlags();
        }
    }

    // ─── 제한시간 타이머 ───────────────────────────────

    /// <summary>난이도를 고르고 게임을 시작할 때 (처음 시작·다시 시작 모두)</summary>
    public static void StartTimer(bool easy)
    {
        timerRunning = IsOn;
        ResetRunFlags();
        pausedSeconds = 0f;
        lastTickTime = Time.realtimeSinceStartup;
        timerStart = Time.realtimeSinceStartup;
        timerLimit = easy ? EasyLimitSeconds : HardLimitSeconds;
    }

    public static bool TimerVisible => IsOn && timerRunning;

    public static float RemainingSeconds =>
        timerRunning ? Mathf.Max(0f, timerLimit - (Time.realtimeSinceStartup - timerStart - pausedSeconds)) : 0f;

    // 엔딩 중에는 제한시간이 흐르지 않음 (EndingState, 사용자 요청 2026-11-04)
    private static void TickPause()
    {
        float now = Time.realtimeSinceStartup;
        if (timerRunning && EndingState.IsEnding)
            pausedSeconds += now - lastTickTime;
        lastTickTime = now;
    }

    /// <summary>제한시간이 다 되면 한 번: 암전 + 트럼펫 + Game Over 대사 → [타이틀 화면으로 가기] (Stage1HelpBar가 매 프레임 부름)</summary>
    public static void CheckTimeUp()
    {
        TickPause();
        if (!TimerVisible || timeUpFired || EndingState.IsEnding || RemainingSeconds > 0f)
            return;   // 엔딩을 강제로 진행 중이면 시간 초과는 안 띄움
        PlayerFaintController faint = PlayerFaintController.Instance;
        if (faint == null || faint.IsBusy)
            return;
        timeUpFired = true;
        faint.ShowBoothTimeUp();
    }

    public static string TimerText()
    {
        int total = Mathf.CeilToInt(RemainingSeconds);
        return "제한시간: " + (total / 60) + "분 " + (total % 60).ToString("00") + "초";
    }

    // ─── 난이도 카드 내용 ───────────────────────────────

    public static readonly Vector2 BoothCardSize = new Vector2(540f, 430f);
    public static readonly Vector2 BoothWindowSize = new Vector2(1250f, 820f);

    private static readonly Color BodyColor = new Color(0.78f, 0.74f, 0.76f, 1f);
    private static readonly Color WarnColor = new Color(1f, 0.15f, 0.2f, 1f);

    /// <summary>부스 모드용 난이도 카드 글자들 (이름 아래). card는 BoothCardSize 크기여야 함</summary>
    public static void FillDifficultyCard(RectTransform card, bool easy, Color accent, TMP_FontAsset font)
    {
        TextMeshProUGUI name = Txt("이름", card, easy ? "이지" : "하드", 64f, accent, font, FontStyles.Normal);
        Top(name.rectTransform, 20f, 80f);

        // 픽셀 폰트라 Bold(가짜 굵게)를 쓰면 글자가 뭉개짐 → 굵게 없이 크기로만 강조 (2026-11-04)
        TextMeshProUGUI big = Txt("제한시간", card, easy ? "제한시간 5분." : "제한시간 10분.", 52f, Color.white, font, FontStyles.Normal);
        Top(big.rectTransform, 105f, 64f);

        float y = 180f;
        if (!easy)
        {
            TextMeshProUGUI warn = Txt("경고", card, "실패하면 간식, 보상을 받지 못해요!", 30f, WarnColor, font, FontStyles.Normal);
            warn.enableWordWrapping = false;   // 한 줄로 (두 줄로 넘치면 아래 설명과 겹침)
            warn.enableAutoSizing = true;
            warn.fontSizeMin = 20f;
            warn.fontSizeMax = 30f;
            Top(warn.rectTransform, y, 48f);
            warn.gameObject.AddComponent<BoothShake>();
            y += 58f;
        }

        string body = easy
            ? "중간에 그만 둘 수 있는 타이밍도 많아요.\n기믹의 난이도가 쉽고 양이 적어요."
            : "중간에 그만 둘 수 없어요..\n이지모드도 헤매면 20분이 넘게 걸려요\n시작하기 전에 잘 생각해 보세요";
        TextMeshProUGUI desc = Txt("설명", card, body, 26f, BodyColor, font, FontStyles.Normal);
        desc.enableWordWrapping = true;
        desc.lineSpacing = 6f;
        desc.alignment = TextAlignmentOptions.Top;
        RectTransform r = desc.rectTransform;
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 1f);
        r.offsetMin = new Vector2(24f, 18f);
        r.offsetMax = new Vector2(-24f, -y);
    }

    /// <summary>난이도 창 가운데 아래 (두 카드와 [뒤로] 사이): 회색 중도 포기 안내 + 노란 부스 직원 안내</summary>
    public static void AddStaffNote(RectTransform window, TMP_FontAsset font)
    {
        TextMeshProUGUI quit = Txt("중도 포기 안내", window,
            "중도 포기는 종료버튼을 눌러 나가면 됩니다. 만족 한 엔딩 조건이 있다면 그 엔딩을 볼 수 있습니다 (클리어판정)",
            23f, new Color(0.62f, 0.60f, 0.61f, 1f), font, FontStyles.Normal);
        RectTransform qr = quit.rectTransform;
        qr.anchorMin = qr.anchorMax = new Vector2(0.5f, 0.5f);
        qr.sizeDelta = new Vector2(1200f, 36f);
        qr.anchoredPosition = new Vector2(0f, -248f);

        TextMeshProUGUI note = Txt("부스 안내", window,
            "*기믹 푸는법 물어보면 부스 직원이 친절히 답해드립니다. 모르겠는건 많이 물어봐주세요",
            27f, new Color(1f, 0.85f, 0.25f, 1f), font, FontStyles.Normal);
        RectTransform r = note.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(1180f, 40f);
        r.anchoredPosition = new Vector2(0f, -288f);
    }

    private static void Top(RectTransform r, float fromTop, float height)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(16f, -fromTop - height);
        r.offsetMax = new Vector2(-16f, -fromTop);
    }

    private static TextMeshProUGUI Txt(string name, Transform parent, string value, float size, Color color, TMP_FontAsset font, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            t.font = font;
        t.text = value;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    // ─── 화면 전체 노이즈 (모드 전환 연출) ───────────────────────────────

    /// <summary>화면 전체에 노이즈가 꼈다가 사라짐. 가장 진할 때 onPeak 호출</summary>
    public static void PlayNoise(MonoBehaviour host, System.Action onPeak)
    {
        host.StartCoroutine(NoiseRoutine(onPeak));
    }

    private static IEnumerator NoiseRoutine(System.Action onPeak)
    {
        GameObject root = new GameObject("부스 모드 노이즈", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32700;

        GameObject imgGo = new GameObject("노이즈", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imgGo.transform.SetParent(root.transform, false);
        RectTransform rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        RawImage raw = imgGo.GetComponent<RawImage>();
        raw.raycastTarget = true;   // 연출 중 클릭 막기

        const int W = 192, H = 108;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        Color32[] px = new Color32[W * H];
        raw.texture = tex;

        EXODSound.Play("glitch", 0.6f);

        const float inTime = 0.35f, holdTime = 0.15f, outTime = 0.5f;
        float total = inTime + holdTime + outTime;
        bool peaked = false;
        float t = 0f;
        while (t < total)
        {
            // 매 프레임 새 노이즈 (가로 줄 흔들림 섞음)
            for (int y = 0; y < H; y++)
            {
                bool band = Random.value < 0.06f;
                byte tint = (byte)Random.Range(0, 60);
                for (int x = 0; x < W; x++)
                {
                    byte v = (byte)Random.Range(0, 256);
                    px[y * W + x] = band ? new Color32(v, tint, tint, 255) : new Color32(v, v, v, 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            raw.uvRect = new Rect(Random.value, Random.value, 1f, 1f);

            float a = t < inTime ? t / inTime : t < inTime + holdTime ? 1f : 1f - (t - inTime - holdTime) / outTime;
            raw.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a) * 0.92f);

            if (!peaked && t >= inTime)
            {
                peaked = true;
                onPeak?.Invoke();
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!peaked)
            onPeak?.Invoke();
        Object.Destroy(tex);
        Object.Destroy(root);
    }
}
