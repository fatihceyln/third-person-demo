using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArrowGame.Gameplay
{
    public sealed class CharacterSelector : MonoBehaviour
    {
        [Serializable]
        private sealed class Character
        {
            [SerializeField] private string displayName;
            // Each model carries its own Animator and PlayerAnimator, so an inactive model stops animating on its own.
            [SerializeField] private GameObject model;
            [SerializeField, Min(0.1f)] private float standingHeight = 2f;
            [SerializeField, Min(0.1f)] private float crouchHeight = 1.75f;
            [SerializeField, Min(0.1f)] private float proneHeight = 0.8f;
            [SerializeField, Min(0.01f)] private float radius = 0.4f;
            // Measured from the Player's pivot, which sits 1 m above the feet, so a small character needs a negative value.
            [SerializeField] private float cameraHeight = 0.4f;

            public string DisplayName => displayName;
            public GameObject Model => model;
            public float StandingHeight => standingHeight;
            public float CrouchHeight => crouchHeight;
            public float ProneHeight => proneHeight;
            public float Radius => radius;
            public float CameraHeight => cameraHeight;
        }

        [SerializeField] private PlayerController playerController;
        [SerializeField] private ThirdPersonCamera thirdPersonCamera;
        [SerializeField] private ParticleSystem switchEffect;
        [SerializeField] private InputActionReference changeCharacterAction;
        [SerializeField] private Character[] characters;

        public int CharacterCount => characters.Length;
        public int SelectedIndex { get; private set; }

        private void Awake()
        {
            Show(SelectedIndex);
        }

        private void Update()
        {
            if (changeCharacterAction.action.WasPressedThisFrame())
            {
                Select((SelectedIndex + 1) % characters.Length);
            }
        }

        public string GetDisplayName(int index)
        {
            return characters[index].DisplayName;
        }

        public void Select(int index)
        {
            if (index == SelectedIndex)
            {
                return;
            }

            Show(index);
            switchEffect.Play();
        }

        private void Show(int index)
        {
            for (var characterIndex = 0; characterIndex < characters.Length; characterIndex++)
            {
                characters[characterIndex].Model.SetActive(characterIndex == index);
            }

            var selected = characters[index];
            playerController.SetBodySize(selected.StandingHeight, selected.CrouchHeight, selected.ProneHeight, selected.Radius);
            thirdPersonCamera.SetTargetHeight(selected.CameraHeight);
            SelectedIndex = index;
        }
    }
}
