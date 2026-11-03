using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 잠수·시간 멈춤 악용 방지 (사용자 요청 2026-11-03)
/// 1) 기믹 창(키패드·서랍 비밀번호·화장실 금고·거울 미로)을 연 채로 10초 동안 아무 입력이 없으면 창을 닫고 실패 처리
/// 2) 기믹을 풀지 않고 그냥 닫아도 실패 처리 (위험도: 기믹 실패와 같음, DangerLevel.GimmickFailed)
///    - 창이 열려 있는 동안 이미 성공/실패 판정이 났거나, 이미 풀린 기믹이면 따로 벌하지 않음
/// 3) 조사 대사(이야기 컷신이 아닌 대사)를 5초 동안 넘기지 않으면 자동으로 다음 줄로 넘기고 위험도 +5 (DangerLevel.IdleAutoAdvanced)
/// 물약 기믹은 실패가 곧 엔딩이라 여기서 다루지 않음.
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
public sealed class Stage1AntiIdle : MonoBehaviour
{
    private const float GimmickIdleSeconds = 10f;
    private const float DialogueIdleSeconds = 5f;

    private enum Gimmick { None, Keypad, Drawer, Safe, Mirror }

    private Gimmick openGimmick;
    private int solvedAtOpen;
    private int failedAtOpen;
    private bool closedByIdle;
    private float lastInputTime;
    private Vector3 lastMouse;
    private PasswordDrawer drawer;

    private DialogueLine lastLine;
    private float lineShownTime;
    private int shownCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;
        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null && slider.GetComponent<Stage1AntiIdle>() == null)
            slider.gameObject.AddComponent<Stage1AntiIdle>();
    }

    private void OnEnable()
    {
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.LineShown += OnLineShown;
        lastInputTime = Time.unscaledTime;
    }

    private void Start()
    {
        // DialogueManager가 늦게 준비되는 경우
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.LineShown -= OnLineShown;
            DialogueManager.Instance.LineShown += OnLineShown;
        }
    }

    private void OnDisable()
    {
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.LineShown -= OnLineShown;
    }

    private void OnLineShown(DialogueLine line)
    {
        lastLine = line;
        lineShownTime = Time.unscaledTime;
        shownCount++;
    }

    private void Update()
    {
        bool input = Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)
            || (Input.mousePosition - lastMouse).sqrMagnitude > 4f;
        lastMouse = Input.mousePosition;
        // 기믹 창: 마우스를 움직이거나 누르면 입력으로 셈
        // 대사: 실제로 다음 줄로 넘겨야만 셈 (줄이 바뀔 때 OnLineShown이 시간을 새로 잼)
        if (input)
            lastInputTime = Time.unscaledTime;

        UpdateGimmick();
        UpdateDialogue();
    }

    // ─── 기믹 창 ─────────────────────────────────────────

    private Gimmick CurrentGimmick()
    {
        if (MirrorMazeGimmick.IsOpen) return Gimmick.Mirror;
        if (Stage1Keypad.IsOpen) return Gimmick.Keypad;
        if (RestroomGimmick.Instance != null && RestroomGimmick.Instance.IsKeypadOpen) return Gimmick.Safe;
        if (drawer == null) drawer = FindFirstObjectByType<PasswordDrawer>(FindObjectsInactive.Include);
        if (drawer != null && drawer.IsPanelOpen) return Gimmick.Drawer;
        return Gimmick.None;
    }

    private void UpdateGimmick()
    {
        Gimmick now = CurrentGimmick();

        if (now != Gimmick.None && openGimmick == Gimmick.None)
        {
            // 열림
            openGimmick = now;
            solvedAtOpen = DangerLevel.SolvedCount;
            failedAtOpen = DangerLevel.FailedCount;
            lastInputTime = Time.unscaledTime;
            closedByIdle = false;
            return;
        }

        if (now == Gimmick.None && openGimmick != Gimmick.None)
        {
            // 닫힘: 판정 없이 닫았으면 실패
            Gimmick closed = openGimmick;
            openGimmick = Gimmick.None;
            bool judged = DangerLevel.SolvedCount != solvedAtOpen || DangerLevel.FailedCount != failedAtOpen;
            if (!judged && !IsAlreadySolved(closed) && !BlockedBySomethingElse())
            {
                DangerLevel.GimmickFailed();
                Debug.Log("[AntiIdle] 기믹을 풀지 않고 닫음 → 실패 (" + closed + (closedByIdle ? ", 10초 무입력" : "") + ")");
            }
            if (closedByIdle)
                Stage1Room.PlayCutscene(Stage1Dialogues.GimmickIdleClosed());
            closedByIdle = false;
            return;
        }

        if (now == Gimmick.None)
            return;

        // 다른 대사·컷신이 떠 있는 동안은 세지 않음 (대사 넘기기는 입력으로 셈)
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
        {
            lastInputTime = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - lastInputTime >= GimmickIdleSeconds)
        {
            closedByIdle = true;
            CloseGimmick(now);
        }
    }

    private static bool IsAlreadySolved(Gimmick g)
    {
        switch (g)
        {
            case Gimmick.Mirror: return MirrorMazeGimmick.IsSolved;
            case Gimmick.Safe: return RestroomGimmick.Instance != null && RestroomGimmick.Instance.IsSafeUnlocked;
            case Gimmick.Drawer:
                PasswordDrawer d = FindFirstObjectByType<PasswordDrawer>(FindObjectsInactive.Include);
                return d != null && d.IsUnlocked;
            default: return false;
        }
    }

    // 여자친구 등장·기절·엔딩 등으로 창이 강제로 닫힌 경우는 벌하지 않음
    private static bool BlockedBySomethingElse()
    {
        return DangerLevel.IsAppearing
            || GimmickInterrupt.JustClosed   // 여자친구 등장·일차 끝 안내 때문에 닫힘
            || (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy)
            || DayController.IsTransitioning;
    }

    private void CloseGimmick(Gimmick g)
    {
        switch (g)
        {
            case Gimmick.Mirror:
                if (MirrorMazeGimmick.Current != null) MirrorMazeGimmick.Current.Close();
                break;
            case Gimmick.Keypad:
                Stage1Keypad.CloseActive();
                break;
            case Gimmick.Safe:
                RestroomGimmick.Instance.CloseKeypad();
                break;
            case Gimmick.Drawer:
                if (drawer != null) drawer.Close();
                break;
        }
    }

    // ─── 조사 대사 자동 넘김 ─────────────────────────────

    private void UpdateDialogue()
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm == null || !dm.IsDialogueOpen || dm.IsChoosing || lastLine == null)
            return;

        // 이야기 컷신·일차 넘김·기절·엔딩·여자친구 등장 중에는 자동으로 넘기지 않음 (조사 대사만)
        if ((Stage1StoryController.Instance != null && Stage1StoryController.Instance.IsPlaying)
            || DayController.IsTransitioning || DangerLevel.IsAppearing || Stage2HouseLink.IsInStage2
            || (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy))
        {
            lineShownTime = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - lineShownTime < DialogueIdleSeconds)
            return;

        lineShownTime = Time.unscaledTime;
        int before = shownCount;
        dm.AdvanceDialogue();
        // 글자가 아직 찍히는 중이었으면 첫 번째는 문장 완성만 함 → 한 번 더 넘김
        if (dm.IsDialogueOpen && shownCount == before)
            dm.AdvanceDialogue();
        DangerLevel.IdleAutoAdvanced();
        Debug.Log("[AntiIdle] 대사를 5초 동안 안 넘김 → 자동 넘김, 위험도 +5");
    }
}
