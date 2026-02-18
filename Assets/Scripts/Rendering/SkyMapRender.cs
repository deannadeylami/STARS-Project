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
    private float[] baseAlpha;
    private float[] baseSize;
    private float[] twinkleOffsets;
    private float[] twinkleSpeeds;

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
        
        int n = particles.Length;
        baseAlpha = new float[n];
        baseSize = new float[n];
        twinkleOffsets = new float[n];
        twinkleSpeeds = new float[n];

        for (int i = 0; i < particles.Length; i++)
        {
            baseAlpha[i] = particles[i].startColor.a;
            baseSize[i] = particles[i].startSize;

            // Randomize twinkle for each star
            twinkleOffsets[i] = UnityEngine.Random.Range(0f, Mathf.PI * 2f); // random phase
            twinkleSpeeds[i] = UnityEngine.Random.Range(0.8f, 1.2f); // random speed
        }

        ps.SetParticles(particles, particles.Length);

        Debug.Log($"Rendered {particles.Length} stars using ParticleSystem.");
    }
    void Update()
    {
        if (particles == null) return;

        for (int i = 0; i < particles.Length; i++)
        {
            Color c = particles[i].startColor;

            if (baseSize[i] > 2.5f) // Bright stars - dramatic twinkle
            {
                float t = Time.time * twinkleSpeeds[i] + twinkleOffsets[i];
                
                float twinkle1 = Mathf.PerlinNoise(t * 0.8f, i * 0.01f);
                float twinkle2 = Mathf.Sin(t * 3f) * 0.5f + 0.5f;
                float combined = (twinkle1 * 0.6f + twinkle2 * 0.4f);
                
                combined = Mathf.Pow(combined, 1.5f);
                
                c.a = baseAlpha[i] * Mathf.Lerp(0.6f, 2.2f, combined); 
                
                particles[i].startSize = baseSize[i] * Mathf.Lerp(0.6f, 2.2f, combined); 
                
            }
            else if (baseSize[i] > 1.5f) // Medium stars - moderate twinkle
            {
                float t = Time.time * twinkleSpeeds[i] * 0.7f + twinkleOffsets[i];
                float twinkle = Mathf.PerlinNoise(t * 0.5f, i * 0.01f);
                
                // Smooth the transitions
                twinkle = Mathf.SmoothStep(0f, 1f, twinkle);
                
                c.a = baseAlpha[i] * Mathf.Lerp(0.7f, 1.4f, twinkle); 
                particles[i].startSize = baseSize[i] * Mathf.Lerp(0.85f, 1.25f, twinkle);
            }
            else // Small stars - very gentle, smooth flicker
            {
                float microTwinkle = Mathf.PerlinNoise(Time.time * 0.2f + twinkleOffsets[i], i * 0.05f);
                
                // Apply smoothing to prevent choppiness
                microTwinkle = Mathf.SmoothStep(0.2f, 0.8f, microTwinkle);
                
                c.a = baseAlpha[i] * Mathf.Lerp(0.85f, 1.1f, microTwinkle);
                particles[i].startSize = baseSize[i]; 
            }

            particles[i].startColor = c;
        }

        ps.SetParticles(particles, particles.Length);
    }
}

