using UnityEngine;
using UnityEngine.InputSystem;

namespace ArrowGame.Gameplay
{
    public sealed class PlayerController : MonoBehaviour
    {
        // Keeps the controller pressed onto the ground so isGrounded stays true between frames.
        private const float GroundedVerticalVelocity = -2f;

        [SerializeField] private CharacterController characterController;
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference sprintAction;
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 9f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
        [SerializeField, Min(0.01f)] private float gravity = 20f;

        private float verticalVelocity;

        private void Update()
        {
            var moveInput = moveAction.action.ReadValue<Vector2>();
            var speed = sprintAction.action.IsPressed() ? sprintSpeed : moveSpeed;
            var horizontalVelocity = new Vector3(moveInput.x, 0f, moveInput.y) * speed;

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = GroundedVerticalVelocity;
            }

            if (characterController.isGrounded && jumpAction.action.WasPressedThisFrame())
            {
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            }

            verticalVelocity -= gravity * Time.deltaTime;
            characterController.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
    }
}
