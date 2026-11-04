// 스테이지 화면의 "거울" 오브젝트에 붙이는 스크립트. 클릭하면 [거울에 비친 단서] 기믹이 열린다.
//
// 사용법 (셋 중 아무거나):
//  1) 스프라이트 오브젝트 + Collider2D(Box/Circle 등)에 이 컴포넌트를 붙인다 → 클릭하면 자동으로 열림
//  2) UI Image(Raycast Target 켜짐)에 붙인다 → EventSystem이 있으면 클릭하면 열림
//  3) UI Button의 OnClick에 MirrorMazeTrigger.OpenGimmick()을 연결한다
//
// 기믹이 열려 있는 동안은 MirrorMazeGimmick.IsOpen 이 true 이므로,
// 스테이지의 다른 클릭 스크립트에서 if (MirrorMazeGimmick.IsOpen) return; 으로 막아주면 안전하다.

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class MirrorMazeTrigger : MonoBehaviour, IPointerClickHandler
{
    [Header("기믹 설정")]
    [Tooltip("미로를 풀면 나오는 숫자 단서")] public string clueCode = "0427";
    [Tooltip("같은 시드 = 항상 같은 미로")] public int seed = 1027;
    [Tooltip("켜면 좌우 조작이 거울처럼 반대")] public bool invertHorizontalControls = false;

    [Header("보상: 전화부스 열쇠")]
    [Tooltip("통합 프로젝트에서 스테이지2의 전화부스 열쇠 ItemData 에셋(Item Id: stage2_booth_key)을 넣는다. 비워 두면 같은 ID로 즉석에서 만들어 준다")]
    public UnityEngine.Object rewardItemAsset;

    [Header("기믹이 열려 있는 동안 숨길 오브젝트 (선택)")]
    [Tooltip("예: 스테이지 좌우 화살표, 대사창 등")] public GameObject[] hideWhileOpen;

    [Header("이벤트")]
    public UnityEvent onOpened = new UnityEvent();
    public UnityEvent<string> onSolved = new UnityEvent<string>();
    public UnityEvent onClosed = new UnityEvent();
    [Tooltip("벽에 3번 부딪혀 실패했을 때 (예: 하트 감소)")]
    public UnityEvent onFailed = new UnityEvent();
    [Tooltip("미로 보상(전화부스 열쇠)을 주웠을 때 (아이템 ID 전달)")]
    public UnityEvent<string> onRewardObtained = new UnityEvent<string>();

    Collider2D col;
    bool openedByMe;

    void Awake() { col = GetComponent<Collider2D>(); }

    void OnEnable()
    {
        MirrorMazeGimmick.Solved += HandleSolved;
        MirrorMazeGimmick.Closed += HandleClosed;
        MirrorMazeGimmick.Failed += HandleFailed;
        MirrorMazeGimmick.RewardObtained += HandleReward;
    }

    void OnDisable()
    {
        MirrorMazeGimmick.Solved -= HandleSolved;
        MirrorMazeGimmick.Closed -= HandleClosed;
        MirrorMazeGimmick.Failed -= HandleFailed;
        MirrorMazeGimmick.RewardObtained -= HandleReward;
    }

    void Update()
    {
        // Collider2D가 있으면 직접 클릭 판정 (EventSystem 없이도 동작)
        if (col == null || MirrorMazeGimmick.IsOpen) return;
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 sp = mouse.position.ReadValue();
        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, Mathf.Abs(cam.transform.position.z - transform.position.z)));
        if (col.OverlapPoint(wp)) OpenGimmick();
    }

    public void OnPointerClick(PointerEventData eventData) => OpenGimmick();

    /// <summary>기믹 열기 (UI Button OnClick 에 연결 가능)</summary>
    public void OpenGimmick()
    {
        if (MirrorMazeGimmick.IsOpen) return;
        if (MirrorMazeGimmick.LastClosedFrame == Time.frameCount) return; // 닫는 클릭으로 바로 다시 열리는 것 방지
        MirrorMazeGimmick.Open(clueCode, seed, invertHorizontalControls, rewardItemAsset);
        openedByMe = true;
        SetHidden(true);
        onOpened?.Invoke();
    }

    void HandleSolved(string code)
    {
        if (openedByMe) onSolved?.Invoke(code);
    }

    void HandleReward(string itemId)
    {
        if (openedByMe) onRewardObtained?.Invoke(itemId);
    }

    void HandleFailed()
    {
        if (openedByMe) onFailed?.Invoke();
    }

    void HandleClosed()
    {
        if (!openedByMe) return;
        openedByMe = false;
        SetHidden(false);
        onClosed?.Invoke();
    }

    void SetHidden(bool hidden)
    {
        if (hideWhileOpen == null) return;
        foreach (var go in hideWhileOpen) if (go != null) go.SetActive(!hidden);
    }
}
