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

            return _tab + "/" + state.roster.Count + "/" + state.nodes.Count + "/" + state.digSites.Count
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
            FoldedHeading = null;

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
            for (var i = _body.childCount - 1; i >= 0; i--)
            {
                var child = _body.GetChild(i).gameObject;
                // Off before Destroy: a destroyed child holds its place in the
                // layout until end of frame, so the old and new pages would
                // share the body for one rendered frame — a visible stutter on
                // every rebuild. Inactive, it leaves the layout at once, and
                // the scroll can settle before this frame draws.
                child.SetActive(false);
                Destroy(child);
            }

            _spread = null;
            if (_wide)
            {
                // A spread: the open page on the left, the Trail always facing
                // it. The Trail keeps the land in view while the player works
                // the Camp or the Record — which is the whole point of the
                // wide layout, and why the Trail has no tab here.
                BuildSpreadColumns(out var left, out var right);
                SetPageColumn(left);
                BuildPage(_tab);
                SetPageColumn(right);
                // The Trail has no lit tab to name it here, so the page names
                // itself — the running head the mock puts over the facing page.
                MakeText(right, "THE TRAIL", 13, TextAnchor.MiddleCenter, Ink2, _smallCaps);
                _trail.BuildTrailPage();
            }
            else
            {
                SetPageColumn(_body);
                BuildPage(_tab);
            }

            SetPageColumn(null);
            // The page has drawn its headings, so the fold has found its
            // landmark (or the zone is gone) — the id has done its job.
            PendingZoneFold = null;
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

        /// <summary>Repaint every live label and plate on the open page.</summary>
        private void RunLiveUpdaters()
        {
            for (var i = 0; i < _liveUpdaters.Count; i++)
            {
                _liveUpdaters[i]();
            }
        }

        /// <summary>
        /// How far down the page the window's top edge sits, in the page's own
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
            PendingZoneFold = zoneId;
            _zoneFoldOffset = HeadingViewportOffset(heading);
            _pendingScroll = ZoneLandmark;
            Dirty = true;
        }

        /// <summary>Distance from the viewport's top edge down to <paramref name="heading"/>'s top edge — where the fold's settle puts it back.</summary>
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
        /// <see cref="ScrollToOnTrail"/> for the sheets — an event popup ends
        /// in the card that answers it, and the Trail is a long scroll to be
        /// dropped into the top of.
        /// </summary>
        internal void GoToTrail(string landmark) => ScrollToOnTrail(landmark);

        /// <summary>
        /// Open the Trail tab and bring a landmark card ("verse") into view —
        /// the tracker deep-links into a page that is otherwise a long scroll.
        /// </summary>
        private void ScrollToOnTrail(string landmark)
        {
            _pendingScroll = landmark;
            OpenTab(TabTrail);
            if (!_dirty)
            {
                // Already on the Trail with no rebuild coming — jump now.
                StartCoroutine(SettleScroll(ScrolledOffset(), _pendingScroll));
                _pendingScroll = null;
            }
        }

        /// <summary>The landmark name a zone fold scrolls back to.</summary>
        private const string ZoneLandmark = "zone";

        /// <summary>The small breath a deep-linked landmark keeps from the viewport's top edge.</summary>
        private const float LandmarkMargin = 6f;

        /// <summary>Where the pressed zone heading stood in the viewport — the fold's settle restores it there.</summary>
        private float _zoneFoldOffset = LandmarkMargin;

        private RectTransform LandmarkCard(string landmark)
        {
            switch (landmark)
            {
                case "verse":
                    return _firstVerseCard;
                case "keeping":
                    return _firstKeepingCard;
                case ZoneLandmark:
                    return FoldedHeading;
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
                // A fold's landmark goes back to where its heading stood; the
                // deep links land theirs at the top of the view.
                ScrollTo(target, landmark == ZoneLandmark ? _zoneFoldOffset : LandmarkMargin);
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
