using UnityEngine;

public class DayUp : MonoBehaviour
{
    void OnMouseDown()
    {
        GManager.Instance.NextDay();
    }
}