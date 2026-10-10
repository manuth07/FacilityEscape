using UnityEngine;
using UnityEngine.InputSystem;

namespace FacilityEscape.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform viewCamera;
        [SerializeField] private Camera originalSceneCamera;
        [SerializeField, Min(0f)] private float walkingSpeed = 5f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private float pitch;
        private float verticalVelocity;
        private bool originalCameraWasEnabled;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (viewCamera == null)
            {
                Camera camera = GetComponentInChildren<Camera>();
                if (camera != null) viewCamera = camera.transform;
            }
        }

        private void OnEnable()
        {
            if (originalSceneCamera != null)
            {
                originalCameraWasEnabled = originalSceneCamera.enabled;
                originalSceneCamera.enabled = false;
            }
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
            if (originalSceneCamera != null)
                originalSceneCamera.enabled = originalCameraWasEnabled;
        }

        private void OnApplicationFocus(bool focused)
        {
            SetCursorLocked(focused);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Application.isFocused)
                SetCursorLocked(true);

            Vector2 movement = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                if (keyboard != null)
                {
                    movement.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                    movement.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                    movement = Vector2.ClampMagnitude(movement, 1f);
                }

                if (mouse != null && viewCamera != null)
                {
                    // Mouse delta is already measured per frame; do not multiply by deltaTime.
                    Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                    transform.Rotate(0f, look.x, 0f);
                    pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                    viewCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                }
            }

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, -50f);

            Vector3 velocity = (transform.right * movement.x + transform.forward * movement.y) * walkingSpeed;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
