using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InputFieldGrabber : MonoBehaviour
{
    // ─── Inspector fields ────────────────────────────────────────────────────

    [Header("Preset Dropdowns")]
    public TMP_Dropdown eventDropdown;
    public TMP_Dropdown locationDropdown;
    public TMP_Dropdown userPresetDropdown;    

    [Header("Save Preset UI")]
    public TMP_InputField presetNameInput;      

    [Header("UI Inputs")]
    public TMP_InputField dateInput;
    public TMP_InputField timeInput;

    [Header("Latitude")]
    public TMP_InputField latDegInput;
    public TMP_InputField latMinInput;
    public TMP_Dropdown   latHemisphereDropdown;

    [Header("Longitude")]
    public TMP_InputField lonDegInput;
    public TMP_InputField lonMinInput;
    public TMP_Dropdown   lonHemisphereDropdown;

    [Header("Error Display")]
    public TMP_Text errorText;

    [Header("Scene Loading")]
    public string skySceneName = "SkyScene";

    [Header("Debug / Testing")]
    [Tooltip("Tick in the Inspector and hit Play to wipe all saved user presets. Auto-resets to false after one run.")]
    public bool clearUserPresetsOnAwake = false;

    // ─── Constants ───────────────────────────────────────────────────────────

    private const string DateFormat      = "yyyy-MM-dd";
    private const string TimeFormat      = "HH:mm";
    private const string UserPresetsKey  = "UserPresets_v1";

    private static readonly DateTime MinDate = new DateTime(1900, 1, 1);
    private static readonly DateTime MaxDate = new DateTime(2100, 1, 1);

    // ─── Runtime state ────────────────────────────────────────────────────────

    private List<UserPreset> userPresets = new List<UserPreset>();

    // ─── Data classes ─────────────────────────────────────────────────────────

    [Serializable]
    public class EventPreset
    {
        public string label;
        public int    latDeg, lonDeg;
        public double latMin, lonMin;
        public string latDir, lonDir;
        public int    year, month, day, hour, minute;
    }

    [Serializable]
    public class LocationPreset
    {
        public string label;
        public int    latDeg, lonDeg;
        public double latMin, lonMin;
        public string latDir, lonDir;
    }

    [Serializable]
    public class UserPreset
    {
        public string label;
        public int    latDeg, lonDeg;
        public double latMin, lonMin;
        public string latDir, lonDir;
        public int    year, month, day, hour, minute;
    }

    [Serializable]
    private class UserPresetList { public List<UserPreset> presets = new List<UserPreset>(); }

    // ─── Built-in data ────────────────────────────────────────────────────────

    private readonly List<EventPreset> events = new List<EventPreset>
    {
        new EventPreset { label="Planetary Parade",              latDeg=40, latMin=42.7, latDir="N", lonDeg=74,  lonMin=0.4,  lonDir="W", year=2026, month=2,  day=28, hour=19, minute=0  },
        new EventPreset { label="Apollo 11 Launch",              latDeg=28, latMin=31.0, latDir="N", lonDeg=80,  lonMin=39.0, lonDir="W", year=1969, month=7,  day=16, hour=21, minute=0  },
        new EventPreset { label="Artemis II Launch",             latDeg=28, latMin=37.6, latDir="N", lonDeg=80,  lonMin=37.3, lonDir="W", year=2026, month=4,  day=1,  hour=22, minute=35 },
        new EventPreset { label="Venus and Jupiter Conjunction", latDeg=51, latMin=30.0, latDir="N", lonDeg=0,   lonMin=7.5,  lonDir="W", year=2026, month=6,  day=8,  hour=15, minute=0  },
        new EventPreset { label="Total Solar Eclipse 2024",      latDeg=37, latMin=5.0,  latDir="N", lonDeg=88,  lonMin=38.0, lonDir="W", year=2024, month=4,  day=8,  hour=14, minute=0  },
    };

    private readonly List<LocationPreset> locations = new List<LocationPreset>
    {
        new LocationPreset { label="NYC Statue of Liberty", latDeg=40, latMin=41.3, latDir="N", lonDeg=74,  lonMin=2.7,  lonDir="W" },
        new LocationPreset { label="Big Ben Clocktower",    latDeg=51, latMin=30.0, latDir="N", lonDeg=0,   lonMin=7.5,  lonDir="W" },
        new LocationPreset { label="Colosseum",             latDeg=23, latMin=24.5, latDir="N", lonDeg=25,  lonMin=39.8, lonDir="E" },
        new LocationPreset { label="Eiffel Tower",          latDeg=48, latMin=51.0, latDir="N", lonDeg=2,   lonMin=17.7, lonDir="E" },
        new LocationPreset { label="Golden Gate Bridge",    latDeg=37, latMin=49.2, latDir="N", lonDeg=122, lonMin=28.7, lonDir="W" },
        new LocationPreset { label="Mount Everest",         latDeg=27, latMin=59.3, latDir="N", lonDeg=86,  lonMin=55.5, lonDir="E" },
    };

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureErrorReference();
        AutoWireIfMissing();
        ClearError();

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
        // Populate dropdowns in Start rather than Awake so that TMP_Dropdown's
        // own Awake (which runs on its GameObject) has already completed first.
        StartCoroutine(InitDropdowns());
    }

    // Waiting one frame ensures TMP has fully initialised every dropdown
    // before we call ClearOptions / AddOptions on any of them.
    private IEnumerator InitDropdowns()
    {
        yield return null;
        SetupEventDropdown();
        SetupLocationDropdown();
        SetupUserPresetDropdown();
    }

    // ─── Dropdown initialisation ──────────────────────────────────────────────

    private void SetupEventDropdown()
    {
        if (eventDropdown == null) return;
        eventDropdown.onValueChanged.RemoveListener(OnEventSelected);
        eventDropdown.ClearOptions();
        var opts = new List<string> { "events" };
        foreach (var e in events) opts.Add(e.label);
        eventDropdown.AddOptions(opts);
        eventDropdown.value = 0;
        eventDropdown.RefreshShownValue();
        eventDropdown.onValueChanged.AddListener(OnEventSelected);
    }

    private void SetupLocationDropdown()
    {
        if (locationDropdown == null) return;
        locationDropdown.onValueChanged.RemoveListener(OnLocationSelected);
        locationDropdown.ClearOptions();
        var opts = new List<string> { "locations" };
        foreach (var l in locations) opts.Add(l.label);
        locationDropdown.AddOptions(opts);
        locationDropdown.value = 0;
        locationDropdown.RefreshShownValue();
        locationDropdown.onValueChanged.AddListener(OnLocationSelected);
    }

    private void SetupUserPresetDropdown()
    {
        if (userPresetDropdown == null) return;
        userPresetDropdown.onValueChanged.RemoveListener(OnUserPresetSelected);
        userPresetDropdown.ClearOptions();
        var opts = new List<string> { "my presets" };
        foreach (var p in userPresets) opts.Add(p.label);
        userPresetDropdown.AddOptions(opts);
        userPresetDropdown.value = 0;
        userPresetDropdown.RefreshShownValue();
        userPresetDropdown.onValueChanged.AddListener(OnUserPresetSelected);
    }

    // ─── Refresh user preset dropdown after save / delete ─────────────────────

    // Called after any change to userPresets. Waits one frame so TMP is ready.
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

        var opts = new List<string> { "my presets" };
        foreach (var p in userPresets) opts.Add(p.label);
        userPresetDropdown.AddOptions(opts);

        userPresetDropdown.value = 0;
        userPresetDropdown.RefreshShownValue();

        userPresetDropdown.onValueChanged.AddListener(OnUserPresetSelected);

        Debug.Log($"[UserPreset] Dropdown refreshed — {userPresets.Count} user preset(s) listed.");
    }

    // ─── Dropdown selection handlers ─────────────────────────────────────────

    private void OnEventSelected(int index)
    {
        if (index == 0) return;
        if (index < 1 || index > events.Count) return;

        var p = events[index - 1];
        latDegInput.text = p.latDeg.ToString();
        latMinInput.text = p.latMin.ToString(CultureInfo.InvariantCulture);
        SetDropdown(latHemisphereDropdown, p.latDir);
        lonDegInput.text = p.lonDeg.ToString();
        lonMinInput.text = p.lonMin.ToString(CultureInfo.InvariantCulture);
        SetDropdown(lonHemisphereDropdown, p.lonDir);
        dateInput.text = new DateTime(p.year, p.month, p.day).ToString(DateFormat);
        timeInput.text = new DateTime(1, 1, 1, p.hour, p.minute, 0).ToString(TimeFormat);

        // Reset the other two dropdowns to their placeholders
        if (locationDropdown    != null) locationDropdown.SetValueWithoutNotify(0);
        if (userPresetDropdown  != null) userPresetDropdown.SetValueWithoutNotify(0);
    }

    private void OnLocationSelected(int index)
    {
        if (index == 0) return;
        if (index < 1 || index > locations.Count) return;

        var p = locations[index - 1];
        latDegInput.text = p.latDeg.ToString();
        latMinInput.text = p.latMin.ToString(CultureInfo.InvariantCulture);
        SetDropdown(latHemisphereDropdown, p.latDir);
        lonDegInput.text = p.lonDeg.ToString();
        lonMinInput.text = p.lonMin.ToString(CultureInfo.InvariantCulture);
        SetDropdown(lonHemisphereDropdown, p.lonDir);

        if (eventDropdown      != null) eventDropdown.SetValueWithoutNotify(0);
        if (userPresetDropdown != null) userPresetDropdown.SetValueWithoutNotify(0);
    }

    private void OnUserPresetSelected(int index)
    {
        if (index == 0) return;
        if (index < 1 || index > userPresets.Count) return;

        var p = userPresets[index - 1];
        latDegInput.text = p.latDeg.ToString();
        latMinInput.text = p.latMin.ToString(CultureInfo.InvariantCulture);
        SetDropdown(latHemisphereDropdown, p.latDir);
        lonDegInput.text = p.lonDeg.ToString();
        lonMinInput.text = p.lonMin.ToString(CultureInfo.InvariantCulture);
        SetDropdown(lonHemisphereDropdown, p.lonDir);
        dateInput.text = new DateTime(p.year, p.month, p.day).ToString(DateFormat);
        timeInput.text = new DateTime(1, 1, 1, p.hour, p.minute, 0).ToString(TimeFormat);

        if (eventDropdown    != null) eventDropdown.SetValueWithoutNotify(0);
        if (locationDropdown != null) locationDropdown.SetValueWithoutNotify(0);
    }

    // ─── Save preset ─────────────────────────────────────────────────────────

    public void SaveCurrentPreset()
    {
        ClearError();

        // Resolve name
        string name = "";
        if (presetNameInput != null)
            name = presetNameInput.text.Trim();
        else
            Debug.LogWarning("[UserPreset] presetNameInput is null — assign it in the Inspector.");

        if (string.IsNullOrWhiteSpace(name))
            name = $"Preset {DateTime.Now:yyyy-MM-dd HH:mm}";

        // Read fields
        string latDegText = latDegInput != null ? latDegInput.text.Trim() : "";
        string latMinText = latMinInput != null ? latMinInput.text.Trim() : "";
        string lonDegText = lonDegInput != null ? lonDegInput.text.Trim() : "";
        string lonMinText = lonMinInput != null ? lonMinInput.text.Trim() : "";
        string dateText   = dateInput   != null ? dateInput.text.Trim()   : "";
        string timeText   = timeInput   != null ? timeInput.text.Trim()   : "";

        // Validate — all fields required
        if (string.IsNullOrWhiteSpace(latDegText) || string.IsNullOrWhiteSpace(latMinText) ||
            string.IsNullOrWhiteSpace(lonDegText) || string.IsNullOrWhiteSpace(lonMinText) ||
            string.IsNullOrWhiteSpace(dateText)   || string.IsNullOrWhiteSpace(timeText))
        { ShowError("Fill in all fields before saving a preset."); return; }

        if (!int.TryParse(latDegText,    NumberStyles.Integer, CultureInfo.InvariantCulture, out int latDeg)    || latDeg < 0 || latDeg > 90)
        { ShowError("Invalid latitude degrees (0–90).");   return; }
        if (!double.TryParse(latMinText, NumberStyles.Float,   CultureInfo.InvariantCulture, out double latMin) || latMin < 0 || latMin >= 60)
        { ShowError("Invalid latitude minutes (0–59.999)."); return; }
        if (!int.TryParse(lonDegText,    NumberStyles.Integer, CultureInfo.InvariantCulture, out int lonDeg)    || lonDeg < 0 || lonDeg > 180)
        { ShowError("Invalid longitude degrees (0–180)."); return; }
        if (!double.TryParse(lonMinText, NumberStyles.Float,   CultureInfo.InvariantCulture, out double lonMin) || lonMin < 0 || lonMin >= 60)
        { ShowError("Invalid longitude minutes (0–59.999)."); return; }

        if (!DateTime.TryParseExact(dateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime datePart))
        { ShowError("Date must be YYYY-MM-DD."); return; }
        if (!DateTime.TryParseExact(timeText, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timePart))
        { ShowError("Time must be HH:mm (24-hour)."); return; }

        string latDir = GetDropdownText(latHemisphereDropdown);
        string lonDir = GetDropdownText(lonHemisphereDropdown);

        // Build and store
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

    // ─── Delete preset ────────────────────────────────────────────────────────
    public void DeleteSelectedUserPreset()
    {
        if (userPresetDropdown == null)
        {
            ShowError("User preset dropdown is not assigned.");
            return;
        }

        int index = userPresetDropdown.value;

        // index 0 is the "my presets" placeholder
        if (index == 0 || userPresets.Count == 0)
        {
            ShowError("Select a saved preset from 'My Presets' to delete it.");
            return;
        }

        int listIndex = index - 1;
        if (listIndex < 0 || listIndex >= userPresets.Count) return;

        string deletedName = userPresets[listIndex].label;
        userPresets.RemoveAt(listIndex);
        SaveUserPresetsToPrefs();
        RefreshUserPresetDropdown();

        ShowSuccess($"Preset \"{deletedName}\" deleted.");
        Debug.Log($"[UserPreset] Deleted '{deletedName}'. Remaining: {userPresets.Count}");
    }

    // ─── PlayerPrefs persistence ─────────────────────────────────────────────

    private void SaveUserPresetsToPrefs()
    {
        string json = JsonUtility.ToJson(new UserPresetList { presets = userPresets });
        PlayerPrefs.SetString(UserPresetsKey, json);
        PlayerPrefs.Save();
        Debug.Log($"[UserPreset] Saved {userPresets.Count} preset(s) to PlayerPrefs.");
    }

    private void LoadUserPresets()
    {
        if (!PlayerPrefs.HasKey(UserPresetsKey))
        {
            Debug.Log("[UserPreset] No saved presets in PlayerPrefs.");
            return;
        }
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
        catch (Exception ex) { Debug.LogError($"[UserPreset] Failed to load: {ex.Message}"); }
    }

    // ─── Submit / load sky scene ─────────────────────────────────────────────

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
        { ShowError("Please fill in all fields."); return; }

        if (!int.TryParse(latDegText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int latDeg) || latDeg < 0 || latDeg > 90)
        { ShowError("Latitude degrees must be 0–90."); return; }
        if (!double.TryParse(latMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out double latMin) || latMin < 0 || latMin >= 60)
        { ShowError("Latitude minutes must be 0–59.999."); return; }
        if (!int.TryParse(lonDegText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lonDeg) || lonDeg < 0 || lonDeg > 180)
        { ShowError("Longitude degrees must be 0–180."); return; }
        if (!double.TryParse(lonMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out double lonMin) || lonMin < 0 || lonMin >= 60)
        { ShowError("Longitude minutes must be 0–59.999."); return; }

        if (latDeg == 90  && latMin != 0) { ShowError("Latitude minutes must be 0 when degrees is 90.");   return; }
        if (lonDeg == 180 && lonMin != 0) { ShowError("Longitude minutes must be 0 when degrees is 180."); return; }

        string latHem = GetDropdownText(latHemisphereDropdown);
        string lonHem = GetDropdownText(lonHemisphereDropdown);
        if (latHem != "N" && latHem != "S") { ShowError("Latitude hemisphere must be N or S.");  return; }
        if (lonHem != "E" && lonHem != "W") { ShowError("Longitude hemisphere must be E or W."); return; }

        double lat = ((latHem == "S") ? -1 : 1) * (latDeg + latMin / 60.0);
        double lon = ((lonHem == "W") ? -1 : 1) * (lonDeg + lonMin / 60.0);

        if (!DateTime.TryParseExact(dateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime datePart))
        { ShowError("Date must be in format yyyy-MM-dd (e.g. 2026-02-05)."); return; }
        if (!DateTime.TryParseExact(timeText, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timePart))
        { ShowError("Time must be in format HH:mm (e.g. 21:30)."); return; }

        DateTime localDateTime = new DateTime(datePart.Year, datePart.Month, datePart.Day,
                                              timePart.Hour, timePart.Minute, 0, DateTimeKind.Unspecified);
        if (localDateTime < MinDate || localDateTime > MaxDate)
        { ShowError($"Date must be between {MinDate:yyyy-MM-dd} and {MaxDate:yyyy-MM-dd}."); return; }

        if (SkySession.Instance == null)
            new GameObject("SkySession").AddComponent<SkySession>();

        string latRaw = $"{latDeg}° {latMin.ToString(CultureInfo.InvariantCulture)}' {latHem}";
        string lonRaw = $"{lonDeg}° {lonMin.ToString(CultureInfo.InvariantCulture)}' {lonHem}";

        SkySession.Instance.SetInputs(lat, lon, localDateTime, latRaw, lonRaw, dateText, timeText);
        SceneManager.LoadScene(skySceneName);
    }

    // ─── Auto-wire helpers ────────────────────────────────────────────────────

    private void EnsureErrorReference()
    {
        if (errorText != null) return;
        foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (t != null && t.gameObject.name == "ErrorMsg") { errorText = t; break; }
        }
        if (errorText == null) Debug.LogError("[InputFieldGrabber] Could not find TMP_Text named 'ErrorMsg'.");
    }

    private void AutoWireIfMissing()
    {
        latDegInput  = latDegInput  ?? FindInput("LatitudeDegreesResponse");
        latMinInput  = latMinInput  ?? FindInput("LatitudeMinutesResponse");
        lonDegInput  = lonDegInput  ?? FindInput("LongitudeDegreesResponse");
        lonMinInput  = lonMinInput  ?? FindInput("LongitudeMinutesResponse");

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
    }

    private TMP_InputField FindInput(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) { Debug.LogWarning($"[AutoWire] GameObject '{name}' not found."); return null; }
        return go.GetComponent<TMP_InputField>() ?? go.GetComponentInChildren<TMP_InputField>(true);
    }

    private TMP_Dropdown FindDropdown(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) { Debug.LogWarning($"[AutoWire] GameObject '{name}' not found."); return null; }
        return go.GetComponent<TMP_Dropdown>() ?? go.GetComponentInChildren<TMP_Dropdown>(true);
    }

    // ─── Dropdown / field helpers ─────────────────────────────────────────────

    private void SetDropdown(TMP_Dropdown dd, string value)
    {
        if (dd == null) return;
        for (int i = 0; i < dd.options.Count; i++)
            if (dd.options[i].text == value) { dd.value = i; return; }
    }

    private static string GetDropdownText(TMP_Dropdown dd)
    {
        if (dd == null || dd.options == null || dd.options.Count == 0) return "";
        int i = dd.value;
        if (i < 0 || i >= dd.options.Count) return "";
        return (dd.options[i].text ?? "").Trim().ToUpperInvariant();
    }

    // ─── Error / success feedback ────────────────────────────────────────────

    private void ShowError(string msg)
    {
        Debug.LogWarning(msg);
        if (errorText == null) return;
        if (!errorText.gameObject.activeInHierarchy) errorText.gameObject.SetActive(true);
        errorText.enabled = true;
        errorText.color   = Color.red;
        errorText.text    = msg;
    }

    private void ShowSuccess(string msg)
    {
        if (errorText == null) return;
        if (!errorText.gameObject.activeInHierarchy) errorText.gameObject.SetActive(true);
        errorText.enabled = true;
        errorText.color   = Color.green;
        errorText.text    = msg;
    }

    private void ClearError()
    {
        if (errorText == null) return;
        errorText.text    = "";
        errorText.enabled = false;
    }
}