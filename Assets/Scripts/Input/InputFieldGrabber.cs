using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InputFieldGrabber : MonoBehaviour
{
    [Header("UI Inputs")]
    public TMP_InputField dateInput;
    public TMP_InputField timeInput;
    public TMP_InputField latitudeInput;
    public TMP_InputField longitudeInput;

    [Header("Error Display (TextMeshPro - Text named ErrorMsg)")]
    public TMP_Text errorText;

    [Header("Scene Loading")]
    public string skySceneName = "SkyScene";

    private const string DateFormat = "yyyy-MM-dd"; // Example: 2026-02-05
    private const string TimeFormat = "HH:mm";      // Example: 21:30 (24h)

    private const double MinLat = -90.0;
    private const double MaxLat = 90.0;
    private const double MinLon = -180.0;
    private const double MaxLon = 180.0;

    private static readonly DateTime MinDate = new DateTime(1900, 1, 1);
    private static readonly DateTime MaxDate = new DateTime(2100, 1, 1);

    private void Awake()
    {
        EnsureErrorReference();
        ClearError();
    }

    private void EnsureErrorReference()
    {
        if (errorText != null) return;

        // Finds inactive objects too
        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (var t in allTexts)
        {
            if (t != null && t.gameObject.name == "ErrorMsg")
            {
                errorText = t;
                break;
            }
        }

        if (errorText == null)
        {
            Debug.LogError("Could not find a TMP text object named 'ErrorMsg'. " +
                           "Make sure your TextMeshPro - Text GameObject is named exactly ErrorMsg.");
        }
    }

    public void GrabAllInputs()
    {
        Debug.Log("Submit clicked: GrabAllInputs() called");

        EnsureErrorReference();
        ClearError();

        string dateText = dateInput != null ? dateInput.text.Trim() : "";
        string timeText = timeInput != null ? timeInput.text.Trim() : "";
        string latText  = latitudeInput != null ? latitudeInput.text.Trim() : "";
        string lonText  = longitudeInput != null ? longitudeInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(dateText) ||
            string.IsNullOrWhiteSpace(timeText) ||
            string.IsNullOrWhiteSpace(latText) ||
            string.IsNullOrWhiteSpace(lonText))
        {
            ShowError("Please fill in all fields.");
            return;
        }

        if (!double.TryParse(latText, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
            lat < MinLat || lat > MaxLat)
        {
            ShowError("Latitude must be a number between -90 and 90.");
            return;
        }

        if (!double.TryParse(lonText, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon) ||
            lon < MinLon || lon > MaxLon)
        {
            ShowError("Longitude must be a number between -180 and 180.");
            return;
        }

        if (!DateTime.TryParseExact(dateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime datePart))
        {
            ShowError("Date must be in format yyyy-MM-dd (example: 2026-02-05).");
            return;
        }

        if (!DateTime.TryParseExact(timeText, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timePart))
        {
            ShowError("Time must be in format HH:mm (24-hour, example: 21:30).");
            return;
        }

        DateTime localDateTime = new DateTime(
            datePart.Year, datePart.Month, datePart.Day,
            timePart.Hour, timePart.Minute, 0,
            DateTimeKind.Unspecified
        );

        if (localDateTime < MinDate || localDateTime > MaxDate)
        {
            ShowError($"Date must be between {MinDate:yyyy-MM-dd} and {MaxDate:yyyy-MM-dd}.");
            return;
        }

        if (SkySession.Instance == null)
        {
            var go = new GameObject("SkySession");
            go.AddComponent<SkySession>();
        }

        SkySession.Instance.SetInputs(
            latDeg: lat,
            lonDeg: lon,
            localDt: localDateTime,
            latRaw: latText,
            lonRaw: lonText,
            dateRaw: dateText,
            timeRaw: timeText
        );

        SceneManager.LoadScene(skySceneName);
    }

    private void ShowError(string message)
    {
        Debug.LogWarning(message);

        if (errorText == null)
        {
            Debug.LogError("ShowError failed because errorText is null. Ensure there is a TMP object named ErrorMsg.");
            return;
        }

        // Make sure it's active so it can render
        if (!errorText.gameObject.activeInHierarchy)
            errorText.gameObject.SetActive(true);

        errorText.enabled = true;
        errorText.text = message;
    }

    private void ClearError()
    {
        if (errorText == null) return;

        errorText.text = "";
        errorText.enabled = false; // hide without disabling the GameObject
    }
}
