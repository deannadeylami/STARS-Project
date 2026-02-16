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
    
    private float zoomSpeed = 10f;                   // Speed of zooming in and out
    private float minZoom = 40f;                      // Minimum zoom distance
    private float maxZoom = 90f;                     // Maximum zoom distance

    private float zoomInput;                         // Input from scroll wheel
    private Camera cam;

    private CameraControl controls;            // Input action map reference, the binding of controls from input.   
    private bool mouseDrag = false;            // Keep track of user dragging/holding mouse button.

    void Awake()                               // Awake is called before the scene is loaded. 
    {
        controls = new CameraControl();             // Setup the input action map.
        controls.Camera.Look.performed += OnLook;   // Action occurs when the player moves the mouse.
        controls.Camera.Look.canceled += OnLook;    // Action stops occuring when the player stops moving the mouse.
        
        controls.Camera.Drag.performed += ctx => mouseDrag = true;  // Button pressed, dragging activated.
        controls.Camera.Drag.canceled += ctx => mouseDrag = false; // Button let go, dragging deactivated.

        controls.Camera.Zoom.performed += OnZoom;   // Action occurs when the player scrolls the mouse wheel.
        controls.Camera.Zoom.canceled += OnZoom;    // Action stops occurring when the player stops scrolling the mouse wheel.

        controls.Enable();                          // Enable these controls.
        cam = GetComponent<Camera>();               // Get the camera component for zooming.

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
        if (mouseDrag)                           // Only rotate if mouse button held down (dragging activated).
        {
        // Update camera based on mouse input. 
        horizontalRotation += lookInput.x * rotationSpeed; // Update left/right rotation
        verticalRotation -= lookInput.y * rotationSpeed;   // Update up/down rotation
        verticalRotation = Mathf.Clamp(verticalRotation, -rotationX, rotationX); // Stay within limits, avoid camera flipping.
        transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0f); // Apply rotation to the camera.
        }

        if (zoomInput != 0f)
        {
            cam.fieldOfView -= zoomInput * zoomSpeed; 
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, minZoom, maxZoom);
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        // Stores the mouse input value to be used in Update().
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnZoom(InputAction.CallbackContext context)
    {
        // Stores the scroll wheel input value to be used in Update().
        zoomInput = context.ReadValue<float>();
        Debug.Log(zoomInput);
    }
}

