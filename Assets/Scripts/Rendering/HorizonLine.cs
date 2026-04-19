using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renders a dashed horizon ring by placing thin scaled cubes
/// tangentially around the circle at Y = 0.
/// </summary>
public class HorizonLine : MonoBehaviour
{
    [Header("Sky Settings")]
    public float skyRadius = 100f;

    [Header("Dash Layout")]
    public int dashCount = 80;
    public float dashLength = 3.5f;   // length along the ring
    public float dashHeight = 0.15f;  // vertical thickness
    public float dashDepth = 0.15f;   // depth into the scene

    [Header("Appearance")]
    public Color dashColor = new Color(1f, 1f, 1f, 0.6f);

    private List<GameObject> dashes = new List<GameObject>();
    private Material dashMaterial;

    void Awake()
    {
        BuildRing();
        SetVisible(false);  // hidden by default
    }

    void BuildRing()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Standard");

        dashMaterial = new Material(shader);
        dashMaterial.color = dashColor;

        for (int i = 0; i < dashCount; i++)
        {
            float angle = (float)i / dashCount * 2f * Mathf.PI;

            // Position on the ring
            Vector3 pos = new Vector3(
                skyRadius * Mathf.Sin(angle),
                0f,
                skyRadius * Mathf.Cos(angle)
            );

            // Tangent direction along the circle at this angle
            Vector3 tangent = new Vector3(
                Mathf.Cos(angle),
                0f,
                -Mathf.Sin(angle)
            );

            GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = $"HorizonDash_{i}";
            dash.transform.parent = transform;
            dash.transform.localPosition = pos;

            // Stretch along the tangent to form a dash
            dash.transform.localScale = new Vector3(dashLength, dashHeight, dashDepth);

            // Rotate to align with the tangent
            dash.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

            Destroy(dash.GetComponent<Collider>());
            dash.GetComponent<Renderer>().material = dashMaterial;

            dashes.Add(dash);
        }
    }

    public void SetVisible(bool visible)
    {
        foreach (var dash in dashes)
        {
            if (dash != null)
                dash.SetActive(visible);
        }
    }
}