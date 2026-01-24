using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

[System.Serializable]
public class StarRecord
{
    // --- Catalog identifiers (may be empty / -1 if missing) ---
    public int id;   // Database primary key in HYG
    public int hip;  // Hipparcos ID
    public int hd;   // Henry Draper ID
    public int hr;   // Harvard Revised ID
    public string gl; // Gliese catalog ID (string)
    public string bf; // Bayer/Flamsteed designation (string)

    public string proper; // Common name, if any

    // --- Equatorial coordinates (J2000) ---
    // NOTE: In hyg_v42.csv, RA is stored in HOURS, Dec in DEGREES.
    public float ra;   // Right Ascension (hours)
    public float dec;  // Declination (degrees)

    // --- Distance and photometry ---
    public float dist; // Distance in parsecs
    public float mag;  // Apparent magnitude (lower = brighter)
    public string spect; // Spectral type (e.g., "G2V")
    public float ci;   // Color index (B-V)

    // --- Proper motion (milliarcseconds per year) ---
    public float pmra;
    public float pmdec;

    // --- Cartesian position (parsecs), relative to the Sun ---
    public float x;
    public float y;
    public float z;

    // --- Cartesian velocity (parsecs per year) ---
    public float vx;
    public float vy;
    public float vz;

    // --- Precomputed radians (useful for fast trig later) ---
    public float rarad;    // RA in radians
    public float decrad;   // Dec in radians
    public float pmrarad;  // Proper motion RA in rad/year
    public float pmdecrad; // Proper motion Dec in rad/year
}

public class HYGCatalogParser : MonoBehaviour
{
    [Header("Catalog Settings")]
    public string catalogFileName = "hyg_v42.csv"; // Expected in StreamingAssets

    [Header("Filtering")]
    public float magnitudeLimit = 6.0f; // Tier 1: ignore stars dimmer than 6.0

    [Header("Debug Verification")]
    public bool logSampleRows = true; // Print first N parsed rows to console
    public int sampleRowCount = 30;

    // Full catalog in memory
    public List<StarRecord> Stars { get; private set; }

    // Cached subset for rendering (mag <= magnitudeLimit), sorted deterministically
    public List<StarRecord> VisibleStarsMag6 { get; private set; }

    void Awake()
    {
        // Parse as soon as this component loads (runtime load requirement).
        ParseCatalog();
    }

    public void ParseCatalog()
    {
        // Pre-allocate to reduce GC / resizing cost (HYG ~120k rows).
        Stars = new List<StarRecord>(120000);
        VisibleStarsMag6 = new List<StarRecord>(10000);

        string path = Path.Combine(Application.streamingAssetsPath, catalogFileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"Catalog file not found: {path}");
            return;
        }

        try
        {
            using (var reader = new StreamReader(path))
            {
                // First line is the CSV header (column names).
                string header = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(header))
                {
                    Debug.LogError("Catalog header line was empty.");
                    return;
                }

                if (logSampleRows)
                    Debug.Log("HYG CSV Header: " + header);

                int lineNumber = 1; // header line
                int logged = 0;

                while (!reader.EndOfStream)
                {
                    lineNumber++;
                    string line = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] fields = line.Split(',');

                    // We access indices up to 26 (pmdecrad), so ensure the row is long enough.
                    if (fields.Length <= 26)
                    {
                        Debug.LogWarning(
                            $"{{\"level\":\"warn\",\"code\":\"catalog_row_too_short\",\"line\":{lineNumber},\"fields\":{fields.Length}}}"
                        );
                        continue;
                    }

                    try
                    {
                        var record = new StarRecord
                        {
                            id = ParseInt(fields[0]),
                            hip = ParseInt(fields[1]),
                            hd = ParseInt(fields[2]),
                            hr = ParseInt(fields[3]),
                            gl = fields[4].Trim('"'),
                            bf = fields[5].Trim('"'),
                            proper = fields[6].Trim('"'),

                            ra = ParseFloat(fields[7]),
                            dec = ParseFloat(fields[8]),
                            dist = ParseFloat(fields[9]),

                            pmra = ParseFloat(fields[10]),
                            pmdec = ParseFloat(fields[11]),

                            mag = ParseFloat(fields[13]),
                            spect = fields[15].Trim('"'),
                            ci = ParseFloat(fields[16]),

                            x = ParseFloat(fields[17]),
                            y = ParseFloat(fields[18]),
                            z = ParseFloat(fields[19]),

                            vx = ParseFloat(fields[20]),
                            vy = ParseFloat(fields[21]),
                            vz = ParseFloat(fields[22]),

                            rarad = ParseFloat(fields[23]),
                            decrad = ParseFloat(fields[24]),
                            pmrarad = ParseFloat(fields[25]),
                            pmdecrad = ParseFloat(fields[26]),
                        };

                        Stars.Add(record);

                        // Build the “renderable” subset now so later systems don’t re-filter every frame.
                        if (!float.IsNaN(record.mag) && record.mag <= magnitudeLimit)
                            VisibleStarsMag6.Add(record);

                        // Optional: quick sanity check of parsed values
                        if (logSampleRows && logged < sampleRowCount)
                        {
                            logged++;
                            Debug.Log(
                                $"HYG line {lineNumber} | id {record.id} | hip {record.hip} | proper '{record.proper}'" +
                                $" | ra(h) {record.ra} | dec(deg) {record.dec} | mag {record.mag} | dist {record.dist}"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        // Keep going if a single row is malformed.
                        Debug.LogWarning(
                            $"{{\"level\":\"warn\",\"code\":\"catalog_row_parse_error\",\"line\":{lineNumber},\"message\":\"{Escape(ex.Message)}\"}}"
                        );
                    }
                }
            }

            // Deterministic ordering for rendering: bright-to-dim, then stable by id.
            VisibleStarsMag6.Sort((a, b) =>
            {
                int m = a.mag.CompareTo(b.mag);
                if (m != 0) return m;
                return a.id.CompareTo(b.id);
            });

            Debug.Log($"HYG Catalog Parsed: {Stars.Count} stars. Visible (mag<={magnitudeLimit:0.0}): {VisibleStarsMag6.Count}.");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }
    }

    // Parses ints using invariant culture. Returns -1 if blank or invalid.
    private static int ParseInt(string value)
    {
        value = value.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value)) return -1;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : -1;
    }

    // Parses floats using invariant culture. Returns NaN if blank or invalid.
    private static float ParseFloat(string value)
    {
        value = value.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value)) return float.NaN;
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : float.NaN;
    }

    // Escapes strings log lines won't break.
    private static string Escape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
