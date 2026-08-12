using UnityEngine;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The three naming sheets: a companion, the warden, the camp. One shape,
    /// priced three ways, because what a name is worth depends on how often it
    /// is read.
    /// <para>
    /// All three take an <c>onClosed</c> continuation, so a caller mid-decision
    /// can put its own sheet back up: renaming from the station picker is an
    /// aside, and losing the post being chosen would make the pen cost more than
    /// it offers. All three refuse INSIDE the sheet rather than in the margin
    /// note, which sits under the scrim where a refusal reads as a dead button.
    /// </para>
    /// </summary>
    internal sealed partial class JournalSheets
    {
        /// <summary>
        /// Rename a familiar. <paramref name="onClosed"/> runs after the sheet
        /// closes however it closes — saved, cancelled, or dismissed by the
        /// scrim — so a caller mid-decision can put its own sheet back up. The
        /// station sheet uses it: renaming there is an aside, and losing the
        /// post you were choosing would make the pen cost more than it offers.
        /// </summary>
        internal void OpenNamingSheet(Familiar familiar, System.Action onClosed = null)
        {
            System.Action done = () =>
            {
                CloseSheet();
                if (onClosed != null)
                {
                    onClosed();
                }
            };

            // The dismiss hook takes over closing entirely when it is set (see
            // DismissSheet), so it has to close as well as continue.
            var sheet = onClosed == null ? BeginSheet() : BeginSheet(done);
            MakeText(sheet, "Rename", 32, TextAnchor.UpperCenter, Ink, _serif);

            var cost = Mathf.FloorToInt((float)_loop.RenameCost());
            if (cost > 0)
            {
                MakeText(sheet, "a new name asks " + SizeOpen(19) + "<color=" + OchreHex + ">"
                                + cost + " amber</color></size>", 18, TextAnchor.UpperCenter, Ink2, _hand);
            }

            var field = MakeInputField(sheet, familiar.name);

            // Refusals must land INSIDE the sheet — the margin note sits under
            // the scrim, so "Save did nothing" read as a broken button.
            var error = MakeText(sheet, string.Empty, 16, TextAnchor.MiddleCenter, Ink2, _serif);

            var save = Button(sheet, cost > 0 ? "Save · " + cost + " amber" : "Save", 320, () =>
            {
                var typed = field.text;
                if (string.IsNullOrWhiteSpace(typed) || typed.Trim() == familiar.name)
                {
                    // Nothing changed — no charge, just close.
                    done();
                    return;
                }

                // Amber is premium and hard-won — refuse rather than part-charge.
                if (!_loop.CanRenameFamiliar())
                {
                    error.text = "<color=" + OchreInkHex + "><i>not enough amber. resin is dear, and the old name holds.</i></color>";
                    return;
                }

                if (_loop.RenameFamiliar(familiar, typed))
                {
                    SetNote(cost > 0 ? "a name, paid in resin, set in the journal." : "a new name, set in the journal.");
                    _dirty = true;
                }

                done();
            });
            KeyAction(save);

            Button(sheet, "Cancel", 320, () => done());
        }

        /// <summary>
        /// Name the warden — the player's own body, priced apart from a
        /// companion's because it is bought once and read on every page.
        /// <para>
        /// The field opens EMPTY on a first naming rather than pre-filled with
        /// "the warden": that phrase is the absence of a name, and offering it as
        /// the starting text invites a player to edit it into "the warden of the
        /// north" — a placeholder mistaken for a default. A later change does
        /// pre-fill, because there the text really is their name.
        /// </para>
        /// </summary>
        internal void OpenWardenNamingSheet(System.Action onClosed = null)
        {
            System.Action done = () =>
            {
                CloseSheet();
                if (onClosed != null)
                {
                    onClosed();
                }
            };

            var sheet = onClosed == null ? BeginSheet() : BeginSheet(done);
            var named = _loop.IsWardenNamed();
            MakeText(sheet, named ? "Take a new name" : "Name the warden", 32, TextAnchor.UpperCenter, Ink, _serif);

            var cost = Mathf.FloorToInt((float)_loop.WardenRenameCost());
            if (cost > 0)
            {
                MakeText(sheet, "your own name asks " + SizeOpen(19) + "<color=" + OchreHex + ">"
                                + cost + " amber</color></size>", 18, TextAnchor.UpperCenter, Ink2, _hand);
            }

            // §7 register: the sheet says what the name is FOR, since the price
            // is steep and its effect is diffuse — it changes lines the player
            // is not looking at while they read this one.
            MakeText(sheet, "<i>a name the whole grove will use: every post, every page.</i>",
                16, TextAnchor.UpperCenter, Ink2, _hand);

            var field = MakeInputField(sheet, named ? _loop.WardenName() : string.Empty);
            var error = MakeText(sheet, string.Empty, 16, TextAnchor.MiddleCenter, Ink2, _serif);

            var save = Button(sheet, cost > 0 ? "Save · " + cost + " amber" : "Save", 320, () =>
            {
                var typed = field.text;
                if (string.IsNullOrWhiteSpace(typed) || typed.Trim() == _loop.WardenName())
                {
                    done();
                    return;
                }

                if (!_loop.CanRenameWarden())
                {
                    error.text = "<color=" + OchreInkHex + "><i>not enough amber. resin is dear, and you go unnamed a while longer.</i></color>";
                    return;
                }

                if (_loop.RenameWarden(typed))
                {
                    SetNote(named
                        ? "a new name, paid in resin, set at the front of the journal."
                        : "you set your name at the front of the journal.");
                    _dirty = true;
                }

                done();
            });
            KeyAction(save);

            Button(sheet, "Cancel", 320, () => done());
        }

        /// <summary>
        /// Name this run's camp (design §9's sink slate) — the warden sheet's
        /// sibling, priced between a companion's naming and the warden's own.
        /// The one difference the sheet must say: this name is the RUN's, and
        /// it folds with the camp — a player paying 40 amber deserves to know
        /// the purchase has a season.
        /// </summary>
        internal void OpenCampNamingSheet(System.Action onClosed = null)
        {
            System.Action done = () =>
            {
                CloseSheet();
                if (onClosed != null)
                {
                    onClosed();
                }
            };

            var sheet = onClosed == null ? BeginSheet() : BeginSheet(done);
            var named = _loop.IsCampNamed();
            MakeText(sheet, named ? "Rename the camp" : "Name the camp", 32, TextAnchor.UpperCenter, Ink, _serif);

            var cost = Mathf.FloorToInt((float)_loop.CampNameCost());
            if (cost > 0)
            {
                MakeText(sheet, "the camp's name asks " + SizeOpen(19) + "<color=" + OchreHex + ">"
                                + cost + " amber</color></size>", 18, TextAnchor.UpperCenter, Ink2, _hand);
            }

            // §7 register: say what the name is for, and say what it isn't —
            // it reads on the forecast and the pages, and it folds with the
            // camp when the run ends.
            MakeText(sheet, "<i>a name for this camp, this season: the pages will use it until the fold takes both.</i>",
                16, TextAnchor.UpperCenter, Ink2, _hand);

            var field = MakeInputField(sheet, named ? _loop.CampName() : string.Empty);
            var error = MakeText(sheet, string.Empty, 16, TextAnchor.MiddleCenter, Ink2, _serif);

            var save = Button(sheet, cost > 0 ? "Save · " + cost + " amber" : "Save", 320, () =>
            {
                var typed = field.text;
                if (string.IsNullOrWhiteSpace(typed) || typed.Trim() == _loop.CampName())
                {
                    done();
                    return;
                }

                if (!_loop.CanNameCamp())
                {
                    error.text = "<color=" + OchreInkHex + "><i>not enough amber. resin is dear, and the camp goes unnamed a while longer.</i></color>";
                    return;
                }

                if (_loop.NameCamp(typed))
                {
                    SetNote(cost > 0
                        ? "a name for the camp, paid in resin, written across the season's pages."
                        : "a name for the camp, written across the season's pages.");
                    _dirty = true;
                }

                done();
            });
            KeyAction(save);

            Button(sheet, "Cancel", 320, () => done());
        }
    }
}
