//Incorporating catalog, Astronomy computations/time conversions and skysession 
using System;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ParticleSystem))] //Ensures ParticleSystem component is attached to GameObject
public class SkyMapRenderer : MonoBehaviour
{
    public HYGCatalogParser catalog;
    public float skyRadius = 100f; //radius of our sky all the stars are placed here

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles; //Array that stores the generated star particles

    void Start()
    {
        ps = GetComponent<ParticleSystem>();//Get the particlesystem component attached to this GameObject

        //Configure particle system for manual control
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = 119626;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = 0; //eliminates star movement
        main.startLifetime = Mathf.Infinity; //maintains stars visibility

        RenderSky(); //generates the star field when scene starts
    }

    public void RenderSky()
    {
        //The 2 if statements below are to ensure that he skySession is being read and that the Catalog is also being read
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

        //Incorporationg of the AstronomyTime script and sky session
        DateTimeOffset utc = AstronomyTime.LocalToUtc(SkySession.Instance.LocalDateTime);
        double jd = AstronomyTime.JulianDate(utc);
        double gmst = AstronomyTime.GreenwichMeanSiderealTimeDeg(jd);
        double lst = AstronomyTime.LocalSiderealTimeDeg(gmst, SkySession.Instance.LongitudeDeg);
        double latitudeRad = AstronomyTime.DegToRad(SkySession.Instance.LatitudeDeg);

        //Temporary list to store generated star particles
        List<ParticleSystem.Particle> particleList = new List<ParticleSystem.Particle>();

        //loop through each visible star with a mag <= 6.0 in the HYG catalog
        foreach (var star in catalog.VisibleStarsMag6)
        {
            //Skip stars with invalid Right Ascension or Declination
            if (float.IsNaN(star.ra) || float.IsNaN(star.dec))
                continue;

            //Convert RA from hours to degrees (1 hour = 15 degrees)
            double raDeg = star.ra * 15.0;
            double haDeg = AstronomyTime.HourAngleDeg(lst, raDeg);
            double haRad = AstronomyTime.DegToRad(haDeg);
            double decRad = AstronomyTime.DegToRad(star.dec);

            //using spherical astronomy formula compute sine of altitude
            double sinAlt =
                Math.Sin(decRad) * Math.Sin(latitudeRad) +
                Math.Cos(decRad) * Math.Cos(latitudeRad) * Math.Cos(haRad);

            //altitude angle above the horizon
            double altRad = Math.Asin(sinAlt);
            //skips stars under the horizon with an altitude <=0
            if (altRad <= 0) continue;

            double cosAz =
                (Math.Sin(decRad) - Math.Sin(altRad) * Math.Sin(latitudeRad)) /
                (Math.Cos(altRad) * Math.Cos(latitudeRad));

            //Clamp value to valid range to prevent floating point error
            cosAz = Math.Clamp(cosAz, -1.0, 1.0);
            double azRad = Math.Acos(cosAz);

            if (Math.Sin(haRad) > 0)
                azRad = 2 * Math.PI - azRad;

            //convert spherical coordinates (alt, az) 
            Vector3 position = new Vector3(
                (float)(skyRadius * Math.Cos(altRad) * Math.Sin(azRad)),//X
                (float)(skyRadius * Math.Sin(altRad)),//Y
                (float)(skyRadius * Math.Cos(altRad) * Math.Cos(azRad))//Z
            );

            //creates new particle for star
            ParticleSystem.Particle p = new ParticleSystem.Particle();
            
            //sets the calculated position
            p.position = position;

            //setting star size based on magnitude (the brighter they are the bigger the size)
            p.startSize = Mathf.Lerp(0.8f, 0.1f, star.mag / catalog.magnitudeLimit);
            p.startColor = Color.white;

            //wont expire
            p.remainingLifetime = Mathf.Infinity; 

            //add star particle to list
            particleList.Add(p);
        }

        //convert list to array
        particles = particleList.ToArray();

        //send particle data to unity
        ps.SetParticles(particles, particles.Length);

        //shows how many stars were rendered
        Debug.Log($"Rendered {particles.Length} stars using ParticleSystem.");
    }
}