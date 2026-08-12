using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The sheets the pump raises unbidden: a waystone read, a companion
    /// arrived, a reward handed over by Play Games, a keeping counted, a bond
    /// made, a place at the warden's side opened.
    /// <para>
    /// These are the run's good news, and the order they are raised in is
    /// <see cref="PumpSheets"/>'s business, not theirs. What they share is the
    /// register: each is a meeting or an honour rather than a transaction, so
    /// each leads with a plate and a name and leaves the numbers to the pages.
    /// </para>
    /// </summary>
    internal sealed partial class JournalSheets
    {
        private void OpenWaystoneSheet(ZoneData zone)
        {
            var text = Narrative.WaystoneText(_loop.Data, zone.id);
            // Dismissing IS walking on — the stone must mark itself read, or
            // the pump re-raises it a quarter-second later.
            var sheet = BeginSheet(() =>
            {
                _loop.MarkWaystoneRead(zone.id);
                CloseSheet();
            });
            MakeText(sheet, "A waystone", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, zone.displayName.ToUpperInvariant(), 18, TextAnchor.UpperCenter, Ink2, _smallCaps);
            if (!string.IsNullOrEmpty(text))
            {
                MakeText(sheet, "<i>“" + text + "”</i>", 24, TextAnchor.MiddleCenter, Ink, _serif);
            }

            Button(sheet, "Walk on", 320, () =>
            {
                _loop.MarkWaystoneRead(zone.id);
                CloseSheet();
                // Several stones unread (a cloud-save adoption) page straight
                // through in one sitting instead of materialising one by one
                // on the pump's cadence; the guard still paces each page.
                var next = Narrative.NextUnreadWaystone(_loop.State, _loop.Data);
                if (next != null)
                {
                    OpenWaystoneSheet(next);
                    AddTapGuard(PumpedSheetGuardSeconds);
                }
            });
        }

        /// <summary>
        /// One of the final waystones (design §7). The same furniture as any
        /// stone — it is the same kind of object, and the reveal lands harder
        /// for arriving in the form the warden has read seven times already.
        /// The only addition is the count: the chain has an end, and a player
        /// who cannot see one has no reason to come back up next season.
        /// There is no page-through here, unlike the ordinary stones — the
        /// chain hands over one a fold on purpose, so there is never a second
        /// one waiting behind this one.
        /// </summary>
        private void OpenFinalWaystoneSheet(StringEntry stone)
        {
            var zoneId = _loop.Data.dialogue.finalWaystones.zoneId;
            _loop.Data.ZonesById.TryGetValue(zoneId ?? string.Empty, out var zone);
            var read = _loop.State.finalWaystonesRead + 1;
            var total = Narrative.FinalWaystoneCount(_loop.Data);
            // Reading the first of these is what brings the Record page's Final
            // Waystones entry into being, and that section is decided when the
            // page is BUILT — the count is in no rebuild signature, so without
            // saying the page is dirty here the entry simply doesn't appear until
            // some unrelated change rebuilds the page.
            var sheet = BeginSheet(() =>
            {
                _loop.MarkFinalWaystoneRead();
                _dirty = true;
                CloseSheet();
            });
            MakeText(sheet, "A waystone", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, (zone != null ? zone.displayName.ToUpperInvariant() + " · " : string.Empty)
                            + read + " OF " + total, 18, TextAnchor.UpperCenter, Ink2, _smallCaps);
            MakeText(sheet, "<i>“" + stone.text + "”</i>", 24, TextAnchor.MiddleCenter, Ink, _serif);
            Button(sheet, read >= total ? "Go down" : "Walk on", 320, () =>
            {
                _loop.MarkFinalWaystoneRead();
                _dirty = true;
                CloseSheet();
            });
        }

        private void OpenArrivalSheet(Familiar familiar)
        {
            // Backing out keeps the (free) suggested name — the arrival must
            // resolve either way, or the pump re-raises the sheet at once.
            var sheet = BeginSheet(() =>
            {
                _loop.TakePendingArrival();
                _dirty = true;
                CloseSheet();
            });
            Celebrate(sheet, ArrivalSeeds);
            MakeText(sheet, "A new friend", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "a " + SpeciesName(familiar.speciesId) + " arrives", 22, TextAnchor.UpperCenter, Ink2, _hand);

            // Who it is, in the book's own hand — a name is being asked for, and
            // naming something you can't see is a form filled in, not a meeting.
            var portrait = ArtLibrary.ForSpecies(familiar.speciesId);
            if (portrait != null)
            {
                PlateImage(sheet, portrait, 200f);
            }

            var cost = Mathf.FloorToInt((float)_loop.RenameCost());
            if (cost > 0)
            {
                MakeText(sheet, "keep this name, or choose one for " + SizeOpen(19) + "<color=" + OchreHex + ">"
                                + cost + " amber</color></size>", 16, TextAnchor.UpperCenter, Ink2, _hand);
            }

            var field = MakeInputField(sheet, familiar.name);
            Button(sheet, "Walk together", 320, () =>
            {
                var typed = field.text;
                // Keeping the suggested name is free; choosing a different one
                // asks the rename price (design §4). Short of amber, the
                // suggestion holds — the arrival never stalls on the coffer.
                if (!string.IsNullOrWhiteSpace(typed) && typed.Trim() != familiar.name
                    && !_loop.RenameFamiliar(familiar, typed))
                {
                    SetNote("not enough amber for a chosen name, so the suggestion holds.");
                }

                _loop.TakePendingArrival();
                _dirty = true;
                CloseSheet();
            });
        }

        /// <summary>
        /// The confirmation Google requires for anything granted outside the
        /// game (design §11). The rules are specific and they outrank the
        /// journal's usual reticence: the item must be named plainly, the source
        /// said out loud, there must be no way to decline, and it must stay up
        /// until the player acknowledges it. So the plain line comes first and
        /// the grove's own voice second, there is one button, the scrim is inert,
        /// and backing out with Esc takes the same door as Continue — the reward
        /// is already granted and saved by the time this opens; this is the
        /// telling, not the taking.
        /// </summary>
        private void OpenRewardSheet(RewardGrant grant)
        {
            var sheet = BeginSheet(CloseSheet, scrimDismisses: false);
            MakeText(sheet, "A gift from Play Games", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, grant.statement, 21, TextAnchor.MiddleCenter, Ink, _serif);
            if (!string.IsNullOrEmpty(grant.flavour))
            {
                MakeText(sheet, "<i>" + grant.flavour + "</i>", 20, TextAnchor.MiddleCenter, Ink2, _hand);
            }

            var accept = Button(sheet, "Continue", 320, CloseSheet);
            KeyAction(accept);
        }

        /// <summary>
        /// A keeping's tier landed (design §15) — the fire's answer, said
        /// plainly and once. The sabbat's plate joins the sheet the day the
        /// art pass paints it ("sabbat-{id}" in the ArtLibrary); until then
        /// the words carry it. Draft wording, the narrative pass re-voices.
        /// </summary>
        private void OpenKeepingSheet(string sabbatId, int tier)
        {
            SabbatData sabbat = null;
            var sabbats = _loop.Data.wheel?.sabbats;
            for (var i = 0; sabbats != null && i < sabbats.Count; i++)
            {
                if (sabbats[i].id == sabbatId)
                {
                    sabbat = sabbats[i];
                    break;
                }
            }

            var name = sabbat?.displayName ?? sabbatId;
            var sheet = BeginSheet();
            MakeText(sheet, name + "-tide", 32, TextAnchor.UpperCenter, Ink, _serif);

            var plate = ArtLibrary.ForJournal("sabbat-" + sabbatId);
            if (plate != null)
            {
                PlateImage(sheet, plate, 220f);
            }

            var word = tier >= 3 ? "The wheel is kept." : tier == 2 ? "The day is kept." : "The eve is kept.";
            MakeText(sheet, word, 24, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "<i>set down at the fire, and counted. the year remembers a keeping.</i>",
                18, TextAnchor.MiddleCenter, Ink2, _hand);
            Button(sheet, "Walk on", 320, CloseSheet);
        }

        private void OpenBondSheet(BondData bond)
        {
            // First bond earned unlocks "First kith" (idempotent — later bonds no-op).
            _loop.GameServices.UnlockAchievement(AchievementIds.FirstKith);

            var sheet = BeginSheet();
            // The heaviest drift in the game. A bond is the one thing the fold
            // can't take back, and it was reading like a receipt for it.
            Celebrate(sheet, BondSeeds);
            MakeText(sheet, "A bond is made", 32, TextAnchor.UpperCenter, Ink, _serif);

            var portrait = ArtLibrary.ForSpecies(bond.species);
            if (portrait != null)
            {
                PlateImage(sheet, portrait, 220f);
            }

            MakeText(sheet, bond.displayName, 26, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "will cross every fold with you.", 22, TextAnchor.UpperCenter, Ink2, _serif);
            MakeText(sheet, "<i>the grove keeps few things through a migration. this is one.</i>",
                18, TextAnchor.MiddleCenter, Ink2, _hand);
            Button(sheet, "Walk together", 320, CloseSheet);
        }

        /// <summary>
        /// The warden's own reach widening (design §4) — a verse sung past a
        /// milestone, or a place bought. Nothing announced it before: the count
        /// on the Warden page simply read one higher the next time anyone
        /// looked, which is no way to mark the thing the whole kith is gated on.
        /// <para>
        /// It is an attunement, not an inventory slot. What widens is how many
        /// wild things will keep step with the warden at once — so the sheet is
        /// about the bond first and the number second: it leads with whoever has
        /// been waiting at camp for it, in their own portrait, and its key action
        /// walks THAT companion out. The old sheet counted the idle and then sent
        /// the player off to find the Warden page to do anything about it, which
        /// is where the reward was quietly left unclaimed.
        /// </para>
        /// <para>
        /// It deliberately says nothing about what opens the NEXT place. This is
        /// the one beat in the run where the grove gives without asking, and a
        /// line pointing at the next rung turned it into a shop window — the
        /// Warden page keeps the whole count, and can be read whenever the
        /// player is actually planning rather than being thanked.
        /// </para>
        /// </summary>
        private void OpenKithSlotSheet(int slots)
        {
            var sheet = BeginSheet();
            Celebrate(sheet, SlotSeeds);
            MakeText(sheet, "Something wild comes closer", 34, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "<i>you have kept this grove well enough that the wild has noticed</i>",
                21, TextAnchor.MiddleCenter, Ink2, _hand);

            // The companion is the news; the place is only why. Their own
            // portrait, for the same reason the arrival sheet carries one —
            // this is a meeting, and the hearth alone is furniture.
            var waiting = FirstResting();
            var plate = waiting != null ? ArtLibrary.ForSpecies(waiting.speciesId) : null;
            var portrait = plate != null;
            if (plate == null)
            {
                // No one idle, or a species whose plate isn't drawn yet — the
                // hearth stands in, as the sheet always used to open.
                plate = ArtLibrary.ForBuilding("fire");
            }

            if (plate != null)
            {
                PlateImage(sheet, plate, portrait ? 200f : 180f);
            }

            // Who it is for, before the count: a name lands, a tally doesn't.
            var resting = _loop.KithResting();
            if (waiting != null)
            {
                MakeText(sheet, resting == 1
                        ? waiting.name + " has waited at camp for this."
                        : waiting.name + " and " + (resting == 2 ? "one other wait" : (resting - 1) + " others wait")
                          + " at camp.",
                    24, TextAnchor.UpperCenter, Ink, _serif);

                // What walking them out actually buys, in the same words the
                // post sheet uses for it. "A place opened" is an abstraction;
                // this companion's own trait is the reward in the hand, and it
                // is the difference between being told and being paid.
                var trait = _loop.FamiliarTrait(waiting);
                if (trait != null)
                {
                    MakeText(sheet, "<i>" + trait.displayName.ToLowerInvariant() + ": " + trait.description + "</i>",
                        17, TextAnchor.UpperCenter, MossDeep, _serif);
                }
            }
            else
            {
                MakeText(sheet, "<i>the next to come in from the trees need not wait. there is room beside you now.</i>",
                    19, TextAnchor.MiddleCenter, Ink2, _hand);
            }

            PaintKithPlaces(BuildKithPlaces(sheet, 34f));
            MakeText(sheet, slots + " of " + Kith.SlotsMax(_loop.Data) + " places at your side",
                16, TextAnchor.UpperCenter, Ink2, _smallCaps);

            // The one thing the sheet never said, and the whole reason this is
            // a reward rather than a notice: a place is kept. Coin, camp, tools
            // and every skill level go into the fold — this doesn't (see
            // GameLoop.Fold: slots ride lifetime verses and re-seed intact).
            // Moss, which is this book's ink for what a thing gives.
            MakeText(sheet, "one more may hold a post, and no migration takes it back.",
                19, TextAnchor.MiddleCenter, MossDeep, _serif);

            // The closing line: the number is not the reward, the deepening is.
            MakeText(sheet, "<i>" + BondDeepeningLine(slots) + "</i>",
                20, TextAnchor.MiddleCenter, Ink, _hand);

            if (waiting == null)
            {
                KeyAction(Button(sheet, "Gladly", 320, CloseSheet));
                return;
            }

            // Where the reward actually lands: a place is worth nothing until
            // someone resting is walked out to a post, so the sheet does that
            // rather than describing where it could be done.
            KeyAction(Button(sheet, "Walk with " + waiting.name, 420, () => OpenStationPickSheet(waiting)));
            Button(sheet, "Later", 320, CloseSheet);
        }

        /// <summary>
        /// What the widening is worth to the bond itself, deepening with every
        /// rung — the attunement sheet's closing line. Written per place rather
        /// than once, because the fifth time the grove gives ground must not
        /// read the same as the second: this is the only place in the run that
        /// says out loud how far the wild has come toward the warden.
        /// <para>
        /// <paramref name="slots"/> is the ordinal of the place just opened —
        /// the warden starts holding one, so the first of these ever seen is
        /// the SECOND place. Every line is written to that ordinal and to
        /// nothing else. In particular none of them counts bodies on the trail:
        /// a place is the right to hold a post, not a companion standing in it,
        /// and a bought place can outrun the roster that fills it.
        /// </para>
        /// <para>
        /// The last line is keyed off <see cref="Kith.SlotsMax"/> rather than
        /// off six, so raising the ceiling in <c>economy.json</c> can't have the
        /// grove declare it has nothing left to give with places still to come.
        /// </para>
        /// </summary>
        private string BondDeepeningLine(int slots)
        {
            if (slots >= Kith.SlotsMax(_loop.Data))
            {
                return "the last of them. the wild has nothing further to hold back.";
            }

            switch (slots)
            {
                case 2:
                    return "a second wild thing has chosen your path over its own.";
                case 3:
                    return "a third. the wood has stopped treating you as weather.";
                case 4:
                    return "a fourth. the trees no longer go quiet when you pass.";
                case 5:
                    return "a fifth. few are ever let this far in; the grove has begun to keep you as you keep it.";
                default:
                    // Only reachable if the ceiling is raised past six in
                    // economy.json — count-free on purpose, so an unwritten
                    // rung reads as quiet rather than as the wrong ordinal.
                    return "another, and the grove has begun to keep you as you keep it.";
            }
        }

        /// <summary>The first companion idle at camp, or null — who a newly opened place is for.</summary>
        private Familiar FirstResting()
        {
            foreach (var familiar in _loop.State.roster)
            {
                if (familiar.IsResting)
                {
                    return familiar;
                }
            }

            return null;
        }

        // How heavy the drift is, by how much the moment is worth: a bond is
        // permanent, a place at the warden's side is the thing the whole kith
        // is gated on, an arrival happens most runs. The order is the point —
        // the two that change the run for good drift heaviest.
        private const int BondSeeds = 24;
        private const int SlotSeeds = 20;
        private const int ArrivalSeeds = 8;

        /// <summary>Sow a drift of seed up a sheet — the journal's one celebration, in the ink it reads in.</summary>
        private static void Celebrate(Transform sheet, int seeds)
        {
            Seedfall.Sow(sheet, seeds, new Color(Ink2.r, Ink2.g, Ink2.b, 0.5f));
        }
    }
}
