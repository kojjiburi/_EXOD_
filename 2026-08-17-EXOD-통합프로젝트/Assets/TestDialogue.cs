using UnityEngine;

public class TestDialogue : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            DialogueManager.Instance.StartCharacterDialogue("얀데레", new string[]
            {
                "찾았다.",
                "어디 가려고 했어?",
                "도망치면 안 돼."
            });
        }
    }
}
