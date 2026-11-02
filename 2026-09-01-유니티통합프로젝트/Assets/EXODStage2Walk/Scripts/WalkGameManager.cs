using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책(외출) 기믹 진행 관리. 산책 씬에 하나 둔다.
    ///  - 시작하면 화면이 밝아지고 그녀의 대사가 나온 뒤 산책이 시작된다 (1스테이지와 같은 대화창).
    ///  - 1분 안에 화면 좌우를 1번 왕복하면 성공 (roundTripsRequired) → 대사 후 집으로 돌아가고 일차는 계속 진행
    ///  - 그녀가 뒤돌아보고 있을 때 화면의 물체와 상호작용하면 실패
    ///    → 스프라이트 3, 그날 시간 1분 차감, 다음 날까지 현관문 사용 불가, 대사 후 집으로 돌아감
    ///  - 산책하는 동안에도 그날의 시간은 계속 흐른다 (대화창이 떠 있는 동안 산책 타이머와 그녀는 멈춘다)
    ///  - [통합] 2스테이지 1분 타이머는 DayClock이 관리한다. 다 되면 timeUpLines("돌아가자") 후 1스테이지로 돌아간다.
    ///    멈추는 기준은 1스테이지 DayCycleController와 같다 (대화·컷신·기절 연출 중).
    /// </summary>
    public class WalkGameManager : MonoBehaviour
    {
        public static WalkGameManager Current { get; private set; }

        const string Her = "여자친구";

        [Header("참조")]
        [SerializeField] WalkPlayerMover player;
        [SerializeField] WalkYandere yandere;
        [SerializeField] Camera worldCamera;

        [Header("규칙")]
        [SerializeField] float walkDurationSeconds = 60f;
        [SerializeField] int roundTripsRequired = 1;
        [Tooltip("들켰을 때 그날 시간에서 빼는 초")]
        [SerializeField] float caughtPenaltySeconds = 60f;
        [Tooltip("들키지 않고 끝나도 그날은 다시 못 나가게 할지 (기획서: 하루 한 번 산책)")]
        [SerializeField] bool lockDoorAfterSuccess = true;
        [Tooltip("Esc로 산책을 중간에 그만둘 수 있는지")]
        [SerializeField] bool allowQuitEarly = true;
        [Tooltip("켜면 물체가 아닌 빈 곳을 클릭해도 상호작용으로 판정 (그녀가 뒤돌아봤을 때 화면 아무 데나 클릭하면 실패)")]
        [SerializeField] bool anyClickCountsAsInteraction = true;

        [Header("귀가")]
        [Tooltip("현관문을 거치지 않고 이 씬을 바로 실행했을 때 돌아갈 씬")]
        [SerializeField] string fallbackHomeScene = "SampleScene";
        [Tooltip("끝난 뒤 대사가 나오기 전까지 기다리는 시간")]
        [SerializeField] float returnDelayAfterSuccess = 0.5f;
        [Tooltip("들킨 뒤(스프라이트 3) 대사가 나오기 전까지 기다리는 시간")]
        [SerializeField] float returnDelayAfterCaught = 1.2f;

        [Header("화면")]
        [Tooltip("오른쪽 위 산책 시간·왕복 표시와 물체 이름 표시")]
        [SerializeField] bool showDebugHud = true;

        [Header("대사 (speaker를 비우면 이름표 없는 나레이션)")]
        [SerializeField] WalkLine[] firstIntroLines =
        {
            new WalkLine(Her, "오늘부터 하루에 한 번은 나랑 같이 산책하는 거야."),
            new WalkLine(Her, "집 대문에서 저 길 끝까지, 한 번만 갔다 오고 들어가자."),
            new WalkLine("", "(길은 저절로 걷는다. 그녀가 뒤돌아봤을 때 무언가를 건드리면 안 된다.)")
        };
        [SerializeField] WalkLine[] introLines = { new WalkLine(Her, "오늘도 같이 걷자.") };
        [SerializeField] WalkLine[] completedLines = { new WalkLine(Her, "오늘 산책 즐거웠어. 이제 들어가자.") };
        [SerializeField] WalkLine[] timeUpLines =
        {
            new WalkLine(Her, "어머, 벌써 시간이 이렇게 됐네. 이제 집에 가자."),
            new WalkLine("", "…다시 그 집으로 돌아간다.")
        };
        [SerializeField] WalkLine[] quitLines =
        {
            new WalkLine(Her, "뭐야? 벌써 가게? …나랑 걷는 게 싫은 거야?"),
            new WalkLine(Her, "농담이야~ 들어가자 ♡")
        };
        [SerializeField] WalkLine[] caughtLines =
        {
            new WalkLine(Her, "…지금 뭐 하는 거야?"),
            new WalkLine(Her, "들어가자. 오늘은 이제 밖에 안 나갈 거야.")
        };
        [SerializeField] WalkLine[] dayEndedLines = { new WalkLine(Her, "늦었다. 이만 들어가자.") };

        [Header("이벤트")]
        public UnityEvent onWalkStarted = new UnityEvent();
        public UnityEvent onWalkSucceeded = new UnityEvent();
        public UnityEvent onCaught = new UnityEvent();

        public bool IsRunning { get; private set; }
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0f, walkDurationSeconds - Elapsed);
        public WalkResult Result { get; private set; } = WalkResult.None;
        public int RoundTripsDone => player != null ? player.LegsCompleted / 2 : 0;
        public int RoundTripsRequired => roundTripsRequired;
        public WalkPlayerMover Player => player;

        public bool AnyClickCountsAsInteraction => anyClickCountsAsInteraction;

        /// <summary>
        /// 산책 화면 위에 덮이는 창(전화 부스 안 등). 열려 있는 동안 화면 이동이 멈추고,
        /// 클릭과 Esc를 이 창이 먼저 처리한다. 없으면 null.
        /// </summary>
        public static IWalkOverlay Overlay { get; set; }

        /// <summary>
        /// 산책이 끝나 마지막 대사가 나오기 직전에 호출된다 (인자: 결과, 나올 대사 목록).
        /// 목록을 고치면 그대로 나온다 (전화 부스 기믹이 "전화가 끊겼다" 같은 줄을 끼워 넣을 때 사용).
        /// </summary>
        public static event Action<WalkResult, List<WalkLine>> FinishingLines;

        static bool firstIntroShown;
        DayClock clock;
        bool finishing;
        // [통합] 왕복을 일찍 마치면 대문 앞에서 2스테이지 1분이 끝날 때까지 기다린다
        bool waitingAtGate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            firstIntroShown = false;
            Overlay = null;
            FinishingLines = null;
        }

        void Awake()
        {
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            DayClock.DayAdvanced -= HandleDayAdvanced;
            DayClock.TimeExpired -= HandleTimeExpired;
        }

        IEnumerator Start()
        {
            clock = DayClock.Ensure();
            DayClock.DayAdvanced += HandleDayAdvanced;
            DayClock.TimeExpired += HandleTimeExpired;

            if (player == null || yandere == null)
            {
                Debug.LogError("[Walk] WalkGameManager에 player / yandere 참조가 비어 있습니다.");
                yield break;
            }

            player.ResetToStart();
            player.CanMove = false;
            if (showDebugHud) WalkHud.Create(this, worldCamera);

            yield return WalkFade.FadeIn();

            WalkLine[] intro = !firstIntroShown && firstIntroLines != null && firstIntroLines.Length > 0 ? firstIntroLines : introLines;
            firstIntroShown = true;
            yield return WalkDialogue.ShowAndWait(intro);

            BeginWalk();
        }

        public void BeginWalk()
        {
            Result = WalkResult.None;
            Elapsed = 0f;
            finishing = false;
            IsRunning = true;
            waitingAtGate = false;
            player.ResetToStart();
            player.CanMove = true;
            yandere.Begin();
            onWalkStarted.Invoke();
        }

        void Update()
        {
            if (!IsRunning) return;

            // 대화창·컷신·기절 연출 중에는 멈춤 (1스테이지 DayCycleController와 같은 기준)
            bool paused = DayClock.IsTimePaused();
            player.CanMove = !paused && !waitingAtGate && Overlay == null;   // 부스 안 등 창이 열려 있으면 화면이 멈춘다
            // 전화 부스 안(창)이 열려 있으면 그녀도 멈추고 산책 시간도 흐르지 않음 (사용자 요청 2026-11-02)
            yandere.Paused = paused || Overlay != null;
            if (paused) return;

            if (Overlay == null)
                Elapsed += Time.deltaTime;

            if (WalkInput.ClickDown(out var screenPos) && !WalkInput.IsPointerOverUI())
                HandleClick(screenPos);
            if (!IsRunning) return;

            if (allowQuitEarly && WalkInput.QuitWalkDown())
            {
                // 창이 열려 있으면 Esc는 창을 닫는 데 먼저 쓴다
                if (Overlay == null || !Overlay.HandleEscape()) { EndWalk(WalkResult.QuitEarly); return; }
            }
            // 왕복을 마치면 바로 끝내지 않고, 대문 앞에 멈춰 서서 1분이 다 될 때까지 기다린다 (그 동안에도 그녀는 뒤돌아봄)
            if (!waitingAtGate && player.LegsCompleted >= roundTripsRequired * 2)
            {
                waitingAtGate = true;
                player.CanMove = false;
            }
            // 시간 초과는 DayClock(2스테이지 1분 타이머)이 알려 준다 → HandleTimeExpired
        }

        void HandleTimeExpired()
        {
            if (!IsRunning) return;
            EndWalk(waitingAtGate ? WalkResult.Completed : WalkResult.TimeUp);
        }

        /// <summary>
        /// [통합] 퀵슬롯에서 끌어다 놓은 아이템. 놓은 순간에만 값이 있고, 그동안 WalkItems.Selected로 읽힌다.
        /// </summary>
        public static ItemData DroppedItem { get; private set; }

        /// <summary>
        /// [통합] 퀵슬롯 아이템을 산책 화면(부스·부스 안 등)에 끌어다 놓았을 때.
        /// 그 자리를 "이 아이템을 들고 클릭"한 것으로 처리한다 (그녀가 뒤돌아봤으면 들킴 규칙 그대로).
        /// </summary>
        public bool HandleItemDrop(ItemData item, Vector2 screenPos)
        {
            if (!IsRunning || item == null || DayClock.IsTimePaused()) return false;

            DroppedItem = item;
            try { HandleClick(screenPos); }
            finally { DroppedItem = null; }
            return true;
        }

        void HandleClick(Vector2 screenPos)
        {
            if (Overlay != null && Overlay.HandleClick(screenPos)) return;
            var target = WalkInput.FindAtScreenPoint<WalkInteractable>(screenPos, worldCamera);
            if (target != null) TryInteract(target);
            else if (anyClickCountsAsInteraction) TryInteract(null);
        }

        /// <summary>
        /// 다른 코드(팀 아이템 줍기 등)에서 산책 중 상호작용을 알릴 때 호출.
        /// 상호작용해도 되면 true, 들켰으면 false(산책 종료)를 돌려준다. 산책 중이 아니면 항상 true.
        /// </summary>
        public static bool ReportInteraction()
        {
            var m = Current;
            if (m == null || !m.IsRunning) return true;
            return m.TryInteract(null);
        }

        public bool TryInteract(WalkInteractable target)
        {
            if (!IsRunning) return false;

            if (yandere.IsWatching)
            {
                if (target != null) target.PerformCaught();
                HandleCaught();
                return false;
            }

            if (target != null) target.Perform();
            return true;
        }

        void HandleCaught()
        {
            IsRunning = false;
            Result = WalkResult.Caught;
            player.CanMove = false;
            yandere.Paused = false;
            yandere.SetCaught();

            // 잠금을 먼저 건다. 1스테이지 시간 차감(1분)은 성공/실패 상관없이
            // 복귀할 때 Stage2HouseLink에서 한 번만 합니다.
            clock.LockFrontDoorForToday();

            onCaught.Invoke();
            StartCoroutine(Finish(caughtLines, returnDelayAfterCaught));
        }

        void EndWalk(WalkResult result)
        {
            IsRunning = false;
            Result = result;
            player.CanMove = false;
            yandere.Stop();

            if (lockDoorAfterSuccess) clock.LockFrontDoorForToday();
            onWalkSucceeded.Invoke();

            WalkLine[] lines = result == WalkResult.Completed ? completedLines
                             : result == WalkResult.TimeUp ? timeUpLines
                             : quitLines;
            StartCoroutine(Finish(lines, returnDelayAfterSuccess));
        }

        void HandleDayAdvanced(int day)
        {
            if (!IsRunning) return;
            IsRunning = false;
            Result = WalkResult.DayEnded;
            player.CanMove = false;
            yandere.Stop();
            StartCoroutine(Finish(dayEndedLines, returnDelayAfterSuccess));
        }

        IEnumerator Finish(WalkLine[] lines, float delay)
        {
            if (finishing) yield break;
            finishing = true;

            WalkSession.LastResult = Result;
            Debug.Log($"[Walk] 산책 종료: {Result}");

            if (FinishingLines != null)
            {
                var list = new List<WalkLine>();
                if (lines != null) list.AddRange(lines);
                FinishingLines.Invoke(Result, list);
                lines = list.ToArray();
            }

            yield return new WaitForSeconds(delay);
            yield return WalkDialogue.ShowAndWait(lines);
            yield return WalkFade.FadeOut();

            // 1스테이지 집 씬 위에 열린 경우: 집 씬은 그대로 두고 끝났다고만 알린다 (Stage2HouseLink가 닫아 줌)
            if (WalkSession.Additive)
            {
                WalkSession.RaiseFinished(Result);
                yield break;
            }

            string scene = string.IsNullOrEmpty(WalkSession.ReturnSceneName) ? fallbackHomeScene : WalkSession.ReturnSceneName;
            if (Application.CanStreamedLevelBeLoaded(scene))
            {
                SceneManager.LoadScene(scene);
            }
            else
            {
                Debug.LogWarning($"[Walk] 돌아갈 씬 '{scene}'을(를) 불러올 수 없습니다. Build Profiles의 씬 목록에 추가했는지 확인하세요.");
                yield return WalkFade.FadeIn();
            }
        }

        /// <summary>집에 돌아온 뒤 따로 알려 줄 내용 (없으면 빈 문자열)</summary>
        public static string ResultLabel(WalkResult r)
        {
            switch (r)
            {
                case WalkResult.Caught: return "들켰다… 오늘 남은 시간이 1분 줄었고, 내일까지 현관문을 열 수 없다.";
                case WalkResult.DayEnded: return "산책하는 사이에 하루가 지나 버렸다.";
                default: return "";
            }
        }
    }
}
