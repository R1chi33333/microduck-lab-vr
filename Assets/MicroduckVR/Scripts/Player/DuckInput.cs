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
        /// <summary>The player's actions: walk, turn, beak, chirp, skates, bag, emote, view and recentre.</summary>
        public readonly InputAction Move, Turn, Beak, Chirp, Skates, Menu, Emote, View, Recenter;

        /// <summary>Makes the actions with their controller and keyboard bindings.</summary>
        public DuckInput()
        {
            Move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            Move.AddBinding(InputPaths.LeftStick);
            Move.AddCompositeBinding("2DVector").With("Up", InputPaths.KeyW).With("Down", InputPaths.KeyS).With("Left", InputPaths.KeyA).With("Right", InputPaths.KeyD);

            Turn = new InputAction("Turn", InputActionType.Value, expectedControlType: "Vector2");
            Turn.AddBinding(InputPaths.RightStick);
            Turn.AddCompositeBinding("2DVector").With("Left", InputPaths.KeyQ).With("Right", InputPaths.KeyE).With("Up", InputPaths.KeyR).With("Down", InputPaths.KeyF);

            Beak = new InputAction("Beak", InputActionType.Value, expectedControlType: "Axis");
            Beak.AddBinding(InputPaths.RightTrigger);
            Beak.AddBinding(InputPaths.KeySpace);

            Chirp = new InputAction("Chirp", InputActionType.Button);
            Chirp.AddBinding(InputPaths.RightPrimary);
            Chirp.AddBinding(InputPaths.KeyC);

            Skates = new InputAction("Skates", InputActionType.Button);
            Skates.AddBinding(InputPaths.RightSecondary);
            Skates.AddBinding(InputPaths.KeyK);

            Emote = new InputAction("Emote", InputActionType.Button);
            Emote.AddBinding(InputPaths.LeftPrimary);
            Emote.AddBinding(InputPaths.KeyX);

            View = new InputAction("View", InputActionType.Button);           // first / third person
            View.AddBinding(InputPaths.LeftStickClick);
            View.AddBinding(InputPaths.KeyV);

            Recenter = new InputAction("Recenter", InputActionType.Button);   // third person: swing the view behind the duck
            Recenter.AddBinding(InputPaths.RightStickClick);
            Recenter.AddBinding(InputPaths.KeyT);

            Menu = new InputAction("Menu", InputActionType.Button);
            Menu.AddBinding(InputPaths.LeftSecondary);
            Menu.AddBinding(InputPaths.KeyTab);
        }

        /// <summary>Turns every action on.</summary>
        public void Enable() { foreach (var a in All) a.Enable(); }
        /// <summary>Turns every action off.</summary>
        public void Disable() { foreach (var a in All) a.Disable(); }
        InputAction[] All => new[] { Move, Turn, Beak, Chirp, Skates, Menu, Emote, View, Recenter };
    }
}
