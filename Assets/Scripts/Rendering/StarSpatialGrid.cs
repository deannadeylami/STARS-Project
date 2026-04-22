using System.Collections.Generic;
using UnityEngine;

// Divides the sky sphere into an azimuth/altitude grid.
// Each cell holds indices into the master star list.
public class StarSpatialGrid
{
    private readonly int azBuckets;   // divisions around the horizon
    private readonly int altBuckets;  // divisions from horizon to zenith
    private readonly Dictionary<int, List<int>> cells = new();
    private readonly Vector3[] positions;

    // starPositions: local-space positions from your particle system
    public StarSpatialGrid(Vector3[] starPositions, int azDivisions = 36, int altDivisions = 18)
    {
        azBuckets  = azDivisions;
        altBuckets = altDivisions;

        positions = starPositions;

        for (int i = 0; i < starPositions.Length; i++)
        {
            int key = CellKey(starPositions[i]);
            if (!cells.TryGetValue(key, out var list))
            {
                list = new List<int>();
                cells[key] = list;
            }
            list.Add(i);
        }
    }

    public Vector3 GetPosition(int index)
    {
        if (index >= 0 && index < positions.Length)
        {
            return positions[index];
        }
        return Vector3.zero;
    }

    // Returns all star indices in cells the ray could intersect.
    // angularRadius: how wide a cone around the ray to include (degrees).
    public List<int> Query(Ray localRay, float angularRadius = 2f)
    {
        var result = new List<int>();
        float radRad = angularRadius * Mathf.Deg2Rad;

        // Step along the ray direction and collect nearby cells
        foreach (var kvp in cells)
        {
            Vector3 cellDir = CellCentre(kvp.Key);
            float angle = Vector3.Angle(localRay.direction, cellDir) * Mathf.Deg2Rad;
            if (angle <= radRad + CellHalfAngle())
                result.AddRange(kvp.Value);
        }
        return result;
    }

    // --- Internals ---

    int CellKey(Vector3 pos)
    {
        SphericalCoords(pos, out float az, out float alt);

        int azIdx = Mathf.Clamp(
            (int)(az / (360f / azBuckets)),
            0,
            azBuckets - 1
        );

        float altShifted = alt + 90f;

        int altIdx = Mathf.Clamp(
            (int)(altShifted / (180f / altBuckets)),
            0,
            altBuckets - 1
        );

        return azIdx * altBuckets + altIdx;
    }

    Vector3 CellCentre(int key)
    {
        int azIdx  = key / altBuckets;
        int altIdx = key % altBuckets;

        float az = (azIdx + 0.5f) * (360f / azBuckets) * Mathf.Deg2Rad;

        float alt = ((altIdx + 0.5f) * (180f / altBuckets) - 90f) * Mathf.Deg2Rad;

        return new Vector3(
            Mathf.Cos(alt) * Mathf.Sin(az),
            Mathf.Sin(alt),
            Mathf.Cos(alt) * Mathf.Cos(az)
        );
    }

    float CellHalfAngle() =>
        Mathf.Max(180f / azBuckets, 90f / altBuckets) * Mathf.Deg2Rad;

    static void SphericalCoords(Vector3 pos, out float azDeg, out float altDeg)
    {
        float r = pos.magnitude;
        altDeg = Mathf.Asin(Mathf.Clamp(pos.y / r, -1f, 1f)) * Mathf.Rad2Deg;
        azDeg  = (Mathf.Atan2(pos.x, pos.z) * Mathf.Rad2Deg + 360f) % 360f;
    }
}