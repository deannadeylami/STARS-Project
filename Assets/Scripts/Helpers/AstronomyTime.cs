using System;
using System.Globalization;
using UnityEngine;

public static class AstronomyTime
{
    // Convert a local DateTime (Kind.Unspecified or Local) into UTC DateTimeOffset
    // using the machine's local timezone offset.
    public static DateTimeOffset LocalToUtc(DateTime localDateTime)
    {
        // Treat Unspecified as Local (your UI is local time).
        if (localDateTime.Kind == DateTimeKind.Unspecified)
        {
            TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
            var localDto = new DateTimeOffset(localDateTime, offset);
            return localDto.ToUniversalTime();
        }

        if (localDateTime.Kind == DateTimeKind.Local)
        {
            return new DateTimeOffset(localDateTime).ToUniversalTime();
        }

        // If already UTC, keep it.
        return new DateTimeOffset(localDateTime, TimeSpan.Zero);
    }

    // Julian Date from UTC DateTimeOffset
    // Valid for modern dates; more than enough for 1900-2100 requirement.
    public static double JulianDate(DateTimeOffset utc)
    {
        // Use UTC components
        int Y = utc.Year;
        int M = utc.Month;
        double D = utc.Day
                   + (utc.Hour + (utc.Minute + (utc.Second + utc.Millisecond / 1000.0) / 60.0) / 60.0) / 24.0;

        if (M <= 2)
        {
            Y -= 1;
            M += 12;
        }

        int A = Y / 100;
        int B = 2 - A + (A / 4);

        // Gregorian calendar formula
        double jd = Math.Floor(365.25 * (Y + 4716))
                  + Math.Floor(30.6001 * (M + 1))
                  + D + B - 1524.5;

        return jd;
    }

    // GMST in degrees [0, 360)
    // Uses a common IAU-style approximation good for this project scale.
    public static double GreenwichMeanSiderealTimeDeg(double julianDate)
    {
        double T = (julianDate - 2451545.0) / 36525.0; // centuries since J2000.0

        // GMST in degrees
        double gmst = 280.46061837
                    + 360.98564736629 * (julianDate - 2451545.0)
                    + 0.000387933 * T * T
                    - (T * T * T) / 38710000.0;

        return NormalizeDegrees(gmst);
    }

    // LST in degrees [0, 360)
    // longitudeDeg: east-positive. (If you use west-positive, flip sign.)
    public static double LocalSiderealTimeDeg(double gmstDeg, double longitudeDegEastPositive)
    {
        return NormalizeDegrees(gmstDeg + longitudeDegEastPositive);
    }

    public static double NormalizeDegrees(double deg)
    {
        deg %= 360.0;
        if (deg < 0) deg += 360.0;
        return deg;
    }

    public static double DegToRad(double deg) => deg * Math.PI / 180.0;
    public static double RadToDeg(double rad) => rad * 180.0 / Math.PI;

    // Hour Angle (HA) in degrees: HA = LST - RA
    // RA is commonly in hours in HYG (ra field), so convert RA hours to degrees first: RAdeg = RAh * 15
    public static double HourAngleDeg(double lstDeg, double raDeg)
    {
        return NormalizeDegrees(lstDeg - raDeg);
    }
}
