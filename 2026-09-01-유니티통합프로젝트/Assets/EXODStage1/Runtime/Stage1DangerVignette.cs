using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 위험도 아이콘 색에 맞춰 화면 가장자리가 검게 깜빡임 (사용자 요청 2026-11-03, 전화 부스 뒤돌기 경고와 같은 모양)
/// - 노랑(주황)불: 천천히 어두워졌다 밝아짐
/// - 빨강불: 빠르게 깜빡깜빡
/// - 초록불·등장 중·산책 중·일차 넘김·기절/엔딩 중에는 꺼짐
/// 하트 HUD·행동 요령보다 아래에 그려져서 UI는 가리지 않습니다.
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
public sealed class Stage1DangerVignette : MonoBehaviour
{
    private const int SortingOrder = 590;          // 방 화면 위, 하트 HUD(600) 아래
    private const float SlowPeriod = 2.4f;         // 노랑: 한 번 어두워졌다 밝아지는 데 걸리는 시간
    private const float FastPeriod = 0.45f;        // 빨강
    private static readonly Vector2 SlowAlpha = new Vector2(0.05f, 0.6f);
    private static readonly Vector2 FastAlpha = new Vector2(0.25f, 0.95f);

    private CanvasGroup group;
    private GameObject rootObject;
    private float phase;

    private void OnDestroy()
    {
        if (rootObject != null)
            Destroy(rootObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;
        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null && slider.GetComponent<Stage1DangerVignette>() == null)
            slider.gameObject.AddComponent<Stage1DangerVignette>();
    }

    private void Start()
    {
        Build();
    }

    private void Update()
    {
        if (group == null)
            return;

        DangerLevel.DangerColor color = DangerLevel.IconColor;
        bool off = Stage2HouseLink.IsInStage2 || DayController.IsTransitioning || DangerLevel.IsAppearing
                   || (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy)
                   || (color != DangerLevel.DangerColor.Yellow && color != DangerLevel.DangerColor.Red);

        if (off)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime * 3f);
            phase = 0f;
            return;
        }

        bool red = color == DangerLevel.DangerColor.Red;
        float period = red ? FastPeriod : SlowPeriod;
        Vector2 range = red ? FastAlpha : SlowAlpha;
        phase += Time.unscaledDeltaTime / period;
        // 0 → 1 → 0 로 부드럽게 오르내림
        float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
        float target = Mathf.Lerp(range.x, range.y, wave);
        // 색이 바뀌는 순간 툭 튀지 않게 살짝 따라감
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * (red ? 8f : 2f));
    }

    private void Build()
    {
        // SliderManager는 방 화면 캔버스 안에 있어서, 그 밑에 붙이면 방 그림에 가려짐 → 씬 맨 위에 따로 만듦
        GameObject root = new GameObject("위험도 가장자리 어둡게", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        rootObject = root;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject image = new GameObject("비네트", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        image.transform.SetParent(root.transform, false);
        RectTransform rect = image.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image img = image.GetComponent<Image>();
        img.sprite = CreateVignette();
        img.raycastTarget = false;
    }

    // 가운데는 투명, 가장자리로 갈수록 검게 (전화 부스 경고와 같은 모양)
    private static Sprite CreateVignette()
    {
        const int w = 256, h = 144;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x / (w - 1f) - 0.5f) * 2f;
                float dy = (y / (h - 1f) - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx * 0.85f + dy * dy * 0.85f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.15f, d)) * 0.95f;
                px[y * w + x] = new Color(0f, 0f, 0f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }
}
