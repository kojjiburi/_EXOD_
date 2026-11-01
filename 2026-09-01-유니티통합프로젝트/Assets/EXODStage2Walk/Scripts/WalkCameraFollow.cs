using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책 카메라. 플레이어를 가로로 따라가되 진행 방향을 조금 앞서 보여주고,
    /// 배경 밖은 보이지 않도록 배경 스프라이트 범위 안에서만 움직인다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public class WalkCameraFollow : MonoBehaviour
    {
        [SerializeField] WalkPlayerMover target;
        [Tooltip("카메라가 벗어나지 않을 범위 (배경 스프라이트)")]
        [SerializeField] SpriteRenderer boundsSource;
        [Tooltip("진행 방향으로 앞서 보여줄 거리")]
        [SerializeField] float lookAhead = 3f;
        [SerializeField] float smoothTime = 0.35f;

        Camera cam;
        float velocity;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void Start()
        {
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            velocity = 0f;
            SetX(DesiredX());
        }

        void LateUpdate()
        {
            if (target == null) return;
            SetX(Mathf.SmoothDamp(transform.position.x, DesiredX(), ref velocity, smoothTime));
        }

        float DesiredX()
        {
            float dir = target.HeadingRight ? 1f : -1f;
            return target.transform.position.x + dir * lookAhead;
        }

        void SetX(float x)
        {
            if (boundsSource != null && cam != null && cam.orthographic)
            {
                var b = boundsSource.bounds;
                float half = cam.orthographicSize * cam.aspect;
                float min = b.min.x + half;
                float max = b.max.x - half;
                x = min > max ? b.center.x : Mathf.Clamp(x, min, max);
            }
            var p = transform.position;
            p.x = x;
            transform.position = p;
        }
    }
}
