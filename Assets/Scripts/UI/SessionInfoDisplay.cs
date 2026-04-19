// SessionInfoDisplay.cs
// Displays the observer's location, date, and time (from the input screen) as a HUD element.
// Place this on a UI GameObject in the Sky Scene canvas, positioned below the compass.

using TMPro;
using UnityEngine;

public class SessionInfoDisplay : MonoBehaviour
{
    [Header("Text References")]
    [Tooltip("Assign a TMP_Text for location, or leave null to auto-find 'SessionLocation'")]
    public TMP_Text locationText;

    [Tooltip("Assign a TMP_Text for date/time, or leave null to auto-find 'SessionDateTime'")]
    public TMP_Text dateTimeText;

    [Header("Display Options")]
    [Tooltip("Format string for the date. Uses SkySession.RawDate (e.g. '2026-04-16').")]
    public bool showRawStrings = true;   // true = show exactly what user typed; false = reformatted

    [Tooltip("Friendly reformat: shown when showRawStrings is false.")]
    public string friendlyDateFormat = "MMM dd, yyyy";
    public string friendlyTimeFormat = "HH:mm";

    private void Awake()
    {
        AutoWireIfMissing();
    }

    private void Start()
    {
        Refresh();
    }

    /// <summary>
    /// Reads SkySession and updates the text labels.
    /// Call this if you ever update the session at runtime.
    /// </summary>
    public void Refresh()
    {
        if (SkySession.Instance == null)
        {
            SetPlaceholder();
            return;
        }

        // --- Location ---
        if (locationText != null)
        {
            string lat = SkySession.Instance.RawLatitude;
            string lon = SkySession.Instance.RawLongitude;

            if (!string.IsNullOrWhiteSpace(lat) && !string.IsNullOrWhiteSpace(lon))
                locationText.text = $"{lat}  |  {lon}";
            else
                locationText.text = FormatDecimalLocation();
        }

        // --- Date / Time ---
        if (dateTimeText != null)
        {
            if (showRawStrings)
            {
                string d = SkySession.Instance.RawDate;
                string t = SkySession.Instance.RawTime;
                dateTimeText.text = $"{d}  ·  {t}";
            }
            else
            {
                // Reformat using the stored DateTime
                System.DateTime dt = SkySession.Instance.LocalDateTime;
                string d = dt.ToString(friendlyDateFormat, System.Globalization.CultureInfo.InvariantCulture);
                string t = dt.ToString(friendlyTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
                dateTimeText.text = $"{d}  ·  {t}";
            }
        }
    }

    // Fallback: format from decimal degrees when raw strings are empty
    private string FormatDecimalLocation()
    {
        double lat = SkySession.Instance.LatitudeDeg;
        double lon = SkySession.Instance.LongitudeDeg;

        string latStr = $"{System.Math.Abs(lat):F2}° {(lat >= 0 ? "N" : "S")}";
        string lonStr = $"{System.Math.Abs(lon):F2}° {(lon >= 0 ? "E" : "W")}";
        return $"{latStr}  |  {lonStr}";
    }

    private void SetPlaceholder()
    {
        if (locationText != null)  locationText.text  = "--° --'  |  --° --'";
        if (dateTimeText != null)  dateTimeText.text  = "----  ·  --:--";
    }

    // Auto-wire: finds child TMP_Text objects by name if not assigned in Inspector
    private void AutoWireIfMissing()
    {
        if (locationText == null)
            locationText = FindChildText("SessionLocation");

        if (dateTimeText == null)
            dateTimeText = FindChildText("SessionDateTime");
    }

    private TMP_Text FindChildText(string goName)
    {
        // Search children of this GameObject first, then scene-wide
        var go = GameObject.Find(goName);
        if (go != null)
        {
            var t = go.GetComponent<TMP_Text>();
            if (t != null) return t;
        }

        Debug.LogWarning($"[SessionInfoDisplay] Could not find TMP_Text on GameObject '{goName}'.");
        return null;
    }
}