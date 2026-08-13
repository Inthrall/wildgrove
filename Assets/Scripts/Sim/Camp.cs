namespace Wildgrove.Sim
{
    /// <summary>
    /// The camp's name on the page (design §9's sink slate, 2026-08-06):
    /// bought once for Amber, read by the fold forecast, the welcome-back
    /// sheet, and the journal's page headers. The name crosses Migration with
    /// the warden's own (amended 2026-08-13) — a camp re-pitched a region
    /// north is the same camp under the same name.
    /// Naming happens in <see cref="Amber.TryNameCamp"/>; this is the one
    /// answer every surface reads, the same shape as <see cref="Warden"/>.
    /// </summary>
    public static class Camp
    {
        /// <summary>
        /// The unnamed camp's name — what every line calls it until a name is
        /// bought. Lower case because it reads mid-sentence far more often
        /// than it opens one.
        /// </summary>
        public const string Anonymous = "the camp";

        /// <summary>
        /// What to call the camp on the page: its bought name, or
        /// <see cref="Anonymous"/> while it has none. Every surface that names
        /// the camp goes through here, so an unnamed run reads exactly as it
        /// always did and a named one changes everywhere at once.
        /// </summary>
        public static string DisplayName(GameState state)
        {
            var name = state?.campName;
            return string.IsNullOrWhiteSpace(name) ? Anonymous : name.Trim();
        }

        /// <summary>Whether the camp has been named — the naming sheet asks, to tell a first naming from a change.</summary>
        public static bool IsNamed(GameState state)
        {
            return !string.IsNullOrWhiteSpace(state?.campName);
        }
    }
}
