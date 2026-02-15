// Incorporating catalog, Astronomy computations/time conversions and skysession 
using System;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ParticleSystem))]
public class SkyMapRenderer : MonoBehaviour
{
    public HYGCatalogParser catalog;
    public float skyRadius = 100f;

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    // Small tolerance to avoid jitter right at the horizon
    private const double HorizonEpsRad = 1e-6;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();

        // Configure particle system for manual control
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = 119626;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = 0;
        main.startLifetime = Mathf.Infinity;

        RenderSky();
    }

    public void RenderSky()
    {
        if (SkySession.Instance == null)
        {
            Debug.LogError("SkySession missing from scene.");
            return;
        }
        if (catalog == null || catalog.VisibleStarsMag6 == null)
        {
            Debug.LogError("Catalogs not there.");
            return;
        }

        DateTimeOffset utc = AstronomyTime.LocalToUtc(SkySession.Instance.LocalDateTime);
        double jd = AstronomyTime.JulianDate(utc);
        double gmst = AstronomyTime.GreenwichMeanSiderealTimeDeg(jd);
        double lst = AstronomyTime.LocalSiderealTimeDeg(gmst, SkySession.Instance.LongitudeDeg);
        double latitudeRad = AstronomyTime.DegToRad(SkySession.Instance.LatitudeDeg);

        List<ParticleSystem.Particle> particleList = new List<ParticleSystem.Particle>();

        foreach (var star in catalog.VisibleStarsMag6)
        {
            if (float.IsNaN(star.ra) || float.IsNaN(star.dec) || float.IsNaN(star.mag))
                continue;

            // RA hours -> degrees
            double raDeg = star.ra * 15.0;
            double haDeg = AstronomyTime.HourAngleDeg(lst, raDeg);
            double haRad = AstronomyTime.DegToRad(haDeg);
            double decRad = AstronomyTime.DegToRad(star.dec);

            // --- Altitude ---
            double sinAlt =
                Math.Sin(decRad) * Math.Sin(latitudeRad) +
                Math.Cos(decRad) * Math.Cos(latitudeRad) * Math.Cos(haRad);

            // Clamp for numeric safety
            sinAlt = Math.Clamp(sinAlt, -1.0, 1.0);

            double altRad = Math.Asin(sinAlt);

            // Skip stars under horizon (with tiny epsilon)
            if (altRad <= HorizonEpsRad) continue;

            // This gives azimuth measured from SOUTH; convert to from NORTH by adding pi.
            double sinHA = Math.Sin(haRad);
            double cosHA = Math.Cos(haRad);

            // tan(dec) can blow up near +/-90°, but Polaris-like stars are fine, and this still behaves better than cos(lat) division.
            double tanDec = Math.Tan(decRad);

            double azSouth = Math.Atan2(
                sinHA,
                (cosHA * Math.Sin(latitudeRad)) - (tanDec * Math.Cos(latitudeRad))
            );

            double azRad = azSouth + Math.PI; // convert south-based to north-based

            // Normalize to [0, 2pi)
            azRad = azRad % (2.0 * Math.PI);
            if (azRad < 0) azRad += 2.0 * Math.PI;

            // Convert spherical (alt, az) to 3D position on dome
            Vector3 position = new Vector3(
                (float)(skyRadius * Math.Cos(altRad) * Math.Sin(azRad)), // X
                (float)(skyRadius * Math.Sin(altRad)),                  // Y
                (float)(skyRadius * Math.Cos(altRad) * Math.Cos(azRad))  // Z
            );

            ParticleSystem.Particle p = new ParticleSystem.Particle
            {
                position = position,
                remainingLifetime = Mathf.Infinity
            };

            // --- Improved magnitude mapping ---
            float t = Mathf.InverseLerp(0f, catalog.magnitudeLimit, star.mag);
            float curved = Mathf.Pow(t, 1.7f);

            float maxSize = 4.5f;
            float minSize = 0.5f;
            p.startSize = Mathf.Lerp(maxSize, minSize, curved);

            float alpha = Mathf.Lerp(1.0f, 0.15f, Mathf.Pow(t, 1.3f));
            p.startColor = new Color(1f, 1f, 1f, alpha);

            particleList.Add(p);
        }

        particles = particleList.ToArray();
        ps.SetParticles(particles, particles.Length);

        Debug.Log($"Rendered {particles.Length} stars using ParticleSystem.");
    }
}
