using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControllerNew : MonoBehaviour
{
    // Rotation settings
    public float rotationSpeed = 100f;          // Speed of camera rotation
    public float rotationX = 85f;               // Looking up and down limit
    public float rotationY = -85f;

    private float horizontalRotation = 0f;     // Left/right rotation
    private float verticalRotation = 0f;       // Up/down rotation
    private Vector2 lookInput;                 // Input from mouse
    
    private CameraControl controls;            // Input action map reference, the binding of controls from input.   

    void Awake()                               // Awake is called before the scene is loaded. 
    {
        controls = new CameraControl();             // Setup the input action map.
        controls.Camera.Look.performed += OnLook;   // Action occurs when the player moves the mouse.
        controls.Camera.Look.canceled += OnLook;    // Action stops occuring when the player stops moving the mouse.
        controls.Enable();                          // Enable these controls.
    }

    void Start()
    {
        // Stops snapping to 0,0. Will be necessary later when adding additional UI screens.
        Vector3 angles = transform.eulerAngles;
        horizontalRotation = angles.y;
        verticalRotation = angles.x;
    }
    
    void Update()
    {
        // Update camera based on mouse input. 
        horizontalRotation += lookInput.x * rotationSpeed; // Update left/right rotation
        verticalRotation -= lookInput.y * rotationSpeed;   // Update up/down rotation
        verticalRotation = Mathf.Clamp(verticalRotation, -rotationX, rotationX); // Stay within limits, avoid camera flipping.

        transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0f); // Apply rotation to the camera.
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        // Stores the mouse input value to be used in Update().
        lookInput = context.ReadValue<Vector2>();
    }
}

