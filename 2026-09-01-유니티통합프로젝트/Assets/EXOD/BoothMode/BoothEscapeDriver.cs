using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EXOD.Walk;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 부스 운영 모드 '운명의 갈림길' → [주소지를 고른다] (사용자 요청 2026-11-03)
/// 실제 산책 시작(여자친구는 뒤돌지 않음) → 전화 부스가 화면 가운데에 오면 부스 창 열림 → 다이얼 1·1·9가 저절로 눌림
/// → 원래 게임의 119 통화(통화 로그 포함), 주소지는 저절로 넘어감 → 집으로 → 탈출 엔딩.
/// 진행 중엔 화면 클릭·터치를 막고 대사 넘기기만 됨.
/// </summary>
public sealed class BoothEscapeDriver : MonoBehaviour
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static bool Running { get; private set; }

    private readonly List<BaseRaycaster> disabledRaycasters = new List<BaseRaycaster>();

    public static void Run()
    {
        GameObject go = new GameObject("[부스 모드] 탈출 엔딩 진행");
        DontDestroyOnLoad(go);
        go.AddComponent<BoothEscapeDriver>();
    }

    private void Start()
    {
        Running = true;
        WalkInput.BlockAll = true;
        StartCoroutine(Routine());
    }

    private void OnDestroy()
    {
        Running = false;
        WalkInput.BlockAll = false;
        foreach (BaseRaycaster r in disabledRaycasters)
            if (r != null)
                r.enabled = true;
        disabledRaycasters.Clear();
    }

    private void Update()
    {
        HoldHerFacingAway();
        BlockUiClicks();
        if (DayClock.Instance != null)
            DayClock.Instance.Paused = true;   // 산책 1분 타이머로 끝나지 않게
    }

    private void LateUpdate()
    {
        HoldHerFacingAway();
    }

    // 여자친구는 계속 앞을 봄 (뒤돌기 시계를 계속 멀리 미룸)
    private static void HoldHerFacingAway()
    {
        foreach (WalkYandere y in FindObjectsByType<WalkYandere>(FindObjectsSortMode.None))
        {
            if (y.State == YandereState.Watching)
                y.CalmDown();
            FieldInfo timer = typeof(WalkYandere).GetField("timer", Any);
            if (timer != null && y.State == YandereState.Away)
                timer.SetValue(y, 999f);
        }
    }

    // 대화창이 있는 캔버스만 남기고 UI 클릭을 막음 (대사 넘기기만 가능)
    // (nicezzomaru 2026-11-04 수정) 대화창 [다음] 버튼은 대화창 안쪽의 중첩 캔버스(자체 Raycaster)에 있어서,
    // 맨 위 캔버스만 남기면 [다음]이 눌리지 않아 산책 첫 대사에서 멈췄음 → 대화창이 들어 있는 캔버스와 그 안의 캔버스는 모두 남김
    private void BlockUiClicks()
    {
        Transform panel = DialogueManager.Instance != null && DialogueManager.Instance.DialoguePanelRect != null
            ? DialogueManager.Instance.DialoguePanelRect
            : null;

        // 예전에 막았는데 지금은 대화창 쪽이면 다시 켬 (대화창이 다른 캔버스로 옮겨졌을 때)
        for (int i = disabledRaycasters.Count - 1; i >= 0; i--)
        {
            BaseRaycaster r = disabledRaycasters[i];
            if (r != null && IsDialogueRaycaster(r, panel))
            {
                r.enabled = true;
                disabledRaycasters.RemoveAt(i);
            }
        }

        foreach (BaseRaycaster r in FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None))
        {
            if (!r.enabled || IsDialogueRaycaster(r, panel))
                continue;
            r.enabled = false;
            disabledRaycasters.Add(r);
        }
    }

    // 대화창을 품고 있는 캔버스이거나, 대화창 안에 있는 캔버스
    private static bool IsDialogueRaycaster(BaseRaycaster r, Transform panel)
    {
        if (panel == null)
            return false;
        return panel.IsChildOf(r.transform) || r.transform.IsChildOf(panel);
    }

    private static bool DialogueOpen() =>
        DayClock.IsAnyDialogueOpen() || (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen);

    private static IEnumerator WaitDialogue()
    {
        yield return null;
        while (DialogueOpen())
            yield return null;
    }

    private IEnumerator Routine()
    {
        // 다른 곳에서 엔딩이 또 뜨지 않게
        PhoneBoothProgress.EndingShown = true;

        if (!Stage2HouseLink.IsInStage2)
            Stage2HouseLink.ForceEnterWalk();

        float waited = 0f;
        while (WalkGameManager.Current == null || !WalkGameManager.Current.IsRunning)
        {
            waited += Time.unscaledDeltaTime;
            if (waited > 30f)
            {
                yield return FinishAtHome();
                yield break;
            }
            yield return null;
        }

        // 전화는 다 고쳐진 상태로
        PhoneBoothProgress.BoothUnlocked = true;
        PhoneBoothProgress.CordFixed = true;
        PhoneBoothProgress.Button1Installed = true;
        PhoneBoothProgress.Button2Installed = true;

        PhoneBooth booth = PhoneBooth.Current != null ? PhoneBooth.Current : FindFirstObjectByType<PhoneBooth>();
        if (booth != null)
            booth.RefreshLook();

        // 전화 부스가 화면 가운데에 올 때까지 걷기
        waited = 0f;
        while (booth != null)
        {
            Camera cam = Camera.main;
            if (cam != null && Mathf.Abs(cam.WorldToViewportPoint(booth.transform.position).x - 0.5f) < 0.04f)
                break;
            if (!DialogueOpen())
                waited += Time.unscaledDeltaTime;
            if (waited > 45f)
                break;
            yield return null;
        }

        PhoneBoothInterior interior = booth != null ? typeof(PhoneBooth).GetField("interior", Any)?.GetValue(booth) as PhoneBoothInterior : null;
        if (interior == null)
            interior = FindFirstObjectByType<PhoneBoothInterior>(FindObjectsInactive.Include);
        if (interior == null)
        {
            yield return FinishAtHome();
            yield break;
        }

        interior.Open();
        yield return WaitDialogue();
        Call(interior, "RefreshAll");
        yield return new WaitForSecondsRealtime(0.7f);

        // 다이얼 1·1·9가 저절로 눌림
        foreach (char key in "119")
        {
            Call(interior, "OnKey", key.ToString());
            yield return new WaitForSecondsRealtime(0.6f);
        }
        yield return WaitDialogue();   // "네, 119입니다…"
        yield return new WaitForSecondsRealtime(0.4f);

        // 주소지가 저절로 넘어감 → 위치를 말함(통화 로그) → 신고 완료 대사
        Call(interior, "StartReport");
        while (typeof(PhoneBoothInterior).GetField("call", Any)?.GetValue(interior)?.ToString() != "Done")
            yield return null;
        yield return WaitDialogue();
        yield return new WaitForSecondsRealtime(0.8f);

        yield return FinishAtHome();
    }

    private IEnumerator FinishAtHome()
    {
        bool home = false;
        Stage2HouseLink.ForceLeaveWalk(() => home = true);
        while (!home)
            yield return null;

        WalkInput.BlockAll = false;
        EscapeEndingSfx.ArmHandcuffs();
        Stage1Room.PlayCutscene(Stage1Dialogues.EscapeCutscene(), () =>
        {
            if (PlayerFaintController.Instance != null)
                PlayerFaintController.Instance.ShowEnding(Stage1Dialogues.EscapeEndingLine, Stage1Dialogues.EscapeEndingTitle,
                    Stage1Dialogues.EscapeEndingSubtitle, EndingStyle.Escape);
        });
        Destroy(gameObject);
    }

    private static void Call(object target, string method, params object[] args)
    {
        MethodInfo m = target.GetType().GetMethod(method, Any);
        if (m != null)
            m.Invoke(target, args.Length == 0 ? null : args);
    }
}
