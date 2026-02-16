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

    [Header("Latitude (Deg/Min + Hemisphere)")]
    public TMP_InputField latDegInput;
    public TMP_InputField latMinInput;
    public TMP_Dropdown latHemisphereDropdown; // Options: N, S

    [Header("Longitude (Deg/Min + Hemisphere)")]
    public TMP_InputField lonDegInput;
    public TMP_InputField lonMinInput;
    public TMP_Dropdown lonHemisphereDropdown; // Options: E, W

    [Header("Error Display (TextMeshPro - Text named ErrorMsg)")]
    public TMP_Text errorText;

    [Header("Scene Loading")]
    public string skySceneName = "SkyScene";

    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm";

    private static readonly DateTime MinDate = new DateTime(1900, 1, 1);
    private static readonly DateTime MaxDate = new DateTime(2100, 1, 1);

    private void Awake()
    {
        EnsureErrorReference();
        AutoWireIfMissing();
        ClearError();
    }


    private void EnsureErrorReference()
    {
        if (errorText != null) return;

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
            Debug.LogError("Could not find a TMP text object named 'ErrorMsg'.");
        }
    }

    private void AutoWireIfMissing()
    {
        latDegInput = latDegInput ?? FindInput("LatitudeDegreesResponse");
        latMinInput = latMinInput ?? FindInput("LatitudeMinutesResponse");
        lonDegInput = lonDegInput ?? FindInput("LongitudeDegreesResponse");
        lonMinInput = lonMinInput ?? FindInput("LongitudeMinutesResponse");

        latHemisphereDropdown = latHemisphereDropdown ?? FindDropdown("LatHemisphereDropDown");
        lonHemisphereDropdown = lonHemisphereDropdown ?? FindDropdown("LongHemisphereDropDown");

        Debug.Log(
            $"[AutoWire] on '{gameObject.name}' " +
            $"latDeg={(latDegInput!=null)} latMin={(latMinInput!=null)} " +
            $"lonDeg={(lonDegInput!=null)} lonMin={(lonMinInput!=null)}"
        );
    }

    private TMP_InputField FindInput(string rootName)
    {
        var go = GameObject.Find(rootName);
        if (go == null)
        {
            Debug.LogWarning($"[AutoWire] Could not find GameObject named '{rootName}'.");
            return null;
        }

        // Works whether TMP_InputField is on the root or a child.
        var field = go.GetComponent<TMP_InputField>() ?? go.GetComponentInChildren<TMP_InputField>(true);
        if (field == null)
            Debug.LogWarning($"[AutoWire] Found '{rootName}' but no TMP_InputField on it or children.");
        return field;
    }

    private TMP_Dropdown FindDropdown(string rootName)
    {
        var go = GameObject.Find(rootName);
        if (go == null)
        {
            Debug.LogWarning($"[AutoWire] Could not find GameObject named '{rootName}'.");
            return null;
        }

        var dd = go.GetComponent<TMP_Dropdown>() ?? go.GetComponentInChildren<TMP_Dropdown>(true);
        if (dd == null)
            Debug.LogWarning($"[AutoWire] Found '{rootName}' but no TMP_Dropdown on it or children.");
        return dd;
    }

    public void GrabAllInputs()
    {
        Debug.Log("Submit clicked: GrabAllInputs() called");

        EnsureErrorReference();
        ClearError();

        // --- Required text ---
        string dateText = dateInput != null ? dateInput.text.Trim() : "";
        string timeText = timeInput != null ? timeInput.text.Trim() : "";

        string latDegText = latDegInput != null ? latDegInput.text.Trim() : "";
        string latMinText = latMinInput != null ? latMinInput.text.Trim() : "";
        string lonDegText = lonDegInput != null ? lonDegInput.text.Trim() : "";
        string lonMinText = lonMinInput != null ? lonMinInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(dateText) ||
            string.IsNullOrWhiteSpace(timeText) ||
            string.IsNullOrWhiteSpace(latDegText) ||
            string.IsNullOrWhiteSpace(latMinText) ||
            string.IsNullOrWhiteSpace(lonDegText) ||
            string.IsNullOrWhiteSpace(lonMinText))
        {
            ShowError("Please fill in all fields.");
            return;
        }

        // --- Parse degrees/minutes ---
        if (!int.TryParse(latDegText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int latDeg) ||
            latDeg < 0 || latDeg > 90)
        {
            ShowError("Latitude degrees must be an integer between 0 and 90.");
            return;
        }

        if (!double.TryParse(latMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out double latMin) ||
            latMin < 0 || latMin >= 60)
        {
            ShowError("Latitude minutes must be a number between 0 and 59.999.");
            return;
        }

        if (!int.TryParse(lonDegText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lonDeg) ||
            lonDeg < 0 || lonDeg > 180)
        {
            ShowError("Longitude degrees must be an integer between 0 and 180.");
            return;
        }

        if (!double.TryParse(lonMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out double lonMin) ||
            lonMin < 0 || lonMin >= 60)
        {
            ShowError("Longitude minutes must be a number between 0 and 59.999.");
            return;
        }

        // Optional but good: prevent 90°xx' or 180°xx'
        if (latDeg == 90 && latMin != 0)
        {
            ShowError("Latitude minutes must be 0 when degrees is 90.");
            return;
        }
        if (lonDeg == 180 && lonMin != 0)
        {
            ShowError("Longitude minutes must be 0 when degrees is 180.");
            return;
        }

        // --- Hemisphere ---
        string latHem = GetDropdownText(latHemisphereDropdown); // "N" or "S"
        string lonHem = GetDropdownText(lonHemisphereDropdown); // "E" or "W"

        if (latHem != "N" && latHem != "S")
        {
            ShowError("Latitude hemisphere must be N or S.");
            return;
        }
        if (lonHem != "E" && lonHem != "W")
        {
            ShowError("Longitude hemisphere must be E or W.");
            return;
        }

        // --- Convert to signed decimal degrees (east-positive lon) ---
        double latAbs = latDeg + (latMin / 60.0);
        double lonAbs = lonDeg + (lonMin / 60.0);

        double lat = (latHem == "S") ? -latAbs : latAbs;
        double lon = (lonHem == "W") ? -lonAbs : lonAbs; // west becomes negative

        // --- Date/time parse ---
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

        // --- Persist session ---
        if (SkySession.Instance == null)
        {
            var go = new GameObject("SkySession");
            go.AddComponent<SkySession>();
        }

        // Build raw strings for debugging/display
        string latRaw = $"{latDeg}° {latMin.ToString(CultureInfo.InvariantCulture)}' {latHem}";
        string lonRaw = $"{lonDeg}° {lonMin.ToString(CultureInfo.InvariantCulture)}' {lonHem}";

        SkySession.Instance.SetInputs(
            latDeg: lat,
            lonDeg: lon,
            localDt: localDateTime,
            latRaw: latRaw,
            lonRaw: lonRaw,
            dateRaw: dateText,
            timeRaw: timeText
        );

        SceneManager.LoadScene(skySceneName);
    }

    private static string GetDropdownText(TMP_Dropdown dd)
    {
        if (dd == null) return "";
        if (dd.options == null || dd.options.Count == 0) return "";
        int i = dd.value;
        if (i < 0 || i >= dd.options.Count) return "";
        return (dd.options[i].text ?? "").Trim().ToUpperInvariant();
    }

    private void ShowError(string message)
    {
        Debug.LogWarning(message);

        if (errorText == null)
        {
            Debug.LogError("ShowError failed because errorText is null.");
            return;
        }

        if (!errorText.gameObject.activeInHierarchy)
            errorText.gameObject.SetActive(true);

        errorText.enabled = true;
        errorText.text = message;
    }

    private void ClearError()
    {
        if (errorText == null) return;
        errorText.text = "";
        errorText.enabled = false;
    }
}
