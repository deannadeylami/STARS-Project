using UnityEngine;

public class ConstellationRenderer : MonoBehaviour
{
    public string fileName = "constellations_with_names.txt";
    public Material lineMaterial;
    public float lineWidth = 0.05f;
    public float labelOffset = 2f;
    public float labelSize = 2f;
    public Color labelColor = Color.blue; 
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
        foreach (var constellation in catalog.All)
        {
            DrawConstellation(constellation);
            CreateLabel(constellation);
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
    void CreateLabel(ConstellationCatalog.Constellation c)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (int hip in c.UniqueHipIds)
        {
            if (skyMap.StarPositions.TryGetValue(hip, out var pos))
            {
                sum += pos;
                count++;
            }
        }

        if (count == 0) return;

        Vector3 center = sum / count;

        // Push label slightly outward so it doesn't overlap lines
        Vector3 labelPos = center.normalized * (center.magnitude + labelOffset);

        GameObject textObj = new GameObject($"{c.Abbrev}_Label");
        textObj.transform.parent = this.transform;
        textObj.transform.position = labelPos;

        var textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = string.IsNullOrEmpty(c.Name) ? c.Abbrev : c.Name;
        textMesh.characterSize = labelSize;
        textMesh.color = labelColor;
        textMesh.anchor = TextAnchor.MiddleCenter;
    }

}

