using UnityEngine;
using TMPro;

public class InputFieldGrabber : MonoBehaviour
{
    public TMP_InputField latitudeInput;

    public string latitudeValue;

    public void GrabLatitude()
    {
        latitudeValue = latitudeInput.text;
        Debug.Log("Latitude entered: " + latitudeValue);
    }
}
