using UnityEngine;
using UnityEngine.UI;

namespace EXOD.Walk
{
    /// <summary>
    /// 스테이지2 단독 프로젝트(EXOD_newstage2) 전용.
    /// 1스테이지 상단 UI(팀 프리팹 EXOD_PlayerStatusSystem: 상단 바, DAY, 하트 5개, 위험도 아이콘)를
    /// 같은 이미지·위치·크기로 똑같이 보여준다. 씬에 넣을 필요 없이 실행하면 자동으로 생긴다.
    ///
    /// 통합 프로젝트에는 진짜 팀 UI가 있으므로, 이 파일(Assets/TeamShared)은 합칠 때 옮겨지지 않는다.
    /// 이미지는 Assets/TeamShared/Resources/TeamUI 에 팀 원본과 GUID까지 같게 복사해 두었다.
    ///
    /// 표시 값:
    ///  - DAY: DayClock의 일차 (3분마다, 침대로 +1)
    ///  - 하트: HP (기본 5) — 테스트할 때 TeamStatusBarPreview.HP 로 바꿀 수 있다
    ///  - 위험도: Emergence (기본 0) — 팀 Yan과 같은 기준 (30 이하 안전, 50 이하 보통, 그 위 위험)
    /// </summary>
    public class TeamStatusBarPreview : MonoBehaviour
    {
        public static TeamStatusBarPreview Instance { get; private set; }

        public static int HP = 5;
        public static int MaxHP = 5;
        public static int Emergence = 0;
        public static bool YanAppeared = false;

        // 팀 프리팹(EXOD_PlayerStatusSystem)의 값 그대로 (1920×1080 기준, 왼쪽 아래 기준 좌표)
        static readonly Vector2 BarPosition = new Vector2(552.8379f, 995.7634f);
        static readonly Vector2 BarSize = new Vector2(1103.76f, 164.16f);
        static readonly float[] HeartX = { 362.76343f, 489.12158f, 615.47974f, 741.8445f, 868.20264f };
        const float IconY = 997.91895f;
        static readonly Vector2 HeartSize = new Vector2(106.974f, 99.414f);
        static readonly Vector2 YanPosition = new Vector2(998f, 997.91895f);
        static readonly Vector2 YanSize = new Vector2(116.64f, 111.24f);
        // DAY는 상단 바의 첫 번째 칸(왼쪽 칸) 가운데. 하트와 같은 왼쪽 위 기준이라 화면 비율이 바뀌어도 칸을 벗어나지 않는다.
        static readonly Vector2 DayPosition = new Vector2(135f, -84f);   // 왼쪽 위 기준
        static readonly Vector2 DaySize = new Vector2(230f, 120f);
        const int DayFontSize = 52;

        Sprite heartFull, heartEmpty, safeSprite, normalSprite, dangerSprite, appearedSprite;
        readonly Image[] hearts = new Image[5];
        Image yanIcon;
        Text dayText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            HP = 5;
            MaxHP = 5;
            Emergence = 0;
            YanAppeared = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateOnStart()
        {
            if (Instance != null) return;
            var go = new GameObject("TeamStatusBarPreview (1스테이지 상단 UI)");
            DontDestroyOnLoad(go);
            go.AddComponent<TeamStatusBarPreview>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DayClock.ExternalDayUiShown = true;   // DAY는 여기서 보여주므로 DayClock 상자는 남은 시간만
            LoadSprites();
            Build();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            DayClock.ExternalDayUiShown = false;
        }

        void LoadSprites()
        {
            heartFull = LoadSprite("HeartFull");
            heartEmpty = LoadSprite("HeartEmpty");
            safeSprite = LoadSprite("2");
            normalSprite = LoadSprite("1");
            dangerSprite = LoadSprite("3");
            appearedSprite = LoadSprite("4");
        }

        static Sprite LoadSprite(string file, string spriteName = null)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>("TeamUI/" + file);
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogWarning($"[TeamStatusBarPreview] Resources/TeamUI/{file} 이미지를 찾지 못했습니다.");
                return null;
            }
            if (spriteName != null)
            {
                foreach (Sprite s in sprites)
                    if (s.name == spriteName) return s;
            }
            return sprites[0];
        }

        void Build()
        {
            Canvas canvas = WalkUi.CreateCanvas("Canvas", 600, transform);   // 팀 캔버스와 같은 정렬 순서

            Icon("QuickSlot_0_UI", canvas.transform, LoadSprite("QuickSlot", "QuickSlot_0"), BarPosition, BarSize);
            for (int i = 0; i < hearts.Length; i++)
                hearts[i] = Icon($"Heart{i + 1}_UI", canvas.transform, heartFull, new Vector2(HeartX[i], IconY), HeartSize);
            yanIcon = Icon("Yan_UI", canvas.transform, safeSprite, YanPosition, YanSize);

            dayText = WalkUi.CreateText("DAY", canvas.transform, DayFontSize, Color.white, TextAnchor.MiddleCenter);
            WalkUi.Place(dayText.rectTransform, new Vector2(0f, 1f), DayPosition, DaySize);
            dayText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        /// <summary>팀 프리팹은 왼쪽 아래 기준 좌표라, 화면 높이 1080 기준으로 왼쪽 위 기준으로 바꿔 놓는다</summary>
        static Image Icon(string name, Transform parent, Sprite sprite, Vector2 bottomLeftPosition, Vector2 size)
        {
            Image image = WalkUi.CreateImage(name, parent, sprite, new Color(1f, 1f, 1f, 0f));
            image.preserveAspect = true;
            WalkUi.Place(image.rectTransform, new Vector2(0f, 1f),
                new Vector2(bottomLeftPosition.x, bottomLeftPosition.y - WalkUi.ReferenceResolution.y), size);
            return image;
        }

        void LateUpdate()
        {
            int day = DayClock.Instance != null ? DayClock.Instance.CurrentDay : 1;
            dayText.text = "DAY " + day;   // 팀 DayText와 같은 형식

            int hp = Mathf.Clamp(HP, 0, MaxHP);
            for (int i = 0; i < hearts.Length; i++)
                hearts[i].sprite = i < hp ? heartFull : heartEmpty;

            // 팀 Yan.UpdateState와 같은 기준
            Sprite state;
            if (YanAppeared) state = appearedSprite;
            else if (Emergence <= 30) state = safeSprite;
            else if (Emergence <= 50) state = normalSprite;
            else state = dangerSprite;
            yanIcon.sprite = state;
        }
    }
}
