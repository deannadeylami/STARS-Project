using UnityEngine;
using UnityEngine.UI;

public class ToggleSwitch : MonoBehaviour
{
    public RectTransform knob;                      // Sliding circle on the track
    public float onX = 10f;                         //  Knob X when toggle on
    public float offX = -10f;                       //  Knob X when toggle off

    private Toggle toggle;                          // Toggle component in UI
    private float targetX;                          // New position X slides towards
    public Image trackImage;                        // Toggle background (color to be changed)
    public Color trackOn = new Color(0f, 0f, 0f);   // Track color on
    public Color trackOff = new Color(0f, 0f, 0f);  // Track color off

    void Start()
    {
        toggle = GetComponent<Toggle>();

        // Set knob to correct starting position and color
        targetX = toggle.isOn ? onX : offX;
        knob.anchoredPosition = new Vector2(targetX, 0);
        trackImage.color = toggle.isOn ? trackOn : trackOff;
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    // When toggle clicked, update position of the knob to slide forward.
    void OnToggleChanged(bool value)
    {
        targetX = value ? onX : offX;
    }

    void Update()
    {   
        // Slide knob towards new position.
        float currentX = knob.anchoredPosition.x;
        float newX = Mathf.Lerp(currentX, targetX, Time.deltaTime * 6f);
        knob.anchoredPosition = new Vector2(newX, 0);

        // Transition color between on and off.
        Color targetColor = toggle.isOn ? trackOn : trackOff;
        trackImage.color = Color.Lerp(trackImage.color, targetColor, Time.deltaTime * 6f);
    }
}