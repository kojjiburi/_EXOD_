// 테스트 전용: 스테이지1 화장실 그림을 임시로 깔고, 선반 빈자리에 손거울을 놓아 클릭 → 기믹 열기를 확인한다.
// 실제 게임에서는 이 스크립트 대신, 스테이지1의 거울 오브젝트에 MirrorMazeTrigger 만 붙이면 된다.

using UnityEngine;
using UnityEngine.UI;

public class MirrorMazeTestRoom : MonoBehaviour
{
    [Tooltip("임시 방 그림 (비우면 Resources/MirrorMaze_Stage1Placeholder)")] public Sprite roomSprite;
    [Tooltip("손거울을 놓을 위치 (방 그림의 선반 빈자리)")] public Vector2 mirrorPosition = new Vector2(2.06f, 4.1f);
    public float mirrorRadius = 0.42f;

    Text status;

    void Start()
    {
        var cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = 5f;

        // 방 그림
        Sprite room = roomSprite;
        if (room == null)
        {
            var tex = Resources.Load<Texture2D>("MirrorMaze_Stage1Placeholder");
            if (tex != null) room = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.height / 10f);
        }
        if (room != null)
        {
            var go = new GameObject("Stage1Room (임시)");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = room;
            sr.sortingOrder = -100;
            float camH = 10f, camW = camH * cam.aspect;
            float k = Mathf.Max(camH / room.bounds.size.y, camW / room.bounds.size.x);
            go.transform.localScale = new Vector3(k, k, 1);
            mirrorPosition *= k;
        }

        // 선반 위 손거울 (클릭하면 기믹 열림)
        var mirror = new GameObject("HandMirror_OnShelf");
        mirror.transform.SetParent(transform, false);
        mirror.transform.localPosition = mirrorPosition;
        Sprite circle = MakeCircle(128), square = MakeSquare();
        float R = mirrorRadius;
        Add(mirror.transform, "Handle", square, new Vector2(0, -R - 0.22f), new Vector2(0.13f, 0.5f), new Color(0.05f, 0.05f, 0.08f), 1);
        Add(mirror.transform, "HandleIn", square, new Vector2(0, -R - 0.22f), new Vector2(0.08f, 0.46f), new Color(0.72f, 0.72f, 0.75f), 2);
        Add(mirror.transform, "Rim", circle, Vector2.zero, Vector2.one * (R + 0.09f) * 2, new Color(0.05f, 0.05f, 0.08f), 3);
        Add(mirror.transform, "RimMetal", circle, Vector2.zero, Vector2.one * (R + 0.05f) * 2, new Color(0.74f, 0.74f, 0.77f), 4);
        Add(mirror.transform, "Glass", circle, Vector2.zero, Vector2.one * R * 2, new Color(0.62f, 0.68f, 0.76f), 5);
        var shine = Add(mirror.transform, "Shine", square, new Vector2(-R * 0.25f, R * 0.1f), new Vector2(0.06f, R * 1.3f), new Color(1, 1, 1, 0.6f), 6);
        shine.transform.localRotation = Quaternion.Euler(0, 0, -30);

        var col = mirror.AddComponent<CircleCollider2D>();
        col.radius = R + 0.15f;
        var trigger = mirror.AddComponent<MirrorMazeTrigger>();
        trigger.onSolved.AddListener(code => { if (status != null) status.text = "[테스트] 미로 클리어! (거울을 다시 누르면 클리어 화면이 보임)"; });

        // 안내 문구
        var canvasGo = new GameObject("TestRoomUI");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var tgo = new GameObject("Status");
        tgo.transform.SetParent(canvasGo.transform, false);
        var rt = tgo.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 240);
        rt.sizeDelta = new Vector2(1600, 60);
        status = tgo.AddComponent<Text>();
        status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        status.fontSize = 32;
        status.alignment = TextAnchor.MiddleCenter;
        status.color = Color.white;
        status.raycastTarget = false;
        status.horizontalOverflow = HorizontalWrapMode.Overflow;
        status.text = "[테스트] 선반 위 손거울을 클릭하세요";
        var sh = tgo.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.9f); sh.effectDistance = new Vector2(2, -2);
        trigger.onFailed.AddListener(() => { if (status != null) status.text = "[테스트] 벽에 3번 부딪혀 실패! 거울을 다시 클릭해 보세요"; });
        trigger.hideWhileOpen = new[] { tgo }; // 기믹이 열려 있는 동안 테스트 문구 숨김
    }

    static SpriteRenderer Add(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite; sr.color = color; sr.sortingOrder = order;
        return sr;
    }

    static Sprite MakeSquare()
    {
        var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
        var px = new Color32[16]; for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    static Sprite MakeCircle(int res)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[res * res]; float r = res / 2f;
        for (int y = 0; y < res; y++) for (int x = 0; x < res; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * res + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
