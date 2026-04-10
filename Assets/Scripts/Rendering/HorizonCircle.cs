using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class HorizonCircle : MonoBehaviour
{
    public float radius = 490f;
    public float heightY = 0f;
    public int segments = 256;

    void Start()
    {
        LineRenderer lr = GetComponent<LineRenderer>();
        lr.positionCount = segments + 1;
        lr.loop = true;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, heightY, Mathf.Sin(angle) * radius));
        }
    }

    void Update() => transform.position = new Vector3(Camera.main.transform.position.x, heightY, Camera.main.transform.position.z);
}