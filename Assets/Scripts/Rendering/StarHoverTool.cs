using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class StarHoverTooltip : MonoBehaviour
{
    [Header("References")]
    public SkyMapRenderer  skyRenderer;
    public HYGCatalogParser catalog;
    public Camera           mainCamera;

    [Header("Tooltip UI")]
    public GameObject  tooltipPanel;
    public TMP_Text tooltipName;
    public TMP_Text tooltipDetails;
    public RectTransform canvasRect;

    [Header("Detection — configurable")]
    [Tooltip("Degrees of angular radius around the ray to query the grid")]
    public float queryAngleDeg   = 2f;
    [Tooltip("Max screen-pixel distance to count as a hit")]
    public float screenPixelRadius = 12f;

    // --- Private state ---
    private ParticleSystem           ps;
    private ParticleSystem.Particle[] particles;
    private List<StarRecord>            orderedStars;   // same order as RenderSky()
    private StarSpatialGrid          grid;
    private int                      lastHitIndex = -1;

    void Start()
    {
        ps = skyRenderer.GetComponent<ParticleSystem>();
        tooltipPanel.SetActive(false);
    }

    // Call this after every RenderSky() call so the grid stays in sync
    public void Rebuild()
    {
        // 1. Snapshot particles
        particles = new ParticleSystem.Particle[ps.particleCount];
        ps.GetParticles(particles);

        // 2. Build ordered star list — same iteration order as SkyMapRenderer.RenderSky()
        orderedStars = new List<StarRecord>(skyRenderer.RenderedStars);

        // 3. Build spatial grid from particle positions (local space)
        var positions = new Vector3[particles.Length];
        for (int i = 0; i < particles.Length; i++)
            positions[i] = particles[i].position;

        grid = new StarSpatialGrid(positions, azDivisions: 36, altDivisions: 18);

        Debug.Log($"StarSpatialGrid built: {particles.Length} stars indexed.");
    }

    void Update()
    {
        if (grid == null) return;
        
        // Check if the mouse exists and the left button was clicked this frame
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            DetectHover();
        }
    }

    void DetectHover()
    {
        // --- Step 1: Ray from camera through mouse ---
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray worldRay = mainCamera.ScreenPointToRay(mousePos);
        // Convert to local space of the particle system transform
        Transform t = skyRenderer.transform;
        Ray localRay = new Ray(
            t.InverseTransformPoint(worldRay.origin),
            t.InverseTransformDirection(worldRay.direction).normalized
        );

        // --- Step 2: Grid query — only check stars in nearby cells ---
        List<int> candidates = grid.Query(localRay, queryAngleDeg);

        // --- Step 3: Find closest candidate within screen-pixel threshold ---
        int   bestIndex = -1;
        float bestDist  = screenPixelRadius;

        foreach (int i in candidates)
        {
            if (i >= particles.Length) continue;

            Vector3 worldPos  = t.TransformPoint(particles[i].position);
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0) continue;   // behind camera

            float screenDist = Vector2.Distance(mousePos, new Vector2(screenPos.x, screenPos.y));

            if (screenDist < bestDist)
            {
                bestDist  = screenDist;
                bestIndex = i;
            }
        }

        // --- Step 4: Show / hide tooltip ---
        if (bestIndex != lastHitIndex)
        {
            lastHitIndex = bestIndex;
            if (bestIndex >= 0)
                ShowTooltip(bestIndex);
            else
                tooltipPanel.SetActive(false);
        }

        if (bestIndex >= 0) MoveTooltip(mousePos);
    }

    void ShowTooltip(int index)
    {
        // index directly maps back to the original catalog entry
        StarRecord star = orderedStars[index];

        string name = !string.IsNullOrEmpty(star.proper) ? star.proper
                    : !string.IsNullOrEmpty(star.bf)     ? star.bf
                    : $"HIP {star.hip}";

        tooltipName.text    = name;
        tooltipDetails.text =
            $"Magnitude:   {star.mag:F2}\n"                               +
            $"RA / Dec:    {star.ra:F3}h  {star.dec:+0.0;-0.0}°\n"       +
            $"Spectral:    {(string.IsNullOrEmpty(star.spect) ? "—" : star.spect)}\n" +
            $"Distance:    {(star.dist > 0 ? $"{star.dist:F1} pc" : "—")}\n" +
            $"HIP:         {star.hip}";

        tooltipPanel.SetActive(true);
    }

    void MoveTooltip(Vector2 mousePos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, mousePos, null, out Vector2 local);

        RectTransform panel = tooltipPanel.GetComponent<RectTransform>();
        local += new Vector2(18f, -18f);

        float hw = panel.rect.width  * 0.5f;
        float hh = panel.rect.height * 0.5f;
        local.x = Mathf.Clamp(local.x, -canvasRect.rect.width  * 0.5f + hw,
                                         canvasRect.rect.width  * 0.5f - hw);
        local.y = Mathf.Clamp(local.y, -canvasRect.rect.height * 0.5f + hh,
                                         canvasRect.rect.height * 0.5f - hh);

        panel.localPosition = local;
    }
}