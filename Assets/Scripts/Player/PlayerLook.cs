using UnityEngine;
using UnityEngine.InputSystem;

namespace ArrowGame.Gameplay
{
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField, Min(0f)] private float lookSensitivity = 0.1f;
        [SerializeField, Range(0f, 90f)] private float maxLookUpAngle = 85f;
        [SerializeField, Range(0f, 90f)] private float maxLookDownAngle = 85f;

        private float pitch;

        private void OnEnable()
        {
            // Keeps the mouse in the game window so turning is never stopped by the screen edge.
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            var lookInput = lookAction.action.ReadValue<Vector2>() * lookSensitivity;

            // The body turns left and right so movement follows the view; only the camera tilts up and down.
            transform.Rotate(0f, lookInput.x, 0f);
            // A negative pitch tilts the camera up.
            pitch = Mathf.Clamp(pitch - lookInput.y, -maxLookUpAngle, maxLookDownAngle);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
