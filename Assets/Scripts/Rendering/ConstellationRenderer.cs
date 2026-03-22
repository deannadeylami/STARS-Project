using UnityEngine;

public class ConstellationRenderer : MonoBehaviour
{
    public string fileName = "constellations_with_names.txt";
    public Material lineMaterial;
    public float lineWidth = 0.05f;

    public SkyMapRenderer skyMap;
    void Start()
    {
        if (skyMap == null)
        {
            Debug.LogError("SkyMapRenderer not assigned!");
            return;
        }

        var catalog = ConstellationCatalog.LoadFromStreamingAssets(fileName);

        foreach (var constellation in catalog.All)
        {
            DrawConstellation(constellation);
        }
    }

    void DrawConstellation(ConstellationCatalog.Constellation c)
    {
        foreach (var seg in c.Segments)
        {
            if (!skyMap.StarPositions.TryGetValue(seg.hip1, out var pos1)) continue;
            if (!skyMap.StarPositions.TryGetValue(seg.hip2, out var pos2)) continue;

            GameObject lineObj = new GameObject($"{c.Abbrev}_line");
            lineObj.transform.parent = this.transform;

            var lr = lineObj.AddComponent<LineRenderer>();

            lr.positionCount = 2;
            lr.SetPosition(0, pos1);
            lr.SetPosition(1, pos2);

            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;

            lr.material = lineMaterial;
            lr.useWorldSpace = true;
        }
    }
}

