using System.Collections;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    // The open page itself: deciding when its structure has actually changed
    // (and so when a rebuild is owed rather than a refresh), building the
    // current tab's cards through the section builders, folding zones,
    // scrolling to a landmark, and the small flash a card gives when something
    // lands on it.
    public sealed partial class GameHud
    {
        // ─────────────────────────── Body ────────────────────────────────────

        private string StructureSignature()
        {
            var state = _loop.State;
            var owned = state.purchasedUpgradeIds.Count;
            var recipes = _loop.AvailableRecipes().Count;
            var buildings = 0;
            foreach (var pair in state.buildingLevels)
            {
                buildings += pair.Value;
            }

            var choice = 0;
            foreach (var pair in state.choiceResources)
            {
                if (pair.Value > BigDouble.Zero)
                {
                    choice++;
                }
            }

            var recordedInsects = 0;
            foreach (var insect in _loop.Data.insects)
            {
                if (Insects.IsRecorded(state, insect))
                {
                    recordedInsects++;
                }
            }

            var revealedVerses = 0;
            var rite = Rite.CurrentRite(state, _loop.Data);
            if (rite != null && rite.verses != null)
            {
                foreach (var verse in rite.verses)
                {
                    if (Rite.IsVerseRevealed(state, _loop.Data, verse))
                    {
                        revealedVerses++;
                    }
                }
            }

            // Sung count as well as revealed: completing the rite's LAST verse
            // reveals nothing new, so without this the Trail never rebuilds and
            // the finished verse stands as a full card — flipped to "sung" by
            // its live updater — instead of collapsing into SUNG VERSES.
            var sungVerses = Rite.CompletedVerseCount(state, _loop.Data);

            // Which sabbat holds the wheel: the seasons run end to end (design
            // §15), so the midnight one takes over from another is a change of
            // page and nothing else here moves at it. Without this the Trail
            // keeps the card it built — the old season's name and plate over the
            // new season's slots, which the rows would swap under it live.
            var season = _loop.OpenTide()?.id ?? string.Empty;

            return _tab + "/" + season + "/" + state.roster.Count + "/" + state.nodes.Count + "/" + state.digSites.Count
                   + "/" + owned + "/" + recipes + "/" + buildings + "/" + choice
                   + "/" + _loop.UnlockedSkills().Count + "/" + state.gearBySlot.Count
                   + "/" + state.fixedResources.Count + "/" + recordedInsects
                   + "/" + state.builtPlanters.Count + "/" + revealedVerses + "/" + sungVerses
                   + "/" + Compendium.DiscoveredCount(state, _loop.Data);
        }

        private void RebuildBody()
        {
            // A structure change mid-read shouldn't snap the reader back to
            // the top of the page — keep the scroll unless the tab changed.
            // In the page's own units rather than as a fraction of it: see
            // JournalNav.KeptPosition for why the fraction was the page
            // sliding out from under the tap that changed it.
            var keepOffset = _builtTab == _tab ? ScrolledOffset() : 0f;
            _builtTab = _tab;
            var landmark = _pendingScroll;
            _pendingScroll = null;
            _firstVerseCard = null;
            _firstKeepingCard = null;
            _amberCard = null;
            AnchoredHeading = null;

            // Where the focus mark stood, before the page under it is destroyed.
            // Only the page's own mark goes: a rebuild can happen under an open
            // sheet, and the sheet's marked control is still standing.
            RememberFocus();
            if (_focused != null && _body != null && _focused.transform.IsChildOf(_body))
            {
                MarkFocus(null);
            }

            _liveUpdaters.Clear();
            _frameUpdaters.Clear();
            _tendFlashes.Clear();
            ClearPage(_body);

            if (_wide)
            {
                // A spread: the open page on the left, the Trail always facing
                // it. The Trail keeps the land in view while the player works
                // the Camp or the Record — which is the whole point of the
                // wide layout, and why the Trail has no tab here.
                //
                // Each page names itself over its own column, the way the mock's
                // do — the Trail because it has no lit tab to name it here, the
                // open page so that the two of them start on the same line.
                BuildSpreadColumns(out var left, out var right);
                SetPageColumn(left);
                RunningHead(left, _tab);
                BuildPage(_tab);
                SetPageColumn(right);
                RunningHead(right, TabTrail);
                _trail.BuildTrailPage();
            }
            else
            {
                SetPageColumn(_body);
                // In a column the head is only worth a line where it is a NAME
                // — the page's own title says the rest, one row above.
                RunningHead(_body, _tab);
                BuildPage(_tab);
            }

            SetPageColumn(null);
            // The page has drawn its headings, so the anchor has found its
            // landmark (or the section is gone) — the id has done its job.
            PendingAnchor = null;
            // The page's words before its measure. A great many labels are
            // built EMPTY and take their text from the live pass — every
            // building line and every rung of the ladder is two or three
            // unwritten lines at this point — so a page measured before that
            // pass has run is missing most of its height. The settle below
            // would pin the scroll against a page far shorter than the one
            // about to be drawn, and the words arriving would shove it
            // somewhere else a frame later: the lurch this used to give.
            RunLiveUpdaters();
            StartCoroutine(SettleScroll(keepOffset, landmark));
        }

        /// <summary>Take a page's cards off it, ahead of drawing them again.</summary>
        private void ClearPage(RectTransform page)
        {
            if (page == null)
            {
                return;
            }

            for (var i = page.childCount - 1; i >= 0; i--)
            {
                var child = page.GetChild(i).gameObject;
                // Off before Destroy: a destroyed child holds its place in the
                // layout until end of frame, so the old and new pages would
                // share the body for one rendered frame — a visible stutter on
                // every rebuild. Inactive, it leaves the layout at once, and
                // the scroll can settle before this frame draws.
                child.SetActive(false);
                Destroy(child);
            }
        }

        /// <summary>Repaint every live label and plate on the open page.</summary>
        private void RunLiveUpdaters()
        {
            for (var i = 0; i < _liveUpdaters.Count; i++)
            {
                _liveUpdaters[i]();
            }
        }

        /// <summary>
        /// How far down a page the window's top edge sits, in the page's own
        /// units — what <see cref="JournalNav.KeptPosition"/> puts back.
        /// </summary>
        private float ScrolledOffset()
        {
            if (_scroll == null || _body == null)
            {
                return 0f;
            }

            var range = _body.rect.height - _scroll.viewport.rect.height;
            if (range <= 0f)
            {
                return 0f;
            }

            return (1f - Mathf.Clamp01(_scroll.verticalNormalizedPosition)) * range;
        }

        /// <summary>Build one page into whichever column is currently open.</summary>
        private void BuildPage(string tab)
        {
            switch (tab)
            {
                case TabCamp:
                    _camp.BuildCampPage();
                    break;
                case TabStores:
                    _stores.BuildStoresPage();
                    break;
                case TabWarden:
                    _warden.BuildWardenPage();
                    break;
                case TabRecord:
                    _record.BuildRecordPage();
                    break;
                default:
                    _trail.BuildTrailPage();
                    break;
            }
        }

        /// <summary>
        /// Fold a zone open or shut, and rebuild the page around its heading.
        /// <para>
        /// The landmark is the point. A rebuild otherwise keeps the scroll's
        /// <em>normalised</em> position, which is a different place on the page
        /// once the page has changed height — so opening a ground near the
        /// bottom would fling the reader somewhere else entirely, and the
        /// ground they just opened would be off-screen. The heading's spot in
        /// the viewport is measured at the press and restored after the
        /// rebuild, so the ground folds open exactly where it stands rather
        /// than leaping to the top of the view.
        /// </para>
        /// </summary>
        internal void FoldZone(string zoneId, RectTransform heading)
        {
            JournalZones.Toggle(ZoneOpen, zoneId, _trail.NewestZoneId());
            KeepInPlace(zoneId, heading);
        }

        /// <summary>
        /// Fold one of the journal's cards open or shut. The landmark matters
        /// more here than it does on the Trail's grounds, not less: the back
        /// pages are the longest scroll in the book, the Compendium alone
        /// changes the page's height by a drawer of plates, and the keeping by
        /// most of a phone screen.
        /// </summary>
        internal void FoldCard(string cardId, RectTransform heading)
        {
            JournalCardFolds.Toggle(CardOpen, cardId);
            KeepInPlace(cardId, heading);
        }

        /// <summary>
        /// <see cref="FoldCard(string,RectTransform)"/> for a card that decides
        /// its own unasked default — the Camp page's stations, where the default
        /// is which station the work is standing at.
        /// </summary>
        internal void FoldCard(string cardId, RectTransform heading, bool openUnasked)
        {
            JournalCardFolds.Toggle(CardOpen, cardId, openUnasked);
            KeepInPlace(cardId, heading);
        }

        /// <summary>
        /// Rebuild the page around <paramref name="heading"/>, and put it back
        /// where it stood — for a press whose answer changes the page ABOVE
        /// itself, which is the one thing keeping the scrolled distance cannot
        /// absorb (see <see cref="JournalNav.KeptPosition"/>). Taking up a rung
        /// that opens a station is the plainest case: the Ladder sits under the
        /// crafting cards, so a whole card arrives above it and the rung the
        /// finger is still on walks off the bottom of the screen.
        /// <para>
        /// The id is how the landmark survives the rebuild that destroys it: the
        /// fresh page calls <see cref="MarkAnchor"/> as it draws, and whichever
        /// rect answers to this id becomes the one the settle scrolls to.
        /// </para>
        /// </summary>
        internal void KeepInPlace(string anchorId, RectTransform heading)
        {
            PendingAnchor = anchorId;
            _anchorOffset = HeadingViewportOffset(heading);
            _pendingScroll = AnchorLandmark;
            Dirty = true;
        }

        /// <summary>
        /// Offer <paramref name="target"/> as the landmark for
        /// <paramref name="anchorId"/> while the page redraws — it is only kept
        /// if that is the id the rebuild is anchored on.
        /// </summary>
        internal void MarkAnchor(string anchorId, RectTransform target)
        {
            if (anchorId != null && anchorId == PendingAnchor)
            {
                AnchoredHeading = target;
            }
        }

        /// <summary>Distance from the viewport's top edge down to <paramref name="heading"/>'s top edge — where the settle puts it back.</summary>
        private float HeadingViewportOffset(RectTransform heading)
        {
            if (heading == null || _scroll == null)
            {
                return LandmarkMargin;
            }

            var viewport = _scroll.viewport;
            var local = (Vector2)viewport.InverseTransformPoint(heading.position);
            return viewport.rect.yMax - (local.y + heading.rect.yMax);
        }

        /// <summary>
        /// <see cref="ScrollToCard"/> for the sheets — an event popup ends
        /// in the card that answers it, and the Trail is a long scroll to be
        /// dropped into the top of.
        /// </summary>
        internal void GoToTrail(string landmark) => ScrollToCard(TabTrail, landmark);

        /// <summary>
        /// <see cref="GoToTrail"/> for the pages that are not the Trail: the
        /// cache's sheet ends at the amber card, which is most of the Camp page
        /// further down.
        /// </summary>
        internal void GoToCard(string tab, string landmark) => ScrollToCard(tab, landmark);

        /// <summary>
        /// Open a tab and bring a landmark card ("verse", the keeping, the
        /// amber) into view — the trackers and the sheets deep-link into pages
        /// that are otherwise a long scroll.
        /// </summary>
        private void ScrollToCard(string tab, string landmark)
        {
            // A door that ends in a shut drawer is not a door. The keeping folds
            // away by default (see JournalCardFolds), and both the things that
            // link to it — the tracker's tide row and the tide sheet's own
            // button — are asking for the slots, not for the head that hides
            // them. Opening it here rather than defaulting it open keeps the
            // player's own fold: once they shut it, only another link reopens it.
            if (landmark == JournalCardFolds.Keeping
                && !JournalCardFolds.IsOpen(CardOpen, JournalCardFolds.Keeping))
            {
                CardOpen[JournalCardFolds.Keeping] = true;
                Dirty = true;
            }

            _pendingScroll = landmark;
            OpenTab(tab);
            if (!_dirty)
            {
                // Already looking at the Trail with no rebuild coming — jump
                // now. On a spread that means the right page moves and the
                // open page beside it does not, which is the whole reason the
                // two carry their own scrolls.
                StartCoroutine(SettleScroll(ScrolledOffset(), _pendingScroll));
                _pendingScroll = null;
            }
        }

        /// <summary>The landmark name a kept place — a fold's heading, or the card a press was answered on — scrolls back to.</summary>
        private const string AnchorLandmark = "anchor";

        /// <summary>The small breath a deep-linked landmark keeps from the viewport's top edge.</summary>
        private const float LandmarkMargin = 6f;

        /// <summary>Where the pressed heading stood in the viewport — the settle restores it there.</summary>
        private float _anchorOffset = LandmarkMargin;

        private RectTransform LandmarkCard(string landmark)
        {
            switch (landmark)
            {
                case "verse":
                    return _firstVerseCard;
                case JournalCardFolds.Keeping:
                    return _firstKeepingCard;
                case "amber":
                    return _amberCard;
                case AnchorLandmark:
                    return AnchoredHeading;
                default:
                    return null;
            }
        }

        private System.Collections.IEnumerator SettleScroll(float offsetFromTop, string landmark)
        {
            // Once now — the rebuild switches its stale children off before
            // Destroy, so the fresh page's height is already real and the
            // scroll can land before this frame ever draws...
            Canvas.ForceUpdateCanvases();
            ApplyScroll(offsetFromTop, landmark);

            // ...and once a frame later, for anything the first layout pass
            // settled late.
            yield return null;
            if (_scroll == null || _body == null)
            {
                yield break;
            }

            Canvas.ForceUpdateCanvases();
            ApplyScroll(offsetFromTop, landmark);

            // The fresh page has its real height now, so the mark can go back
            // where it was — and RevealFocused can measure honestly.
            RestoreFocus();
        }

        private void ApplyScroll(float offsetFromTop, string landmark)
        {
            if (_scroll == null || _body == null)
            {
                return;
            }

            var target = LandmarkCard(landmark);
            if (target != null)
            {
                // A kept place goes back to where its heading stood; the deep
                // links land theirs at the top of the view.
                ScrollTo(target, landmark == AnchorLandmark ? _anchorOffset : LandmarkMargin);
            }
            else
            {
                _scroll.verticalNormalizedPosition = JournalNav.KeptPosition(
                    offsetFromTop, _body.rect.height - _scroll.viewport.rect.height);
            }
        }

        private void ScrollTo(RectTransform target, float offsetFromTop)
        {
            var range = _body.rect.height - _scroll.viewport.rect.height;
            if (range <= 0f)
            {
                return;
            }

            // The content's pivot sits at its top, so a child's top edge in
            // content-local space is its distance down the page.
            var local = (Vector2)_body.InverseTransformPoint(target.position);
            var distanceFromTop = -(local.y + target.rect.yMax);
            _scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((distanceFromTop - offsetFromTop) / range);
        }

        /// <summary>
        /// A transient outcome note that rises and fades where the action
        /// happened — the margin note alone is usually off-screen from the
        /// button that was pressed.
        /// </summary>
        internal void Flash(Component near, string message, bool good)
        {
            if (near == null || _feedbackLayer == null)
            {
                return;
            }

            var flash = MakeText(_feedbackLayer, message, 22, TextAnchor.MiddleCenter, good ? MossDeep : Ochre, _hand);
            flash.raycastTarget = false;
            var rect = (RectTransform)flash.transform;
            rect.sizeDelta = new Vector2(700f, 60f);
            rect.position = near.transform.position;
            StartCoroutine(RiseAndFade(flash, rect));
        }

        private System.Collections.IEnumerator RiseAndFade(Text flash, RectTransform rect)
        {
            var colour = flash.color;
            // Start above the control so the note never covers its label.
            var start = rect.anchoredPosition + new Vector2(0f, 34f);
            const float life = 1.1f;
            for (var age = 0f; age < life; age += Time.deltaTime)
            {
                var t = age / life;
                flash.color = new Color(colour.r, colour.g, colour.b, 1f - t);
                rect.anchoredPosition = start + new Vector2(0f, 26f * t);
                yield return null;
            }

            Destroy(flash.gameObject);
        }
    }
}
