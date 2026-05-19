using UnityEngine;

public class HeartManager : MonoBehaviour
{
    // 하트 오브젝트들의 SpriteRenderer 넣는 배열
    public SpriteRenderer[] hearts;

    // 꽉 찬 하트 이미지
    public Sprite HeartFull;

    // 빈 하트 이미지
    public Sprite HeartEmpty;

    // 현재 체력
    public int HP = 5;

    // 최대 체력
    public int MaxHP = 5;

    void Start()
    {
        UpdateHearts();
    }

    // HP 감소
    public void HPM()
    {
        // HP가 0보다 클 때만 감소
        if (HP > 0)
        {
            HP--;
            UpdateHearts();
        }
    }

    // HP 증가
    public void HPP()
    {
        // HP가 최대보다 작을 때만 증가
        if (HP < MaxHP)
        {
            HP++;
            UpdateHearts();
        }
    }

    // 하트 이미지 갱신
    void UpdateHearts()
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            // 현재 HP보다 작은 번호는 Full
            if (i < HP)
            {
                hearts[i].sprite = HeartFull;
            }
            // 나머지는 Empty
            else
            {
                hearts[i].sprite = HeartEmpty;
            }
        }
    }
}