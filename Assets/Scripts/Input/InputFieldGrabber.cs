using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class InputFieldGrabber : MonoBehaviour
{
    public TMP_InputField dateInput, timeInput, latitudeInput, longitudeInput;
    public TMP_Text errorMessage;

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
            errorMessage.text = "Please fill in every field.";
            return;
        }

        // Validate latitude and longitude by checking if they are within valid ranges.
        if (!float.TryParse(latitude, out float latValue) || latValue <-90 || latValue >90)
        {
            Debug.LogWarning("Latitude is not a number between -90 and 90.");
            errorMessage.text = "Latitude must be a number between -90 and 90.";
            return;
        }

        if (!float.TryParse(longitude, out float lonValue) || lonValue <-180 || lonValue >180)
        {
            Debug.LogWarning("Longitude is not a number between -180 and 180.");
            errorMessage.text = "Longitude must be a number between -180 and 180.";
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