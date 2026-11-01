using UnityEngine;
using UnityEngine.Events;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책 중 좌우 이동과 왕복 횟수 계산. 기본은 자동 이동(autoMove), 끄면 A/D·←/→로 움직인다.
    ///
    /// 화면 이동 모드(moveScreen, 기본 켜짐):
    ///  - 플레이어는 보이지 않고, 화면(카메라)이 밤길 배경을 따라 좌우로 움직인다.
    ///  - 화면이 배경 왼쪽 끝(집 대문 쪽)에서 시작 → 오른쪽 끝 도착(1구간) → 왼쪽 끝 도착(2구간) = 1왕복.
    ///  - 이 오브젝트는 보이지 않는 기준점으로 남아 화면 가운데를 따라다닌다.
    ///
    /// moveScreen을 끄면 예전처럼 플레이어가 고정 화면 안에서 걷는다.
    /// </summary>
    public class WalkPlayerMover : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;
        [Tooltip("켜면 키를 누르지 않아도 끝에서 끝까지 저절로 왕복한다 (대화창이 떠 있는 동안은 멈춤)")]
        [SerializeField] bool autoMove = true;
        [Tooltip("[통합] 켜면 자동 이동 속도를 2스테이지 시간(DayClock, 1분)에 맞춰서, 시간이 끝날 즈음 정해진 왕복을 마치게 합니다. (moveSpeed 무시)")]
        [SerializeField] bool matchDayClockDuration = true;
        [Tooltip("시간이 끝나기 몇 초 전에 왕복을 마칠지")]
        [SerializeField] float finishSecondsEarly = 0.5f;
        [Tooltip("[통합] 자동 이동일 때 양 끝에 도착하면 이만큼(초) 멈췄다가 돌아간다")]
        [SerializeField] float endPauseSeconds = 2f;

        [Header("화면 이동 모드")]
        [Tooltip("켜면 플레이어를 숨기고 화면을 좌우로 움직인다 (화면 왕복)")]
        [SerializeField] bool moveScreen = true;
        [Tooltip("움직일 카메라 (비워 두면 같은 씬의 산책 카메라)")]
        [SerializeField] Camera screenCamera;

        [Header("좌우 끝 (플레이어 이동 모드 전용. 비워 두면 카메라 화면 끝 기준)")]
        [SerializeField] Transform leftEdge;
        [SerializeField] Transform rightEdge;
        [SerializeField] float screenEdgeMargin = 0.6f;
        [SerializeField] float arriveTolerance = 0.05f;

        [Header("보는 방향 뒤집기")]
        [SerializeField] SpriteRenderer spriteToFlip;
        [SerializeField] bool spriteFacesRight = true;

        [Tooltip("끝에 도착할 때마다 실행 (인자: 지금까지 완료한 구간 수)")]
        public UnityEvent<int> onLegCompleted = new UnityEvent<int>();

        public int LegsCompleted { get; private set; }
        public bool HeadingRight { get; private set; } = true;
        public bool CanMove { get; set; }
        public float MinX => minX;
        public float MaxX => maxX;
        public bool MovesScreen => moveScreen;

        float pauseTimer;
        float minX = -8f;
        float maxX = 8f;
        WalkCameraFit fit;

        void Awake()
        {
            if (moveScreen) SetupScreenMode();
        }

        void SetupScreenMode()
        {
            if (screenCamera == null) screenCamera = WalkCameraFit.FindInScene(gameObject.scene);
            if (screenCamera != null)
            {
                fit = screenCamera.GetComponent<WalkCameraFit>();
                if (fit != null) fit.PanMode = true;
            }

            // 플레이어 모습과 이름표를 숨긴다 (기준점 역할만 한다)
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var label in GetComponentsInChildren<WorldLabel>(true)) label.enabled = false;
        }

        /// <summary>왼쪽 끝으로 옮기고 왕복 기록을 초기화</summary>
        public void ResetToStart()
        {
            ComputeBounds();
            SetX(minX);
            LegsCompleted = 0;
            HeadingRight = true;
            pauseTimer = 0f;
        }

        void ComputeBounds()
        {
            if (moveScreen && screenCamera != null)
            {
                if (fit != null)
                {
                    fit.Fit();
                    minX = fit.PanMinX;
                    maxX = fit.PanMaxX;
                }
                else
                {
                    minX = maxX = screenCamera.transform.position.x;
                }
            }
            else if (leftEdge != null && rightEdge != null)
            {
                minX = leftEdge.position.x;
                maxX = rightEdge.position.x;
            }
            else
            {
                var cam = Camera.main;
                if (cam != null && cam.orthographic)
                {
                    float half = cam.orthographicSize * cam.aspect;
                    minX = cam.transform.position.x - half + screenEdgeMargin;
                    maxX = cam.transform.position.x + half - screenEdgeMargin;
                }
            }
            if (minX > maxX) { float t = minX; minX = maxX; maxX = t; }
        }

        float CurrentX => moveScreen && screenCamera != null ? screenCamera.transform.position.x : transform.position.x;

        void SetX(float x)
        {
            if (moveScreen && screenCamera != null)
            {
                var c = screenCamera.transform.position;
                c.x = x;
                screenCamera.transform.position = c;
            }
            var p = transform.position;
            p.x = x;
            transform.position = p;
        }

        void Update()
        {
            // 화면 크기가 바뀌면 끝 위치도 달라진다
            if (moveScreen && fit != null && (fit.PanMinX != minX || fit.PanMaxX != maxX))
            {
                minX = fit.PanMinX;
                maxX = fit.PanMaxX;
                SetX(Mathf.Clamp(CurrentX, minX, maxX));
            }

            if (!CanMove) return;

            // 끝에 도착하면 잠깐 멈춘다
            if (pauseTimer > 0f)
            {
                pauseTimer -= Time.deltaTime;
                return;
            }

            float h = autoMove ? (HeadingRight ? 1f : -1f) : WalkInput.Horizontal();
            if (h != 0f)
            {
                SetX(Mathf.Clamp(CurrentX + h * CurrentSpeed() * Time.deltaTime, minX, maxX));
                if (spriteToFlip != null) spriteToFlip.flipX = spriteFacesRight ? h < 0f : h > 0f;
            }

            float x = CurrentX;
            if (HeadingRight && x >= maxX - arriveTolerance)
            {
                LegsCompleted++;
                HeadingRight = false;
                if (autoMove) pauseTimer = endPauseSeconds;
                onLegCompleted.Invoke(LegsCompleted);
            }
            else if (!HeadingRight && x <= minX + arriveTolerance)
            {
                LegsCompleted++;
                HeadingRight = true;
                if (autoMove) pauseTimer = endPauseSeconds;
                onLegCompleted.Invoke(LegsCompleted);
            }
        }

        // 자동 이동일 때 2스테이지 시간(1분) 안에 정해진 왕복(기본 2번 = 4구간)을 마치는 속도
        float CurrentSpeed()
        {
            if (!autoMove || !matchDayClockDuration || DayClock.Instance == null)
                return moveSpeed;

            int legs = WalkGameManager.Current != null ? WalkGameManager.Current.RoundTripsRequired * 2 : 4;
            // 마지막 도착 전까지 끝에서 멈추는 시간(구간 수 - 1번)을 빼고 계산
            float pauses = Mathf.Max(0, legs - 1) * Mathf.Max(0f, endPauseSeconds);
            float seconds = Mathf.Max(1f, DayClock.Instance.DayLength - finishSecondsEarly - pauses);
            float distance = Mathf.Max(0.01f, maxX - minX) * legs;
            return distance / seconds;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            float y = transform.position.y;
            Gizmos.DrawLine(new Vector3(minX, y - 1f), new Vector3(minX, y + 1f));
            Gizmos.DrawLine(new Vector3(maxX, y - 1f), new Vector3(maxX, y + 1f));
        }
    }
}
