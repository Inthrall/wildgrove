using UnityEngine;
using UnityEngine.InputSystem;

namespace Wildgrove.Game.Input
{
    /// <summary>
    /// <see cref="IGameInput"/> backed by the new Input System's device layer
    /// (never the legacy <c>UnityEngine.Input</c>). A Tend is any pointer press
    /// (touch or mouse — both surface through <see cref="Pointer.current"/>),
    /// the Space key, or the gamepad South button, matching the design's
    /// "Tending = tap / click / Space / pad-A".
    /// </summary>
    public sealed class InputSystemGameInput : IGameInput
    {
        public bool TendTriggered(out Vector2? screenPosition)
        {
            // Non-positional confirms first: the caller resolves these against
            // the selected node rather than a hit point.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                screenPosition = null;
                return true;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
            {
                screenPosition = null;
                return true;
            }

            // Positional tap/click — Pointer covers both touch and mouse.
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                screenPosition = pointer.position.ReadValue();
                return true;
            }

            screenPosition = null;
            return false;
        }
    }
}
