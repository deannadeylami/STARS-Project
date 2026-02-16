using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class PlanetCsvLoader : MonoBehaviour
{
    public string csvFileName = "PlanetEphemerisData.csv";

    public List<Planet> planets = new List<Planet>();

    public void Start()
    {
        LoadPlanets();
    }

    void LoadPlanets()
    {
        string path = Path.Combine(Application.streamingAssetsPath, csvFileName);
        if(!File.Exists(path))
        {
            Debug.LogError($"CSV file not found at path: {path}");
            return;
        }

        string[] lines = File.ReadAllLines(path);
        for(int i = 1; i < lines.Length; i++) //Skips header
        {
            if(string.IsNullOrWhiteSpace(lines[i]))
                continue;
            
            Planet planet = ParsePlanetLine(lines[i]);
            if(planet != null)
            {
                planets.Add(planet);
                Debug.Log($"Loaded Planet: {planet}");
            }
        }
        Debug.Log($"Loaded {planets.Count} planets.");
    }
    Planet ParsePlanetLine(string line)
    {
        string[] p = line.Split(',');

        try
        {
            return new Planet
            {
                body = p[0],
                date = DateTime.Parse(p[1], CultureInfo.InvariantCulture),
                raDeg = double.Parse(p[2], CultureInfo.InvariantCulture),
                decDeg = double.Parse(p[3], CultureInfo.InvariantCulture),

                xArcsec = double.Parse(p[4], CultureInfo.InvariantCulture),
                yArcsec = double.Parse(p[5], CultureInfo.InvariantCulture),

                xRad = double.Parse(p[6], CultureInfo.InvariantCulture),
                yRad = double.Parse(p[7], CultureInfo.InvariantCulture),

                distanceAU = double.Parse(p[8], CultureInfo.InvariantCulture),
                magnitude = double.Parse(p[9], CultureInfo.InvariantCulture)
            };
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing line: {line}\nException: {e}");
            return null;
        }
    }
}