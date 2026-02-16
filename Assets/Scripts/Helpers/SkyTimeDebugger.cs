using UnityEngine;

public class SkyTimeDebug : MonoBehaviour
{
    private void Start()
    {
        if (SkySession.Instance == null)
        {
            Debug.LogError("SkySession.Instance is null. Did you enter through InputScene?");
            return;
        }

        double lat = SkySession.Instance.LatitudeDeg;
        double lon = SkySession.Instance.LongitudeDeg;
        var local = SkySession.Instance.LocalDateTime;

        // IMPORTANT: This expects longitude east-positive.
        // Your input in the US is usually negative (west), which is correct for east-positive convention.
        var utc = AstronomyTime.LocalToUtc(local);

        double jd = AstronomyTime.JulianDate(utc);
        double gmst = AstronomyTime.GreenwichMeanSiderealTimeDeg(jd);
        double lst = AstronomyTime.LocalSiderealTimeDeg(gmst, lon);

        Debug.Log($"Local: {local:dd/MM/yyyy HH:mm} (Kind={local.Kind})");
        Debug.Log($"UTC:   {utc:yyyy-MM-dd HH:mm:ss zzz}");
        Debug.Log($"JD:    {jd:F6}");
        Debug.Log($"GMST:  {gmst:F6} deg");
        Debug.Log($"LST:   {lst:F6} deg  ({lst/15.0:F6} hours)");
        Debug.Log($"Lat:   {lat:F6} deg  Lon: {lon:F6} deg (east+)");
    }
}
