using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class InputFieldGrabber : MonoBehaviour
{
    public TMP_InputField dateInput, timeInput, latitudeInput, longitudeInput;


    public void GrabInput()
    {   
        TMP_InputField current = GetCurrentInputField();
        
        if (current != null)
        {
            Debug.Log($"{current.name} entered: {current.text}.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
        else
        {
            Debug.LogWarning("No input field is assigned to be grabbed.");
        }
    }
    
    private TMP_InputField GetCurrentInputField()
    {
        if (dateInput != null)
            return dateInput;
        else if (timeInput != null)
            return timeInput;
        else if (latitudeInput != null)
            return latitudeInput;
        else if (longitudeInput != null)
            return longitudeInput;
        else
            return null;
    }
}