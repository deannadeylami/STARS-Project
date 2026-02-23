using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Exports a 2D sky chart as a JPEG (zenith-centered azimuthal equidistant projection).
/// Draw order: horizon (optional) -> constellation lines (optional) -> stars -> labels (optional)
///
/// Constellation CSV (StreamingAssets):
/// con,hd1,hd2
/// ARI,17573,12929
/// ...
/// Lines starting with # are ignored. Inline # comments are allowed.
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
    public float maxStarRadiusPx = 4.5f; // brightest
    public float minStarRadiusPx = 0.7f; // dimmest
    public float minAlpha = 0.15f;       // dimmest stars
    public float maxAlpha = 1.00f;       // brightest stars

    [Header("Labels")]
    public bool enableLabels = true;
    public float maxLabelMagnitude = 2.5f;
    [Range(1, 4)] public int labelFontScale = 1;
    public Color32 labelColor = new Color32(200, 200, 200, 255);
    public bool basicLabelCollisionAvoidance = true;

    [Header("Constellations")]
    public bool enableConstellationLines = true;
    public string constellationLinesCsvFileName = "constellation_lines_hd.csv";
    public Color32 constellationLineColor = new Color32(80, 120, 200, 255);
    [Range(1, 4)] public int constellationLineThicknessPx = 1;

    [Tooltip("Only draw a line if BOTH endpoint stars have mag <= this value. Set to 99 to ignore magnitude filtering.")]
    public float constellationLineMagLimit = 6.0f;

    [Tooltip("Safety: skip drawing a segment if its pixel length exceeds this fraction of chart radius.")]
    [Range(0.1f, 2.0f)] public float maxConstellationLineLengthFracOfRadius = 0.65f;

    [Header("Output")]
    public string folderName = "StarMapExports";

    private struct LabelCandidate
    {
        public int x, y;
        public float mag;
        public string name;
    }

    private struct ConSegHd
    {
        public string con;
        public int hd1, hd2;
    }

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

        if (drawHorizonCircle)
            DrawCircleOutline(pixels, width, height, cx, cy, Mathf.RoundToInt(R), horizonThicknessPx, horizonColor);

        // ----- Build HD lookup from HYG -----
        var hdToStar = BuildHdLookup(catalog.Stars);

        // ----- Load constellation segments (HD-based) -----
        List<ConSegHd> segs = null;
        if (enableConstellationLines)
        {
            segs = LoadConstellationSegmentsHd(constellationLinesCsvFileName);
            Debug.Log($"[StarChartExporter2D] Constellation HD segments loaded: {segs.Count} from {constellationLinesCsvFileName}");
        }

        // ----- Precompute projected endpoints for constellation segments -----
        Dictionary<int, (int x, int y, float mag)> hdToPix = null;

        int endpointsNeeded = 0;
        int endpointsHaveStar = 0;
        int endpointsProjected = 0;

        if (enableConstellationLines && segs != null && segs.Count > 0)
        {
            hdToPix = new Dictionary<int, (int x, int y, float mag)>(4096);
            var needed = new HashSet<int>();

            foreach (var s in segs)
            {
                needed.Add(s.hd1);
                needed.Add(s.hd2);
            }

            endpointsNeeded = needed.Count;

            foreach (int hd in needed)
            {
                if (!hdToStar.TryGetValue(hd, out StarRecord star))
                    continue;

                endpointsHaveStar++;

                if (TryProjectToPixel(star, lstDeg, latRad, cx, cy, R, out int px, out int py, out _))
                {
                    float m = float.IsNaN(star.mag) ? 99f : star.mag;
                    hdToPix[hd] = (px, py, m);
                    endpointsProjected++;
                }
            }

            Debug.Log($"[StarChartExporter2D] Constellation endpoints: needed={endpointsNeeded}, haveStar={endpointsHaveStar}, projected(above horizon)={endpointsProjected}");
        }

        // ----- Draw constellation lines FIRST -----
        int linesDrawn = 0;
        int skipMissingHd = 0;
        int skipNotProjected = 0;
        int skipMag = 0;
        int skipTooLong = 0;

        if (enableConstellationLines && segs != null && hdToPix != null)
        {
            float maxLen = maxConstellationLineLengthFracOfRadius * R;
            float maxLen2 = maxLen * maxLen;

            foreach (var s in segs)
            {
                if (!hdToStar.ContainsKey(s.hd1) || !hdToStar.ContainsKey(s.hd2))
                {
                    skipMissingHd++;
                    continue;
                }

                if (!hdToPix.TryGetValue(s.hd1, out var a) || !hdToPix.TryGetValue(s.hd2, out var b))
                {
                    // usually means one or both endpoints are below the horizon for the chosen inputs
                    skipNotProjected++;
                    continue;
                }

                if (a.mag > constellationLineMagLimit || b.mag > constellationLineMagLimit)
                {
                    skipMag++;
                    continue;
                }

                float dx = a.x - b.x;
                float dy = a.y - b.y;
                float d2 = dx * dx + dy * dy;

                if (d2 > maxLen2)
                {
                    skipTooLong++;
                    continue;
                }

                DrawLineThick(pixels, width, height, a.x, a.y, b.x, b.y, constellationLineColor, constellationLineThicknessPx);
                linesDrawn++;
            }

            Debug.Log($"[StarChartExporter2D] Constellation lines: drawn={linesDrawn} skipMissingHd={skipMissingHd} skipNotProjected={skipNotProjected} skipMag={skipMag} skipTooLong={skipTooLong}");
        }

        // ----- Draw stars -----
        var labelCandidates = enableLabels ? new List<LabelCandidate>(512) : null;
        bool[] labelOcc = basicLabelCollisionAvoidance ? new bool[width * height] : null;

        int starsDrawn = 0;

        foreach (var star in catalog.VisibleStarsMag6)
        {
            if (float.IsNaN(star.ra) || float.IsNaN(star.dec) || float.IsNaN(star.mag))
                continue;

            if (!TryProjectToPixel(star, lstDeg, latRad, cx, cy, R, out int x, out int y, out _))
                continue;

            float t = Mathf.InverseLerp(0f, catalog.magnitudeLimit, star.mag);
            float curved = Mathf.Pow(t, 1.7f);

            float radius = Mathf.Lerp(maxStarRadiusPx, minStarRadiusPx, curved);
            float alpha = Mathf.Lerp(maxAlpha, minAlpha, Mathf.Pow(t, 1.3f));

            DrawSoftDot(pixels, width, height, x, y, radius, alpha);
            starsDrawn++;

            if (enableLabels &&
                !string.IsNullOrWhiteSpace(star.proper) &&
                star.mag <= maxLabelMagnitude)
            {
                labelCandidates.Add(new LabelCandidate
                {
                    x = x,
                    y = y,
                    mag = star.mag,
                    name = star.proper.Trim()
                });
            }
        }

        // ----- Draw labels -----
        int labelsDrawn = 0;
        if (enableLabels && labelCandidates != null && labelCandidates.Count > 0)
        {
            labelCandidates.Sort((a, b) =>
            {
                int m = a.mag.CompareTo(b.mag);
                if (m != 0) return m;
                return string.CompareOrdinal(a.name, b.name);
            });

            foreach (var lc in labelCandidates)
            {
                string text = SanitizeLabel(lc.name);
                if (string.IsNullOrEmpty(text)) continue;

                int textW = BitmapFont5x7.MeasureWidth(text, labelFontScale);
                int textH = BitmapFont5x7.MeasureHeight(labelFontScale);

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

                    if (basicLabelCollisionAvoidance &&
                        RectAnyOccupied(labelOcc, width, height, tx, ty, textW, textH))
                        continue;

                    BitmapFont5x7.DrawText(pixels, width, height, tx, ty, text, labelFontScale, labelColor);

                    if (basicLabelCollisionAvoidance)
                        MarkRectOccupied(labelOcc, width, height, tx, ty, textW, textH);

                    labelsDrawn++;
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

        Debug.Log($"[StarChartExporter2D] Saved: {file} | stars={starsDrawn} labels={labelsDrawn} constLines={linesDrawn}");
    }

    // ----------------- CSV loading (HD-based) -----------------

    private static List<ConSegHd> LoadConstellationSegmentsHd(string fileName)
    {
        var list = new List<ConSegHd>(4096);
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[StarChartExporter2D] Constellation CSV NOT FOUND at: {path}\n" +
                           $"Make sure the file is in Assets/StreamingAssets/{fileName}");
            return list;
        }

        string[] lines = File.ReadAllLines(path);

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            if (string.IsNullOrWhiteSpace(raw)) continue;

            string line = raw.Trim();

            // whole-line comment
            if (line.StartsWith("#")) continue;

            // header
            if (i == 0 && line.StartsWith("con", StringComparison.OrdinalIgnoreCase))
                continue;

            // strip inline comments
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line.Substring(0, hash).Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] p = line.Split(',');
            if (p.Length < 3) continue;

            string con = p[0].Trim();
            if (!int.TryParse(p[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hd1)) continue;
            if (!int.TryParse(p[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hd2)) continue;

            if (hd1 <= 0 || hd2 <= 0) continue;

            list.Add(new ConSegHd { con = con, hd1 = hd1, hd2 = hd2 });
        }

        return list;
    }

    private static Dictionary<int, StarRecord> BuildHdLookup(List<StarRecord> allStars)
    {
        var dict = new Dictionary<int, StarRecord>(allStars.Count / 3);
        foreach (var s in allStars)
        {
            if (s == null) continue;
            if (s.hd > 0 && !dict.ContainsKey(s.hd))
                dict[s.hd] = s;
        }
        return dict;
    }

    // ----------------- Projection -----------------
    // Zenith-centered azimuthal equidistant projection:
    // r = (pi/2 - alt) / (pi/2) * R
    // x = cx + r*sin(az), y = cy + r*cos(az)
    private static bool TryProjectToPixel(StarRecord star, double lstDeg, double latRad, int cx, int cy, float R,
        out int x, out int y, out float altDeg)
    {
        x = 0; y = 0; altDeg = 0;

        double raDeg = star.ra * 15.0;
        double haDeg = AstronomyTime.HourAngleDeg(lstDeg, raDeg);
        double haRad = AstronomyTime.DegToRad(haDeg);
        double decRad = AstronomyTime.DegToRad(star.dec);

        // altitude
        double sinAlt =
            Math.Sin(decRad) * Math.Sin(latRad) +
            Math.Cos(decRad) * Math.Cos(latRad) * Math.Cos(haRad);

        sinAlt = Math.Clamp(sinAlt, -1.0, 1.0);
        double altRad = Math.Asin(sinAlt);
        altDeg = (float)AstronomyTime.RadToDeg(altRad);
        if (altRad <= 0) return false;

        // azimuth (robust atan2 form to behave better near poles)
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

        // inside circle
        float dx = x - cx;
        float dy = y - cy;
        if (dx * dx + dy * dy > R * R) return false;

        return (uint)x < (uint)Int32.MaxValue && (uint)y < (uint)Int32.MaxValue;
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