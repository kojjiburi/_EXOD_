using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 위험도(여자친구 등장) 시스템 — 하트 오른쪽 아이콘(팀 HUD의 Yan)을 이 코드가 움직입니다.
///
/// [위험도 0~100]  0~35 초록불 / 36~64 노랑불 / 65 이상 빨강불 (이지 모드는 71 이상) / 등장 중 검은 바탕 흰 눈 (2026-11-03 사용자 요청)
///  - 새 일차가 시작되면 0, 여자친구가 나타나면 0
///
/// [오르는 경우]
///  - 시간: 2초마다 40% 확률로 +1 (대화·컷신·기절·산책·일차 넘김 페이드 중에는 멈춤)
///          71 이상이면 대신 1초마다 +1 (위험도가 0으로 돌아가면 다시 2초마다 40%) (2026-11-03 사용자 요청)
///  - 현관문 비밀번호 틀림: +20 (기믹 실패 +10 대신, FrontDoorPasswordWrong)
///  - 방 이동: +4 (위험도 71 이상이면 +1, 침실로 들어갈 땐 0) ※ 2배 적용 안 함
///  - 물건 얻기: +5 (같은 물건은 하루에 한 번만, 산책 중에 얻은 건 0)
///  - 기믹 성공 +5 / 기믹 실패 +10 (각 기믹 코드가 GimmickSolved/GimmickFailed 호출)
///  - 눌러서 열리는 화면(조사 화면·키패드·비밀번호 창·메모 확대·손전등 화면)이 열릴 때 +1
///  - 상호작용이 안 되는 곳을 클릭할 때 +1 (2초 안에 5번이면 "소리가 많이 나는 것 같은데, 괜찮을까?")
///  - 현관문 비밀번호를 처음 푼 뒤부터 방 이동을 뺀 나머지는 2배 (이지 모드는 1.5배, 소수점은 모아 두었다가 1이 되면 더함)
///  - 첫 등장 이벤트(튜토리얼)가 끝나기 전에는 오르지 않음
///
/// [여자친구 등장]
///  - 71 이상: 구두 발소리 (10초마다 다시 들림). 발소리가 들릴 때 침실에 들어가면 바로 등장 (방 안이라 안전) (2026-11-03: 65 → 71)
///  - 76 이상: 위험도가 오를 때마다 5초 간격으로 확률 판정. 확률 = 10% - (76 - 위험도) x 2% → 76은 10%, 80은 18%, 90은 38%, 99는 56% (2026-11-03 사용자 요청)
///  - 100: 바로 등장 (위 확률 판정과 같이 돌아감)
///  - 등장했을 때 침실에 없으면 걸림 → 하트 -1 → 침실로 끌려가 방문이 다시 잠기고 황금 열쇠는 서랍 안으로
///  - 대사: Stage1Dialogues.VisitSafe / VisitCaught / VisitLockedAgain (nicezzomaru)
///
/// [2스테이지(산책)] 위험도는 오르지도 내리지도 않음. 아이콘은 그녀 상태를 보여 줌
///  - 앞을 봄: 초록 / 뒤돌기 전 말풍선: 노랑 / 뒤돌아봄: 빨강 / 들킴: 검정(흰 눈)
///  - 산책에서 돌아온 뒤 15초 동안은 등장 없음
///
/// [경고]
///  - 방 밖: 방 이동·기믹으로 위험도가 82 이상이 될 상황이면 "지금 이 행동을 하면 위험할 것 같다. 방으로 돌아갈까?" (2026-11-03: 100 → 82)
///  - 침실 안: 기믹으로 72 이상이 될 상황이면 경고 대신 여자친구가 방문을 벌컥 열고 확인하러 옴 → 위험도 0 (Stage1Dialogues.VisitRoomCheck)
///    구두 소리가 나는 동안(문 열리기 전)은 조작 가능. 그 사이 침실 밖으로 나가면 방 밖에서 들킨 컷신으로 바뀜
///    (기믹은 실패 +10 기준) 이 대사 뒤로는 방 이동으로 위험도가 오르지 않음
///
/// [첫 등장 이벤트(튜토리얼)] (2026-11-02 사용자 결정: 1일차 남은 1분 → 방 밖에 처음 나간 뒤 20초)
///  - 침실 밖에 처음 나간 뒤 게임 타이머(멈춘 시간 빼고)로 20초가 지나면 발소리 + "방으로 들어가 있자"
///    → 침실에 들어가면 여자친구 등장(안전). 이 이벤트가 끝나기 전에는 위험도가 오르지 않음
///  - 그 뒤로 행동 요령 띠에 "여자친구가 올 것 같으면 방에 들어가 있자" 표시 (HideHintUnlocked)
///  - 아이콘: 발소리 뒤 침실에 들어가기 전까지 빨강 고정 → 등장(이벤트) 중 검정 → 끝나면 원래대로 (2026-11-03 사용자 요청)
///
/// 1스테이지 씬(SliderManager가 있는 씬)에 자동으로 붙습니다.
/// </summary>
[DefaultExecutionOrder(290)]
public sealed class DangerLevel : MonoBehaviour
{
    // ── 수치 ─────────────────────────────────────────────
    public const int Max = 100;
    private const float TickSeconds = 2f;
    private const float TickChance = 0.4f;
    private const int MoveAmount = 4;
    private const int MoveAmountWhenHigh = 1;   // 71 이상 (2026-11-03 사용자 요청: 76 → 71)
    private const int MoveHighThreshold = 71;
    private const int ItemAmount = 5;
    private const int GimmickSolvedAmount = 5;
    private const int GimmickFailedAmount = 10;
    private const int UiOpenAmount = 1;
    private const int EmptyClickAmount = 1;
    private const int EmptyClickBurstCount = 5;
    private const float EmptyClickBurstSeconds = 2f;
    private const int FootstepThreshold = 71;   // 발소리, 이때 침실에 들어가면 등장 (2026-11-03: 65 → 71)
    private const int FastTickThreshold = 71;   // 이 이상이면 2초마다 40% 대신 1초마다 +1
    private const float FastTickSeconds = 1f;
    private const int FrontDoorWrongAmount = 20;
    private const float FootstepRepeatSeconds = 10f;
    private const int HighThreshold = 76;       // 확률 판정 시작 (2026-11-03 사용자 요청: 76 → 72 → 다시 76)
    private const int WarnFromOutside = 82;     // 방 밖: 이 값 이상이 될 상황이면 경고 (원래 100)
    private const int WarnFromInBedroom = 72;   // 침실 안: 이 값 이상이 될 상황이면 여자친구가 확인하러 옴
    private const string FootstepShortSound = "footsteps_heels";   // 침실 확인·잘 시간 방문: 긴 구두 발소리를 앞부분만 틀고 줄임
    private const string DoorBurstSound = "door_burst";
    private const float RollCooldown = 5f;
    private const float RollChance = 0.1f;          // 76일 때 10%
    private const float RollChancePerPoint = 0.02f; // 76보다 1 높을 때마다 +2%

    // 등장 확률 = 10% - (76 - 위험도) x 2%  (사용자 요청 2026-11-03)
    private float CurrentRollChance =>
        Mathf.Clamp01(RollChance - (RollThreshold - value) * RollChancePerPoint);

    private bool InBedroom => slider != null && slider.CurrentRoom == BedroomName;
    private int RollThreshold => HighThreshold;
    private int WarnFrom => InBedroom ? WarnFromInBedroom : WarnFromOutside;
    private const float PostWalkGraceSeconds = 15f;
    private const float TutorialDelayAfterLeavingSeconds = 20f;   // 침실 밖에 처음 나간 뒤 게임 타이머 20초에 첫 등장 이벤트

    public const string BedroomName = "침실";
    private const string FootstepSound = "footsteps_heels";
    private const string IconFolder = "EXODDanger/";

    // ── 대사 (임시) ──────────────────────────────────────
    private const string WarningText = "지금 이 행동을 하면 위험할 것 같다. 방으로 돌아갈까?";

    private static readonly string[] TutorialLines =
    {
        "…어디선가 또각또각, 구두 소리가 들린다.",
        "방으로 들어가 있자.",
    };

    private const string NoisyText = "…소리가 많이 나는 것 같은데, 괜찮을까?";

    // ── 상태 ─────────────────────────────────────────────
    public static DangerLevel Instance { get; private set; }

    /// <summary>첫 등장 이벤트(튜토리얼)를 본 뒤 true. 그 전에는 위험도가 오르지 않음 (행동 요령 띠에 "방에 들어가 있자" 표시)</summary>
    public static bool HideHintUnlocked { get; private set; }

    public static int Value => Instance != null ? Instance.value : 0;
    public static bool IsAppearing => Instance != null && Instance.appearing;

    private int value;
    private int lastDay = -1;
    private float tickTimer;
    private float lastRollTime = -999f;
    private float graceUntil = -999f;
    private bool wasInStage2;
    private bool warnedNearMax;
    private bool footstepsPlayed;
    private bool appearing;
    private bool tutorialShown;
    private bool tutorialWaitingForRoom;
    private bool leftBedroomOnce;          // 침실 밖에 한 번이라도 나갔는지
    private float timeSinceLeftBedroom;    // 그 뒤 흐른 게임 시간 (시간이 멈춘 동안은 안 셈)
    private float nextFootstepTime;
    private readonly List<float> emptyClickTimes = new List<float>();

    // 화면이 열릴 때 +1 을 세기 위한 이전 상태
    private bool wasInspectionOpen, wasMemoOpen, wasFlashlightOpen, wasRestroomKeypadOpen;

    private readonly HashSet<string> itemsCountedToday = new HashSet<string>();
    private readonly HashSet<QuickSlotEntry> knownEntries = new HashSet<QuickSlotEntry>();

    private SliderManager slider;
    private Yan yan;
    private EXOD.Walk.WalkYandere walkYandere;
    private bool walkCaughtFxPlayed;
    private bool redIconSoundPlayed;   // 위험도 아이콘이 붉은색이 되는 순간 i_see_you 한 번
    private Sprite safeIcon, normalIcon, dangerIcon, appearedIcon;
    // 검은 눈 아이콘: 나올 때 그림 1~7(글리치 6장 + 원래 그림)을 한 번, 그 뒤로는 4~7을 계속 반복해 지지직거림
    private const int GlitchFrameCount = 6;
    private const float GlitchFrameSeconds = 0.07f;
    private const int CleanFrame = -1;   // 원래 눈 그림 (7번)
    private static readonly int[] GlitchIntro = { 0, 1, 2, 3, 4, 5, CleanFrame };
    private static readonly int[] GlitchLoop = { 3, 4, 5, CleanFrame };
    private readonly Sprite[] glitchIcons = new Sprite[GlitchFrameCount];
    private bool showingAppearedIcon;
    private float appearedIconStartTime;

    // ═══════════════════════════════════════════════════
    //  설치
    // ═══════════════════════════════════════════════════

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        HideHintUnlocked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || Instance != null)
            return;

        SliderManager found = FindFirstObjectByType<SliderManager>();
        if (found == null || found.gameObject.scene != scene)
            return;

        // 새 게임(다시 시작·로비에서 시작)마다 튜토리얼 안내도 처음부터
        HideHintUnlocked = false;
        found.gameObject.AddComponent<DangerLevel>();
    }

    private void Awake()
    {
        Instance = this;
        safeIcon = Resources.Load<Sprite>(IconFolder + "Danger_Safe_Green");
        normalIcon = Resources.Load<Sprite>(IconFolder + "Danger_Normal_Yellow");
        dangerIcon = Resources.Load<Sprite>(IconFolder + "Danger_Danger_Red");
        appearedIcon = Resources.Load<Sprite>(IconFolder + "Danger_Appeared_Eyes");
        for (int i = 0; i < GlitchFrameCount; i++)
            glitchIcons[i] = Resources.Load<Sprite>(IconFolder + "Danger_Appeared_Glitch" + i);
    }

    private void Start()
    {
        slider = GetComponent<SliderManager>();
        if (slider == null)
            slider = SliderManager.Instance;
        if (slider != null)
            slider.RoomChanged += OnRoomChanged;

        SyncKnownEntries();
    }

    private void OnDestroy()
    {
        if (slider != null)
            slider.RoomChanged -= OnRoomChanged;

        if (Instance == this)
            Instance = null;
    }

    // ═══════════════════════════════════════════════════
    //  매 프레임
    // ═══════════════════════════════════════════════════

    private void Update()
    {
        UpdateIcon();
#if UNITY_EDITOR
        UpdateEditorTestKeys();
#endif

        int day = GManager.Instance != null ? GManager.Instance.Day : 1;
        if (day != lastDay)
            OnNewDay(day);

        // 산책 중에는 아무것도 오르지 않고, 돌아온 뒤 15초 동안은 등장 없음
        if (Stage2HouseLink.IsInStage2)
        {
            wasInStage2 = true;
            SyncKnownEntries(); // 산책에서 얻은 물건은 위험도 0
            return;
        }
        if (wasInStage2)
        {
            wasInStage2 = false;
            graceUntil = Time.time + PostWalkGraceSeconds;
            SyncKnownEntries();
        }

        if (appearing || DayController.IsTransitioning)
            return;

        // 첫 등장 이벤트가 끝나기 전에는 위험도 무효
        if (!HideHintUnlocked)
        {
            UpdateTutorial();
            SyncKnownEntries();
            UpdateUiOpenStates(false);
            return;
        }

        CheckNewItems();
        UpdateUiOpenStates(true);

        if (DayCycleController.IsTimePaused())
            return;

        CheckEmptyClick();

        // 발소리가 들리는 동안 10초마다 다시
        if (value >= FootstepThreshold && Time.time >= nextFootstepTime)
            PlayFootsteps();

        // 시간: 2초마다 40% 확률로 +1 / 71 이상이면 1초마다 +1 (사용자 요청 2026-11-03)
        tickTimer += Time.deltaTime;
        if (value >= FastTickThreshold)
        {
            while (tickTimer >= FastTickSeconds)
            {
                tickTimer -= FastTickSeconds;
                Add(1);
            }
        }
        else
        {
            while (tickTimer >= TickSeconds)
            {
                tickTimer -= TickSeconds;
                if (Random.value < TickChance)
                    Add(Scaled(1));
            }
        }

        // 산책 후 대기 시간이 끝났는데 이미 100이면 바로 등장
        if (value >= Max && CanAppear())
            Appear();
    }

    private void LateUpdate()
    {
        // 첫 등장 이벤트: 대사가 끝난 뒤 침실에 있으면 여자친구 등장
        if (tutorialWaitingForRoom && !appearing && !DayCycleController.IsTimePaused() &&
            slider != null && slider.CurrentRoom == BedroomName)
        {
            Appear();
        }
    }

    private void OnNewDay(int day)
    {
        lastDay = day;
        value = 0;
        multiplierCarry = 0f;
        tickTimer = 0f;
        warnedNearMax = false;
        footstepsPlayed = false;
        nextFootstepTime = 0f;
        emptyClickTimes.Clear();
        itemsCountedToday.Clear();
        SyncKnownEntries();
    }

    // ═══════════════════════════════════════════════════
    //  오르는 경우
    // ═══════════════════════════════════════════════════

    // 현관문 비밀번호를 처음 푼 뒤부터 (방 이동 제외) 2배, 이지 모드는 1.5배 (2026-11-03 사용자 요청)
    private static float Multiplier
    {
        get
        {
            Stage1FrontDoorGimmick frontDoor = FindFirstObjectByType<Stage1FrontDoorGimmick>();
            if (frontDoor == null || !frontDoor.IsPasswordSolved)
                return 1f;
            return GameDifficulty.IsEasy ? 1.5f : 2f;
        }
    }

    // 1.5배일 때 생기는 0.5는 모아 두었다가 1이 되면 더함
    private static float multiplierCarry;

    private static int Scaled(int amount)
    {
        multiplierCarry += amount * Multiplier;
        int whole = Mathf.FloorToInt(multiplierCarry + 0.0001f);
        multiplierCarry -= whole;
        return whole;
    }

    // 경고 판단용 (올림해서 넉넉하게)
    private static int Predicted(int amount) => Mathf.CeilToInt(amount * Multiplier - 0.0001f);

#if UNITY_EDITOR
    // 에디터 테스트용 단축키 (빌드에는 안 들어감): F8 일차 넘기기, F9 여자친구 등장(방 밖에서 들킨 것으로), F10 산책 나가기, F12 게임 화면 스크린샷(프로젝트 폴더)
    private void UpdateEditorTestKeys()
    {
        if (Input.GetKeyDown(KeyCode.F8) && DayCycleController.Instance != null)
            DayCycleController.Instance.AdvanceDay();
        if (Input.GetKeyDown(KeyCode.F9) && !Stage2HouseLink.IsInStage2)
            Appear(true);
        if (Input.GetKeyDown(KeyCode.F10) && !Stage2HouseLink.IsInStage2)
            Stage2HouseLink.RequestEnter();
        if (Input.GetKeyDown(KeyCode.F12))
            ScreenCapture.CaptureScreenshot("Screenshot_" + System.DateTime.Now.ToString("HHmmss") + ".png");
    }
#endif

    // 아이콘 색 기준 (사용자 요청 2026-11-03): 초록 0~35 (이지·하드 같음), 빨강 하드 65 이상 / 이지 71 이상
    /// <summary>집(1스테이지)에서 지금 아이콘 색 — 화면 가장자리 깜빡임(Stage1DangerVignette)이 씀</summary>
    public enum DangerColor { Green, Yellow, Red, Appeared }
    public static DangerColor IconColor { get; private set; }

    private const int GreenMax = 35;
    private static int RedFrom => 71;   // 이지·하드 모두 71부터 빨강 (2026-11-03)

    private bool CountsNow()
    {
        return HideHintUnlocked && !Stage2HouseLink.IsInStage2 && !appearing && !DayController.IsTransitioning && !BoothMode.ForcingEnding;
    }

    private void Add(int amount)
    {
        if (amount <= 0)
            return;

        value = Mathf.Min(Max, value + amount);

        if (value >= FootstepThreshold && !footstepsPlayed)
            PlayFootsteps();

        if (!CanAppear())
            return;

        if (value >= Max)
        {
            Appear();
            return;
        }

        // 76 이상: 오를 때마다 5초 간격으로 확률 판정 (위험도가 높을수록 잘 나옴)
        if (value >= RollThreshold && Time.time >= lastRollTime + RollCooldown)
        {
            lastRollTime = Time.time;
            if (Random.value < CurrentRollChance)
                Appear();
        }
    }

    private void OnRoomChanged(string roomName)
    {
        if (roomName != BedroomName)
            leftBedroomOnce = true;

        if (appearing || Stage2HouseLink.IsInStage2)
            return;

        if (roomName == BedroomName)
        {
            if (tutorialWaitingForRoom)
            {
                if (!DayCycleController.IsTimePaused())
                    Appear();
                return;
            }

            // 발소리가 들릴 때(71 이상) 침실에 들어가면 바로 등장
            if (CountsNow() && value >= FootstepThreshold && CanAppear())
                Appear();
            return;
        }

        if (!CountsNow() || warnedNearMax)
            return;

        int amount = value >= MoveHighThreshold ? MoveAmountWhenHigh : MoveAmount;
        if (value + amount >= WarnFromOutside)
        {
            ShowWarning();
            return;
        }

        Add(amount);
    }

    private void CheckNewItems()
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        if (inventory == null)
            return;

        foreach (QuickSlotEntry entry in inventory.Entries)
        {
            if (entry == null || knownEntries.Contains(entry))
                continue;

            knownEntries.Add(entry);
            string itemId = entry.ItemData != null ? entry.ItemData.ItemId : string.Empty;
            if (string.IsNullOrEmpty(itemId) || itemId == ProposalEnding.RingItemId || !itemsCountedToday.Add(itemId))
                continue;

            if (CountsNow())
                Add(Scaled(ItemAmount));
        }

        knownEntries.RemoveWhere(entry => entry == null || !ContainsEntry(inventory, entry));
    }

    private static bool ContainsEntry(QuickSlotInventory inventory, QuickSlotEntry target)
    {
        foreach (QuickSlotEntry entry in inventory.Entries)
        {
            if (entry == target)
                return true;
        }
        return false;
    }

    private void SyncKnownEntries()
    {
        knownEntries.Clear();
        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        if (inventory == null)
            return;

        foreach (QuickSlotEntry entry in inventory.Entries)
        {
            if (entry != null)
                knownEntries.Add(entry);
        }
    }

    // ── 기믹에서 부르는 곳 ────────────────────────────────

    /// <summary>기믹을 풀었을 때 (+5, 현관문 연 뒤 2배)</summary>
    /// <summary>지금까지 기믹 성공/실패가 불린 횟수 (위험도가 안 오르는 때도 셈). 잠수 방지(Stage1AntiIdle)가 "그냥 닫았는지" 판단할 때 씀</summary>
    public static int SolvedCount { get; private set; }
    public static int FailedCount { get; private set; }

    /// <summary>
    /// 조사 대사를 5초 동안 안 넘겨서 자동으로 넘겼을 때 (+5, 현관문 연 뒤 2배) — 잠수·시간 멈춤 악용 방지 (사용자 요청 2026-11-03)
    /// 이것 때문에 100이 될 상황이면, 기믹과 같은 경고("지금 이 행동을 하면 위험할 것 같다…")를 먼저 한 번 보여 줌 (대사가 끝난 뒤)
    /// </summary>
    public static void IdleAutoAdvanced()
    {
        DangerLevel d = Instance;
        if (d == null || !d.CountsNow())
            return;
        if (d.value + Predicted(IdleAutoAdvanceAmount) >= d.WarnFrom)
        {
            // 침실 안: 경고 대신 여자친구가 확인하러 옴 (대사가 끝난 뒤)
            if (d.InBedroom)
            {
                d.StartCoroutine(d.RoomCheckAfterDialogue());
                return;
            }
            if (!d.warnedNearMax)
            {
                d.warnedNearMax = true;
                d.StartCoroutine(d.ShowWarningAfterDialogue());
                return;
            }
        }
        d.Add(Scaled(IdleAutoAdvanceAmount));
    }

    private const int IdleAutoAdvanceAmount = 5;

    private System.Collections.IEnumerator RoomCheckAfterDialogue()
    {
        while (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            yield return null;
        if (CanAppear())
            AppearRoomCheck();
    }

    private System.Collections.IEnumerator ShowWarningAfterDialogue()
    {
        while (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            yield return null;
        DialogueManager.Instance?.StartDialogue(Stage1Dialogues.Thought(WarningText));
    }

    public static void GimmickSolved()
    {
        SolvedCount++;
        if (Instance != null && Instance.CountsNow())
            Instance.Add(Scaled(GimmickSolvedAmount));
    }

    /// <summary>기믹을 틀렸을 때 (+10, 현관문 연 뒤 2배)</summary>
    /// <summary>현관문 비밀번호를 틀렸을 때: +20 (기믹 실패 +10은 따로 안 더함, 사용자 요청 2026-11-03)</summary>
    public static void FrontDoorPasswordWrong()
    {
        FailedCount++;
        if (Instance != null && Instance.CountsNow())
            Instance.Add(FrontDoorWrongAmount);
    }

    public static void GimmickFailed()
    {
        FailedCount++;
        if (Instance != null && Instance.CountsNow())
            Instance.Add(Scaled(GimmickFailedAmount));
    }

    /// <summary>눌러서 열리는 화면(키패드·비밀번호 창 등)이 열렸을 때 (+1, 현관문 연 뒤 2배)</summary>
    public static void UiOpened()
    {
        if (Instance != null && Instance.CountsNow())
            Instance.Add(Scaled(UiOpenAmount));
    }

    /// <summary>
    /// 기믹을 열기 전에 부릅니다. 실패(+10) 기준으로 100이 될 상황이면 경고 대사를 띄우고 true(이번엔 열지 않음)를 돌려줍니다.
    /// 경고는 한 번만 나오고, 다음에 다시 누르면 그대로 열립니다.
    /// </summary>
    public static bool WarnBeforeGimmick()
    {
        DangerLevel d = Instance;
        if (d == null || !d.CountsNow())
            return false;

        if (d.value + Predicted(GimmickFailedAmount) < d.WarnFrom)
            return false;

        // 침실 안: 경고 대신 여자친구가 방문을 벌컥 열고 확인하러 옴 → 위험도 0 (이번엔 기믹을 열지 않음)
        if (d.InBedroom)
        {
            if (!d.CanAppear())
                return false;
            d.AppearRoomCheck();
            return true;
        }

        if (d.warnedNearMax)
            return false;

        d.ShowWarning();
        return true;
    }

    private void ShowWarning()
    {
        warnedNearMax = true;
        DialogueManager.Instance?.StartDialogue(Stage1Dialogues.Thought(WarningText));
    }

    // ═══════════════════════════════════════════════════
    //  여자친구 등장
    // ═══════════════════════════════════════════════════

    private bool CanAppear()
    {
        if (appearing || Stage2HouseLink.IsInStage2 || DayController.IsTransitioning || BoothMode.ForcingEnding)
            return false;
        if (Time.time < graceUntil)
            return false;
        if (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy)
            return false;
        return true;
    }

    private void PlayFootsteps()
    {
        footstepsPlayed = true;
        nextFootstepTime = Time.time + FootstepRepeatSeconds;
        EXODSound.Play(FootstepSound);
    }

    // 침실 안에서 위험도가 72 이상이 될 행동을 하려 할 때: 구두 소리 → 방문 벌컥 → 확인 대사 → 구두 소리 (위험도 0)
    private void AppearRoomCheck()
    {
        if (appearing)
            return;

        GimmickInterrupt.CloseAll();
        appearing = true;
        value = 0;
        UpdateIcon();
        StartCoroutine(RoomCheckRoutine());
    }

    private System.Collections.IEnumerator RoomCheckRoutine()
    {
        // 구두 소리가 나는 동안은 조작을 막지 않음. 그 사이 침실 밖으로 나가면 방 밖에서 들킨 것으로 (뒤 대사는 취소)
        PlayVisitFootsteps();
        float waited = 0f;
        while (waited < 1.5f)
        {
            if (!InBedroom)
            {
                StopVisitFootsteps(0.3f);
                appearing = false;
                Appear(true);
                yield break;
            }
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        StopVisitFootsteps(0.4f);
        EXODSound.Play(DoorBurstSound);
        yield return new WaitForSecondsRealtime(0.5f);
        PlayCutscene(Stage1Dialogues.VisitRoomCheck(), () =>
        {
            EXODSound.Play(FootstepSound);
            FinishAppear();
        });
    }

    // 하루 시간이 다 됐는데 침대로 넘기지 않았을 때 (DayCycleController): 침실 안이면 '아직도 안 자?' 방문, 밖이면 들킴 → 끝나면 then (다음 일차)
    private System.Action afterAppear;

    public static bool TryAppearAtDayEnd(System.Action then)
    {
        DangerLevel d = Instance;
        if (d == null || d.appearing || Stage2HouseLink.IsInStage2 || DayController.IsTransitioning)
            return false;
        if (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy)
            return false;

        d.afterAppear = then;
        if (d.InBedroom)
        {
            GimmickInterrupt.CloseAll();
            d.appearing = true;
            d.value = 0;
            d.UpdateIcon();
            d.StartCoroutine(d.BedtimeRoutine());
        }
        else
        {
            d.Appear(true);
        }
        return true;
    }

    private System.Collections.IEnumerator BedtimeRoutine()
    {
        PlayVisitFootsteps();
        yield return new WaitForSecondsRealtime(1.5f);
        StopVisitFootsteps(0.4f);
        PlayCutscene(Stage1Dialogues.VisitBedtime(), FinishAppear);
    }

    // 방문 직전 구두 소리: 긴 구두 발소리를 틀었다가 문이 열릴 때 서서히 끔
    private AudioSource visitStepSource;
    private Coroutine visitStepFade;

    private void PlayVisitFootsteps()
    {
        AudioClip clip = Resources.Load<AudioClip>("EXODStage1/Sfx/" + FootstepShortSound);
        if (clip == null)
            return;
        if (visitStepSource == null)
        {
            visitStepSource = gameObject.AddComponent<AudioSource>();
            visitStepSource.playOnAwake = false;
            visitStepSource.spatialBlend = 0f;
        }
        if (visitStepFade != null)
            StopCoroutine(visitStepFade);
        visitStepSource.clip = clip;
        visitStepSource.volume = 1f;   // 전체 볼륨은 AudioListener가 적용
        visitStepSource.Play();
    }

    private void StopVisitFootsteps(float fadeSeconds)
    {
        if (visitStepSource == null || !visitStepSource.isPlaying)
            return;
        if (visitStepFade != null)
            StopCoroutine(visitStepFade);
        visitStepFade = StartCoroutine(FadeVisitFootsteps(fadeSeconds));
    }

    private System.Collections.IEnumerator FadeVisitFootsteps(float seconds)
    {
        float start = visitStepSource.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            visitStepSource.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        visitStepSource.Stop();
        visitStepFade = null;
    }

    private void Appear(bool forceCaught = false)
    {
        if (appearing)
            return;

        // 열려 있던 기믹 창·조사 화면은 닫음 (실패 아님)
        GimmickInterrupt.CloseAll();

        bool inRoom = !forceCaught && slider != null && slider.CurrentRoom == BedroomName;
        appearing = true;
        value = 0;
        UpdateIcon();

        if (inRoom)
        {
            PlayCutscene(Stage1Dialogues.VisitSafe(), FinishAppear);
            return;
        }

        // 침실 밖에서 들킴 → (들킨 대사 동안 화면 색수차) → 하트 -1 → (게임 오버가 아니면) 침실로 끌려가 다시 갇힘
        // 색수차는 끌려가 다시 갇히는 대사까지 컷신 내내 유지 (사용자 요청 2026-11-03)
        ScreenAberration.Play(() => PlayCutscene(Stage1Dialogues.VisitCaught(), () =>
        {
            HeartManager hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
            if (hearts != null)
                hearts.HPM();

            if (hearts != null && hearts.HP <= 0)
            {
                ScreenAberration.Stop();
                afterAppear = null;   // 게임 오버면 다음 일차로 넘기지 않음
                FinishAppear();
                return;
            }

            LockBackInBedroom();
            PlayCutscene(Stage1Dialogues.VisitLockedAgain(), () =>
            {
                ScreenAberration.Stop();
                FinishAppear();
            });
        }), false, ScreenAberration.HouseCutscene);
    }

    private static void PlayCutscene(List<DialogueLine> lines, System.Action onFinished)
    {
        if (Stage1StoryController.Instance != null)
        {
            Stage1StoryController.Instance.PlayCutscene(lines, onFinished);
            return;
        }

        DialogueManager dialogue = DialogueManager.Instance;
        if (dialogue == null)
        {
            onFinished?.Invoke();
            return;
        }

        UnityEngine.Events.UnityAction once = null;
        once = () =>
        {
            dialogue.onDialogueEnd.RemoveListener(once);
            onFinished?.Invoke();
        };
        dialogue.onDialogueEnd.AddListener(once);
        dialogue.StartDialogue(lines);
    }

    // 침실로 끌려감: 방문이 다시 잠기고 황금 열쇠는 서랍 안으로 (서랍 비밀번호는 풀린 채라 서랍을 열면 다시 주울 수 있음)
    private void LockBackInBedroom()
    {
        ReturnGoldenKeyToDrawer();
        if (slider != null)
            slider.LockBedroomAgain();
    }

    private static void ReturnGoldenKeyToDrawer()
    {
        const string goldenKeyId = "golden_key";
        ItemPositionManager positions = ItemPositionManager.Instance;
        QuickSlotInventory inventory = QuickSlotInventory.Instance;

        // 열쇠가 어디에 있든 (퀵슬롯 / 다른 자리에 놓음) 먼저 거둬들입니다.
        ItemData keyItem = null;
        if (inventory != null)
        {
            foreach (QuickSlotEntry entry in inventory.Entries)
            {
                if (entry?.ItemData != null && entry.ItemData.ItemId == goldenKeyId)
                {
                    keyItem = entry.ItemData;
                    inventory.TryRemove(entry);
                    break;
                }
            }
        }

        if (positions == null)
            return;

        ItemPositionRecord drawerSlot = null;
        foreach (ItemPositionRecord record in positions.PositionRecords)
        {
            record.RefreshOccupancy();
            if (record.OriginalItemId == goldenKeyId)
                drawerSlot = record;
            else if (record.IsOccupied && record.CurrentItemId == goldenKeyId)
            {
                WorldItemPickup misplacedKey = record.CurrentPickup;
                keyItem = keyItem != null ? keyItem : misplacedKey.ItemData;
                record.SetOccupant(null);
                Destroy(misplacedKey.gameObject);
            }
        }

        // 서랍 자리에 열쇠를 놓으면 PlayerSystemsSceneBridge가 서랍 안으로 넣어 줍니다.
        if (drawerSlot != null && keyItem != null && !(drawerSlot.IsOccupied && drawerSlot.CurrentItemId == goldenKeyId))
            positions.TryPlaceItem(keyItem, drawerSlot.OriginalPosition);
    }

    private void FinishAppear()
    {
        appearing = false;
        value = 0;
        warnedNearMax = false;
        footstepsPlayed = false;
        lastRollTime = -999f;
        emptyClickTimes.Clear();

        if (tutorialWaitingForRoom)
        {
            tutorialWaitingForRoom = false;
            HideHintUnlocked = true;
        }

        // 하루 시간이 다 돼서 온 경우: 끝나면 다음 일차로
        System.Action then = afterAppear;
        afterAppear = null;
        then?.Invoke();
    }

    // ── 화면 열림 / 빈 곳 클릭 ───────────────────────────

    // 조사 화면·메모 확대·손전등 화면·화장실 금고 키패드가 새로 열리면 +1
    // (서랍 비밀번호 창·냉장고/현관 키패드는 각 코드에서 UiOpened 호출)
    private void UpdateUiOpenStates(bool count)
    {
        bool inspection = InspectionManager.Instance != null && InspectionManager.Instance.IsOpen;
        MemoZoom memo = FindFirstObjectByType<MemoZoom>();
        bool memoOpen = memo != null && memo.IsOpen;
        FlashlightGimmick flashlight = FlashlightGimmick.Instance;
        bool flashlightOpen = flashlight != null && flashlight.IsViewerOpen;
        bool restroomKeypad = RestroomGimmick.Instance != null && RestroomGimmick.Instance.IsKeypadOpen;

        if (count)
        {
            int opened = 0;
            if (inspection && !wasInspectionOpen) opened++;
            // 메모 확대·손전등 화면은 퀵슬롯 안 상호작용이라 위험도 없음 (사용자 요청 2026-11-03)
            if (restroomKeypad && !wasRestroomKeypadOpen) opened++;
            if (opened > 0 && CountsNow())
                Add(Scaled(opened * UiOpenAmount));
        }

        wasInspectionOpen = inspection;
        wasMemoOpen = memoOpen;
        wasFlashlightOpen = flashlightOpen;
        wasRestroomKeypadOpen = restroomKeypad;
    }

    // 상호작용이 안 되는 곳을 클릭하면 +1, 2초 안에 5번이면 "소리가 많이 나는 것 같은데, 괜찮을까?"
    private void CheckEmptyClick()
    {
        if (!Input.GetMouseButtonDown(0) || !IsEmptySpot(Input.mousePosition))
            return;

        float now = Time.time;
        emptyClickTimes.Add(now);
        emptyClickTimes.RemoveAll(t => now - t > EmptyClickBurstSeconds);

        if (emptyClickTimes.Count >= EmptyClickBurstCount)
        {
            emptyClickTimes.Clear();
            DialogueManager.Instance?.StartDialogue(Stage1Dialogues.Thought(NoisyText));
        }

        if (CountsNow())
            Add(Scaled(EmptyClickAmount));
    }

    private static bool IsEmptySpot(Vector2 screenPosition)
    {
        // 다른 화면이 열려 있을 때의 클릭은 세지 않음
        if (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen)
            return false;
        if (RestroomGimmick.Instance != null && RestroomGimmick.Instance.IsKeypadOpen)
            return false;
        if (FlashlightGimmick.Instance != null && (FlashlightGimmick.Instance.IsViewerOpen || FlashlightGimmick.Instance.IsSelectingTarget))
            return false;
        if (GameMenu.IsOpen || (DialogueLog.Instance != null && DialogueLog.Instance.IsOpen))
            return false;

        UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null)
        {
            var pointer = new UnityEngine.EventSystems.PointerEventData(eventSystem) { position = screenPosition };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            foreach (var hit in hits)
            {
                if (IsInteractive(hit.gameObject))
                    return false;
            }
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(cam.transform.position.z)));
            foreach (Collider2D col in Physics2D.OverlapPointAll(world))
            {
                if (IsInteractive(col.gameObject))
                    return false;
            }
        }

        return true;
    }

    private static bool IsInteractive(GameObject go)
    {
        if (go == null)
            return false;
        if (go.GetComponentInParent<UnityEngine.UI.Selectable>() != null)
            return true;
        if (go.GetComponentInParent<UnityEngine.EventSystems.EventTrigger>() != null)
            return true;
        if (go.GetComponentInParent<UnityEngine.EventSystems.IPointerClickHandler>() != null)
            return true;
        if (go.GetComponentInParent<UnityEngine.EventSystems.IPointerDownHandler>() != null)
            return true;
        if (go.GetComponentInParent<UnityEngine.EventSystems.IBeginDragHandler>() != null)
            return true;
        return false;
    }

    // ── 첫 등장 이벤트(튜토리얼) ───────────────────────────────────

    private void UpdateTutorial()
    {
        if (tutorialShown || !leftBedroomOnce || DayCycleController.IsTimePaused())
            return;

        // 침실 밖에 처음 나간 뒤 게임 시간으로 20초
        timeSinceLeftBedroom += Time.deltaTime;
        if (timeSinceLeftBedroom < TutorialDelayAfterLeavingSeconds)
            return;

        tutorialShown = true;
        tutorialWaitingForRoom = true;
        GimmickInterrupt.CloseAll();
        PlayFootsteps();
        DialogueManager.Instance?.StartDialogue(Stage1Dialogues.Thought(TutorialLines));
    }

    // ═══════════════════════════════════════════════════
    //  아이콘 (팀 HUD의 Yan 아이콘 자리)
    // ═══════════════════════════════════════════════════

    private void UpdateIcon()
    {
        if (yan == null)
        {
            yan = FindFirstObjectByType<Yan>(FindObjectsInactive.Include);
            if (yan == null)
                return;
        }

        yan.Emergence = value;
        if (yan.StateRenderer == null)
            return;

        Sprite icon;
        if (Stage2HouseLink.IsInStage2)
        {
            // 산책 중: 그녀 상태 (앞을 봄 초록 / 말풍선 노랑 / 뒤돌아봄 빨강 / 들킴 검정)
            if (walkYandere == null)
                walkYandere = FindFirstObjectByType<EXOD.Walk.WalkYandere>();
            bool caught = walkYandere != null && walkYandere.State == EXOD.Walk.YandereState.Caught;
            // 들킨 순간 화면 색수차 (들킨 대사가 끝날 때까지)
            if (caught && !walkCaughtFxPlayed)
                ScreenAberration.Play(null, true, ScreenAberration.WalkCutscene);
            walkCaughtFxPlayed = caught;

            if (walkYandere == null)
                icon = safeIcon;
            else if (caught)
                icon = appearedIcon;
            else if (walkYandere.State == EXOD.Walk.YandereState.Watching)
                icon = dangerIcon;
            else if (walkYandere.IsTurnWarningShown)
                icon = normalIcon;
            else
                icon = safeIcon;
        }
        else
        {
            walkYandere = null;
            walkCaughtFxPlayed = false;
            icon = appearing ? appearedIcon
                 : tutorialWaitingForRoom ? dangerIcon   // 첫 등장 이벤트: 발소리 뒤 빨강 고정
                 : value <= GreenMax ? safeIcon
                 : value < RedFrom ? normalIcon
                 : dangerIcon;

            bool red = icon == dangerIcon;
            if (red && !redIconSoundPlayed)
                EXODSound.Play("i_see_you", 0.5f);
            redIconSoundPlayed = red;
            IconColor = icon == normalIcon ? DangerColor.Yellow : icon == dangerIcon ? DangerColor.Red
                      : icon == appearedIcon ? DangerColor.Appeared : DangerColor.Green;
        }
        icon = ApplyAppearGlitch(icon);
        if (icon != null && yan.StateRenderer.sprite != icon)
            yan.StateRenderer.sprite = icon;
    }

    // 검은 눈 아이콘이면 글리치 그림을 돌림 (처음 1~7 한 번, 그 뒤 4~7 반복)
    private Sprite ApplyAppearGlitch(Sprite icon)
    {
        if (icon == null || icon != appearedIcon)
        {
            showingAppearedIcon = false;
            return icon;
        }

        if (!showingAppearedIcon)
        {
            showingAppearedIcon = true;
            appearedIconStartTime = Time.unscaledTime;
        }

        int step = (int)((Time.unscaledTime - appearedIconStartTime) / GlitchFrameSeconds);
        int frameIndex = step < GlitchIntro.Length
            ? GlitchIntro[step]
            : GlitchLoop[(step - GlitchIntro.Length) % GlitchLoop.Length];
        if (frameIndex == CleanFrame)
            return icon;

        Sprite frame = glitchIcons[frameIndex];
        return frame != null ? frame : icon;
    }
}
