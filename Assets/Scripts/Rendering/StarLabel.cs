//StarLabel renders text labels for visible stars on the sky map by their "proper" given name from the HYG catalog
//Converts celestial coordinates (Ra/Dec) into 3D world positions using astronomy calculations based on the observers location and time.

using System;
using System.Collections.Generic;
using UnityEngine;

public class StarLabel : MonoBehaviour
{
    public HYGCatalogParser catalog;
    public float skyRadius = 100f;
    public GameObject starLabelPrefab;

    [Header("Label Settings")]
    public float labelScale = 1f;   // Constant size for all labels

    private const double HorizonEpsRad = 1e-6; //small epsilon to prevent floating pt errors at the horizon 
    private List<GameObject> activeLabels = new List<GameObject>(); //keeps track of all currently rendered labels

    void Start()
    {
        RenderLabels();
    }

    public void RenderLabels()
    {
        //Bottom two if statements check skysession and catalog making sure its there
        if (SkySession.Instance == null)
        {
            Debug.LogError("SkySession missing.");
            return;
        }

        if (catalog == null || catalog.VisibleStarsMag6 == null)
        {
            Debug.LogError("Catalog missing.");
            return;
        }

        ClearLabels(); //Remove previously created labels before re-rendering

        //time conversions
        DateTimeOffset utc = AstronomyTime.LocalToUtc(SkySession.Instance.LocalDateTime);
        double jd = AstronomyTime.JulianDate(utc);
        double gmst = AstronomyTime.GreenwichMeanSiderealTimeDeg(jd);

        //compute local sidereal time using observer longitude
        double lst = AstronomyTime.LocalSiderealTimeDeg(gmst, SkySession.Instance.LongitudeDeg);

        //Convert observer latitude from degrees to radians
        double latitudeRad = AstronomyTime.DegToRad(SkySession.Instance.LatitudeDeg);

        //loop through all the stars with a mag <= 6
        foreach (var star in catalog.VisibleStarsMag6)
        {
            //No label get skipped
            if (string.IsNullOrWhiteSpace(star.proper))
                continue;

            //Invalid coordinate data get skipped
            if (float.IsNaN(star.ra) || float.IsNaN(star.dec))
                continue;

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

            //If altitude is <= 0 (at or below horizon), skip this star
            if (altRad <= HorizonEpsRad)
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
            CreateLabel(star.proper, starPosition);
        }
        //Log how many labels were successfully created
        Debug.Log($"Rendered {activeLabels.Count} star labels.");
    }
    private void CreateLabel(string starName, Vector3 starPosition)
    {
        // Move label slightly outward from the sky dome
        // This prevents z-fighting with the star particles
        Vector3 labelPosition = starPosition.normalized * (skyRadius + 1.5f);

        // Instantiate the label prefab at computed position
        // Quaternion.identity = no rotation
        // transform = make this script's GameObject the parent
        GameObject label = Instantiate(starLabelPrefab, labelPosition, Quaternion.identity, transform);

        // Get the TextMesh component from the prefab
        TextMesh textMesh = label.GetComponent<TextMesh>();

        // Set the displayed text to the star's proper name
        textMesh.text = starName;

        // Apply constant uniform scale to all labels
        label.transform.localScale = Vector3.one * labelScale;

        // Store reference so we can delete it later when re-rendering
        activeLabels.Add(label);
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
    }
}