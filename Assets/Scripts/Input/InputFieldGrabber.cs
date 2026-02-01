using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class InputFieldGrabber : MonoBehaviour
{
    public TMP_InputField dateInput, timeInput, latitudeInput, longitudeInput;


    public void GrabAllInputs()
    {           
        // Grab input field values
        string date = dateInput.text;
        string time = timeInput.text;
        string latitude = latitudeInput.text;
        string longitude = longitudeInput.text;

        // Check for empty fields
        if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(time) || string.IsNullOrEmpty(latitude) || string.IsNullOrEmpty(longitude))
        {
            Debug.LogWarning("At least one input field is empty. Please fill in every field.");
            return;
        }

        // Log the values to the console
        Debug.Log("Latitude: " + latitude);
        Debug.Log("Longitude: " + longitude);
        Debug.Log("Date: " + date);
        Debug.Log("Time: " + time);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);

    }

}