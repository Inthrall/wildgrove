using Wildgrove.Sim.Saves;

namespace Wildgrove.Game
{
    /// <summary>
    /// The save slot on this device, behind a seam so the run's persistence
    /// rules can be exercised without a disk. <see cref="SaveFile"/> keeps the
    /// awkward parts (the atomic replace, setting a corrupt save aside).
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>Read the slot. False when there is nothing to load (or it was set aside as unreadable).</summary>
        bool TryLoad(out SaveData save);

        /// <summary>Write the slot, replacing whatever was there.</summary>
        void Write(SaveData save);
    }

    /// <summary>The real slot: save.json under the app's persistent data path.</summary>
    public sealed class SaveFileStore : ISaveStore
    {
        public bool TryLoad(out SaveData save)
        {
            return SaveFile.TryLoad(out save);
        }

        public void Write(SaveData save)
        {
            SaveFile.Write(save);
        }
    }
}
