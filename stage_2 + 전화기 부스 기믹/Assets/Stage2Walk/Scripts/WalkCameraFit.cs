using UnityEngine;
using UnityEngine.SceneManagement;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책 카메라 크기 맞추기.
    ///  - 화면 이동 모드(PanMode): 배경 세로가 화면 높이에 꽉 차게 확대하고, 가로 위치는 WalkPlayerMover가 움직인다.
    ///    PanMinX ~ PanMaxX는 배경 밖이 보이지 않는 카메라 가운데 위치 범위다.
    ///  - 고정 모드: 배경 전체(좌우 끝까지)가 한 화면에 들어오도록 맞춘다. 남는 위아래는 카메라 배경색(검정)으로 보인다.
    /// 화면 비율이 바뀌어도 다시 맞춘다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class WalkCameraFit : MonoBehaviour
    {
        [SerializeField] SpriteRenderer fitTarget;
        [Tooltip("배경 바깥으로 남길 여백 (유닛, 고정 모드 전용)")]
        [SerializeField] float padding = 0f;

        /// <summary>true면 배경 높이에 맞춰 확대하고 가로로 움직일 수 있게 한다 (WalkPlayerMover가 켠다)</summary>
        public bool PanMode { get; set; }
        public float PanMinX { get; private set; }
        public float PanMaxX { get; private set; }

        Camera cam;
        int lastW, lastH;

        /// <summary>
        /// 이 씬 안의 산책 카메라를 찾는다. 통합 프로젝트에서는 집 씬 위에 산책 씬이 겹쳐 열려
        /// Main Camera가 두 개일 수 있어서, Camera.main 대신 같은 씬의 카메라를 쓴다.
        /// </summary>
        public static Camera FindInScene(Scene scene)
        {
            // 씬이 아직 다 열리지 않은 Awake 중에도 찾을 수 있게, 열린 WalkCameraFit 중 같은 씬 것을 고른다
            foreach (var fit in FindObjectsByType<WalkCameraFit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fit != null && fit.gameObject.scene == scene) return fit.GetComponent<Camera>();
            }
            return Camera.main;
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            Fit();
        }

        void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH) Fit();
        }

        public void Fit()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null || fitTarget == null) return;
            lastW = Screen.width;
            lastH = Screen.height;

            var b = fitTarget.bounds;
            cam.orthographic = true;
            var p = transform.position;

            if (PanMode)
            {
                // 세로를 꽉 채운다. 화면이 배경보다 넓으면 가로가 꽉 차게 맞춘다.
                float sizeForHeight = b.size.y * 0.5f;
                float sizeForWidth = b.size.x * 0.5f / Mathf.Max(0.01f, cam.aspect);
                cam.orthographicSize = Mathf.Min(sizeForHeight, sizeForWidth);

                float half = cam.orthographicSize * cam.aspect;
                PanMinX = b.min.x + half;
                PanMaxX = b.max.x - half;
                if (PanMinX > PanMaxX) PanMinX = PanMaxX = b.center.x;

                p.x = Mathf.Clamp(p.x, PanMinX, PanMaxX);
                p.y = b.center.y;
            }
            else
            {
                float sizeForWidth = (b.size.x * 0.5f + padding) / Mathf.Max(0.01f, cam.aspect);
                float sizeForHeight = b.size.y * 0.5f + padding;
                cam.orthographicSize = Mathf.Max(sizeForWidth, sizeForHeight);

                p.x = b.center.x;
                p.y = b.center.y;
                PanMinX = PanMaxX = b.center.x;
            }

            transform.position = p;
        }
    }
}
