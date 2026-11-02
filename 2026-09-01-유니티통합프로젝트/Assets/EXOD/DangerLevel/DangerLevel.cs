using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 위험도(여자친구 등장) 시스템 — 하트 오른쪽 아이콘(팀 HUD의 Yan)을 이 코드가 움직입니다.
///
/// [위험도 0~100]  0~30 초록불 / 31~50 노랑불 / 51 이상 빨강불 / 등장 중 검은 바탕 흰 눈
///  - 새 일차가 시작되면 0, 여자친구가 나타나면 0
///
/// [오르는 경우]
///  - 시간: 2초마다 40% 확률로 +1 (대화·컷신·기절·산책·일차 넘김 페이드 중에는 멈춤)
///  - 방 이동: +4 (위험도 76 이상이면 +1, 침실로 들어갈 땐 0) ※ 2배 적용 안 함
///  - 물건 얻기: +5 (같은 물건은 하루에 한 번만, 산책 중에 얻은 건 0)
///  - 기믹 성공 +5 / 기믹 실패 +10 (각 기믹 코드가 GimmickSolved/GimmickFailed 호출)
///  - 현관문 비밀번호를 처음 푼 뒤부터 방 이동을 뺀 나머지는 2배
///  - 1일차에는 오르지 않음 (대신 튜토리얼)
///
/// [여자친구 등장]
///  - 76 이상: 구두 발소리 + 위험도가 오를 때마다 5초 간격으로 10% 확률 판정
///  - 100: 바로 등장 (위 확률 판정과 같이 돌아감)
///  - 70 이상에서 침실에 들어가면 바로 등장 (방 안이라 안전)
///  - 등장했을 때 침실에 없으면 걸림 → 하트 -1
///  - 산책에서 돌아온 뒤 15초 동안은 등장 없음
///
/// [경고]
///  - 방 이동·기믹으로 위험도가 100이 될 상황이면 "지금 이 행동을 하면 위험할 것 같다. 방으로 돌아갈까?"
///    (기믹은 실패 +10 기준) 이 대사 뒤로는 방 이동으로 위험도가 오르지 않음
///
/// [1일차 튜토리얼]
///  - 남은 시간 1분 30초에 발소리 + "방으로 들어가 있자" → 침실에 들어가면 여자친구 등장(안전)
///  - 그 뒤로 행동 요령 띠에 "여자친구가 올 것 같으면 방에 들어가 있자" 표시 (HideHintUnlocked)
///
/// 등장·걸림 대사는 임시입니다. (대사 담당이 아래 Lines 상수를 바꾸면 됨)
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
    private const int MoveAmountWhenHigh = 1;   // 76 이상
    private const int ItemAmount = 5;
    private const int GimmickSolvedAmount = 5;
    private const int GimmickFailedAmount = 10;
    private const int HighThreshold = 76;       // 발소리·확률 판정 시작
    private const float RollCooldown = 5f;
    private const float RollChance = 0.1f;
    private const int BedroomAppearThreshold = 70;
    private const float PostWalkGraceSeconds = 15f;
    private const float TutorialRemainingSeconds = 90f;

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

    private static readonly string[] AppearInRoomLines =
    {
        "자기야~ 나 왔어. 얌전히 방에 있었네? 착하다 ♡",
    };

    private static readonly string[] AppearCaughtLines =
    {
        "…자기야? 왜 방에 없어?",
        "어디 가려고 했던 거야?",
    };

    // ── 상태 ─────────────────────────────────────────────
    public static DangerLevel Instance { get; private set; }

    /// <summary>1일차 튜토리얼을 본 뒤 true (행동 요령 띠에 "방에 들어가 있자" 표시)</summary>
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
    private bool appearCaught;
    private bool tutorialShown;
    private bool tutorialWaitingForRoom;
    private bool waitingDialogueEnd;

    private readonly HashSet<string> itemsCountedToday = new HashSet<string>();
    private readonly HashSet<QuickSlotEntry> knownEntries = new HashSet<QuickSlotEntry>();

    private SliderManager slider;
    private Yan yan;
    private Sprite safeIcon, normalIcon, dangerIcon, appearedIcon;

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
        StopListeningDialogueEnd();

        if (Instance == this)
            Instance = null;
    }

    // ═══════════════════════════════════════════════════
    //  매 프레임
    // ═══════════════════════════════════════════════════

    private void Update()
    {
        UpdateIcon();

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

        if (day <= 1)
        {
            UpdateTutorial();
            SyncKnownEntries();
            return;
        }

        CheckNewItems();

        if (DayCycleController.IsTimePaused())
            return;

        // 시간: 2초마다 40% 확률로 +1
        tickTimer += Time.deltaTime;
        while (tickTimer >= TickSeconds)
        {
            tickTimer -= TickSeconds;
            if (Random.value < TickChance)
                Add(1 * Multiplier);
        }

        // 산책 후 대기 시간이 끝났는데 이미 100이면 바로 등장
        if (value >= Max && CanAppear())
            Appear();
    }

    private void LateUpdate()
    {
        // 1일차 튜토리얼: 대사가 끝난 뒤 침실에 있으면 여자친구 등장
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
        tickTimer = 0f;
        warnedNearMax = false;
        footstepsPlayed = false;
        itemsCountedToday.Clear();
        SyncKnownEntries();
    }

    // ═══════════════════════════════════════════════════
    //  오르는 경우
    // ═══════════════════════════════════════════════════

    // 현관문 비밀번호를 처음 푼 뒤부터 (방 이동 제외) 2배
    private static int Multiplier
    {
        get
        {
            Stage1FrontDoorGimmick frontDoor = FindFirstObjectByType<Stage1FrontDoorGimmick>();
            return frontDoor != null && frontDoor.IsPasswordSolved ? 2 : 1;
        }
    }

    private bool CountsNow()
    {
        int day = GManager.Instance != null ? GManager.Instance.Day : 1;
        return day > 1 && !Stage2HouseLink.IsInStage2 && !appearing && !DayController.IsTransitioning;
    }

    private void Add(int amount)
    {
        if (amount <= 0)
            return;

        value = Mathf.Min(Max, value + amount);

        if (value >= HighThreshold && !footstepsPlayed)
        {
            footstepsPlayed = true;
            EXODSound.Play(FootstepSound);
        }

        if (!CanAppear())
            return;

        if (value >= Max)
        {
            Appear();
            return;
        }

        // 76 이상: 오를 때마다 5초 간격으로 10% 확률
        if (value >= HighThreshold && Time.time >= lastRollTime + RollCooldown)
        {
            lastRollTime = Time.time;
            if (Random.value < RollChance)
                Appear();
        }
    }

    private void OnRoomChanged(string roomName)
    {
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

            if (CountsNow() && value >= BedroomAppearThreshold && CanAppear())
                Appear();
            return;
        }

        if (!CountsNow() || warnedNearMax)
            return;

        int amount = value >= HighThreshold ? MoveAmountWhenHigh : MoveAmount;
        if (value + amount >= Max)
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
                Add(ItemAmount * Multiplier);
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
    public static void GimmickSolved()
    {
        if (Instance != null && Instance.CountsNow())
            Instance.Add(GimmickSolvedAmount * Multiplier);
    }

    /// <summary>기믹을 틀렸을 때 (+10, 현관문 연 뒤 2배)</summary>
    public static void GimmickFailed()
    {
        if (Instance != null && Instance.CountsNow())
            Instance.Add(GimmickFailedAmount * Multiplier);
    }

    /// <summary>
    /// 기믹을 열기 전에 부릅니다. 실패(+10) 기준으로 100이 될 상황이면 경고 대사를 띄우고 true(이번엔 열지 않음)를 돌려줍니다.
    /// 경고는 한 번만 나오고, 다음에 다시 누르면 그대로 열립니다.
    /// </summary>
    public static bool WarnBeforeGimmick()
    {
        DangerLevel d = Instance;
        if (d == null || !d.CountsNow() || d.warnedNearMax)
            return false;

        if (d.value + GimmickFailedAmount * Multiplier < Max)
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
        if (appearing || Stage2HouseLink.IsInStage2 || DayController.IsTransitioning)
            return false;
        if (Time.time < graceUntil)
            return false;
        if (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy)
            return false;
        return true;
    }

    private void Appear()
    {
        if (appearing)
            return;

        bool inRoom = slider != null && slider.CurrentRoom == BedroomName;
        appearing = true;
        appearCaught = !inRoom;
        value = 0;
        UpdateIcon();

        if (!footstepsPlayed)
            EXODSound.Play(FootstepSound);

        List<DialogueLine> lines = new List<DialogueLine>();
        foreach (string text in inRoom ? AppearInRoomLines : AppearCaughtLines)
            lines.Add(new DialogueLine { speakerName = Stage1Dialogues.Girlfriend, text = text, dialogueType = DialogueType.Character });

        DialogueManager dialogue = DialogueManager.Instance;
        if (dialogue == null)
        {
            FinishAppear();
            return;
        }

        dialogue.StartDialogue(lines);
        StopListeningDialogueEnd();
        dialogue.onDialogueEnd.AddListener(FinishAppear);
        waitingDialogueEnd = true;
    }

    private void FinishAppear()
    {
        StopListeningDialogueEnd();

        appearing = false;
        value = 0;
        warnedNearMax = false;
        footstepsPlayed = false;
        lastRollTime = -999f;

        if (tutorialWaitingForRoom)
        {
            tutorialWaitingForRoom = false;
            HideHintUnlocked = true;
        }

        // 방에 없었으면 걸림 → 하트 -1 (0이 되면 PlayerFaintController가 게임 오버)
        if (appearCaught)
        {
            HeartManager hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
            if (hearts != null)
                hearts.HPM();
        }
        appearCaught = false;
    }

    private void StopListeningDialogueEnd()
    {
        if (!waitingDialogueEnd)
            return;

        waitingDialogueEnd = false;
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.onDialogueEnd.RemoveListener(FinishAppear);
    }

    // ── 1일차 튜토리얼 ───────────────────────────────────

    private void UpdateTutorial()
    {
        if (tutorialShown || DayCycleController.IsTimePaused())
            return;

        DayCycleController cycle = DayCycleController.Instance;
        if (cycle == null || cycle.RemainingSeconds > TutorialRemainingSeconds)
            return;

        tutorialShown = true;
        tutorialWaitingForRoom = true;
        footstepsPlayed = true;
        EXODSound.Play(FootstepSound);
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

        Sprite icon = appearing ? appearedIcon
                    : value <= 30 ? safeIcon
                    : value <= 50 ? normalIcon
                    : dangerIcon;
        if (icon != null && yan.StateRenderer.sprite != icon)
            yan.StateRenderer.sprite = icon;
    }
}
