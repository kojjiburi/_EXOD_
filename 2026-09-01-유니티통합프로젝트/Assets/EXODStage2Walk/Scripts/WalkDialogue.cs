using System;
using System.Collections;
using System.Collections.Generic;

namespace EXOD.Walk
{
    /// <summary>대사 한 줄. speaker를 비우면 이름표 없이 나온다(나레이션).</summary>
    [Serializable]
    public class WalkLine
    {
        public string speaker = "";
        [UnityEngine.TextArea(2, 4)] public string text = "";

        public WalkLine() { }

        public WalkLine(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }

    /// <summary>
    /// 산책 대사 출력.
    ///  - 통합 프로젝트: Stage2HouseLink가 ExternalShow를 연결해 두므로 1스테이지와 똑같은 팀 DialogueManager 대화창으로 나온다.
    ///  - 스테이지2 단독 프로젝트: 팀 대화창과 같은 이미지·폰트·배치로 만든 WalkDialogueBox로 나온다.
    /// </summary>
    public static class WalkDialogue
    {
        /// <summary>대사를 대신 보여줄 쪽. 보여줬으면 true.</summary>
        public static Func<IReadOnlyList<WalkLine>, bool> ExternalShow;
        /// <summary>대신 보여주는 쪽의 대화창이 열려 있는지</summary>
        public static Func<bool> ExternalIsOpen;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ExternalShow = null;
            ExternalIsOpen = null;
        }

        /// <summary>대화창이 떠 있거나 이번 프레임에 닫혔으면 true (이때 클릭은 산책 화면 클릭으로 치지 않는다)</summary>
        public static bool BlocksWorldClick
        {
            get
            {
                if (IsOpen) return true;
                var box = WalkDialogueBox.Instance;
                return box != null && box.ClosedFrame == UnityEngine.Time.frameCount;
            }
        }

        public static bool IsOpen
        {
            get
            {
                if (ExternalIsOpen != null && ExternalIsOpen()) return true;
                return WalkDialogueBox.Instance != null && WalkDialogueBox.Instance.IsOpen;
            }
        }

        public static void Show(IReadOnlyList<WalkLine> lines)
        {
            if (lines == null || lines.Count == 0) return;
            if (ExternalShow != null && ExternalShow(lines)) return;
            WalkDialogueBox.Ensure().Show(lines);
        }

        public static void Show(string speaker, string text)
        {
            Show(new[] { new WalkLine(speaker, text) });
        }

        /// <summary>대사를 보여주고 대화창이 닫힐 때까지 기다린다 (코루틴에서 yield return)</summary>
        public static IEnumerator ShowAndWait(IReadOnlyList<WalkLine> lines)
        {
            if (lines == null || lines.Count == 0) yield break;
            Show(lines);
            yield return null;
            while (IsOpen) yield return null;
        }
    }
}
