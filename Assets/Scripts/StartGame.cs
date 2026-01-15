using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGame : MonoBehaviour
{
    public string Intro;
    public void StartScene()

    {
        Debug.Log("Start button pressed. Loading scene: " + Intro);
        SceneManager.LoadScene(Intro);
    } 

}
