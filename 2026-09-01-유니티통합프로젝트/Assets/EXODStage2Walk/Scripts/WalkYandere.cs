using UnityEngine;
using UnityEngine.Events;

namespace EXOD.Walk
{
    public enum YandereState
    {
        Away,      // 스프라이트 1: 앞을 보고 있음 (플레이어를 보지 않음)
        Watching,  // 스프라이트 2: 뒤돌아 플레이어 쪽을 봄
        Caught     // 스프라이트 3: 상호작용하다 들킴
    }

    /// <summary>
    /// 산책 중 화면 가운데에 있는 얀데레. 무작위 시간마다 뒤돌아본다.
    /// 스프라이트가 비어 있으면 색으로 상태를 대신 보여준다 (흰색 / 노란색 / 빨간색).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class WalkYandere : MonoBehaviour
    {
        [Header("스프라이트 (비워 두면 색으로 대신 표시)")]
        [SerializeField] Sprite sprite1Away;
        [SerializeField] Sprite sprite2Watching;
        [SerializeField] Sprite sprite3Caught;

        [Header("타이밍 (초, 최소~최대 사이 무작위)")]
        [Tooltip("앞을 보고 있는 시간")]
        [SerializeField] Vector2 awayDuration = new Vector2(2f, 5f);
        [Tooltip("뒤돌아보고 있는 시간")]
        [SerializeField] Vector2 watchingDuration = new Vector2(1f, 2.5f);
        [Tooltip("산책 시작 직후 최소 이 시간 동안은 뒤돌아보지 않음")]
        [SerializeField] float firstTurnMinDelay = 2f;

        [Header("같이 걷기")]
        [Tooltip("비워 두면 같이 걷지 않음")]
        [SerializeField] WalkPlayerMover followTarget;
        [Tooltip("같이 걷지 않을 때, 화면이 움직여도 항상 화면 가운데에 서 있게 한다")]
        [SerializeField] bool stayAtScreenCenter = true;
        [Tooltip("플레이어보다 진행 방향으로 이만큼 앞서 걷는다")]
        [SerializeField] float leadDistance = 2.5f;
        [SerializeField] float walkSpeed = 6f;
        [Tooltip("진행 방향에 맞춰 스프라이트를 좌우 반전")]
        [SerializeField] bool flipWithDirection = true;
        [Tooltip("원본 스프라이트 그림이 오른쪽을 보고 있으면 켜기")]
        [SerializeField] bool spritesFaceRight = true;

        [Header("뒤돌기 전 말풍선 (2SWW)")]
        [Tooltip("1920x1080 화면 크기 그림. 비워 두면 Resources/Walk_2SWW")]
        [SerializeField] Sprite turnWarningSprite;
        [Tooltip("뒤돌기 몇 초 전부터 말풍선을 보여줄지 (다시 앞을 볼 때까지 유지)")]
        [SerializeField] float turnWarningLead = 0.5f;

        [Header("양 끝에서 좌우 반전 (같이 걷지 않을 때)")]
        [Tooltip("화면이 오른쪽 끝에 도착하면 그림을 좌우 반전, 왼쪽 끝에 도착하면 다시 반전")]
        [SerializeField] bool flipAtWalkEnds = true;

        [Header("임시 색 (스프라이트가 없을 때)")]
        [SerializeField] Color awayColor = Color.white;
        [SerializeField] Color watchingColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color caughtColor = new Color(1f, 0.25f, 0.25f);

        [Header("이벤트 (효과음 등 연결용)")]
        public UnityEvent onStartWatching = new UnityEvent();
        public UnityEvent onStopWatching = new UnityEvent();
        public UnityEvent onCaught = new UnityEvent();

        public YandereState State { get; private set; } = YandereState.Away;
        public bool IsWatching => State == YandereState.Watching;
        /// <summary>대화창이 떠 있는 동안 등 잠시 멈춤 (뒤돌아보는 타이머와 걷기 정지)</summary>
        public bool Paused { get; set; }

        SpriteRenderer sr;
        SpriteRenderer warningSr;
        WalkPlayerMover walkMover;
        bool running;
        float timer;
        Camera centerCamera;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            ApplyVisual();
        }

        public void Begin()
        {
            running = true;
            Paused = false;
            SetState(YandereState.Away);
            if (followTarget != null)
            {
                var p = transform.position;
                p.x = FollowTargetX();
                transform.position = p;
            }
            timer = Mathf.Max(firstTurnMinDelay, RandomIn(awayDuration));
        }

        public void Stop()
        {
            running = false;
            SetWarningVisible(false);
        }

        public void SetCaught()
        {
            running = false;
            SetWarningVisible(false);
            SetState(YandereState.Caught);
            onCaught.Invoke();
        }

        void Update()
        {
            if (!running || Paused) return;
            timer -= Time.deltaTime;

            // 뒤돌기 0.5초 전부터 다시 앞을 볼 때까지 말풍선 표시
            SetWarningVisible(State == YandereState.Watching ||
                              (State == YandereState.Away && timer <= turnWarningLead));

            if (timer > 0f) return;

            if (State == YandereState.Away)
            {
                SetState(YandereState.Watching);
                timer = RandomIn(watchingDuration);
                onStartWatching.Invoke();
            }
            else
            {
                SetState(YandereState.Away);
                timer = RandomIn(awayDuration);
                onStopWatching.Invoke();
            }
        }

        void LateUpdate()
        {
            // 화면이 좌우로 움직여도 그녀는 화면 가운데에 서 있다
            if (followTarget == null && stayAtScreenCenter)
            {
                if (centerCamera == null) centerCamera = WalkCameraFit.FindInScene(gameObject.scene);
                var cam = centerCamera;
                if (cam != null)
                {
                    var c = transform.position;
                    c.x = cam.transform.position.x;
                    transform.position = c;
                }
            }

            // 앞을 보고 걷는 중에만 이동하고, 뒤돌아볼 때는 멈춰 선다
            if (followTarget != null && running && !Paused && State == YandereState.Away)
            {
                var p = transform.position;
                p.x = Mathf.MoveTowards(p.x, FollowTargetX(), walkSpeed * Time.deltaTime);
                transform.position = p;
            }
            UpdateFacing();
            UpdateEndFlip();
            UpdateWarningTransform();
        }

        // 같이 걷지 않을 때: 화면이 오른쪽 끝에 도착하면 반전, 왼쪽 끝에 도착하면 원래대로 (세 그림 모두)
        void UpdateEndFlip()
        {
            if (!flipAtWalkEnds || followTarget != null || sr == null) return;
            if (walkMover == null) walkMover = FindMoverInScene();
            if (walkMover == null) return;
            sr.flipX = !walkMover.HeadingRight;
        }

        WalkPlayerMover FindMoverInScene()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var m = root.GetComponentInChildren<WalkPlayerMover>(true);
                if (m != null) return m;
            }
            return null;
        }

        void SetWarningVisible(bool visible)
        {
            if (visible && warningSr == null)
            {
                if (turnWarningSprite == null) turnWarningSprite = Resources.Load<Sprite>("Walk_2SWW");
                if (turnWarningSprite == null) return;
                var go = new GameObject("뒤돌기 전 말풍선 (2SWW)");
                go.transform.SetParent(transform.parent, false);
                warningSr = go.AddComponent<SpriteRenderer>();
                warningSr.sprite = turnWarningSprite;
                if (sr != null)
                {
                    warningSr.sortingLayerID = sr.sortingLayerID;
                    warningSr.sortingOrder = sr.sortingOrder + 1;
                }
            }
            if (warningSr != null && warningSr.gameObject.activeSelf != visible)
            {
                warningSr.gameObject.SetActive(visible);
                if (visible) UpdateWarningTransform();
            }
        }

        // 말풍선 그림은 1920x1080 화면 전체 크기라 화면에 꽉 차게 깔고, 그녀가 반전되면 같이 반전한다
        void UpdateWarningTransform()
        {
            if (warningSr == null || !warningSr.gameObject.activeSelf) return;
            if (centerCamera == null) centerCamera = WalkCameraFit.FindInScene(gameObject.scene);
            var cam = centerCamera;
            if (cam == null || !cam.orthographic || warningSr.sprite == null) return;

            float worldHeight = cam.orthographicSize * 2f;
            float spriteHeight = warningSr.sprite.bounds.size.y;
            float scale = spriteHeight > 0f ? worldHeight / spriteHeight : 1f;
            var t = warningSr.transform;
            t.position = new Vector3(cam.transform.position.x, cam.transform.position.y, transform.position.z);
            t.localScale = new Vector3(scale, scale, 1f);
            warningSr.flipX = sr != null && sr.flipX;
        }

        float FollowTargetX()
        {
            float dir = followTarget.HeadingRight ? 1f : -1f;
            return Mathf.Clamp(followTarget.transform.position.x + dir * leadDistance, followTarget.MinX, followTarget.MaxX);
        }

        void UpdateFacing()
        {
            if (!flipWithDirection || followTarget == null || sr == null) return;
            bool faceRight = State == YandereState.Away
                ? followTarget.HeadingRight                                        // 진행 방향을 봄
                : followTarget.transform.position.x > transform.position.x;       // 플레이어 쪽을 봄
            sr.flipX = spritesFaceRight ? !faceRight : faceRight;
        }

        void SetState(YandereState s)
        {
            State = s;
            ApplyVisual();
        }

        void ApplyVisual()
        {
            if (sr == null) return;
            Sprite target;
            Color placeholder;
            switch (State)
            {
                case YandereState.Watching: target = sprite2Watching; placeholder = watchingColor; break;
                case YandereState.Caught: target = sprite3Caught; placeholder = caughtColor; break;
                default: target = sprite1Away; placeholder = awayColor; break;
            }

            if (target != null)
            {
                sr.sprite = target;
                sr.color = Color.white;
            }
            else
            {
                sr.color = placeholder;
            }
        }

        static float RandomIn(Vector2 range)
        {
            return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        }

        public string StateLabel
        {
            get
            {
                switch (State)
                {
                    case YandereState.Watching: return "보는 중!";
                    case YandereState.Caught: return "들킴";
                    default: return "앞을 보는 중";
                }
            }
        }
    }
}
