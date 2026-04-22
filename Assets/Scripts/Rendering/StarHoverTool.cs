using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

// Handles clicking stars and showing tooltip info
public class StarHoverTool : MonoBehaviour
{
    [Header("References")]
    public SkyMapRenderer   skyRenderer;
    public HYGCatalogParser catalog;
    public Camera           mainCamera;
    private Vector2 mouseDownPos;
    public GameObject SettingsPanel;


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

    // Rebuilds particle + grid data (call after rendering stars)
    public void Rebuild()
    {        
        if (ps == null)
        {
            ps = skyRenderer.GetComponent<ParticleSystem>();
        }

        Vector3[] positions;

        if (skyRenderer.gpuAccel) 
        {
            // If GPU mode is on, we don't have particles, we use the renderer's list
            // We'll need to add a public way to get these positions from SkyMapRenderer
            positions = skyRenderer.GetStarPositions(); 
        }
        else
        {
            // grab particles
            particles = new ParticleSystem.Particle[ps.particleCount];
            ps.GetParticles(particles);

            // build a grid from positions
            positions = new Vector3[particles.Length];
            for (int i = 0; i < particles.Length; i++)
                positions[i] = particles[i].position;

        }
        
        // copy star data
        orderedStars = new List<StarRecord>(skyRenderer.RenderedStars);

        grid = new StarSpatialGrid(positions, azDivisions: 36, altDivisions: 18);
   }

void Update()
{
    // prevent tooltip popups while in settings menu.
    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
    {
        return;
    }

    if (grid == null) return;

    if (Mouse.current != null)
    {
        // record where click was
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            mouseDownPos = Mouse.current.position.ReadValue();
        }

        // make sure it was a click, not a drag.
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            Vector2 mouseUpPos = Mouse.current.position.ReadValue();
            float dragDistance = Vector2.Distance(mouseDownPos, mouseUpPos);

            if (dragDistance < 40f)
            {
                int hitIndex = GetStarUnderMouse(mouseUpPos);

                if (hitIndex >= 0)
                {
                    // toggle tooltip if 
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
    // Finds closest star to mouse
    int GetStarUnderMouse(Vector2 mousePos)
    {
        // convert ray into local space
        Ray worldRay = mainCamera.ScreenPointToRay(mousePos);
        Transform t  = skyRenderer.transform;
        Ray localRay = new Ray(
            t.InverseTransformPoint(worldRay.origin),
            t.InverseTransformDirection(worldRay.direction).normalized
        );
        
        // get nearby stars from grid
        List<int> candidates = grid.Query(localRay, queryAngleDeg);

        int   bestIndex = -1;
        float bestDist  = screenPixelRadius;
        
        // find closest one on screen
        foreach (int i in candidates)
        {
            // Get the position from our grid/list instead of the particle system
            Vector3 localPos = grid.GetPosition(i); 
            Vector3 worldPos = t.TransformPoint(localPos);
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            
            if (screenPos.z < 0) continue;

            float screenDist = Vector2.Distance(mousePos, new Vector2(screenPos.x, screenPos.y));
            if (screenDist < bestDist)
            {
                bestDist = screenDist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    // Opens tooltip for a star
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

    public void ClosePanel()
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

    // Keeps tooltip near mouse and inside screen
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