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
            DateTime defaultDateTime = new DateTime(2025, 1, 18, 18, 30, 0);

            // Build formatted raw strings that match InputFieldGrabber's format:
            // e.g. "40° 0' N" and "74° 0' W"
            string latRaw = DecimalToRawString(defaultLat, isLatitude: true);
            string lonRaw = DecimalToRawString(defaultLon, isLatitude: false);

            SkySession.Instance.SetInputs(
                latDeg:   defaultLat,
                lonDeg:   defaultLon,
                localDt:  defaultDateTime,
                latRaw:   latRaw,
                lonRaw:   lonRaw,
                dateRaw:  defaultDateTime.ToString("yyyy-MM-dd"),
                timeRaw:  defaultDateTime.ToString("HH:mm"));
        }

        SceneManager.LoadScene("SkyScene");
    }

    /// <summary>
    /// Converts a signed decimal degree value into a display string matching
    /// InputFieldGrabber's format: "DD° MM.mm' H"
    /// e.g.  40.75  (lat) → "40° 45' N"
    ///       -74.5  (lon) → "74° 30' W"
    /// </summary>
    private static string DecimalToRawString(double decimalDeg, bool isLatitude)
    {
        double abs     = Math.Abs(decimalDeg);
        int    degrees = (int)abs;
        double minutes = (abs - degrees) * 60.0;

        string hemisphere;
        if (isLatitude)
            hemisphere = decimalDeg >= 0 ? "N" : "S";
        else
            hemisphere = decimalDeg >= 0 ? "E" : "W";

        // Match the format produced by InputFieldGrabber: "40° 0' N"
        return $"{degrees}° {minutes:F2}' {hemisphere}";
    }

    // Close out the application.
    public void QuitApplication()
    {
        Application.Quit();
    }
}