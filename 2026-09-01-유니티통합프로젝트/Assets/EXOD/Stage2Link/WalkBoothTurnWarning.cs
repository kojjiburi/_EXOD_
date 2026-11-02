using EXOD.Walk;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 전화 부스 안에 있을 때 그녀가 뒤돌기 직전 경고 (사용자 요청 2026-11-03)
/// - 뒤돌기 0.3~0.7초 전(밖에서 말풍선이 뜨는 때와 같은 기준, WalkYandere.IsTurnWarningShown)에
///   두근두근 효과음 + 화면 가장자리가 검게 깜빡깜빡.
/// - 부스 안에서는 바깥(그녀)이 안 보이므로 이걸로 알아차리게 함.
/// 산책 씬(Stage2_Walk)이 열릴 때 자동으로 붙습니다.
/// </summary>
public sealed class WalkBoothTurnWarning : MonoBehaviour
{
    private const int SortingOrder = 900;   // 부스 안 화면 위, 대화창(DialogueOnTop) 아래

    private WalkYandere yandere;
    private PhoneBoothInterior interior;
    private CanvasGroup group;
    private bool warning;
    private float blinkTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != Stage2HouseLink.WalkSceneName || FindFirstObjectByType<WalkBoothTurnWarning>() != null)
            return;

        GameObject go = new GameObject("부스 안 뒤돌기 경고");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<WalkBoothTurnWarning>();
    }

    private void Start()
    {
        Build();
    }

    private void OnDestroy()
    {
        if (warning)
            EXODSound.StopHeartbeat(0.2f);
    }

    private void Update()
    {
        if (yandere == null)
            yandere = FindFirstObjectByType<WalkYandere>();
        if (interior == null)
            interior = FindFirstObjectByType<PhoneBoothInterior>(FindObjectsInactive.Include);

        bool inBooth = interior != null && interior.IsOpen;
        bool aboutToTurn = inBooth && yandere != null && !yandere.Paused && yandere.IsTurnWarningShown;

        if (aboutToTurn && !warning)
        {
            warning = true;
            blinkTime = 0f;
            EXODSound.PlayHeartbeat(0.9f, 1.7f, 0.05f);
        }
        else if (!aboutToTurn && warning)
        {
            warning = false;
            // 그대로 뒤돌아보는 중이면 심장 소리는 행동 요령의 '보고 있다' 경고가 이어받음
            if (yandere == null || !yandere.IsWatching)
                EXODSound.StopHeartbeat(0.3f);
        }

        if (warning)
        {
            blinkTime += Time.unscaledDeltaTime;
            // 깜빡깜빡: 0.15초마다 어두워졌다 밝아짐
            float pulse = Mathf.Abs(Mathf.Sin(blinkTime * Mathf.PI / 0.15f));
            group.alpha = Mathf.Lerp(0.35f, 1f, pulse);
        }
        else
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime * 6f);
        }
    }

    private void Build()
    {
        GameObject root = new GameObject("가장자리 어둡게", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
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

    // 가운데는 투명, 가장자리로 갈수록 검게
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
