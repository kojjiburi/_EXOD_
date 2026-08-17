using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 방 해금 관리 매니저
/// 침실/거실 기본 해금
/// 침실 클리어 → 복도, 화장실 해금
/// 거실 클리어 → 마당 해금
/// 마당 탈출 → 집 밖 해금
/// </summary>
public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("=== 방 해금 상태 ===")]
    public bool 침실_해금 = true;
    public bool 거실_해금 = true;
    public bool 복도_해금 = false;
    public bool 화장실_해금 = false;
    public bool 마당_해금 = false;
    public bool 집밖_해금 = false;

    [Header("=== 기믹 클리어 상태 ===")]
    public bool 침실_클리어 = false;
    public bool 거실_클리어 = false;
    public bool 마당_클리어 = false;

    [Header("=== 이벤트 ===")]
    public UnityEvent on침실클리어;
    public UnityEvent on거실클리어;
    public UnityEvent on마당클리어;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>침실 기믹 클리어 시 호출</summary>
    public void Clear침실()
    {
        if (침실_클리어) return;
        침실_클리어 = true;

        // 복도, 화장실 해금
        복도_해금 = true;
        화장실_해금 = true;

        Debug.Log("침실 클리어! 복도, 화장실 해금!");
        on침실클리어?.Invoke();

        // 슬라이더 갱신
        FindAnyObjectByType<SliderManager>()?.UpdateSlider();
    }

    /// <summary>거실 기믹 클리어 시 호출</summary>
    public void Clear거실()
    {
        if (거실_클리어) return;
        거실_클리어 = true;

        // 마당 해금
        마당_해금 = true;

        Debug.Log("거실 클리어! 마당 해금!");
        on거실클리어?.Invoke();

        FindAnyObjectByType<SliderManager>()?.UpdateSlider();
    }

    /// <summary>마당 탈출 시 호출</summary>
    public void Clear마당()
    {
        if (마당_클리어) return;
        마당_클리어 = true;

        // 집 밖 해금
        집밖_해금 = true;

        Debug.Log("마당 클리어! 집 밖 해금!");
        on마당클리어?.Invoke();

        FindAnyObjectByType<SliderManager>()?.UpdateSlider();
    }

    /// <summary>해당 방이 해금됐는지 확인</summary>
    public bool IsUnlocked(string roomName)
    {
        switch (roomName)
        {
            case "침실": return 침실_해금;
            case "거실": return 거실_해금;
            case "복도": return 복도_해금;
            case "화장실": return 화장실_해금;
            case "마당": return 마당_해금;
            case "집밖": return 집밖_해금;
            default: return false;
        }
    }
}
