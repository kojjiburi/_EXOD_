using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EXOD.Walk
{
    /// <summary>
    /// 전화 부스 안 (클로즈업 화면). 빨간 톤의 부스 안이 화면을 덮고, 가운데 유리창 너머로 그녀가 보인다.
    ///  - 산책 시간은 계속 흐르고, 그녀가 뒤돌아봤을 때 클릭하면 들킨다 (화면 아무 데나 클릭 포함, 산책 설정 그대로).
    ///  - 부스 안에 있는 동안 화면(카메라)은 멈춘다. 왕복을 못 해도 불이익은 없다.
    ///  - 퀵슬롯에서 아이템을 골라 해당 위치를 클릭해 쓴다 (테이프 → 수화기 선, 버튼 [1]·[2] → 빈 자리, 주소지 → 수화기).
    ///  - 전화를 다 고치면 신호음이 들리고, 1·1·2(경찰) 또는 1·1·9(119)를 누르면 연결된다. 어느 쪽이든 신고에 성공하면 같은 탈출 엔딩.
    ///  - 주소지를 골라 수화기를 클릭하면 위치를 말하기 시작한다. 다 말하는 데 reportSeconds초(산책 시간)가 걸리고,
    ///    그 전에 산책이 끝나면 전화가 끊긴다 (다음 날 다시 걸어야 함).
    ///  - '부스에서 나가기' 또는 Esc로 나간다.
    ///
    /// 이미지 칸을 비워 두면 임시 모양(빨간 판, 회색 전화기)으로 보인다.
    /// background에 그림을 넣을 때는 유리창 부분을 투명하게 뚫어 두면 그 너머로 산책 화면(그녀)이 보인다.
    /// </summary>
    public class PhoneBoothInterior : MonoBehaviour, IWalkOverlay
    {
        enum CallState { Idle, Dialing, Connected, Reporting, Done }

        [Header("이미지 (비워 두면 임시 모양)")]
        [Tooltip("부스 안 화면 전체 그림. 유리창 부분은 투명하게")]
        [SerializeField] Sprite background;
        [SerializeField] Sprite phoneBody;
        [SerializeField] Sprite handsetSprite;
        [SerializeField] Sprite cordCutSprite;
        [SerializeField] Sprite cordFixedSprite;
        [Tooltip("다이얼 버튼 (모든 버튼에 같이 쓰임)")]
        [SerializeField] Sprite dialKeySprite;
        [Tooltip("버튼이 빠진 빈 자리")]
        [SerializeField] Sprite missingKeySprite;

        [Header("화면 배치 (화면 비율 0~1, 왼쪽 아래 기준 x, y, 너비, 높이)")]
        [Tooltip("유리창 (그녀가 보이는 곳). 그녀는 화면 가운데에 서 있다")]
        [SerializeField] Rect windowArea = new Rect(0.365f, 0.215f, 0.27f, 0.55f);
        [SerializeField] Rect phoneArea = new Rect(0.06f, 0.23f, 0.25f, 0.66f);
        [SerializeField] Rect callArea = new Rect(0.67f, 0.33f, 0.29f, 0.45f);
        [SerializeField] Rect exitArea = new Rect(0.74f, 0.225f, 0.2f, 0.075f);

        [Header("색 (임시 모양)")]
        [SerializeField] Color boothRed = new Color(0.50f, 0.03f, 0.06f, 1f);
        [SerializeField] Color boothDarkRed = new Color(0.26f, 0.01f, 0.03f, 1f);
        [SerializeField] Color glassTint = new Color(1f, 0.08f, 0.08f, 0.16f);
        [Tooltip("켜면 유리창 위에 glassTint 색을 덧씌운다 (끄면 완전히 투명한 유리)")]
        [SerializeField] bool tintGlass = false;
        [Tooltip("퀵슬롯(590)·상단 UI(600)보다 아래, 대화창(700)보다 아래")]
        [SerializeField] int canvasSortingOrder = 580;

        [Header("부스 안에서 숨길 화면 (DAY·하트·위험도 상단 UI)")]
        [Tooltip("부스 안에 있는 동안 숨길 오브젝트 이름. 그 오브젝트의 Canvas만 끄고(스크립트는 계속 동작) 나오면 다시 켠다.\n" +
                 "EXOD_PlayerStatusSystem = 통합 프로젝트의 팀 상단 UI, TeamStatusBarPreview… = 이 프로젝트의 미리보기")]
        [SerializeField] string[] hideWhileInside = { "EXOD_PlayerStatusSystem", "TeamStatusBarPreview (1스테이지 상단 UI)" };
        [Tooltip("이름 대신 직접 넣어도 된다")]
        [SerializeField] GameObject[] hideObjectsWhileInside = new GameObject[0];

        [Header("수화기 들기")]
        [Tooltip("마우스를 올렸을 때 수화기가 들리는 높이 (1920×1080 기준 픽셀)")]
        [SerializeField] float handsetHoverLift = 18f;
        [Tooltip("통화하는 동안 수화기가 들려 있는 높이")]
        [SerializeField] float handsetCallLift = 34f;
        [Tooltip("통화 중일 때 기울어지는 각도")]
        [SerializeField] float handsetTilt = 12f;

        [Header("신고")]
        [SerializeField] string emergencyNumber = "112";
        [Tooltip("112 말고도 신고가 되는 번호. 이 번호로 걸면 '대사 - 119' 대사가 나온다")]
        [SerializeField] string fireNumber = "119";
        [Tooltip("위치를 다 말하는 데 걸리는 산책 시간(초). 대화창이 떠 있을 때는 멈춘다. 그 전에 산책이 끝나면 전화가 끊긴다.")]
        [SerializeField] float reportSeconds = 8f;
        [SerializeField, TextArea(2, 5)] string reportSpeech =
            "여기는 해월시 서림동 은하로 3길 27 앞이에요. 가로등 관리번호는 0412-B예요. 지금 집에 갇혀 있어요… 제발 와 주세요.";

        [Header("소리 (선택)")]
        [SerializeField] AudioClip dialToneClip;
        [SerializeField] AudioClip keyBeepClip;
        [SerializeField] AudioClip connectClip;
        [SerializeField] AudioClip hangUpClip;

        [Header("대사 - 들어갈 때")]
        [SerializeField] WalkLine[] firstEnterLines =
        {
            new WalkLine("", "(부스 안은 온통 빨갛다. 유리창 너머로 그녀의 뒷모습이 보인다.)"),
            new WalkLine("", "(전화기는 수화기 선이 잘려 있고, 다이얼 버튼 [1]과 [2]가 빠져 있다.)"),
            new WalkLine("", "(여기서도 그녀가 뒤돌아봤을 때 움직이면 들킨다.)")
        };
        [SerializeField] WalkLine[] enterAfterReportLines =
        {
            new WalkLine("", "(신고는 끝났다. 이제 경찰이 오기를 기다리면 된다.)")
        };

        [Header("대사 - 수리")]
        [SerializeField] WalkLine[] cordCutLines = { new WalkLine("", "(수화기 선이 잘려 있다. 무언가로 이어 붙여야 한다.)") };
        [SerializeField] WalkLine[] cordFixedLines = { new WalkLine("", "(테이프로 감아 둔 선이다. 단단히 붙어 있다.)") };
        [SerializeField] WalkLine[] tapeUsedLines = { new WalkLine("", "(절연 테이프로 잘린 선을 꽁꽁 감아 이었다.)") };
        [SerializeField] WalkLine[] missingButton1Lines = { new WalkLine("", "(버튼 하나가 빠져 있다. [1] 자리다.)") };
        [SerializeField] WalkLine[] missingButton2Lines = { new WalkLine("", "(버튼 하나가 빠져 있다. [2] 자리다.)") };
        [SerializeField] WalkLine[] button1UsedLines = { new WalkLine("", "(빈 자리에 [1] 버튼을 꾹 끼웠다.)") };
        [SerializeField] WalkLine[] button2UsedLines = { new WalkLine("", "(빈 자리에 [2] 버튼을 꾹 끼웠다.)") };
        // [하드] 젖은 버튼 [2]를 끼우려 할 때 (수건으로 닦아야 함, DialButtonWetness — 사용자 요청 2026-11-03)
        static readonly WalkLine[] wetButton2Lines = { new WalkLine("주인공", "이거.. 감전될 거 같은데?") };
        [SerializeField] WalkLine[] repairedLines = { new WalkLine("", "(뚜—… 수화기에서 신호음이 들린다. 이제 번호를 누를 수 있다.)") };

        [Header("대사 - 전화")]
        [SerializeField] WalkLine[] brokenKeyLines = { new WalkLine("", "(버튼을 눌러 봤지만 아무 소리도 나지 않는다. 전화가 고장 나 있다.)") };
        [SerializeField] WalkLine[] handsetDeadLines = { new WalkLine("", "(수화기를 귀에 대 봤지만 아무 소리도 들리지 않는다.)") };
        [SerializeField] WalkLine[] handsetToneLines = { new WalkLine("", "(뚜— 신호음이 들린다. 어디에 걸어야 할까.)") };
        [SerializeField] WalkLine[] wrongNumberLines = { new WalkLine("", "(없는 번호라는 안내가 흘러나왔다.)") };
        [SerializeField] WalkLine[] connectedLines =
        {
            new WalkLine("경찰", "네, 112입니다. 지금 계신 곳이 어디입니까?")
        };
        [Tooltip("연결됐지만 주소지를 고르지 않고 수화기를 클릭했을 때")]
        [SerializeField] WalkLine[] needAddressLines =
        {
            new WalkLine("경찰", "여보세요? 지금 계신 곳 주소를 말씀해 주세요."),
            new WalkLine("", "(여기가 어디인지 정확히 말해야 한다. 퀵슬롯의 [주소지]를 수화기에 끌어다 놓자.)")
        };
        [SerializeField] WalkLine[] reportDoneLines =
        {
            new WalkLine("경찰", "확인했습니다. 지금 바로 출동하겠습니다."),
            new WalkLine("경찰", "들키지 않게 평소처럼 행동하세요. 전화는 끊으셔도 됩니다."),
            new WalkLine("", "(조용히 수화기를 내려놓았다.)")
        };
        [SerializeField] WalkLine[] afterReportLines = { new WalkLine("", "(할 일은 끝났다. 이제 평소처럼 돌아가자.)") };
        [Header("대사 - 119 (fireNumber로 걸었을 때)")]
        [SerializeField] WalkLine[] connected119Lines =
        {
            new WalkLine("119", "네, 119입니다. 무슨 일이세요? 지금 계신 곳이 어디입니까?")
        };
        [SerializeField] WalkLine[] needAddress119Lines =
        {
            new WalkLine("119", "여보세요? 주소를 말씀해 주셔야 출동할 수 있어요."),
            new WalkLine("", "(여기가 어디인지 정확히 말해야 한다. 퀵슬롯의 [주소지]를 수화기에 끌어다 놓자.)")
        };
        [SerializeField] WalkLine[] reportDone119Lines =
        {
            new WalkLine("119", "확인했습니다. 경찰에도 바로 알리고 함께 출동하겠습니다."),
            new WalkLine("119", "들키지 않게 평소처럼 행동하세요. 전화는 끊으셔도 됩니다."),
            new WalkLine("", "(조용히 수화기를 내려놓았다.)")
        };

        [Tooltip("통화 중에 부스를 나갔을 때")]
        [SerializeField] WalkLine[] hangUpLeaveLines = { new WalkLine("", "(수화기를 내려놓자 전화가 끊겼다.)") };

        // ---------------- 상태 ----------------
        public PhoneBooth Booth { get; set; }
        public bool IsOpen { get; private set; }
        /// <summary>경찰과 연결됐지만 아직 신고를 끝내지 못한 상태</summary>
        public bool IsOnCall => call == CallState.Connected || call == CallState.Reporting;
        /// <summary>이번 산책에서 신고를 마쳤는지</summary>
        public bool ReportedThisWalk { get; private set; }

        static bool firstEnterShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => firstEnterShown = false;

        CallState call = CallState.Idle;
        string dialed = "";
        string connectedNumber = "";
        bool Is119 => !string.IsNullOrEmpty(fireNumber) && connectedNumber == fireNumber;
        string CallerName => Is119 ? "119" : "경찰";
        float reportProgress;

        // ---------------- 화면 ----------------
        class Hotspot
        {
            public string id;
            public RectTransform rect;
            public Image image;
            public Text label;
            public Color baseColor;
            public Action onClick;
        }

        readonly List<Hotspot> hotspots = new List<Hotspot>();
        Canvas canvas;
        Text displayText;
        Text callTitle;
        Text callBody;
        RectTransform progressFill;
        GameObject progressBar;
        Hotspot cordSpot, handsetSpot, exitSpot;
        RectTransform handsetVisual;
        float handsetLift;
        Hotspot key1Spot, key2Spot;
        readonly List<Hotspot> keySpots = new List<Hotspot>();
        AudioSource audioSource;
        Hotspot hovered;
        readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        readonly List<CanvasGroup> hiddenGroups = new List<CanvasGroup>();
        readonly List<CanvasGroup> addedGroups = new List<CanvasGroup>();

        static readonly string[] KeyOrder = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "*", "0", "#" };

        void OnDisable()
        {
            if (ReferenceEquals(WalkGameManager.Overlay, this)) WalkGameManager.Overlay = null;
            ShowStatusUi();   // 산책 씬이 닫힐 때도 상단 UI를 꼭 되돌린다
        }

        // ---------------- 상단 UI 숨기기 ----------------
        void HideStatusUi()
        {
            ShowStatusUi();
            var targets = new List<GameObject>();
            if (hideObjectsWhileInside != null)
                foreach (var go in hideObjectsWhileInside) if (go != null) targets.Add(go);
            if (hideWhileInside != null)
                foreach (var n in hideWhileInside)
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    var go = GameObject.Find(n);
                    if (go != null && !targets.Contains(go)) targets.Add(go);
                }

            foreach (var go in targets)
            {
                var canvases = go.GetComponentsInChildren<Canvas>(false);
                if (canvases.Length > 0)
                {
                    foreach (var c in canvases)
                        if (c.enabled) { c.enabled = false; hiddenCanvases.Add(c); }
                }
                else
                {
                    // 자체 Canvas가 없는 UI면 CanvasGroup으로 투명하게
                    var g = go.GetComponent<CanvasGroup>();
                    if (g == null) { g = go.AddComponent<CanvasGroup>(); addedGroups.Add(g); }
                    else hiddenGroups.Add(g);
                    g.alpha = 0f;
                }
            }
        }

        void ShowStatusUi()
        {
            foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
            hiddenCanvases.Clear();
            foreach (var g in hiddenGroups) if (g != null) g.alpha = 1f;
            hiddenGroups.Clear();
            foreach (var g in addedGroups) if (g != null) Destroy(g);
            addedGroups.Clear();
        }

        // ---------------- 열기 / 닫기 ----------------
        public void Open()
        {
            var manager = WalkGameManager.Current;
            if (IsOpen || manager == null || !manager.IsRunning) return;
            if (canvas == null) Build();

            IsOpen = true;
            handsetLift = 0f;
            canvas.gameObject.SetActive(true);
            WalkGameManager.Overlay = this;
            HideStatusUi();
            RefreshAll();
            Debug.Log("[PhoneBooth] 부스 안으로 들어감");

            var lines = new List<WalkLine>();
            if (!firstEnterShown && firstEnterLines != null)
            {
                foreach (var line in firstEnterLines)
                {
                    // [이지] 수화기 선·버튼 [2]는 처음부터 고쳐져 있으므로 빠진 건 버튼 [1]뿐 (사용자 요청 2026-11-05)
                    if (GameDifficulty.IsEasy && line.text != null && line.text.Contains("수화기 선이 잘려"))
                        lines.Add(new WalkLine(line.speaker, "(전화기는 다이얼 버튼 [1]이 빠져 있다.)"));
                    else
                        lines.Add(line);
                }
            }
            firstEnterShown = true;
            if (PhoneBoothProgress.Reported && enterAfterReportLines != null) lines.AddRange(enterAfterReportLines);
            Say(lines);
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (IsOnCall)
            {
                DropCall();
                Play(hangUpClip);
                Say(hangUpLeaveLines);
            }
            else if (call == CallState.Dialing)
            {
                call = CallState.Idle;
                dialed = "";
            }

            IsOpen = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
            ShowStatusUi();
            if (ReferenceEquals(WalkGameManager.Overlay, this)) WalkGameManager.Overlay = null;
            Debug.Log("[PhoneBooth] 부스에서 나옴");
        }

        /// <summary>통화를 끊는다 (신고를 끝내지 못한 경우 처음부터 다시 걸어야 함)</summary>
        public void DropCall()
        {
            if (call == CallState.Done) return;
            call = CallState.Idle;
            dialed = "";
            reportProgress = 0f;
            RefreshAll();
        }

        // ---------------- IWalkOverlay ----------------
        public bool HandleEscape()
        {
            if (!IsOpen) return false;
            Close();
            return true;
        }

        public bool HandleClick(Vector2 screenPosition)
        {
            if (!IsOpen) return false;
            var manager = WalkGameManager.Current;

            Hotspot target = FindHotspot(screenPosition);
            if (target == null && manager != null && !manager.AnyClickCountsAsInteraction) return true;

            // 그녀가 뒤돌아보고 있으면 여기서 들킨다 (산책이 끝나고 클릭한 동작은 실행되지 않음)
            if (!WalkGameManager.ReportInteraction()) return true;

            target?.onClick?.Invoke();
            return true;
        }

        Hotspot FindHotspot(Vector2 screenPosition)
        {
            for (int i = hotspots.Count - 1; i >= 0; i--)
            {
                var h = hotspots[i];
                if (h.rect != null && h.rect.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(h.rect, screenPosition, null))
                    return h;
            }
            return null;
        }

        // ---------------- 매 프레임 ----------------
        void Update()
        {
            if (!IsOpen) return;
            var manager = WalkGameManager.Current;
            bool running = manager != null && manager.IsRunning;

            // 신고 중: 대화창이 없을 때 산책 시간과 함께 흐른다
            if (call == CallState.Reporting && running && !WalkDialogue.IsOpen)
            {
                reportProgress += Time.deltaTime;
                if (reportProgress >= reportSeconds) FinishReport();
                else RefreshCallPanel();
            }

            UpdateHover(running && !WalkDialogue.IsOpen);
            UpdateHandset();
        }

        /// <summary>마우스를 올리면 살짝 들리고, 통화 중에는 들린 채로 있다. 클릭 판정 영역은 제자리.</summary>
        void UpdateHandset()
        {
            if (handsetVisual == null) return;
            float target = IsOnCall ? handsetCallLift : (hovered == handsetSpot ? handsetHoverLift : 0f);
            handsetLift = Mathf.Lerp(handsetLift, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            float t = handsetCallLift > 0f ? Mathf.Clamp01(handsetLift / handsetCallLift) : 0f;
            handsetVisual.anchoredPosition = new Vector2(6f * t, handsetLift);
            handsetVisual.localRotation = Quaternion.Euler(0f, 0f, handsetTilt * t);
        }

        void UpdateHover(bool active)
        {
            Hotspot now = active ? FindHotspot(WalkInput.PointerScreenPosition()) : null;
            if (now == hovered) return;
            if (hovered != null && hovered.image != null) hovered.image.color = hovered.baseColor;
            hovered = now;
            if (hovered != null && hovered.image != null)
                hovered.image.color = Color.Lerp(hovered.baseColor, new Color(1f, 0.85f, 0.45f, 1f), 0.45f);   // 노랗게 강조
        }

        // ---------------- 동작 ----------------
        void OnCord()
        {
            if (PhoneBoothProgress.CordFixed) { Say(cordFixedLines); return; }

            var tape = Booth != null ? Booth.InsulatingTape : null;
            if (tape != null && WalkItems.IsUsing(tape))
            {
                PhoneBoothProgress.CordFixed = true;
                WalkItems.UseUp(tape);
                AfterRepair(tapeUsedLines);
                Debug.Log("[PhoneBooth] 수화기 선 수리");
            }
            else Say(cordCutLines);
        }

        void OnKey(string key)
        {
            if (key == "1" && !PhoneBoothProgress.Button1Installed) { TryInstall(1); return; }
            if (key == "2" && !PhoneBoothProgress.Button2Installed) { TryInstall(2); return; }

            if (call == CallState.Done) { Say(afterReportLines); return; }
            if (IsOnCall) { Play(keyBeepClip); return; }
            if (!PhoneBoothProgress.PhoneRepaired) { Say(brokenKeyLines); return; }

            Play(keyBeepClip);
            call = CallState.Dialing;
            dialed += key;
            if (dialed.Length >= emergencyNumber.Length)
            {
                if (dialed == emergencyNumber || (!string.IsNullOrEmpty(fireNumber) && dialed == fireNumber)) Connect(dialed);
                else
                {
                    Debug.Log($"[PhoneBooth] 잘못 건 번호: {dialed}");
                    call = CallState.Idle;
                    dialed = "";
                    Say(wrongNumberLines);
                }
            }
            RefreshAll();
        }

        void TryInstall(int number)
        {
            ItemData item = Booth == null ? null : number == 1 ? Booth.DialButton1 : Booth.DialButton2;
            if (number == 2 && item != null && WalkItems.IsUsing(item) && DialButtonWetness.IsWet(WalkItems.Selected))
            {
                EXODSound.Play("electric_zap");
                Say(wetButton2Lines);
                return;
            }
            if (item != null && WalkItems.IsUsing(item))
            {
                if (number == 1) PhoneBoothProgress.Button1Installed = true;
                else PhoneBoothProgress.Button2Installed = true;
                WalkItems.UseUp(item);
                AfterRepair(number == 1 ? button1UsedLines : button2UsedLines);
                Debug.Log($"[PhoneBooth] 다이얼 버튼 [{number}] 끼움");
            }
            else Say(number == 1 ? missingButton1Lines : missingButton2Lines);
        }

        void AfterRepair(WalkLine[] actionLines)
        {
            var lines = new List<WalkLine>();
            if (actionLines != null) lines.AddRange(actionLines);
            if (PhoneBoothProgress.PhoneRepaired)
            {
                if (repairedLines != null) lines.AddRange(repairedLines);
                Play(dialToneClip);
                Debug.Log("[PhoneBooth] 전화 수리 완료 (신호음)");
            }
            RefreshAll();
            Say(lines);
        }

        void OnCallPanelDrop()
        {
            var address = Booth != null ? Booth.AddressNote : null;
            if (call == CallState.Connected && address != null && WalkItems.IsUsing(address))
                StartReport();
        }

        void OnHandset()
        {
            switch (call)
            {
                case CallState.Connected:
                    var address = Booth != null ? Booth.AddressNote : null;
                    if (address != null && WalkItems.IsUsing(address)) StartReport();
                    else Say(Is119 ? needAddress119Lines : needAddressLines);
                    return;
                case CallState.Reporting:
                    return;
                case CallState.Done:
                    Say(afterReportLines);
                    return;
            }

            if (PhoneBoothProgress.PhoneRepaired) { Play(dialToneClip); Say(handsetToneLines); }
            else Say(handsetDeadLines);
        }

        void Connect(string number)
        {
            call = CallState.Connected;
            connectedNumber = number;
            dialed = number;
            Play(connectClip);
            Debug.Log($"[PhoneBooth] {number} 연결");
            RefreshAll();
            Say(Is119 ? connected119Lines : connectedLines);
        }

        void StartReport()
        {
            call = CallState.Reporting;
            reportProgress = 0f;
            Debug.Log("[PhoneBooth] 위치 말하기 시작");
            RefreshAll();
        }

        void FinishReport()
        {
            call = CallState.Done;
            reportProgress = reportSeconds;
            ReportedThisWalk = true;
            PhoneBoothProgress.Reported = true;
            if (Booth != null)
            {
                WalkItems.UseUp(Booth.AddressNote);
                Booth.OnReported();
            }
            Play(hangUpClip);
            // 신고 성공 대사가 나오는 동안 그녀가 뒤돌아본 채로 있지 않게 앞을 보게 돌려놓음 (사용자 요청 2026-11-03)
            var yandere = FindFirstObjectByType<WalkYandere>();
            if (yandere != null) yandere.CalmDown();
            Debug.Log("[PhoneBooth] 신고 완료 → 하루가 끝나면 탈출 엔딩");
            RefreshAll();
            Say(Is119 ? reportDone119Lines : reportDoneLines);
        }

        // ---------------- 표시 갱신 ----------------
        void RefreshAll()
        {
            if (canvas == null) return;

            bool cord = PhoneBoothProgress.CordFixed;
            SetSprite(cordSpot, cord ? cordFixedSprite : cordCutSprite, cord ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.55f, 0.18f, 0.12f));
            cordSpot.label.text = cord ? "수화기 선\n(테이프)" : "수화기 선\n(잘림)";
            cordSpot.label.enabled = cordSpot.image.sprite == null;   // 그림이 있으면 글자는 숨김
            handsetSpot.label.enabled = handsetSpot.image.sprite == null;

            UpdateKey(key1Spot, "1", PhoneBoothProgress.Button1Installed);
            UpdateKey(key2Spot, "2", PhoneBoothProgress.Button2Installed);

            displayText.text = DisplayLine();
            RefreshCallPanel();
        }

        void UpdateKey(Hotspot spot, string number, bool installed)
        {
            if (installed)
            {
                SetSprite(spot, dialKeySprite, new Color(0.82f, 0.80f, 0.76f));
                spot.label.text = number;
                spot.label.color = new Color(0.15f, 0.1f, 0.1f);
            }
            else
            {
                SetSprite(spot, missingKeySprite, new Color(0.08f, 0.06f, 0.06f));
                spot.label.text = missingKeySprite != null ? "" : "빈 자리";
                spot.label.color = new Color(0.7f, 0.6f, 0.6f);
            }
        }

        string DisplayLine()
        {
            switch (call)
            {
                case CallState.Dialing: return dialed;
                case CallState.Connected: return connectedNumber + "  연결됨";
                case CallState.Reporting: return connectedNumber + "  통화 중";
                case CallState.Done: return "통화 종료";
            }
            return PhoneBoothProgress.PhoneRepaired ? "뚜—" : "- - -";
        }

        void RefreshCallPanel()
        {
            if (callBody == null) return;
            bool showBar = call == CallState.Reporting;
            if (progressBar.activeSelf != showBar) progressBar.SetActive(showBar);

            switch (call)
            {
                case CallState.Connected:
                    callTitle.text = "통화: " + CallerName;
                    callBody.text = CallerName + ": 지금 계신 곳이 어디입니까?\n\n(퀵슬롯의 [주소지]를 수화기에 끌어다 놓기)";
                    break;
                case CallState.Reporting:
                    float t = Mathf.Clamp01(reportProgress / Mathf.Max(0.01f, reportSeconds));
                    int n = Mathf.Clamp(Mathf.CeilToInt(reportSpeech.Length * t), 0, reportSpeech.Length);
                    callTitle.text = "통화: 위치 말하는 중";
                    callBody.text = "나: " + reportSpeech.Substring(0, n) + "\n\n(다 말하기 전에 산책이 끝나면 전화가 끊긴다)";
                    progressFill.anchorMax = new Vector2(t, 1f);
                    break;
                case CallState.Done:
                    callTitle.text = "신고 완료";
                    callBody.text = "경찰이 오기로 했다.\n들키지 않게 평소처럼 돌아가자.";
                    break;
                default:
                    callTitle.text = "전화";
                    callBody.text = HintLine();
                    break;
            }
        }

        string HintLine()
        {
            if (!PhoneBoothProgress.CordFixed && !(PhoneBoothProgress.Button1Installed && PhoneBoothProgress.Button2Installed))
                return "수화기 선이 잘려 있고 버튼이 빠져 있다.";
            if (!PhoneBoothProgress.CordFixed) return "수화기 선이 잘려 있다.";
            if (!PhoneBoothProgress.PhoneRepaired) return "다이얼 버튼이 빠져 있다.";
            return call == CallState.Dialing ? "누른 번호: " + dialed : "신호음이 들린다. 번호를 누를 수 있다.";
        }

        static void SetSprite(Hotspot spot, Sprite sprite, Color fallback)
        {
            if (spot == null || spot.image == null) return;
            spot.image.sprite = sprite;
            spot.baseColor = sprite != null ? Color.white : fallback;
            spot.image.color = spot.baseColor;
        }

        // ---------------- 화면 만들기 ----------------
        void Build()
        {
            canvas = WalkUi.CreateCanvas("PhoneBoothInteriorCanvas", canvasSortingOrder, transform);
            Transform root = canvas.transform;

            // 부스 안 벽 (유리창만 뚫려 있음)
            Rect w = windowArea;
            if (background != null)
            {
                var bg = WalkUi.CreateImage("부스 안 배경", root, background, Color.white);
                WalkUi.Stretch(bg.rectTransform);
            }
            else
            {
                Panel(root, "벽 왼쪽", new Rect(0f, 0f, w.xMin, 1f), boothRed);
                Panel(root, "벽 오른쪽", new Rect(w.xMax, 0f, 1f - w.xMax, 1f), boothRed);
                Panel(root, "벽 아래", new Rect(w.xMin, 0f, w.width, w.yMin), boothDarkRed);
                Panel(root, "벽 위", new Rect(w.xMin, w.yMax, w.width, 1f - w.yMax), boothDarkRed);

                // 유리창 틀
                const float f = 0.008f;
                Panel(root, "창틀 왼쪽", new Rect(w.xMin - f, w.yMin - f, f, w.height + f * 2f), boothDarkRed);
                Panel(root, "창틀 오른쪽", new Rect(w.xMax, w.yMin - f, f, w.height + f * 2f), boothDarkRed);
                Panel(root, "창틀 위", new Rect(w.xMin, w.yMax, w.width, f), boothDarkRed);
                Panel(root, "창틀 아래", new Rect(w.xMin, w.yMin - f, w.width, f), boothDarkRed);
            }
            if (tintGlass) Panel(root, "유리 (빨간 빛)", w, glassTint);

            BuildPhone(root);
            BuildCallPanel(root);

            exitSpot = AddHotspot("나가기", root, exitArea, null, new Color(0.18f, 0.05f, 0.06f, 0.95f), "부스에서 나가기 (Esc)", 30, Close);
            exitSpot.label.color = Color.white;

            canvas.gameObject.SetActive(false);
        }

        void BuildPhone(Transform root)
        {
            var body = WalkUi.CreateImage("전화기", root, phoneBody, new Color(0.16f, 0.13f, 0.13f, 1f));
            SetAnchors(body.rectTransform, phoneArea);
            Transform p = body.rectTransform;

            // 번호 표시창
            var screen = WalkUi.CreateImage("표시창", p, null, new Color(0.05f, 0.12f, 0.08f, 1f));
            SetAnchors(screen.rectTransform, new Rect(0.08f, 0.86f, 0.84f, 0.11f));
            displayText = WalkUi.CreateText("번호", screen.rectTransform, 40, new Color(0.55f, 1f, 0.6f), TextAnchor.MiddleCenter);
            WalkUi.Stretch(displayText.rectTransform, new Vector2(-16f, -8f));

            handsetSpot = AddHotspot("수화기", p, new Rect(0.06f, 0.63f, 0.56f, 0.2f), null, Color.clear, "수화기", 32, OnHandset);
            handsetSpot.label.color = new Color(0.85f, 0.85f, 0.85f);
            // 그림은 판정 영역 안의 자식으로 따로 두고 이것만 움직인다
            var visual = WalkUi.CreateImage("수화기 그림", handsetSpot.rect, handsetSprite, new Color(0.08f, 0.08f, 0.08f));
            WalkUi.Stretch(visual.rectTransform);
            visual.transform.SetAsFirstSibling();
            handsetSpot.image = visual;
            handsetSpot.baseColor = visual.color;
            handsetVisual = visual.rectTransform;
            cordSpot = AddHotspot("수화기 선", p, new Rect(0.65f, 0.63f, 0.29f, 0.2f), cordCutSprite, new Color(0.55f, 0.18f, 0.12f), "수화기 선", 24, OnCord);
            cordSpot.label.color = Color.white;

            // 다이얼 (4줄 × 3칸)
            var pad = new Rect(0.08f, 0.04f, 0.84f, 0.55f);
            const float gap = 0.02f;
            float cw = (pad.width - gap * 2f) / 3f;
            float ch = (pad.height - gap * 3f) / 4f;
            for (int i = 0; i < KeyOrder.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                string key = KeyOrder[i];
                var r = new Rect(pad.x + col * (cw + gap), pad.yMax - (row + 1) * ch - row * gap, cw, ch);
                var spot = AddHotspot("버튼 " + key, p, r, dialKeySprite, new Color(0.82f, 0.80f, 0.76f), key, 40, () => OnKey(key));
                spot.label.color = new Color(0.15f, 0.1f, 0.1f);
                keySpots.Add(spot);
                if (key == "1") key1Spot = spot;
                if (key == "2") key2Spot = spot;
            }
        }

        void BuildCallPanel(Transform root)
        {
            var panel = WalkUi.CreateImage("통화 창", root, WalkUi.NameBoxSprite, new Color(0.12f, 0.02f, 0.03f, 0.85f));
            if (WalkUi.NameBoxSprite != null) panel.type = Image.Type.Sliced;
            SetAnchors(panel.rectTransform, callArea);

            // 오른쪽 통화 창('위치 말하는 중')에 주소지를 끌어다 놓아도 수화기에 놓은 것과 같게 (nicezzomaru 2026-11-04, 사용자 요청)
            // 그냥 클릭했을 때는 아무 일도 없음 (주소지를 놓았을 때만)
            hotspots.Add(new Hotspot
            {
                id = "통화 창",
                rect = panel.rectTransform,
                image = null,
                onClick = OnCallPanelDrop
            });

            callTitle = WalkUi.CreateText("제목", panel.rectTransform, 34, WalkUi.NameTextColor, TextAnchor.UpperCenter);
            SetAnchors(callTitle.rectTransform, new Rect(0.05f, 0.8f, 0.9f, 0.16f));

            callBody = WalkUi.CreateText("내용", panel.rectTransform, 28, WalkUi.NameBoxSprite != null ? WalkUi.DialogueTextColor : Color.white, TextAnchor.UpperLeft);
            SetAnchors(callBody.rectTransform, new Rect(0.08f, 0.18f, 0.84f, 0.6f));

            var barBg = WalkUi.CreateImage("진행", panel.rectTransform, null, new Color(0f, 0f, 0f, 0.5f));
            SetAnchors(barBg.rectTransform, new Rect(0.08f, 0.07f, 0.84f, 0.06f));
            var fill = WalkUi.CreateImage("채움", barBg.rectTransform, null, new Color(1f, 0.25f, 0.25f, 1f));
            progressFill = fill.rectTransform;
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
            progressBar = barBg.gameObject;
            progressBar.SetActive(false);
        }

        Hotspot AddHotspot(string id, Transform parent, Rect area, Sprite sprite, Color fallback, string label, int fontSize, Action onClick)
        {
            var image = WalkUi.CreateImage(id, parent, sprite, fallback);
            SetAnchors(image.rectTransform, area);
            image.preserveAspect = false;
            var text = WalkUi.CreateText("글자", image.rectTransform, fontSize, Color.white, TextAnchor.MiddleCenter);
            WalkUi.Stretch(text.rectTransform, new Vector2(-8f, -8f));
            text.text = label;

            var spot = new Hotspot
            {
                id = id,
                rect = image.rectTransform,
                image = image,
                label = text,
                baseColor = image.color,
                onClick = onClick
            };
            hotspots.Add(spot);
            return spot;
        }

        static Image Panel(Transform parent, string name, Rect area, Color color)
        {
            var image = WalkUi.CreateImage(name, parent, null, color);
            SetAnchors(image.rectTransform, area);
            return image;
        }

        static void SetAnchors(RectTransform rt, Rect area)
        {
            rt.anchorMin = new Vector2(area.xMin, area.yMin);
            rt.anchorMax = new Vector2(area.xMax, area.yMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void Play(AudioClip clip)
        {
            if (clip == null) return;
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
            audioSource.PlayOneShot(clip);
        }

        static void Say(IReadOnlyList<WalkLine> lines)
        {
            if (lines != null && lines.Count > 0) WalkDialogue.Show(lines);
        }
    }
}
