using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple first-person controller: WASD move, mouse look, Shift sprint, Ctrl crouch.
/// Esc frees the mouse; click to capture it again.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraPivot;
    public float moveSpeed = 3.5f;
    public float sprintMultiplier = 1.8f;
    public float lookSensitivity = 0.1f;
    public float gravity = -20f;
    public float standingEyeHeight = 1.6f;
    public float crouchEyeHeight = 0.9f;

    CharacterController controller;
    float pitch;
    float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (cameraPivot == null) cameraPivot = GetComponentInChildren<Camera>().transform;
        SetCursorLocked(true);
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        else if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame) SetCursorLocked(true);

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
            cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        // Crouch to reach the underside
        float eye = kb.leftCtrlKey.isPressed ? crouchEyeHeight : standingEyeHeight;
        Vector3 camPos = cameraPivot.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, eye, Time.deltaTime * 10f);
        cameraPivot.localPosition = camPos;

        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed) input.x -= 1f;

        Vector3 move = transform.forward * input.y + transform.right * input.x;
        if (move.sqrMagnitude > 1f) move.Normalize();
        float speed = moveSpeed * (kb.leftShiftKey.isPressed ? sprintMultiplier : 1f);

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -1f;
        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((move * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
