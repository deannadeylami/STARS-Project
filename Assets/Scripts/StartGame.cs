using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGame : MonoBehaviour
{
    public string Intro;
    public void StartScene()

    {
        SceneManager.LoadScene(Intro);
    } 

}
