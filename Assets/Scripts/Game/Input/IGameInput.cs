using UnityEngine;

namespace Wildgrove.Game.Input
{
    /// <summary>
    /// The device-agnostic seam for the game's world interactions. The UI layer
    /// asks this for intents ("did the player tend this frame, and where?")
    /// instead of reading <c>UnityEngine.Input</c>, touches or specific devices
    /// directly, so touch, mouse, keyboard and gamepad all flow through one place
    /// (dev-setup.md: "all interactions through an input abstraction from day
    /// one"; design §Phase 2: "brutal to retrofit"). uGUI widgets keep their own
    /// EventSystem-driven pointer/keyboard/gamepad navigation — this covers only
    /// the free-space gestures that aren't a button press.
    /// </summary>
    public interface IGameInput
    {
        /// <summary>
        /// True on the single frame the player asked to Tend. For pointer devices
        /// (touch, mouse) <paramref name="screenPosition"/> is the tap/click point,
        /// so the caller can resolve which node was hit. For non-positional
        /// devices (keyboard Space, gamepad South) it is <c>null</c> and the caller
        /// tends the currently selected node.
        /// </summary>
        bool TendTriggered(out Vector2? screenPosition);

        /// <summary>
        /// True on the single frame a pointer (touch or mouse) pressed, whether
        /// or not it landed on a widget — the UI layer uses this to drop stale
        /// uGUI focus after taps, keeping widget focus a controller-navigation
        /// concept.
        /// </summary>
        bool PointerPressedThisFrame { get; }
    }
}
