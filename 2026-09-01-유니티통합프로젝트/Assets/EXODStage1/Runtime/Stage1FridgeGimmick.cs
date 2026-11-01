using System.Collections;
using UnityEngine;

/// <summary>
/// 주방 냉장고 기믹을 맡습니다.
/// - 냉장고 메모를 누르면 메모 대사 (현관 비밀번호의 순서 단서)
/// - 냉장고(키패드)를 누르면 잠금 대사 후 숫자 키패드가 열립니다.
/// - 곰돌이 쪽지의 비밀번호(Stage1Dialogues.FridgePassword)를 넣으면 냉장고가 열리고 과일 봉지 대사가 나옵니다.
/// 클릭 영역은 주방 배경 그림 위의 위치(비율)에 맞춰 실행 중에 만들어집니다.
/// </summary>
[DefaultExecutionOrder(260)]
public sealed class Stage1FridgeGimmick : MonoBehaviour
{
    // 주방 배경 그림(1920x1080) 기준 위치. (왼쪽 아래 x, y, 오른쪽 위 x, y) 비율
    private static readonly Rect FridgeArea = Rect.MinMaxRect(0.409f, 0.208f, 0.600f, 0.767f);
    private static readonly Rect MemoArea = Rect.MinMaxRect(0.470f, 0.575f, 0.515f, 0.650f);
    private static readonly Rect KeypadArea = Rect.MinMaxRect(0.553f, 0.550f, 0.588f, 0.628f);

    private bool isOpened;

    private IEnumerator Start()
    {
        // SliderManager가 방 배경 화면들을 만든 뒤에 찾습니다.
        yield return null;

        RectTransform kitchen = Stage1Room.FindRoomView("주방");
        if (kitchen == null)
        {
            Debug.LogWarning("[Stage1Fridge] 주방 배경 화면을 찾을 수 없어 냉장고 기믹을 만들지 못했습니다.", this);
            yield break;
        }

        Stage1Room.CreateHotspot(kitchen, "냉장고 클릭 영역", FridgeArea, OnFridgeClicked);
        Stage1Room.CreateHotspot(kitchen, "냉장고 키패드 클릭 영역", KeypadArea, OnFridgeClicked);
        Stage1Room.CreateHotspot(kitchen, "냉장고 메모 클릭 영역", MemoArea,
            () => Stage1Room.PlayCutscene(Stage1Dialogues.FridgeMemo()));
    }

    private void OnFridgeClicked()
    {
        if (isOpened)
        {
            Stage1Room.PlayCutscene(Stage1Dialogues.FridgeAlreadyOpen());
            return;
        }

        // 틀리면 키패드에서 다시 입력합니다. (벌칙 없음)
        Stage1Room.PlayCutscene(Stage1Dialogues.FridgeLocked(), () =>
            Stage1Keypad.Show(Stage1Dialogues.FridgePassword, OnUnlocked));
    }

    private void OnUnlocked()
    {
        isOpened = true;
        Stage1Room.PlayCutscene(Stage1Dialogues.FridgeOpened());
    }
}
