using UnityEngine;
using UnityEngine.InputSystem;

namespace ThirdPersonDemo.Gameplay
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField, Min(0f)] private float lookSensitivity = 0.1f;
        [SerializeField, Range(0f, 90f)] private float maxLookUpAngle = 50f;
        [SerializeField, Range(0f, 90f)] private float maxLookDownAngle = 45f;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.4f, -3.5f);
        [SerializeField, Min(0f)] private float positionSmoothTime = 0.1f;
        [SerializeField, Min(0f)] private float rotationSharpness = 15f;
        [SerializeField] private LayerMask collisionLayers;
        [SerializeField, Min(0f)] private float collisionRadius = 0.2f;

        // Set by the selected character through SetTargetHeight, so the camera looks at each character's own height.
        private float targetHeight;
        private float yaw;
        private float pitch;
        private float smoothedYaw;
        private float smoothedPitch;
        private Vector3 followPoint;
        private Vector3 followVelocity;

        private void Awake()
        {
            yaw = transform.eulerAngles.y;
            smoothedYaw = yaw;
        }

        private void OnEnable()
        {
            // Keeps the mouse in the game window so turning is never stopped by the screen edge.
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Start()
        {
            // Runs after every Awake, so the selected character has already set targetHeight and the camera does not slide up on the first frames.
            followPoint = target.position + Vector3.up * targetHeight;
        }

        private void LateUpdate()
        {
            var lookInput = lookAction.action.ReadValue<Vector2>() * lookSensitivity;
            yaw += lookInput.x;
            // A negative pitch tilts the camera up.
            pitch = Mathf.Clamp(pitch - lookInput.y, -maxLookUpAngle, maxLookDownAngle);

            // Smoothing the angles instead of the camera position keeps the camera on its orbit and the player centered while turning.
            // Exponential easing keeps the smoothing the same at any frame rate.
            var blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            smoothedYaw = Mathf.Lerp(smoothedYaw, yaw, blend);
            smoothedPitch = Mathf.Lerp(smoothedPitch, pitch, blend);

            // Only the point being followed lags, so walking and jumping feel soft without bending the orbit.
            followPoint = Vector3.SmoothDamp(followPoint, target.position + Vector3.up * targetHeight, ref followVelocity, positionSmoothTime);

            var rotation = Quaternion.Euler(smoothedPitch, smoothedYaw, 0f);
            var toCamera = rotation * offset;

            // Pulls the camera in front of the ground or a wall between it and the player, so it never ends up inside them.
            if (Physics.SphereCast(followPoint, collisionRadius, toCamera.normalized, out var hit, toCamera.magnitude, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                toCamera = toCamera.normalized * hit.distance;
            }

            transform.position = followPoint + toCamera;
            transform.rotation = rotation;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
        }

        public void SetTargetHeight(float height)
        {
            targetHeight = height;
        }
    }
}
