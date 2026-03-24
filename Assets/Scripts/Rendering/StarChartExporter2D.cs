// =============================================================================
// StarChartExporter2D.cs
// =============================================================================
// PURPOSE:
//   Exports the current sky session as a 2D azimuthal equidistant star chart
//   rendered entirely in software (CPU pixel rasterisation) and saved as a
//   JPEG image. No Unity camera, RenderTexture, or GPU readback is used.
//
// NOTES:
//   - North is rendered at the TOP directly by projection math.
//   - The pixel buffer is written in Unity's bottom-origin row layout from the
//     start, so no final image flip is needed.
//   - Uses ConstellationCatalog instead of private constellation parsing.
//   - Prevents overlapping exports with _isExporting.
//   - Includes improved label placement and symbol avoidance.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using CosineKitty;

public class StarChartExporter2D : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("The HYGCatalogParser that owns the star lists.")]
    public HYGCatalogParser catalog;

    [Header("Export Resolution")]
    public int width = 2048;
    public int height = 2048;
    [Range(1, 100)]
    public int jpegQuality = 92;

    [Header("Chart Style")]
    public bool drawHorizonCircle = true;
    [Range(1, 6)]
    public int horizonThicknessPx = 2;
    public Color32 horizonColor = new Color32(80, 80, 80, 255);

    [Header("Background Gradient")]
    [Tooltip("Draw a radial colour gradient instead of flat black.")]
    public bool enableRadialBackground = true;
    [Tooltip("Colour at the zenith (chart centre).")]
    public Color32 backgroundZenithColor = new Color32(8, 8, 22, 255);
    [Tooltip("Colour at the horizon (chart edge).")]
    public Color32 backgroundHorizonColor = new Color32(2, 2, 8, 255);

    [Header("Altitude Rings")]
    [Tooltip("Draw concentric rings at the altitude angles listed below.")]
    public bool enableAltitudeRings = true;
    [Tooltip("Altitude values (°) at which to draw rings. Default 30° and 60°.")]
    public float[] altitudeRingDegrees = { 30f, 60f };
    public Color32 altitudeRingColor = new Color32(35, 35, 55, 255);
    [Range(1, 3)]
    public int altitudeRingThicknessPx = 1;
    [Tooltip("Render a small degree label beside each altitude ring.")]
    public bool enableAltitudeRingLabels = true;
    [Range(1, 3)]
    public int altitudeRingLabelFontScale = 1;
    public Color32 altitudeRingLabelColor = new Color32(70, 70, 100, 255);

    [Header("Cardinal Directions")]
    [Tooltip("Draw N / E / S / W labels (and optionally NE/SE/SW/NW) around the horizon.")]
    public bool enableCardinalLabels = true;
    [Tooltip("Also show the four intercardinal labels NE, SE, SW, NW.")]
    public bool enableIntercardinalLabels = true;
    [Range(1, 4)]
    public int cardinalLabelFontScale = 2;
    public Color32 cardinalLabelColor = new Color32(160, 160, 160, 255);
    [Range(4f, 40f)]
    [Tooltip("Distance inward from the horizon circle edge to centre each label (px).")]
    public float cardinalLabelInsetPx = 18f;
    [Tooltip("Draw a short radial tick mark at each cardinal/intercardinal direction.")]
    public bool enableCardinalTicks = true;
    [Range(2, 12)]
    public int cardinalTickLengthPx = 6;
    [Range(1, 3)]
    public int cardinalTickThicknessPx = 1;
    public Color32 cardinalTickColor = new Color32(120, 120, 120, 255);

    [Header("Stars: magnitude -> size/brightness")]
    public float maxStarRadiusPx = 4.5f;
    public float minStarRadiusPx = 0.7f;
    public float minAlpha = 0.15f;
    public float maxAlpha = 1.00f;

    [Header("Star Color (B-V Spectral Tint)")]
    [Tooltip("Tint stars by B-V spectral index. Stars with no data default to white.")]
    public bool enableStarColors = true;
    [Range(0f, 1f)]
    [Tooltip("0 = white only | 1 = full spectral colour.")]
    public float starColorStrength = 0.85f;

    [Header("Bright Star Glow")]
    [Tooltip("Draw a soft halo behind stars brighter than GlowMagnitudeThreshold.")]
    public bool enableBrightStarGlow = true;
    public float glowMagnitudeThreshold = 1.5f;
    [Range(1.5f, 5f)]
    public float glowRadiusMultiplier = 2.5f;
    [Range(0.05f, 0.5f)]
    public float glowAlphaMultiplier = 0.22f;

    [Header("Star Labels")]
    public bool enableStarLabels = true;
    public float maxStarLabelMagnitude = 2.5f;
    [Range(1, 4)]
    public int starLabelFontScale = 1;
    public Color32 starLabelColor = new Color32(200, 200, 200, 255);

    [Header("Solar System (Astronomy Engine - offline)")]
    public bool enablePlanets = true;
    public bool enableSun = true;
    public bool enableMoon = true;
    public bool enablePlanetLabels = true;
    public bool enableSunLabel = true;
    public bool enableMoonLabel = true;
    [Tooltip("Apply atmospheric refraction to altitude values.")]
    public bool refraction = true;

    [Header("Planet Style")]
    [Range(1f, 10f)] public float planetDotRadiusPx = 2.5f;
    [Range(2f, 16f)] public float planetRingRadiusPx = 5.5f;
    [Range(1, 4)] public int planetRingThicknessPx = 1;
    public Color32 planetColor = new Color32(220, 210, 160, 255);
    [Tooltip("Skip planets dimmer than this magnitude. Set 99 to draw all.")]
    public float planetMagnitudeLimit = 99f;

    [Header("Sun Style")]
    [Range(2f, 16f)] public float sunRadiusPx = 7f;
    public Color32 sunColor = new Color32(255, 220, 120, 255);

    [Header("Moon Style (with phase)")]
    [Range(2f, 20f)] public float moonRadiusPx = 8f;
    public Color32 moonLitColor = new Color32(220, 220, 220, 255);
    public Color32 moonDarkColor = new Color32(70, 70, 70, 255);

    [Header("Labels: Planets/Sun/Moon")]
    [Range(1, 4)] public int solarLabelFontScale = 2;
    public Color32 solarLabelColor = new Color32(220, 210, 160, 255);
    [Range(0f, 12f)]
    [Tooltip("Extra pixel padding around symbols when reserving occupancy grid space.")]
    public float symbolCollisionPaddingPx = 2f;

    [Header("Constellations")]
    public bool enableConstellationLines = true;
    public bool enableConstellationLabels = true;
    [Tooltip("Filename inside Assets/StreamingAssets.")]
    public string constellationsFileName = "constellations_with_names.txt";
    public Color32 constellationLineColor = new Color32(80, 120, 200, 255);
    [Range(1, 4)]
    public int constellationLineThicknessPx = 1;
    [Tooltip("Skip a segment if either endpoint star is dimmer than this magnitude.")]
    public float constellationLineMagLimit = 6.0f;

    [Header("Constellation Label Style")]
    [Range(1, 4)] public int constellationLabelFontScale = 2;
    public Color32 constellationLabelColor = new Color32(140, 170, 220, 255);

    [Header("Label Collision")]
    [Tooltip("Use a pixel occupancy grid to prevent text labels overlapping.")]
    public bool basicLabelCollisionAvoidance = true;

    [Header("Advanced Label Placement")]
    [Range(1, 6)] public int labelPlacementRings = 3;
    [Range(2, 20)] public int labelPlacementStepPx = 6;
    [Range(0f, 16f)] public float extraConstellationLabelClearancePx = 4f;
    [Range(0f, 16f)] public float extraStarLabelClearancePx = 3f;
    [Range(0f, 16f)] public float extraSolarLabelClearancePx = 6f;
    public bool reserveNamedStarSymbolArea = true;
    [Range(0f, 12f)] public float namedStarSymbolPaddingPx = 2f;

    [Header("Circle Mask")]
    [Tooltip("Prevent constellation line pixels from landing outside the horizon disc.")]
    public bool maskLinesToChartCircle = true;

    [Header("Output")]
    public string folderName = "StarMapExports";

    public event Action<string> OnExportComplete;
    public event Action<string> OnExportFailed;

    private struct LabelCandidate
    {
        public int x, y;
        public float mag;
        public string name;
        public float avoidRadiusPx;
    }

    private struct PrecomputedBody
    {
        public string name;
        public bool aboveHorizon;
        public int px, py;
        public float mag;
        public bool isSun, isMoon;
        public float moonPhaseFraction;
        public bool moonWaxing;
    }

    private struct RasterStats
    {
        public int starsDrawn;
        public int conLinesDrawn;
        public int conLabelsDrawn;
        public int solarLabelsDrawn;
        public int starLabelsDrawn;
        public int planetsDrawn;

        public int skipMissing;
        public int skipNoProj;
        public int skipMag;
        public int skipBelow;
        public int skipNoIsect;

        public bool drewSun;
        public bool drewMoon;
    }

    private sealed class RasterInputs
    {
        public Color32[] pixels;
        public bool[] occ;
        public int w;
        public int h;

        public double lstDeg;
        public double latRad;

        public List<StarRecord> starsSnapshot;
        public float magLimit;
        public List<ConstellationCatalog.Constellation> constellations;
        public List<PrecomputedBody> precomputedBodies;
        public Dictionary<int, StarRecord> hipLookupSnapshot;

        public bool doHorizonCircle;
        public int horizThick;
        public Color32 horizCol;

        public bool doRadialBg;
        public Color32 bgZenith;
        public Color32 bgHorizon;

        public bool doAltRings;
        public float[] altRingDegs;
        public Color32 altRingCol;
        public int altRingThick;
        public bool doAltRingLabels;
        public int altRingLabelScale;
        public Color32 altRingLabelCol;

        public bool doCardinalLabels;
        public bool doIntercardinals;
        public int cardinalScale;
        public Color32 cardinalCol;
        public float cardinalInset;
        public bool doCardinalTicks;
        public int cardinalTickLen;
        public int cardinalTickThick;
        public Color32 cardinalTickCol;

        public bool doStarColors;
        public float starColorStr;
        public bool doStarGlow;
        public float glowMagThresh;
        public float glowRadMul;
        public float glowAlphaMul;
        public float maxStarR;
        public float minStarR;
        public float minA;
        public float maxA;

        public bool doStarLabels;
        public float maxStarLabelMag;
        public int starLabelScale;
        public Color32 starLabelCol;

        public bool doConLines;
        public bool doConLabels;
        public Color32 conLineCol;
        public int conLineThick;
        public float conLineMagLim;
        public int conLabelScale;
        public Color32 conLabelCol;
        public bool doMaskLines;
        public bool doCollision;

        public float planetDotR;
        public float planetRingR;
        public int planetRingThick;
        public Color32 planetCol;
        public float sunR;
        public Color32 sunCol;
        public float moonR;
        public Color32 moonLit;
        public Color32 moonDark;
        public float symPad;
        public int solarLabelScale;
        public Color32 solarLabelCol;
        public bool doPlanetLabels;
        public bool doSunLabel;
        public bool doMoonLabel;

        public int labelPlacementRings;
        public int labelPlacementStepPx;
        public float extraConstellationLabelClearancePx;
        public float extraStarLabelClearancePx;
        public float extraSolarLabelClearancePx;
        public bool reserveNamedStarSymbolArea;
        public float namedStarSymbolPaddingPx;
    }

    private Dictionary<int, StarRecord> _cachedHipLookup;
    private Color32[] _pixelBuffer;
    private bool[] _occBuffer;
    private bool _isExporting;

    private ConstellationCatalog.Catalog GetConstellationCatalog()
    {
        return ConstellationCatalog.LoadFromStreamingAssets(constellationsFileName);
    }

    private void EnsureBuffers(int w, int h)
    {
        int size = w * h;
        if (_pixelBuffer == null || _pixelBuffer.Length != size)
        {
            _pixelBuffer = new Color32[size];
            _occBuffer = new bool[size];
        }
        else if (_occBuffer != null)
        {
            Array.Clear(_occBuffer, 0, _occBuffer.Length);
        }
    }

    private List<PrecomputedBody> PrecomputeBodies(
        DateTimeOffset utc, double latDeg, double lonDeg, int w, int h)
    {
        var result = new List<PrecomputedBody>(10);
        if (!enablePlanets && !enableSun && !enableMoon) return result;

        var observer = new Observer(latDeg, lonDeg, 0.0);
        var astroTime = new AstroTime(utc.UtcDateTime);
        var refr = refraction ? Refraction.Normal : Refraction.None;
        int cx = w / 2, cy = h / 2;
        float R = Mathf.Min(cx, cy) - 6f;

        if (enableSun)
        {
            var b = new PrecomputedBody { name = "Sun", isSun = true, mag = -26f };
            if (TryGetBodyAltAz(Body.Sun, astroTime, observer, refr, out double alt, out double az)
                && alt > 0.0
                && TryProjectAltAzToPixel((float)alt, (float)az, cx, cy, R, out b.px, out b.py))
            {
                b.aboveHorizon = true;
            }
            result.Add(b);
        }

        if (enableMoon)
        {
            var b = new PrecomputedBody { name = "Moon", isMoon = true };
            if (TryGetBodyAltAz(Body.Moon, astroTime, observer, refr, out double alt, out double az)
                && alt > 0.0
                && TryProjectAltAzToPixel((float)alt, (float)az, cx, cy, R, out b.px, out b.py))
            {
                b.aboveHorizon = true;
                var ill = Astronomy.Illumination(Body.Moon, astroTime);
                b.mag = (float)ill.mag;
                b.moonPhaseFraction = (float)ill.phase_fraction;
                b.moonWaxing = Astronomy.MoonPhase(astroTime) < 180.0;
            }
            result.Add(b);
        }

        if (enablePlanets)
        {
            var planetBodies = new (Body body, string name)[]
            {
                (Body.Mercury, "Mercury"),
                (Body.Venus,   "Venus"),
                (Body.Mars,    "Mars"),
                (Body.Jupiter, "Jupiter"),
                (Body.Saturn,  "Saturn"),
                (Body.Uranus,  "Uranus"),
                (Body.Neptune, "Neptune"),
            };

            foreach (var (body, name) in planetBodies)
            {
                double mag = 0;
                try { mag = Astronomy.Illumination(body, astroTime).mag; }
                catch { }

                if (mag > planetMagnitudeLimit) continue;

                var b = new PrecomputedBody { name = name, mag = (float)mag };
                if (TryGetBodyAltAz(body, astroTime, observer, refr, out double alt, out double az)
                    && alt > 0.0
                    && TryProjectAltAzToPixel((float)alt, (float)az, cx, cy, R, out b.px, out b.py))
                {
                    b.aboveHorizon = true;
                }

                result.Add(b);
            }
        }

        return result;
    }

    public async void Export2DChartJpeg()
    {
        if (_isExporting)
        {
            Debug.LogWarning("[StarChartExporter2D] Export already in progress.");
            OnExportFailed?.Invoke("Export already in progress.");
            return;
        }

        _isExporting = true;

        try
        {
            if (SkySession.Instance == null)
            {
                Debug.LogError("[StarChartExporter2D] SkySession missing.");
                OnExportFailed?.Invoke("SkySession missing.");
                return;
            }

            if (catalog == null || catalog.Stars == null || catalog.VisibleStarsMag6 == null)
            {
                Debug.LogError("[StarChartExporter2D] Catalog missing.");
                OnExportFailed?.Invoke("Catalog missing.");
                return;
            }

            DateTimeOffset utc = AstronomyTime.LocalToUtc(SkySession.Instance.LocalDateTime);
            double jd = AstronomyTime.JulianDate(utc);
            double gmst = AstronomyTime.GreenwichMeanSiderealTimeDeg(jd);
            double lstDeg = AstronomyTime.LocalSiderealTimeDeg(gmst, SkySession.Instance.LongitudeDeg);
            double latRad = AstronomyTime.DegToRad(SkySession.Instance.LatitudeDeg);
            double latDeg = SkySession.Instance.LatitudeDeg;
            double lonDeg = SkySession.Instance.LongitudeDeg;
            string dtStr = SkySession.Instance.LocalDateTime.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
            string persistentPath = Application.persistentDataPath;

            var starsSnapshot = new List<StarRecord>(catalog.VisibleStarsMag6);
            float magLimit = catalog.magnitudeLimit;

            var constellationCatalog = GetConstellationCatalog();
            var constellations = constellationCatalog != null
                ? constellationCatalog.All
                : new List<ConstellationCatalog.Constellation>();

            var precomputedBodies = PrecomputeBodies(utc, latDeg, lonDeg, width, height);

            if (_cachedHipLookup == null)
                _cachedHipLookup = BuildHipLookup(catalog.Stars);

            var hipLookupSnapshot = _cachedHipLookup;

            EnsureBuffers(width, height);
            var pixels = _pixelBuffer;
            var occ = basicLabelCollisionAvoidance ? _occBuffer : null;

            var inputs = new RasterInputs
            {
                pixels = pixels,
                occ = occ,
                w = width,
                h = height,
                lstDeg = lstDeg,
                latRad = latRad,
                starsSnapshot = starsSnapshot,
                magLimit = magLimit,
                constellations = constellations,
                precomputedBodies = precomputedBodies,
                hipLookupSnapshot = hipLookupSnapshot,

                doHorizonCircle = drawHorizonCircle,
                horizThick = horizonThicknessPx,
                horizCol = horizonColor,

                doRadialBg = enableRadialBackground,
                bgZenith = backgroundZenithColor,
                bgHorizon = backgroundHorizonColor,

                doAltRings = enableAltitudeRings,
                altRingDegs = altitudeRingDegrees != null ? (float[])altitudeRingDegrees.Clone() : Array.Empty<float>(),
                altRingCol = altitudeRingColor,
                altRingThick = altitudeRingThicknessPx,
                doAltRingLabels = enableAltitudeRingLabels,
                altRingLabelScale = altitudeRingLabelFontScale,
                altRingLabelCol = altitudeRingLabelColor,

                doCardinalLabels = enableCardinalLabels,
                doIntercardinals = enableIntercardinalLabels,
                cardinalScale = cardinalLabelFontScale,
                cardinalCol = cardinalLabelColor,
                cardinalInset = cardinalLabelInsetPx,
                doCardinalTicks = enableCardinalTicks,
                cardinalTickLen = cardinalTickLengthPx,
                cardinalTickThick = cardinalTickThicknessPx,
                cardinalTickCol = cardinalTickColor,

                doStarColors = enableStarColors,
                starColorStr = starColorStrength,
                doStarGlow = enableBrightStarGlow,
                glowMagThresh = glowMagnitudeThreshold,
                glowRadMul = glowRadiusMultiplier,
                glowAlphaMul = glowAlphaMultiplier,
                maxStarR = maxStarRadiusPx,
                minStarR = minStarRadiusPx,
                minA = minAlpha,
                maxA = maxAlpha,

                doStarLabels = enableStarLabels,
                maxStarLabelMag = maxStarLabelMagnitude,
                starLabelScale = starLabelFontScale,
                starLabelCol = starLabelColor,

                doConLines = enableConstellationLines,
                doConLabels = enableConstellationLabels,
                conLineCol = constellationLineColor,
                conLineThick = constellationLineThicknessPx,
                conLineMagLim = constellationLineMagLimit,
                conLabelScale = constellationLabelFontScale,
                conLabelCol = constellationLabelColor,
                doMaskLines = maskLinesToChartCircle,
                doCollision = basicLabelCollisionAvoidance,

                planetDotR = planetDotRadiusPx,
                planetRingR = planetRingRadiusPx,
                planetRingThick = planetRingThicknessPx,
                planetCol = planetColor,
                sunR = sunRadiusPx,
                sunCol = sunColor,
                moonR = moonRadiusPx,
                moonLit = moonLitColor,
                moonDark = moonDarkColor,
                symPad = symbolCollisionPaddingPx,
                solarLabelScale = solarLabelFontScale,
                solarLabelCol = solarLabelColor,
                doPlanetLabels = enablePlanetLabels,
                doSunLabel = enableSunLabel,
                doMoonLabel = enableMoonLabel,

                labelPlacementRings = labelPlacementRings,
                labelPlacementStepPx = labelPlacementStepPx,
                extraConstellationLabelClearancePx = extraConstellationLabelClearancePx,
                extraStarLabelClearancePx = extraStarLabelClearancePx,
                extraSolarLabelClearancePx = extraSolarLabelClearancePx,
                reserveNamedStarSymbolArea = reserveNamedStarSymbolArea,
                namedStarSymbolPaddingPx = namedStarSymbolPaddingPx,
            };

            RasterStats stats;
            try
            {
                stats = await Task.Run(() => RasterizeChart(inputs));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StarChartExporter2D] Rasterisation failed: {ex}");
                OnExportFailed?.Invoke(ex.Message);
                return;
            }

            Texture2D tex = new Texture2D(inputs.w, inputs.h, TextureFormat.RGB24, false);
            tex.SetPixels32(inputs.pixels);
            tex.Apply(false, false);
            byte[] jpg = tex.EncodeToJPG(jpegQuality);
            Destroy(tex);

            string dir = Path.Combine(persistentPath, folderName);
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"StarChart2D_{dtStr}_lat{latDeg:F4}_lon{lonDeg:F4}.jpg");
            File.WriteAllBytes(file, jpg);

            Debug.Log(
                $"[StarChartExporter2D] Con lines: drawn={stats.conLinesDrawn} " +
                $"skipMissing={stats.skipMissing} skipNoProj={stats.skipNoProj} " +
                $"skipMag={stats.skipMag} skipBelow={stats.skipBelow} skipNoIsect={stats.skipNoIsect}"
            );

            Debug.Log(
                $"[StarChartExporter2D] Saved: {file} | " +
                $"stars={stats.starsDrawn} starLabels={stats.starLabelsDrawn} " +
                $"solarLabels={stats.solarLabelsDrawn} conLines={stats.conLinesDrawn} " +
                $"conLabels={stats.conLabelsDrawn} sun={stats.drewSun} moon={stats.drewMoon} " +
                $"planets={stats.planetsDrawn}"
            );

            OnExportComplete?.Invoke(file);
        }
        finally
        {
            _isExporting = false;
        }
    }

    private static RasterStats RasterizeChart(RasterInputs i)
    {
        RasterStats stats = default;

        int w = i.w;
        int h = i.h;
        int cx = w / 2;
        int cy = h / 2;
        float R = Mathf.Min(cx, cy) - 6f;
        float R2 = R * R;
        Vector2 chartCenter = new Vector2(cx, cy);

        // 1. Background
        Clear(i.pixels, new Color32(0, 0, 0, 255));
        if (i.doRadialBg)
            DrawRadialBackground(i.pixels, w, h, cx, cy, R, i.bgZenith, i.bgHorizon);

        // 2. Altitude rings
        if (i.doAltRings && i.altRingDegs != null && i.altRingDegs.Length > 0)
        {
            DrawAltitudeRings(
                i.pixels, w, h, cx, cy, R,
                i.altRingDegs, i.altRingCol, i.altRingThick,
                i.doAltRingLabels, i.altRingLabelScale, i.altRingLabelCol
            );
        }

        // 3. Horizon circle
        if (i.doHorizonCircle)
            DrawCircleOutline(i.pixels, w, h, cx, cy, Mathf.RoundToInt(R), i.horizThick, i.horizCol);

        // 4. Pre-project constellation stars, including below-horizon ones
        Dictionary<int, (Vector2 pix, float mag, double altRad)> hipToPix = null;
        if ((i.doConLines || i.doConLabels) && i.constellations != null && i.constellations.Count > 0)
        {
            var needed = new HashSet<int>();
            foreach (var con in i.constellations)
            {
                foreach (int hip in con.UniqueHipIds)
                    needed.Add(hip);
            }

            hipToPix = new Dictionary<int, (Vector2, float, double)>(needed.Count);
            foreach (int hip in needed)
            {
                if (!i.hipLookupSnapshot.TryGetValue(hip, out StarRecord star))
                    continue;

                if (ProjectToPixelAllowBelowHorizon(star, i.lstDeg, i.latRad, cx, cy, R, out Vector2 p, out double altRad))
                {
                    float m = float.IsNaN(star.mag) ? 99f : star.mag;
                    hipToPix[hip] = (p, m, altRad);
                }
            }
        }

        // Pre-reserve solar symbol areas so earlier labels (like constellation labels)
        // stay off top of Sun/Moon/planet glyphs.
        if (i.doCollision && i.occ != null && i.precomputedBodies != null)
        {
            foreach (var body in i.precomputedBodies)
            {
                if (!body.aboveHorizon) continue;

                int reserveRadius;
                if (body.isSun)
                    reserveRadius = Mathf.CeilToInt(i.sunR + i.symPad + i.extraSolarLabelClearancePx);
                else if (body.isMoon)
                    reserveRadius = Mathf.CeilToInt(i.moonR + i.symPad + i.extraSolarLabelClearancePx);
                else
                    reserveRadius = Mathf.CeilToInt(i.planetRingR + i.planetRingThick + i.symPad + i.extraSolarLabelClearancePx);

                MarkCircleOccupied(i.occ, w, h, body.px, body.py, reserveRadius);
            }
        }

        // 5. Constellation lines
        if (i.doConLines && i.constellations != null && hipToPix != null)
        {
            foreach (var con in i.constellations)
            {
                foreach (var seg in con.Segments)
                {
                    if (!i.hipLookupSnapshot.ContainsKey(seg.hip1) || !i.hipLookupSnapshot.ContainsKey(seg.hip2))
                    {
                        stats.skipMissing++;
                        continue;
                    }

                    if (!hipToPix.TryGetValue(seg.hip1, out var a) || !hipToPix.TryGetValue(seg.hip2, out var b))
                    {
                        stats.skipNoProj++;
                        continue;
                    }

                    if (a.mag > i.conLineMagLim || b.mag > i.conLineMagLim)
                    {
                        stats.skipMag++;
                        continue;
                    }

                    if (a.altRad <= 0.0 && b.altRad <= 0.0)
                    {
                        stats.skipBelow++;
                        continue;
                    }

                    if (!ClipSegmentToCircle(a.pix, b.pix, chartCenter, R, out Vector2 c0, out Vector2 c1))
                    {
                        stats.skipNoIsect++;
                        continue;
                    }

                    int x0 = Mathf.RoundToInt(c0.x);
                    int y0 = Mathf.RoundToInt(c0.y);
                    int x1 = Mathf.RoundToInt(c1.x);
                    int y1 = Mathf.RoundToInt(c1.y);

                    if (i.doMaskLines)
                    {
                        if (DrawLineThickMaskedToCircle(i.pixels, w, h, x0, y0, x1, y1, i.conLineCol, i.conLineThick, cx, cy, R2))
                            stats.conLinesDrawn++;
                    }
                    else
                    {
                        DrawLineThick(i.pixels, w, h, x0, y0, x1, y1, i.conLineCol, i.conLineThick);
                        stats.conLinesDrawn++;
                    }
                }
            }
        }

        // 6. Constellation labels
        if (i.doConLabels && i.constellations != null && hipToPix != null)
        {
            foreach (var con in i.constellations)
            {
                string label = SanitizeLabel(string.IsNullOrWhiteSpace(con.Name) ? con.Abbrev : con.Name);
                if (string.IsNullOrEmpty(label)) continue;

                int count = 0;
                double sumX = 0;
                double sumY = 0;

                foreach (int hip in con.UniqueHipIds)
                {
                    if (!hipToPix.TryGetValue(hip, out var p)) continue;

                    float ddx = p.pix.x - cx;
                    float ddy = p.pix.y - cy;
                    if (ddx * ddx + ddy * ddy > R2) continue;

                    sumX += p.pix.x;
                    sumY += p.pix.y;
                    count++;
                }

                if (count < 2) continue;

                int lx = (int)Math.Round(sumX / count);
                int ly = (int)Math.Round(sumY / count);

                float ldx = lx - cx;
                float ldy = ly - cy;
                if (ldx * ldx + ldy * ldy > R2) continue;

                if (TryPlaceLabelAroundAnchor(
                        i.pixels, i.occ, w, h,
                        lx, ly,
                        label, i.conLabelScale, i.conLabelCol,
                        i.extraConstellationLabelClearancePx,
                        i.doCollision,
                        i.labelPlacementRings,
                        i.labelPlacementStepPx))
                {
                    stats.conLabelsDrawn++;
                }
            }
        }

        // 7. Stars
        List<LabelCandidate> starLabelCandidates = i.doStarLabels ? new List<LabelCandidate>(512) : null;

        foreach (var star in i.starsSnapshot)
        {
            if (float.IsNaN(star.ra) || float.IsNaN(star.dec) || float.IsNaN(star.mag))
                continue;

            if (!TryProjectToPixelAboveHorizonOnly(star, i.lstDeg, i.latRad, cx, cy, R, out int sx, out int sy, out _))
                continue;

            float t = Mathf.InverseLerp(0f, i.magLimit, star.mag);
            float radius = Mathf.Lerp(i.maxStarR, i.minStarR, Mathf.Pow(t, 1.7f));
            float alpha = Mathf.Lerp(i.maxA, i.minA, Mathf.Pow(t, 1.3f));

            Color32 tint = new Color32(255, 255, 255, 255);
            bool useColor = i.doStarColors && !float.IsNaN(star.ci);
            if (useColor)
                tint = BVToColor(star.ci, i.starColorStr);

            if (i.doStarGlow && star.mag <= i.glowMagThresh)
            {
                if (useColor)
                    DrawSoftDotColored(i.pixels, w, h, sx, sy, radius * i.glowRadMul, alpha * i.glowAlphaMul, tint);
                else
                    DrawSoftDot(i.pixels, w, h, sx, sy, radius * i.glowRadMul, alpha * i.glowAlphaMul);
            }

            if (useColor)
                DrawSoftDotColored(i.pixels, w, h, sx, sy, radius, alpha, tint);
            else
                DrawSoftDot(i.pixels, w, h, sx, sy, radius, alpha);

            stats.starsDrawn++;

            bool wantsStarLabel =
                i.doStarLabels &&
                !string.IsNullOrWhiteSpace(star.proper) &&
                star.mag <= i.maxStarLabelMag;

            if (wantsStarLabel && i.doCollision && i.occ != null && i.reserveNamedStarSymbolArea)
            {
                MarkCircleOccupied(
                    i.occ, w, h, sx, sy,
                    Mathf.CeilToInt(radius + i.namedStarSymbolPaddingPx)
                );
            }

            if (wantsStarLabel)
            {
                starLabelCandidates.Add(new LabelCandidate
                {
                    x = sx,
                    y = sy,
                    mag = star.mag,
                    name = star.proper.Trim(),
                    avoidRadiusPx = radius + i.extraStarLabelClearancePx
                });
            }
        }

        // 8. Solar system bodies
        List<LabelCandidate> solarLabelCandidates =
            (i.doPlanetLabels || i.doSunLabel || i.doMoonLabel)
            ? new List<LabelCandidate>(64)
            : null;

        foreach (var body in i.precomputedBodies)
        {
            if (!body.aboveHorizon) continue;

            if (body.isSun)
            {
                DrawSolidDot(i.pixels, w, h, body.px, body.py, Mathf.CeilToInt(i.sunR), i.sunCol);

                if (i.doCollision && i.occ != null)
                    MarkCircleOccupied(i.occ, w, h, body.px, body.py, Mathf.CeilToInt(i.sunR + i.symPad));

                stats.drewSun = true;

                if (i.doSunLabel && solarLabelCandidates != null)
                {
                    solarLabelCandidates.Add(new LabelCandidate
                    {
                        x = body.px,
                        y = body.py,
                        mag = body.mag,
                        name = "Sun",
                        avoidRadiusPx = i.sunR + i.symPad + i.extraSolarLabelClearancePx
                    });
                }
            }
            else if (body.isMoon)
            {
                DrawMoonPhaseSymbol(
                    i.pixels, w, h, body.px, body.py,
                    i.moonR, body.moonPhaseFraction, body.moonWaxing,
                    i.moonLit, i.moonDark
                );

                if (i.doCollision && i.occ != null)
                    MarkCircleOccupied(i.occ, w, h, body.px, body.py, Mathf.CeilToInt(i.moonR + i.symPad));

                stats.drewMoon = true;

                if (i.doMoonLabel && solarLabelCandidates != null)
                {
                    solarLabelCandidates.Add(new LabelCandidate
                    {
                        x = body.px,
                        y = body.py,
                        mag = body.mag,
                        name = "Moon",
                        avoidRadiusPx = i.moonR + i.symPad + i.extraSolarLabelClearancePx
                    });
                }
            }
            else
            {
                DrawPlanetSymbol(i.pixels, w, h, body.px, body.py, i.planetDotR, i.planetRingR, i.planetRingThick, i.planetCol);

                stats.planetsDrawn++;

                if (i.doCollision && i.occ != null)
                    MarkCircleOccupied(i.occ, w, h, body.px, body.py, Mathf.CeilToInt(i.planetRingR + i.planetRingThick + i.symPad));

                if (i.doPlanetLabels && solarLabelCandidates != null)
                {
                    solarLabelCandidates.Add(new LabelCandidate
                    {
                        x = body.px,
                        y = body.py,
                        mag = body.mag,
                        name = body.name,
                        avoidRadiusPx = i.planetRingR + i.planetRingThick + i.symPad + i.extraSolarLabelClearancePx
                    });
                }
            }
        }

        // 9. Cardinal markings
        if (i.doCardinalLabels || i.doCardinalTicks)
        {
            DrawCardinalMarkings(
                i.pixels, w, h, cx, cy, R,
                i.doIntercardinals,
                i.doCardinalTicks, i.cardinalTickLen, i.cardinalTickThick, i.cardinalTickCol,
                i.doCardinalLabels, i.cardinalScale, i.cardinalCol, i.cardinalInset,
                i.occ, i.doCollision
            );
        }

        // 10. Deferred labels: solar first
        if (solarLabelCandidates != null && solarLabelCandidates.Count > 0)
        {
            solarLabelCandidates.Sort((a, b) =>
            {
                int m = a.mag.CompareTo(b.mag);
                return m != 0 ? m : string.CompareOrdinal(a.name, b.name);
            });

            foreach (var lc in solarLabelCandidates)
            {
                string text = SanitizeLabel(lc.name);
                if (string.IsNullOrEmpty(text)) continue;

                if (TryPlaceLabelAroundAnchor(
                        i.pixels, i.occ, w, h,
                        lc.x, lc.y,
                        text, i.solarLabelScale, i.solarLabelCol,
                        lc.avoidRadiusPx,
                        i.doCollision,
                        i.labelPlacementRings,
                        i.labelPlacementStepPx))
                {
                    stats.solarLabelsDrawn++;
                }
            }
        }

        // 10b. Star labels
        if (i.doStarLabels && starLabelCandidates != null && starLabelCandidates.Count > 0)
        {
            starLabelCandidates.Sort((a, b) =>
            {
                int m = a.mag.CompareTo(b.mag);
                return m != 0 ? m : string.CompareOrdinal(a.name, b.name);
            });

            foreach (var lc in starLabelCandidates)
            {
                string text = SanitizeLabel(lc.name);
                if (string.IsNullOrEmpty(text)) continue;

                if (TryPlaceLabelAroundAnchor(
                        i.pixels, i.occ, w, h,
                        lc.x, lc.y,
                        text, i.starLabelScale, i.starLabelCol,
                        lc.avoidRadiusPx,
                        i.doCollision,
                        i.labelPlacementRings,
                        i.labelPlacementStepPx))
                {
                    stats.starLabelsDrawn++;
                }
            }
        }

        return stats;
    }

    private static bool TryPlaceLabelAroundAnchor(
        Color32[] pix, bool[] occ, int w, int h,
        int anchorX, int anchorY,
        string text, int scale, Color32 color,
        float clearancePx,
        bool doCollision,
        int searchRings,
        int stepPx)
    {
        int textW = BitmapFont5x7.MeasureWidth(text, scale);
        int textH = BitmapFont5x7.MeasureHeight(scale);

        int baseR = Mathf.Max(1, Mathf.CeilToInt(clearancePx));

        for (int ring = 0; ring < searchRings; ring++)
        {
            int r = baseR + ring * stepPx;
            int halfStep = Mathf.Max(1, stepPx / 2);

            Vector2Int[] offsets =
            {
                new Vector2Int( r,                    -textH / 2),
                new Vector2Int(-r - textW,           -textH / 2),
                new Vector2Int(-textW / 2,           -r - textH),
                new Vector2Int(-textW / 2,            r),

                new Vector2Int( r,                   -r - textH),
                new Vector2Int(-r - textW,          -r - textH),
                new Vector2Int( r,                    r),
                new Vector2Int(-r - textW,            r),

                new Vector2Int( r + halfStep,        -textH),
                new Vector2Int( r + halfStep,         0),
                new Vector2Int(-r - textW - halfStep, -textH),
                new Vector2Int(-r - textW - halfStep,  0),
            };

            foreach (var off in offsets)
            {
                int tx = anchorX + off.x;
                int ty = anchorY + off.y;

                if (tx < 0 || ty < 0 || tx + textW >= w || ty + textH >= h)
                    continue;

                if (doCollision && RectAnyOccupied(occ, w, h, tx, ty, textW, textH))
                    continue;

                BitmapFont5x7.DrawText(pix, w, h, tx, ty, text, scale, color);

                if (doCollision)
                    MarkRectOccupied(occ, w, h, tx, ty, textW, textH);

                return true;
            }
        }

        return false;
    }

    private static void DrawCardinalMarkings(
        Color32[] pix, int w, int h, int cx, int cy, float R,
        bool doIntercardinals,
        bool doTicks, int tickLen, int tickThick, Color32 tickCol,
        bool doLabels, int labelScale, Color32 labelCol, float labelInset,
        bool[] occ, bool doCollision)
    {
        var dirs = new List<(float az, string label)>(8)
        {
            (0f,   "N"),
            (90f,  "E"),
            (180f, "S"),
            (270f, "W"),
        };

        if (doIntercardinals)
        {
            dirs.Add((45f,  "NE"));
            dirs.Add((135f, "SE"));
            dirs.Add((225f, "SW"));
            dirs.Add((315f, "NW"));
        }

        foreach (var (az, labelText) in dirs)
        {
            float azRad = az * Mathf.Deg2Rad;
            float dirX = -Mathf.Sin(azRad);
            float dirY = -Mathf.Cos(azRad);

            float bx = cx + R * dirX;
            float by = cy + R * dirY;

            if (doTicks)
            {
                int x0 = Mathf.RoundToInt(bx - dirX * tickLen);
                int y0 = Mathf.RoundToInt(by - dirY * tickLen);
                DrawLineThick(pix, w, h, x0, y0, Mathf.RoundToInt(bx), Mathf.RoundToInt(by), tickCol, tickThick);
            }

            if (doLabels)
            {
                int textW = BitmapFont5x7.MeasureWidth(labelText, labelScale);
                int textH = BitmapFont5x7.MeasureHeight(labelScale);

                float lcx = bx - dirX * labelInset;
                float lcy = by - dirY * labelInset;

                int tx = Mathf.Clamp(Mathf.RoundToInt(lcx) - textW / 2, 0, w - textW - 1);
                int ty = Mathf.Clamp(Mathf.RoundToInt(lcy) - textH / 2, 0, h - textH - 1);

                if (doCollision && RectAnyOccupied(occ, w, h, tx, ty, textW, textH))
                    continue;

                BitmapFont5x7.DrawText(pix, w, h, tx, ty, labelText, labelScale, labelCol);

                if (doCollision)
                    MarkRectOccupied(occ, w, h, tx, ty, textW, textH);
            }
        }
    }

    private static void DrawAltitudeRings(
        Color32[] pix, int w, int h, int cx, int cy, float R,
        float[] altRingDegs, Color32 ringCol, int ringThick,
        bool doLabels, int labelScale, Color32 labelCol)
    {
        foreach (float altDeg in altRingDegs)
        {
            if (altDeg <= 0f || altDeg >= 90f) continue;

            float ringR = (90f - altDeg) / 90f * R;
            if (ringR < 2f) continue;

            DrawCircleOutline(pix, w, h, cx, cy, Mathf.RoundToInt(ringR), ringThick, ringCol);

            if (doLabels)
            {
                string labelText = ((int)Mathf.Round(altDeg)).ToString();
                int textW = BitmapFont5x7.MeasureWidth(labelText, labelScale);
                int textH = BitmapFont5x7.MeasureHeight(labelScale);
                int tx = Mathf.Clamp(cx + Mathf.RoundToInt(ringR) + 3, 0, w - textW - 1);
                int ty = Mathf.Clamp(cy - textH / 2, 0, h - textH - 1);
                BitmapFont5x7.DrawText(pix, w, h, tx, ty, labelText, labelScale, labelCol);
            }
        }
    }

    private static bool TryGetBodyAltAz(
        Body body, AstroTime time, Observer obs, Refraction refr,
        out double altDeg, out double azDeg)
    {
        altDeg = 0;
        azDeg = 0;

        try
        {
            var eq = Astronomy.Equator(body, time, obs, EquatorEpoch.OfDate, Aberration.Corrected);
            var hor = Astronomy.Horizon(time, obs, eq.ra, eq.dec, refr);
            altDeg = hor.altitude;
            azDeg = hor.azimuth;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<int, StarRecord> BuildHipLookup(List<StarRecord> allStars)
    {
        var dict = new Dictionary<int, StarRecord>(allStars.Count / 3);
        foreach (var s in allStars)
        {
            if (s != null && s.hip > 0 && !dict.ContainsKey(s.hip))
                dict[s.hip] = s;
        }
        return dict;
    }

    private static bool TryProjectToPixelAboveHorizonOnly(
        StarRecord star, double lstDeg, double latRad, int cx, int cy, float R,
        out int x, out int y, out float altDeg)
    {
        x = 0;
        y = 0;
        altDeg = 0;

        double raDeg = star.ra * 15.0;
        double haDeg = AstronomyTime.HourAngleDeg(lstDeg, raDeg);
        double haRad = AstronomyTime.DegToRad(haDeg);
        double decRad = AstronomyTime.DegToRad(star.dec);

        double sinAlt = Math.Clamp(
            Math.Sin(decRad) * Math.Sin(latRad) +
            Math.Cos(decRad) * Math.Cos(latRad) * Math.Cos(haRad),
            -1.0, 1.0
        );

        double altR = Math.Asin(sinAlt);
        altDeg = (float)AstronomyTime.RadToDeg(altR);
        if (altR <= 0) return false;

        double sinHA = Math.Sin(haRad);
        double cosHA = Math.Cos(haRad);
        double azSouth = Math.Atan2(
            sinHA,
            cosHA * Math.Sin(latRad) - Math.Tan(decRad) * Math.Cos(latRad)
        );
        double azRad = ((azSouth + Math.PI) % (2.0 * Math.PI) + 2.0 * Math.PI) % (2.0 * Math.PI);

        float pr = (float)((Math.PI / 2.0 - altR) / (Math.PI / 2.0)) * R;

        x = cx - Mathf.RoundToInt(pr * Mathf.Sin((float)azRad));
        y = cy - Mathf.RoundToInt(pr * Mathf.Cos((float)azRad));

        float dx = x - cx;
        float dy = y - cy;
        return dx * dx + dy * dy <= R * R;
    }

    private static bool ProjectToPixelAllowBelowHorizon(
        StarRecord star, double lstDeg, double latRad, int cx, int cy, float R,
        out Vector2 pix, out double altRad)
    {
        pix = default;
        altRad = 0;

        if (float.IsNaN(star.ra) || float.IsNaN(star.dec)) return false;

        double raDeg = star.ra * 15.0;
        double haDeg = AstronomyTime.HourAngleDeg(lstDeg, raDeg);
        double haRad = AstronomyTime.DegToRad(haDeg);
        double decRad = AstronomyTime.DegToRad(star.dec);

        double sinAlt = Math.Clamp(
            Math.Sin(decRad) * Math.Sin(latRad) +
            Math.Cos(decRad) * Math.Cos(latRad) * Math.Cos(haRad),
            -1.0, 1.0
        );
        altRad = Math.Asin(sinAlt);

        double sinHA = Math.Sin(haRad);
        double cosHA = Math.Cos(haRad);
        double azSouth = Math.Atan2(
            sinHA,
            cosHA * Math.Sin(latRad) - Math.Tan(decRad) * Math.Cos(latRad)
        );
        double azRad = ((azSouth + Math.PI) % (2.0 * Math.PI) + 2.0 * Math.PI) % (2.0 * Math.PI);

        float pr = (float)((Math.PI / 2.0 - altRad) / (Math.PI / 2.0)) * R;
        pix = new Vector2(
            cx - pr * Mathf.Sin((float)azRad),
            cy - pr * Mathf.Cos((float)azRad)
        );

        return true;
    }

    private static bool TryProjectAltAzToPixel(
        float altDeg, float azDeg, int cx, int cy, float R,
        out int x, out int y)
    {
        x = 0;
        y = 0;

        if (altDeg <= 0f) return false;

        float pr = Mathf.Clamp01((90f - altDeg) / 90f) * R;
        float azRad = azDeg * Mathf.Deg2Rad;

        x = Mathf.RoundToInt(cx - pr * Mathf.Sin(azRad));
        y = Mathf.RoundToInt(cy - pr * Mathf.Cos(azRad));

        float dx = x - cx;
        float dy = y - cy;
        return dx * dx + dy * dy <= R * R;
    }

    private static bool ClipSegmentToCircle(
        Vector2 a, Vector2 b, Vector2 c, float R,
        out Vector2 outA, out Vector2 outB)
    {
        outA = default;
        outB = default;

        Vector2 A = a - c;
        Vector2 B = b - c;
        Vector2 d = B - A;

        float r2 = R * R;
        float aIn = Vector2.Dot(A, A) <= r2 ? 1 : 0;
        float bIn = Vector2.Dot(B, B) <= r2 ? 1 : 0;

        if (aIn > 0 && bIn > 0)
        {
            outA = a;
            outB = b;
            return true;
        }

        float Ac = Vector2.Dot(d, d);
        if (Ac < 1e-12f) return false;

        float Bc = 2f * Vector2.Dot(A, d);
        float Cc = Vector2.Dot(A, A) - r2;
        float disc = Bc * Bc - 4f * Ac * Cc;
        if (disc < 0) return false;

        float sq = Mathf.Sqrt(disc);
        float t0 = (-Bc - sq) / (2f * Ac);
        float t1 = (-Bc + sq) / (2f * Ac);
        if (t0 > t1) (t0, t1) = (t1, t0);

        float enter = Mathf.Clamp01(t0);
        float exit = Mathf.Clamp01(t1);

        if (exit <= enter)
        {
            float t = (t0 >= 0f && t0 <= 1f) ? t0 : ((t1 >= 0f && t1 <= 1f) ? t1 : -1f);
            if (t < 0) return false;

            if (aIn > 0)
            {
                outA = a;
                outB = c + (A + t * d);
                return true;
            }

            if (bIn > 0)
            {
                outA = c + (A + t * d);
                outB = b;
                return true;
            }

            return false;
        }

        Vector2 Pe = c + (A + enter * d);
        Vector2 Px = c + (A + exit * d);

        if (aIn > 0)
        {
            outA = a;
            outB = Px;
            return true;
        }

        if (bIn > 0)
        {
            outA = Pe;
            outB = b;
            return true;
        }

        outA = Pe;
        outB = Px;
        return true;
    }

    private static void Clear(Color32[] pix, Color32 c)
    {
        Array.Fill(pix, c);
    }

    private static void DrawRadialBackground(
        Color32[] pix, int w, int h,
        int cx, int cy, float R,
        Color32 zenith, Color32 horizon)
    {
        float invR = R > 1e-4f ? 1f / R : 0f;
        int iR = (int)R;
        int minX = Mathf.Max(0, cx - iR);
        int maxX = Mathf.Min(w - 1, cx + iR);
        int minY = Mathf.Max(0, cy - iR);
        int maxY = Mathf.Min(h - 1, cy + iR);

        for (int yy = minY; yy <= maxY; yy++)
        {
            float dy = yy - cy;
            float dy2 = dy * dy;
            int row = (h - 1 - yy) * w;

            for (int xx = minX; xx <= maxX; xx++)
            {
                float dx = xx - cx;
                float d2 = dx * dx + dy2;
                if (d2 > R * R) continue;

                float t = Mathf.Sqrt(d2) * invR;
                t *= t;
                pix[row + xx] = Color32.Lerp(zenith, horizon, t);
            }
        }
    }

    private static void DrawCircleOutline(
        Color32[] pix, int w, int h,
        int cx, int cy, int r, int thickness, Color32 col)
    {
        int outer2 = r * r;
        int inner = Mathf.Max(0, r - thickness);
        int inner2 = inner * inner;
        int minX = Mathf.Max(0, cx - r);
        int maxX = Mathf.Min(w - 1, cx + r);
        int minY = Mathf.Max(0, cy - r);
        int maxY = Mathf.Min(h - 1, cy + r);

        for (int yy = minY; yy <= maxY; yy++)
        {
            int dy2 = (yy - cy) * (yy - cy);
            int row = (h - 1 - yy) * w;
            for (int xx = minX; xx <= maxX; xx++)
            {
                int d2 = (xx - cx) * (xx - cx) + dy2;
                if (d2 <= outer2 && d2 >= inner2)
                    pix[row + xx] = col;
            }
        }
    }

    private static void DrawSoftDot(
        Color32[] pix, int w, int h,
        int cx, int cy, float radius, float alpha)
    {
        int r = Mathf.CeilToInt(radius);
        int minX = Mathf.Max(0, cx - r);
        int maxX = Mathf.Min(w - 1, cx + r);
        int minY = Mathf.Max(0, cy - r);
        int maxY = Mathf.Min(h - 1, cy + r);
        float r2 = radius * radius;
        float invR = radius > 1e-4f ? 1f / radius : 0f;

        for (int yy = minY; yy <= maxY; yy++)
        {
            float dy = yy - cy;
            float dy2 = dy * dy;
            int row = (h - 1 - yy) * w;

            for (int xx = minX; xx <= maxX; xx++)
            {
                float dx = xx - cx;
                float d2 = dx * dx + dy2;
                if (d2 > r2) continue;

                float falloff = 1f - Mathf.Sqrt(d2) * invR;
                AdditiveBlendWhite(ref pix[row + xx], alpha * falloff * falloff);
            }
        }
    }

    private static void DrawSoftDotColored(
        Color32[] pix, int w, int h,
        int cx, int cy, float radius, float alpha, Color32 tint)
    {
        int r = Mathf.CeilToInt(radius);
        int minX = Mathf.Max(0, cx - r);
        int maxX = Mathf.Min(w - 1, cx + r);
        int minY = Mathf.Max(0, cy - r);
        int maxY = Mathf.Min(h - 1, cy + r);
        float r2 = radius * radius;
        float invR = radius > 1e-4f ? 1f / radius : 0f;

        for (int yy = minY; yy <= maxY; yy++)
        {
            float dy = yy - cy;
            float dy2 = dy * dy;
            int row = (h - 1 - yy) * w;

            for (int xx = minX; xx <= maxX; xx++)
            {
                float dx = xx - cx;
                float d2 = dx * dx + dy2;
                if (d2 > r2) continue;

                float falloff = 1f - Mathf.Sqrt(d2) * invR;
                float a = alpha * falloff * falloff;

                ref Color32 dst = ref pix[row + xx];
                dst.r = (byte)Mathf.Min(255, dst.r + Mathf.RoundToInt(tint.r * a));
                dst.g = (byte)Mathf.Min(255, dst.g + Mathf.RoundToInt(tint.g * a));
                dst.b = (byte)Mathf.Min(255, dst.b + Mathf.RoundToInt(tint.b * a));
                dst.a = 255;
            }
        }
    }

    private static void AdditiveBlendWhite(ref Color32 dst, float a)
    {
        byte add = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * a), 0, 255);
        dst.r = (byte)Mathf.Min(255, dst.r + add);
        dst.g = (byte)Mathf.Min(255, dst.g + add);
        dst.b = (byte)Mathf.Min(255, dst.b + add);
        dst.a = 255;
    }

    private static void DrawLineThick(
        Color32[] pix, int w, int h,
        int x0, int y0, int x1, int y1,
        Color32 col, int thickness)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        int r = Mathf.Max(0, thickness - 1);

        while (true)
        {
            DrawSolidDot(pix, w, h, x0, y0, r, col);
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private static bool DrawLineThickMaskedToCircle(
        Color32[] pix, int w, int h,
        int x0, int y0, int x1, int y1,
        Color32 col, int thickness,
        int chartCx, int chartCy, float chartR2)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        int r = Mathf.Max(0, thickness - 1);
        bool wrote = false;

        while (true)
        {
            wrote |= DrawSolidDotMaskedToCircle(pix, w, h, x0, y0, r, col, chartCx, chartCy, chartR2);
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }

        return wrote;
    }

    private static void DrawSolidDot(
        Color32[] pix, int w, int h,
        int cx, int cy, int radius, Color32 col)
    {
        int minX = Mathf.Max(0, cx - radius);
        int maxX = Mathf.Min(w - 1, cx + radius);
        int minY = Mathf.Max(0, cy - radius);
        int maxY = Mathf.Min(h - 1, cy + radius);
        int r2 = radius * radius;

        for (int yy = minY; yy <= maxY; yy++)
        {
            int dy2 = (yy - cy) * (yy - cy);
            int row = (h - 1 - yy) * w;
            for (int xx = minX; xx <= maxX; xx++)
            {
                int dxv = xx - cx;
                if (dxv * dxv + dy2 <= r2)
                    pix[row + xx] = col;
            }
        }
    }

    private static bool DrawSolidDotMaskedToCircle(
        Color32[] pix, int w, int h,
        int px, int py, int radius, Color32 col,
        int chartCx, int chartCy, float chartR2)
    {
        int minX = Mathf.Max(0, px - radius);
        int maxX = Mathf.Min(w - 1, px + radius);
        int minY = Mathf.Max(0, py - radius);
        int maxY = Mathf.Min(h - 1, py + radius);
        int dotR2 = radius * radius;
        bool wrote = false;

        for (int yy = minY; yy <= maxY; yy++)
        {
            int dyDot = (yy - py) * (yy - py);
            int dyChart = yy - chartCy;
            int row = (h - 1 - yy) * w;

            for (int xx = minX; xx <= maxX; xx++)
            {
                int dxDot = xx - px;
                if (dxDot * dxDot + dyDot > dotR2) continue;

                int dxC = xx - chartCx;
                if (dxC * dxC + dyChart * dyChart > chartR2) continue;

                pix[row + xx] = col;
                wrote = true;
            }
        }

        return wrote;
    }

    private static void DrawPlanetSymbol(
        Color32[] pix, int w, int h,
        int x, int y, float dotR, float ringR, int ringThick, Color32 col)
    {
        DrawSolidDot(pix, w, h, x, y, Mathf.CeilToInt(dotR), col);
        DrawCircleOutline(pix, w, h, x, y, Mathf.CeilToInt(ringR), ringThick, col);
    }

    private static void DrawMoonPhaseSymbol(
        Color32[] pix, int w, int h,
        int cx, int cy, float radiusPx, float phaseFraction, bool waxing,
        Color32 lit, Color32 dark)
    {
        int r = Mathf.CeilToInt(radiusPx);
        int r2 = r * r;
        int minX = Mathf.Max(0, cx - r);
        int maxX = Mathf.Min(w - 1, cx + r);
        int minY = Mathf.Max(0, cy - r);
        int maxY = Mathf.Min(h - 1, cy + r);

        for (int yy = minY; yy <= maxY; yy++)
        {
            int dy2 = (yy - cy) * (yy - cy);
            int row = (h - 1 - yy) * w;
            for (int xx = minX; xx <= maxX; xx++)
            {
                int dx = xx - cx;
                if (dx * dx + dy2 <= r2)
                    pix[row + xx] = dark;
            }
        }

        float f = Mathf.Clamp01(phaseFraction);
        float a = Mathf.Abs(2f * f - 1f);
        bool gibbous = f >= 0.5f;

        for (int yy = minY; yy <= maxY; yy++)
        {
            int dy2 = (yy - cy) * (yy - cy);
            int inside = r2 - dy2;
            if (inside < 0) continue;

            float xEdge = a * Mathf.Sqrt(inside);
            int row = (h - 1 - yy) * w;

            for (int xx = minX; xx <= maxX; xx++)
            {
                int dx = xx - cx;
                if (dx * dx + dy2 > r2) continue;

                bool illuminated = gibbous
                    ? (waxing ? dx >= -xEdge : dx <= xEdge)
                    : (waxing ? dx >= xEdge : dx <= -xEdge);

                if (illuminated)
                    pix[row + xx] = lit;
            }
        }
    }

    private static Color32 BVToColor(float bv, float strength)
    {
        bv = Mathf.Clamp(bv, -0.4f, 2.0f);
        float t = (bv + 0.4f) / 2.4f;

        Color32 s;
        if (t < 0.20f)
            s = Color32.Lerp(new Color32(155, 176, 255, 255), new Color32(170, 191, 255, 255), t / 0.20f);
        else if (t < 0.40f)
            s = Color32.Lerp(new Color32(170, 191, 255, 255), new Color32(255, 255, 255, 255), (t - 0.20f) / 0.20f);
        else if (t < 0.55f)
            s = Color32.Lerp(new Color32(255, 255, 255, 255), new Color32(255, 255, 210, 255), (t - 0.40f) / 0.15f);
        else if (t < 0.70f)
            s = Color32.Lerp(new Color32(255, 255, 210, 255), new Color32(255, 244, 160, 255), (t - 0.55f) / 0.15f);
        else if (t < 0.85f)
            s = Color32.Lerp(new Color32(255, 244, 160, 255), new Color32(255, 200, 100, 255), (t - 0.70f) / 0.15f);
        else
            s = Color32.Lerp(new Color32(255, 200, 100, 255), new Color32(255, 90, 40, 255), (t - 0.85f) / 0.15f);

        return Color32.Lerp(new Color32(255, 255, 255, 255), s, strength);
    }

    private static bool RectAnyOccupied(bool[] occ, int w, int h, int x, int y, int rw, int rh)
    {
        if (occ == null) return false;

        int x2 = Mathf.Min(w - 1, x + rw);
        int y2 = Mathf.Min(h - 1, y + rh);

        for (int yy = y; yy <= y2; yy++)
        {
            int row = yy * w;
            for (int xx = x; xx <= x2; xx++)
            {
                if (occ[row + xx]) return true;
            }
        }

        return false;
    }

    private static void MarkRectOccupied(bool[] occ, int w, int h, int x, int y, int rw, int rh)
    {
        if (occ == null) return;

        int x2 = Mathf.Min(w - 1, x + rw);
        int y2 = Mathf.Min(h - 1, y + rh);

        for (int yy = y; yy <= y2; yy++)
        {
            int row = yy * w;
            for (int xx = x; xx <= x2; xx++)
                occ[row + xx] = true;
        }
    }

    private static void MarkCircleOccupied(bool[] occ, int w, int h, int cx, int cy, int radius)
    {
        if (occ == null) return;

        int r = Mathf.Max(0, radius);
        int r2 = r * r;
        int minX = Mathf.Max(0, cx - r);
        int maxX = Mathf.Min(w - 1, cx + r);
        int minY = Mathf.Max(0, cy - r);
        int maxY = Mathf.Min(h - 1, cy + r);

        for (int yy = minY; yy <= maxY; yy++)
        {
            int dy2 = (yy - cy) * (yy - cy);
            int row = yy * w;
            for (int xx = minX; xx <= maxX; xx++)
            {
                int dx = xx - cx;
                if (dx * dx + dy2 <= r2)
                    occ[row + xx] = true;
            }
        }
    }

    private static string SanitizeLabel(string s)
    {
        s = s.Trim().ToUpperInvariant();
        var sb = new StringBuilder(s.Length);
        bool lastSpace = false;

        foreach (char c in s)
        {
            bool ok =
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '-' || c == '.' || c == '\'';

            if (ok)
            {
                sb.Append(c);
                lastSpace = false;
            }
            else
            {
                if (!lastSpace) sb.Append(' ');
                lastSpace = true;
            }
        }

        if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
            sb.Length--;

        return sb.ToString();
    }

    private static class BitmapFont5x7
    {
        private static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
        {
            {' ',new byte[]{0,0,0,0,0,0,0}},
            {'0',new byte[]{0x1E,0x11,0x13,0x15,0x19,0x11,0x1E}},
            {'1',new byte[]{0x04,0x0C,0x04,0x04,0x04,0x04,0x0E}},
            {'2',new byte[]{0x1E,0x01,0x01,0x1E,0x10,0x10,0x1F}},
            {'3',new byte[]{0x1E,0x01,0x01,0x0E,0x01,0x01,0x1E}},
            {'4',new byte[]{0x02,0x06,0x0A,0x12,0x1F,0x02,0x02}},
            {'5',new byte[]{0x1F,0x10,0x10,0x1E,0x01,0x01,0x1E}},
            {'6',new byte[]{0x0E,0x10,0x10,0x1E,0x11,0x11,0x0E}},
            {'7',new byte[]{0x1F,0x01,0x02,0x04,0x08,0x08,0x08}},
            {'8',new byte[]{0x0E,0x11,0x11,0x0E,0x11,0x11,0x0E}},
            {'9',new byte[]{0x0E,0x11,0x11,0x0F,0x01,0x01,0x0E}},
            {'-',new byte[]{0,0,0,0x1F,0,0,0}},
            {'.',new byte[]{0,0,0,0,0,0x0C,0x0C}},
            {'\'',new byte[]{0x04,0x04,0x02,0,0,0,0}},
            {'A',new byte[]{0x0E,0x11,0x11,0x1F,0x11,0x11,0x11}},
            {'B',new byte[]{0x1E,0x11,0x11,0x1E,0x11,0x11,0x1E}},
            {'C',new byte[]{0x0E,0x11,0x10,0x10,0x10,0x11,0x0E}},
            {'D',new byte[]{0x1C,0x12,0x11,0x11,0x11,0x12,0x1C}},
            {'E',new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x1F}},
            {'F',new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x10}},
            {'G',new byte[]{0x0E,0x11,0x10,0x17,0x11,0x11,0x0E}},
            {'H',new byte[]{0x11,0x11,0x11,0x1F,0x11,0x11,0x11}},
            {'I',new byte[]{0x0E,0x04,0x04,0x04,0x04,0x04,0x0E}},
            {'J',new byte[]{0x07,0x02,0x02,0x02,0x02,0x12,0x0C}},
            {'K',new byte[]{0x11,0x12,0x14,0x18,0x14,0x12,0x11}},
            {'L',new byte[]{0x10,0x10,0x10,0x10,0x10,0x10,0x1F}},
            {'M',new byte[]{0x11,0x1B,0x15,0x11,0x11,0x11,0x11}},
            {'N',new byte[]{0x11,0x19,0x15,0x13,0x11,0x11,0x11}},
            {'O',new byte[]{0x0E,0x11,0x11,0x11,0x11,0x11,0x0E}},
            {'P',new byte[]{0x1E,0x11,0x11,0x1E,0x10,0x10,0x10}},
            {'Q',new byte[]{0x0E,0x11,0x11,0x11,0x15,0x12,0x0D}},
            {'R',new byte[]{0x1E,0x11,0x11,0x1E,0x14,0x12,0x11}},
            {'S',new byte[]{0x0F,0x10,0x10,0x0E,0x01,0x01,0x1E}},
            {'T',new byte[]{0x1F,0x04,0x04,0x04,0x04,0x04,0x04}},
            {'U',new byte[]{0x11,0x11,0x11,0x11,0x11,0x11,0x0E}},
            {'V',new byte[]{0x11,0x11,0x11,0x11,0x11,0x0A,0x04}},
            {'W',new byte[]{0x11,0x11,0x11,0x11,0x15,0x1B,0x11}},
            {'X',new byte[]{0x11,0x11,0x0A,0x04,0x0A,0x11,0x11}},
            {'Y',new byte[]{0x11,0x11,0x0A,0x04,0x04,0x04,0x04}},
            {'Z',new byte[]{0x1F,0x01,0x02,0x04,0x08,0x10,0x1F}},
        };

        public static int MeasureWidth(string text, int scale)
        {
            return text.Length * 6 * scale;
        }

        public static int MeasureHeight(int scale)
        {
            return 7 * scale;
        }

        public static void DrawText(Color32[] pix, int w, int h, int x, int y, string text, int scale, Color32 col)
        {
            int penX = x;
            foreach (char c in text)
            {
                DrawChar(pix, w, h, penX, y, c, scale, col);
                penX += 6 * scale;
            }
        }

        private static void DrawChar(Color32[] pix, int w, int h, int x, int y, char c, int scale, Color32 col)
        {
            if (!Glyphs.TryGetValue(c, out var rows))
                rows = Glyphs[' '];

            for (int row = 0; row < 7; row++)
            {
                byte bits = rows[row];
                for (int cb = 0; cb < 5; cb++)
                {
                    if ((bits & (1 << (4 - cb))) == 0) continue;

                    int px = x + cb * scale;
                    int py = y + row * scale;

                    for (int sy = 0; sy < scale; sy++)
                    {
                        int yy = py + sy;
                        if ((uint)yy >= (uint)h) continue;

                        int baseRow = (h - 1 - yy) * w;
                        for (int sx = 0; sx < scale; sx++)
                        {
                            int xx = px + sx;
                            if ((uint)xx < (uint)w)
                                pix[baseRow + xx] = col;
                        }
                    }
                }
            }
        }
    }
}