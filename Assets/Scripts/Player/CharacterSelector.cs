using System;
using UnityEngine;

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

            public string DisplayName => displayName;
            public GameObject Model => model;
        }

        [SerializeField] private Character[] characters;

        public int CharacterCount => characters.Length;
        public int SelectedIndex { get; private set; }

        private void Awake()
        {
            Select(SelectedIndex);
        }

        public string GetDisplayName(int index)
        {
            return characters[index].DisplayName;
        }

        public void Select(int index)
        {
            for (var characterIndex = 0; characterIndex < characters.Length; characterIndex++)
            {
                characters[characterIndex].Model.SetActive(characterIndex == index);
            }

            SelectedIndex = index;
        }
    }
}
