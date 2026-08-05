using Wildgrove.Sim;

namespace Wildgrove.Game
{
    /// <summary>
    /// The scene side of the run, behind a seam: the live state itself, and the
    /// four things that have to move with it when the whole run is replaced.
    /// <see cref="GameLoop"/> is the only implementation the game ships with —
    /// this exists so <see cref="RunSwap"/>'s ordering can be pinned by an
    /// EditMode test rather than a PlayMode fixture, since a swap done in the
    /// wrong order throws nothing and looks exactly like a swap done right.
    /// </summary>
    public interface IRunHost
    {
        /// <summary>The run in hand — what everything on the scene side is reading, and what a swap puts down in its place.</summary>
        GameState State { get; set; }

        /// <summary>Throw away a deferred absence still being credited: it was crediting the run about to be set aside.</summary>
        void DropCatchUp();

        /// <summary>Credit an absence against the run now in hand, and queue the welcome-back sheet it owes.</summary>
        void CreditAbsence(double awaySeconds);

        /// <summary>Fold in everything the store says this player holds — the run now in hand may predate a purchase or a reward.</summary>
        void SyncStoreEntitlements();

        /// <summary>Write the run to the device and push it to Play Games in the same breath.</summary>
        void SaveAndSync();
    }
}
