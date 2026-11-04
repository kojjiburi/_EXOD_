using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// [이지 모드] 씬이 열릴 때 바꾸는 것들 (GameDifficulty 설명 참고)
/// - 산책 씬: 수화기 선은 처음부터 테이프로 이어져 있음, 가로등 번호판 1개만 확인하면 주소지,
///            그녀가 앞을 보는 시간 2배(덜 자주 뒤돌아봄)
/// - 집 씬: 화장실 금고(잠금판)가 아주 반짝반짝 (금고가 열리면 사라짐)
/// 하드 모드에서는 아무것도 하지 않습니다.
/// </summary>
public static class Stage1EasyModeHelpers
{
    private const string SafeBlockName = "RRSafe_block";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!GameDifficulty.IsEasy)
            return;

        if (scene.name == Stage2HouseLink.WalkSceneName)
        {
            SetupWalk();
            return;
        }

        SliderManager slider = Object.FindFirstObjectByType<SliderManager>();
        if (slider != null && slider.GetComponent<EasySafeSparkleFinder>() == null)
            slider.gameObject.AddComponent<EasySafeSparkleFinder>();
    }

    private static void SetupWalk()
    {
        // 수화기 선은 이미 테이프로 감겨 있음 (테이프는 산책 길에 안 나옴: Stage2HouseLink)
        EXOD.Walk.PhoneBoothProgress.CordFixed = true;
        // 다이얼 버튼 [2]도 이미 끼워져 있음 ([1]만 산책 길에서 찾기, 사용자 요청 2026-11-03)
        EXOD.Walk.PhoneBoothProgress.Button2Installed = true;

        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (EXOD.Walk.PhoneBooth booth in Object.FindObjectsByType<EXOD.Walk.PhoneBooth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            typeof(EXOD.Walk.PhoneBooth).GetField("lampsRequired", flags)?.SetValue(booth, 1);

        foreach (EXOD.Walk.WalkYandere y in Object.FindObjectsByType<EXOD.Walk.WalkYandere>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            FieldInfo away = typeof(EXOD.Walk.WalkYandere).GetField("awayDuration", flags);
            if (away != null && away.GetValue(y) is Vector2 range)
                away.SetValue(y, range * 2f);
        }
    }

    // 화장실 화면이 만들어진 뒤 금고 잠금판을 찾아 반짝임을 붙임
    private sealed class EasySafeSparkleFinder : MonoBehaviour
    {
        private float nextTry;

        private void Update()
        {
            if (Time.unscaledTime < nextTry)
                return;
            nextTry = Time.unscaledTime + 0.5f;

            GameObject block = GameObject.Find(SafeBlockName);
            if (block == null || block.GetComponent<UiSparkleStars>() != null)
                return;
            UiSparkleStars stars = block.AddComponent<UiSparkleStars>();
            stars.Count = 9;
            stars.MinSize = 22f;
            stars.MaxSize = 78f;
            stars.Period = 0.9f;
            stars.SortingOrder = 615;   // 금고는 행동 요령 띠(610) 밑에 있어서 그 위로
        }
    }
}

/// <summary>
/// UI 그림 위에 작은 별이 반짝이게 함 (붙인 그림이 없어지면 함께 사라짐). 클릭은 막지 않음.
/// 하트 HUD(600)보다 위에 그려짐.
/// </summary>
public sealed class UiSparkleStars : MonoBehaviour
{
    public int Count = 5;
    public float MinSize = 16f;
    public float MaxSize = 58f;
    public float Period = 1.3f;
    public int SortingOrder = 605;   // 하트 HUD(600) 위

    private static Sprite starSprite;
    private RectTransform[] stars;
    private Image[] images;
    private float[] phase;

    private void Start()
    {
        stars = new RectTransform[Count];
        images = new Image[Count];
        phase = new float[Count];
        for (int i = 0; i < Count; i++)
        {
            GameObject go = new GameObject("반짝임", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Canvas));
            go.transform.SetParent(transform, false);
            Image img = go.GetComponent<Image>();
            img.sprite = GetStar();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.97f, 0.8f, 0f);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            stars[i] = r;
            images[i] = img;
            phase[i] = i / (float)Count;
            Move(i);
        }
    }

    private void Move(int i)
    {
        Vector2 size = ((RectTransform)transform).rect.size;
        stars[i].anchoredPosition = new Vector2(Random.Range(-0.45f, 0.45f) * size.x, Random.Range(-0.45f, 0.45f) * size.y);
    }

    private void Update()
    {
        if (stars == null)
            return;
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null)
                continue;
            phase[i] += Time.unscaledDeltaTime / Period;
            if (phase[i] >= 1f)
            {
                phase[i] -= 1f;
                Move(i);
            }
            float t = Mathf.Clamp01(phase[i] / 0.6f);
            float glow = phase[i] < 0.6f ? Mathf.Sin(t * Mathf.PI) : 0f;
            float s = Mathf.Lerp(MinSize, MaxSize, glow);
            stars[i].sizeDelta = new Vector2(s, s);
            stars[i].localRotation = Quaternion.Euler(0f, 0f, phase[i] * 90f);
            images[i].color = new Color(1f, 0.97f, 0.8f, glow);
            Canvas c = stars[i].GetComponent<Canvas>();
            if (!c.overrideSorting || c.sortingOrder != SortingOrder)
            {
                c.overrideSorting = true;
                c.sortingOrder = SortingOrder;
            }
        }
    }

    private static Sprite GetStar()
    {
        if (starSprite != null)
            return starSprite;
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - c) / c, dy = Mathf.Abs(y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ray = Mathf.Max(Mathf.Clamp01(1f - dx) * Mathf.Clamp01(1f - dy * 9f), Mathf.Clamp01(1f - dy) * Mathf.Clamp01(1f - dx * 9f));
                float core = Mathf.Clamp01(1f - r * 2.6f);
                float halo = Mathf.Clamp01(1f - r) * 0.25f;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(ray, core) + halo * halo));
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        starSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return starSprite;
    }
}
