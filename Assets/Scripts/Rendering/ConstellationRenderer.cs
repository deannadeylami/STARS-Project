//Constellation Labels 

using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System;

public class ConstellationRenderer : MonoBehaviour
{
    public string fileName = "constellations_with_names.txt";
    public Material lineMaterial;
    public float lineWidth = 0.05f;
    private List<GameObject> labels = new List<GameObject> ();
    public float labelOffset = 2f;

    public float minScale = 0.3f;
    public float maxScale = 50f;
    
   // public Color labelColor = new Color(1f, 0.85f, 0.4f); 
    public SkyMapRenderer skyMap;

    public TMP_FontAsset fontAsset;

    public event Action<bool> OnConstellationsVisibilityChanged;

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
                sum += pos; count++; 
            } 
        } 
        if (count == 0)
            return;
        Vector3 center = sum / count; 
        Vector3 labelPos = center.normalized * (center.magnitude + labelOffset); 

        GameObject textObj = new GameObject($"{c.Abbrev}_Label");
        textObj.transform.parent = this.transform; 
        textObj.transform.position = labelPos; 

        var tmp = textObj.AddComponent<TextMeshPro>();
        tmp.color = new Color(1f, 0.85f, 0.4f); 
        tmp.font = fontAsset;
        tmp.enableVertexGradient = false;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color(0f, 0f, 0f, 0.8f);

        tmp.text = string.IsNullOrEmpty(c.Name) ? c.Abbrev : c.Name;
        
        tmp.fontSize = 10; 
       
        tmp.alignment = TextAlignmentOptions.Center; 
        labels.Add(textObj);
    }
    void Update()
    {
        if (Camera.main == null)
            return;
        Camera cam = Camera.main;

        foreach (var label in labels)
        {
            if (label == null) 
                continue;

            label.transform.forward = cam.transform.forward;
            float distance = Vector3.Distance(cam.transform.position, label.transform.position);

            float scale = Mathf.Pow(distance * 0.02f, 1.1f);
   
            scale = Mathf.Clamp(scale, minScale, maxScale);

            label.transform.localScale = Vector3.one * scale;
        }
    }

    public void SetConstellationsVisible(bool visible)
    {
        gameObject.SetActive(visible);
        OnConstellationsVisibilityChanged?.Invoke(visible);
    }
}