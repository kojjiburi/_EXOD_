using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EXOD.Walk;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 1스테이지(집) ↔ 2스테이지(산책) 연결
/// - 1스테이지 현관문을 나서면 컷신(지금은 테스트 문구, 친구가 만들 예정) 뒤에 2스테이지 산책 씬을 집 씬 위에 추가로(Additive) 엽니다.
///   1스테이지 씬은 그대로 남아 있으므로 진행 상황이 유지됩니다. 산책 중에는 3분 타이머(DayCycleController)가 멈추고, 복귀 시 성공/실패 상관없이 1분을 뺍니다.
/// - 2스테이지 대사는 1스테이지와 같은 DialogueManager 대화창으로, 산책에서 주운 아이템은 팀 퀵슬롯으로 들어갑니다.
/// - 산책이 끝나면(2스테이지 1분 끝, 왕복 완료, Esc로 그만둠, 들킴, 1스테이지 일차 넘어감) 산책 씬을 닫고 복도로 돌아옵니다.
/// - 산책 결과로 현관문이 잠긴 날(DayClock 기준)에는 다시 나갈 수 없습니다.
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
[DefaultExecutionOrder(225)]
public sealed class Stage2HouseLink : MonoBehaviour
{
    public static Stage2HouseLink Instance { get; private set; }

    public const string WalkSceneName = "Stage2_Walk";
    private const string ReturnRoomName = "복도";
    private const string Stage2RoomLabel = "산책";

    // 현관문을 나설 때 나오는 컷신은 Stage1Dialogues.Stage2EnterFirst / Stage2EnterAgain (처음 / 두 번째부터)

    private const string DoorLockedText = "오늘은 더 이상 밖에 나갈 수 없다…";

    // 2스테이지에 있는 동안 1스테이지 대화창은 그대로 씁니다.
    private static readonly HashSet<string> KeepVisibleOnCanvas = new() { "대화창", "이름표" };

    private bool isTransitioning;
    private bool hasEnteredOnce;
    private bool inStage2;
    private readonly List<GameObject> hiddenObjects = new();
    private Camera hiddenCamera;
    private string savedRoomLabel;

    public const float WalkTimeCostSeconds = 60f;

    public static bool IsInStage2 => Instance != null && Instance.inStage2;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == WalkSceneName || FindFirstObjectByType<Stage2HouseLink>() != null)
            return;

        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null)
            slider.gameObject.AddComponent<Stage2HouseLink>();
    }

    private GManager subscribedDayManager;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // 전화 부스 탈출 엔딩은 산책이 끝난 뒤 집에서 나오므로, 산책 대사 연결(팀 대화창)은 집에서도 유지합니다.
        WalkDialogue.ExternalShow = ShowWithTeamDialogue;
        WalkDialogue.ExternalIsOpen = () => DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen;

        // 집에 있는 동안에는 산책 시계(DayClock)가 없으므로, 일차가 넘어간 것을 전화 부스 엔딩에 직접 알려 줍니다.
        subscribedDayManager = GManager.Instance != null ? GManager.Instance : FindFirstObjectByType<GManager>(FindObjectsInactive.Include);
        if (subscribedDayManager != null)
            subscribedDayManager.onDayChanged += OnTeamDayChanged;

        // 전화 부스 탈출 엔딩의 [다시 시작]/[로비로]도 팀 게임 오버 화면과 같은 동작 (퀵슬롯·진행 상황 초기화)
        PhoneBoothEnding.RestartAction = () => PlayerFaintController.Instance?.Restart();
        PhoneBoothEnding.LobbyAction = () => PlayerFaintController.Instance?.BackToLobby();
        // 탈출 엔딩 화면도 다른 엔딩과 같은 화려한 화면으로 (경찰차 불빛 분위기)
        PhoneBoothEnding.ScreenOverride = (title, subtitle) =>
        {
            if (PlayerFaintController.Instance == null)
                return false;
            PlayerFaintController.Instance.ShowEnding(null, title, subtitle, EndingStyle.Escape);
            return true;
        };
    }

    private void OnDestroy()
    {
        if (subscribedDayManager != null)
            subscribedDayManager.onDayChanged -= OnTeamDayChanged;
        UnhookStage2();
        WalkDialogue.ExternalShow = null;
        WalkDialogue.ExternalIsOpen = null;
        if (Instance == this)
            Instance = null;
    }

    private static void OnTeamDayChanged(int day)
    {
        PhoneBoothEnding.NotifyDayAdvanced(day);
    }

    // ═══════════════════════════════════════════════════
    //  입장
    // ═══════════════════════════════════════════════════

    /// <summary>현관문을 나설 때 호출합니다. (Stage1FrontDoorGimmick)</summary>
    public static void RequestEnter()
    {
        if (Instance == null)
        {
            Debug.LogError("[Stage2Link] Stage2HouseLink가 없습니다.");
            return;
        }

        Instance.TryEnter();
    }

    private void TryEnter()
    {
        if (isTransitioning || inStage2)
            return;

        if (DayClock.IsFrontDoorLockedToday())
        {
            DialogueManager.Instance?.StartDialogue(Stage1Dialogues.Thought(DoorLockedText));
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(WalkSceneName))
        {
            Debug.LogError($"[Stage2Link] '{WalkSceneName}' 씬을 불러올 수 없습니다. Build Settings 씬 목록을 확인하세요.");
            return;
        }

        isTransitioning = true;
        List<DialogueLine> cutscene = hasEnteredOnce
            ? Stage1Dialogues.Stage2EnterAgain()
            : Stage1Dialogues.Stage2EnterFirst();
        hasEnteredOnce = true;

        if (Stage1StoryController.Instance != null)
            Stage1StoryController.Instance.PlayCutscene(cutscene, () => StartCoroutine(EnterRoutine()));
        else
            StartCoroutine(EnterRoutine());
    }

    private IEnumerator EnterRoutine()
    {
        yield return WalkFade.FadeOut();

        HookStage2();
        HideStage1();

        AsyncOperation load = SceneManager.LoadSceneAsync(WalkSceneName, LoadSceneMode.Additive);
        while (load != null && !load.isDone)
            yield return null;

        Scene walkScene = SceneManager.GetSceneByName(WalkSceneName);
        if (walkScene.IsValid())
            SceneManager.SetActiveScene(walkScene);

        // 산책 씬이 자기 EventSystem·오디오 리스너를 가지고 있으면 1스테이지 것과 겹치지 않게 끕니다.
        DisableDuplicates(walkScene);

        inStage2 = true;
        isTransitioning = false;
        // 화면 밝히기·첫 대사는 WalkGameManager가 합니다.
    }

    // ═══════════════════════════════════════════════════
    //  복귀
    // ═══════════════════════════════════════════════════

    private void OnWalkFinished(WalkResult result)
    {
        if (!inStage2)
            return;

        StartCoroutine(ReturnRoutine(result));
    }

    private IEnumerator ReturnRoutine(WalkResult result)
    {
        isTransitioning = true;
        UnhookStage2();

        // WalkGameManager가 이미 화면을 어둡게 한 상태입니다.
        Scene stage1 = gameObject.scene;
        if (stage1.IsValid())
            SceneManager.SetActiveScene(stage1);

        Scene walkScene = SceneManager.GetSceneByName(WalkSceneName);
        if (walkScene.IsValid() && walkScene.isLoaded)
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(walkScene);
            while (unload != null && !unload.isDone)
                yield return null;
        }

        // 산책 씬 밖에 만들어진 2스테이지 시계가 남아 있으면 지웁니다.
        if (DayClock.Instance != null)
            Destroy(DayClock.Instance.gameObject);

        RestoreStage1();
        inStage2 = false;

        // 산책은 성공이든 실패든 1스테이지 하루(3분)에서 1분을 씁니다.
        if (DayCycleController.Instance != null)
            DayCycleController.Instance.ConsumeTime(WalkTimeCostSeconds);

        yield return WalkFade.FadeIn();
        isTransitioning = false;

        // 산책 중 그녀에게 들켰으면 하트 -1 (소리·붉은 화면은 Stage1SoundEffects, HP 0이면 PlayerFaintController가 게임 오버)
        if (result == WalkResult.Caught)
        {
            HeartManager hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
            if (hearts != null)
            {
                hearts.HPM();
                if (hearts.HP <= 0)
                {
                    WalkSession.LastResult = WalkResult.None;
                    yield break;
                }
            }
        }

        string message = WalkGameManager.ResultLabel(result);
        WalkSession.LastResult = WalkResult.None;
        if (!string.IsNullOrEmpty(message))
            DialogueManager.Instance?.StartDialogue(Stage1Dialogues.Thought(message));
    }

    // ═══════════════════════════════════════════════════
    //  1스테이지 화면 숨기기 / 되돌리기
    // ═══════════════════════════════════════════════════

    private void HideStage1()
    {
        hiddenObjects.Clear();
        SliderManager slider = SliderManager.Instance;

        // 방 화면·기믹 등 1스테이지 메인 캔버스 내용 (대화창·이름표·방 이름 패널은 남김)
        Canvas mainCanvas = slider != null && slider.sliderPanel != null
            ? slider.sliderPanel.GetComponentInParent<Canvas>(true)
            : null;
        if (mainCanvas != null)
        {
            foreach (Transform child in mainCanvas.transform)
            {
                if (KeepVisibleOnCanvas.Contains(child.name) ||
                    (slider.sliderPanel != null && child.gameObject == slider.sliderPanel))
                {
                    continue;
                }

                Hide(child.gameObject);
            }
        }

        // 방 이동 화살표는 숨기고, 방 이름 자리에는 "산책"을 표시 (아래 3분 타이머·2스테이지 타이머는 그대로)
        if (slider != null)
        {
            if (slider.leftButton != null) Hide(slider.leftButton.gameObject);
            if (slider.rightButton != null) Hide(slider.rightButton.gameObject);
            if (slider.roomNameText != null)
            {
                savedRoomLabel = slider.roomNameText.text;
                slider.roomNameText.text = Stage2RoomLabel;
            }
        }

        // 1스테이지 카메라는 끄고(오디오 리스너는 유지) 산책 씬 카메라로 봅니다.
        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam.gameObject.scene == gameObject.scene && cam.enabled && cam.CompareTag("MainCamera"))
            {
                hiddenCamera = cam;
                cam.enabled = false;
                break;
            }
        }
    }

    private void Hide(GameObject target)
    {
        if (target == null || !target.activeSelf)
            return;

        target.SetActive(false);
        hiddenObjects.Add(target);
    }

    private void RestoreStage1()
    {
        foreach (GameObject target in hiddenObjects)
        {
            if (target != null)
                target.SetActive(true);
        }
        hiddenObjects.Clear();

        if (hiddenCamera != null)
            hiddenCamera.enabled = true;
        hiddenCamera = null;

        SliderManager slider = SliderManager.Instance;
        if (slider != null)
        {
            if (slider.roomNameText != null && savedRoomLabel != null)
                slider.roomNameText.text = savedRoomLabel;
            slider.GoToRoom(ReturnRoomName);
        }
    }

    private static void DisableDuplicates(Scene walkScene)
    {
        if (!walkScene.IsValid())
            return;

        foreach (GameObject root in walkScene.GetRootGameObjects())
        {
            foreach (UnityEngine.EventSystems.EventSystem es in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
                es.gameObject.SetActive(false);
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;
        }
    }

    // ═══════════════════════════════════════════════════
    //  2스테이지 코드와 팀 시스템 연결
    // ═══════════════════════════════════════════════════

    private void HookStage2()
    {
        // 퀵슬롯 칸에 '끌어다 놓기 → 산책 기믹' 연결을 붙입니다 (전화 부스 아이템 사용)
        WalkQuickSlotDropBridge.AttachToAllSlots();

        WalkSession.Additive = true;
        WalkSession.ReturnSceneName = gameObject.scene.name;
        WalkSession.LastResult = WalkResult.None;
        WalkSession.Finished -= OnWalkFinished;
        WalkSession.Finished += OnWalkFinished;

        // 산책 대사 → 1스테이지 DialogueManager 대화창
        WalkDialogue.ExternalShow = ShowWithTeamDialogue;
        WalkDialogue.ExternalIsOpen = () => DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen;

        // 산책에서 주운 아이템 → 팀 퀵슬롯
        WalkItemPickup.Giver = item =>
        {
            QuickSlotInventory inventory = QuickSlotInventory.Instance != null
                ? QuickSlotInventory.Instance
                : FindFirstObjectByType<QuickSlotInventory>();
            return inventory != null && inventory.TryAdd(item);
        };

        // 전화 부스 기믹: 퀵슬롯 아이템은 끌어다 놓아서 쓴다 (WalkQuickSlotDropBridge → WalkGameManager.HandleItemDrop)
        WalkItems.SelectedProvider = () => WalkGameManager.DroppedItem;
        WalkItems.HasProvider = item => FindQuickSlotEntry(item) != null;
        WalkItems.RemoveProvider = item =>
        {
            QuickSlotEntry entry = FindQuickSlotEntry(item);
            return entry != null && QuickSlotInventory.Instance != null && QuickSlotInventory.Instance.TryRemove(entry);
        };

        // 산책 길의 아이템(종이 A~E)이 다시 나오는지:
        //  - 퀵슬롯에 이미 있으면 안 나옴
        //  - 화장실 금고를 열어 종이가 영구히 없어졌으면 안 나옴
        //  - 그 외(아직 안 주웠거나, 일차 초기화로 퀵슬롯에서 빠짐)에는 다시 나옴 → 일차 초기화 때 '얻는 기회'도 초기화됨
        WalkItemPickup.IsUnavailable = item =>
        {
            if (item == null)
                return false;

            ItemPositionManager positions = ItemPositionManager.Instance;
            if (positions != null && positions.ArePapersRemovedPermanently && positions.IsPaperItem(item.ItemId))
                return true;

            // 종이가 아닌 산책 아이템(전화 부스 부품 등)은 한 번 얻으면 다시 안 나옴
            // (통화 중에 들키면 PhoneBooth가 WalkItemPickup.Forget으로 테이프를 다시 나오게 함)
            if (positions == null || !positions.IsPaperItem(item.ItemId))
            {
                if (WalkItemPickup.HasPicked(item))
                    return true;
            }

            QuickSlotInventory inventory = QuickSlotInventory.Instance != null
                ? QuickSlotInventory.Instance
                : FindFirstObjectByType<QuickSlotInventory>();
            return inventory != null && inventory.Entries.Any(entry =>
                entry != null && entry.ItemData != null && entry.ItemData.ItemId == item.ItemId);
        };
    }

    private void UnhookStage2()
    {
        WalkSession.Finished -= OnWalkFinished;
        WalkSession.Additive = false;
        WalkItemPickup.Giver = null;
        WalkItemPickup.IsUnavailable = null;
        WalkItems.SelectedProvider = null;
        WalkItems.HasProvider = null;
        WalkItems.RemoveProvider = null;
    }

    private static QuickSlotEntry FindQuickSlotEntry(ItemData item)
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance != null
            ? QuickSlotInventory.Instance
            : FindFirstObjectByType<QuickSlotInventory>();
        if (inventory == null || item == null)
            return null;

        return inventory.Entries.FirstOrDefault(entry => entry != null && WalkItems.Same(entry.ItemData, item));
    }

    private static bool ShowWithTeamDialogue(IReadOnlyList<WalkLine> walkLines)
    {
        DialogueManager dialogue = DialogueManager.Instance;
        if (dialogue == null)
            return false;

        List<DialogueLine> lines = new();
        foreach (WalkLine line in walkLines)
        {
            bool narration = string.IsNullOrEmpty(line.speaker);
            lines.Add(new DialogueLine
            {
                speakerName = narration ? string.Empty : line.speaker,
                text = line.text,
                dialogueType = narration ? DialogueType.Narration : DialogueType.Character,
            });
        }

        dialogue.StartDialogue(lines);
        return true;
    }
}
