using UnityEngine;
using UnityEngine.InputSystem;

namespace ArrowGame.Gameplay
{
    public sealed class PlayerController : MonoBehaviour
    {
        // Keeps the controller pressed onto the ground so isGrounded stays true between frames.
        private const float GroundedVerticalVelocity = -2f;

        private enum Stance
        {
            Standing,
            Crouching,
            Prone,
        }

        [SerializeField] private CharacterController characterController;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference sprintAction;
        [SerializeField] private InputActionReference crouchAction;
        [SerializeField] private InputActionReference proneAction;
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 9f;
        [SerializeField, Min(0f)] private float crouchSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float crouchHeight = 1.75f;
        [SerializeField, Min(0f)] private float crawlSpeed = 1f;
        [SerializeField, Min(0.1f)] private float proneHeight = 0.8f;
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
        [SerializeField, Min(0.01f)] private float gravity = 20f;
        [SerializeField, Min(0f)] private float edgeSlideSpeed = 3f;
        [SerializeField] private LayerMask groundLayers;

        private float verticalVelocity;
        private Vector3 groundNormal = Vector3.up;
        private float standingHeight;
        private Vector3 standingCenter;
        private Stance stance;

        public float HorizontalSpeed { get; private set; }
        public bool IsGrounded => characterController.isGrounded;
        public bool IsCrouching => stance == Stance.Crouching;
        public bool IsProne => stance == Stance.Prone;

        private void Awake()
        {
            standingHeight = characterController.height;
            standingCenter = characterController.center;
        }

        private void Update()
        {
            var moveInput = moveAction.action.ReadValue<Vector2>();

            if (crouchAction.action.WasPressedThisFrame())
            {
                TrySetStance(IsCrouching ? Stance.Standing : Stance.Crouching);
            }

            if (proneAction.action.WasPressedThisFrame())
            {
                TrySetStance(IsProne ? Stance.Standing : Stance.Prone);
            }

            var speed = stance switch
            {
                Stance.Crouching => crouchSpeed,
                Stance.Prone => crawlSpeed,
                _ => sprintAction.action.IsPressed() ? sprintSpeed : moveSpeed,
            };
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

            if (characterController.isGrounded && !onEdge && !IsProne && jumpAction.action.WasPressedThisFrame())
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

        private void TrySetStance(Stance target)
        {
            var height = target switch
            {
                Stance.Crouching => crouchHeight,
                Stance.Prone => proneHeight,
                _ => standingHeight,
            };

            // Rising under something low would push the capsule into it, so the player stays down.
            if (height > characterController.height && !HasRoomFor(height))
            {
                return;
            }

            stance = target;
            characterController.height = height;
            characterController.center = CenterFor(height);
        }

        private bool HasRoomFor(float height)
        {
            // Only the space the taller capsule adds above the current one is checked, so touching the floor or a nearby wall does not count.
            var radius = characterController.radius * 0.95f;
            var currentTop = transform.position + characterController.center + Vector3.up * (characterController.height / 2f - characterController.radius);
            var targetTop = transform.position + CenterFor(height) + Vector3.up * (height / 2f - characterController.radius);
            return !Physics.CheckCapsule(currentTop, targetTop, radius, groundLayers, QueryTriggerInteraction.Ignore);
        }

        private Vector3 CenterFor(float height)
        {
            // The capsule shrinks from the top so the feet stay on the ground.
            return standingCenter + Vector3.down * ((standingHeight - height) / 2f);
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
