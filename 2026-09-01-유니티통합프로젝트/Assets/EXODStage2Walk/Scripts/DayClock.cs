using System;
using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 2스테이지(산책) 전용 시계.
    /// - 2스테이지에 들어오자마자 1분 타이머가 시작되고, 다 되면 TimeExpired가 호출됩니다.
    ///   (WalkGameManager가 받아서 "돌아가자" 대사를 띄운 뒤 1스테이지로 돌아갑니다)
    /// - 멈추는 기준은 1스테이지 DayCycleController와 같습니다: 대화·컷신·기절/게임 오버 연출 중.
    /// - 일차(DAY)는 올리지 않습니다. 일차는 1스테이지 DayCycleController가 관리하며, 2스테이지에 있는 동안에도
    ///   1스테이지 3분 타이머는 계속 흐릅니다. 그 사이 1스테이지에서 일차가 넘어가면 DayAdvanced가 호출됩니다.
    /// - 남은 시간은 1스테이지 3분 타이머 바로 아래에 표시됩니다(DayCycleController가 그림).
    /// - 현관문 잠금(들켰을 때 등)은 1스테이지 일차 기준으로 그날 하루 유지됩니다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DayClock : MonoBehaviour
    {
        public static DayClock Instance { get; private set; }

        /// <summary>1스테이지에서 일차가 넘어갔을 때 (인자: 새 일차)</summary>
        public static event Action<int> DayAdvanced;

        /// <summary>2스테이지 1분 타이머가 다 됐을 때</summary>
        public static event Action TimeExpired;

        [Header("2스테이지 시간 (초)")]
        [SerializeField] float dayLengthSeconds = 60f;

        public float DayLength => dayLengthSeconds;
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0f, dayLengthSeconds - Elapsed);
        public bool Paused { get; set; }
        public bool Expired { get; private set; }

        // 현관문 잠금: 잠근 날(1스테이지 일차)과 같은 날이면 잠김. 씬이 바뀌어도 유지됩니다.
        static int lockedDay = int.MinValue;
        int lastSeenDay = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            lockedDay = int.MinValue;
            DayAdvanced = null;
            TimeExpired = null;
        }

        /// <summary>오늘(1스테이지 일차 기준) 현관문을 쓸 수 없는지</summary>
        public bool FrontDoorLocked => IsFrontDoorLockedToday();

        /// <summary>[통합] 새 게임(다시 시작·타이틀로)일 때 현관문 잠금 기록을 지웁니다.</summary>
        public static void ResetForNewGame() => lockedDay = int.MinValue;

        public static bool IsFrontDoorLockedToday() => lockedDay != int.MinValue && lockedDay == CurrentStage1Day();

        public int CurrentDay => CurrentStage1Day();

        /// <summary>DayClock이 없으면 새로 만든다. (산책 씬 안에 만들어지며, 산책 씬이 닫히면 함께 사라짐)</summary>
        public static DayClock Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<DayClock>();
            if (existing != null) return existing;
            var go = new GameObject("DayClock");
            return go.AddComponent<DayClock>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Elapsed = 0f;
            Expired = false;
            lastSeenDay = CurrentStage1Day();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            int day = CurrentStage1Day();
            if (day > lastSeenDay)
            {
                lastSeenDay = day;
                DayAdvanced?.Invoke(day);
            }

            if (Expired || Paused || IsTimePaused()) return;

            Elapsed += Time.deltaTime;
            if (Elapsed >= dayLengthSeconds)
            {
                Elapsed = dayLengthSeconds;
                Expired = true;
                Debug.Log("[DayClock] 2스테이지 시간 종료");
                TimeExpired?.Invoke();
            }
        }

        /// <summary>
        /// 그날(1스테이지 일차) 시간에서 초를 뺀다. (산책에서 들켰을 때 등)
        /// 1스테이지 DayCycleController의 3분 타이머에서 빠집니다.
        /// </summary>
        public void ConsumeTime(float seconds)
        {
            if (seconds <= 0f) return;
            if (DayCycleController.Instance != null)
                DayCycleController.Instance.ConsumeTime(seconds);
        }

        /// <summary>오늘(1스테이지 일차) 하루 현관문 잠금</summary>
        public void LockFrontDoorForToday() => lockedDay = CurrentStage1Day();

        // 1스테이지 DayCycleController와 같은 기준으로 멈춥니다.
        public static bool IsTimePaused()
        {
            if (IsAnyDialogueOpen()) return true;
            if (Stage1StoryController.Instance != null && Stage1StoryController.Instance.IsPlaying) return true;
            return PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy;
        }

        /// <summary>산책이 진행 중이면 true</summary>
        public static bool IsWalking()
        {
            var walk = WalkGameManager.Current;
            return walk != null && walk.IsRunning;
        }

        /// <summary>산책 대화창 또는 팀 DialogueManager 대화창이 떠 있으면 true</summary>
        public static bool IsAnyDialogueOpen()
        {
            if (WalkDialogue.IsOpen) return true;
            return DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen;
        }

        static int CurrentStage1Day()
        {
            GManager manager = GManager.Instance != null ? GManager.Instance : FindAnyObjectByType<GManager>();
            return manager != null ? manager.Day : 1;
        }

        public static string FormatTime(float seconds)
        {
            int t = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{t / 60}:{t % 60:00}";
        }

        internal static GUIStyle HudStyle(int size = 22)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(size * Screen.height / 720f) };
            s.font = WalkUi.Font;
            s.normal.textColor = Color.white;
            return s;
        }
    }
}
