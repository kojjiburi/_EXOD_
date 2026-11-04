using EXOD.Walk;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 산책 길 전화 부스의 잠긴 문에 자물쇠 그림을 겁니다. (사용자 그림, 2026-11-02)
/// - 그림: Resources/EXODStage1/BoothPadlock.png (부스 열쇠 아이콘은 BoothKeyIcon.png / Icon_BoothKey.png)
/// - PhoneBooth의 '잠김 모습(lockedVersion)' 아래에 붙이므로, 열쇠로 문을 열면 잠김 모습과 함께 자동으로 사라집니다.
/// - 씬 파일(Stage2_Walk)은 건드리지 않고 실행 중에 붙입니다.
/// </summary>
public static class WalkBoothPadlock
{
    private const string PadlockPath = "EXODStage1/BoothPadlock";
    private const string PadlockName = "자물쇠";
    private const float HeightRatio = 0.2f;   // 문 높이의 20%

    private static Sprite padlockSprite;

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

        foreach (PhoneBooth booth in Object.FindObjectsByType<PhoneBooth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Attach(booth);
    }

    private static void Attach(PhoneBooth booth)
    {
        var field = typeof(PhoneBooth).GetField("lockedVersion",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        GameObject locked = field != null ? field.GetValue(booth) as GameObject : null;
        if (locked == null || locked.transform.Find(PadlockName) != null)
            return;

        // 잠긴 문 그림: 이름에 '닫힘'이 들어간 것, 없으면 가장 큰 그림
        SpriteRenderer door = null;
        float biggest = 0f;
        foreach (SpriteRenderer sr in locked.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null)
                continue;
            if (sr.name.Contains("닫힘"))
            {
                door = sr;
                break;
            }
            float area = sr.bounds.size.x * sr.bounds.size.y;
            if (area > biggest)
            {
                biggest = area;
                door = sr;
            }
        }
        if (door == null)
            return;

        Sprite sprite = LoadSprite();
        if (sprite == null)
            return;

        GameObject go = new GameObject(PadlockName, typeof(SpriteRenderer));
        go.transform.SetParent(locked.transform, false);
        SpriteRenderer padlock = go.GetComponent<SpriteRenderer>();
        padlock.sprite = sprite;
        padlock.sortingLayerID = door.sortingLayerID;
        padlock.sortingOrder = door.sortingOrder + 1;

        // 크기: 문 높이의 20% / 위치: 문 가운데, 조금 아래 (손잡이 높이)
        Bounds doorBounds = door.bounds;
        float targetHeight = doorBounds.size.y * HeightRatio;
        float spriteHeight = sprite.bounds.size.y;
        float worldScale = spriteHeight > 0f ? targetHeight / spriteHeight : 1f;
        Vector3 parentScale = locked.transform.lossyScale;
        go.transform.localScale = new Vector3(
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            1f);
        go.transform.position = new Vector3(
            doorBounds.center.x,
            doorBounds.center.y - doorBounds.size.y * 0.05f,
            door.transform.position.z);
    }

    private static Sprite LoadSprite()
    {
        if (padlockSprite != null)
            return padlockSprite;

        Texture2D texture = Resources.Load<Texture2D>(PadlockPath);
        if (texture == null)
        {
            Debug.LogWarning("[WalkBoothPadlock] 자물쇠 그림이 없습니다: Resources/" + PadlockPath);
            return null;
        }

        padlockSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        return padlockSprite;
    }
}
