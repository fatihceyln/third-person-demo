using UnityEngine;

namespace ArrowGame.Gameplay
{
    public sealed class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int RunPlaybackSpeedParameter = Animator.StringToHash("RunPlaybackSpeed");
        private static readonly int CrouchedParameter = Animator.StringToHash("Crouched");
        private static readonly int ProneParameter = Animator.StringToHash("Prone");
        private static readonly int CrawlPlaybackSpeedParameter = Animator.StringToHash("CrawlPlaybackSpeed");

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerController playerController;
        // The movement speed at which the run clip's feet match the ground; measured from the clip's planted toes.
        [SerializeField, Min(0.01f)] private float runAnimationSpeed = 4f;
        // The movement speed at which the crawl clip's hands match the ground; set by how far a planted hand slides per cycle.
        [SerializeField, Min(0.01f)] private float crawlAnimationSpeed = 0.84f;

        private void Update()
        {
            animator.SetFloat(SpeedParameter, playerController.HorizontalSpeed);
            animator.SetBool(GroundedParameter, playerController.IsGrounded);
            animator.SetFloat(RunPlaybackSpeedParameter, playerController.HorizontalSpeed / runAnimationSpeed);
            animator.SetBool(CrouchedParameter, playerController.IsCrouching);
            animator.SetBool(ProneParameter, playerController.IsProne);
            animator.SetFloat(CrawlPlaybackSpeedParameter, playerController.HorizontalSpeed / crawlAnimationSpeed);
        }
    }
}
