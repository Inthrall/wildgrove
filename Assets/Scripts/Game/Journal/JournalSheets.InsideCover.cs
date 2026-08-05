using UnityEngine;
using UnityEngine.UI;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The inside cover — the journal's settings surface. A book keeps its
    /// practical matter inside the back board: where the book is kept, what it
    /// tells anyone, what was paid for, who drew the plates, and the way to
    /// begin a new one. None of that is the run, which is why none of it is a
    /// page.
    /// <para>
    /// It is a sheet rather than a fifth tab on purpose: the chrome budget rule
    /// (a bar is pinned only if it is read on every tab) applies twice over to
    /// a tab, and a settings screen is read about once a month. It opens from
    /// the last card of the last page.
    /// </para>
    /// <para>
    /// Sheets are snapshots — nothing here registers a live updater, because
    /// the page's updaters are cleared with the body they belong to and would
    /// outlive a closed sheet. The lines that can change while the sheet is
    /// open are re-read by the buttons that change them.
    /// </para>
    /// </summary>
    internal sealed partial class JournalSheets
    {
        /// <summary>
        /// The privacy policy, hosted rather than carried: it is the same URL the
        /// Play listing gives, so there is one copy to keep true instead of two
        /// that drift — and a policy shipped inside a build can only be corrected
        /// by shipping another. Play expects an in-app link once ads and consent
        /// are in play, which is what the row below is.
        /// </summary>
        private const string PrivacyPolicyUrl = "https://decryptic.app/wildgrove/privacy";

        /// <summary>
        /// How long "Begin a new book" stays dead after the question opens. The
        /// only hold in the game, on the only tap that destroys something the
        /// player cannot earn back: long enough that the warning above it is read
        /// rather than tapped past, short enough not to read as a broken button.
        /// </summary>
        private const float StartAgainHoldSeconds = 3f;

        internal void OpenInsideCoverSheet()
        {
            var sheet = BeginSheet();
            MakeText(sheet, "The Inside Cover", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "where the book is kept, and whose hands are in it",
                17, TextAnchor.UpperCenter, Ink2, _hand);

            BuildKeeping(sheet);
            BuildTelling(sheet);
            BuildBought(sheet);
            BuildColophon(sheet);
            BuildStartingAgain(sheet);

            // The build, for the one conversation where it matters: a player
            // reporting something that went wrong.
            MakeHairline((RectTransform)sheet);
            MakeText(sheet, "Wildgrove " + Application.version, 13, TextAnchor.MiddleCenter, Ink2);

            var done = Button(sheet, "Close the book", 320, CloseSheet);
            KeyAction(done);
        }

        /// <summary>A ruled break and the section's name, in the chrome's small caps.</summary>
        private void SheetSection(Transform sheet, string head)
        {
            MakeHairline((RectTransform)sheet);
            MakeText(sheet, head, 15, TextAnchor.MiddleCenter, Ink2, _smallCaps);
        }

        /// <summary>
        /// Where the run lives. Both lines were true and unsaid before this: the
        /// book is written every half minute and mirrored to Play Games each
        /// time, and a mirror that fails does so in silence.
        /// </summary>
        private void BuildKeeping(Transform sheet)
        {
            SheetSection(sheet, "THE KEEPING");

            var written = MakeText(sheet, string.Empty, 18, TextAnchor.UpperLeft, Ink, _serif);
            var cloud = MakeText(sheet, string.Empty, 16, TextAnchor.UpperLeft, Ink2, _serif);

            void Reread()
            {
                written.text = SaveStanding.WrittenLine(_loop.LastSavedUnixMs, _loop.NowUnixMs());
                cloud.text = SaveStanding.CloudLine(_loop.GameServices.IsSignedIn,
                    _loop.CloudWriteFailed, _loop.AdoptedFromCloud);
            }

            Reread();

            Button write = null;
            write = Button(sheet, "Write it down now", 320, () =>
            {
                _loop.SaveNow();
                Reread();
                Flash(write, "written down", true);
            });

            if (_loop.GameServices.IsSignedIn)
            {
                return;
            }

            // Signed out is the one state the player can do something about
            // from here — the cloud line above says what it costs them.
            Button signIn = null;
            signIn = Button(sheet, "Sign in to Play Games", 320, () =>
            {
                Flash(signIn, "asking Play Games", true);
                _loop.GameServices.SignInInteractive(signedIn =>
                {
                    // Play Games can take its time, and the player can shut the
                    // cover while it does — everything below writes to widgets
                    // this sheet owns, so a closed sheet means there is nothing
                    // left to tell. The sign-in itself has already taken effect.
                    if (signIn == null)
                    {
                        return;
                    }

                    Reread();
                    if (signedIn)
                    {
                        SetButtonLabel(signIn, "Signed in");
                        return;
                    }

                    Flash(signIn, "Play Games didn't answer", false);
                });
            });
        }

        /// <summary>
        /// What leaves the device. The analytics choice is ours to keep; the ad
        /// one belongs to Google's consent form, which stores its own answer —
        /// so this offers the way back to that form rather than a second switch
        /// that would disagree with it.
        /// </summary>
        private void BuildTelling(Transform sheet)
        {
            SheetSection(sheet, "WHAT THIS BOOK TELLS US");

            MakeText(sheet,
                "Notes on how the game is played — which trails open, where a run stops — "
                + "go back to us so it can be made better. Nothing about you, and nothing you have named.",
                16, TextAnchor.UpperLeft, Ink2, _serif);

            Button share = null;
            share = Button(sheet, ShareLabel(), 460, () =>
            {
                var sharing = !_loop.Preferences.ShareAnalytics;
                _loop.Preferences.ShareAnalytics = sharing;
                _loop.Telemetry.SetCollectionEnabled(sharing);
                SetButtonLabel(share, ShareLabel());
                Flash(share, sharing ? "shared" : "kept here", true);
            });

            MakeText(sheet,
                "<i>Crash reports are sent whatever this says. They carry nothing of the run, "
                + "and a build that cannot report its own faults cannot be mended.</i>",
                14, TextAnchor.UpperLeft, Ink2, _serif);

            // Plainly named rather than written in the grove's voice: it is the
            // one line here a player might be looking for by its real name.
            Button policy = null;
            policy = Button(sheet, "The privacy policy in full", 460, () =>
            {
                Application.OpenURL(PrivacyPolicyUrl);
                Flash(policy, "opening", true);
            });

            if (!_loop.Ads.PrivacyOptionsAvailable)
            {
                // No form was ever required of this player, so there is nothing
                // to re-open — and a button that opens nothing reads as broken.
                return;
            }

            Button privacy = null;
            privacy = Button(sheet, "Ad privacy choices", 320,
                () => _loop.Ads.ShowPrivacyOptions(() => Flash(privacy, "noted", true)));
        }

        /// <summary>
        /// Restoring what was paid for. Entitlements are asked of the store
        /// rather than trusted to the save (a reinstall, a second device, a
        /// cloud save older than the purchase), and until now the only thing
        /// that asked was the launch — so a player whose slot didn't come back
        /// had nothing to press.
        /// </summary>
        private void BuildBought(Transform sheet)
        {
            SheetSection(sheet, "WHAT WAS BOUGHT");

            MakeText(sheet,
                "Anything bought is held by Play, not by this device — a new phone, or this one wiped, "
                + "gets it all back.",
                16, TextAnchor.UpperLeft, Ink2, _serif);

            Button restore = null;
            restore = Button(sheet, "Restore what was bought", 320, () =>
            {
                Flash(restore, "asking Play", true);

                // Flash "asked and answered" only when Play actually answered:
                // it is the one reading a player with a missing purchase must
                // not be given, because it says the store looked.
                _loop.Store.RestorePurchases(answered => Flash(restore,
                    answered ? "asked and answered" : "Play didn't answer", answered));
            });
        }

        /// <summary>
        /// The colophon — the plates and the hands that drew them, moved here
        /// from its own card on the Record page. The five CC BY works are named
        /// in full with their licence and the change made to them, because that
        /// is what the licence asks of a shipped build; the public-domain
        /// remainder is thanked without obligation.
        /// </summary>
        private void BuildColophon(Transform sheet)
        {
            SheetSection(sheet, "THE COLOPHON");
            MakeText(sheet, ArtCredits.Preamble, 16, TextAnchor.UpperLeft, Ink2, _serif);

            foreach (var work in ArtCredits.Licensed)
            {
                MakeText(sheet, ArtCredits.Line(work), 14, TextAnchor.UpperLeft, Ink, _serif);
            }

            MakeText(sheet, ArtCredits.PublicDomainNote, 14, TextAnchor.UpperLeft, Ink2, _serif);
        }

        /// <summary>
        /// A new book. Behind a worded confirm because it is the only button in
        /// the game that destroys something the player cannot earn back — and
        /// it reaches the cloud copy too, which the wording has to say or a
        /// second device would quietly hand the old run back.
        /// </summary>
        private void BuildStartingAgain(Transform sheet)
        {
            SheetSection(sheet, "STARTING AGAIN");

            MakeText(sheet,
                "A new book opens at the first camp with nothing in it. This one — every fold, "
                + "every companion, every page — is struck out here and with Play Games, and cannot be had back. "
                + "What was paid for stays yours.",
                16, TextAnchor.UpperLeft, Ink2, _serif);

            Button(sheet, "Start the book again", 320, () =>
            {
                // One sheet at a time: this one goes before the question opens,
                // or its panel would be orphaned behind the confirm.
                CloseSheet();
                OpenConfirmSheet("Start again?",
                    "<b><color=" + OchreHex + ">This cannot be undone.</color></b>\n"
                    + "Every fold, every companion, every page of this book is struck out — here and with Play Games, "
                    + "so no other device can hand it back. Nothing of this run is kept, and nothing of it can be found again.\n\n"
                    + "<i>What was paid for stays yours.</i>",
                    "Begin a new book",
                    () =>
                    {
                        _loop.StartAgain();
                        _hud.ForgetTeaching();
                        // Which zones the player had folded shut is a reading
                        // position in the old book, not a setting. Left standing,
                        // a zone folded away last run opens the new book already
                        // shut — in a book that is meant to have nothing in it.
                        _zoneOpen.Clear();
                        _dirty = true;
                        SetNote("the first camp, and nothing in it but the morning.");
                    },
                    StartAgainHoldSeconds);
            });
        }

        private string ShareLabel()
        {
            return _loop.Preferences.ShareAnalytics
                ? "Play notes: shared"
                : "Play notes: kept to this device";
        }
    }
}
