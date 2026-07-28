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
        public bool PointerPressedThisFrame
        {
            get
            {
                var pointer = Pointer.current;
                return pointer != null && pointer.press.wasPressedThisFrame;
            }
        }

        public bool BackTriggered
        {
            get
            {
                // Android delivers the hardware/gesture Back as the Escape key.
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    return true;
                }

                // East (B) is where a pad player looks for the way out of a
                // sheet; without it a controller could open one and not close it.
                var gamepad = Gamepad.current;
                return gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
            }
        }

        /// <summary>How far the left stick must be pushed to count as asking to move.</summary>
        private const float StickDeadzone = 0.5f;

        public bool NavigateHeld
        {
            get
            {
                var keyboard = Keyboard.current;
                if (keyboard != null
                    && (keyboard.upArrowKey.isPressed || keyboard.downArrowKey.isPressed
                        || keyboard.leftArrowKey.isPressed || keyboard.rightArrowKey.isPressed
                        || keyboard.wKey.isPressed || keyboard.sKey.isPressed
                        || keyboard.aKey.isPressed || keyboard.dKey.isPressed))
                {
                    return true;
                }

                var gamepad = Gamepad.current;
                if (gamepad == null)
                {
                    return false;
                }

                return gamepad.dpad.up.isPressed || gamepad.dpad.down.isPressed
                    || gamepad.dpad.left.isPressed || gamepad.dpad.right.isPressed
                    || gamepad.leftStick.ReadValue().sqrMagnitude > StickDeadzone * StickDeadzone;
            }
        }

        public int TabStep
        {
            get
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.eKey.wasPressedThisFrame)
                    {
                        return 1;
                    }

                    if (keyboard.qKey.wasPressedThisFrame)
                    {
                        return -1;
                    }
                }

                var gamepad = Gamepad.current;
                if (gamepad != null)
                {
                    if (gamepad.rightShoulder.wasPressedThisFrame)
                    {
                        return 1;
                    }

                    if (gamepad.leftShoulder.wasPressedThisFrame)
                    {
                        return -1;
                    }
                }

                return 0;
            }
        }

        public bool CatchTriggered
        {
            get
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
                {
                    return true;
                }

                var gamepad = Gamepad.current;
                return gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
            }
        }

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
