using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Manage settings menu.
public class SettingsMenu : MonoBehaviour
{
    public GameObject settingsPanel;
    public CameraControllerNew cameraController;
    public GameObject Ground;
    public Toggle horizonToggle;
    public Toggle groundToggle;
    private CameraControl controls;

    private void Awake()
    {
        controls = new CameraControl();
        controls.UI.Cancel.performed += OnCancel;
        controls.Enable();
    }

    // Pressing Esc brings up the menu.
    private void OnCancel(InputAction.CallbackContext context)
    {
        ToggleMenu();
    }

    // Opens/closes settings menu, and enables/disables camera controls.
    public void ToggleMenu()
    {
        bool isOpen = settingsPanel.activeSelf;

        settingsPanel.SetActive(!isOpen);

        if (!isOpen)
        {
            cameraController.DisableControls();
        }
        else
        {
            cameraController.EnableControls();
        }
    }

    // Shows/hides ground plane.
    public void ToggleGround(bool enabled)
    {
        Ground.SetActive(enabled);
    }
    
    // Called by "Generate Stars under Horizon" toggle.
    // Disables ground toggle when below-horizon stars are generated to avoid label clipping bug.
    public void OnHorizonToggle(bool value)
    {
        if (value)
        {
            groundToggle.isOn = false;
            groundToggle.interactable = false;
        }
        else
        {
            groundToggle.interactable = true;
        }
    }

    // Called by "Enable Ground" toggle.
    // Disables below-horizon stars toggle when ground toggle is on to avoid label clipping bug.
    public void OnGroundToggle(bool value)
    {
        if (value)
        {
            horizonToggle.isOn = false;
            horizonToggle.interactable = false;
        }
        else
        {
            horizonToggle.interactable = true;
        }
    }
}