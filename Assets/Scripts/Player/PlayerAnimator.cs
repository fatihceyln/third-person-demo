using UnityEngine;

namespace ArrowGame.Gameplay
{
    public sealed class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerController playerController;

        private void Update()
        {
            animator.SetFloat(SpeedParameter, playerController.HorizontalSpeed);
            animator.SetBool(GroundedParameter, playerController.IsGrounded);
        }
    }
}
