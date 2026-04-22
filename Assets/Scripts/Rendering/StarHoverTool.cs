using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class StarHoverTool : MonoBehaviour
{
    [Header("References")]
    public SkyMapRenderer   skyRenderer;
    public HYGCatalogParser catalog;
    public Camera           mainCamera;
    private Vector2 mouseDownPos;


    [Header("Tooltip UI")]
    public GameObject    tooltipPanel;
    public TMP_Text      tooltipName;
    public TMP_Text      tooltipDetails;
    public RectTransform canvasRect;
    

    [Header("Detection")]
    public float queryAngleDeg     = 2f;
    public float screenPixelRadius = 12f;

    private ParticleSystem            ps;
    private ParticleSystem.Particle[] particles;
    private List<StarRecord>          orderedStars;
    private StarSpatialGrid           grid;
    private int                       openPanelIndex = -1;

    void Start()
    {
        tooltipPanel.SetActive(false);
    }
    public void Rebuild()
    {
        Debug.Log("Rebuild started");
        
        if (ps == null)
        {
            Debug.Log("ps is null, getting component");
            ps = skyRenderer.GetComponent<ParticleSystem>();
        }

        Debug.Log($"Particle count: {ps.particleCount}");
        
        particles = new ParticleSystem.Particle[ps.particleCount];
        ps.GetParticles(particles);

        Debug.Log($"RenderedStars count: {skyRenderer.RenderedStars.Count}");
        
        orderedStars = new List<StarRecord>(skyRenderer.RenderedStars);

        var positions = new Vector3[particles.Length];
        for (int i = 0; i < particles.Length; i++)
            positions[i] = particles[i].position;

        grid = new StarSpatialGrid(positions, azDivisions: 36, altDivisions: 18);
        Debug.Log($"Rebuild complete — grid built with {particles.Length} stars");
    }

void Update()
{
    if (grid == null) return;

    if (Mouse.current != null)
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            mouseDownPos = Mouse.current.position.ReadValue();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            Vector2 mouseUpPos = Mouse.current.position.ReadValue();
            float dragDistance = Vector2.Distance(mouseDownPos, mouseUpPos);

            Debug.Log($"Drag distance: {dragDistance}");
            Debug.Log($"Particle count: {particles.Length}");
            Debug.Log($"OrderedStars count: {orderedStars.Count}");

            if (dragDistance < 40f)
            {
                int hitIndex = GetStarUnderMouse(mouseUpPos);
                Debug.Log($"Hit index: {hitIndex}");

                if (hitIndex >= 0)
                {
                    if (hitIndex == openPanelIndex && tooltipPanel.activeSelf)
                        ClosePanel();
                    else
                        OpenPanel(hitIndex, mouseUpPos);
                }
                else
                {
                    ClosePanel();
                }
            }
        }
    }
}

    int GetStarUnderMouse(Vector2 mousePos)
    {
        Ray worldRay = mainCamera.ScreenPointToRay(mousePos);
        Transform t  = skyRenderer.transform;
        Ray localRay = new Ray(
            t.InverseTransformPoint(worldRay.origin),
            t.InverseTransformDirection(worldRay.direction).normalized
        );

        List<int> candidates = grid.Query(localRay, queryAngleDeg);

        int   bestIndex = -1;
        float bestDist  = screenPixelRadius;

        foreach (int i in candidates)
        {
            if (i >= particles.Length) continue;

            Vector3 worldPos  = t.TransformPoint(particles[i].position);
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) continue;

            float screenDist = Vector2.Distance(mousePos,
                                   new Vector2(screenPos.x, screenPos.y));
            if (screenDist < bestDist)
            {
                bestDist  = screenDist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    void OpenPanel(int index, Vector2 mousePos)
    {
        openPanelIndex = index;
        StarRecord star = orderedStars[index];

        tooltipName.text    = GetStarName(star);
        tooltipDetails.text =
            $"RA:        {star.ra:F3}h\n"
          + $"Dec:       {star.dec:+0.0;-0.0}°\n"
          + $"Magnitude: {star.mag:F2}\n"
          + $"Spectral:  {(string.IsNullOrEmpty(star.spect) ? "—" : star.spect)}\n"
          + $"Distance:  {(star.dist > 0 ? $"{star.dist:F1} pc" : "—")}";

        tooltipPanel.SetActive(true);
        PositionPanel(tooltipPanel.GetComponent<RectTransform>(), mousePos);
    }

    void ClosePanel()
    {
        tooltipPanel.SetActive(false);
        openPanelIndex = -1;
    }

    string GetStarName(StarRecord star)
    {
        if (!string.IsNullOrEmpty(star.proper)) return star.proper;
        if (!string.IsNullOrEmpty(star.bf))     return star.bf;
        return $"HIP {star.hip}";
    }

    void PositionPanel(RectTransform panel, Vector2 mousePos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, mousePos, null, out Vector2 local);

        local += new Vector2(18f, -18f);

        float hw = panel.rect.width  * 0.5f;
        float hh = panel.rect.height * 0.5f;
        local.x = Mathf.Clamp(local.x,
            -canvasRect.rect.width  * 0.5f + hw,
             canvasRect.rect.width  * 0.5f - hw);
        local.y = Mathf.Clamp(local.y,
            -canvasRect.rect.height * 0.5f + hh,
             canvasRect.rect.height * 0.5f - hh);

        panel.localPosition = local;
    }
}