using UnityEngine;
using UnityEngine.InputSystem;

namespace MicroduckVR.Player
{
    /// <summary>
    /// Input for the player's duck. Quest Touch Pro controllers through OpenXR, with a keyboard
    /// fallback so the scene can be tried on the desktop (WASD walk, Q/E snap turn, Space beak, C chirp, V view).
    /// </summary>
    public class DuckInput
    {
        public readonly InputAction Move, Turn, Beak, Chirp, Skates, Menu, Emote, View;

        public DuckInput()
        {
            Move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            Move.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");

            Turn = new InputAction("Turn", InputActionType.Value, expectedControlType: "Vector2");
            Turn.AddBinding("<XRController>{RightHand}/{Primary2DAxis}");
            Turn.AddCompositeBinding("2DVector").With("Left", "<Keyboard>/q").With("Right", "<Keyboard>/e").With("Up", "<Keyboard>/r").With("Down", "<Keyboard>/f");

            Beak = new InputAction("Beak", InputActionType.Value, expectedControlType: "Axis");
            Beak.AddBinding("<XRController>{RightHand}/trigger");
            Beak.AddBinding("<Keyboard>/space");

            Chirp = new InputAction("Chirp", InputActionType.Button);
            Chirp.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            Chirp.AddBinding("<Keyboard>/c");

            Skates = new InputAction("Skates", InputActionType.Button);
            Skates.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            Skates.AddBinding("<Keyboard>/k");

            Emote = new InputAction("Emote", InputActionType.Button);
            Emote.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
            Emote.AddBinding("<Keyboard>/x");

            View = new InputAction("View", InputActionType.Button);           // first / third person
            View.AddBinding("<XRController>{LeftHand}/{Primary2DAxisClick}");
            View.AddBinding("<Keyboard>/v");

            Menu = new InputAction("Menu", InputActionType.Button);
            Menu.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
            Menu.AddBinding("<Keyboard>/tab");
        }

        public void Enable() { foreach (var a in All) a.Enable(); }
        public void Disable() { foreach (var a in All) a.Disable(); }
        InputAction[] All => new[] { Move, Turn, Beak, Chirp, Skates, Menu, Emote, View };
    }
}
