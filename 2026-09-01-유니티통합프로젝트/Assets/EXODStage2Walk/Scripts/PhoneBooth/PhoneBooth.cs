using System.Collections.Generic;
using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책 길의 빨간 전화 부스 (바깥 모습 + 기믹 설정).
    ///
    /// 진행:
    ///  0. 처음에는 잠겨 있다. 다른 기믹에서 얻은 '부스 열쇠'를 퀵슬롯에서 골라 부스를 클릭하면 열린다.
    ///  1. 열린 부스를 클릭하면 부스 안 클로즈업(PhoneBoothInterior)이 열린다. 테이프·버튼 [1]·[2]로 전화를 고친다.
    ///  2. 가로등 4개의 번호판(PhoneBoothLamp)을 모두 보면 '주소지'를 얻는다.
    ///  3. 112를 누르고, 주소지를 골라 수화기를 클릭해 위치를 끝까지 말하면 신고 성공.
    ///  4. 하루가 끝나면(다음 날 아침) 초인종이 울리고 경찰이 도착 → 탈출 엔딩 (PhoneBoothEnding).
    ///
    /// 클릭은 모두 산책 규칙을 따른다: 그녀가 뒤돌아보고 있을 때 클릭하면 들킨다.
    ///
    /// 모습: 자식 오브젝트 '잠긴 버전' / '열린 버전'의 SpriteRenderer에 이미지를 넣으면 된다.
    /// 이미지를 넣으면 그 아래의 '임시 모양'은 플레이할 때 자동으로 숨겨진다(지워도 된다).
    /// </summary>
    [RequireComponent(typeof(WalkInteractable))]
    public class PhoneBooth : MonoBehaviour
    {
        public static PhoneBooth Current { get; private set; }

        const string Her = "여자친구";

        [Header("부스 모습 (각 오브젝트의 SpriteRenderer에 이미지 넣기)")]
        [SerializeField] GameObject lockedVersion;
        [SerializeField] GameObject openVersion;
        [Tooltip("켜 두면 지금 보이는 버전에 이미지가 있을 때 클릭 판정(BoxCollider2D)을 이미지 크기에 맞춘다")]
        [SerializeField] bool fitColliderToImage = true;

        [Header("아이템 (팀 공용 ItemData)")]
        [SerializeField] ItemData boothKey;
        [SerializeField] ItemData insulatingTape;
        [SerializeField] ItemData dialButton1;
        [SerializeField] ItemData dialButton2;
        [SerializeField] ItemData addressNote;

        [Header("가로등")]
        [Tooltip("이만큼 서로 다른 가로등 번호판을 보면 '주소지'를 얻는다")]
        [SerializeField] int lampsRequired = 4;

        [Header("부스 안")]
        [SerializeField] PhoneBoothInterior interior;

        [Header("단독 프로젝트 테스트")]
        [Tooltip("스테이지2 단독 테스트(테스트 집의 현관문으로 산책한 경우)에서만, 이 일차부터 부스 열쇠를 퀵슬롯에 넣어 준다. 0이면 주지 않는다.\n통합 프로젝트(1스테이지 위에 산책 씬이 열리는 경우)에서는 동작하지 않는다.")]
        [SerializeField] int standaloneTestKeyFromDay = 2;

        [Header("대사 - 부스 문")]
        [SerializeField] WalkLine[] lockedLines =
        {
            new WalkLine("", "(빨간 전화 부스다. 문이 잠겨 있다.)")
        };
        [Tooltip("열쇠를 가지고 있지만 퀵슬롯에서 고르지 않았을 때")]
        [SerializeField] WalkLine[] lockedHasKeyLines =
        {
            new WalkLine("", "(문이 잠겨 있다. 퀵슬롯의 열쇠를 부스에 끌어다 놓아 보자.)")
        };
        [SerializeField] WalkLine[] unlockLines =
        {
            new WalkLine("", "(열쇠를 꽂고 돌리자 철컥, 부스 문이 열렸다.)")
        };

        [Header("대사 - 가로등 / 주소지")]
        [SerializeField] WalkLine[] addressObtainedLines =
        {
            new WalkLine("", "(가로등 번호판을 모두 확인했다. 지금 있는 곳의 주소를 알아냈다.)"),
            new WalkLine("", "[주소지]를 얻었다.")
        };
        [SerializeField] WalkLine[] addressSlotsFullLines =
        {
            new WalkLine("", "(주소는 알아냈지만 아이템 칸이 꽉 차서 적어 둘 수가 없다.)")
        };

        [Header("대사 - 산책 끝")]
        [Tooltip("신고를 마친 날, 그녀가 아무것도 모른 채 하는 귀가 대사 (들킨 경우 제외)")]
        [SerializeField] WalkLine[] afterReportReturnLines =
        {
            new WalkLine(Her, "오늘따라 얼굴이 밝네? 나랑 산책하는 게 그렇게 좋았어?"),
            new WalkLine(Her, "내일도 같이 나오자. 내일도, 그다음 날도… 계속."),
        };
        [Tooltip("통화 중에 산책이 끝났을 때 (맨 앞에 붙는다)")]
        [SerializeField] WalkLine[] callDroppedLines =
        {
            new WalkLine("", "(뚝— 말을 다 하기도 전에 전화가 끊겼다. 내일 다시 걸어야 한다.)")
        };
        [Tooltip("통화 중에 들켰을 때 (그녀의 첫 대사 다음에 붙는다)")]
        [SerializeField] WalkLine[] caughtOnCallLines =
        {
            new WalkLine("", "(그녀가 말없이 수화기를 빼앗더니, 선을 다시 뜯어 버렸다.)")
        };

        [Header("엔딩 (신고 성공 뒤 하루가 끝나면)")]
        [SerializeField] WalkLine[] endingLines =
        {
            new WalkLine("", "(딩동— 초인종이 울렸다.)"),
            new WalkLine(Her, "…이 시간에 누구지?"),
            new WalkLine("경찰", "경찰입니다! 신고 받고 출동했습니다. 문 열어 주세요!"),
            new WalkLine(Her, "경찰…? 왜 경찰이…"),
            new WalkLine(Her, "…너야? 네가 부른 거야?"),
            new WalkLine("", "(문이 열리고 경찰이 들어왔다. 드디어 이 집을 나갈 수 있다.)")
        };
        [SerializeField] string endingTitle = "탈출 엔딩";
        [SerializeField] string endingSubtitle = "경찰에 신고해 집을 빠져나왔다.";
        [Tooltip("엔딩 화면 뒤에 불러올 씬 (비워 두면 엔딩 화면에서 멈춤). Build Profiles 씬 목록에 있어야 한다.")]
        [SerializeField] string endingSceneName = "";

        public ItemData BoothKey => boothKey;
        public ItemData InsulatingTape => insulatingTape;
        public ItemData DialButton1 => dialButton1;
        public ItemData DialButton2 => dialButton2;
        public ItemData AddressNote => addressNote;
        public int LampsRequired => lampsRequired;

        static bool testKeyGiven;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            testKeyGiven = false;
        }

        void Awake()
        {
            Current = this;
            GetComponent<WalkInteractable>().onInteract.AddListener(OnClicked);
            if (interior == null) interior = GetComponentInChildren<PhoneBoothInterior>(true);
            if (interior != null) interior.Booth = this;
            HidePlaceholderIfImage(lockedVersion);
            HidePlaceholderIfImage(openVersion);
            RefreshLook();
        }

        void OnEnable()
        {
            WalkGameManager.FinishingLines += OnWalkFinishing;
        }

        void OnDisable()
        {
            WalkGameManager.FinishingLines -= OnWalkFinishing;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Start()
        {
            GiveTestKeyIfNeeded();
            // 신고는 했는데 아직 엔딩 전이면 (산책 씬을 다시 열었을 때) 엔딩 대기를 이어 간다
            if (PhoneBoothProgress.Reported && !PhoneBoothProgress.EndingShown) ArmEnding();
        }

        void GiveTestKeyIfNeeded()
        {
            if (WalkSession.Additive || standaloneTestKeyFromDay <= 0 || testKeyGiven) return;
            if (boothKey == null || PhoneBoothProgress.BoothUnlocked || WalkItems.Has(boothKey)) return;
            var clock = DayClock.Ensure();
            if (clock.CurrentDay < standaloneTestKeyFromDay) return;

            if (WalkItems.Give(boothKey))
            {
                testKeyGiven = true;
                Debug.Log($"[PhoneBooth] (단독 테스트) DAY {clock.CurrentDay}: 부스 열쇠를 퀵슬롯에 넣었습니다. 다른 기믹 보상 대신입니다.");
            }
        }

        // ---------------- 부스 문 ----------------
        void OnClicked()
        {
            if (!PhoneBoothProgress.BoothUnlocked)
            {
                if (boothKey == null)
                {
                    Debug.LogWarning("[PhoneBooth] 부스 열쇠(ItemData)가 비어 있습니다.", this);
                    WalkDialogue.Show(lockedLines);
                    return;
                }

                if (WalkItems.IsUsing(boothKey))
                {
                    PhoneBoothProgress.BoothUnlocked = true;
                    WalkItems.UseUp(boothKey);
                    RefreshLook();
                    WalkDialogue.Show(unlockLines);
                    Debug.Log("[PhoneBooth] 부스 문 열림");
                }
                else
                {
                    WalkDialogue.Show(WalkItems.Has(boothKey) ? lockedHasKeyLines : lockedLines);
                }
                return;
            }

            if (interior != null) interior.Open();
            else Debug.LogWarning("[PhoneBooth] PhoneBoothInterior(부스 안)가 연결되지 않았습니다.", this);
        }

        /// <summary>잠김 / 열림 모습을 진행 상황에 맞춘다</summary>
        public void RefreshLook()
        {
            bool open = PhoneBoothProgress.BoothUnlocked;
            if (lockedVersion != null) lockedVersion.SetActive(!open);
            if (openVersion != null) openVersion.SetActive(open);
            if (fitColliderToImage) FitCollider(open ? openVersion : lockedVersion);
        }

        void FitCollider(GameObject version)
        {
            if (version == null) return;
            var sr = version.GetComponent<SpriteRenderer>();
            var box = GetComponent<BoxCollider2D>();
            if (sr == null || sr.sprite == null || box == null) return;

            Bounds b = sr.bounds;
            Vector3 scale = transform.lossyScale;
            box.offset = transform.InverseTransformPoint(b.center);
            box.size = new Vector2(b.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)), b.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
        }

        static void HidePlaceholderIfImage(GameObject version)
        {
            if (version == null) return;
            var sr = version.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;
            foreach (Transform child in version.transform)
                if (child.name.StartsWith("임시")) child.gameObject.SetActive(false);
        }

        // ---------------- 가로등 / 주소지 ----------------
        /// <summary>가로등 번호판을 봤다. 주소지를 이번에 얻었으면 그 대사를 돌려준다.</summary>
        public List<WalkLine> OnLampChecked(int lampIndex)
        {
            var extra = new List<WalkLine>();
            PhoneBoothProgress.LampsChecked.Add(lampIndex);
            Debug.Log($"[PhoneBooth] 가로등 {lampIndex} 확인 ({PhoneBoothProgress.LampsChecked.Count}/{lampsRequired})");

            if (PhoneBoothProgress.AddressObtained || PhoneBoothProgress.LampsChecked.Count < lampsRequired) return extra;
            if (addressNote == null)
            {
                Debug.LogWarning("[PhoneBooth] 주소지(ItemData)가 비어 있습니다.", this);
                return extra;
            }

            if (WalkItems.Give(addressNote))
            {
                PhoneBoothProgress.AddressObtained = true;
                extra.AddRange(addressObtainedLines);
                Debug.Log("[PhoneBooth] 주소지 획득");
            }
            else
            {
                extra.AddRange(addressSlotsFullLines);
            }
            return extra;
        }

        // ---------------- 산책 끝 ----------------
        void OnWalkFinishing(WalkResult result, List<WalkLine> lines)
        {
            bool onCall = interior != null && interior.IsOnCall;
            bool reportedToday = interior != null && interior.ReportedThisWalk;

            if (onCall)
            {
                interior.DropCall();
                if (result == WalkResult.Caught)
                {
                    // 통화 중에 들킴 → 선이 다시 끊기고, 벤치에 테이프가 한 번 더 생긴다
                    PhoneBoothProgress.CordFixed = false;
                    if (insulatingTape != null && !WalkItems.Has(insulatingTape)) WalkItemPickup.Forget(insulatingTape);
                    lines.InsertRange(Mathf.Min(1, lines.Count), caughtOnCallLines);
                    Debug.Log("[PhoneBooth] 통화 중에 들킴: 수화기 선이 다시 끊겼습니다. 다음 산책에서 벤치에 테이프가 다시 생깁니다.");
                }
                else
                {
                    lines.InsertRange(0, callDroppedLines);
                    Debug.Log("[PhoneBooth] 신고를 마치기 전에 산책이 끝나 전화가 끊겼습니다.");
                }
                return;
            }

            if (reportedToday && result != WalkResult.Caught && afterReportReturnLines != null && afterReportReturnLines.Length > 0)
            {
                lines.Clear();
                lines.AddRange(afterReportReturnLines);
            }
        }

        /// <summary>신고를 끝까지 마쳤을 때 (PhoneBoothInterior가 호출)</summary>
        internal void OnReported()
        {
            ArmEnding();
        }

        void ArmEnding()
        {
            PhoneBoothEnding.Arm(endingLines, endingTitle, endingSubtitle, endingSceneName);
        }

        // ---------------- 테스트 ----------------
        /// <summary>테스트용: 열쇠·테이프·버튼 [1]·[2]를 퀵슬롯에 넣는다 (이미 가진 것은 건너뜀)</summary>
        [ContextMenu("테스트: 기믹 아이템 전부 받기 (플레이 중)")]
        public void GiveAllItemsForTest()
        {
            if (!Application.isPlaying) { Debug.Log("[PhoneBooth] 플레이 중에만 쓸 수 있습니다."); return; }
            foreach (var item in new[] { boothKey, insulatingTape, dialButton1, dialButton2 })
            {
                if (item == null || WalkItems.Has(item)) continue;
                if (item == boothKey && PhoneBoothProgress.BoothUnlocked) continue;
                if (item == insulatingTape && PhoneBoothProgress.CordFixed) continue;
                if (item == dialButton1 && PhoneBoothProgress.Button1Installed) continue;
                if (item == dialButton2 && PhoneBoothProgress.Button2Installed) continue;
                WalkItems.Give(item);
            }
            Debug.Log("[PhoneBooth] (테스트) 기믹 아이템을 퀵슬롯에 넣었습니다. " + PhoneBoothProgress.Summary);
        }

        [ContextMenu("테스트: 진행 상황 초기화 (플레이 중)")]
        public void ResetProgressForTest()
        {
            PhoneBoothProgress.ResetAll();
            RefreshLook();
            Debug.Log("[PhoneBooth] (테스트) 전화 부스 진행 상황을 처음으로 되돌렸습니다.");
        }

        [ContextMenu("테스트: 진행 상황 보기")]
        public void LogProgress()
        {
            Debug.Log("[PhoneBooth] " + PhoneBoothProgress.Summary);
        }
    }
}
