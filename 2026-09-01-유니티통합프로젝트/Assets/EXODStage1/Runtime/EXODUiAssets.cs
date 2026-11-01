using TMPro;
using UnityEngine;

/// <summary>
/// 코드로 만드는 UI가 함께 쓰는 에셋 모음입니다.
/// 파일: Assets/Resources/EXODStage1/EXODUiAssets.asset (Inspector에서 폰트를 바꿀 수 있습니다)
/// </summary>
public sealed class EXODUiAssets : ScriptableObject
{
    private const string ResourcePath = "EXODStage1/EXODUiAssets";

    [Tooltip("게임 글자 폰트 (Galmuri9)")]
    public TMP_FontAsset font;

    private static EXODUiAssets loaded;

    public static TMP_FontAsset Font
    {
        get
        {
            if (loaded == null)
                loaded = Resources.Load<EXODUiAssets>(ResourcePath);

            return loaded != null ? loaded.font : null;
        }
    }
}
