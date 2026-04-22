using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InputFieldGrabber : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Preset Dropdowns")]
    public TMP_Dropdown eventDropdown;
    public TMP_Dropdown locationDropdown;
    public TMP_Dropdown userPresetDropdown;         // "My Presets" dropdown

    [Header("Save / Delete Preset UI")]
    public TMP_InputField presetNameInput;          // name field beside the Save button
    // Wire in Inspector:
    //   Save Preset button   → InputFieldGrabber.SaveCurrentPreset()
    //   Delete Preset button → InputFieldGrabber.DeleteSelectedUserPreset()

    [Header("UI Inputs")]
    public TMP_InputField dateInput;
    public TMP_InputField timeInput;

    [Header("Latitude (Deg/Min + Hemisphere)")]
    public TMP_InputField latDegInput;
    public TMP_InputField latMinInput;
    public TMP_Dropdown   latHemisphereDropdown;    // N / S

    [Header("Longitude (Deg/Min + Hemisphere)")]
    public TMP_InputField lonDegInput;
    public TMP_InputField lonMinInput;
    public TMP_Dropdown   lonHemisphereDropdown;    // E / W

    [Header("Error Display (TextMeshPro - Text named ErrorMsg)")]
    public TMP_Text errorText;

    [Header("Scene Loading")]
    public string skySceneName = "SkyScene";

    [Header("Debug / Testing")]
    [Tooltip("Tick in the Inspector then hit Play to wipe all saved user presets. Auto-resets to false after one run.")]
    public bool clearUserPresetsOnAwake = false;

    // ── Constants ─────────────────────────────────────────────────────────────

    private const string DateFormat     = "yyyy-MM-dd";
    private const string TimeFormat     = "HH:mm";
    private const string UserPresetsKey = "UserPresets_v1";

    private static readonly DateTime MinDate = new DateTime(1900, 1, 1);
    private static readonly DateTime MaxDate = new DateTime(2100, 1, 1);

    // ── Runtime state ─────────────────────────────────────────────────────────

    private List<UserPreset> userPresets = new List<UserPreset>();

    // ── Data classes ──────────────────────────────────────────────────────────

    [System.Serializable]
    public class EventPreset
    {
        public string label;
        public int    latDeg, lonDeg;
        public double latMin, lonMin;
        public string latDir, lonDir;
        public int    year, month, day, hour, minute;
    }

    [System.Serializable]
    public class LocationPreset
    {
        public string label;
        public int    latDeg, lonDeg;
        public double latMin, lonMin;
        public string latDir, lonDir;
    }

    [System.Serializable]
    public class UserPreset
    {
        public string label;
        public int    latDeg, lonDeg;
        public double latMin, lonMin;
        public string latDir, lonDir;
        public int    year, month, day, hour, minute;
    }

    [System.Serializable]
    private class UserPresetList { public List<UserPreset> presets = new List<UserPreset>(); }

    // ── Built-in preset data ──────────────────────────────────────────────────

    private List<EventPreset> events = new List<EventPreset>
    {
        new EventPreset { label="Planetary Parade",              latDeg=40, latMin=0,    latDir="N", lonDeg=74, lonMin=0,    lonDir="W", year=2025, month=1, day=18, hour=18, minute=30 },
        new EventPreset { label="Apollo 11 Launch",              latDeg=28, latMin=31.0, latDir="N", lonDeg=80, lonMin=39.0, lonDir="W", year=1969, month=7, day=16, hour=21, minute=0  },
        new EventPreset { label="Artemis II Launch",             latDeg=28, latMin=37.6, latDir="N", lonDeg=80, lonMin=37.3, lonDir="W", year=2026, month=4, day=1,  hour=22, minute=35 },
        new EventPreset { label="Venus and Jupiter Conjunction", latDeg=51, latMin=30.0, latDir="N", lonDeg=0,  lonMin=7.5,  lonDir="W", year=2026, month=6, day=8,  hour=15, minute=0  },
        new EventPreset { label="Total Solar Eclipse 2024",      latDeg=37, latMin=5.0,  latDir="N", lonDeg=88, lonMin=38.0, lonDir="W", year=2024, month=4, day=8,  hour=14, minute=0  },
    };

    private List<LocationPreset> locations = new List<LocationPreset>
    {
        new LocationPreset { label="NYC Statue of Liberty", latDeg=40, latMin=41.3, latDir="N", lonDeg=74,  lonMin=2.7,  lonDir="W" },
        new LocationPreset { label="Big Ben Clocktower",    latDeg=51, latMin=30.0, latDir="N", lonDeg=0,   lonMin=7.5,  lonDir="W" },
        new LocationPreset { label="Colosseum",             latDeg=23, latMin=24.5, latDir="N", lonDeg=25,  lonMin=39.8, lonDir="E" },
        new LocationPreset { label="Eiffel Tower",          latDeg=48, latMin=51.0, latDir="N", lonDeg=2,   lonMin=17.7, lonDir="E" },
        new LocationPreset { label="Golden Gate Bridge",    latDeg=37, latMin=49.2, latDir="N", lonDeg=122, lonMin=28.7, lonDir="W" },
        new LocationPreset { label="Mount Everest",         latDeg=27, latMin=59.3, latDir="N", lonDeg=86,  lonMin=55.5, lonDir="E" },
    };

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureErrorReference();
        AutoWireIfMissing();
        ClearError();

        // Debug flag: tick in Inspector to wipe all saved presets, auto-resets after one run
        if (clearUserPresetsOnAwake)
        {
            PlayerPrefs.DeleteKey(UserPresetsKey);
            PlayerPrefs.Save();
            clearUserPresetsOnAwake = false;
            Debug.Log("[UserPreset] Cleared all saved presets.");
        }

        LoadUserPresets();
    }

    private void Start()
    {
        // Populate dropdowns in Start via coroutine so TMP_Dropdown's own Awake
        // has already run on every dropdown GameObject before we touch them.
        StartCoroutine(InitDropdowns());
    }

    private IEnumerator InitDropdowns()
    {
        yield return null;              // wait one frame for TMP to fully initialise
        SetupEventDropdown();
        SetupLocationDropdown();
        SetupUserPresetDropdown();
    }

    // ── Dropdown setup ────────────────────────────────────────────────────────

    private void SetupEventDropdown()
    {
        if (eventDropdown == null) return;
        eventDropdown.onValueChanged.RemoveListener(OnEventSelected);
        eventDropdown.ClearOptions();
        var options = new List<string> { "events" };
        foreach (var e in events) options.Add(e.label);
        eventDropdown.AddOptions(options);
        eventDropdown.value = 0;
        eventDropdown.RefreshShownValue();
        eventDropdown.onValueChanged.AddListener(OnEventSelected);
    }

    private void SetupLocationDropdown()
    {
        if (locationDropdown == null) return;
        locationDropdown.onValueChanged.RemoveListener(OnLocationSelected);
        locationDropdown.ClearOptions();
        var options = new List<string> { "locations" };
        foreach (var l in locations) options.Add(l.label);
        locationDropdown.AddOptions(options);
        locationDropdown.value = 0;
        locationDropdown.RefreshShownValue();
        locationDropdown.onValueChanged.AddListener(OnLocationSelected);
    }

    private void SetupUserPresetDropdown()
    {
        if (userPresetDropdown == null) return;
        userPresetDropdown.onValueChanged.RemoveListener(OnUserPresetSelected);
        userPresetDropdown.ClearOptions();
        var options = new List<string> { "my presets" };
        foreach (var p in userPresets) options.Add(p.label);
        userPresetDropdown.AddOptions(options);
        userPresetDropdown.value = 0;
        userPresetDropdown.RefreshShownValue();
        userPresetDropdown.onValueChanged.AddListener(OnUserPresetSelected);
    }

    // Rebuilds only the user preset dropdown after a save or delete.
    // Waits one frame so TMP processes the ClearOptions/AddOptions cycle correctly.
    private void RefreshUserPresetDropdown()
    {
        StartCoroutine(RefreshUserPresetDropdownRoutine());
    }

    private IEnumerator RefreshUserPresetDropdownRoutine()
    {
        yield return null;
        if (userPresetDropdown == null) yield break;

        userPresetDropdown.onValueChanged.RemoveListener(OnUserPresetSelected);
        userPresetDropdown.ClearOptions();
        var options = new List<string> { "my presets" };
        foreach (var p in userPresets) options.Add(p.label);
        userPresetDropdown.AddOptions(options);
        userPresetDropdown.value = 0;
        userPresetDropdown.RefreshShownValue();
        userPresetDropdown.onValueChanged.AddListener(OnUserPresetSelected);

        Debug.Log($"[UserPreset] Dropdown refreshed — {userPresets.Count} preset(s) listed.");
    }

    // ── Dropdown selection handlers ───────────────────────────────────────────

    private void OnEventSelected(int index)
    {
        if (index == 0) return;

        var p = events[index - 1];
        latDegInput.text = p.latDeg.ToString();
        latMinInput.text = p.latMin.ToString();
        SetDropdown(latHemisphereDropdown, p.latDir);
        lonDegInput.text = p.lonDeg.ToString();
        lonMinInput.text = p.lonMin.ToString();
        SetDropdown(lonHemisphereDropdown, p.lonDir);
        dateInput.text = new DateTime(p.year, p.month, p.day).ToString(DateFormat);
        timeInput.text = new DateTime(1, 1, 1, p.hour, p.minute, 0).ToString(TimeFormat);

        if (locationDropdown   != null) locationDropdown.SetValueWithoutNotify(0);
        if (userPresetDropdown != null) userPresetDropdown.SetValueWithoutNotify(0);
    }

    private void OnLocationSelected(int index)
    {
        if (index == 0) return;

        var p = locations[index - 1];
        latDegInput.text = p.latDeg.ToString();
        latMinInput.text = p.latMin.ToString();
        SetDropdown(latHemisphereDropdown, p.latDir);
        lonDegInput.text = p.lonDeg.ToString();
        lonMinInput.text = p.lonMin.ToString();
        SetDropdown(lonHemisphereDropdown, p.lonDir);

        if (eventDropdown      != null) eventDropdown.SetValueWithoutNotify(0);
        if (userPresetDropdown != null) userPresetDropdown.SetValueWithoutNotify(0);
    }

    private void OnUserPresetSelected(int index)
    {
        if (index == 0) return;

        var p = userPresets[index - 1];
        latDegInput.text = p.latDeg.ToString();
        latMinInput.text = p.latMin.ToString();
        SetDropdown(latHemisphereDropdown, p.latDir);
        lonDegInput.text = p.lonDeg.ToString();
        lonMinInput.text = p.lonMin.ToString();
        SetDropdown(lonHemisphereDropdown, p.lonDir);
        dateInput.text = new DateTime(p.year, p.month, p.day).ToString(DateFormat);
        timeInput.text = new DateTime(1, 1, 1, p.hour, p.minute, 0).ToString(TimeFormat);

        if (eventDropdown    != null) eventDropdown.SetValueWithoutNotify(0);
        if (locationDropdown != null) locationDropdown.SetValueWithoutNotify(0);
    }

    // ── Save preset ───────────────────────────────────────────────────────────

    /// <summary>Wire the Save Preset button OnClick to this.</summary>
    public void SaveCurrentPreset()
    {
        ClearError();

        // Read the name the user typed; fall back to a timestamp if they left it blank
        string name = "";
        if (presetNameInput != null)
            name = presetNameInput.text.Trim();
        else
            Debug.LogWarning("[UserPreset] presetNameInput is null — assign it in the Inspector.");

        if (string.IsNullOrWhiteSpace(name))
            name = $"Preset {DateTime.Now:yyyy-MM-dd HH:mm}";

        // Read current field values
        string latDegText = latDegInput != null ? latDegInput.text.Trim() : "";
        string latMinText = latMinInput != null ? latMinInput.text.Trim() : "";
        string lonDegText = lonDegInput != null ? lonDegInput.text.Trim() : "";
        string lonMinText = lonMinInput != null ? lonMinInput.text.Trim() : "";
        string dateText   = dateInput   != null ? dateInput.text.Trim()   : "";
        string timeText   = timeInput   != null ? timeInput.text.Trim()   : "";

        // All fields required
        if (string.IsNullOrWhiteSpace(latDegText) || string.IsNullOrWhiteSpace(latMinText) ||
            string.IsNullOrWhiteSpace(lonDegText) || string.IsNullOrWhiteSpace(lonMinText) ||
            string.IsNullOrWhiteSpace(dateText)   || string.IsNullOrWhiteSpace(timeText))
        { ShowError("Fill in all fields before saving a preset."); return; }

        if (!int.TryParse(latDegText,    NumberStyles.Integer, CultureInfo.InvariantCulture, out int latDeg)    || latDeg < 0 || latDeg > 90)
        { ShowError("Invalid latitude degrees (0–90).");    return; }
        if (!double.TryParse(latMinText, NumberStyles.Float,   CultureInfo.InvariantCulture, out double latMin) || latMin < 0 || latMin >= 60)
        { ShowError("Invalid latitude minutes (0–59.999)."); return; }
        if (!int.TryParse(lonDegText,    NumberStyles.Integer, CultureInfo.InvariantCulture, out int lonDeg)    || lonDeg < 0 || lonDeg > 180)
        { ShowError("Invalid longitude degrees (0–180)."); return; }
        if (!double.TryParse(lonMinText, NumberStyles.Float,   CultureInfo.InvariantCulture, out double lonMin) || lonMin < 0 || lonMin >= 60)
        { ShowError("Invalid longitude minutes (0–59.999)."); return; }

        if (!DateTime.TryParseExact(dateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime datePart))
        { ShowError("Date must be in format yyyy-MM-dd."); return; }
        if (!DateTime.TryParseExact(timeText, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timePart))
        { ShowError("Time must be in format HH:mm (24-hour)."); return; }

        string latDir = GetDropdownText(latHemisphereDropdown);
        string lonDir = GetDropdownText(lonHemisphereDropdown);

        userPresets.Add(new UserPreset
        {
            label  = name,
            latDeg = latDeg,  latMin = latMin, latDir = latDir,
            lonDeg = lonDeg,  lonMin = lonMin, lonDir = lonDir,
            year   = datePart.Year,  month  = datePart.Month,  day    = datePart.Day,
            hour   = timePart.Hour,  minute = timePart.Minute
        });

        SaveUserPresetsToPrefs();
        RefreshUserPresetDropdown();

        if (presetNameInput != null) presetNameInput.text = "";
        ShowSuccess($"Preset \"{name}\" saved!");
        Debug.Log($"[UserPreset] Saved '{name}'. Total: {userPresets.Count}");
    }

    // ── Delete preset ─────────────────────────────────────────────────────────

    /// <summary>Wire the Delete Preset button OnClick to this.</summary>
    public void DeleteSelectedUserPreset()
    {
        if (userPresetDropdown == null)
        { ShowError("User preset dropdown is not assigned."); return; }

        int index = userPresetDropdown.value;

        if (index == 0 || userPresets.Count == 0)
        { ShowError("Select a saved preset from 'My Presets' to delete it."); return; }

        int listIndex = index - 1;
        if (listIndex < 0 || listIndex >= userPresets.Count) return;

        string deletedName = userPresets[listIndex].label;
        userPresets.RemoveAt(listIndex);
        SaveUserPresetsToPrefs();
        RefreshUserPresetDropdown();

        ShowSuccess($"Preset \"{deletedName}\" deleted.");
        Debug.Log($"[UserPreset] Deleted '{deletedName}'. Remaining: {userPresets.Count}");
    }

    // ── PlayerPrefs persistence ───────────────────────────────────────────────

    private void SaveUserPresetsToPrefs()
    {
        string json = JsonUtility.ToJson(new UserPresetList { presets = userPresets });
        PlayerPrefs.SetString(UserPresetsKey, json);
        PlayerPrefs.Save();
        Debug.Log($"[UserPreset] Persisted {userPresets.Count} preset(s) to PlayerPrefs.");
    }

    private void LoadUserPresets()
    {
        if (!PlayerPrefs.HasKey(UserPresetsKey))
        { Debug.Log("[UserPreset] No saved presets found."); return; }

        string json = PlayerPrefs.GetString(UserPresetsKey, "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var wrapper = JsonUtility.FromJson<UserPresetList>(json);
            if (wrapper?.presets != null)
            {
                userPresets = wrapper.presets;
                Debug.Log($"[UserPreset] Loaded {userPresets.Count} preset(s) from PlayerPrefs.");
            }
        }
        catch (Exception ex) { Debug.LogError($"[UserPreset] Failed to load presets: {ex.Message}"); }
    }

    // ── Submit / load sky scene ───────────────────────────────────────────────

    public void GrabAllInputs()
    {
        Debug.Log("Submit clicked: GrabAllInputs() called");

        EnsureErrorReference();
        ClearError();

        string dateText   = dateInput   != null ? dateInput.text.Trim()   : "";
        string timeText   = timeInput   != null ? timeInput.text.Trim()   : "";
        string latDegText = latDegInput != null ? latDegInput.text.Trim() : "";
        string latMinText = latMinInput != null ? latMinInput.text.Trim() : "";
        string lonDegText = lonDegInput != null ? lonDegInput.text.Trim() : "";
        string lonMinText = lonMinInput != null ? lonMinInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(dateText)   || string.IsNullOrWhiteSpace(timeText)   ||
            string.IsNullOrWhiteSpace(latDegText) || string.IsNullOrWhiteSpace(latMinText) ||
            string.IsNullOrWhiteSpace(lonDegText) || string.IsNullOrWhiteSpace(lonMinText))
        {
            ShowError("Please fill in all fields.");
            return;
        }

        if (!int.TryParse(latDegText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int latDeg) ||
            latDeg < 0 || latDeg > 90)
        { ShowError("Latitude degrees must be an integer between 0 and 90."); return; }

        if (!double.TryParse(latMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out double latMin) ||
            latMin < 0 || latMin >= 60)
        { ShowError("Latitude minutes must be a number between 0 and 59.999."); return; }

        if (!int.TryParse(lonDegText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lonDeg) ||
            lonDeg < 0 || lonDeg > 180)
        { ShowError("Longitude degrees must be an integer between 0 and 180."); return; }

        if (!double.TryParse(lonMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out double lonMin) ||
            lonMin < 0 || lonMin >= 60)
        { ShowError("Longitude minutes must be a number between 0 and 59.999."); return; }

        if (latDeg == 90  && latMin != 0) { ShowError("Latitude minutes must be 0 when degrees is 90.");   return; }
        if (lonDeg == 180 && lonMin != 0) { ShowError("Longitude minutes must be 0 when degrees is 180."); return; }

        string latHem = GetDropdownText(latHemisphereDropdown);
        string lonHem = GetDropdownText(lonHemisphereDropdown);

        if (latHem != "N" && latHem != "S") { ShowError("Latitude hemisphere must be N or S.");  return; }
        if (lonHem != "E" && lonHem != "W") { ShowError("Longitude hemisphere must be E or W."); return; }

        double latAbs = latDeg + (latMin / 60.0);
        double lonAbs = lonDeg + (lonMin / 60.0);
        double lat    = (latHem == "S") ? -latAbs :  latAbs;
        double lon    = (lonHem == "W") ? -lonAbs :  lonAbs;

        if (!DateTime.TryParseExact(dateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime datePart))
        { ShowError("Date must be in format yyyy-MM-dd (example: 2026-02-05)."); return; }

        if (!DateTime.TryParseExact(timeText, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timePart))
        { ShowError("Time must be in format HH:mm (24-hour, example: 21:30)."); return; }

        DateTime localDateTime = new DateTime(
            datePart.Year, datePart.Month, datePart.Day,
            timePart.Hour, timePart.Minute, 0,
            DateTimeKind.Unspecified);

        if (localDateTime < MinDate || localDateTime > MaxDate)
        { ShowError($"Date must be between {MinDate:yyyy-MM-dd} and {MaxDate:yyyy-MM-dd}."); return; }

        if (SkySession.Instance == null)
        {
            var go = new GameObject("SkySession");
            go.AddComponent<SkySession>();
        }

        string latRaw = $"{latDeg}° {latMin.ToString(CultureInfo.InvariantCulture)}' {latHem}";
        string lonRaw = $"{lonDeg}° {lonMin.ToString(CultureInfo.InvariantCulture)}' {lonHem}";

        SkySession.Instance.SetInputs(
            latDeg:  lat,
            lonDeg:  lon,
            localDt: localDateTime,
            latRaw:  latRaw,
            lonRaw:  lonRaw,
            dateRaw: dateText,
            timeRaw: timeText);

        SceneManager.LoadScene(skySceneName);
    }

    // ── Auto-wire helpers ─────────────────────────────────────────────────────

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
            Debug.LogError("Could not find a TMP text object named 'ErrorMsg'.");
    }

    private void AutoWireIfMissing()
    {
        latDegInput = latDegInput ?? FindInput("LatitudeDegreesResponse");
        latMinInput = latMinInput ?? FindInput("LatitudeMinutesResponse");
        lonDegInput = lonDegInput ?? FindInput("LongitudeDegreesResponse");
        lonMinInput = lonMinInput ?? FindInput("LongitudeMinutesResponse");

        latHemisphereDropdown = latHemisphereDropdown ?? FindDropdown("LatHemisphereDropDown");
        lonHemisphereDropdown = lonHemisphereDropdown ?? FindDropdown("LongHemisphereDropDown");

        if (presetNameInput == null)
        {
            presetNameInput = FindInput("PresetNameInput");
            if (presetNameInput == null)
                Debug.LogWarning("[AutoWire] presetNameInput not found — assign it in the Inspector.");
        }

        if (userPresetDropdown == null)
        {
            userPresetDropdown = FindDropdown("UserPresetDropdown");
            if (userPresetDropdown == null)
                Debug.LogWarning("[AutoWire] userPresetDropdown not found — assign it in the Inspector.");
        }

        Debug.Log(
            $"[AutoWire] on '{gameObject.name}' " +
            $"latDeg={(latDegInput!=null)} latMin={(latMinInput!=null)} " +
            $"lonDeg={(lonDegInput!=null)} lonMin={(lonMinInput!=null)} " +
            $"presetNameInput={(presetNameInput!=null)} userPresetDropdown={(userPresetDropdown!=null)}"
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

    // ── Shared helpers ────────────────────────────────────────────────────────

    private void SetDropdown(TMP_Dropdown dropdown, string value)
    {
        if (dropdown == null) return;
        for (int i = 0; i < dropdown.options.Count; i++)
        {
            if (dropdown.options[i].text == value)
            {
                dropdown.value = i;
                return;
            }
        }
    }

    private static string GetDropdownText(TMP_Dropdown dd)
    {
        if (dd == null) return "";
        if (dd.options == null || dd.options.Count == 0) return "";
        int i = dd.value;
        if (i < 0 || i >= dd.options.Count) return "";
        return (dd.options[i].text ?? "").Trim().ToUpperInvariant();
    }

    // ── Error / success display ───────────────────────────────────────────────

    private void ShowError(string message)
    {
        Debug.LogWarning(message);
        if (errorText == null) { Debug.LogError("ShowError failed: errorText is null."); return; }
        if (!errorText.gameObject.activeInHierarchy) errorText.gameObject.SetActive(true);
        errorText.enabled = true;
        errorText.color   = Color.red;
        errorText.text    = message;
    }

    private void ShowSuccess(string message)
    {
        if (errorText == null) return;
        if (!errorText.gameObject.activeInHierarchy) errorText.gameObject.SetActive(true);
        errorText.enabled = true;
        errorText.color   = Color.green;
        errorText.text    = message;
    }

    private void ClearError()
    {
        if (errorText == null) return;
        errorText.text    = "";
        errorText.enabled = false;
    }

    // ── Scene navigation ──────────────────────────────────────────────────────

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("Title");
    }
}