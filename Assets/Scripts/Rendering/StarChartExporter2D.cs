using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class StarChartExporter2D : MonoBehaviour
{
    [Header("Dependencies")]
    public HYGCatalogParser catalog;

    [Header("Export Resolution")]
    public int width = 2048;
    public int height = 2048;

    [Range(1, 100)]
    public int jpegQuality = 92;

    [Header("Chart Style")]
    public bool drawHorizonCircle = true;
    public int horizonThicknessPx = 2;

    // Star size tuning (in pixels)
    public float maxStarRadiusPx = 6.0f;   // brightest
    public float minStarRadiusPx = 1.3f;   // dimmest

    // Brightness tuning
    public float minAlpha = 0.25f;         // dimmest stars
    public float maxAlpha = 1.00f;         // brightest stars

    [Header("Output")]
    public string folderName = "StarMapExports";

    public void Export2DChartJpeg()
    {
        if (SkySession.Instance == null)
        {
            Debug.LogError("[2D Export] SkySession missing.");
            return;
        }
        if (catalog == null || catalog.VisibleStarsMag6 == null)
        {
            Debug.LogError("[2D Export] Catalog missing/empty.");
            return;
        }

        // --- Time setup (same as your renderer) ---
        DateTimeOffset utc = AstronomyTime.LocalToUtc(SkySession.Instance.LocalDateTime);
        double jd = AstronomyTime.JulianDate(utc);
        double gmst = AstronomyTime.GreenwichMeanSiderealTimeDeg(jd);
        double lst = AstronomyTime.LocalSiderealTimeDeg(gmst, SkySession.Instance.LongitudeDeg);
        double latRad = AstronomyTime.DegToRad(SkySession.Instance.LatitudeDeg);

        // --- Image buffer ---
        var pixels = new Color32[width * height];
        Clear(pixels, width, height, new Color32(0, 0, 0, 255));

        int cx = width / 2;
        int cy = height / 2;
        float R = Mathf.Min(cx, cy) - 4; // leave a small margin

        if (drawHorizonCircle)
            DrawCircleOutline(pixels, width, height, cx, cy, (int)R, horizonThicknessPx, new Color32(80, 80, 80, 255));

        // --- Draw stars (above horizon only) ---
        int drawn = 0;
        foreach (var star in catalog.VisibleStarsMag6)
        {
            if (float.IsNaN(star.ra) || float.IsNaN(star.dec) || float.IsNaN(star.mag))
                continue;

            // RA hours -> degrees
            double raDeg = star.ra * 15.0;
            double haDeg = AstronomyTime.HourAngleDeg(lst, raDeg);
            double haRad = AstronomyTime.DegToRad(haDeg);
            double decRad = AstronomyTime.DegToRad(star.dec);

            // Altitude
            double sinAlt =
                Math.Sin(decRad) * Math.Sin(latRad) +
                Math.Cos(decRad) * Math.Cos(latRad) * Math.Cos(haRad);

            double altRad = Math.Asin(sinAlt);
            if (altRad <= 0) continue; // below horizon

            // Azimuth
            double cosAz =
                (Math.Sin(decRad) - Math.Sin(altRad) * Math.Sin(latRad)) /
                (Math.Cos(altRad) * Math.Cos(latRad));

            cosAz = Math.Clamp(cosAz, -1.0, 1.0);
            double azRad = Math.Acos(cosAz);
            if (Math.Sin(haRad) > 0)
                azRad = 2 * Math.PI - azRad;

            // --- 2D projection: Azimuthal Equidistant centered on zenith ---
            float r = (float)((Math.PI / 2.0 - altRad) / (Math.PI / 2.0)); // 0..1
            float pr = r * R;

            int x = cx + Mathf.RoundToInt(pr * Mathf.Sin((float)azRad));
            int y = cy + Mathf.RoundToInt(pr * Mathf.Cos((float)azRad));

            if ((uint)x >= (uint)width || (uint)y >= (uint)height)
                continue;

            // --- Magnitude -> size/alpha mapping (nonlinear) ---
            float t = Mathf.InverseLerp(0f, catalog.magnitudeLimit, star.mag); // 0 bright -> 1 dim
            float curved = Mathf.Pow(t, 1.7f);

            float radiusPx = Mathf.Lerp(maxStarRadiusPx, minStarRadiusPx, curved);
            float alpha = Mathf.Lerp(maxAlpha, minAlpha, Mathf.Pow(t, 1.3f));

            // Draw a soft star dot
            DrawSoftDot(pixels, width, height, x, y, radiusPx, alpha);

            drawn++;
        }

        // --- Write JPG ---
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        byte[] jpg = tex.EncodeToJPG(jpegQuality);
        Destroy(tex);

        string dir = Path.Combine(Application.persistentDataPath, folderName);
        Directory.CreateDirectory(dir);

        string dt = SkySession.Instance.LocalDateTime.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
        string file = Path.Combine(dir, $"StarChart2D_{dt}_lat{SkySession.Instance.LatitudeDeg:F4}_lon{SkySession.Instance.LongitudeDeg:F4}.jpg");

        File.WriteAllBytes(file, jpg);

        Debug.Log($"[2D Export] Drew {drawn} stars. Saved: {file}");
    }

    // ---- Drawing helpers ----

    private static void Clear(Color32[] pix, int w, int h, Color32 c)
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

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - cy;
            int dy2 = dy * dy;
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - cx;
                int d2 = dx * dx + dy2;
                if (d2 <= rOuter2 && d2 >= rInner2)
                    pix[y * w + x] = col;
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

        for (int y = minY; y <= maxY; y++)
        {
            float dy = y - cy;
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - cx;
                float d2 = dx * dx + dy * dy;
                if (d2 > r2) continue;

                // Soft falloff: brighter at center
                float d = Mathf.Sqrt(d2);
                float falloff = 1f - (d * invR);
                float a = alpha * falloff * falloff;

                AdditiveBlend(ref pix[y * w + x], a);
            }
        }
    }

    // Add white light to a pixel (additive blend)
    private static void AdditiveBlend(ref Color32 dst, float a)
    {
        byte add = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * a), 0, 255);

        int r = dst.r + add;
        int g = dst.g + add;
        int b = dst.b + add;

        dst.r = (byte)Mathf.Min(255, r);
        dst.g = (byte)Mathf.Min(255, g);
        dst.b = (byte)Mathf.Min(255, b);
        dst.a = 255;
    }
}
