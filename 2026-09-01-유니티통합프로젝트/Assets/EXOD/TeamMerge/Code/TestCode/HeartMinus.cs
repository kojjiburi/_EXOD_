using UnityEngine;

public class HeartMinus : MonoBehaviour
{
    void OnMouseDown()
    {
        Debug.Log("Å¬¸¯µÊ!");
        FindAnyObjectByType<HeartManager>().HPM();
    }
}