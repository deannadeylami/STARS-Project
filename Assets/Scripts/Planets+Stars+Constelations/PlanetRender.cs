
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using UnityEngine;



public class PlanetRender : MonoBehaviour
{
    public GameObject planetLabelPrefab;

    [Header("Label Settings")]
     public float labelOffset = 1.5f;
    public float labelScale = 1f;
    public UnityEngine.Color labelColor = UnityEngine.Color.purple;
    public bool showBelowHorizon = false;
    private bool labelVisible = true;
    private List<GameObject> activeLabels = new List<GameObject>();
    private GameObject labelParent;
    public PlanetCSVReader planetLoader;
    public GameObject planetPrefab;
    public float skyRadius = 100f;
[Header("Planet Materials")]
public Material mercuryMat;
public Material venusMat;
public Material moonMat;
public Material marsMat;
public Material jupiterMat;
public Material saturnMat;
public Material uranusMat;
public Material neptuneMat;

UnityEngine.Vector3 sunDir = UnityEngine.Vector3.zero;
    private Dictionary<string, GameObject> spawnedPlanets =
        new Dictionary<string, GameObject>();


    public event Action<bool> OnLabelsVisibilityChanged;

    public void RenderPlanets()
    {
        ClearLabels();
        if (SkySession.Instance == null)
        {
            UnityEngine.Debug.LogError("SkySession missing from scene.");
            return;
        }

        if (planetLoader == null || planetLoader.planets == null)
        {
            UnityEngine.Debug.LogError("Planet loader missing.");
            return;
        }

        labelParent = new GameObject("PlanetLabels");
        labelParent.transform.parent = transform;

        DateTimeOffset utc = AstronomyTime.LocalToUtc(SkySession.Instance.LocalDateTime);

        double jd = AstronomyTime.JulianDate(utc);
        double gmst = AstronomyTime.GreenwichMeanSiderealTimeDeg(jd);
        double lst = AstronomyTime.LocalSiderealTimeDeg(gmst,SkySession.Instance.LongitudeDeg);

        double latitudeRad =
            AstronomyTime.DegToRad(SkySession.Instance.LatitudeDeg);

foreach (Planet planet in planetLoader.planets)
{
    if (planet.body != "Sun") continue;

    if (double.IsNaN(planet.raDeg) || double.IsNaN(planet.decDeg))
        continue;

    double haDeg = AstronomyTime.HourAngleDeg(lst, planet.raDeg);

    double haRad = AstronomyTime.DegToRad(haDeg);
    double decRad = AstronomyTime.DegToRad(planet.decDeg);

    double sinAlt =
        Math.Sin(decRad) * Math.Sin(latitudeRad) +
        Math.Cos(decRad) * Math.Cos(latitudeRad) * Math.Cos(haRad);

    double altRad = Math.Asin(sinAlt);

    double cosAz =
        (Math.Sin(decRad) - Math.Sin(altRad) * Math.Sin(latitudeRad)) /
        (Math.Cos(altRad) * Math.Cos(latitudeRad));

    cosAz = Math.Clamp(cosAz, -1.0, 1.0);

    double azRad = Math.Acos(cosAz);

    if (Math.Sin(haRad) > 0)
        azRad = 2 * Math.PI - azRad;

    UnityEngine.Vector3 position = new UnityEngine.Vector3(
        (float)(skyRadius * Math.Cos(altRad) * Math.Sin(azRad)),
        (float)(skyRadius * Math.Sin(altRad)),
        (float)(skyRadius * Math.Cos(altRad) * Math.Cos(azRad))
    );

    sunDir = position.normalized;
    UnityEngine.Debug.Log("Sun Direction: " + sunDir);
    break; // stop once found
}
        foreach (Planet planet in planetLoader.planets)
        {
            if(planet.body == "Sun")
                continue;
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
            if (!showBelowHorizon && altRad <= 0)
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
            Instantiate(planetPrefab, position, Quaternion.identity, transform);

        obj.name = planet.body;

        Material mat = getPlanetMaterial(planet.body);
        if (mat != null)
        {
            Renderer renderer = obj.GetComponentInChildren<Renderer>();
            renderer.material = mat;
            if(planet.body == "Moon" && renderer != null)
                {
                    if(sunDir != UnityEngine.Vector3.zero)
                    {
                        renderer.material.SetVector("_SunDir", sunDir);
                        UnityEngine.Debug.Log("Set Moon shader _SunDir to: " + sunDir);
                    }
                }
        }

        // === Magnitude scaling ===
        float size = Mathf.Lerp(0.8f, 0.2f, (float)(planet.magnitude / 6.0f));
        obj.transform.localScale = Vector3.one * size;

            spawnedPlanets[planet.body] = obj;

            // Magnitude scaling (same concept as stars)
            obj.transform.localScale = Vector3.one * size;
            UnityEngine.Debug.Log("Current Planet Body: " + planet.body);
            CreateLabel(planet.body, position);
            spawnedPlanets[planet.body] = obj;
        }

        UnityEngine.Debug.Log($"Rendered {spawnedPlanets.Count} planets.");
    }

    public void SetLabelsVisible(bool visible)
    {
        labelVisible = visible;

        if (labelParent == null)
            RenderPlanets();

        labelParent.SetActive(visible);
        OnLabelsVisibilityChanged?.Invoke(visible);
    }

private void CreateLabel(string planetName, UnityEngine.Vector3 planetPosition)
{
    if (planetLabelPrefab == null)
    {
        UnityEngine.Debug.LogError("Planet label prefab missing!");
        return;
    }

    // Move label slightly outward from sky dome
    UnityEngine.Vector3 labelPosition =
        planetPosition.normalized * (skyRadius + labelOffset);

    // Instantiate label
    GameObject label = Instantiate(
        planetLabelPrefab,
        labelPosition,
        UnityEngine.Quaternion.identity,
        labelParent.transform
    );

    TextMesh textMesh = label.GetComponent<TextMesh>();

        textMesh.text = planetName;
        textMesh.color = labelColor;
    label.transform.localScale = UnityEngine.Vector3.one * labelScale;

        label.transform.LookAt(Camera.main.transform);
        label.transform.Rotate(0, 180f, 0);

    activeLabels.Add(label);
}

public void OnHorizonToggleChanged(bool value)
{
    showBelowHorizon = value;
    RenderPlanets();
    
    // If planet labels were hidden, make sure they stay hidden when toggling planets under the horizon.
    labelParent.SetActive(labelVisible); 
}

private void ClearLabels()
{
    foreach (var label in activeLabels)
    {
        if (label != null)
            Destroy(label);
    }

    activeLabels.Clear();
}

Material getPlanetMaterial(string body)
    {
        switch(body)
        {
            case "Mercury":
                return mercuryMat;
            case "Venus":
                return venusMat;
            case "Moon":
                return moonMat;
            case "Mars":
                return marsMat;
            case "Jupiter":
                return jupiterMat;
            case "Saturn":
                return saturnMat;
            case "Uranus":
                return uranusMat;
            case "Neptune":
                return neptuneMat;
            default:
                return null;
        }
    }

}