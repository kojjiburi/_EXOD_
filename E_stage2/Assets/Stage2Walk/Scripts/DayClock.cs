using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace EXOD.Walk
{
    /// <summary>
    /// 일차(DAY) 시계. 실제 시간 1분이 지나면 다음 날로 넘어간다.
    /// 씬이 바뀌어도 유지되며(DontDestroyOnLoad), 현관문 잠금 상태도 여기서 관리한다.
    ///
    /// 팀 프로젝트에 합칠 때:
    ///  - linkGManager를 켜 두면 씬에 있는 GManager의 Day 값을 읽고,
    ///    1분이 지나면 GManager.NextDay()를 자동으로 호출한다(리플렉션이라 GManager 코드를 고칠 필요 없음).
    ///  - 침대 등으로 GManager.NextDay()가 따로 호출되어 Day가 오르면 그것도 감지해서 타이머를 0으로 되돌린다.
    ///
    /// 하루 시간은 산책을 나간 뒤(산책 진행 중)에만 흐른다(runOnlyDuringWalk). 집에 있을 때는 멈춰 있다.
    /// 대화창이 떠 있는 동안에는 하루 시간이 멈춘다(pauseDuringDialogue).
    /// 산책 대화창(WalkDialogue)과 팀 DialogueManager 대화창 둘 다 감지한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DayClock : MonoBehaviour
    {
        public static DayClock Instance { get; private set; }

        /// <summary>새 날이 시작될 때 (인자: 새 일차)</summary>
        public static event Action<int> DayAdvanced;

        [Header("하루 길이 (초)")]
        [SerializeField] float dayLengthSeconds = 60f;

        [Header("시간이 흐르는 때")]
        [Tooltip("켜 두면 산책을 나간 뒤에만 하루 시간이 흐릅니다. 집에 있을 때는 멈춰 있습니다.")]
        [SerializeField] bool runOnlyDuringWalk = true;

        [Header("대화 중 멈춤")]
        [Tooltip("켜 두면 대화창이 떠 있는 동안 하루 시간이 줄지 않습니다.")]
        [SerializeField] bool pauseDuringDialogue = true;

        [Header("팀 GManager 연동")]
        [Tooltip("켜 두면 GManager의 Day 값을 읽고, 시간이 다 되면 GManager.NextDay()를 자동 호출합니다. GManager가 없으면 자체 일차를 사용합니다.")]
        [SerializeField] bool linkGManager = true;

        [Header("디버그")]
        [SerializeField] bool showDebugHud = true;

        [Tooltip("새 날이 시작될 때 실행 (인자: 새 일차)")]
        public UnityEvent<int> onDayAdvanced = new UnityEvent<int>();

        /// <summary>DAY를 다른 UI(1스테이지 상단 UI 등)가 이미 보여주고 있으면 true → 남은 시간만 표시</summary>
        public static bool ExternalDayUiShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ExternalDayUiShown = false;

        public float DayLength => dayLengthSeconds;
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0f, dayLengthSeconds - Elapsed);
        public bool Paused { get; set; }

        /// <summary>오늘 하루 현관문을 쓸 수 없는지</summary>
        public bool FrontDoorLocked { get; private set; }

        public int CurrentDay
        {
            get
            {
                if (linkGManager && TryReadExternalDay(out int d)) return d;
                return internalDay;
            }
        }

        int internalDay = 1;

        // ---------- GManager 리플렉션 ----------
        static Type gmType;
        static bool gmSearched;
        static FieldInfo dayField;
        static PropertyInfo dayProp;
        static MethodInfo nextDayMethod;
        UnityEngine.Object gmInstance;
        int lastSeenExternalDay = int.MinValue;
        float pollTimer;

        /// <summary>DayClock이 없으면 새로 만든다.</summary>
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
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (linkGManager) PollExternalDay();
            if (Paused) return;
            if (runOnlyDuringWalk && !IsWalking()) return;
            if (pauseDuringDialogue && IsAnyDialogueOpen()) return;

            Elapsed += Time.deltaTime;
            if (Elapsed >= dayLengthSeconds) AdvanceDay();
        }

        /// <summary>오늘 남은 시간에서 초를 뺀다. 0이 되면 바로 다음 날로 넘어간다.</summary>
        public void ConsumeTime(float seconds)
        {
            if (seconds <= 0f) return;
            Elapsed += seconds;
            if (Elapsed >= dayLengthSeconds) AdvanceDay();
        }

        /// <summary>다음 날이 될 때까지 현관문 잠금</summary>
        public void LockFrontDoorForToday() => FrontDoorLocked = true;

        /// <summary>다음 날로 넘긴다 (시간 종료, 침대 등).</summary>
        public void AdvanceDay()
        {
            internalDay++;
            if (linkGManager && TryCallNextDay())
            {
                if (TryReadExternalDay(out int d)) lastSeenExternalDay = d;
            }
            StartNewDay();
        }

        void StartNewDay()
        {
            Elapsed = 0f;
            FrontDoorLocked = false;
            int day = CurrentDay;
            Debug.Log($"[DayClock] DAY {day} 시작");
            onDayAdvanced?.Invoke(day);
            DayAdvanced?.Invoke(day);
        }

        // ---------- GManager 연동 ----------
        void PollExternalDay()
        {
            pollTimer -= Time.unscaledDeltaTime;
            if (pollTimer > 0f) return;
            pollTimer = 0.25f;

            bool hadInstance = gmInstance != null;
            if (!TryReadExternalDay(out int d)) return;
            bool newlyFound = !hadInstance && gmInstance != null;

            if (lastSeenExternalDay == int.MinValue || newlyFound || d < lastSeenExternalDay)
            {
                lastSeenExternalDay = d;
                return;
            }
            if (d > lastSeenExternalDay)
            {
                // 침대 등 외부에서 NextDay가 호출됨
                lastSeenExternalDay = d;
                StartNewDay();
            }
        }

        // ---------- 대화창 감지 ----------

        static bool dmSearched;
        static PropertyInfo dmInstanceProp;
        static PropertyInfo dmIsOpenProp;

        /// <summary>산책이 진행 중이면 true (산책 씬이 열리고 걷기가 시작된 뒤 ~ 끝날 때까지)</summary>
        public static bool IsWalking()
        {
            var walk = WalkGameManager.Current;
            return walk != null && walk.IsRunning;
        }

        /// <summary>산책 대화창 또는 팀 DialogueManager 대화창이 떠 있으면 true</summary>
        public static bool IsAnyDialogueOpen()
        {
            if (WalkDialogue.IsOpen) return true;
            return TeamDialogueOpen();
        }

        static bool TeamDialogueOpen()
        {
            if (!dmSearched)
            {
                dmSearched = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = asm.GetName().Name;
                    if (n.StartsWith("System") || n.StartsWith("Unity") || n.StartsWith("mscorlib") ||
                        n.StartsWith("Mono") || n.StartsWith("netstandard") || n.StartsWith("nunit"))
                        continue;

                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { types = e.Types; }

                    foreach (var t in types)
                    {
                        if (t == null || t.Name != "DialogueManager") continue;
                        var inst = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        var open = t.GetProperty("IsDialogueOpen", BindingFlags.Public | BindingFlags.Instance);
                        if (inst != null && open != null && open.PropertyType == typeof(bool))
                        {
                            dmInstanceProp = inst;
                            dmIsOpenProp = open;
                            break;
                        }
                    }
                    if (dmInstanceProp != null) break;
                }
            }
            if (dmInstanceProp == null) return false;

            try
            {
                object manager = dmInstanceProp.GetValue(null);
                if (manager == null || (manager is UnityEngine.Object o && o == null)) return false;
                return (bool)dmIsOpenProp.GetValue(manager);
            }
            catch
            {
                return false;
            }
        }

        static bool ResolveGManagerType()
        {
            if (!gmSearched)
            {
                gmSearched = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = asm.GetName().Name;
                    if (n.StartsWith("System") || n.StartsWith("Unity") || n.StartsWith("mscorlib") ||
                        n.StartsWith("Mono") || n.StartsWith("netstandard") || n.StartsWith("nunit"))
                        continue;

                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { types = e.Types; }

                    foreach (var t in types)
                    {
                        if (t != null && t.Name == "GManager") { gmType = t; break; }
                    }
                    if (gmType != null) break;
                }

                if (gmType != null)
                {
                    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase;
                    dayField = gmType.GetField("Day", F);
                    if (dayField == null) dayProp = gmType.GetProperty("Day", F);
                    nextDayMethod = gmType.GetMethod("NextDay", F, null, Type.EmptyTypes, null);
                }
            }
            return gmType != null;
        }

        bool TryGetTarget(bool isStatic, out object target)
        {
            target = null;
            if (isStatic) return true;
            if (gmInstance == null)
            {
                if (!typeof(UnityEngine.Object).IsAssignableFrom(gmType)) return false;
                gmInstance = FindAnyObjectByType(gmType);
            }
            target = gmInstance;
            return gmInstance != null;
        }

        bool TryReadExternalDay(out int day)
        {
            day = 0;
            if (!ResolveGManagerType()) return false;
            try
            {
                if (dayField != null)
                {
                    if (!TryGetTarget(dayField.IsStatic, out var tgt)) return false;
                    day = Convert.ToInt32(dayField.GetValue(tgt));
                    return true;
                }
                if (dayProp != null && dayProp.CanRead)
                {
                    var getter = dayProp.GetGetMethod(true);
                    if (!TryGetTarget(getter.IsStatic, out var tgt)) return false;
                    day = Convert.ToInt32(dayProp.GetValue(tgt));
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        bool TryCallNextDay()
        {
            if (!ResolveGManagerType() || nextDayMethod == null) return false;
            if (!TryGetTarget(nextDayMethod.IsStatic, out var tgt)) return false;
            try
            {
                nextDayMethod.Invoke(tgt, null);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DayClock] GManager.NextDay() 호출 실패: {e.InnerException?.Message ?? e.Message}");
                return false;
            }
        }

        // ---------- 디버그 표시 ----------
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

        // ---------- 남은 시간 표시 (1스테이지 UI와 같은 이름표 상자) ----------
        UnityEngine.UI.Text timeText;
        UnityEngine.UI.Text lockText;
        GameObject view;
        float teamUiCheckTimer;
        bool teamDayUi;

        void Start()
        {
            if (showDebugHud) BuildView();
        }

        void BuildView()
        {
            Canvas canvas = WalkUi.CreateCanvas("DayClockCanvas", 640, transform);
            view = canvas.gameObject;

            // 화면 오른쪽 위. 산책 중 표시(WalkHud)는 이 아래에 놓인다.
            timeText = WalkUi.CreateNameBox("남은 시간", canvas.transform, out RectTransform box, 32, WalkUi.DialogueTextColor);
            WalkUi.Place(box, new Vector2(1f, 1f), new Vector2(-170f, -60f), WalkUi.NameBoxSize);

            lockText = WalkUi.CreateOutlinedText("현관문 잠김", canvas.transform, 22, TextAnchor.MiddleCenter);
            WalkUi.Place(lockText.rectTransform, new Vector2(1f, 1f), new Vector2(-170f, -112f), new Vector2(300f, 32f));
            lockText.text = "오늘은 현관문 사용 불가";
        }

        void LateUpdate()
        {
            if (view == null) return;

            teamUiCheckTimer -= Time.unscaledDeltaTime;
            if (teamUiCheckTimer <= 0f)
            {
                teamUiCheckTimer = 0.5f;
                teamDayUi = ExternalDayUiShown || (linkGManager && TryReadExternalDay(out _));
            }

            // 팀 상단 UI가 DAY를 보여주면 남은 시간만, 없으면 DAY도 함께
            timeText.text = teamDayUi
                ? "남은 시간 " + FormatTime(Remaining)
                : $"DAY {CurrentDay}  {FormatTime(Remaining)}";
            if (lockText.gameObject.activeSelf != FrontDoorLocked)
                lockText.gameObject.SetActive(FrontDoorLocked);
        }
    }
}
