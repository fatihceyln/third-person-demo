using UnityEngine;

namespace ArrowGame.Gameplay
{
    public sealed class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int RunPlaybackSpeedParameter = Animator.StringToHash("RunPlaybackSpeed");

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerController playerController;
        // The movement speed at which the run clip's feet match the ground; measured from the clip's planted toes.
        [SerializeField, Min(0.01f)] private float runAnimationSpeed = 4f;

        private void Update()
        {
            animator.SetFloat(SpeedParameter, playerController.HorizontalSpeed);
            animator.SetBool(GroundedParameter, playerController.IsGrounded);
            animator.SetFloat(RunPlaybackSpeedParameter, playerController.HorizontalSpeed / runAnimationSpeed);
        }
    }
}
