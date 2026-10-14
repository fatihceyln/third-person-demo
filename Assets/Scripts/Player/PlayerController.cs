using UnityEngine;
using UnityEngine.InputSystem;

namespace ArrowGame.Gameplay
{
    public sealed class PlayerController : MonoBehaviour
    {
        // Keeps the controller pressed onto the ground so isGrounded stays true between frames.
        private const float GroundedVerticalVelocity = -2f;

        [SerializeField] private CharacterController characterController;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference sprintAction;
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 9f;
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
        [SerializeField, Min(0.01f)] private float gravity = 20f;
        [SerializeField, Min(0f)] private float edgeSlideSpeed = 3f;
        [SerializeField] private LayerMask groundLayers;

        private float verticalVelocity;
        private Vector3 groundNormal = Vector3.up;

        public float HorizontalSpeed { get; private set; }
        public bool IsGrounded => characterController.isGrounded;

        private void Update()
        {
            var moveInput = moveAction.action.ReadValue<Vector2>();
            var speed = sprintAction.action.IsPressed() ? sprintSpeed : moveSpeed;
            // Movement follows the camera's heading so W always walks away from the camera.
            var cameraYaw = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            var moveDirection = cameraYaw * new Vector3(moveInput.x, 0f, moveInput.y);
            var horizontalVelocity = moveDirection * speed;
            HorizontalSpeed = horizontalVelocity.magnitude;

            if (moveDirection != Vector3.zero)
            {
                var targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            // The rounded bottom of the capsule can rest on a ledge corner and still count as grounded, leaving the player hanging in the air.
            var onEdge = characterController.isGrounded && IsOnEdge();

            if (onEdge)
            {
                horizontalVelocity += new Vector3(groundNormal.x, 0f, groundNormal.z).normalized * edgeSlideSpeed;
            }

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = GroundedVerticalVelocity;
            }

            if (characterController.isGrounded && !onEdge && jumpAction.action.WasPressedThisFrame())
            {
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            }

            verticalVelocity -= gravity * Time.deltaTime;
            groundNormal = Vector3.zero;
            characterController.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Only contacts on the lower half-sphere are ground; the flattest one wins when several are touched.
            var bottomSphereHeight = transform.position.y + characterController.center.y - characterController.height / 2f + characterController.radius;

            if (hit.point.y <= bottomSphereHeight && hit.normal.y > groundNormal.y)
            {
                groundNormal = hit.normal;
            }
        }

        private bool IsOnEdge()
        {
            // A flat contact has no direction to slide in. The slope limit is not used here: ledge bevels can be exactly 45 degrees.
            if (new Vector2(groundNormal.x, groundNormal.z).sqrMagnitude < 0.0001f)
            {
                return false;
            }

            // Stair steps also touch the capsule at an angle, but there is still ground right under its center.
            var bottomSphereCenter = transform.position + characterController.center + Vector3.down * (characterController.height / 2f - characterController.radius);
            var probeLength = characterController.radius + characterController.stepOffset + characterController.skinWidth;
            return !Physics.Raycast(bottomSphereCenter, Vector3.down, probeLength, groundLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
