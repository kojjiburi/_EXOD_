using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책 화면이 1스테이지와 같은 모양이 되도록 팀 UI 에셋을 모아 둔 설정.
    /// Resources/WalkUiTheme.asset 하나만 있으면 되고, 비어 있는 칸은 기본 모양으로 대신한다.
    /// 들어 있는 에셋(Galmuri9 폰트, 대화창, 이름표)은 팀 프로젝트와 GUID가 같아서 합친 뒤에도 그대로 연결된다.
    /// </summary>
    [CreateAssetMenu(fileName = "WalkUiTheme", menuName = "EXOD/Stage 2 산책/UI 테마")]
    public class WalkUiTheme : ScriptableObject
    {
        [Tooltip("팀 폰트 Galmuri9.ttf")]
        public Font font;
        [Tooltip("팀 대화창 이미지 (방탈출 대화창_0)")]
        public Sprite dialogueBox;
        [Tooltip("팀 이름표 이미지 (방탈출 이름표_0)")]
        public Sprite nameBox;
    }
}
