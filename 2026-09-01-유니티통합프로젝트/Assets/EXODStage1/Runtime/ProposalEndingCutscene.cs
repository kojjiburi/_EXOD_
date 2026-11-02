using UnityEngine;

/// <summary>
/// 청혼 엔딩 연출 (nicezzomaru 담당 자리, 2026-11-02)
/// 화장실 금고의 청혼반지를 집으면 RestroomGimmick이 ProposalEnding.Trigger()를 부르고, 여기서:
///  컷신(Stage1Dialogues.ProposalCutscene) → 암전 → 여자친구 마지막 대사(대화창 위) →
///  엔딩 화면 "청혼 엔딩 : 해피 엔딩…?" (EndingScreen, 결혼식 분위기: 꽃잎·하트·오르골)
/// </summary>
public static class ProposalEndingCutscene
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Register()
    {
        // ProposalEnding.Requested는 SubsystemRegistration에서 비워지므로, 그 뒤에 연결합니다.
        ProposalEnding.Requested -= Play;
        ProposalEnding.Requested += Play;
    }

    private static void Play()
    {
        Stage1Room.PlayCutscene(Stage1Dialogues.ProposalCutscene(), () =>
        {
            if (PlayerFaintController.Instance != null)
                PlayerFaintController.Instance.ShowEnding(
                    Stage1Dialogues.ProposalEndingLine,
                    Stage1Dialogues.ProposalEndingTitle,
                    Stage1Dialogues.ProposalEndingSubtitle,
                    EndingStyle.Proposal);
        });
    }
}
