using UnityEngine;
using UnityEngine.UI;

public class ToggleHandler : MonoBehaviour
{
    private Toggle toggle;

    void Awake()
    {
        toggle = GetComponent<Toggle>();

        // ✅ Restore saved value when scene loads
        toggle.isOn = GameSettings.GPUAccel;

        // ✅ Hook listener (safe even if scene reloads)
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    void OnToggleChanged(bool value)
    {
        GameSettings.GPUAccel = value;
        Debug.Log("GPUAccel set to: " + value);
    }
}