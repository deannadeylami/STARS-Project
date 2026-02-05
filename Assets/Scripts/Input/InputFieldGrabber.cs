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

    [Header("Scene Loading")]
    [Tooltip("Name of the scene to load after validation succeeds (your viewer scene).")]
    public string skySceneName = "SkyScene";

    // Required formats (keep strict to avoid messy edge cases)
    private const string DateFormat = "yyyy-MM-dd"; // Example: 2026-02-05
    private const string TimeFormat = "HH:mm";      // Example: 21:30 (24h)

    // Range requirements
    private const double MinLat = -90.0;
    private const double MaxLat = 90.0;
    private const double MinLon = -180.0;
    private const double MaxLon = 180.0;

    private static readonly DateTime MinDate = new DateTime(1900, 1, 1);
    private static readonly DateTime MaxDate = new DateTime(2100, 1, 1);

    public void GrabAllInputs()
    {
        string dateText = (dateInput != null) ? dateInput.text.Trim() : "";
        string timeText = (timeInput != null) ? timeInput.text.Trim() : "";
        string latText  = (latitudeInput != null) ? latitudeInput.text.Trim() : "";
        string lonText  = (longitudeInput != null) ? longitudeInput.text.Trim() : "";

        // Empty checks
        if (string.IsNullOrWhiteSpace(dateText) ||
            string.IsNullOrWhiteSpace(timeText) ||
            string.IsNullOrWhiteSpace(latText) ||
            string.IsNullOrWhiteSpace(lonText))
        {
            Debug.LogWarning("At least one input field is empty. Please fill in every field.");
            return;
        }

        // Parse latitude/longitude (InvariantCulture so decimals always work like 34.72)
        if (!double.TryParse(latText, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat))
        {
            Debug.LogWarning("Latitude format invalid. Use decimal degrees like 34.72");
            return;
        }

        if (!double.TryParse(lonText, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
        {
            Debug.LogWarning("Longitude format invalid. Use decimal degrees like -86.64");
            return;
        }

        // Range checks
        if (lat < MinLat || lat > MaxLat)
        {
            Debug.LogWarning($"Latitude out of range. Must be between {MinLat} and {MaxLat}.");
            return;
        }

        if (lon < MinLon || lon > MaxLon)
        {
            Debug.LogWarning($"Longitude out of range. Must be between {MinLon} and {MaxLon}.");
            return;
        }

        // Parse date/time strictly
        if (!DateTime.TryParseExact(dateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime datePart))
        {
            Debug.LogWarning($"Date format invalid. Use {DateFormat} (example: 2026-02-05).");
            return;
        }

        if (!DateTime.TryParseExact(timeText, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timePart))
        {
            Debug.LogWarning($"Time format invalid. Use {TimeFormat} 24-hour time (example: 21:30).");
            return;
        }

        // Combine into one local DateTime
        DateTime localDateTime = new DateTime(
            datePart.Year, datePart.Month, datePart.Day,
            timePart.Hour, timePart.Minute, 0,
            DateTimeKind.Unspecified // local-without-timezone for now
        );

        // Date range validation (1900-01-01 through 2100-01-01)
        if (localDateTime < MinDate || localDateTime > MaxDate)
        {
            Debug.LogWarning($"Date out of acceptable range: {MinDate:yyyy-MM-dd} to {MaxDate:yyyy-MM-dd}.");
            return;
        }

        // Ensure SkySession exists
        if (SkySession.Instance == null)
        {
            Debug.LogWarning("SkySession not found. Creating one automatically.");
            var go = new GameObject("SkySession");
            go.AddComponent<SkySession>();
        }

        // Store validated values
        SkySession.Instance.SetInputs(
            latDeg: lat,
            lonDeg: lon,
            localDt: localDateTime,
            latRaw: latText,
            lonRaw: lonText,
            dateRaw: dateText,
            timeRaw: timeText
        );

        Debug.Log($"Stored Inputs => Lat: {lat}  Lon: {lon}  LocalDT: {localDateTime:yyyy-MM-dd HH:mm}");

        // Load the viewer scene
        if (string.IsNullOrWhiteSpace(skySceneName))
        {
            Debug.LogError("Sky scene name is empty. Set skySceneName in the inspector.");
            return;
        }

        SceneManager.LoadScene(skySceneName);
    }
}
