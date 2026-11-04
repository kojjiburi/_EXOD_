using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SpriteRendererUiMirror : MonoBehaviour
{
    // Keeps SpriteRenderer-driven status icons synchronized with UI Images.
    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField] private Image targetImage;

    public void Configure(SpriteRenderer source, Image target)
    {
        sourceRenderer = source;
        targetImage = target;
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (sourceRenderer == null || targetImage == null)
            return;

        targetImage.sprite = sourceRenderer.sprite;
        targetImage.color = sourceRenderer.color;
        targetImage.enabled = sourceRenderer.sprite != null;
    }
}
