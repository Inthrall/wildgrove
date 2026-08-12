using UnityEngine;
using UnityEngine.UI;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The sheet scaffold every other sheet is built on: the one-open-sheet
    /// lifecycle, the scrim and its dismissal, the first-moments tap guard, and
    /// the generic yes/no confirm.
    /// <para>
    /// Nothing here knows what any sheet is about. That is the point: the
    /// invariant that only one sheet is ever open lives in
    /// <see cref="BeginSheet"/> rather than being remembered at forty call
    /// sites, and an async callback that raises a sheet after the book has
    /// moved on cannot orphan the one already up.
    /// </para>
    /// </summary>
    internal sealed partial class JournalSheets
    {
        // How much of the screen a sheet's lines may fill before they scroll
        // instead of growing (the card's padding rides on top).
        private const float SheetMaxCanvasShare = 0.72f;

        // The open sheet's safe way out — what Android Back and a tap on the
        // scrim mean. Null when no sheet is open.
        private System.Action _sheetDismiss;

        /// <summary>
        /// Open the standard sheet scaffold. <paramref name="dismiss"/> is the
        /// sheet's safe way out (Back / scrim tap) — plain close by default;
        /// <paramref name="scrimDismisses"/> false keeps the scrim inert for
        /// confirms, where a stray tap must never answer the question (Back
        /// still cancels — cancelling is always safe).
        /// <para>
        /// The panel hugs its content until it would outgrow the screen, then
        /// pins and scrolls. Without that, the sheets whose length is a function
        /// of the save (the posting sheet lists the whole roster, the station
        /// pick every post) grow straight off the top and bottom of the display,
        /// taking "Never mind" with them. The scroll layer lives INSIDE the
        /// panel so the card, its rules and its padding stay put and only the
        /// lines move.
        /// </para>
        /// </summary>
        private Transform BeginSheet(System.Action dismiss = null, bool scrimDismisses = true)
        {
            // One sheet at a time is the whole model, and this is where it is
            // kept. CloseSheet and DismissSheet only ever know about the
            // current sheet, so a second one opened over the first orphans it:
            // its scrim goes on covering the journal and nothing left can reach
            // it — taps route to a sheet that is no longer tracked, Back
            // returns at the null check, and the book is shut until the app is
            // killed. Callers mostly do close before they open; an async
            // callback (a leaderboard read, a sign-in) cannot know what was
            // raised while it was away, so the invariant is enforced here
            // rather than remembered at every call site.
            CloseSheet();

            var dim = MakePanel("Sheet", (RectTransform)_modalLayer, DimColor);
            Stretch((RectTransform)dim.transform);
            _sheet = dim;
            _sheetDismiss = dismiss;

            var panel = MakePanel("Panel", (RectTransform)dim.transform, PagePaper);
            if (scrimDismisses)
            {
                var tap = dim.AddComponent<ScrimTap>();
                tap.OnTap = DismissSheet;
                tap.Panel = (RectTransform)panel.transform;
            }
            AddBorder(panel, Ink2);
            AddBorder(panel, RulePaper, 6f);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // Fixed width, hugged height — a fixed height leaves a welcome-back
            // note floating in a half-empty card.
            rt.sizeDelta = new Vector2(800, 0);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            // The scroll layer is the panel's only child and carries no width of
            // its own — it takes the card's.
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(30, 30, 30, 36);
            layout.spacing = 0;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollGo = new GameObject("SheetScroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(panel.transform, false);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = (RectTransform)scrollGo.transform;

            var content = MakeRect("Content", (RectTransform)scrollGo.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = 16;
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            // Hug the lines until they'd fill the screen, then pin — the panel's
            // own padding rides on top of this, so the card lands near 80%.
            var clamp = scrollGo.AddComponent<HeightClampedElement>();
            clamp.content = content;
            clamp.canvas = (RectTransform)_modalLayer;
            clamp.maxCanvasShare = SheetMaxCanvasShare;

            AddSheetStitch(panel, scroll);
            // A cross in the corner, where a hand looks for the way out — but
            // only where leaving is free. A sheet whose scrim is inert is
            // asking a question, and those keep their own worded way out
            // rather than gaining a corner that answers for the player.
            if (scrimDismisses)
            {
                AddSheetClose(panel);
            }

            return content;
        }

        /// <summary>
        /// The sheet's close cross — pinned to the panel's top-right corner,
        /// outside the vertical flow (and so outside the scroll), so a long
        /// sheet can't carry it off the bottom of the screen the way a trailing
        /// "Never mind" did.
        /// </summary>
        private void AddSheetClose(GameObject panel)
        {
            var close = IconButton(panel.transform, JournalSprites.CrossSprite(), 36f, 96f, DismissSheet);
            var element = close.GetComponent<LayoutElement>();
            element.ignoreLayout = true;
            var rect = (RectTransform)close.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            // Sized by hand: an ignored layout element still carries the touch
            // size, but nothing is left to apply it.
            rect.sizeDelta = new Vector2(96f, 96f);
            // Riding the panel's own padding, so it sits in the corner rather
            // than shouldering the title out of the middle.
            rect.anchoredPosition = new Vector2(-6f, -6f);
        }

        /// <summary>
        /// The sheet's scroll stitch — the page's slim ink scrollbar, riding the
        /// panel's right padding. Outside the scroll's mask so it isn't clipped,
        /// and auto-hidden while the sheet fits, so its presence is itself the
        /// signal that there is more below.
        /// </summary>
        private static void AddSheetStitch(GameObject panel, ScrollRect scroll)
        {
            var barGo = new GameObject("Stitch", typeof(Image), typeof(Scrollbar), typeof(LayoutElement));
            barGo.transform.SetParent(panel.transform, false);
            barGo.GetComponent<LayoutElement>().ignoreLayout = true;
            var track = barGo.GetComponent<Image>();
            track.color = new Color(RulePaper.r, RulePaper.g, RulePaper.b, 0.45f);
            var barRect = (RectTransform)barGo.transform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            // 6 wide, inset into the card's right padding, and short of the
            // panel's top and bottom padding so it reads as a margin rule.
            barRect.sizeDelta = new Vector2(6f, -60f);
            barRect.anchoredPosition = new Vector2(-12f, -3f);

            var handleGo = new GameObject("Handle", typeof(Image));
            handleGo.transform.SetParent(barGo.transform, false);
            var handle = handleGo.GetComponent<Image>();
            handle.color = new Color(Ink2.r, Ink2.g, Ink2.b, 0.55f);
            var handleRect = (RectTransform)handleGo.transform;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            var scrollbar = barGo.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            NoNavigation(scrollbar);
        }

        private void CloseSheet()
        {
            _sheetDismiss = null;
            if (_sheet != null)
            {
                Object.Destroy(_sheet);
                _sheet = null;
            }
        }

        /// <summary>
        /// Dismiss the open sheet the safe way — hardware Back and the scrim
        /// route here. Each sheet chooses what "dismiss" means (cancel, keep
        /// the suggested name, walk on); plain close is the default.
        /// </summary>
        internal void DismissSheet()
        {
            if (_sheet == null)
            {
                return;
            }

            var dismiss = _sheetDismiss;
            _sheetDismiss = null;
            if (dismiss != null)
            {
                dismiss();
            }
            else
            {
                CloseSheet();
            }
        }

        /// <summary>
        /// Forwards a tap on a sheet's dim scrim — but only outside the paper
        /// panel — to the sheet's dismissal. uGUI clicks bubble up from the
        /// panel's own widgets to the scrim, so the handler must check where
        /// the tap actually landed rather than trusting that it reached here.
        /// </summary>
        private sealed class ScrimTap : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
        {
            internal System.Action OnTap;
            internal RectTransform Panel;

            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
            {
                if (Panel == null
                    || !RectTransformUtility.RectangleContainsScreenPoint(Panel, eventData.position, eventData.enterEventCamera))
                {
                    OnTap?.Invoke();
                }
            }
        }

        /// <summary>
        /// A transparent click-eater over the whole open sheet for its first
        /// moments. It carries its own (listener-less) Button so uGUI's click
        /// bubbling stops at it rather than walking up to the scrim's dismiss.
        /// </summary>
        private void AddTapGuard(float seconds)
        {
            if (_sheet == null)
            {
                return;
            }

            var guard = new GameObject("TapGuard", typeof(Image), typeof(Button));
            guard.transform.SetParent(_sheet.transform, false);
            Stretch((RectTransform)guard.transform);
            guard.GetComponent<Image>().color = Color.clear;
            var guardButton = guard.GetComponent<Button>();
            guardButton.transition = Selectable.Transition.None;
            // It only exists to swallow taps — focus landing on it would be a
            // dead end for the half-second it lives.
            NoNavigation(guardButton);
            _hud.StartCoroutine(DestroyAfter(guard, seconds));
        }

        private static System.Collections.IEnumerator DestroyAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null)
            {
                Object.Destroy(go);
            }
        }

        /// <summary>
        /// A modal yes/no confirmation — a title, a body line, and a paired
        /// "never mind" / go-ahead choice styled like the Fold sheet's Migrate.
        /// The confirmed action runs after the sheet closes, so it may open a
        /// sheet of its own.
        /// <para>
        /// <paramref name="holdSeconds"/> keeps the go-ahead dead, counting down
        /// on its own face, for that long after the sheet opens — a hold for the
        /// one confirm behind which something cannot be earned back. It is an
        /// argument rather than this sheet's rule because a trade or a spend
        /// doesn't earn a wait: made universal it would only teach the player to
        /// sit through the count without reading it.
        /// </para>
        /// </summary>
        internal void OpenConfirmSheet(string title, string body, string confirmLabel, System.Action onConfirm,
            float holdSeconds = 0f)
        {
            // Same rule as the Fold sheet: a confirm's scrim is inert.
            var sheet = BeginSheet(scrimDismisses: false);
            MakeText(sheet, title, 32, TextAnchor.UpperCenter, Ink, _serif);
            if (!string.IsNullOrEmpty(body))
            {
                MakeText(sheet, body, 20, TextAnchor.MiddleCenter, Ink2, _serif);
            }

            Button(sheet, "Never mind", 320, CloseSheet);

            // Latched, because CloseSheet's Destroy only takes effect at the end
            // of the frame: two pointers released on this button in the same
            // frame both dispatch, and the answer would be given twice. The
            // things behind a confirm are the expensive ones — amber spent, a
            // pile traded away, a book started again.
            var answered = false;
            var confirm = Button(sheet, confirmLabel, 320, () =>
            {
                if (answered)
                {
                    return;
                }

                answered = true;
                CloseSheet();
                onConfirm?.Invoke();
            });
            KeyAction(confirm);

            if (holdSeconds > 0f)
            {
                confirm.interactable = false;
                SetButtonTint(confirm, false, true);
                _hud.StartCoroutine(ArmAfter(confirm, confirmLabel, holdSeconds));
            }
        }

        /// <summary>
        /// Hold a confirm's go-ahead shut for a few seconds, counting the wait
        /// down on the button's own face so it reads as a deliberate pause and
        /// not as a plate that failed to wake up. Then it arms, wearing the
        /// words it was given.
        /// </summary>
        private static System.Collections.IEnumerator ArmAfter(Button button, string label, float seconds)
        {
            for (var remaining = Mathf.CeilToInt(seconds); remaining > 0; remaining--)
            {
                // The sheet can be dismissed mid-count — writing to a destroyed
                // widget throws, and there is nothing left to arm.
                if (button == null)
                {
                    yield break;
                }

                SetButtonLabel(button, label + " · " + remaining);
                yield return new WaitForSeconds(1f);
            }

            if (button == null)
            {
                yield break;
            }

            SetButtonLabel(button, label);
            button.interactable = true;
            SetButtonTint(button, true, true);
        }
    }
}
