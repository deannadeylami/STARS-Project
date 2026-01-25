using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControllerNew : MonoBehaviour
{
    // Rotation settings
    public float rotationSpeed = 100f;
    public float rotationX = 85f;
    public float rotationY = -85f;

    private float horizontalRotation = 0f;
    private float verticalRotation = 0f;
    private Vector2 lookInput;
    
    private CameraControl controls;

    void Awake()
    {
        controls = new CameraControl();
        controls.Camera.Look.performed += OnLook;
        controls.Camera.Look.canceled += OnLook;
        controls.Enable();
    }

    void OnEnable()
    {
        controls.Enable();
    }

    void OnDisable()
    {
        controls.Disable();
    }

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        horizontalRotation = angles.y;
        verticalRotation = angles.x;
    }
    
    void Update()
    {
        // Adding in a deadzone to stop drifting from minor input
        float deadzone = 0.01f;
        lookInput.x = Mathf.Abs(lookInput.x) < deadzone ? 0 : lookInput.x;
        lookInput.y = Mathf.Abs(lookInput.y) < deadzone ? 0 : lookInput.y;

        horizontalRotation += lookInput.x * rotationSpeed;
        verticalRotation -= lookInput.y * rotationSpeed;
        verticalRotation = Mathf.Clamp(verticalRotation, -rotationX, rotationX);

        transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0f);
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }
}

