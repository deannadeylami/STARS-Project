using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class TabNavigation : MonoBehaviour
{
    public TMP_InputField[] inputFields;

    void Start()
    {
        foreach (var field in inputFields)
        {
            field.onSubmit.AddListener(_ => FocusNext(field));
        }
    }

    void Update()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            foreach (var field in inputFields)
            {
                if (field.isFocused)
                {
                    FocusNext(field);
                    return;
                }
            }
        }
    }

    void FocusNext(TMP_InputField current)
    {
        int i = System.Array.IndexOf(inputFields, current);
        int next = (i + 1) % inputFields.Length;
        inputFields[next].Select();
        inputFields[next].ActivateInputField();
    }
}