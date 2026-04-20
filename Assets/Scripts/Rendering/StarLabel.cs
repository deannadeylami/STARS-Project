// StarLabel renders text labels for visible stars on the sky map by their "proper" given name from the HYG catalog
// Converts celestial coordinates (Ra/Dec) into 3D world positions using astronomy calculations based on the observers location and time.

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class catalogErrorData
{
    public bool catalogNull;
    public bool visibleStarsNull;
}

public class StarLabel : MonoBehaviour
{
    public HYGCatalogParser catalog;
    public float skyRadius = 100f;
    public GameObject starLabelPrefab;
    private GameObject labelParent; //Parent object for all labels
    public bool showBelowHorizon = false; //Toggle for whether stars below the horizon should be rendered.
    public bool labelVisible = true;
    public event Action<bool> OnLabelsVisibilityChanged;

    [Header("Label Settings")]
    public float labelScale = 1f;   // Constant size for all labels

    private const double HorizonEpsRad = 1e-6; //small epsilon to prevent floating pt errors at the horizon 
    private List<GameObject> activeLabels = new List<GameObject>(); //keeps track of all currently rendered labels
    private List<(Vector3 position, float magnitude)> placedLabels = new List<(Vector3, float)>();

    void Start()
    {
        RenderLabels();
    }

    public void RenderLabels()
    {
        if (catalog == null || catalog.VisibleStarsMag6 == null)
        {
            Logging.Error(
            "StarLabel", "Catalog missing or not initialized",
            new catalogErrorData
            {
                catalogNull = (catalog == null),
                visibleStarsNull = (catalog == null || catalog.VisibleStarsMag6 == null)
            }
         );
            return;

        }
       

        // Create parent for labels
        labelParent = new GameObject("StarLabels");
        labelParent.transform.parent = transform;

        //time conversions
        DateTimeOffset utc = AstronomyTime.LocalToUtc(SkySession.Instance.LocalDateTime);
        double jd = AstronomyTime.JulianDate(utc);
        double gmst = AstronomyTime.GreenwichMeanSiderealTimeDeg(jd);

        //compute local sidereal time using observer longitude
        double lst = AstronomyTime.LocalSiderealTimeDeg(gmst, SkySession.Instance.LongitudeDeg);

        //Convert observer latitude from degrees to radians
        double latitudeRad = AstronomyTime.DegToRad(SkySession.Instance.LatitudeDeg);

        var sortedStars = new List<StarRecord>(catalog.VisibleStarsMag6);
        sortedStars.Sort((a, b) => a.mag.CompareTo(b.mag));

        //loop through all the stars with a mag <= 6
        foreach (var star in sortedStars)
        {
            //No label get skipped
            if (string.IsNullOrWhiteSpace(star.proper))
                continue;

            //Invalid coordinate data get skipped
            if (float.IsNaN(star.ra) || float.IsNaN(star.dec))
            {
                Logging.Warning(
                    "StarLabel", "Invalid star coordinates detected",
                    new OverlapDetected
                    {
                        labelA = star.proper,
                        labelB = "INVALID COORDINATES",
                        distance = 0.5f
                    }
                );
                continue;
            }

            // Convert Right Ascension from hours to degrees
            double raDeg = star.ra * 15.0;
            double haDeg = AstronomyTime.HourAngleDeg(lst, raDeg);
            double haRad = AstronomyTime.DegToRad(haDeg);
            double decRad = AstronomyTime.DegToRad(star.dec);

            //Altitude Calc.
            //This determines how high the star is above the horizon.
            double sinAlt =
                Math.Sin(decRad) * Math.Sin(latitudeRad) +
                Math.Cos(decRad) * Math.Cos(latitudeRad) * Math.Cos(haRad);

            //Clamp value to [-1, 1] to prevent floating-point rounding errors
            //that could cause Math.Asin() to fail
            sinAlt = Math.Clamp(sinAlt, -1.0, 1.0);
            double altRad = Math.Asin(sinAlt);

            // Toggle is off, skip star labels at below the horizon.
            // Toggle is on, allow them to render.
            if (!showBelowHorizon && altRad <= HorizonEpsRad)
                continue;

            //Azimuth Calculation
            double sinHA = Math.Sin(haRad);
            double cosHA = Math.Cos(haRad);
            double tanDec = Math.Tan(decRad);

            //Compute azimuth relative to south using spherical trig
            double azSouth = Math.Atan2(
                sinHA,
                (cosHA * Math.Sin(latitudeRad)) - (tanDec * Math.Cos(latitudeRad))
            );

            //Convert from south-based azimuth to north-based azimuth
            double azRad = azSouth + Math.PI;

            //Normalize azimuth into range [0, 2pi)
            azRad %= (2.0 * Math.PI);
            if (azRad < 0) azRad += 2.0 * Math.PI;

            // We now convert (altitude, azimuth) into 3D coordinates 
            // x = r cos(alt) sin(az)
            // y = r sin(alt)
            // z = r cos(alt) cos(az)
            Vector3 starPosition = new Vector3(
                (float)(skyRadius * Math.Cos(altRad) * Math.Sin(azRad)),
                (float)(skyRadius * Math.Sin(altRad)),
                (float)(skyRadius * Math.Cos(altRad) * Math.Cos(azRad))
            );
            // Create a text label at the computed 3D position
            CreateLabel(star.proper, starPosition, star.mag);
        }
        //Log how many labels were successfully created
        Logging.Log(
            "StarLabel", "Star labels rendered",
            new OverlapDetected
            {
                labelA = "TOTAL",
                labelB = "LABELS",
                distance = activeLabels.Count
            }
        );
    }

    // Enable/disable all labels (Called by settings menu toggle).
    public void SetLabelsVisible(bool visible)
    {
        labelVisible = visible;

        if (labelParent == null)
            RenderLabels();

        labelParent.SetActive(visible);

        OnLabelsVisibilityChanged?.Invoke(visible);
    }

    private void CreateLabel(string starName, Vector3 starPosition, float magnitude)
    {
        Vector3 dir = starPosition.normalized;

        // Base position slightly outside sky dome
        Vector3 basePosition = dir * (skyRadius + 1.5f);

        // Base offset direction
        Vector3 perpendicular = Vector3.Cross(dir, Vector3.up).normalized;

        if (perpendicular == Vector3.zero)
            perpendicular = Vector3.Cross(dir, Vector3.right).normalized;

        float offsetAmount = 2.0f;

        // Initial position
        Vector3 labelPosition = basePosition + perpendicular * offsetAmount;

        float minDistance = 3.0f;
        int stackLevel = 0;
       

        foreach (var existing in placedLabels)
        {
            float distance = Vector3.Distance(labelPosition, existing.position);

            if (distance < minDistance)
            {
                // If this star is dimmer, stack it higher
                if (magnitude > existing.magnitude)
                {
                    stackLevel++;
                }
            }
        }
        if (stackLevel > 3)
        {
            Logging.Warning(
                "StarLabel", "Too much clutter",
                new OverlapDetected
                {
                    labelA = starName,
                    labelB = "STACKING",
                    distance = stackLevel
                }
            );
        }

        // Apply vertical stacking
        float verticalOffset = 1.5f;
        labelPosition += Vector3.up * (stackLevel * verticalOffset);

        if (starLabelPrefab == null)
        {
            Logging.Error(
                "StarLabel", "Star label prefab is not assigned"
                );
            return;
        }

        GameObject label = Instantiate(starLabelPrefab, labelPosition, Quaternion.identity, labelParent.transform);

        TextMesh textMesh = label.GetComponent<TextMesh>();
        if (textMesh == null)
        {
            Logging.Error(
                "StarLabel", "TextMesh missing",
                new OverlapDetected
                {
                    labelA = starName,
                    labelB = "ERROR ON TEXT MESH",
                    distance = 0f
                }
            );
            return;
        }
        textMesh.text = starName;

        float minScale = 0.6f;
        float maxScale = 1.4f;
        float normalizeMag = Mathf.InverseLerp(6f, -1f, magnitude);
        //make bigger text for brighter stars
        float scale = Mathf.Lerp(minScale, maxScale, normalizeMag);
        label.transform.localScale = Vector3.one * labelScale * scale;

        label.transform.LookAt(Camera.main.transform);
        label.transform.Rotate(0, 180f, 0);

        activeLabels.Add(label);
        placedLabels.Add((labelPosition, magnitude));
    }

    private void ClearLabels()
    {
        // Loop through all previously created labels
        foreach (var label in activeLabels)
        {
            // Destroy label GameObject if it still exists
            if (label != null)
                Destroy(label);
        }
        // Clear list so we can repopulate it cleanly
        activeLabels.Clear();
        placedLabels.Clear();
    }

    // Called by "Generate Stars under Horizon toggle."
    public void OnHorizonToggleChanged(bool value)
    {
        showBelowHorizon = value;
        ClearLabels();
        RenderLabels();

        // If star labels were hidden, make sure they stay hidden when toggling stars under the horizon.
        labelParent.SetActive(labelVisible);

    }

    public void QuitApplication()
    {
        Application.Quit();
    }
}