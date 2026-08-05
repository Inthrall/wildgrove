using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wildgrove.Game.Input;
using static Wildgrove.Game.JournalWidgets;
using static Wildgrove.Game.JournalTheme;

namespace Wildgrove.Game
{
    // Reaching every control without a touchscreen (design §13 Phase 2, a
    // Level Up gate rather than polish): the Back gesture, the focus mark and
    // where it may travel, remembering it across a rebuild, and stepping the
    // tabs. uGUI's EventSystem moves focus geometrically; this is the part that
    // decides what is reachable and keeps the mark honest.
    public sealed partial class GameHud
    {
        private void HandleBack()
        {
            if (!_input.BackTriggered)
            {
                return;
            }

            if (_sheet != null)
            {
                _sheets.DismissSheet();
            }
            else if (_tab != TabTrail)
            {
                OpenTab(TabTrail);
            }
            else if (!DeviceForm.IsDesktopLike)
            {
                // Only a handheld exits on Back — that's its platform
                // convention. A window has its own way of closing, and Escape
                // killing the app on Play Games on PC reads as a crash
                // (isMobilePlatform is true there, so it used to).
                Application.Quit();
            }
        }

        // ─────────────────────────── Focus navigation ────────────────────────
        // Level Up asks for every interaction to be reachable without touch,
        // and the design (§13 Phase 2) makes it a gate rather than polish. The
        // pieces: uGUI's EventSystem already moves focus geometrically once
        // something is selected, so this supplies what it can't — waking focus
        // on the first key press, a visible mark, scrolling the page to keep
        // the marked control in view, surviving the journal's own rebuilds,
        // trapping focus inside an open sheet, and stepping tabs from the
        // shoulders (crossing a long page to reach the tab bar is a trek, not
        // navigation). The rules worth pinning live in <see cref="JournalNav"/>.

        /// <summary>
        /// True while the focus mark is up and a control is under it — the state
        /// in which Submit (Space, pad South) belongs to that control rather
        /// than to the world strip.
        /// </summary>
        private bool FocusHasTarget => _focusEngaged && _focused != null;

        /// <summary>
        /// True while a text field has the keyboard — the rename field, when a
        /// familiar arrives or is renamed from the roster, is the journal's only
        /// one. Everything the journal binds to a key stands down while it is
        /// lit: Q and E turn the page, C catches a windfall, Space tends, and
        /// every one of them belongs inside a familiar's name instead.
        /// </summary>
        private static bool TextEntryActive
        {
            get
            {
                var events = EventSystem.current;
                var selected = events != null ? events.currentSelectedGameObject : null;
                if (selected == null)
                {
                    return false;
                }

                var field = selected.GetComponent<InputField>();
                return field != null && field.isFocused;
            }
        }

        /// <summary>
        /// Hand navigation to the field, or take it back. One flag, the same
        /// shape as the modal trap: uGUI's default UI actions bind Navigate to
        /// WASD <em>and</em> the arrows, so without this, moving the caret also
        /// walks focus off the field mid-word. The field keeps reading keys
        /// through the selected-object update, which this doesn't touch, so
        /// typing, Enter and Escape all still reach it.
        /// </summary>
        private static void SendNavigationEvents(bool send)
        {
            var events = EventSystem.current;
            if (events != null && events.sendNavigationEvents != send)
            {
                events.sendNavigationEvents = send;
            }
        }

        /// <summary>
        /// Back, while a field is lit, only leaves the field — it must not also
        /// close the sheet the field is standing in. A pad player has no Escape
        /// key, and with Cancel suppressed along with the rest of navigation,
        /// East is the only way out of a field they opened with South. The typed
        /// text is kept: nothing reads the field until its own button is pressed.
        /// </summary>
        private void LeaveFieldOnBack()
        {
            if (_input.BackTriggered)
            {
                Select(null);
            }
        }

        private void HandleFocus()
        {
            var events = EventSystem.current;
            if (events == null)
            {
                return;
            }

            // While a sheet is open the page is switched off, so navigation
            // can't leave the sheet. Only on a change — the setter dirties the
            // canvas group's whole subtree.
            var pageLive = _sheet == null;
            if (_pageGroup != null && _pageGroup.interactable != pageLive)
            {
                _pageGroup.interactable = pageLive;
            }

            // A tap hands the journal back to the finger. The mark is a
            // navigation cue; left lit after a tap it reads as a cursor the
            // touch player doesn't have. uGUI's own selection is deliberately
            // left alone — clearing it here would cancel a rename field the
            // same frame the tap opened it.
            if (_input.PointerPressedThisFrame)
            {
                _focusEngaged = false;
            }

            if (!_focusEngaged)
            {
                if (!_input.NavigateHeld)
                {
                    MarkFocus(null);
                    return;
                }

                // The first direction press wakes the mark rather than moving
                // it: there is nothing to move from, and uGUI needs a selection
                // to move against. A live selection left by an earlier tap is
                // resumed instead of jumped away from.
                _focusEngaged = true;
                var resumed = events.currentSelectedGameObject;
                if (resumed == null || !resumed.activeInHierarchy || !InCurrentContext(resumed))
                {
                    FocusFirst();
                }
            }
            else if (!_restoreFocus)
            {
                // Self-healing, and the reason opening a sheet needs no hook:
                // whenever the selection is gone or somewhere it shouldn't be
                // (a sheet just opened over it, a sheet just closed under it, a
                // rebuild destroyed it), focus lands somewhere sensible again.
                var selected = events.currentSelectedGameObject;
                if (selected == null || !selected.activeInHierarchy || !InCurrentContext(selected))
                {
                    FocusFirst();
                }
            }

            var target = _focusEngaged ? events.currentSelectedGameObject : null;
            if (target != _focused)
            {
                MarkFocus(target);
                RevealFocused(target);
            }
        }

        /// <summary>Draw (or put away) the focus ring, tracking the marked control.</summary>
        private void MarkFocus(GameObject target)
        {
            if (_focusRing != null)
            {
                Destroy(_focusRing);
                _focusRing = null;
            }

            _focused = target;
            if (target != null)
            {
                _focusRing = AddFocusRing(target);
            }
        }

        /// <summary>
        /// Which controls focus may visit right now: a sheet's, while one is
        /// open — everything else is switched off behind it — otherwise the
        /// page's and the chrome's.
        /// </summary>
        private bool InCurrentContext(GameObject go)
        {
            if (_sheet != null)
            {
                return go.transform.IsChildOf(_sheet.transform);
            }

            return _root != null && go.transform.IsChildOf(_root);
        }

        private void FocusFirst()
        {
            // The page before the chrome: a player who just asked to move wants
            // the cards they were reading, not the ledger three rows above them.
            var first = FirstFocusable(_sheet != null ? _sheet.transform : (Transform)_body)
                        ?? FirstFocusable(_sheet != null ? _sheet.transform : (Transform)_root);
            Select(first);
        }

        private void Select(Selectable selectable)
        {
            var events = EventSystem.current;
            if (events != null)
            {
                events.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
            }
        }

        private static Selectable FirstFocusable(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            var candidates = root.GetComponentsInChildren<Selectable>(false);
            foreach (var candidate in candidates)
            {
                if (IsFocusable(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsFocusable(Selectable selectable)
        {
            return selectable != null
                   && selectable.IsActive()
                   && selectable.IsInteractable()
                   && selectable.navigation.mode != Navigation.Mode.None;
        }

        /// <summary>
        /// Bring the marked control into view, in whichever scroll it lives —
        /// the open page, or a long sheet's own. Geometric navigation is happy
        /// to walk focus straight off the bottom of a scroll, so without this
        /// half the journal is unreachable by pad even though every control in
        /// it is "navigable".
        /// </summary>
        private void RevealFocused(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            var rect = target.transform as RectTransform;
            var scroll = target.GetComponentInParent<ScrollRect>();
            if (rect == null || scroll == null || scroll.content == null || scroll.viewport == null
                || !rect.IsChildOf(scroll.content))
            {
                return;
            }

            // Both scrolls anchor their content by the top, so a child's top
            // edge in content-local space is its distance down the page (the
            // same measure ScrollTo takes).
            var local = (Vector2)scroll.content.InverseTransformPoint(rect.position);
            var distanceFromTop = -(local.y + rect.rect.yMax);
            scroll.verticalNormalizedPosition = JournalNav.RevealPosition(
                scroll.verticalNormalizedPosition,
                scroll.content.rect.height,
                scroll.viewport.rect.height,
                distanceFromTop,
                rect.rect.height,
                JournalNav.RevealPad);
        }

        /// <summary>
        /// Remember where focus sat in the page, by position among its
        /// controls, so the rebuild about to destroy it can put it back. The
        /// journal rebuilds on its own cadence — a familiar arrives, a batch
        /// finishes — and a controller player must not be thrown to the top of
        /// the page each time.
        /// </summary>
        private void RememberFocus()
        {
            _focusMemo = -1;
            _restoreFocus = false;
            if (!_focusEngaged || _focused == null || _body == null
                || !_focused.transform.IsChildOf(_body))
            {
                return;
            }

            var candidates = _body.GetComponentsInChildren<Selectable>(false);
            var index = 0;
            foreach (var candidate in candidates)
            {
                if (!IsFocusable(candidate))
                {
                    continue;
                }

                if (candidate.gameObject == _focused)
                {
                    _focusMemo = index;
                    _restoreFocus = true;
                    return;
                }

                index++;
            }
        }

        private void RestoreFocus()
        {
            if (!_restoreFocus)
            {
                return;
            }

            _restoreFocus = false;
            if (!_focusEngaged || _body == null)
            {
                return;
            }

            var focusable = new List<Selectable>();
            foreach (var candidate in _body.GetComponentsInChildren<Selectable>(false))
            {
                if (IsFocusable(candidate))
                {
                    focusable.Add(candidate);
                }
            }

            var index = JournalNav.RestoreIndex(_focusMemo, focusable.Count);
            if (index >= 0)
            {
                Select(focusable[index]);
            }
        }

        /// <summary>
        /// The shoulders (and Q/E) turn the page. Blocked under a sheet: the
        /// tabs are switched off behind it, and turning the page beneath an open
        /// question would be answering it by accident.
        /// </summary>
        private void HandleTabStep()
        {
            if (_sheet != null)
            {
                return;
            }

            var step = _input.TabStep;
            if (step != 0)
            {
                OpenTab(JournalNav.StepTab(_tab, step, _wide));
            }
        }
    }
}
