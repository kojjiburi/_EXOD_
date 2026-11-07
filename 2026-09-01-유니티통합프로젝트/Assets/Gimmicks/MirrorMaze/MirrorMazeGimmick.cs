// [거울에 비친 단서] 기믹 (스테이지1 화장실 선반의 손거울)
// - 화장실 선반의 손거울을 클릭하면 MirrorMazeGimmick.Open()으로 이 기믹이 스테이지 화면 위에 열린다.
// - 손거울을 마우스로 끌어 움직이면, 거울 안에만 "반대편 벽"(립스틱으로 그려진 미로)이 비친다.
// - WASD / 방향키로 미로 속 흰 점을 움직인다. 점도 거울 안에서만 보이므로 거울로 따라가야 한다.
// - [터치] 왼쪽 아래 조이스틱으로도 움직일 수 있고, 오른쪽 아래 [다시하기]·[나가기] 버튼이 R·ESC 대신이다.
//   손가락 여러 개를 따로 추적하므로 한 손가락은 조이스틱, 다른 손가락은 손거울을 동시에 끌 수 있다.
// - 붉은 점(출구)에 도착하면 클리어 → onSolved / Solved 이벤트가 호출된다 (화면에 숫자는 표시하지 않음).
// - 오른쪽 위 X 버튼 또는 ESC로 닫으면 원래 화면으로 돌아간다.
// 별도 스프라이트/프리팹 없이 동작한다. 기믹은 월드의 먼 곳(worldOffset)에 자기 카메라로 그려지므로 스테이지 오브젝트와 겹치지 않는다.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MirrorMazeGimmick : MonoBehaviour
{
    // =========================================================
    //  외부 API
    // =========================================================
    /// <summary>현재 열려 있는 기믹 (없으면 null)</summary>
    public static MirrorMazeGimmick Current { get; private set; }
    public static bool IsOpen => Current != null;
    /// <summary>한 번이라도 미로를 풀었는지 (다시 열어도 유지)</summary>
    public static bool IsSolved { get; private set; }
    public static string LastClue { get; private set; }
    /// <summary>미로를 풀었을 때 (단서 코드 전달)</summary>
    public static event Action<string> Solved;
    /// <summary>기믹 창이 닫혔을 때</summary>
    public static event Action Closed;
    /// <summary>벽에 너무 많이 부딪혀 실패했을 때 (Closed 보다 먼저 호출)</summary>
    public static event Action Failed;
    /// <summary>보상(전화부스 열쇠)을 주웠는지 (다시 열어도 유지)</summary>
    public static bool RewardTaken { get; private set; }
    /// <summary>보상을 주웠을 때 (아이템 ID 전달)</summary>
    public static event Action<string> RewardObtained;
    /// <summary>마지막으로 닫힌 프레임 (같은 클릭으로 바로 다시 열리는 것 방지용)</summary>
    public static int LastClosedFrame { get; private set; } = -1;

    /// <summary>기믹을 연다. 이미 열려 있으면 그걸 돌려준다.</summary>
    public static MirrorMazeGimmick Open(string clueCode = null, int? seed = null, bool? invertHorizontal = null, UnityEngine.Object rewardItemAsset = null)
    {
        if (Current != null) return Current;
        var go = new GameObject("MirrorMazeGimmick (Runtime)");
        var g = go.AddComponent<MirrorMazeGimmick>();
        if (rewardItemAsset != null) g.rewardItemAsset = rewardItemAsset;
        if (!string.IsNullOrEmpty(clueCode)) g.clueCode = clueCode;
        if (seed.HasValue) g.seed = seed.Value;
        if (invertHorizontal.HasValue) g.invertHorizontalControls = invertHorizontal.Value;
        return g;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Current = null; IsSolved = false; LastClue = null; Solved = null; Closed = null; Failed = null; LastClosedFrame = -1; RewardTaken = false; RewardObtained = null; }

    // =========================================================
    //  인스펙터 설정
    // =========================================================
    [Header("미로")]
    public int mazeWidth = 11;
    public int mazeHeight = 7;
    public float cellSize = 0.72f;
    [Tooltip("기믹 화면 기준 미로 중심")] public Vector2 mazeCenter = new Vector2(0f, 0.6f);
    [Tooltip("같은 시드 = 항상 같은 미로")] public int seed = 1027;
    [Tooltip("갈림길 정도 (0 = 긴 외길, 1 = 짧은 갈림길이 아주 많음)")] [Range(0f, 1f)] public float branchiness = 0.5f;
    [Tooltip("벽을 몇 군데 더 터서 출구까지 가는 길을 여러 갈래로 만든다")] [Range(0, 20)] public int extraOpenings = 4;

    [Header("손거울")]
    [Tooltip("거울 유리 반지름")] public float mirrorRadius = 1.55f;
    [Tooltip("켜면 좌우 조작이 거울처럼 반대 (난이도 상승)")] public bool invertHorizontalControls = false;

    [Header("단서")]
    [Tooltip("화면에는 표시되지 않음. 클리어 이벤트(onSolved/Solved)로만 전달되는 값")] public string clueCode = "0427";
    [Tooltip("이미 푼 상태로 다시 열면 클리어 화면을 바로 보여준다")] public bool keepSolvedState = true;

    [Header("보상: 전화부스 열쇠 (스테이지2)")]
    [Tooltip("팀 ItemData 의 Item Id. 스테이지2 전화부스가 이 ID의 열쇠로 열린다")] public string rewardItemId = "stage2_booth_key";
    [Tooltip("아이템 이름 (퀵슬롯·획득 문구)")] public string rewardName = "전화부스 열쇠";
    [Tooltip("통합 프로젝트에서 스테이지2의 열쇠 ItemData 에셋(PhoneBooth_Key)을 넣으면 그걸 그대로 준다. 비워도 됨")]
    public UnityEngine.Object rewardItemAsset;
    [Tooltip("비워두면 Resources/MirrorMaze_BoothKey 사용")] public Sprite rewardIcon;

    [Header("화면")]
    [Tooltip("비워두면 Resources/MirrorMaze_ShelfBG 사용")] public Sprite backgroundSprite;
    [Tooltip("기믹을 그릴 월드 위치 (스테이지와 겹치지 않는 먼 곳)")] public Vector2 worldOffset = new Vector2(1000f, 0f);
    [Tooltip("기믹 카메라 depth (스테이지 카메라보다 커야 위에 보임)")] public float cameraDepth = 100f;
    [Tooltip("비워두면 유니티 기본 폰트")] public Font customFont;
    [Tooltip("기믹 UI의 Canvas sortingOrder")] public int canvasSortingOrder = 500;

    [Header("연출")]
    public bool enableEyes = true;

    [Header("실패")]
    [Tooltip("벽에 이만큼 부딪히면 기믹이 닫힌다 (키를 새로 누를 때만 셈)")] public int maxBumps = 3;

    [Header("이벤트")]
    public UnityEvent<string> onSolved = new UnityEvent<string>();
    public UnityEvent onClosed = new UnityEvent();
    [Tooltip("전화부스 열쇠를 주웠을 때 (아이템 ID 전달)")]
    public UnityEvent<string> onRewardObtained = new UnityEvent<string>();
    [Tooltip("벽에 maxBumps번 부딪혀 실패했을 때 (onClosed 보다 먼저 호출)")]
    public UnityEvent onFailed = new UnityEvent();

    // =========================================================
    //  내부
    // =========================================================
    const int OrderKey = 40, OrderBg = -100, OrderRimBack = 8, OrderGlass = 10, OrderTiles = 11, OrderTrail = 13, OrderWalls = 14,
              OrderGoal = 15, OrderPlayer = 16, OrderEyes = 17, OrderShine = 25,
              OrderHandle = 28, OrderRim = 30;
    static readonly Color TileNavy = new Color(0.16f, 0.18f, 0.39f), Grout = new Color(0.52f, 0.55f, 0.70f, 0.6f),
                          Lipstick = new Color(0.68f, 0.06f, 0.10f), Ink = new Color(0.05f, 0.05f, 0.08f),
                          Metal = new Color(0.74f, 0.74f, 0.77f), MetalDark = new Color(0.52f, 0.52f, 0.56f);

    Camera cam;
    Sprite squareSprite, circleSprite, ringSprite;
    Transform mirrorRoot, mazeRoot, eyesRoot;
    SpriteRenderer playerSr, goalSr;
    bool[,] eastWall, southWall, visited;
    Vector2Int playerCell, goalCell;
    bool solved, dragging, closing;
    Vector2 dragOffset;
    float holdTimer; Vector2Int heldDir;
    float eyesTimer, eyesShowTimer, bumpShake, flashTimer, failTimer = -1f;
    int bumpCount;
    SpriteRenderer glassSr;
    Font uiFont;
    Text popupText;
    GameObject popupPanel, canvasGo;
    RectTransform closeButton;
    // [터치] 조이스틱 · 다시하기 · 나가기 버튼
    RectTransform joyBase, joyKnob, restartButton, exitButton;
    const float JoyRadius = 120f;          // 손잡이가 움직이는 최대 거리 (캔버스 단위)
    const float JoyDeadZone = 0.35f;       // 이만큼(반지름 비율) 이상 밀어야 이동
    int joyPointerId = int.MinValue;       // 조이스틱을 잡은 손가락 (마우스 = -1)
    int dragPointerId = int.MinValue;      // 손거울을 잡은 손가락 (마우스 = -1)
    Vector2Int joyDir;
    const int NoPointer = int.MinValue, MousePointer = -1;
    SpriteRenderer keySr, keyGlintSr;
    Sprite keySprite;
    Image popupIcon;
    float keyDropT = -1f, keyTakeT = -1f;
    Vector2 keyRestLocal = new Vector2(-0.05f, -1.56f);

    Vector2 Origin => worldOffset;

    void Awake()
    {
        if (Current != null && Current != this) { Destroy(gameObject); return; }
        Current = this;
        transform.position = new Vector3(worldOffset.x, worldOffset.y, 0);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
        if (canvasGo != null) Destroy(canvasGo);
    }

    void Start()
    {
        transform.position = new Vector3(worldOffset.x, worldOffset.y, 0);
        squareSprite = MakeSquareSprite();
        circleSprite = MakeCircleSprite(128);
        ringSprite = MakeRingSprite(256, 0.86f);

        BuildCamera();
        BuildBackground();
        BuildMirror();
        BuildUI();
        BuildMaze();

        if (keepSolvedState && IsSolved) ShowSolvedState(false);
        MoveMirrorTo(playerSr.transform.position);
    }

    // =========================================================
    //  생성
    // =========================================================
    void BuildCamera()
    {
        var camGo = new GameObject("MirrorMazeCamera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0, 0, -10);
        cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.depth = cameraDepth;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.07f, 0.10f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 50f;
    }

    void BuildBackground()
    {
        Sprite bg = backgroundSprite;
        if (bg == null)
        {
            var tex = Resources.Load<Texture2D>("MirrorMaze_ShelfBG");
            if (tex != null) bg = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.height / 10f);
        }
        var go = new GameObject("Background");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = OrderBg;
        if (bg != null)
        {
            sr.sprite = bg;
            float camH = cam.orthographicSize * 2f, camW = camH * cam.aspect;
            Vector2 size = bg.bounds.size;
            float k = Mathf.Max(camH / size.y, camW / size.x);
            go.transform.localScale = new Vector3(k, k, 1);
        }
        else
        {
            sr.sprite = squareSprite;
            sr.color = new Color(0.47f, 0.47f, 0.50f);
            go.transform.localScale = new Vector3(40, 30, 1);
        }
    }

    void BuildMirror()
    {
        mirrorRoot = new GameObject("HandMirror").transform;
        mirrorRoot.SetParent(transform, false);
        float R = mirrorRadius;

        // 손잡이 (아래) — 위쪽 끝은 테두리 링 밑으로 숨김
        float hl = 1.5f, hw = 0.36f, hc = -R - 0.12f - hl / 2f;
        MakeRect("HandleOutline", mirrorRoot, new Vector2(0, hc), new Vector2(hw + 0.1f, hl + 0.1f), Ink, OrderHandle, false);
        MakeRect("Handle", mirrorRoot, new Vector2(0, hc), new Vector2(hw, hl), Metal, OrderHandle, false);
        MakeRect("HandleShade", mirrorRoot, new Vector2(hw * 0.25f, hc), new Vector2(hw * 0.3f, hl), MetalDark, OrderHandle, false);
        MakeRect("HandleHi", mirrorRoot, new Vector2(-hw * 0.22f, hc), new Vector2(0.05f, hl * 0.9f), new Color(0.92f, 0.92f, 0.95f), OrderHandle, false);
        var cap = MakeRect("HandleCap", mirrorRoot, new Vector2(0, hc - hl / 2f), Vector2.one * (hw + 0.14f), Ink, OrderHandle, false); cap.sprite = circleSprite;
        var cap2 = MakeRect("HandleCap2", mirrorRoot, new Vector2(0, hc - hl / 2f), Vector2.one * (hw + 0.02f), MetalDark, OrderHandle, false); cap2.sprite = circleSprite;

        // 유리 바탕 + 마스크
        var glass = MakeRect("Glass", mirrorRoot, Vector2.zero, Vector2.one * R * 2, TileNavy, OrderGlass, false); glass.sprite = circleSprite;
        glassSr = glass;
        var maskGo = new GameObject("Mask");
        maskGo.transform.SetParent(mirrorRoot, false);
        maskGo.transform.localScale = new Vector3(R * 2, R * 2, 1);
        var mask = maskGo.AddComponent<SpriteMask>();
        mask.sprite = circleSprite;
        mask.alphaCutoff = 0.5f;

        // 테두리 (검은 외곽선 + 금속 링 + 안쪽 선)
        var rimOutline = MakeRect("RimOutline", mirrorRoot, Vector2.zero, Vector2.one * (R + 0.30f) * 2, Ink, OrderRim, false); rimOutline.sprite = ringSprite;
        var rimMetal = MakeRect("RimMetal", mirrorRoot, Vector2.zero, Vector2.one * (R + 0.24f) * 2, Metal, OrderRim + 1, false); rimMetal.sprite = MakeRingSprite(256, R / (R + 0.24f) * 1.02f);
        var rimInner = MakeRect("RimInner", mirrorRoot, Vector2.zero, Vector2.one * (R + 0.03f) * 2, Ink, OrderRim + 2, false); rimInner.sprite = MakeRingSprite(256, 0.97f);
        var rimHi = MakeRect("RimHighlight", mirrorRoot, new Vector2(-0.05f, 0.05f), Vector2.one * (R + 0.17f) * 2, new Color(1, 1, 1, 0.55f), OrderRim + 2, false); rimHi.sprite = MakeArcSprite(256, 0.95f, 110f, 200f);

        // 유리 반사광 (마스크 안에서만)
        var shine = MakeRect("Shine", mirrorRoot, new Vector2(-R * 0.25f, R * 0.15f), new Vector2(0.18f, R * 2.4f), new Color(1, 1, 1, 0.10f), OrderShine, true);
        shine.transform.localRotation = Quaternion.Euler(0, 0, -32);
        var shine2 = MakeRect("Shine2", mirrorRoot, new Vector2(-R * 0.05f, R * 0.15f), new Vector2(0.07f, R * 2.4f), new Color(1, 1, 1, 0.08f), OrderShine, true);
        shine2.transform.localRotation = Quaternion.Euler(0, 0, -32);

        // 반대편 벽 타일 줄눈 (기믹 월드에 고정)
        var tiles = new GameObject("OppositeSide_Tiles").transform;
        tiles.SetParent(transform, false);
        for (float x = -10.08f; x <= 10.1f; x += 0.42f)
            MakeRect("GroutV", tiles, new Vector2(x, 0), new Vector2(0.045f, 12f), Grout, OrderTiles, true);
        for (float y = -5.88f; y <= 5.9f; y += 0.42f)
            MakeRect("GroutH", tiles, new Vector2(0, y), new Vector2(21f, 0.045f), Grout, OrderTiles, true);
    }

    void BuildMaze()
    {
        if (mazeRoot != null) Destroy(mazeRoot.gameObject);
        solved = false;

        mazeRoot = new GameObject("OppositeSide_Maze").transform;
        mazeRoot.SetParent(transform, false);
        mazeRoot.localPosition = mazeCenter;
        mazeRoot.localScale = new Vector3(-1, 1, 1); // 거울에 비친 모습 → 좌우 반전

        int W = Mathf.Max(2, mazeWidth), H = Mathf.Max(2, mazeHeight);
        GenerateMaze(W, H, seed);

        float cs = cellSize, wt = cs * 0.14f;
        MakeRect("BorderTop", mazeRoot, new Vector2(0, H * cs / 2), new Vector2(W * cs + wt, wt), Lipstick, OrderWalls, true);
        MakeRect("BorderBottom", mazeRoot, new Vector2(0, -H * cs / 2), new Vector2(W * cs + wt, wt), Lipstick, OrderWalls, true);
        MakeRect("BorderLeft", mazeRoot, new Vector2(-W * cs / 2, 0), new Vector2(wt, H * cs + wt), Lipstick, OrderWalls, true);
        MakeRect("BorderRight", mazeRoot, new Vector2(W * cs / 2, 0), new Vector2(wt, H * cs + wt), Lipstick, OrderWalls, true);
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                Vector2 c = CellLocal(x, y);
                if (x < W - 1 && eastWall[x, y]) MakeRect("W", mazeRoot, c + new Vector2(cs / 2, 0), new Vector2(wt, cs + wt), Lipstick, OrderWalls, true);
                if (y > 0 && southWall[x, y]) MakeRect("W", mazeRoot, c + new Vector2(0, -cs / 2), new Vector2(cs + wt, wt), Lipstick, OrderWalls, true);
            }

        playerCell = new Vector2Int(0, H - 1);
        goalCell = new Vector2Int(W - 1, 0);
        MakeRect("Start", mazeRoot, CellLocal(playerCell.x, playerCell.y), Vector2.one * cs * 0.7f, new Color(1, 1, 1, 0.16f), OrderTrail, true);
        goalSr = MakeRect("Goal", mazeRoot, CellLocal(goalCell.x, goalCell.y), Vector2.one * cs * 0.55f, new Color(0.88f, 0.08f, 0.15f), OrderGoal, true);
        goalSr.sprite = circleSprite;
        playerSr = MakeRect("Player", mazeRoot, CellLocal(playerCell.x, playerCell.y), Vector2.one * cs * 0.45f, new Color(0.97f, 0.97f, 0.97f), OrderPlayer, true);
        playerSr.sprite = circleSprite;
        visited[playerCell.x, playerCell.y] = true;

        eyesRoot = new GameObject("Eyes").transform;
        eyesRoot.SetParent(mazeRoot, false);
        var e1 = MakeRect("EyeL", eyesRoot, new Vector2(-0.18f, 0), new Vector2(0.22f, 0.12f), new Color(0.9f, 0f, 0.05f), OrderEyes, true);
        var e2 = MakeRect("EyeR", eyesRoot, new Vector2(0.18f, 0), new Vector2(0.22f, 0.12f), new Color(0.9f, 0f, 0.05f), OrderEyes, true);
        e1.sprite = circleSprite; e2.sprite = circleSprite;
        eyesRoot.gameObject.SetActive(false);
        eyesTimer = UnityEngine.Random.Range(5f, 10f);


        if (popupPanel != null) popupPanel.SetActive(false);
    }

    void GenerateMaze(int W, int H, int s)
    {
        eastWall = new bool[W, H]; southWall = new bool[W, H]; visited = new bool[W, H];
        var seen = new bool[W, H];
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) { eastWall[x, y] = true; southWall[x, y] = true; }
        var rng = new System.Random(s);

        // Growing Tree: 가장 최근 칸(긴 길)과 무작위 칸(갈림길)을 섞어서 판다
        var active = new List<Vector2Int>();
        var start = new Vector2Int(0, H - 1);
        active.Add(start); seen[start.x, start.y] = true;
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        var options = new List<Vector2Int>(4);
        while (active.Count > 0)
        {
            int idx = rng.NextDouble() < branchiness ? rng.Next(active.Count) : active.Count - 1;
            var cur = active[idx];
            options.Clear();
            foreach (var d in dirs)
            {
                var n = cur + d;
                if (n.x >= 0 && n.y >= 0 && n.x < W && n.y < H && !seen[n.x, n.y]) options.Add(d);
            }
            if (options.Count == 0) { active.RemoveAt(idx); continue; }
            var dir = options[rng.Next(options.Count)];
            OpenWall(cur, dir);
            var next = cur + dir;
            seen[next.x, next.y] = true;
            active.Add(next);
        }

        // 남은 안쪽 벽 몇 개를 더 허물어 순환로(여러 갈래 길)를 만든다
        var walls = new List<(bool east, int x, int y)>();
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                if (x < W - 1 && eastWall[x, y]) walls.Add((true, x, y));
                if (y > 0 && southWall[x, y]) walls.Add((false, x, y));
            }
        for (int i = 0; i < extraOpenings && walls.Count > 0; i++)
        {
            int k = rng.Next(walls.Count);
            var w = walls[k];
            if (w.east) eastWall[w.x, w.y] = false; else southWall[w.x, w.y] = false;
            walls.RemoveAt(k);
        }
    }

    void OpenWall(Vector2Int c, Vector2Int d)
    {
        if (d == Vector2Int.right) eastWall[c.x, c.y] = false;
        else if (d == Vector2Int.left) eastWall[c.x - 1, c.y] = false;
        else if (d == Vector2Int.down) southWall[c.x, c.y] = false;
        else southWall[c.x, c.y + 1] = false;
    }

    bool CanMove(Vector2Int c, Vector2Int d)
    {
        var n = c + d;
        if (n.x < 0 || n.y < 0 || n.x >= eastWall.GetLength(0) || n.y >= eastWall.GetLength(1)) return false;
        if (d == Vector2Int.right) return !eastWall[c.x, c.y];
        if (d == Vector2Int.left) return !eastWall[c.x - 1, c.y];
        if (d == Vector2Int.down) return !southWall[c.x, c.y];
        if (d == Vector2Int.up) return !southWall[c.x, c.y + 1];
        return false;
    }

    Vector2 CellLocal(int x, int y)
    {
        int W = eastWall.GetLength(0), H = eastWall.GetLength(1);
        return new Vector2((x + 0.5f) * cellSize - W * cellSize / 2f, (y + 0.5f) * cellSize - H * cellSize / 2f);
    }

    void BuildUI()
    {
        uiFont = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        canvasGo = new GameObject("MirrorMazeUI");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        // [통합] URP에서는 미로 카메라가 화면 위 UI(Overlay)를 덮어 안내·X 버튼이 안 보였음 → 미로 카메라에 붙여 그림 (nicezzomaru 2026-11-03)
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = canvasSortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 뒤쪽 스테이지 UI 클릭을 막는 투명 판
        var blocker = new GameObject("InputBlocker");
        blocker.transform.SetParent(canvasGo.transform, false);
        var brt = blocker.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
        var bimg = blocker.AddComponent<Image>();
        bimg.color = new Color(0, 0, 0, 0);

        MakeText(canvasGo.transform, "Guide", "손거울을 끌어 반대편을 비춰보자   ·   조이스틱: 이동   ·   벽에 3번 부딪히면 실패",
            new Vector2(0.5f, 0f), new Vector2(0, 225), new Vector2(1800, 50), 28, new Color(1f, 1f, 1f, 0.92f));


        // 닫기 버튼 (오른쪽 위)
        var cb = new GameObject("CloseButton");
        cb.transform.SetParent(canvasGo.transform, false);
        closeButton = cb.AddComponent<RectTransform>();
        closeButton.anchorMin = closeButton.anchorMax = new Vector2(1f, 1f);
        closeButton.anchoredPosition = new Vector2(-70, -70);
        closeButton.sizeDelta = new Vector2(84, 84);
        var cbImg = cb.AddComponent<Image>();
        cbImg.color = new Color(0.06f, 0.06f, 0.09f, 0.9f);
        var inner = new GameObject("Inner"); inner.transform.SetParent(cb.transform, false);
        var irt = inner.AddComponent<RectTransform>(); irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = new Vector2(5, 5); irt.offsetMax = new Vector2(-5, -5);
        var iimg = inner.AddComponent<Image>(); iimg.color = new Color(0.82f, 0.82f, 0.85f); iimg.raycastTarget = false;
        var x = MakeText(cb.transform, "X", "X", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84), 52, new Color(0.08f, 0.08f, 0.1f));
        x.fontStyle = FontStyle.Bold;
        x.GetComponent<Shadow>().enabled = false;

        BuildTouchControls();

        popupPanel = new GameObject("Popup");
        popupPanel.transform.SetParent(canvasGo.transform, false);
        var prt = popupPanel.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = new Vector2(0, 250);
        prt.sizeDelta = new Vector2(900, 170);
        var img = popupPanel.AddComponent<Image>();
        img.color = new Color(0.06f, 0.06f, 0.09f, 0.9f);
        img.raycastTarget = false;
        popupText = MakeText(popupPanel.transform, "PopupText", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 160), 38, new Color(0.95f, 0.95f, 0.97f));
        var iconGo = new GameObject("PopupIcon");
        iconGo.transform.SetParent(popupPanel.transform, false);
        var icrt = iconGo.AddComponent<RectTransform>();
        icrt.anchorMin = icrt.anchorMax = new Vector2(0f, 0.5f);
        icrt.anchoredPosition = new Vector2(95, 0);
        icrt.sizeDelta = new Vector2(130, 130);
        popupIcon = iconGo.AddComponent<Image>();
        popupIcon.raycastTarget = false;
        popupIcon.preserveAspect = true;
        iconGo.SetActive(false);
        popupPanel.SetActive(false);
    }

    // [터치] 왼쪽 아래 조이스틱 + 오른쪽 아래 [다시하기]·[나가기] 버튼
    void BuildTouchControls()
    {
        joyBase = MakeCircleUI("JoystickBase", canvasGo.transform, new Vector2(0f, 0f), new Vector2(230, 230), JoyRadius * 2f + 40f, new Color(1f, 1f, 1f, 0.16f));
        var ring = MakeCircleUI("Ring", joyBase, new Vector2(0.5f, 0.5f), Vector2.zero, JoyRadius * 2f + 40f, new Color(1f, 1f, 1f, 0.35f));
        ring.GetComponent<Image>().sprite = ringSprite;
        joyKnob = MakeCircleUI("JoystickKnob", joyBase, new Vector2(0.5f, 0.5f), Vector2.zero, 120f, new Color(0.9f, 0.9f, 0.93f, 0.85f));

        restartButton = MakeButtonUI("RestartButton", "다시하기", new Vector2(-330, 110));
        exitButton = MakeButtonUI("ExitButton", "나가기", new Vector2(-130, 110));
    }

    RectTransform MakeCircleUI(string name, Transform parent, Vector2 anchor, Vector2 pos, float size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite; img.color = color; img.raycastTarget = false;
        return rt;
    }

    RectTransform MakeButtonUI(string name, string label, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(canvasGo.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(180, 90);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.06f, 0.06f, 0.09f, 0.9f);
        var inner = new GameObject("Inner"); inner.transform.SetParent(go.transform, false);
        var irt = inner.AddComponent<RectTransform>(); irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = new Vector2(4, 4); irt.offsetMax = new Vector2(-4, -4);
        var iimg = inner.AddComponent<Image>(); iimg.color = new Color(0.82f, 0.82f, 0.85f); iimg.raycastTarget = false;
        var t = MakeText(go.transform, "Label", label, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 90), 36, new Color(0.08f, 0.08f, 0.1f));
        t.fontStyle = FontStyle.Bold;
        t.GetComponent<Shadow>().enabled = false;
        return rt;
    }

    Text MakeText(Transform parent, string name, string content, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.font = uiFont; t.fontSize = fontSize; t.alignment = TextAnchor.MiddleCenter; t.color = color; t.text = content;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.85f); sh.effectDistance = new Vector2(2, -2);
        return t;
    }

    // =========================================================
    //  업데이트
    // =========================================================
    void Update()
    {
        if (mazeRoot == null || closing) return;
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (kb != null && kb.rKey.wasPressedThisFrame) RestartMaze();

        if (failTimer >= 0f)
        {
            HandleEffects();
            failTimer -= Time.deltaTime;
            if (failTimer < 0f) Fail();
            return;
        }

        HandlePointer();
        if (closing) return;
        HandleMovement();
        HandleEffects();
    }

    void RestartMaze()
    {
        if (solved) return;
        BuildMaze();
        MoveMirrorTo(playerSr.transform.position);
    }

    // [터치] 마우스와 손가락을 함께 처리한다. 손가락마다 번호(touchId)로 따로 추적해서
    // 조이스틱과 손거울을 동시에 잡을 수 있다. 터치 중에는 마우스 입력을 무시한다 (터치가 마우스로도 들어오는 경우 대비).
    void HandlePointer()
    {
        bool touchActive = false;
        var ts = Touchscreen.current;
        if (ts != null)
        {
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed && !t.press.wasPressedThisFrame) continue;
                touchActive = true;
                if (t.press.wasPressedThisFrame && PointerDown(t.touchId.ReadValue(), t.position.ReadValue())) return;
            }
        }

        var mouse = Mouse.current;
        bool useMouse = !touchActive && mouse != null;
        if (useMouse && mouse.leftButton.wasPressedThisFrame && PointerDown(MousePointer, mouse.position.ReadValue())) return;

        // 조이스틱
        if (joyPointerId != NoPointer)
        {
            if (TryGetHeld(joyPointerId, ts, useMouse ? mouse : null, out var p)) UpdateJoystick(p);
            else ReleaseJoystick();
        }

        // 손거울 끌기
        if (dragging)
        {
            if (TryGetHeld(dragPointerId, ts, useMouse ? mouse : null, out var p))
            {
                Vector2 world = cam.ScreenToWorldPoint(new Vector3(p.x, p.y, 10f));
                MoveMirrorTo(world + dragOffset);
            }
            else { dragging = false; dragPointerId = NoPointer; }
        }
    }

    /// <summary>손가락/마우스를 누른 순간. Close 했으면 true</summary>
    bool PointerDown(int id, Vector2 screen)
    {
        if (RectTransformUtility.RectangleContainsScreenPoint(closeButton, screen, cam) ||
            RectTransformUtility.RectangleContainsScreenPoint(exitButton, screen, cam)) { Close(); return true; }
        if (RectTransformUtility.RectangleContainsScreenPoint(restartButton, screen, cam)) { RestartMaze(); return false; }

        if (joyPointerId == NoPointer &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(joyBase, screen, cam, out var jl) &&
            jl.magnitude <= JoyRadius + 60f)
        {
            joyPointerId = id;
            UpdateJoystick(screen);
            return false;
        }

        Vector2 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
        if (keySr != null && keySr.gameObject.activeSelf && keyTakeT < 0f &&
            Vector2.Distance(world, keySr.transform.position) <= 0.75f) { TakeKey(); return false; }
        if (dragging) return false;
        Vector2 mp = mirrorRoot.position;
        Vector2 local = world - mp;
        bool onGlass = local.magnitude <= mirrorRadius + 0.3f;
        bool onHandle = Mathf.Abs(local.x) <= 0.35f && local.y < -mirrorRadius && local.y > -mirrorRadius - 1.9f;
        if (onGlass || onHandle) { dragging = true; dragPointerId = id; dragOffset = mp - world; }
        return false;
    }

    static bool TryGetHeld(int id, Touchscreen ts, Mouse mouse, out Vector2 screen)
    {
        screen = default;
        if (id == MousePointer)
        {
            if (mouse == null || !mouse.leftButton.isPressed) return false;
            screen = mouse.position.ReadValue();
            return true;
        }
        if (ts == null) return false;
        foreach (var t in ts.touches)
        {
            if (!t.press.isPressed || t.touchId.ReadValue() != id) continue;
            screen = t.position.ReadValue();
            return true;
        }
        return false;
    }

    void UpdateJoystick(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(joyBase, screen, cam, out var local);
        local = Vector2.ClampMagnitude(local, JoyRadius);
        joyKnob.anchoredPosition = local;
        if (local.magnitude < JoyRadius * JoyDeadZone) { joyDir = Vector2Int.zero; return; }
        if (Mathf.Abs(local.x) >= Mathf.Abs(local.y)) joyDir = local.x > 0 ? Vector2Int.right : Vector2Int.left;
        else joyDir = local.y > 0 ? Vector2Int.up : Vector2Int.down;
    }

    void ReleaseJoystick()
    {
        joyPointerId = NoPointer;
        joyDir = Vector2Int.zero;
        joyKnob.anchoredPosition = Vector2.zero;
    }

    void MoveMirrorTo(Vector2 target)
    {
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect, m = mirrorRadius + 0.3f;
        Vector2 o = Origin;
        target.x = Mathf.Clamp(target.x, o.x - halfW + m, o.x + halfW - m);
        target.y = Mathf.Clamp(target.y, o.y - halfH + m, o.y + halfH - m);
        mirrorRoot.position = new Vector3(target.x, target.y, 0);
    }

    void HandleMovement()
    {
        if (solved) return;
        var kb = Keyboard.current;
        Vector2Int dir = Vector2Int.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir = Vector2Int.up;
            else if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir = Vector2Int.down;
            else if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir = Vector2Int.left;
            else if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir = Vector2Int.right;
        }
        if (dir == Vector2Int.zero) dir = joyDir; // [터치] 조이스틱
        if (dir == Vector2Int.zero) { heldDir = Vector2Int.zero; return; }

        bool fresh = dir != heldDir, fire = false;
        if (fresh) { heldDir = dir; holdTimer = 0.25f; fire = true; }
        else { holdTimer -= Time.deltaTime; if (holdTimer <= 0f) { holdTimer = 0.1f; fire = true; } }
        if (!fire) return;

        // 미로는 좌우 반전되어 있으므로 x를 뒤집어야 화면 방향과 일치
        Vector2Int mazeDir = dir;
        if (dir.x != 0 && !invertHorizontalControls) mazeDir.x = -dir.x;

        if (CanMove(playerCell, mazeDir))
        {
            playerCell += mazeDir;
            if (!visited[playerCell.x, playerCell.y])
            {
                visited[playerCell.x, playerCell.y] = true;
                var dot = MakeRect("Trail", mazeRoot, CellLocal(playerCell.x, playerCell.y), Vector2.one * cellSize * 0.18f, new Color(1, 1, 1, 0.45f), OrderTrail, true);
                dot.sprite = circleSprite;
            }
            if (playerCell == goalCell) ShowSolvedState(true);
        }
        else
        {
            bumpShake = 0.15f;
            if (fresh) Bump();
        }
    }

    void Bump()
    {
        bumpCount++;
        flashTimer = 0.35f;
        if (bumpCount >= Mathf.Max(1, maxBumps)) failTimer = 0.5f; // 잠깐 붉게 번쩍인 뒤 닫힘
    }

    void ShowSolvedState(bool fireEvents)
    {
        solved = true;
        playerCell = goalCell;
        goalSr.gameObject.SetActive(false);
        popupPanel.SetActive(true);

        if (!RewardTaken)
        {
            // 미로를 빠져나오면 선반 위로 전화부스 열쇠가 떨어진다
            SpawnKey(drop: fireEvents);
            ShowPopup("거울 속 미로를 빠져나왔다…\n<size=28>선반 위에 무언가 떨어졌다</size>", null);
        }
        else ShowPopup("거울 속 미로를 빠져나왔다…\n<size=24>[나가기] / X : 돌아가기</size>", null);

        if (!fireEvents) return;
        IsSolved = true;
        LastClue = clueCode;
        onSolved?.Invoke(clueCode);
        Solved?.Invoke(clueCode);
        Debug.Log("[MirrorMazeGimmick] 클리어!");
    }

    void ShowPopup(string text, Sprite icon)
    {
        popupPanel.SetActive(true);
        popupText.text = text;
        var prt = popupText.rectTransform;
        if (icon != null)
        {
            popupIcon.sprite = icon;
            popupIcon.gameObject.SetActive(true);
            prt.anchoredPosition = new Vector2(70, 0);
        }
        else
        {
            popupIcon.gameObject.SetActive(false);
            prt.anchoredPosition = Vector2.zero;
        }
    }

    Sprite GetKeySprite()
    {
        if (keySprite != null) return keySprite;
        if (rewardIcon != null) return keySprite = rewardIcon;
        var tex = Resources.Load<Texture2D>("MirrorMaze_BoothKey");
        if (tex != null) keySprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.height / 1.6f); // 화면에서 높이 1.6 유닛
        return keySprite;
    }

    void SpawnKey(bool drop)
    {
        if (keySr != null) return;
        var go = new GameObject("BoothKey (보상)");
        go.transform.SetParent(transform, false);
        keySr = go.AddComponent<SpriteRenderer>();
        keySr.sprite = GetKeySprite();
        if (keySr.sprite == null) { keySr.sprite = circleSprite; keySr.color = new Color(0.75f, 0.75f, 0.78f); go.transform.localScale = Vector3.one * 0.5f; }
        keySr.sortingOrder = OrderKey;
        // 반짝임 (줍기 전까지)
        var glint = new GameObject("Glint");
        glint.transform.SetParent(go.transform, false);
        glint.transform.localPosition = new Vector3(-0.12f, 0.28f, 0);
        keyGlintSr = glint.AddComponent<SpriteRenderer>();
        keyGlintSr.sprite = circleSprite;
        keyGlintSr.sortingOrder = OrderKey + 1;
        keyDropT = drop ? 0f : -1f;
        go.transform.localPosition = drop ? new Vector3(keyRestLocal.x, 1.2f, 0) : (Vector3)keyRestLocal;
    }

    void TakeKey()
    {
        var result = MirrorMazeReward.Give(rewardItemId, rewardName, GetKeySprite(), rewardItemAsset);
        if (result == MirrorMazeReward.Result.InventoryFull)
        {
            ShowPopup("가방이 가득 찼다…\n<size=28>퀵슬롯을 비우고 다시 줍자</size>", GetKeySprite());
            return;
        }
        RewardTaken = true;
        keyTakeT = 0f;
        ShowPopup($"<color=#ff5a66>{rewardName}</color>을(를) 얻었다.\n<size=24>[나가기] / X : 돌아가기</size>", GetKeySprite());
        onRewardObtained?.Invoke(rewardItemId);
        RewardObtained?.Invoke(rewardItemId);
    }

    void AnimateKey()
    {
        if (keySr == null) return;
        var tr = keySr.transform;
        if (keyDropT >= 0f)
        {
            // 위에서 떨어져 한 번 튕기고 멈춤
            keyDropT += Time.deltaTime;
            float t = keyDropT, y;
            if (t < 0.45f) y = Mathf.Lerp(1.2f, keyRestLocal.y, (t / 0.45f) * (t / 0.45f));
            else if (t < 0.7f) { float u = (t - 0.45f) / 0.25f; y = keyRestLocal.y + Mathf.Sin(u * Mathf.PI) * 0.25f; }
            else { y = keyRestLocal.y; keyDropT = -1f; }
            tr.localPosition = new Vector3(keyRestLocal.x, y, 0);
            tr.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 20f) * 8f * Mathf.Clamp01(1f - t / 0.7f));
        }
        if (keyTakeT >= 0f)
        {
            // 주우면 살짝 떠오르며 사라짐
            keyTakeT += Time.deltaTime;
            float u = Mathf.Clamp01(keyTakeT / 0.4f);
            tr.localPosition = (Vector3)keyRestLocal + new Vector3(0, u * 0.6f, 0);
            tr.localScale = Vector3.one * (1f + u * 0.3f);
            keySr.color = new Color(1, 1, 1, 1f - u);
            keyGlintSr.color = new Color(1, 1, 1, 0);
            if (u >= 1f) { keySr.gameObject.SetActive(false); keyTakeT = -1f; }
            return;
        }
        // 반짝임
        float g = Mathf.Repeat(Time.time, 1.6f) / 1.6f;
        float a = g < 0.25f ? Mathf.Sin(g / 0.25f * Mathf.PI) : 0f;
        keyGlintSr.color = new Color(1, 1, 1, a * 0.9f);
        keyGlintSr.transform.localScale = Vector3.one * (0.08f + a * 0.12f);
    }

    void HandleEffects()
    {
        AnimateKey();
        float t = Time.time;
        if (goalSr != null && goalSr.gameObject.activeSelf)
            goalSr.transform.localScale = Vector3.one * cellSize * (0.5f + 0.08f * Mathf.Sin(t * 5f));

        Vector2 basePos = CellLocal(playerCell.x, playerCell.y);
        if (bumpShake > 0f) { bumpShake -= Time.deltaTime; basePos += UnityEngine.Random.insideUnitCircle * 0.05f; }
        playerSr.transform.localPosition = basePos;

        if (flashTimer > 0f) flashTimer -= Time.deltaTime;
        glassSr.color = Color.Lerp(TileNavy, new Color(0.75f, 0.05f, 0.08f), Mathf.Clamp01(flashTimer / 0.35f) * 0.8f);

        if (!enableEyes || eyesRoot == null) return;
        if (eyesShowTimer > 0f)
        {
            eyesShowTimer -= Time.deltaTime;
            if (eyesShowTimer <= 0f) eyesRoot.gameObject.SetActive(false);
        }
        else if ((eyesTimer -= Time.deltaTime) <= 0f)
        {
            eyesTimer = UnityEngine.Random.Range(7f, 14f);
            Vector2 r = UnityEngine.Random.insideUnitCircle * mirrorRadius * 0.55f;
            eyesRoot.position = mirrorRoot.position + new Vector3(r.x, r.y, 0);
            eyesRoot.gameObject.SetActive(true);
            eyesShowTimer = 0.45f;
        }
    }

    void Fail()
    {
        Debug.Log("[MirrorMazeGimmick] 벽에 너무 많이 부딪혀 실패");
        onFailed?.Invoke();
        Failed?.Invoke();
        Close();
    }

    /// <summary>기믹 창을 닫고 원래 화면으로 돌아간다.</summary>
    public void Close()
    {
        if (closing) return;
        closing = true;
        LastClosedFrame = Time.frameCount;
        onClosed?.Invoke();
        Closed?.Invoke();
        Destroy(gameObject);
    }

    // =========================================================
    //  유틸
    // =========================================================
    SpriteRenderer MakeRect(string name, Transform parent, Vector2 localPos, Vector2 size, Color color, int order, bool masked)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = color;
        sr.sortingOrder = order;
        sr.maskInteraction = masked ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
        return sr;
    }

    static Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    static Sprite MakeCircleSprite(int res) => MakeArcSprite(res, 0f, 0f, 360f);
    static Sprite MakeRingSprite(int res, float innerRatio) => MakeArcSprite(res, innerRatio, 0f, 360f);

    /// <summary>원/링/호 스프라이트 (크기 1유닛). innerRatio: 안쪽 반지름 비율, 각도는 도 단위(0=오른쪽, 반시계)</summary>
    static Sprite MakeArcSprite(int res, float innerRatio, float fromDeg, float toDeg)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[res * res];
        float r = res / 2f, ri = r * innerRatio;
        bool full = toDeg - fromDeg >= 360f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r, d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d);
                if (innerRatio > 0f) a *= Mathf.Clamp01(d - ri);
                if (!full)
                {
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; if (ang < 0) ang += 360f;
                    if (ang < fromDeg || ang > toDeg) a = 0f;
                }
                px[y * res + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
