using System.Linq;

namespace Wildgrove.Sim.Saves
{
    /// <summary>
    /// The version ladder: one case per rung, each bringing a save up exactly
    /// one version and letting the loop carry it the rest of the way.
    /// <para>
    /// A rung fills in only what its version predates, and never reaches for the
    /// current content data, because a migration has to hold for a save opened
    /// years later. Adding one means a case here and a bump to
    /// <see cref="CurrentVersion"/>, and nothing else: leave
    /// <see cref="EarliestReadableVersion"/> where it is, since raising it is a
    /// decision about whose saves stop working.
    /// </para>
    /// </summary>
    public static partial class SaveCodec
    {
        /// <summary>
        /// Bring an older save up to <see cref="CurrentVersion"/> in place.
        /// Returns false for a save this build must not read: one from a future
        /// build, and one older than <see cref="EarliestReadableVersion"/> —
        /// never guess at a shape this build doesn't know.
        /// </summary>
        public static bool TryMigrate(SaveData save)
        {
            if (save == null || save.version > CurrentVersion || save.version < EarliestReadableVersion)
            {
                return false;
            }

            while (save.version < CurrentVersion)
            {
                switch (save.version)
                {
                    // One case per rung, each bringing a save up exactly one
                    // version and letting the loop carry it the rest of the way.
                    // A step only ever fills in what its version predates; it
                    // never reaches for the current content data, because a
                    // migration has to hold for a save opened years later.
                    //
                    // Retiring the bottom of the ladder means raising
                    // EarliestReadableVersion to match, so a save that can no
                    // longer climb is refused outright rather than half-read.

                    case 42:
                        // v43 added the clock ratchet's high water mark. Left at
                        // zero on purpose — "this run has never been told the
                        // time", which is exactly true of a save written before
                        // the ratchet existed. Stamping it with anything else
                        // would either invent a mark (and freeze the run's
                        // cooldowns until real time passed it) or need the
                        // current clock, which a migration must never reach for.
                        save.version = 43;
                        break;

                    case 43:
                        // v44 added the warden's bought name. Left null: a save
                        // written before the rename existed belongs to a warden
                        // who was never named, and null is exactly how an
                        // un-renamed v44 run reads — every line falls back to
                        // "the warden", which is what that save already said.
                        save.version = 44;
                        break;

                    case 44:
                        // v45 added the camp's bought name (design §9's sink
                        // slate). Left null for the same reason as the
                        // warden's: a save written before camp naming existed
                        // describes a camp that was never named, and null is
                        // exactly how that reads — "the camp", as every line
                        // already said.
                        save.version = 45;
                        break;

                    case 45:
                        // v46 added the drover's consideration pair (window
                        // index + count). Left zero: no consideration was ever
                        // pressed on a save from before bribes existed, and a
                        // zero pair reads exactly as that — the window's plain
                        // first deal.
                        save.version = 46;
                        break;

                    case 46:
                        // v47 added the run's second craft-queue purchase.
                        // Left false: a save from before the queue could be
                        // bought describes a run that never bought it, and
                        // false is exactly how that reads — one order per
                        // station, as it always was.
                        save.version = 47;
                        break;

                    case 47:
                        // v48 added the keepsake pages, and v51 removed them
                        // again — the rung stays because the ladder must not
                        // gap, and there is nothing left to fill in.
                        save.version = 48;
                        break;

                    case 48:
                        // v49 added the Wheel (design §15): the hemisphere
                        // choice and the kept-sabbat claims. Both left at
                        // their defaults — an unset hemisphere is re-derived
                        // from locale by the host, exactly as a fresh run's
                        // is, and no sabbat was ever kept on a save from
                        // before the Wheel turned.
                        save.version = 49;
                        break;

                    case 49:
                        // v50 added the keeping — the tide's own verse. Left
                        // null: no keeping had begun on a save from before it
                        // existed, and Keeping.Current generates one the
                        // moment an open tide is next read.
                        save.version = 50;
                        break;

                    case 50:
                        // v51 dropped the keepsake pages: the sink was removed
                        // (the shelf's whole payoff was one line naming a camp
                        // whose name folds anyway). Nothing to fill in — the
                        // field is gone from SaveData, so a v50 save's
                        // "keepsakes" array is simply not deserialized. The
                        // rung exists so an older build refuses a save this one
                        // wrote rather than reading it a field short.
                        save.version = 51;
                        break;

                    case 51:
                        // v52 moved the kith ladder off the lifetime verse
                        // tally and onto three NAMED verses
                        // (economy.kith.slotVerseZones). A save written before
                        // this has no record of WHICH verses it sang — only how
                        // many — so sungVerseZones is left empty, which is the
                        // honest shape: it says "unknown", not "none".
                        //
                        // What it does know is how many places the old ladder
                        // had earned, and that must not be taken away. The old
                        // milestones were 2 / 5 / 10 lifetime verses; they are
                        // written out here rather than read from the current
                        // economy because a migration must hold for a save
                        // opened years after those numbers stopped existing.
                        // Kith.Slots floors the earned rungs on the result, and
                        // the named verses overtake it as they are sung.
                        //
                        // foldedVersesSung alone undercounts a save folded
                        // mid-run, which can only ever grandfather FEWER places
                        // than were standing — and Restore's own Rite.Settle
                        // sweep records that run's answered verses immediately
                        // after, so the shortfall closes on the first load
                        // wherever those verses were named ones.
                        var earnedUnderTheOldLadder = 0;
                        foreach (var milestone in new[] { 2, 5, 10 })
                        {
                            if (save.foldedVersesSung >= milestone)
                            {
                                earnedUnderTheOldLadder++;
                            }
                        }

                        save.grandfatheredKithSlots = earnedUnderTheOldLadder;
                        save.version = 52;
                        break;

                    case 52:
                        // v53 made the watch a place: one watch post per
                        // observation site ("dig:{zone}") in place of the single
                        // roaming post that watched every site at once. The
                        // station ids are left exactly as written — Restore is
                        // where "wander" is put down at the first open site's
                        // watch, because only the restored run knows which sites
                        // this build opens and a migration must never reach for
                        // the current content data. The rung exists so an older
                        // build refuses a save this one wrote rather than
                        // reading a watch post it would rest on sight.
                        save.version = 53;
                        break;

                    case 53:
                        // v54 renamed the work: a post at an observation site is
                        // SKETCHING, not watching, and its id says so —
                        // "sketch:{zone}" where v53 wrote "dig:{zone}". Unlike
                        // 53, this rung really does rewrite the ids, and it can:
                        // the zone half is carried through untouched, so no
                        // content data is consulted and a site this build no
                        // longer opens is still left for Restore to rest.
                        //
                        // Why the ids moved at all, rather than only the words:
                        // "dig" is excavation vocabulary that outlived
                        // excavation by two renames, and a persisted id nobody
                        // dares touch is how the next reader learns the wrong
                        // noun for the work.
                        RewriteSketchStations(save);
                        save.version = 54;
                        break;

                    case 54:
                        // v55 persists the confirmations a delivered reward is
                        // still owed (design §11). A save written before this
                        // held them in memory alone, so whatever it was owing
                        // died with the process that wrote it: an empty list is
                        // the honest shape and this rung fills nothing in. It
                        // exists so an older build refuses a save this one wrote
                        // rather than reading it a field short.
                        save.version = 55;
                        break;

                    default:
                        // A gap in the ladder is a coding error — refuse rather
                        // than spin.
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Move every observation-site post from the v53 <c>dig:{zone}</c> id to
        /// v54's <c>sketch:{zone}</c> — the roster's own posts and the warden's.
        /// <para>
        /// Deliberately NOT <c>save.stations</c>: that list is the craft queue,
        /// whose <c>stationId</c> is a workbench and not a place a body stands.
        /// The two fields share a name and nothing else, and rewriting the wrong
        /// one would empty a player's queue on load without erroring.
        /// </para>
        /// </summary>
        private static void RewriteSketchStations(SaveData save)
        {
            if (save.roster != null)
            {
                foreach (var familiar in save.roster)
                {
                    if (familiar != null)
                    {
                        familiar.stationId = RewriteSketchStation(familiar.stationId);
                    }
                }
            }

            save.wardenPostNodeId = RewriteSketchStation(save.wardenPostNodeId);
        }

        /// <summary>
        /// One post id carried across the v54 rename, or handed back untouched.
        /// Anything that is not a v53 site post — a node id, the pony's lane, the
        /// retired roaming <c>wander</c>, null — passes through, because this rung
        /// renames one thing and must not be the place a second meaning is
        /// invented.
        /// </summary>
        private static string RewriteSketchStation(string stationId)
        {
            if (string.IsNullOrEmpty(stationId)
                || !stationId.StartsWith(Familiar.LegacyWatchStationPrefix))
            {
                return stationId;
            }

            var zoneId = stationId.Substring(Familiar.LegacyWatchStationPrefix.Length);
            return Familiar.SketchStation(zoneId);
        }
    }
}
