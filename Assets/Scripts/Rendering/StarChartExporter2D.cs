using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Exports a 2D sky chart as a JPEG (zenith-centered azimuthal equidistant projection).
/// Draw order: horizon (optional) -> constellation lines (optional) -> constellation labels (optional) -> stars -> star labels (optional)
///
/// Constellation file format (whitespace, with quoted name):
///   Aql "Aquila" 8  98036 97649 97649 97278 ...
/// Or legacy (no name):
///   Aql 8  98036 97649 ...
///
/// FIXES INCLUDED:
/// 1) Constellation endpoints are projected even if below horizon (alt <= 0).
/// 2) Each constellation segment is clipped to the horizon circle before drawing (so it stops at the horizon).
/// 3) IMPORTANT: If BOTH endpoints are below horizon, the segment is NOT drawn (prevents giant chords across the chart).
/// </summary>
public class StarChartExporter2D : MonoBehaviour
{
    [Header("Dependencies")]
    public HYGCatalogParser catalog;

    [Header("Export Resolution")]
    public int width = 2048;
    public int height = 2048;
    [Range(1, 100)] public int jpegQuality = 92;

    [Header("Chart Style")]
    public bool drawHorizonCircle = true;
    [Range(1, 6)] public int horizonThicknessPx = 2;
    public Color32 horizonColor = new Color32(80, 80, 80, 255);

    [Header("Stars: magnitude -> size/brightness")]
    public float maxStarRadiusPx = 4.5f;
    public float minStarRadiusPx = 0.7f;
    public float minAlpha = 0.15f;
    public float maxAlpha = 1.00f;

    [Header("Star Labels")]
    public bool enableStarLabels = true;
    public float maxStarLabelMagnitude = 2.5f;
    [Range(1, 4)] public int starLabelFontScale = 1;
    public Color32 starLabelColor = new Color32(200, 200, 200, 255);

    [Header("Constellations")]
    public bool enableConstellationLines = true;
    public bool enableConstellationLabels = true;

    [Tooltip("Combined constellation file: Abbrev \"Name\" N hip1 hip2 ...")]
    public string constellationsFileName = "constellations_with_names.txt";

    public Color32 constellationLineColor = new Color32(80, 120, 200, 255);
    [Range(1, 4)] public int constellationLineThicknessPx = 1;

    [Tooltip("Only draw a line if BOTH endpoint stars have mag <= this value. Set to 99 to ignore.")]
    public float constellationLineMagLimit = 6.0f;

    [Header("Constellation Label Style")]
    [Range(1, 4)] public int constellationLabelFontScale = 2;
    public Color32 constellationLabelColor = new Color32(140, 170, 220, 255);

    [Tooltip("If true, we avoid label overlaps (stars + constellations) with a simple occupancy grid.")]
    public bool basicLabelCollisionAvoidance = true;

    [Header("Circle Mask")]
    [Tooltip("If true, the line rasterizer never writes pixels outside the horizon circle.")]
    public bool maskLinesToChartCircle = true;

    [Header("Output")]
    public string folderName = "StarMapExports";

    // --------- internal types ---------

    private struct LabelCandidate
    {
        public int x, y;
        public float mag;
        public string name;
    }

    private struct ConSegHip
    {
        public string conKey; // normalized abbrev key
        public int hip1;
        public int hip2;
    }

    private class ConstellationData
    {
        public string abbrev;    // e.g. Aql
        public string abbrevKey; // AQL
        public string name;      // Aquila
        public List<ConSegHip> segments = new List<ConSegHip>(32);
        public HashSet<int> uniqueHip = new HashSet<int>();
    }

    // ----------------- Main Export -----------------

    public void Export2DChartJpeg()
    {
        if (SkySession.Instance == null)
        {
            Debug.LogError("[StarChartExporter2D] SkySession missing.");
            return;
        }

        if (catalog == null || catalog.Stars == null || catalog.VisibleStarsMag6 == null)
        {
            Debug.LogError("[StarChartExporter2D] Catalog missing. Assign HYGCatalogParser in inspector.");
            return;
        }

        // ----- Time -----
        DateTimeOffset utc = AstronomyTime.LocalToUtc(SkySession.Instance.LocalDateTime);
        double jd = AstronomyTime.JulianDate(utc);
        double gmst = AstronomyTime.GreenwichMeanSiderealTimeDeg(jd);
        double lstDeg = AstronomyTime.LocalSiderealTimeDeg(gmst, SkySession.Instance.LongitudeDeg);
        double latRad = AstronomyTime.DegToRad(SkySession.Instance.LatitudeDeg);

        // ----- Image -----
        var pixels = new Color32[width * height];
        Clear(pixels, new Color32(0, 0, 0, 255));

        int cx = width / 2;
        int cy = height / 2;
        float R = Mathf.Min(cx, cy) - 6;
        float R2 = R * R;
        Vector2 chartCenter = new Vector2(cx, cy);

        if (drawHorizonCircle)
            DrawCircleOutline(pixels, width, height, cx, cy, Mathf.RoundToInt(R), horizonThicknessPx, horizonColor);

        // Occupancy map for basic label collision avoidance
        bool[] occ = basicLabelCollisionAvoidance ? new bool[width * height] : null;

        // ----- HIP lookup from HYG -----
        var hipToStar = BuildHipLookup(catalog.Stars);

        // ----- Load constellation data -----
        List<ConstellationData> constellations = null;
        if (enableConstellationLines || enableConstellationLabels)
        {
            constellations = LoadConstellationsWithNames(constellationsFileName);

            // Deterministic order
            constellations.Sort((a, b) => string.CompareOrdinal(a.abbrevKey, b.abbrevKey));
            Debug.Log($"[StarChartExporter2D] Constellations loaded: {constellations.Count} from {constellationsFileName}");
        }

        // ----- Pre-project HIP endpoints (ALLOW BELOW HORIZON) -----
        // Store pixel position even if outside circle. We clip segments to circle during drawing.
        Dictionary<int, (Vector2 pix, float mag, double altRad)> hipToPix = null;

        if ((enableConstellationLines || enableConstellationLabels) && constellations != null && constellations.Count > 0)
        {
            hipToPix = new Dictionary<int, (Vector2 pix, float mag, double altRad)>(4096);
            var needed = new HashSet<int>();

            foreach (var con in constellations)
                foreach (int hip in con.uniqueHip)
                    needed.Add(hip);

            int neededCount = needed.Count;
            int haveStar = 0;
            int projected = 0;

            foreach (int hip in needed)
            {
                if (!hipToStar.TryGetValue(hip, out StarRecord star))
                    continue;

                haveStar++;

                if (ProjectToPixelAllowBelowHorizon(star, lstDeg, latRad, cx, cy, R, out Vector2 p, out double altRad))
                {
                    float m = float.IsNaN(star.mag) ? 99f : star.mag;
                    hipToPix[hip] = (p, m, altRad);
                    projected++;
                }
            }

            Debug.Log($"[StarChartExporter2D] Constellation endpoints: needed={neededCount}, haveStar={haveStar}, projected(all altitudes)={projected}");
        }

        // ----- Draw constellation lines FIRST (CLIPPED TO HORIZON CIRCLE) -----
        int conLinesDrawn = 0;
        if (enableConstellationLines && constellations != null && hipToPix != null)
        {
            int skipMissingHip = 0, skipNoProjection = 0, skipMag = 0, skipBothBelow = 0, skipNoIntersection = 0;

            foreach (var con in constellations)
            {
                foreach (var seg in con.segments)
                {
                    if (!hipToStar.ContainsKey(seg.hip1) || !hipToStar.ContainsKey(seg.hip2))
                    {
                        skipMissingHip++;
                        continue;
                    }

                    if (!hipToPix.TryGetValue(seg.hip1, out var a) || !hipToPix.TryGetValue(seg.hip2, out var b))
                    {
                        skipNoProjection++;
                        continue;
                    }

                    if (a.mag > constellationLineMagLimit || b.mag > constellationLineMagLimit)
                    {
                        skipMag++;
                        continue;
                    }

                    // IMPORTANT: if BOTH endpoints are below horizon, don't draw this segment.
                    // This prevents long "chords" across the chart caused by two below-horizon points
                    // whose segment passes through the circle.
                    if (a.altRad <= 0.0 && b.altRad <= 0.0)
                    {
                        skipBothBelow++;
                        continue;
                    }

                    // Clip segment to the horizon circle: draw only the visible portion inside R.
                    if (!ClipSegmentToCircle(a.pix, b.pix, chartCenter, R, out Vector2 c0, out Vector2 c1))
                    {
                        skipNoIntersection++;
                        continue;
                    }

                    int x0 = Mathf.RoundToInt(c0.x);
                    int y0 = Mathf.RoundToInt(c0.y);
                    int x1 = Mathf.RoundToInt(c1.x);
                    int y1 = Mathf.RoundToInt(c1.y);

                    if (maskLinesToChartCircle)
                    {
                        bool drew = DrawLineThickMaskedToCircle(
                            pixels, width, height,
                            x0, y0, x1, y1,
                            constellationLineColor, constellationLineThicknessPx,
                            cx, cy, R2
                        );
                        if (drew) conLinesDrawn++;
                    }
                    else
                    {
                        DrawLineThick(pixels, width, height, x0, y0, x1, y1, constellationLineColor, constellationLineThicknessPx);
                        conLinesDrawn++;
                    }
                }
            }

            Debug.Log($"[StarChartExporter2D] Constellation lines: drawn={conLinesDrawn} skipMissingHip={skipMissingHip} skipNoProjection={skipNoProjection} skipMag={skipMag} skipBothBelow={skipBothBelow} skipNoIntersection={skipNoIntersection}");
        }

        // ----- Constellation labels (centroid of projected HIP points that are INSIDE horizon circle) -----
        int conLabelsDrawn = 0;
        if (enableConstellationLabels && constellations != null && hipToPix != null)
        {
            foreach (var con in constellations)
            {
                string label = string.IsNullOrWhiteSpace(con.name) ? con.abbrev : con.name;
                label = SanitizeLabel(label);
                if (string.IsNullOrEmpty(label)) continue;

                int count = 0;
                double sumX = 0, sumY = 0;

                foreach (int hip in con.uniqueHip)
                {
                    if (!hipToPix.TryGetValue(hip, out var p)) continue;

                    float dx = p.pix.x - cx;
                    float dy = p.pix.y - cy;
                    if (dx * dx + dy * dy > R2) continue;

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

                int textW = BitmapFont5x7.MeasureWidth(label, constellationLabelFontScale);
                int textH = BitmapFont5x7.MeasureHeight(constellationLabelFontScale);

                int tx = lx - (textW / 2);
                int ty = ly - (textH / 2);

                tx = Mathf.Clamp(tx, 0, width - textW - 1);
                ty = Mathf.Clamp(ty, 0, height - textH - 1);

                if (basicLabelCollisionAvoidance && RectAnyOccupied(occ, width, height, tx, ty, textW, textH))
                    continue;

                BitmapFont5x7.DrawText(pixels, width, height, tx, ty, label, constellationLabelFontScale, constellationLabelColor);

                if (basicLabelCollisionAvoidance)
                    MarkRectOccupied(occ, width, height, tx, ty, textW, textH);

                conLabelsDrawn++;
            }

            Debug.Log($"[StarChartExporter2D] Constellation labels drawn={conLabelsDrawn}");
        }

        // ----- Draw stars -----
        int starsDrawn = 0;
        var starLabelCandidates = enableStarLabels ? new List<LabelCandidate>(512) : null;

        foreach (var star in catalog.VisibleStarsMag6)
        {
            if (float.IsNaN(star.ra) || float.IsNaN(star.dec) || float.IsNaN(star.mag))
                continue;

            if (!TryProjectToPixelAboveHorizonOnly(star, lstDeg, latRad, cx, cy, R, out int x, out int y, out _))
                continue;

            float t = Mathf.InverseLerp(0f, catalog.magnitudeLimit, star.mag);
            float curved = Mathf.Pow(t, 1.7f);

            float radius = Mathf.Lerp(maxStarRadiusPx, minStarRadiusPx, curved);
            float alpha = Mathf.Lerp(maxAlpha, minAlpha, Mathf.Pow(t, 1.3f));

            DrawSoftDot(pixels, width, height, x, y, radius, alpha);
            starsDrawn++;

            if (enableStarLabels &&
                !string.IsNullOrWhiteSpace(star.proper) &&
                star.mag <= maxStarLabelMagnitude)
            {
                starLabelCandidates.Add(new LabelCandidate
                {
                    x = x,
                    y = y,
                    mag = star.mag,
                    name = star.proper.Trim()
                });
            }
        }

        // ----- Star labels -----
        int starLabelsDrawn = 0;
        if (enableStarLabels && starLabelCandidates != null && starLabelCandidates.Count > 0)
        {
            starLabelCandidates.Sort((a, b) =>
            {
                int m = a.mag.CompareTo(b.mag);
                if (m != 0) return m;
                return string.CompareOrdinal(a.name, b.name);
            });

            foreach (var lc in starLabelCandidates)
            {
                string text = SanitizeLabel(lc.name);
                if (string.IsNullOrEmpty(text)) continue;

                int textW = BitmapFont5x7.MeasureWidth(text, starLabelFontScale);
                int textH = BitmapFont5x7.MeasureHeight(starLabelFontScale);

                Vector2Int[] offsets =
                {
                    new Vector2Int(  6,  6),
                    new Vector2Int( -6 - textW,  6),
                    new Vector2Int(  6, -6 - textH),
                    new Vector2Int( -6 - textW, -6 - textH),
                    new Vector2Int(  8,  0),
                    new Vector2Int( -8 - textW, 0),
                };

                foreach (var off in offsets)
                {
                    int tx = lc.x + off.x;
                    int ty = lc.y + off.y;

                    if (tx < 0 || ty < 0 || tx + textW >= width || ty + textH >= height)
                        continue;

                    if (basicLabelCollisionAvoidance && RectAnyOccupied(occ, width, height, tx, ty, textW, textH))
                        continue;

                    BitmapFont5x7.DrawText(pixels, width, height, tx, ty, text, starLabelFontScale, starLabelColor);

                    if (basicLabelCollisionAvoidance)
                        MarkRectOccupied(occ, width, height, tx, ty, textW, textH);

                    starLabelsDrawn++;
                    break;
                }
            }
        }

        // Flip for Texture2D bottom-left origin
        FlipVerticalInPlace(pixels, width, height);

        // ----- Save JPG -----
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        byte[] jpg = tex.EncodeToJPG(jpegQuality);
        Destroy(tex);

        string dir = Path.Combine(Application.persistentDataPath, folderName);
        Directory.CreateDirectory(dir);

        string dtStr = SkySession.Instance.LocalDateTime.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
        string file = Path.Combine(dir, $"StarChart2D_{dtStr}_lat{SkySession.Instance.LatitudeDeg:F4}_lon{SkySession.Instance.LongitudeDeg:F4}.jpg");

        File.WriteAllBytes(file, jpg);

        Debug.Log($"[StarChartExporter2D] Saved: {file} | stars={starsDrawn} starLabels={starLabelsDrawn} conLines={conLinesDrawn} conLabels={conLabelsDrawn}");
    }

    // ----------------- Constellation Loader (abbr "name" N hip hip ...) -----------------

    private static List<ConstellationData> LoadConstellationsWithNames(string fileName)
    {
        var list = new List<ConstellationData>(88);
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[StarChartExporter2D] Constellation file NOT FOUND at: {path}\n" +
                           $"Make sure the file is in Assets/StreamingAssets/{fileName}");
            return list;
        }

        var byKey = new Dictionary<string, ConstellationData>(88);

        string[] lines = File.ReadAllLines(path);

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            if (string.IsNullOrWhiteSpace(raw)) continue;

            string line = raw.Trim();
            if (line.StartsWith("#")) continue;

            int hash = line.IndexOf('#');
            if (hash >= 0) line = line.Substring(0, hash).Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Tokenize with quotes preserved as a single token (quotes removed)
            List<string> tokens = TokenizeWhitespaceWithQuotes(line);
            if (tokens.Count < 3) continue;

            string abbrev = tokens[0].Trim();
            if (string.IsNullOrWhiteSpace(abbrev)) continue;

            string abbrevKey = abbrev.Trim().ToUpperInvariant();

            int idx = 1;
            string name = "";

            // If tokens[1] is not an int, treat it as the name
            if (!int.TryParse(tokens[idx], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                name = tokens[idx].Trim();
                idx++;
            }

            if (idx >= tokens.Count) continue;

            // tokens[idx] should be the count (ignore its value)
            int.TryParse(tokens[idx], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            idx++;

            if (idx >= tokens.Count) continue;

            // Remaining tokens are HIP ids
            var ids = new List<int>(tokens.Count - idx);
            for (int t = idx; t < tokens.Count; t++)
            {
                if (int.TryParse(tokens[t], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hip) && hip > 0)
                    ids.Add(hip);
            }

            if (ids.Count < 2) continue;

            if (!byKey.TryGetValue(abbrevKey, out var con))
            {
                con = new ConstellationData
                {
                    abbrev = abbrev,
                    abbrevKey = abbrevKey,
                    name = name
                };
                byKey[abbrevKey] = con;
                list.Add(con);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(con.name) && !string.IsNullOrWhiteSpace(name))
                    con.name = name;
            }

            // Pairs -> segments
            for (int k = 0; k + 1 < ids.Count; k += 2)
            {
                int a = ids[k];
                int b = ids[k + 1];
                if (a <= 0 || b <= 0) continue;

                con.segments.Add(new ConSegHip { conKey = abbrevKey, hip1 = a, hip2 = b });
                con.uniqueHip.Add(a);
                con.uniqueHip.Add(b);
            }
        }

        return list;
    }

    /// <summary>
    /// Splits by whitespace but preserves quoted substrings as one token.
    /// Quotes are removed.
    /// Example: Aql "Ursa Major" 8  123 456 -> tokens: [Aql, Ursa Major, 8, 123, 456]
    /// </summary>
    private static List<string> TokenizeWhitespaceWithQuotes(string line)
    {
        var tokens = new List<string>(64);
        bool inQuotes = false;
        var cur = new System.Text.StringBuilder(32);

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (cur.Length > 0)
                {
                    tokens.Add(cur.ToString());
                    cur.Clear();
                }
                continue;
            }

            cur.Append(c);
        }

        if (cur.Length > 0)
            tokens.Add(cur.ToString());

        return tokens;
    }

    // ----------------- Lookups -----------------

    private static Dictionary<int, StarRecord> BuildHipLookup(List<StarRecord> allStars)
    {
        var dict = new Dictionary<int, StarRecord>(allStars.Count / 3);
        foreach (var s in allStars)
        {
            if (s == null) continue;
            if (s.hip > 0 && !dict.ContainsKey(s.hip))
                dict[s.hip] = s;
        }
        return dict;
    }

    // ----------------- Projection -----------------

    private static bool TryProjectToPixelAboveHorizonOnly(StarRecord star, double lstDeg, double latRad, int cx, int cy, float R,
        out int x, out int y, out float altDeg)
    {
        x = 0; y = 0; altDeg = 0;

        double raDeg = star.ra * 15.0;
        double haDeg = AstronomyTime.HourAngleDeg(lstDeg, raDeg);
        double haRad = AstronomyTime.DegToRad(haDeg);
        double decRad = AstronomyTime.DegToRad(star.dec);

        double sinAlt =
            Math.Sin(decRad) * Math.Sin(latRad) +
            Math.Cos(decRad) * Math.Cos(latRad) * Math.Cos(haRad);

        sinAlt = Math.Clamp(sinAlt, -1.0, 1.0);
        double altRad = Math.Asin(sinAlt);
        altDeg = (float)AstronomyTime.RadToDeg(altRad);
        if (altRad <= 0) return false;

        double sinHA = Math.Sin(haRad);
        double cosHA = Math.Cos(haRad);
        double tanDec = Math.Tan(decRad);

        double azSouth = Math.Atan2(
            sinHA,
            (cosHA * Math.Sin(latRad)) - (tanDec * Math.Cos(latRad))
        );

        double azRad = azSouth + Math.PI;
        azRad %= (2.0 * Math.PI);
        if (azRad < 0) azRad += 2.0 * Math.PI;

        float r01 = (float)((Math.PI / 2.0 - altRad) / (Math.PI / 2.0));
        float pr = r01 * R;

        x = cx + Mathf.RoundToInt(pr * Mathf.Sin((float)azRad));
        y = cy + Mathf.RoundToInt(pr * Mathf.Cos((float)azRad));

        float dx = x - cx;
        float dy = y - cy;
        if (dx * dx + dy * dy > R * R) return false;

        return true;
    }

    private static bool ProjectToPixelAllowBelowHorizon(StarRecord star, double lstDeg, double latRad, int cx, int cy, float R,
        out Vector2 pix, out double altRad)
    {
        pix = default;
        altRad = 0;

        if (float.IsNaN(star.ra) || float.IsNaN(star.dec)) return false;

        double raDeg = star.ra * 15.0;
        double haDeg = AstronomyTime.HourAngleDeg(lstDeg, raDeg);
        double haRad = AstronomyTime.DegToRad(haDeg);
        double decRad = AstronomyTime.DegToRad(star.dec);

        double sinAlt =
            Math.Sin(decRad) * Math.Sin(latRad) +
            Math.Cos(decRad) * Math.Cos(latRad) * Math.Cos(haRad);

        sinAlt = Math.Clamp(sinAlt, -1.0, 1.0);
        altRad = Math.Asin(sinAlt);

        double sinHA = Math.Sin(haRad);
        double cosHA = Math.Cos(haRad);
        double tanDec = Math.Tan(decRad);

        double azSouth = Math.Atan2(
            sinHA,
            (cosHA * Math.Sin(latRad)) - (tanDec * Math.Cos(latRad))
        );

        double azRad = azSouth + Math.PI;
        azRad %= (2.0 * Math.PI);
        if (azRad < 0) azRad += 2.0 * Math.PI;

        float r01 = (float)((Math.PI / 2.0 - altRad) / (Math.PI / 2.0));
        float pr = r01 * R;

        float x = cx + pr * Mathf.Sin((float)azRad);
        float y = cy + pr * Mathf.Cos((float)azRad);

        pix = new Vector2(x, y);
        return true;
    }

    // ----------------- Segment clipping to horizon circle -----------------

    private static bool ClipSegmentToCircle(Vector2 a, Vector2 b, Vector2 c, float R, out Vector2 outA, out Vector2 outB)
    {
        outA = default;
        outB = default;

        Vector2 A = a - c;
        Vector2 B = b - c;
        Vector2 d = B - A;

        float r2 = R * R;

        bool aIn = Vector2.Dot(A, A) <= r2;
        bool bIn = Vector2.Dot(B, B) <= r2;

        if (aIn && bIn)
        {
            outA = a;
            outB = b;
            return true;
        }

        float Acoef = Vector2.Dot(d, d);
        if (Acoef < 1e-12f) return false;

        float Bcoef = 2f * Vector2.Dot(A, d);
        float Ccoef = Vector2.Dot(A, A) - r2;

        float disc = Bcoef * Bcoef - 4f * Acoef * Ccoef;
        if (disc < 0f) return false;

        float sqrt = Mathf.Sqrt(disc);
        float t0 = (-Bcoef - sqrt) / (2f * Acoef);
        float t1 = (-Bcoef + sqrt) / (2f * Acoef);

        if (t0 > t1) (t0, t1) = (t1, t0);

        float enter = Mathf.Clamp01(t0);
        float exit = Mathf.Clamp01(t1);

        if (exit <= enter)
        {
            if (aIn)
            {
                float t = (t0 >= 0f && t0 <= 1f) ? t0 : ((t1 >= 0f && t1 <= 1f) ? t1 : -1f);
                if (t < 0f) return false;
                outA = a;
                outB = c + (A + t * d);
                return true;
            }

            if (bIn)
            {
                float t = (t0 >= 0f && t0 <= 1f) ? t0 : ((t1 >= 0f && t1 <= 1f) ? t1 : -1f);
                if (t < 0f) return false;
                outA = c + (A + t * d);
                outB = b;
                return true;
            }

            return false;
        }

        Vector2 Penter = c + (A + enter * d);
        Vector2 Pexit = c + (A + exit * d);

        if (aIn && !bIn) { outA = a; outB = Pexit; return true; }
        if (!aIn && bIn) { outA = Penter; outB = b; return true; }

        outA = Penter;
        outB = Pexit;
        return true;
    }

    // ----------------- Drawing -----------------

    private static void Clear(Color32[] pix, Color32 c)
    {
        for (int i = 0; i < pix.Length; i++) pix[i] = c;
    }

    private static void DrawCircleOutline(Color32[] pix, int w, int h, int cx, int cy, int r, int thickness, Color32 col)
    {
        int rOuter = r;
        int rInner = Mathf.Max(0, r - thickness);

        int rOuter2 = rOuter * rOuter;
        int rInner2 = rInner * rInner;

        int minX = Mathf.Max(0, cx - rOuter);
        int maxX = Mathf.Min(w - 1, cx + rOuter);
        int minY = Mathf.Max(0, cy - rOuter);
        int maxY = Mathf.Min(h - 1, cy + rOuter);

        for (int yy = minY; yy <= maxY; yy++)
        {
            int dy = yy - cy;
            int dy2 = dy * dy;
            int row = yy * w;

            for (int xx = minX; xx <= maxX; xx++)
            {
                int dx = xx - cx;
                int d2 = dx * dx + dy2;
                if (d2 <= rOuter2 && d2 >= rInner2)
                    pix[row + xx] = col;
            }
        }
    }

    private static void DrawSoftDot(Color32[] pix, int w, int h, int cx, int cy, float radius, float alpha)
    {
        int r = Mathf.CeilToInt(radius);
        int minX = Mathf.Max(0, cx - r);
        int maxX = Mathf.Min(w - 1, cx + r);
        int minY = Mathf.Max(0, cy - r);
        int maxY = Mathf.Min(h - 1, cy + r);

        float r2 = radius * radius;
        float invR = (radius > 0.0001f) ? (1f / radius) : 0f;

        for (int yy = minY; yy <= maxY; yy++)
        {
            float dy = yy - cy;
            int row = yy * w;
            for (int xx = minX; xx <= maxX; xx++)
            {
                float dx = xx - cx;
                float d2 = dx * dx + dy * dy;
                if (d2 > r2) continue;

                float d = Mathf.Sqrt(d2);
                float falloff = 1f - (d * invR);
                float a = alpha * falloff * falloff;

                AdditiveBlendWhite(ref pix[row + xx], a);
            }
        }
    }

    private static void AdditiveBlendWhite(ref Color32 dst, float a)
    {
        byte add = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * a), 0, 255);

        int rr = dst.r + add;
        int gg = dst.g + add;
        int bb = dst.b + add;

        dst.r = (byte)Mathf.Min(255, rr);
        dst.g = (byte)Mathf.Min(255, gg);
        dst.b = (byte)Mathf.Min(255, bb);
        dst.a = 255;
    }

    private static void DrawLineThick(Color32[] pix, int w, int h, int x0, int y0, int x1, int y1, Color32 col, int thickness)
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
        bool wroteAny = false;

        while (true)
        {
            wroteAny |= DrawSolidDotMaskedToCircle(pix, w, h, x0, y0, r, col, chartCx, chartCy, chartR2);

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }

        return wroteAny;
    }

    private static void DrawSolidDot(Color32[] pix, int w, int h, int cx, int cy, int radius, Color32 col)
    {
        int minX = Mathf.Max(0, cx - radius);
        int maxX = Mathf.Min(w - 1, cx + radius);
        int minY = Mathf.Max(0, cy - radius);
        int maxY = Mathf.Min(h - 1, cy + radius);

        int r2 = radius * radius;

        for (int yy = minY; yy <= maxY; yy++)
        {
            int dy = yy - cy;
            int dy2 = dy * dy;
            int row = yy * w;

            for (int xx = minX; xx <= maxX; xx++)
            {
                int dx = xx - cx;
                if (dx * dx + dy2 <= r2)
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
            int dyDot = yy - py;
            int dyDot2 = dyDot * dyDot;
            int row = yy * w;

            int dyChart = yy - chartCy;

            for (int xx = minX; xx <= maxX; xx++)
            {
                int dxDot = xx - px;
                if (dxDot * dxDot + dyDot2 > dotR2)
                    continue;

                int dxChart = xx - chartCx;
                if (dxChart * dxChart + dyChart * dyChart > chartR2)
                    continue;

                pix[row + xx] = col;
                wrote = true;
            }
        }

        return wrote;
    }

    private static void FlipVerticalInPlace(Color32[] pix, int w, int h)
    {
        int half = h / 2;
        for (int y = 0; y < half; y++)
        {
            int y2 = h - 1 - y;
            int row1 = y * w;
            int row2 = y2 * w;

            for (int x = 0; x < w; x++)
            {
                int i1 = row1 + x;
                int i2 = row2 + x;
                (pix[i1], pix[i2]) = (pix[i2], pix[i1]);
            }
        }
    }

    // ----------------- Label collision helpers -----------------

    private static bool RectAnyOccupied(bool[] occ, int w, int h, int x, int y, int rw, int rh)
    {
        if (occ == null) return false;
        int x2 = Mathf.Min(w - 1, x + rw);
        int y2 = Mathf.Min(h - 1, y + rh);

        for (int yy = y; yy <= y2; yy++)
        {
            int row = yy * w;
            for (int xx = x; xx <= x2; xx++)
                if (occ[row + xx]) return true;
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

    private static string SanitizeLabel(string s)
    {
        s = s.Trim().ToUpperInvariant();
        char[] arr = s.ToCharArray();

        for (int i = 0; i < arr.Length; i++)
        {
            char c = arr[i];
            bool ok =
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == ' ' || c == '-' || c == '.' || c == '\'';

            if (!ok) arr[i] = ' ';
        }

        string cleaned = new string(arr);
        while (cleaned.Contains("  ")) cleaned = cleaned.Replace("  ", " ");
        return cleaned.Trim();
    }

    // ----------------- Tiny 5x7 bitmap font -----------------
    private static class BitmapFont5x7
    {
        private static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
        {
            { ' ', new byte[] { 0,0,0,0,0,0,0 } },
            { '0', new byte[] { 0x1E,0x11,0x13,0x15,0x19,0x11,0x1E } },
            { '1', new byte[] { 0x04,0x0C,0x04,0x04,0x04,0x04,0x0E } },
            { '2', new byte[] { 0x1E,0x01,0x01,0x1E,0x10,0x10,0x1F } },
            { '3', new byte[] { 0x1E,0x01,0x01,0x0E,0x01,0x01,0x1E } },
            { '4', new byte[] { 0x02,0x06,0x0A,0x12,0x1F,0x02,0x02 } },
            { '5', new byte[] { 0x1F,0x10,0x10,0x1E,0x01,0x01,0x1E } },
            { '6', new byte[] { 0x0E,0x10,0x10,0x1E,0x11,0x11,0x0E } },
            { '7', new byte[] { 0x1F,0x01,0x02,0x04,0x08,0x08,0x08 } },
            { '8', new byte[] { 0x0E,0x11,0x11,0x0E,0x11,0x11,0x0E } },
            { '9', new byte[] { 0x0E,0x11,0x11,0x0F,0x01,0x01,0x0E } },
            { '-', new byte[] { 0x00,0x00,0x00,0x1F,0x00,0x00,0x00 } },
            { '.', new byte[] { 0x00,0x00,0x00,0x00,0x00,0x0C,0x0C } },
            { '\'', new byte[] { 0x04,0x04,0x02,0x00,0x00,0x00,0x00 } },

            { 'A', new byte[] { 0x0E,0x11,0x11,0x1F,0x11,0x11,0x11 } },
            { 'B', new byte[] { 0x1E,0x11,0x11,0x1E,0x11,0x11,0x1E } },
            { 'C', new byte[] { 0x0E,0x11,0x10,0x10,0x10,0x11,0x0E } },
            { 'D', new byte[] { 0x1C,0x12,0x11,0x11,0x11,0x12,0x1C } },
            { 'E', new byte[] { 0x1F,0x10,0x10,0x1E,0x10,0x10,0x1F } },
            { 'F', new byte[] { 0x1F,0x10,0x10,0x1E,0x10,0x10,0x10 } },
            { 'G', new byte[] { 0x0E,0x11,0x10,0x17,0x11,0x11,0x0E } },
            { 'H', new byte[] { 0x11,0x11,0x11,0x1F,0x11,0x11,0x11 } },
            { 'I', new byte[] { 0x0E,0x04,0x04,0x04,0x04,0x04,0x0E } },
            { 'J', new byte[] { 0x07,0x02,0x02,0x02,0x02,0x12,0x0C } },
            { 'K', new byte[] { 0x11,0x12,0x14,0x18,0x14,0x12,0x11 } },
            { 'L', new byte[] { 0x10,0x10,0x10,0x10,0x10,0x10,0x1F } },
            { 'M', new byte[] { 0x11,0x1B,0x15,0x11,0x11,0x11,0x11 } },
            { 'N', new byte[] { 0x11,0x19,0x15,0x13,0x11,0x11,0x11 } },
            { 'O', new byte[] { 0x0E,0x11,0x11,0x11,0x11,0x11,0x0E } },
            { 'P', new byte[] { 0x1E,0x11,0x11,0x1E,0x10,0x10,0x10 } },
            { 'Q', new byte[] { 0x0E,0x11,0x11,0x11,0x15,0x12,0x0D } },
            { 'R', new byte[] { 0x1E,0x11,0x11,0x1E,0x14,0x12,0x11 } },
            { 'S', new byte[] { 0x0F,0x10,0x10,0x0E,0x01,0x01,0x1E } },
            { 'T', new byte[] { 0x1F,0x04,0x04,0x04,0x04,0x04,0x04 } },
            { 'U', new byte[] { 0x11,0x11,0x11,0x11,0x11,0x11,0x0E } },
            { 'V', new byte[] { 0x11,0x11,0x11,0x11,0x11,0x0A,0x04 } },
            { 'W', new byte[] { 0x11,0x11,0x11,0x11,0x15,0x1B,0x11 } },
            { 'X', new byte[] { 0x11,0x11,0x0A,0x04,0x0A,0x11,0x11 } },
            { 'Y', new byte[] { 0x11,0x11,0x0A,0x04,0x04,0x04,0x04 } },
            { 'Z', new byte[] { 0x1F,0x01,0x02,0x04,0x08,0x10,0x1F } },
        };

        public static int MeasureWidth(string text, int scale) => text.Length * (6 * scale);
        public static int MeasureHeight(int scale) => 7 * scale;

        public static void DrawText(Color32[] pix, int w, int h, int x, int y, string text, int scale, Color32 col)
        {
            int penX = x;
            for (int i = 0; i < text.Length; i++)
            {
                DrawChar(pix, w, h, penX, y, text[i], scale, col);
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
                for (int colBit = 0; colBit < 5; colBit++)
                {
                    bool on = (bits & (1 << (4 - colBit))) != 0;
                    if (!on) continue;

                    int px = x + colBit * scale;
                    int py = y + row * scale;

                    for (int sy = 0; sy < scale; sy++)
                    {
                        int yy = py + sy;
                        if ((uint)yy >= (uint)h) continue;
                        int baseRow = yy * w;

                        for (int sx = 0; sx < scale; sx++)
                        {
                            int xx = px + sx;
                            if ((uint)xx >= (uint)w) continue;
                            pix[baseRow + xx] = col;
                        }
                    }
                }
            }
        }
    }
}