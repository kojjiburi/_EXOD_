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
///  - 눌러서 열리는 화면(조사 화면·키패드·비밀번호 창·메모 확대·손전등 화면)이 열릴 때 +1
///  - 상호작용이 안 되는 곳을 클릭할 때 +1 (2초 안에 5번이면 "소리가 많이 나는 것 같은데, 괜찮을까?")
///  - 현관문 비밀번호를 처음 푼 뒤부터 방 이동을 뺀 나머지는 2배
///  - 1일차에는 오르지 않음 (대신 튜토리얼)
///
/// [여자친구 등장]
///  - 65 이상: 구두 발소리 (10초마다 다시 들림). 발소리가 들릴 때 침실에 들어가면 바로 등장 (방 안이라 안전)
///  - 76 이상: 위험도가 오를 때마다 5초 간격으로 10% 확률 판정
///  - 100: 바로 등장 (위 확률 판정과 같이 돌아감)
///  - 등장했을 때 침실에 없으면 걸림 → 하트 -1 → 침실로 끌려가 방문이 다시 잠기고 황금 열쇠는 서랍 안으로
///  - 대사: Stage1Dialogues.VisitSafe / VisitCaught / VisitLockedAgain (nicezzomaru)
///
/// [2스테이지(산책)] 위험도는 오르지도 내리지도 않음. 아이콘은 그녀 상태를 보여 줌
///  - 앞을 봄: 초록 / 뒤돌기 전 말풍선: 노랑 / 뒤돌아봄: 빨강 / 들킴: 검정(흰 눈)
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
    private const int UiOpenAmount = 1;
    private const int EmptyClickAmount = 1;
    private const int EmptyClickBurstCount = 5;
    private const float EmptyClickBurstSeconds = 2f;
    private const int FootstepThreshold = 65;   // 발소리, 이때 침실에 들어가면 등장
    private const float FootstepRepeatSeconds = 10f;
    private const int HighThreshold = 76;       // 확률 판정 시작
    private const float RollCooldown = 5f;
    private const float RollChance = 0.1f;
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

    private const string NoisyText = "…소리가 많이 나는 것 같은데, 괜찮을까?";

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
    private bool tutorialShown;
    private bool tutorialWaitingForRoom;
    private float nextFootstepTime;
    private readonly List<float> emptyClickTimes = new List<float>();

    // 화면이 열릴 때 +1 을 세기 위한 이전 상태
    private bool wasInspectionOpen, wasMemoOpen, wasFlashlightOpen, wasRestroomKeypadOpen;

    private readonly HashSet<string> itemsCountedToday = new HashSet<string>();
    private readonly HashSet<QuickSlotEntry> knownEntries = new HashSet<QuickSlotEntry>();

    private SliderManager slider;
    private Yan yan;
    private EXOD.Walk.WalkYandere walkYandere;
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
        nextFootstepTime = 0f;
        emptyClickTimes.Clear();
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

        if (value >= FootstepThreshold && !footstepsPlayed)
            PlayFootsteps();

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

            // 발소리가 들릴 때(65 이상) 침실에 들어가면 바로 등장
            if (CountsNow() && value >= FootstepThreshold && CanAppear())
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

    /// <summary>눌러서 열리는 화면(키패드·비밀번호 창 등)이 열렸을 때 (+1, 현관문 연 뒤 2배)</summary>
    public static void UiOpened()
    {
        if (Instance != null && Instance.CountsNow())
            Instance.Add(UiOpenAmount * Multiplier);
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

    private void PlayFootsteps()
    {
        footstepsPlayed = true;
        nextFootstepTime = Time.time + FootstepRepeatSeconds;
        EXODSound.Play(FootstepSound);
    }

    private void Appear()
    {
        if (appearing)
            return;

        bool inRoom = slider != null && slider.CurrentRoom == BedroomName;
        appearing = true;
        value = 0;
        UpdateIcon();

        if (inRoom)
        {
            PlayCutscene(Stage1Dialogues.VisitSafe(), FinishAppear);
            return;
        }

        // 침실 밖에서 들킴 → 하트 -1 → (게임 오버가 아니면) 침실로 끌려가 다시 갇힘
        PlayCutscene(Stage1Dialogues.VisitCaught(), () =>
        {
            HeartManager hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
            if (hearts != null)
                hearts.HPM();

            if (hearts != null && hearts.HP <= 0)
            {
                FinishAppear();
                return;
            }

            LockBackInBedroom();
            PlayCutscene(Stage1Dialogues.VisitLockedAgain(), FinishAppear);
        });
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
            if (memoOpen && !wasMemoOpen) opened++;
            if (flashlightOpen && !wasFlashlightOpen) opened++;
            if (restroomKeypad && !wasRestroomKeypadOpen) opened++;
            if (opened > 0 && CountsNow())
                Add(opened * UiOpenAmount * Multiplier);
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
            Add(EmptyClickAmount * Multiplier);
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
            if (walkYandere == null)
                icon = safeIcon;
            else if (walkYandere.State == EXOD.Walk.YandereState.Caught)
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
            icon = appearing ? appearedIcon
                 : value <= 30 ? safeIcon
                 : value <= 50 ? normalIcon
                 : dangerIcon;
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
