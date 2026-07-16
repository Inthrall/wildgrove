namespace Wildgrove.Data
{
    /// <summary>What earns a bond: a completed Museum set or an owned Almanac node.</summary>
    public sealed class BondSourceDef
    {
        /// <summary>"museumSet" or "almanacNode".</summary>
        public string Type { get; set; }

        /// <summary>The id of the set or node that grants this companion.</summary>
        public string Id { get; set; }
    }

    /// <summary>
    /// One bonded familiar (design doc §7): a permanent companion earned —
    /// never bought — from exactly one source, role-locked, crossing every
    /// Migration with the warden.
    /// </summary>
    public sealed class BondDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>"gatherer" or "carrier" — a carrier bonds as a carrier.</summary>
        public string Role { get; set; }

        public BondSourceDef Source { get; set; }
    }
}
