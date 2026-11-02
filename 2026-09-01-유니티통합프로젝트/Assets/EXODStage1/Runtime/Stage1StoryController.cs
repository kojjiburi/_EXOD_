using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 1스테이지 이야기 진행을 맡습니다.
/// - 침실 씬(SliderManager가 있는 씬)이 열리면 인트로를 재생합니다.
/// - 거실에 처음 들어가면 거실 대사를 재생합니다.
/// - 이야기 대사(컷신)가 나오는 동안 방 조사 영역과 장소 이동 버튼을 잠급니다.
/// </summary>
[DefaultExecutionOrder(220)]
public sealed class Stage1StoryController : MonoBehaviour
{
    public static Stage1StoryController Instance { get; private set; }

    private readonly List<Image> lockedAreas = new List<Image>();
    private readonly List<Button> lockedButtons = new List<Button>();
    private Action onCutsceneFinished;
    private bool isPlaying;
    private bool hasVisitedLivingRoom;

    /// <summary>인트로나 컷신이 재생 중인지 여부</summary>
    public bool IsPlaying => isPlaying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 시작 화면을 거쳐 들어와도 동작하도록 씬이 열릴 때마다 확인합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider == null)
            return;

        if (FindFirstObjectByType<Stage1StoryController>() == null)
            slider.gameObject.AddComponent<Stage1StoryController>();

        if (FindFirstObjectByType<PlayerFaintController>() == null)
            slider.gameObject.AddComponent<PlayerFaintController>();

        if (FindFirstObjectByType<DialoguePortrait>() == null)
            slider.gameObject.AddComponent<DialoguePortrait>();

        if (FindFirstObjectByType<Stage1FridgeGimmick>() == null)
            slider.gameObject.AddComponent<Stage1FridgeGimmick>();

        if (FindFirstObjectByType<Stage1FrontDoorGimmick>() == null)
            slider.gameObject.AddComponent<Stage1FrontDoorGimmick>();

        if (FindFirstObjectByType<DialogueLog>() == null)
            slider.gameObject.AddComponent<DialogueLog>();

        if (FindFirstObjectByType<MemoZoom>() == null)
            slider.gameObject.AddComponent<MemoZoom>();

        if (FindFirstObjectByType<Stage1HelpBar>() == null)
            slider.gameObject.AddComponent<Stage1HelpBar>();

        if (FindFirstObjectByType<DialogueDimmer>() == null)
            slider.gameObject.AddComponent<DialogueDimmer>();

        if (FindFirstObjectByType<Stage1SoundEffects>() == null)
            slider.gameObject.AddComponent<Stage1SoundEffects>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (SliderManager.Instance != null)
            SliderManager.Instance.RoomChanged += OnRoomChanged;

        StartCoroutine(PlayIntro());
    }

    private void OnDestroy()
    {
        if (SliderManager.Instance != null)
            SliderManager.Instance.RoomChanged -= OnRoomChanged;

        if (DialogueManager.Instance != null)
            DialogueManager.Instance.onDialogueEnd.RemoveListener(OnCutsceneEnd);

        if (Instance == this)
            Instance = null;
    }

    // ═══════════════════════════════════════════════════
    //  컷신
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 이야기 대사를 재생합니다. 끝날 때까지 방을 잠그고, 끝나면 onFinished를 호출합니다.
    /// </summary>
    public void PlayCutscene(List<DialogueLine> lines, Action onFinished = null)
    {
        if (DialogueManager.Instance == null || lines == null || lines.Count == 0)
        {
            onFinished?.Invoke();
            return;
        }

        BeginCutscene(onFinished);
        DialogueManager.Instance.StartDialogue(lines);
    }

    /// <summary>
    /// 선택지가 있는 이야기 대사를 재생합니다.
    /// 선택한 뒤에도 방은 잠긴 채로 유지되므로, onChosen 안에서 PlayCutscene으로 이어 주세요.
    /// </summary>
    public void PlayChoiceCutscene(List<DialogueLine> lines, string[] choices, Action<int> onChosen)
    {
        if (DialogueManager.Instance == null)
            return;

        isPlaying = true;
        LockRoom();
        DialogueManager.Instance.StartChoiceDialogue(lines, choices, onChosen);
    }

    private void BeginCutscene(Action onFinished)
    {
        // 이전 컷신이 끝나기 전에 새 컷신이 시작되면 이전 리스너를 정리합니다.
        DialogueManager.Instance.onDialogueEnd.RemoveListener(OnCutsceneEnd);
        onCutsceneFinished = onFinished;

        isPlaying = true;
        LockRoom();
        DialogueManager.Instance.onDialogueEnd.AddListener(OnCutsceneEnd);
    }

    private void OnCutsceneEnd()
    {
        DialogueManager.Instance.onDialogueEnd.RemoveListener(OnCutsceneEnd);
        UnlockRoom();
        isPlaying = false;

        Action callback = onCutsceneFinished;
        onCutsceneFinished = null;
        callback?.Invoke();
    }

    // ═══════════════════════════════════════════════════
    //  이야기 장면
    // ═══════════════════════════════════════════════════

    private IEnumerator PlayIntro()
    {
        // DialogueManager.Start()가 대화창을 닫은 뒤에 시작해야 하므로 한 프레임 기다립니다.
        yield return null;

        if (DialogueManager.Instance == null)
        {
            Debug.LogError("[Stage1Story] DialogueManager를 찾을 수 없어 인트로를 건너뜁니다.", this);
            yield break;
        }

        PlayChoiceCutscene(
            Stage1Dialogues.IntroOpening(),
            Stage1Dialogues.IntroChoices,
            choice => PlayCutscene(Stage1Dialogues.IntroReply(choice)));
    }

    private void OnRoomChanged(string roomName)
    {
        if (roomName == "거실" && !hasVisitedLivingRoom)
        {
            hasVisitedLivingRoom = true;
            PlayLivingRoomArrival();
        }
    }

    // 거실 도착 → 목이 말라 테이블 위의 이상한 물약을 마심 → 머릿속 연타 기믹 (선택지 없음, 사용자 결정 2026-11-02)
    private void PlayLivingRoomArrival()
    {
        List<DialogueLine> lines = Stage1Dialogues.LivingRoomArrival();
        lines.AddRange(Stage1Dialogues.PotionDrink());
        PlayCutscene(lines, StartPotionGimmick);
    }

    private void StartPotionGimmick()
    {
        StrangePotionGimmick.GetOrCreate().Begin(OnPotionGimmickFinished);
    }

    private void OnPotionGimmickFinished(bool isCleared)
    {
        if (isCleared)
        {
            PlayCutscene(Stage1Dialogues.PotionSuccess());
            return;
        }

        // 실패: "그냥 이렇게 살자" → 하트 -1 → (하트가 남아 있으면) 다시 도전
        // 하트가 0이 되면 PlayerFaintController가 게임 오버를 띄웁니다.
        PlayCutscene(Stage1Dialogues.PotionFail(), () =>
        {
            HeartManager hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
            if (hearts != null)
            {
                EXODSound.Play("hp_lost");
                hearts.HPM();
                if (hearts.HP <= 0)
                    return;
            }

            PlayCutscene(Stage1Dialogues.PotionRetry(), StartPotionGimmick);
        });
    }

    // ═══════════════════════════════════════════════════
    //  방 잠금
    // ═══════════════════════════════════════════════════

    private void LockRoom()
    {
        // 이미 잠겨 있으면 다시 잠그지 않습니다. (컷신이 이어질 때)
        if (lockedAreas.Count > 0 || lockedButtons.Count > 0)
            return;

        // 방 조사 영역: 클릭을 받지 않게 합니다.
        InspectableItem[] items = FindObjectsByType<InspectableItem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (InspectableItem item in items)
        {
            Image area = item.GetComponent<Image>();
            if (area != null && area.raycastTarget)
            {
                area.raycastTarget = false;
                lockedAreas.Add(area);
            }
        }

        // 장소 이동 좌우 버튼
        SliderManager slider = SliderManager.Instance;
        if (slider != null)
        {
            LockButton(slider.leftButton);
            LockButton(slider.rightButton);
        }
    }

    private void LockButton(Button button)
    {
        if (button == null || !button.interactable)
            return;

        button.interactable = false;
        lockedButtons.Add(button);
    }

    private void UnlockRoom()
    {
        foreach (Image area in lockedAreas)
        {
            if (area != null)
                area.raycastTarget = true;
        }

        foreach (Button button in lockedButtons)
        {
            if (button != null)
                button.interactable = true;
        }

        lockedAreas.Clear();
        lockedButtons.Clear();
    }
}
