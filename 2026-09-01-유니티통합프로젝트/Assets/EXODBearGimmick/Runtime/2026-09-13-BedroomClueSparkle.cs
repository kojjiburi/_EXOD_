using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(210)]
internal sealed class BedroomClueSparkleBootstrap : MonoBehaviour
{
    private static readonly HashSet<string> ClueIds = new()
    {
        "bedroom_calendar",
        "bedroom_photo",
        "bedroom_memo"
    };

    private readonly List<BedroomClueSparkle> sparkles = new();
    private Sprite sparkleSprite;

    // 시작 화면을 거쳐 들어오거나 [다시하기]로 씬을 다시 불러와도 동작하도록 씬이 열릴 때마다 확인합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 방 조사 화면이 있는 씬(침실 씬)에서만 반짝임을 붙입니다.
        if (FindFirstObjectByType<SliderManager>() == null
            || FindFirstObjectByType<BedroomClueSparkleBootstrap>() != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
            canvas.gameObject.AddComponent<BedroomClueSparkleBootstrap>();
    }

    private void Start()
    {
        sparkleSprite = CreateSparkleSprite();

        InspectableItem[] items = FindObjectsByType<InspectableItem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (InspectableItem item in items)
        {
            if (item == null || !ClueIds.Contains(item.ItemId))
                continue;

            CreateSparkles(item);
        }
    }

    private void CreateSparkles(InspectableItem item)
    {
        Vector2[] offsets =
        {
            new(-34f, 26f),
            new(38f, -18f),
            new(8f, 42f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject star = new(
                $"단서 반짝임 {i + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(BedroomClueSparkle));
            star.transform.SetParent(item.transform, false);

            RectTransform rect = star.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offsets[i];
            rect.sizeDelta = i == 0 ? new Vector2(34f, 34f) : new Vector2(23f, 23f);

            Image image = star.GetComponent<Image>();
            image.sprite = sparkleSprite;
            image.color = i == 1
                ? new Color(1f, 0.90f, 0.48f, 0.9f)
                : new Color(1f, 0.98f, 0.78f, 0.95f);
            image.raycastTarget = false;

            BedroomClueSparkle sparkle = star.GetComponent<BedroomClueSparkle>();
            sparkle.Configure(item.ItemId, offsets[i], i * 1.73f);
            sparkles.Add(sparkle);
        }
    }

    private static Sprite CreateSparkleSprite()
    {
        const int size = 48;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
        texture.name = "BedroomClueSparkleRuntime";
        texture.filterMode = FilterMode.Bilinear;
        Color clear = new(1f, 1f, 1f, 0f);

        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center) / center;
                float dy = Mathf.Abs(y - center) / center;
                float arm = Mathf.Min(dx + dy * 3.2f, dy + dx * 3.2f);
                float alpha = 1f - Mathf.SmoothStep(0.18f, 1f, arm);
                texture.SetPixel(x, y, alpha > 0f ? new Color(1f, 1f, 1f, alpha) : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}

internal sealed class BedroomClueSparkle : MonoBehaviour
{
    private string clueId;
    private Vector2 basePosition;
    private float phase;
    private RectTransform rect;
    private Image image;
    private Color baseColor;

    internal void Configure(string id, Vector2 position, float animationPhase)
    {
        clueId = id;
        basePosition = position;
        phase = animationPhase;
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        baseColor = image.color;
    }

    private void Update()
    {
        if (rect == null || image == null)
            return;

        bool uiBlocked =
            (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen) ||
            (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen);
        bool examined = InspectionState.IsExamined(clueId);
        bool shouldShow = !uiBlocked && !examined;
        image.enabled = shouldShow;
        if (!shouldShow)
            return;

        float time = Time.unscaledTime;
        float wave = Mathf.Sin(time * 2.4f + phase);
        float sideWave = Mathf.Cos(time * 1.55f + phase);
        rect.anchoredPosition = basePosition + new Vector2(sideWave * 4f, wave * 9f);
        rect.localRotation = Quaternion.Euler(0f, 0f, time * 38f + phase * 25f);
        float scale = 0.82f + (wave + 1f) * 0.18f;
        rect.localScale = Vector3.one * scale;

        Color color = baseColor;
        color.a = Mathf.Lerp(0.38f, baseColor.a, (wave + 1f) * 0.5f);
        image.color = color;
    }
}
