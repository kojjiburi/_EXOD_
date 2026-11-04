using System;
using UnityEngine;

/// <summary>
/// 청혼 엔딩 호출 자리
/// - 화장실 금고 안의 청혼반지(proposal_ring)를 주우면 RestroomGimmick이 ProposalEnding.Trigger()를 부릅니다.
/// - 엔딩 연출은 nicezzomaru 담당: Requested 이벤트에 연출을 연결하세요.
///   예) ProposalEnding.Requested += () => PlayerFaintController.Instance.ShowEnding(...);
/// - 아직 아무도 연결하지 않았으면 임시 대사만 띄웁니다.
/// </summary>
public static class ProposalEnding
{
    public const string RingItemId = "proposal_ring";

    /// <summary>청혼 엔딩을 띄워야 할 때 호출됩니다. (엔딩 연출을 여기에 연결)</summary>
    public static event Action Requested;

    public static bool HasPlayed { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Requested = null;
        HasPlayed = false;
    }

    public static void Trigger()
    {
        HasPlayed = true;
        EndingState.Begin();
        Debug.Log("[Ending] 청혼 엔딩 호출");

        if (Requested != null)
        {
            Requested.Invoke();
            return;
        }

        // 엔딩 연출이 아직 연결되지 않았을 때의 임시 대사
        DialogueManager.Instance?.StartDialogue(Stage1Dialogues.Thought("(임시) 청혼 엔딩 자리입니다. 엔딩 연출이 아직 연결되지 않았습니다."));
    }
}
