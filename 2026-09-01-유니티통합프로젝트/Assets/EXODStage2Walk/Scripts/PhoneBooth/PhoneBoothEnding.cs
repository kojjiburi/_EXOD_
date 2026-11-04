using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EXOD.Walk
{
    /// <summary>
    /// 전화 부스 기믹의 탈출 엔딩.
    /// 신고에 성공하면 대기하다가, 하루가 끝나 다음 날이 되는 순간(DayClock.DayAdvanced) 집에서 엔딩이 나온다:
    /// 초인종 → 경찰 도착 대사 → 화면이 어두워지고 "탈출 엔딩" 화면.
    /// 산책 중에 날이 바뀌었으면 산책이 끝나 집으로 돌아온 뒤에 나온다.
    ///
    /// 팀 엔딩과 연결하려면 PhoneBoothProgress.EndingReached 이벤트를 쓰거나,
    /// PhoneBooth의 endingSceneName에 엔딩 씬 이름을 넣는다.
    /// 씬이 바뀌어도 유지된다(DontDestroyOnLoad). PhoneBooth가 알아서 만든다.
    /// </summary>
    public class PhoneBoothEnding : MonoBehaviour
    {
        static PhoneBoothEnding instance;

        WalkLine[] lines;
        string title;
        string subtitle;
        string sceneName;
        bool pending;
        bool playing;
        float readyTimer;

        /// <summary>
        /// [통합] 엔딩 화면 버튼 동작. 비워 두면 기본 동작(지금 씬 다시 불러오기 / 시작 화면)을 쓴다.
        /// 통합 프로젝트에서는 Stage2HouseLink가 팀 게임 오버 화면과 같은 동작(진행 상황 초기화 포함)으로 연결한다.
        /// </summary>
        /// <summary>탈출 엔딩 연출·화면이 나오는 중인지 (팀 [종료] 버튼 숨김용)</summary>
        public static bool IsShowing => instance != null && instance.playing;

        /// <summary>
        /// [통합] 엔딩 화면을 대신 띄우는 쪽 (제목, 설명). true를 돌려주면 이 스크립트의 기본 화면은 만들지 않는다.
        /// 통합 프로젝트에서는 Stage2HouseLink가 팀 공용 엔딩 화면(EndingScreen)으로 연결한다.
        /// </summary>
        public static System.Func<string, string, bool> ScreenOverride;

        /// <summary>
        /// [통합] 엔딩 직전 대사를 대신 보여 주는 쪽 (끝나면 받은 콜백을 부른다). true를 돌려주면 endingLines는 쓰지 않는다.
        /// 통합 프로젝트에서는 Stage2HouseLink가 팀 컷신(Stage1Dialogues.EscapeCutscene)으로 연결한다.
        /// </summary>
        public static System.Func<System.Action, bool> CutsceneOverride;

        public static System.Action RestartAction;
        public static System.Action LobbyAction;
        const string LobbySceneName = "StartScenes";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            ScreenOverride = null;
            CutsceneOverride = null;
            RestartAction = null;
            LobbyAction = null;
        }

        /// <summary>엔딩 대기 시작 (이미 대기 중이면 설정만 바꾼다)</summary>
        public static void Arm(WalkLine[] endingLines, string endingTitle, string endingSubtitle, string endingScene)
        {
            if (PhoneBoothProgress.EndingShown) return;
            if (instance == null)
            {
                var go = new GameObject("PhoneBoothEnding (신고 성공 → 다음 날 탈출 엔딩)");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<PhoneBoothEnding>();
            }
            instance.lines = endingLines;
            instance.title = endingTitle;
            instance.subtitle = endingSubtitle;
            instance.sceneName = endingScene;
        }

        /// <summary>테스트용: 하루가 끝나기를 기다리지 않고 바로 엔딩을 띄운다</summary>
        public static void TriggerNow()
        {
            if (instance != null) instance.pending = true;
        }

        void OnEnable() => DayClock.DayAdvanced += OnDayAdvanced;
        void OnDisable() => DayClock.DayAdvanced -= OnDayAdvanced;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>
        /// [통합] 집(1스테이지)에서 일차가 넘어갔을 때 알려 준다. 통합 프로젝트에서는 집에 있는 동안 DayClock이 없어서
        /// DayClock.DayAdvanced가 오지 않으므로 Stage2HouseLink가 GManager.onDayChanged를 이리로 넘겨준다.
        /// </summary>
        public static void NotifyDayAdvanced(int day)
        {
            if (instance != null) instance.OnDayAdvanced(day);
        }

        void OnDayAdvanced(int day)
        {
            if (PhoneBoothProgress.Reported && !PhoneBoothProgress.EndingShown)
            {
                pending = true;
                Debug.Log($"[PhoneBooth] DAY {day}: 신고한 다음 날 → 집으로 돌아오면 탈출 엔딩");
            }
        }

        void Update()
        {
            if (!pending || playing) return;

            // 산책 씬이 닫히고, 화면이 밝아지고, 대화창이 닫힌 뒤에 시작한다
            bool ready = WalkGameManager.Current == null && !WalkDialogue.IsOpen && !WalkFade.IsDark;
            if (!ready) { readyTimer = 0f; return; }
            readyTimer += Time.unscaledDeltaTime;
            if (readyTimer < 0.6f) return;

            StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            playing = true;
            pending = false;
            if (DayClock.Instance != null) DayClock.Instance.Paused = true;
            Debug.Log("[PhoneBooth] 탈출 엔딩 시작");

            bool cutsceneDone = false;
            if (CutsceneOverride != null && CutsceneOverride(() => cutsceneDone = true))
            {
                while (!cutsceneDone) yield return null;
            }
            else
            {
                yield return WalkDialogue.ShowAndWait(lines);
            }
            yield return WalkFade.FadeOut(1f);

            if (ScreenOverride != null && ScreenOverride(title, subtitle))
            {
                // 팀 엔딩 화면이 대신 나옴 → 산책용 암전은 걷어 낸다
                yield return WalkFade.FadeIn(0.05f);
                playing = false;
            }
            else
            {
                ShowEndingScreen();
            }
            PhoneBoothProgress.RaiseEnding();

            if (!string.IsNullOrEmpty(sceneName))
            {
                yield return new WaitForSecondsRealtime(3f);
                if (Application.CanStreamedLevelBeLoaded(sceneName)) SceneManager.LoadScene(sceneName);
                else Debug.LogWarning($"[PhoneBooth] 엔딩 씬 '{sceneName}'을(를) 불러올 수 없습니다. Build Profiles의 씬 목록에 추가했는지 확인하세요.");
            }
        }

        void ShowEndingScreen()
        {
            Canvas canvas = WalkUi.CreateCanvas("EndingCanvas", 1001, transform);   // 페이드(1000)보다 위
            var black = WalkUi.CreateImage("검은 화면", canvas.transform, null, Color.black);
            WalkUi.Stretch(black.rectTransform);
            black.raycastTarget = true;

            var t = WalkUi.CreateText("엔딩 제목", canvas.transform, 96, new Color(0.85f, 0.1f, 0.1f), TextAnchor.MiddleCenter);
            WalkUi.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1600f, 140f));
            t.text = title;

            var s = WalkUi.CreateText("엔딩 설명", canvas.transform, 40, Color.white, TextAnchor.MiddleCenter);
            WalkUi.Place(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1600f, 80f));
            s.text = subtitle;

            // 다른 엔딩·게임 오버 화면과 같은 세 버튼
            CreateButton(canvas.transform, "다시 시작", new Vector2(-380f, -220f), Restart);
            CreateButton(canvas.transform, "로비로", new Vector2(0f, -220f), Lobby);
            CreateButton(canvas.transform, "게임 종료", new Vector2(380f, -220f), Quit);
        }

        static void CreateButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var image = WalkUi.CreateImage(label, parent, null, Color.white);
            image.raycastTarget = true;
            WalkUi.Place(image.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(340f, 84f));

            var button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.20f, 0.14f, 0.14f, 1f);
            colors.highlightedColor = new Color(0.45f, 0.16f, 0.20f, 1f);
            colors.pressedColor = new Color(0.62f, 0.20f, 0.26f, 1f);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            var text = WalkUi.CreateText("글자", image.transform, 34, Color.white, TextAnchor.MiddleCenter);
            WalkUi.Stretch(text.rectTransform);
            text.text = label;
        }

        void Restart()
        {
            Destroy(gameObject);   // 씬을 넘어가도 남는 엔딩 화면을 먼저 지운다
            if (RestartAction != null) { RestartAction(); return; }
            PhoneBoothProgress.ResetAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void Lobby()
        {
            Destroy(gameObject);
            if (LobbyAction != null) { LobbyAction(); return; }
            PhoneBoothProgress.ResetAll();
            if (Application.CanStreamedLevelBeLoaded(LobbySceneName)) SceneManager.LoadScene(LobbySceneName);
            else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
