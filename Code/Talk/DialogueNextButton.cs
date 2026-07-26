using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class DialogueNextButton : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Advance);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(Advance);
    }

    private static void Advance()
    {
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.AdvanceDialogue();
    }
}
