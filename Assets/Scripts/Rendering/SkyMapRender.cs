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
    //Toggle for whether stars below the horizon should be rendered
    public bool showBelowHorizon = false; 
    public GameObject groundObject;
    [SerializeField] public bool gpuAccel;

    struct StarData
    {
        public UnityEngine.Vector3 position;
        public float size;
        public float alpha;
    }
    // GPU rendering Components
    public Mesh quadMesh;
    public Material starGPUMaterial;
    public ComputeBuffer starBuffer;
    private ComputeBuffer argsBuffer;
    private List<StarData> starDataList = new List<StarData>();
    
    void Start()
    {
        // Check if user turned on GPU acceleration.
        gpuAccel = GameSettings.GPUAccel;
        UnityEngine.Debug.Log("GPU Render is set to: " + gpuAccel);   
        ps = GetComponent<ParticleSystem>();

        // Configure particle system for manual control
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = 119626;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = 0;
        main.startLifetime = Mathf.Infinity;
        if(quadMesh == null)
        {
            quadMesh = CreateQuad();
        }

        RenderSky();
    }

    // Filtered by horizon — used for drawing constellation line segments.
    public Dictionary<int, UnityEngine.Vector3> StarPositions = new Dictionary<int, UnityEngine.Vector3>();

    // Always populated regardless of horizon toggle — used for constellation label centroid and line clipping.
    public Dictionary<int, UnityEngine.Vector3> AllStarPositions = new Dictionary<int, UnityEngine.Vector3>();

    public void RenderSky()
    {
        starDataList.Clear();
        StarPositions.Clear();
        AllStarPositions.Clear();

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
            UnityEngine.Vector3 position = new UnityEngine.Vector3(
                (float)(skyRadius * Math.Cos(altRad) * Math.Sin(azRad)), // X
                (float)(skyRadius * Math.Sin(altRad)),                   // Y
                (float)(skyRadius * Math.Cos(altRad) * Math.Cos(azRad))  // Z
            );

            // Always record full-sky position for constellation label centroid and line clipping (horizon-agnostic).
            if (star.hip > 0)
                AllStarPositions[star.hip] = position;

            // Toggle is off, skip stars below the horizon.
            // Toggle is on, allow them to render.
            if (!showBelowHorizon && altRad <= HorizonEpsRad)
                continue;

            float t = Mathf.InverseLerp(0f, catalog.magnitudeLimit, star.mag);
            float curved = Mathf.Pow(t, 1.7f);
            float maxSize;
            float minSize;
            if(!gpuAccel)
            {
                maxSize = 2.0f;
                minSize = 0.1f;                
            }
            else
            {
                // GPU can handle smaller stars better, so we can be more aggressive with size falloff
                maxSize = 0.5f;
                minSize = 0.2f;
            }
            StarData data = new StarData
            {
                position = position,
                size = Mathf.Lerp(maxSize, minSize, curved),
                alpha = Mathf.Lerp(1.0f, 0.15f, Mathf.Pow(t, 1.3f))
            };

            starDataList.Add(data);

            if (star.hip > 0)
            {
                StarPositions[star.hip] = position;
            }
        }

        if(!gpuAccel)
        {
            RenderStarsCPU();
            Debug.Log($"Rendered {particles.Length} stars using ParticleSystem.");
        }

        // Notify tracker that stars are done (covers both CPU and GPU paths)
        if (SkySceneReadyTracker.Instance != null)
            SkySceneReadyTracker.Instance.ReportReady("Stars");
        else
            Debug.LogWarning("[SkyMapRenderer] SkySceneReadyTracker not found — loading overlay won't dismiss.");
    }

    public void OnHorizonToggleChanged(bool value)
    {
        showBelowHorizon = value;
        RenderSky();
    }

    void RenderStarsCPU()
    {
        particles = new ParticleSystem.Particle[starDataList.Count];

        for (int i = 0; i < starDataList.Count; i++)
        {
            particles[i].position = starDataList[i].position;
            particles[i].startSize = starDataList[i].size;
            particles[i].startColor = new Color(1f, 1f, 1f, starDataList[i].alpha);
            particles[i].remainingLifetime = Mathf.Infinity;
        }
        ps.SetParticles(particles, particles.Length);
    }

    void RenderStarsGPU()
    {
        int count = starDataList.Count;

        if (count == 0)
            return;

        // Ensure buffer exists
        if (starBuffer == null || starBuffer.count != count)
        {
            starBuffer?.Release();
            starBuffer = new ComputeBuffer(count, sizeof(float) * 5);
        }

        starBuffer.SetData(starDataList);

        // CRITICAL: Set buffer BEFORE draw
        starGPUMaterial.SetBuffer("_StarBuffer", starBuffer);

        // Setup args buffer
        if (argsBuffer == null)
        {
            argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        uint[] args = new uint[5]
        {
            quadMesh.GetIndexCount(0),
            (uint)count,
            quadMesh.GetIndexStart(0),
            quadMesh.GetBaseVertex(0),
            0
        };

        argsBuffer.SetData(args);

        // Draw
        Graphics.DrawMeshInstancedIndirect(
            quadMesh,
            0,
            starGPUMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000f),
            argsBuffer
        );
    }

    Mesh CreateQuad()
    {
        Mesh mesh = new Mesh();

        mesh.vertices = new UnityEngine.Vector3[]
        {
            new UnityEngine.Vector3(-0.5f, -0.5f, 0),
            new UnityEngine.Vector3( 0.5f, -0.5f, 0),
            new UnityEngine.Vector3(-0.5f,  0.5f, 0),
            new UnityEngine.Vector3( 0.5f,  0.5f, 0),
        };

        mesh.uv = new UnityEngine.Vector2[]
        {
            new UnityEngine.Vector2(0,0),
            new UnityEngine.Vector2(1,0),
            new UnityEngine.Vector2(0,1),
            new UnityEngine.Vector2(1,1),
        };

        mesh.triangles = new int[]
        {
            0, 2, 1,
            2, 3, 1
        };

        return mesh;
    }

    void OnDestroy()
    {
        starBuffer?.Release();
        argsBuffer?.Release();
    }

    void Update()
    {
        if (gpuAccel)
        {
            RenderStarsGPU();
        }
    }

}