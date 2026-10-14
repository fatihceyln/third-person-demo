using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ArrowGame.Gameplay
{
    public sealed class GameMenu : MonoBehaviour
    {
        [SerializeField] private PanelRenderer panelRenderer;
        [SerializeField] private CharacterSelector characterSelector;
        [SerializeField] private InputActionReference menuAction;
        [SerializeField] private InputActionAsset inputActions;
        // The game keeps running while the menu is open; only this map's input (movement and camera) is switched off.
        [SerializeField] private string gameplayActionMap = "Player";
        [SerializeField] private string menuName = "game-menu";
        [SerializeField] private string characterListName = "character-list";
        [SerializeField] private string hiddenClassName = "game-menu--hidden";
        [SerializeField] private string characterButtonClassName = "game-menu__character";
        [SerializeField] private string selectedCharacterClassName = "game-menu__character--selected";

        private readonly List<Button> characterButtons = new List<Button>();
        private VisualElement menu;
        private bool isOpen;

        private void OnEnable()
        {
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void Update()
        {
            if (menuAction.action.WasPressedThisFrame())
            {
                SetOpen(!isOpen);
            }
        }

        private void OnDisable()
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            menu = root.Q(menuName);
            var characterList = root.Q(characterListName);
            characterList.Clear();
            characterButtons.Clear();

            // Buttons come from the selector's list so a new character only needs to be added there.
            for (var index = 0; index < characterSelector.CharacterCount; index++)
            {
                var characterIndex = index;
                var button = new Button(() => SelectCharacter(characterIndex)) { text = characterSelector.GetDisplayName(index) };
                button.AddToClassList(characterButtonClassName);
                characterList.Add(button);
                characterButtons.Add(button);
            }

            RefreshSelection();
            menu.EnableInClassList(hiddenClassName, !isOpen);
        }

        private void SetOpen(bool open)
        {
            isOpen = open;
            menu.EnableInClassList(hiddenClassName, !open);
            var gameplayActions = inputActions.FindActionMap(gameplayActionMap, true);

            if (open)
            {
                gameplayActions.Disable();
            }
            else
            {
                gameplayActions.Enable();
            }

            UnityEngine.Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private void SelectCharacter(int index)
        {
            characterSelector.Select(index);
            RefreshSelection();
            SetOpen(false);
        }

        private void RefreshSelection()
        {
            for (var index = 0; index < characterButtons.Count; index++)
            {
                characterButtons[index].EnableInClassList(selectedCharacterClassName, index == characterSelector.SelectedIndex);
            }
        }
    }
}
