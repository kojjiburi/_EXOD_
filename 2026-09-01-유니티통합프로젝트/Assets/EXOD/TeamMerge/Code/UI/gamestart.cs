using UnityEngine;
using UnityEngine.SceneManagement;

public class gamestart : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void OnMouseDown()
    {
        // 침실(1스테이지) 씬으로 시작합니다. 인트로 대화는 이 씬에서 자동으로 재생됩니다.
        SceneManager.LoadScene("SampleScene");
    }
}
