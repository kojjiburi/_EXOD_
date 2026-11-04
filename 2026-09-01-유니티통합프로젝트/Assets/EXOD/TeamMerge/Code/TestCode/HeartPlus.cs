using UnityEngine;

public class HeartPlus : MonoBehaviour
{
    void OnMouseDown()
    {
        Debug.Log("Å¬¸¯µÊ!");
        FindAnyObjectByType<HeartManager>().HPP();
    }
}