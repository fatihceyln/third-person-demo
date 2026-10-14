using UnityEngine;

namespace ThirdPersonDemo.Gameplay
{
    public sealed class FootstepAudio : MonoBehaviour
    {
        private static readonly int RunState = Animator.StringToHash("Run");
        private static readonly int CrouchWalkState = Animator.StringToHash("CrouchWalk");

        [SerializeField] private Animator animator;
        // Shared by every character, so it lives on the Player and keeps playing while models switch.
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float crouchVolume = 0.4f;
        [SerializeField, Range(0f, 0.5f)] private float pitchVariation = 0.05f;

        private int lastStateHash;
        private int lastStepIndex;
        private int lastClipIndex = -1;

        private void Update()
        {
            // Every walk clip plants a foot at its start and halfway through, so a step is due each time the loop passes one of those points.
            var state = animator.GetCurrentAnimatorStateInfo(0);
            var stepIndex = Mathf.FloorToInt(state.normalizedTime * 2f);
            var isWalking = state.shortNameHash == RunState || state.shortNameHash == CrouchWalkState;

            if (isWalking && state.shortNameHash == lastStateHash && stepIndex != lastStepIndex)
            {
                PlayStep(state.shortNameHash == CrouchWalkState ? crouchVolume : volume);
            }

            lastStateHash = state.shortNameHash;
            lastStepIndex = stepIndex;
        }

        private void PlayStep(float stepVolume)
        {
            // Never the same clip twice in a row, so the steps do not sound looped.
            var clipIndex = Random.Range(0, footstepClips.Length - 1);

            if (clipIndex >= lastClipIndex)
            {
                clipIndex++;
            }

            lastClipIndex = clipIndex;
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(footstepClips[clipIndex], stepVolume);
        }
    }
}
