using UnityEngine;

public class ConstellationTest : MonoBehaviour
{
    void Start()
    {
        var catalog = ConstellationCatalog.LoadFromStreamingAssets("constellations_with_names.txt");

        Debug.Log("Total constellations: " + catalog.All.Count);

        foreach (var c in catalog.All)
        {
            Debug.Log($"Constellation: {c.Abbrev} ({c.Name})");
            Debug.Log($"Segments: {c.Segments.Count}");
        }
    }
}