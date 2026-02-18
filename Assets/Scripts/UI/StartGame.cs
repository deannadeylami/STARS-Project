using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class StartGame : MonoBehaviour
{
    public void StartScene()

    {
        Debug.Log("Start button pressed. Loading next scene.");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    } 

    public void QuickStart()
    {
        Debug.Log("Quick Start button pressed. Skipping input and loading next scene with defaults.");

        if (SkySession.Instance != null)
        {
            double defaultLat = 40.0;
            double defaultLon = -74.0;
            DateTime defaultDateTime = new DateTime(2024, 6, 1, 22, 0, 0);

            SkySession.Instance.SetInputs(
                defaultLat,
                defaultLon,
                defaultDateTime,
                defaultLat.ToString(),
                defaultLon.ToString(),
                defaultDateTime.ToString("yyyy-MM-dd"),
                defaultDateTime.ToString("HH:mm"));
        }

        SceneManager.LoadScene("SkyScene");
    }
}
