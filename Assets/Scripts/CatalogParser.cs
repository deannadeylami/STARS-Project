using UnityEngine;
using System.IO;
using System.IO.Compression;
using System.Globalization;

public class CatalogParser : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        string path = Path.Combine(
            Application.streamingAssetsPath,
            "ybsc5.gz"
        );

        Debug.Log("Catalog path: " + path);

        if (!File.Exists(path))
        {
            Debug.LogError("Catalog file not found!");
            return;
        }

        ParseCatalog(path);
    }

    void ParseCatalog(string path)
    {
        using (FileStream fs = File.OpenRead(path))
        using (GZipStream gzip = new GZipStream(fs, CompressionMode.Decompress))
        using (StreamReader reader = new StreamReader(gzip))
        {
            int parseCount = 0;
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();

                int HR = ParseInt(line, 0, 4); //Parsing Harvard Revised Number
                string Name = ParseString(line, 4, 10); //Name generally Bayer
                string SPType = ParseString(line, 127, 20); //Spectral type

                int RAh = ParseInt(line, 75, 2); //Hours RA, Equinox J2000, epoch 2000.0
                int RAm = ParseInt(line, 77, 2); //Minutes RA, Equinox J2000, epoch 2000.0
                float RAs = ParseFloat(line, 79, 4); //Seconds RA, equinox J2000, epoch 2000.0

                char DE = line[83]; //Sign Dec, Equinox J2000, epoch 2000.0
                int DEd = ParseInt(line, 84, 2); //Degrees Dec, equinox J2000, epoch 2000.0
                int DEm = ParseInt(line, 86, 2); //Minutes Dec, equinox J2000, epoch 2000.0
                int DEs = ParseInt(line, 88, 2); //Seconds Dec, equinox J2000, epoch 2000.0

                float Vmag = ParseFloat(line, 102, 5); //Visual magnitude
                float pmRA = ParseFloat(line, 148, 6); //Annual proper motion in RA J2000, FK5 system
                float pmDE = ParseFloat(line, 154, 6); //Annual proper motion in Dec J2000, FK5 system
                //all of the parsed information should be all we need but still needs to be filtered by magnitude and type some of these are not stars and that needs to get processed
                //reasoning behind only using J2000 values bc its better to use for the forward propagation to calculate for yr 2100
                if (parseCount < 9111)
                {
                    Debug.Log($"HarvRevised {HR} // Name {Name} // SPType {SPType} // Hours RA {RAh} " +
                        $"// Min RA {RAm} // Second RA {RAs} // Dec Sign {DE} // Dec Degree {DEd} // " +
                        $"Dec Min {DEm} // Dec Sec {DEs} // Visual Mag {Vmag} // RA J2000 annual prop {pmRA} //" +
                        $"Dec J2000 annual prop {pmDE} DONEEE  "
                    );
                    parseCount++;

                }
            }
        }
    }
    //Parsing helpers
    int ParseInt(string line, int start, int length)
    {
        string s = line.Substring(start, length).Trim();
        return int.TryParse(s, out int v) ? v : 0;
    }

    float ParseFloat(string line, int start, int length)
    {
        string s = line.Substring(start, length).Trim();
        return float.TryParse(
            s,
            NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out float v
        ) ? v : 0f;
    }

    string ParseString(string line, int start, int length)
    {
        return line.Substring(start, length).Trim();
    }
}

