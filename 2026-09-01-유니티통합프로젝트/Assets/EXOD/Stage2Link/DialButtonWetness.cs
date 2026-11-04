using System.Reflection;
using UnityEngine;

/// <summary>
/// [하드 모드] 산책 길에서 줍는 다이얼 버튼 [2]는 젖어 있음 (사용자 요청 2026-11-03)
/// - 퀵슬롯의 수건을 누르고 → 젖은 버튼 [2]를 누르면 물기를 닦아 안 젖은 버튼 [2]로 바뀜 (FlashlightGimmick)
/// - 젖은 버튼을 전화기에 끼우려 하면 "이거.. 감전될 거 같은데?" 하고 안 끼워짐 (PhoneBoothInterior)
/// 젖은 버튼 = 원래 아이템 에셋(PhoneBooth_DialButton2, 젖은 그림), 닦은 버튼 = 실행 중에 만든 복사본(같은 ID, 마른 그림).
/// 아이템 ID가 같아서 일차 끝 점검 예외·압수·부스 수리는 원래대로 동작함. 이지 모드는 버튼 [2]가 처음부터 끼워져 있음.
/// 마른 그림: Assets/EXOD/Stage2Link/Resources/EXODDialButton/Icon_DialButton2_Dry.png
/// </summary>
public static class DialButtonWetness
{
    public const string ItemId = "stage2_dial_button_2";
    private const string DryIconPath = "EXODDialButton/Icon_DialButton2_Dry";

    private static ItemData dryItem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => dryItem = null;

    public static bool IsWet(ItemData item)
    {
        return item != null && item.ItemId == ItemId && !GameDifficulty.IsEasy && item != dryItem;
    }

    /// <summary>젖은 버튼을 닦은 버튼(같은 ID, 마른 그림)으로</summary>
    public static ItemData GetDry(ItemData wet)
    {
        if (dryItem != null)
            return dryItem;

        dryItem = Object.Instantiate(wet);
        dryItem.name = wet.name + " (닦음)";
        Texture2D texture = Resources.Load<Texture2D>(DryIconPath);
        if (texture != null)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            FieldInfo icon = typeof(ItemData).GetField("icon", BindingFlags.NonPublic | BindingFlags.Instance);
            icon?.SetValue(dryItem, sprite);
        }
        return dryItem;
    }
}
