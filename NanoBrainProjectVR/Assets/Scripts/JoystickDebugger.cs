using UnityEngine;
using UnityEngine.InputSystem;

public class JoystickDebugger : MonoBehaviour
{
    [Tooltip("Drag the XRI Left Locomotion/Move action here")]
    public InputActionReference leftJoystickAction;

    private void Update()
    {
        if (leftJoystickAction != null && leftJoystickAction.action != null)
        {
            Vector2 value = leftJoystickAction.action.ReadValue<Vector2>();
            if (value.magnitude > 0.1f)
            {
                Debug.Log($"<color=cyan>[JOYSTICK DEBUG]</color> Left Joystick is moving! Value: {value}");
            }
        }
    }
}
