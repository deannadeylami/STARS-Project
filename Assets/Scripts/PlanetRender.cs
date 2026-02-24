using System;
using System.Collections.Generic;
using UnityEngine;

public class PlanetRender : MonoBehaviour
{
    public PlanetCsvLoader planetLoader;
    public GameObject planetPrefab;
    public float skyRadius = 100f;

    private Dictionary<string, GameObject> spawnedPlanets =
        new Dictionary<string, GameObject>();


    public void RenderPlanets()
    {
        if (SkySession.Instance == null)
        {
            Debug.LogError("SkySession missing from scene.");
            return;
        }

        if (planetLoader == null || planetLoader.planets == null)
        {
            Debug.LogError("Planet loader missing.");
            return;
        }

        // === SAME TIME PIPELINE AS SKYMAPRENDERER ===
        DateTimeOffset utc =
            AstronomyTime.LocalToUtc(SkySession.Instance.LocalDateTime);

        double jd = AstronomyTime.JulianDate(utc);
        double gmst = AstronomyTime.GreenwichMeanSiderealTimeDeg(jd);
        double lst = AstronomyTime.LocalSiderealTimeDeg(
            gmst,
            SkySession.Instance.LongitudeDeg
        );

        double latitudeRad =
            AstronomyTime.DegToRad(SkySession.Instance.LatitudeDeg);

        foreach (Planet planet in planetLoader.planets)
        {
            if (double.IsNaN(planet.raDeg) || double.IsNaN(planet.decDeg))
                continue;

            double haDeg =
                AstronomyTime.HourAngleDeg(lst, planet.raDeg);

            double haRad = AstronomyTime.DegToRad(haDeg);
            double decRad = AstronomyTime.DegToRad(planet.decDeg);

            double sinAlt =
                Math.Sin(decRad) * Math.Sin(latitudeRad) +
                Math.Cos(decRad) * Math.Cos(latitudeRad) * Math.Cos(haRad);

            double altRad = Math.Asin(sinAlt);

            // === MATCH SKYMAP: skip below horizon ===
            if (altRad <= 0)
                continue;
            double cosAz =
                (Math.Sin(decRad) - Math.Sin(altRad) * Math.Sin(latitudeRad)) /
                (Math.Cos(altRad) * Math.Cos(latitudeRad));

            cosAz = Math.Clamp(cosAz, -1.0, 1.0);

            double azRad = Math.Acos(cosAz);

            if (Math.Sin(haRad) > 0)
                azRad = 2 * Math.PI - azRad;

            Vector3 position = new Vector3(
                (float)(skyRadius * Math.Cos(altRad) * Math.Sin(azRad)),
                (float)(skyRadius * Math.Sin(altRad)),
                (float)(skyRadius * Math.Cos(altRad) * Math.Cos(azRad))
            );

            GameObject obj =
                Instantiate(planetPrefab, position, Quaternion.identity);

            obj.name = planet.body;

            // Magnitude scaling (same concept as stars)
            float size = Mathf.Lerp(
                0.8f,
                0.2f,
                (float)(planet.magnitude / 6.0f)
            );

            obj.transform.localScale = Vector3.one * size;

            spawnedPlanets[planet.body] = obj;
        }

        Debug.Log($"Rendered {spawnedPlanets.Count} planets.");
    }
}
