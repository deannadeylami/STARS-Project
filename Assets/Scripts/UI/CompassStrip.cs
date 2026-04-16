// CompassStrip.cs
// Renders a scrolling compass strip across the top of the SkyScene UI.
// Shows cardinal/intercardinal labels (N, NE, E, SE, S, SW, W, NW) and a
// live degree readout of the current heading.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("SkyScene/Compass Strip")]
public class CompassStrip : MonoBehaviour
{
    // ── Inspector References ─────────────────────────────────────────────────

    [Header("Camera")]
    [Tooltip("Drag the GameObject that has CameraControllerNew attached.")]
    public CameraControllerNew cameraController;   // Source of the live heading value

    [Header("UI References")]
    [Tooltip("The RectTransform with the Mask component — the visible window.")]
    public RectTransform stripViewport;            // Masked panel that clips the scrolling labels

    [Tooltip("Empty child of stripViewport — this is what moves left/right each frame.")]
    public RectTransform stripContent;             // Shifted each frame to scroll the labels

    [Tooltip("TextMeshProUGUI showing the current heading, e.g. '47°'.")]
    public TextMeshProUGUI headingLabel;           // Centred degree readout beneath the strip

    [Header("Font")]
    [Tooltip("Drag any TMP font asset here — e.g. LiberationSans SDF from " +
             "Assets/TextMesh Pro/Resources/Fonts & Materials/.")]
    public TMP_FontAsset labelFont;                // Must be set explicitly — runtime won't find a fallback automatically

    // ── Strip Settings ───────────────────────────────────────────────────────

    [Header("Strip Settings")]
    [Tooltip("Pixels per degree of heading. Higher = labels spread further apart.")]
    public float pixelsPerDegree = 6f;             // Controls how fast the strip scrolls

    [Tooltip("Font size for major cardinals (N, E, S, W).")]
    public float majorFontSize = 20f;

    [Tooltip("Font size for intercardinals (NE, SE, SW, NW).")]
    public float minorFontSize = 15f;

    [Tooltip("Colour for N, E, S, W labels.")]
    public Color majorColor = new Color(1f, 0.92f, 0.7f, 1f);      // Warm amber-white — stands out against dark sky

    [Tooltip("Colour for NE, SE, SW, NW labels.")]
    public Color minorColor = new Color(0.8f, 0.85f, 1f, 0.85f);   // Cool blue-white — visually distinct from cardinals

    [Tooltip("Outline colour behind labels — makes them readable against any sky.")]
    public Color outlineColor = new Color(0f, 0f, 0f, 0.85f);      // Dark outline so labels read over stars

    [Tooltip("Outline thickness (0 = none, 0.2 = subtle, 0.5 = strong).")]
    [Range(0f, 0.5f)]
    public float outlineWidth = 0.25f;

    // ── Internals ────────────────────────────────────────────────────────────

    // The 8 compass points at 45° intervals that make up one full rotation of the strip
    private static readonly (float degrees, string label)[] k_Points =
    {
        (  0f, "N"),  ( 45f, "NE"), ( 90f, "E"),  (135f, "SE"),
        (180f, "S"),  (225f, "SW"), (270f, "W"),  (315f, "NW"),
    };

    // Three copies of the 360° label set are laid out: [-360, 0), [0, 360), [360, 720).
    // This ensures the strip is always fully populated regardless of the current heading,
    // avoiding any gaps when the heading wraps around 0°/360°.
    private const int k_Copies     = 3;
    private const int k_OriginCopy = 1;   // Index of the copy that sits at the 0–360° range

    private RectTransform[] _labelRects;  // Cached RectTransforms for all spawned labels
    private bool _ready = false;          // Guards Update() until Start() completes cleanly

    // ── Unity Lifecycle ──────────────────────────────────────────────────────

    void Start()
    {
        // Bail early if any required Inspector references are missing
        if (!ValidateReferences()) return;

        // Fall back to the default TMP font if none was assigned in the Inspector.
        // Note: this fallback only works in the editor — always assign labelFont explicitly
        // for runtime builds.
        if (labelFont == null)
        {
            labelFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (labelFont == null)
            {
                Debug.LogError("[CompassStrip] No font assigned and fallback could not be loaded. " +
                               "Assign a TMP_FontAsset to 'Label Font' in the Inspector.", this);
                return;
            }
        }

        // Force stripContent to be centre-anchored so anchoredPosition.x acts as a
        // clean pixel offset from the middle of the viewport. Without this, any
        // stretch-anchor set in the Inspector would break the scrolling math.
        stripContent.anchorMin        = new Vector2(0.5f, 0.5f);
        stripContent.anchorMax        = new Vector2(0.5f, 0.5f);
        stripContent.pivot            = new Vector2(0.5f, 0.5f);
        stripContent.anchoredPosition = Vector2.zero;
        stripContent.sizeDelta        = Vector2.zero;   // Size doesn't matter — Mask clips to viewport

        BuildLabels();
        _ready = true;
    }

    void Update()
    {
        // Skip until setup has completed and all references are confirmed valid
        if (!_ready || cameraController == null || stripContent == null) return;

        float heading = cameraController.HeadingDegrees;   // 0–360°, north = 0
        ScrollStrip(heading);
        UpdateHeadingLabel(heading);
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Instantiates all compass label GameObjects as children of stripContent.
    /// Each label is placed at a fixed pixel offset matching its degree position —
    /// only stripContent itself moves each frame, not the individual labels.
    /// </summary>
    private void BuildLabels()
    {
        int total   = k_Points.Length * k_Copies;
        _labelRects = new RectTransform[total];

        for (int i = 0; i < total; i++)
        {
            int copyIndex  = i / k_Points.Length;           // Which of the 3 copies (0, 1, 2)
            int pointIndex = i % k_Points.Length;           // Which of the 8 directions
            var (deg, label) = k_Points[pointIndex];

            // Shift each copy by ±360° so they span [-360, 720°) in total
            float absDeg = deg + (copyIndex - k_OriginCopy) * 360f;

            // Create a new child object for this label
            var go = new GameObject($"Compass_{label}_{copyIndex}", typeof(RectTransform));
            go.transform.SetParent(stripContent, false);

            // Anchor each label to the centre of stripContent so pixel offsets
            // are measured from the same origin point as stripContent itself
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(60f, 40f);
            rt.anchoredPosition = new Vector2(absDeg * pixelsPerDegree, 0f);   // Fixed position within content

            // Build the TMP text component
            var tmp           = go.AddComponent<TextMeshProUGUI>();
            tmp.font          = labelFont;              // Explicit font required for runtime builds
            tmp.text          = label;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;                  // Labels don't need to receive clicks

            // Cardinals (N/E/S/W) are larger and bolder to make them easy to scan quickly
            bool isMajor  = (label.Length == 1);
            tmp.fontSize  = isMajor ? majorFontSize : minorFontSize;
            tmp.color     = isMajor ? majorColor    : minorColor;
            tmp.fontStyle = isMajor ? FontStyles.Bold : FontStyles.Normal;

            // Outline ensures readability against any star/sky background
            tmp.outlineColor = outlineColor;
            tmp.outlineWidth = outlineWidth;

            _labelRects[i] = rt;
        }
    }

    /// <summary>
    /// Shifts stripContent horizontally so the label at the current heading
    /// is always centred in the viewport. Labels themselves never move —
    /// only their parent container does.
    /// </summary>
    private void ScrollStrip(float heading)
    {
        // A heading of 0° (North) means no offset — N label sits at x=0 (centre).
        // As heading increases, content shifts left, pulling later labels into view.
        stripContent.anchoredPosition = new Vector2(-heading * pixelsPerDegree, 0f);
    }

    /// <summary>
    /// Updates the heading degree readout below the strip, e.g. "47°".
    /// </summary>
    private void UpdateHeadingLabel(float heading)
    {
        if (headingLabel == null) return;
        headingLabel.text = $"{Mathf.RoundToInt(heading)}\u00b0";   // No zero-padding, plain 0–360
    }

    /// <summary>
    /// Checks that all required Inspector references are assigned.
    /// Logs a specific error for each missing field so problems are easy to locate.
    /// </summary>
    private bool ValidateReferences()
    {
        bool ok = true;
        if (cameraController == null) { Debug.LogError("[CompassStrip] 'Camera Controller' not assigned.", this); ok = false; }
        if (stripViewport    == null) { Debug.LogError("[CompassStrip] 'Strip Viewport' not assigned.", this);    ok = false; }
        if (stripContent     == null) { Debug.LogError("[CompassStrip] 'Strip Content' not assigned.", this);     ok = false; }
        if (headingLabel     == null)   Debug.LogWarning("[CompassStrip] 'Heading Label' not assigned — readout skipped.", this);
        return ok;
    }
}