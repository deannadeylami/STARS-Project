
using System;
using System.Collections;
using System.Collections.Generic;using System.Globalization;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class CelestialEphemerisUnity : MonoBehaviour
{
    private static readonly HttpClient client = new HttpClient();

    private readonly Dictionary<string, string> celestialBodies = new Dictionary<string, string>
    {
        {"Mercury", "199"},
        {"Venus", "299"},
        {"Mars", "499"},
        {"Jupiter", "599"},
        {"Saturn", "699"},
        {"Uranus", "799"},
        {"Neptune", "899"},
    };

    private readonly Dictionary<string, string> Moon = new Dictionary<string, string>
    {
        {"Moon", "301"},
    };

        private double latitude;
        private double longitude;
        private double altitude = 0.0;
        private string simDate;

    private void Start()
    {
            if(SkySession.Instance == null)
            {
                Debug.LogError("SkySession instance not found. Ensure SkySession is initialized before CelestialEphemerisUnity.");
                return;
            }

            latitude = SkySession.Instance.LatitudeDeg;
            longitude = SkySession.Instance.LongitudeDeg;
            simDate = SkySession.Instance.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
            StartCoroutine(QueryHorizons());
    }

    private IEnumerator QueryHorizons()
    {
        DateTime startDate = DateTime.ParseExact(simDate, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        DateTime stopDate = startDate.AddDays(1);
        string center = $"{latitude},{longitude},{altitude}@399";

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("Body,Date,RA_deg (App),Dec_deg (App),X(Sat-Prim)_Arcsec,Y(Sat-Prim)_Arcsec,X(Sat-Prim)_Rad,Y(Sat-Prim)_Rad,Distance_AU,Mag");

        foreach (var body in celestialBodies)
        {
            Debug.Log($"Querying Horizons for {body.Key}...");
            string url = BuildHorizonsUrl(body.Value, center, startDate, stopDate, "1,2,3,5,6,9,20");
            Debug.Log("Url: " + url);
            yield return StartCoroutine(FetchAndParse(url, body.Key, csvBuilder));
        }

        foreach (var body in Moon)
        {
            Debug.Log($"Querying Horizons for {body.Key}...");
            string url = BuildHorizonsUrl(body.Value, center, startDate, stopDate, "1,2,3,9,20");
            yield return StartCoroutine(FetchAndParse(url, body.Key, csvBuilder, isMoon: true));
        }

        string csvOutput = csvBuilder.ToString();
        Debug.Log("\n=== CSV Ephemeris Output ===\n" + csvOutput);

        string streamingPath = System.IO.Path.Combine(Application.dataPath, "StreamingAssets");
        if (!System.IO.Directory.Exists(streamingPath))
        {
            System.IO.Directory.CreateDirectory(streamingPath); // make folder if it doesn't exist
        }
        string filePath = System.IO.Path.Combine(streamingPath, "PlanetEphemerisData.csv");
        System.IO.File.WriteAllText(filePath, csvOutput);
        Debug.Log($"\nSaved ephemeris to {filePath}");
        // === Trigger Load + Render AFTER CSV exists ===
        PlanetCSVReader loader = FindFirstObjectByType<PlanetCSVReader>();
        PlanetRender renderer = FindFirstObjectByType<PlanetRender>();

        if (loader != null && renderer != null)
        {
            loader.planets.Clear();
            loader.SendMessage("LoadPlanets", SendMessageOptions.DontRequireReceiver);
            renderer.RenderPlanets();
        }
        else
        {
            Debug.LogError("Planet system components not found.");
        }

    }

    private string BuildHorizonsUrl(string command, string center, DateTime startDate, DateTime stopDate, string quantities)
    {
        return $"https://ssd.jpl.nasa.gov/api/horizons.api?" +
               $"format=json&COMMAND='{command}'&CENTER='{center}'" +
               $"&EPHEM_TYPE=OBSERVER&START_TIME='{startDate:yyyy-MM-dd HH:mm}'" +
               $"&STOP_TIME='{stopDate:yyyy-MM-dd HH:mm}'&STEP_SIZE='1 d'" +
               $"&QUANTITIES='{quantities}'&ANG_FORMAT='DEG'&REF_SYSTEM='ICRF'&CSV_FORMAT='YES'";
    }

    private IEnumerator FetchAndParse(string url, string bodyName, StringBuilder csvBuilder, bool isMoon = false)
    {
        var task = client.GetStringAsync(url);
        while (!task.IsCompleted) yield return null;

        if (task.IsFaulted)
        {
            Debug.LogError($"Error querying {bodyName}: {task.Exception}");
            yield break;
        }

        string jsonString = task.Result;

        JObject json = JObject.Parse(jsonString);
        string result = (string)json["result"];
        if (string.IsNullOrEmpty(result))
        {
            Debug.LogError($"No ephemeris returned for {bodyName}");
            yield break;
        }

        bool insideTable = false;
        foreach (string line in result.Split('\n'))
        {
            if (line.Contains("$$SOE")) { insideTable = true; continue; }
            if (line.Contains("$$EOE")) { insideTable = false; break; }
            if (!insideTable || string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 5) continue;

            string dateString = parts[0].Trim();
            if (!DateTime.TryParse(dateString, out DateTime entryDate)) continue;

            DateTime startDate = DateTime.ParseExact(simDate, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            if (entryDate.Date != startDate.Date) continue;

            if (!isMoon)
            {
                double xNum = double.Parse(parts[11].Trim(), CultureInfo.InvariantCulture);
                double yNum = double.Parse(parts[12].Trim(), CultureInfo.InvariantCulture);
                double xRad = xNum * Math.PI / (180.0 * 3600.0);
                double yRad = yNum * Math.PI / (180.0 * 3600.0);

                string csvLine = string.Join(",",
                    bodyName,
                    parts[0].Trim(),
                    parts[5].Trim(),
                    parts[6].Trim(),
                    parts[11].Trim(),
                    parts[12].Trim(),
                    xRad.ToString(CultureInfo.InvariantCulture),
                    yRad.ToString(CultureInfo.InvariantCulture),
                    parts[16].Trim(),
                    parts[14].Trim()
                );
                csvBuilder.AppendLine(csvLine);
            }
            else
            {
                double raDeg = double.Parse(parts[3], CultureInfo.InvariantCulture);
                double decDeg = double.Parse(parts[4], CultureInfo.InvariantCulture);
                double ra0Deg = 0.0;
                double dec0Deg = 45.0;

                double ra = raDeg * Math.PI / 180.0;
                double dec = decDeg * Math.PI / 180.0;
                double ra0 = ra0Deg * Math.PI / 180.0;
                double dec0 = dec0Deg * Math.PI / 180.0;

                double dRA = ra - ra0;
                double sinDec = Math.Sin(dec);
                double cosDec = Math.Cos(dec);
                double sinDec0 = Math.Sin(dec0);
                double cosDec0 = Math.Cos(dec0);
                double D = sinDec0 * sinDec + cosDec0 * cosDec * Math.Cos(dRA);

                double xRad = (cosDec * Math.Sin(dRA)) / D;
                double yRad = (cosDec0 * sinDec - sinDec0 * cosDec * Math.Cos(dRA)) / D;
                double xArcsec = xRad * 206264.806;
                double yArcsec = yRad * 206264.806;

                string csvLine = string.Join(",",
                    bodyName,
                    parts[0].Trim(),
                    parts[3].Trim(),
                    parts[4].Trim(),
                    xArcsec.ToString("F3", CultureInfo.InvariantCulture),
                    yArcsec.ToString("F3", CultureInfo.InvariantCulture),
                    xRad.ToString(CultureInfo.InvariantCulture),
                    yRad.ToString(CultureInfo.InvariantCulture),
                    parts[11].Trim(),
                    parts[9].Trim()
                );
                csvBuilder.AppendLine(csvLine);
            }
        }
    }
}