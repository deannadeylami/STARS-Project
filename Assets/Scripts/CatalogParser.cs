using UnityEngine;
using System.IO;
using System.IO.Compression;

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
            int count = 0;
            while (!reader.EndOfStream && count < 5)
            {
                Debug.Log(reader.ReadLine());
                count++;
            }
        }
    }
}
