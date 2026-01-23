using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

[System.Serializable]
public class StarRecord
{
    //Catalog identifiers
    public int id;        //Database primary key
    public int hip;       //Hipparcos ID
    public int hd;        //Henry Draper ID
    public int hr;        //Harvard Revised ID
    public string gl;     //Gliese catalog ID
    public string bf;     //Bayer/Flamsteed designation


    public string proper; //Common name

    //Equatorial coordinates (degrees, J2000)
    public float ra;      //Right ascension
    public float dec;     //Declination

    //Distance and photometry
    public float dist;    //Distance in parsecs
    public float mag;     //Apparent magnitude
    public string spect;  //Spectral type
    public float ci;      //Color index

    //Proper motion (milliarcseconds per year)
    public float pmra;
    public float pmdec;

    //Cartesian position (parsecs)
    public float x;
    public float y;
    public float z;

    //Cartesian velocity (parsecs per year)
    public float vx;
    public float vy;
    public float vz;

    //Radian-based values
    public float rarad;
    public float decrad;
    public float pmrarad;
    public float pmdecrad;
}

public class HYGCatalogParser : MonoBehaviour
{
    [Header("Catalog Settings")]
    public string catalogFileName = "hyg_v42.csv";

    [Header("Debug Verification")]
    public bool logSampleRows = true;

    public List<StarRecord> Stars { get; private set; }

    void Awake()
    {
        ParseCatalog();
    }

    private void ParseCatalog()
    {
        Stars = new List<StarRecord>();

        string path = Path.Combine(Application.streamingAssetsPath, catalogFileName);

        if (!File.Exists(path))
        {
            Debug.LogError("Catalog not found File not found: " + path);
            return;
        }

        using (StreamReader reader = new StreamReader(path))
        {
            string header = reader.ReadLine();
            Debug.Log("HYG CSV Header: " + header);

            int lineNumber = 0;

            while (!reader.EndOfStream)
            {
                lineNumber++;
                string line = reader.ReadLine();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] fields = line.Split(',');

                try
                {
                    StarRecord record = new StarRecord
                    {
                        id = ParseInt(fields[0]),
                        hip = ParseInt(fields[1]),
                        hd = ParseInt(fields[2]),
                        hr = ParseInt(fields[3]),
                        gl = fields[4],
                        bf = fields[5],
                        proper = fields[6],

                        ra = ParseFloat(fields[7]),
                        dec = ParseFloat(fields[8]),
                        dist = ParseFloat(fields[9]),
                        mag = ParseFloat(fields[10]),

                        spect = fields[13],
                        ci = ParseFloat(fields[16]),

                        pmra = ParseFloat(fields[17]),
                        pmdec = ParseFloat(fields[18]),

                        x = ParseFloat(fields[19]),
                        y = ParseFloat(fields[20]),
                        z = ParseFloat(fields[21]),

                        vx = ParseFloat(fields[22]),
                        vy = ParseFloat(fields[23]),
                        vz = ParseFloat(fields[24]),

                        rarad = ParseFloat(fields[25]),
                        decrad = ParseFloat(fields[26]),
                        pmrarad = ParseFloat(fields[27]),
                        pmdecrad = ParseFloat(fields[28])
                    };

                    Stars.Add(record);

                    if (logSampleRows && lineNumber <= 30)
                    {
                        LogStarRecord(record, lineNumber);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning(
                        "Hyg Catalog Parse error at line " +
                        lineNumber + ": " + ex.Message
                    );
                }
            }
        }

        Debug.Log("Hyg Catalog Parsed " + Stars.Count + " star records.");
    }

    private void LogStarRecord(StarRecord r, int row)
    {
        Debug.Log(
            "Hyg Catalog Row " + row + " " +
            "id " + r.id +
            " | hip " + r.hip +
            " | hd " + r.hd +
            " | hr " + r.hr +
            " | gl '" + r.gl + "'" +
            " | bf '" + r.bf + "'" +
            " | proper '" + r.proper + "'" +
            " | ra " + r.ra +
            " | dec " + r.dec +
            " | dist " + r.dist +
            " | mag " + r.mag +
            " | pmra " + r.pmra +
            " | pmdec " + r.pmdec
        );
    }

    //Safe parsing helpers

    private int ParseInt(string value)
    {
        int result;
        return int.TryParse(value, out result) ? result : -1;
    }

    private float ParseFloat(string value)
    {
        float result;
        return float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result
        ) ? result : float.NaN;
    }
}
