using UnityEngine;

public class Yan : MonoBehaviour
{
    // 현재 위험도
    public int Emergence = 0;

    // 최대 위험도
    public int MaxEmergence = 100;

    // 상태 표시용 SpriteRenderer
    public SpriteRenderer StateRenderer;

    // 상태 스프라이트
    public Sprite SafeSprite;
    public Sprite NormalSprite;
    public Sprite DangerSprite;
    public Sprite AppearedSprite;

    // 얀데레 등장 여부
    private bool YanAppeared = false;

    // 등장 확률 (10이면 10%)
    [Range(0, 100)]
    public int AppearChance = 10;

    // 확률 계산 쿨타임
    public float CheckCooldown = 5f;

    // 마지막 확률 계산 시간
    private float LastCheckTime = -999f;

    void Start()
    {
        UpdateState();
    }

    // 위험도 증가
    public void AddEmergence(int amount)
    {
        // 이미 등장했으면 종료
        if (YanAppeared)
            return;

        // 위험도 증가
        Emergence += amount;

        // 최대값 제한
        if (Emergence > MaxEmergence)
        {
            Emergence = MaxEmergence;
        }

        // 상태 갱신
        UpdateState();

        // 위험도 76 이상일 때만 등장 판정 가능
        if (Emergence >= 76)
        {
            // 마지막 판정 이후 5초 지났는지 확인
            if (Time.time >= LastCheckTime + CheckCooldown)
            {
                // 마지막 판정 시간 갱신
                LastCheckTime = Time.time;

                // 0~99 랜덤값 생성
                int random = Random.Range(0, 100);

                // 10% 확률
                if (random < AppearChance)
                {
                    YanAppeared = true;

                    // 등장 상태 스프라이트 변경
                    StateRenderer.sprite = AppearedSprite;

                    // TODO:
                    // 여기서 발소리 재생
                    // AudioSource.Play() 등 사용

                    // TODO:
                    FindAnyObjectByType<HeartManager>().HPM();
                    // 여기서 얀데레 등장 처리
                    // 등장 애니메이션 등등

                    Emergence = 0; //Emergence 초기화
                }
            }
        }
    }

    // 위험도 감소
    public void RemoveEmergence(int amount)
    {
        Emergence -= amount;

        // 최소값 제한
        if (Emergence < 0)
        {
            Emergence = 0;
        }

        UpdateState();
    }

    // 상태에 따라 스프라이트 변경
    void UpdateState()
    {
        // 이미 등장했으면 등장 스프라이트 유지
        if (YanAppeared)
        {
            StateRenderer.sprite = AppearedSprite;
            return;
        }

        // 안전
        if (Emergence <= 30)
        {
            StateRenderer.sprite = SafeSprite;
        }

        // 보통
        else if (Emergence <= 50)
        {
            StateRenderer.sprite = NormalSprite;
        }

        // 위험
        else
        {
            StateRenderer.sprite = DangerSprite;
        }
    }
}