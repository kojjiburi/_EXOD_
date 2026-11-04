using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화장실 수건 반짝임 (사용자 요청 2026-11-02)
/// 수건 아래에 손전등이 숨겨져 있다는 걸 알아차리기 쉽도록, 수건 위에서 작은 별이 반짝입니다.
/// - 손전등을 한 번이라도 얻으면(퀵슬롯에 들어오면) 그 판에서는 더 이상 반짝이지 않습니다.
/// - 수건만 반짝입니다. (다른 물건에는 없음)
/// 수건 그림은 RestroomGimmick이 "선반_towel" 이름으로 화장실 화면에 만듭니다. (이 파일은 그 그림에 별만 붙임)
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
public sealed class Stage1TowelSparkle : MonoBehaviour
{
    private const string TowelViewName = "선반_" + RestroomGimmick.TowelItemId;
    private const int StarCount = 5;

    private static Sprite starSprite;

    private bool flashlightFound;
    private Transform towelView;
    private RectTransform[] stars;
    private Image[] starImages;
    private float[] starPhase;
    private float nextSearchTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<Stage1TowelSparkle>() != null)
            return;

        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null)
            slider.gameObject.AddComponent<Stage1TowelSparkle>();
    }

    private void Update()
    {
        if (flashlightFound)
            return;

        if (HasFlashlight())
        {
            flashlightFound = true;
            ClearStars();
            return;
        }

        // 수건 그림은 선반이 바뀔 때마다 새로 만들어지므로, 없어졌으면 다시 찾습니다.
        if (towelView == null)
        {
            stars = null;
            if (Time.unscaledTime < nextSearchTime)
                return;
            nextSearchTime = Time.unscaledTime + 0.3f;

            GameObject found = FindTowelView();
            if (found == null)
                return;
            towelView = found.transform;
            BuildStars();
        }

        AnimateStars();
    }

    private static GameObject FindTowelView()
    {
        RestroomGimmick restroom = RestroomGimmick.Instance;
        if (restroom == null || !restroom.IsShelfCorrect)
            return null;

        // 화장실 화면 아래에서 수건 그림을 찾습니다.
        foreach (RectTransform rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rect.name == TowelViewName)
                return rect.gameObject;
        }
        return null;
    }

    private void BuildStars()
    {
        Sprite sprite = GetStarSprite();
        stars = new RectTransform[StarCount];
        starImages = new Image[StarCount];
        starPhase = new float[StarCount];

        for (int i = 0; i < StarCount; i++)
        {
            GameObject go = new GameObject("반짝임", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(towelView, false);
            // 수건은 하트 HUD(600) 뒤에 가려지므로, 별만 HUD 위·행동 요령(610) 아래에 그림
            Canvas starCanvas = go.AddComponent<Canvas>();
            starCanvas.overrideSorting = true;
            starCanvas.sortingOrder = 605;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;   // 수건 클릭을 막지 않음
            image.color = new Color(1f, 0.97f, 0.8f, 0f);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            stars[i] = rect;
            starImages[i] = image;
            starPhase[i] = i / (float)StarCount;
            MoveStar(i);
        }
    }

    // 수건 안 아무 곳으로 별을 옮깁니다. (가운데 쪽 위주)
    private void MoveStar(int i)
    {
        RectTransform towel = towelView as RectTransform;
        Vector2 size = towel != null ? towel.rect.size : new Vector2(200f, 60f);
        stars[i].anchoredPosition = new Vector2(
            Random.Range(-0.38f, 0.38f) * size.x,
            Random.Range(-0.25f, 0.3f) * size.y);
    }

    private void AnimateStars()
    {
        if (stars == null)
            return;

        const float period = 1.3f;
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null)
                continue;

            float before = starPhase[i];
            starPhase[i] += Time.unscaledDeltaTime / period;
            if (starPhase[i] >= 1f)
            {
                starPhase[i] -= 1f;
                MoveStar(i);
            }

            // 반짝: 커졌다가 사라짐 (한 주기의 앞쪽 40%만 보임)
            float t = Mathf.Clamp01(starPhase[i] / 0.6f);
            float glow = starPhase[i] < 0.6f ? Mathf.Sin(t * Mathf.PI) : 0f;
            float size = Mathf.Lerp(16f, 58f, glow);
            stars[i].sizeDelta = new Vector2(size, size);
            // 중첩 캔버스의 overrideSorting은 만든 직후에는 적용이 안 될 때가 있어 매 프레임 확인
            Canvas starCanvas = stars[i].GetComponent<Canvas>();
            if (starCanvas != null && (!starCanvas.overrideSorting || starCanvas.sortingOrder != 605))
            {
                starCanvas.overrideSorting = true;
                starCanvas.sortingOrder = 605;
            }
            stars[i].localRotation = Quaternion.Euler(0f, 0f, starPhase[i] * 90f);
            starImages[i].color = new Color(1f, 0.97f, 0.8f, glow);
        }
    }

    private void ClearStars()
    {
        if (stars != null)
        {
            foreach (RectTransform star in stars)
            {
                if (star != null)
                    Destroy(star.gameObject);
            }
        }
        stars = null;
        towelView = null;
    }

    private static bool HasFlashlight()
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        if (inventory == null)
            return false;
        foreach (QuickSlotEntry entry in inventory.Entries)
        {
            if (entry != null && entry.ItemData != null && entry.ItemData.ItemId == FlashlightGimmick.FlashlightItemId)
                return true;
        }
        return false;
    }

    // 4갈래 별 + 부드러운 빛 (코드로 그림)
    private static Sprite GetStarSprite()
    {
        if (starSprite != null)
            return starSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - c) / c;
                float dy = Mathf.Abs(y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                // 가로·세로로 가늘게 뻗는 빛줄기
                float ray = Mathf.Max(
                    Mathf.Clamp01(1f - dx) * Mathf.Clamp01(1f - dy * 9f),
                    Mathf.Clamp01(1f - dy) * Mathf.Clamp01(1f - dx * 9f));
                float core = Mathf.Clamp01(1f - r * 2.6f);
                float halo = Mathf.Clamp01(1f - r) * 0.25f;
                float a = Mathf.Clamp01(Mathf.Max(ray, core) + halo * halo);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        starSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return starSprite;
    }
}
