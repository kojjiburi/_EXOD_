using EXOD.Walk;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 산책 중 여자친구가 걷는 것처럼 위아래로 통통 움직이게 합니다. (사용자 요청 2026-11-02)
/// - 기본 상태(앞을 보고 걷는 중, Away)이고 실제로 걷고 있을 때만 움직입니다.
/// - 뒤돌아볼 때(Watching)·들켰을 때(Caught)·멈춰 있을 때는 제자리로 부드럽게 돌아옵니다.
/// - WalkYandere는 x 위치만 바꾸므로, 여기서는 y에 더한 만큼을 매 프레임 되돌렸다가 다시 더합니다.
/// 산책 씬(Stage2_Walk)이 열릴 때 WalkYandere에 자동으로 붙습니다.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class WalkYandereBob : MonoBehaviour
{
    private const float StepsPerSecond = 2.2f;    // 1초에 걸음 수
    private const float HeightRatio = 0.012f;     // 그림 높이의 1.2%만큼 오르내림

    private WalkYandere yandere;
    private SpriteRenderer spriteRenderer;
    private float appliedOffset;
    private float weight;
    private float phase;
    private float lastX;
    private float lastCameraX;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != Stage2HouseLink.WalkSceneName)
            return;

        foreach (WalkYandere y in FindObjectsByType<WalkYandere>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (y.GetComponent<WalkYandereBob>() == null)
                y.gameObject.AddComponent<WalkYandereBob>();
        }
    }

    private void Awake()
    {
        yandere = GetComponent<WalkYandere>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        lastX = transform.position.x;
        lastCameraX = Camera.main != null ? Camera.main.transform.position.x : 0f;
    }

    private void LateUpdate()
    {
        // 지난 프레임에 더한 만큼 먼저 되돌립니다.
        Vector3 p = transform.position;
        p.y -= appliedOffset;

        // 걷는 중인지: 그녀나 화면(카메라)이 움직이고 있으면 걷는 중
        float cameraX = Camera.main != null ? Camera.main.transform.position.x : lastCameraX;
        bool moving = Mathf.Abs(p.x - lastX) > 0.0005f || Mathf.Abs(cameraX - lastCameraX) > 0.0005f;
        lastX = p.x;
        lastCameraX = cameraX;

        bool walking = yandere != null && yandere.State == YandereState.Away && !yandere.Paused && moving
            && Time.deltaTime > 0f;
        weight = Mathf.MoveTowards(weight, walking ? 1f : 0f, Time.deltaTime * 5f);
        if (weight > 0f)
            phase += Time.deltaTime * StepsPerSecond;

        float height = spriteRenderer != null && spriteRenderer.sprite != null ? spriteRenderer.bounds.size.y : 5f;
        // 발을 디딜 때마다 한 번씩 튀어 오르는 모양 (|sin|)
        appliedOffset = Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * height * HeightRatio * weight;
        p.y += appliedOffset;
        transform.position = p;
    }

    private void OnDisable()
    {
        Vector3 p = transform.position;
        p.y -= appliedOffset;
        transform.position = p;
        appliedOffset = 0f;
    }
}
