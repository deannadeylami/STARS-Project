using UnityEngine;
using UnityEngine.InputSystem;

public class SettingsMenu : MonoBehaviour
{
    public GameObject settingsPanel;
    public CameraControllerNew cameraController;

    private CameraControl controls;

    private void Awake()
    {
        controls = new CameraControl();
        controls.UI.Cancel.performed += OnCancel;
        controls.Enable();
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        ToggleMenu();
    }

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
}