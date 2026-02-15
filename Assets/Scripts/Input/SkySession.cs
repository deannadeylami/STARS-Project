using System;
using UnityEngine;

public class SkySession : MonoBehaviour
{
    public static SkySession Instance { get; private set; }

    [Header("Validated Inputs (read-only at runtime)")]
    [SerializeField] private double latitudeDeg;
    [SerializeField] private double longitudeDeg;
    [SerializeField] private DateTime localDateTime;

    [Header("Optional: Raw input strings (for debugging/display)")]
    [SerializeField] private string rawLatitude;
    [SerializeField] private string rawLongitude;
    [SerializeField] private string rawDate;
    [SerializeField] private string rawTime;

    public double LatitudeDeg => latitudeDeg;
    public double LongitudeDeg => longitudeDeg;
    public DateTime LocalDateTime => localDateTime;

    public string RawLatitude => rawLatitude;
    public string RawLongitude => rawLongitude;
    public string RawDate => rawDate;
    public string RawTime => rawTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetInputs(
        double latDeg,
        double lonDeg,
        DateTime localDt,
        string latRaw,
        string lonRaw,
        string dateRaw,
        string timeRaw)
    {
        latitudeDeg = latDeg;
        longitudeDeg = lonDeg;
        localDateTime = localDt;

        rawLatitude = latRaw;
        rawLongitude = lonRaw;
        rawDate = dateRaw;
        rawTime = timeRaw;
    }

    public void Clear()
    {
        latitudeDeg = 0;
        longitudeDeg = 0;
        localDateTime = default;

        rawLatitude = "";
        rawLongitude = "";
        rawDate = "";
        rawTime = "";
    }
}
