using System;
using System.Collections.Generic;
using System.Linq;
using BreakInfinity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Game.Input;
using Wildgrove.Game.Services;
using Wildgrove.Game.World;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalSprites;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The Warden's Journal — the code-built uGUI HUD, laid out after the
    /// docs/wildgrove-journal.html mock: a paper page with a title header, a
    /// currency ledger, a handwritten margin note, a pinned Rite / Fold
    /// tracker, and five journal tabs along the bottom (Trail · Camp ·
    /// Stores · Warden · Record) — set in the journal's own type (IM Fell / Caveat /
    /// Lora), ruled ink borders, paper grain, and the small motion touches
    /// (tend flash, the carrier on the trail line). All game logic stays in
    /// Wildgrove.Sim; this only reads state and calls <see cref="GameLoop"/>
    /// actions.
    /// <para>
    /// The chrome is kept deliberately thin: everything pinned here is paid
    /// for out of the open page, which is the only row that can give. A bar
    /// belongs up here only if it is read on every tab — the trail-home line
    /// went back to the Trail page and the camp actions to the Camp page for
    /// exactly that reason, and the ledger carries the three meta currencies
    /// rather than the whole stores list (which grows all game).
    /// </para>
    /// <para>
    /// This is the HUD coordinator: it owns the MonoBehaviour lifecycle, the
    /// per-frame and cadence loops, the tabs, and the handles every other part
    /// works through. The journal's palette, generated sprites, uGUI factory and
    /// formatters live in the static <c>Journal*</c> helpers; each tab's cards
    /// are built by the section builders (<see cref="TrailPage"/>,
    /// <see cref="CampPage"/>, <see cref="WardenPage"/>, <see cref="RecordPage"/>,
    /// <see cref="JournalSheets"/>). The body rebuilds only when its structure
    /// (or the open tab) changes; live numbers refresh through
    /// <see cref="_liveUpdaters"/>.
    /// </para>
    /// <para>
    /// THIS file holds the coordinator. The rest is grouped by concern in the
    /// partial files beside it, the same way <see cref="GameLoop"/> is split —
    /// <c>GameHud.Layout.cs</c> (building the chrome and folding it to the
    /// screen), <c>.Chrome.cs</c> (what the chrome says), <c>.Focus.cs</c>
    /// (reaching it all without touch), <c>.World.cs</c> (the strip above the
    /// page and the taps on it), <c>.Body.cs</c> (the open page and its
    /// rebuilds). A new member belongs in whichever file already owns its
    /// neighbours rather than here; the fields stay together in this one,
    /// because a field is the one thing all five share.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(GameLoop))]
    public sealed partial class GameHud : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;

        // The paper margin down each side of the page, before JournalLayout
        // widens it to centre the book on a very broad canvas.
        private const int PageMargin = 16;

        private GameLoop _loop;
        private IGameInput _input;
        private Font _font;      // body — Lora
        private Font _serif;     // titles, verses, lore — IM Fell English
        private Font _smallCaps; // chrome: eyebrows, card heads, tabs, buttons — IM Fell English SC
        private Font _hand;      // margin notes, posted lines, flourishes — Caveat
        private WorldView _world;

        // The journal's section builders — one per tab, plus the modal sheets.
        private JournalText _labels;
        private TrailPage _trail;
        private CampPage _camp;
        private StoresPage _stores;
        private WardenPage _warden;
        private RecordPage _record;
        private JournalSheets _sheets;

        private Text _title;
        private Text _slotCounter;
        private Text _ledger;
        // The head's two rows, and the rule under the ledger — a spread folds
        // the ledger up into the title's row and drops that rule.
        private RectTransform _headRow;
        private RectTransform _ledgerRow;
        private GameObject _ledgerRule;
        private RectTransform _trackerRow;
        private Text _note;
        private Text _trackerText;
        private GameObject _trackerPanel;
        private Button _foldButton;

        private RectTransform _worldGap;
        private RectTransform _body;
        private Transform _modalLayer;
        private Canvas _canvas;
        private RectTransform _root;

        // ─── Keyboard / controller focus (design §13 Phase 2) ───
        // The journal is touch-first: nothing is focused until the player asks
        // to move with keys or a pad, and a tap puts the mark away again.
        private CanvasGroup _pageGroup;
        private GameObject _focusRing;
        private GameObject _focused;
        private bool _focusEngaged;
        // Where focus sat in the page before a rebuild destroyed it, so a
        // controller player isn't thrown back to the top of the page every time
        // a craft finishes.
        private int _focusMemo = -1;
        private bool _restoreFocus;
        private ScrollRect _scroll;
        private LayoutElement _worldGapElement;
        private RectTransform _feedbackLayer;
        private RectTransform _firstVerseCard;
        private string _pendingScroll;
        private string _builtTab;
        private Rect _appliedSafeArea;
        private float _appliedCanvasHeight;
        private float _appliedCanvasWidth;

        /// <summary>
        /// True while the canvas is wide enough to carry a spread (see
        /// <see cref="JournalLayout"/>): the open page on the left, the Trail
        /// pinned on the right. Recomputed whenever the canvas changes shape,
        /// so a foldable or a window resize turns the page mid-run.
        /// </summary>
        private bool _wide;

        /// <summary>
        /// Where page builders are currently writing. Null outside a rebuild;
        /// set to one column at a time while a spread is being laid out, which
        /// is why <see cref="Body"/> reads it first — every builder funnels
        /// through Body (and JournalWidgets.Content, kept in step), so the
        /// pages need no idea whether they are a column or the whole page.
        /// </summary>
        private RectTransform _pageColumn;

        /// <summary>The spread's two page columns, alive only while wide.</summary>
        private RectTransform _spread;
        private readonly Dictionary<string, Button> _tabButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Text> _tabLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, GameObject> _tabOuterRules = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _tabMerges = new Dictionary<string, GameObject>();
        private static readonly Vector3[] Corners = new Vector3[4];

        private readonly List<Action> _liveUpdaters = new List<Action>();
        // Per-frame animation hooks (tend flash, the carrier on the trail line) —
        // cleared with the body they animate.
        private readonly List<Action> _frameUpdaters = new List<Action>();
        private readonly Dictionary<string, float> _flashAges = new Dictionary<string, float>();
        private readonly Dictionary<string, Text> _tendFlashes = new Dictionary<string, Text>();
        private float _refreshCountdown;
        private string _structureSignature;
        private bool _dirty;

        // Fonts whose dynamic atlas was repacked and whose text has not been
        // re-generated against it yet — see RefreshRebuiltFonts. Usually one
        // entry, and empty on almost every frame.
        private readonly List<Font> _atlasRebuilt = new List<Font>();
        private readonly List<Text> _textBuffer = new List<Text>();

        private string _tab = TabTrail;

        // One open sheet at a time; the dim layer blocks input beneath it.
        private GameObject _sheet;

        // First-run teaching. Outcome notes drift back to the teaching line
        // after a few seconds until each core gesture (post someone, catch a
        // windfall) has been performed once — otherwise the first action's
        // outcome note overwrites the margin note for good. Posting is derived
        // from state (a loaded save with a posted kith has learned it); the
        // catch is a one-time flag that survives restarts.
        private const string HintCaughtKey = "wildgrove.hint.caught";
        private const float NoteRevertSeconds = 6f;
        private bool _hintPostDone;
        private bool _hintCatchDone;
        private float _noteRevert;

        // ─────────────────────────── Section access ──────────────────────────
        // The section builders reach shared HUD state and coordinator calls
        // through these; see JournalSection.

        internal GameLoop Loop => _loop;
        /// <summary>
        /// A rebuild is owed. Setting it answers on the next frame rather than
        /// at the next cadence tick: a quarter second between the press and the
        /// page changing under it reads as the page moving of its own accord,
        /// not as an answer to the tap.
        /// </summary>
        internal bool Dirty
        {
            get => _dirty;
            set
            {
                _dirty = value;
                if (value)
                {
                    _refreshCountdown = 0f;
                }
            }
        }
        internal RectTransform Body => _pageColumn != null ? _pageColumn : _body;
        internal Transform ModalLayer => _modalLayer;
        internal GameObject Sheet { get => _sheet; set => _sheet = value; }
        internal RectTransform FirstVerseCard { get => _firstVerseCard; set => _firstVerseCard = value; }
        internal RectTransform FirstKeepingCard { get => _firstKeepingCard; set => _firstKeepingCard = value; }
        private RectTransform _firstKeepingCard;
        internal List<Action> LiveUpdaters => _liveUpdaters;
        internal List<Action> FrameUpdaters => _frameUpdaters;
        internal Dictionary<string, float> FlashAges => _flashAges;
        internal Dictionary<string, Text> TendFlashes => _tendFlashes;

        /// <summary>
        /// The zones the player has folded open or shut on the Trail page, by
        /// id — only the ones pressed by hand; the rest follow
        /// <see cref="JournalZones.IsOpen"/>'s newest-only default. Held here
        /// rather than in the page so it survives the rebuild a press causes,
        /// and deliberately not saved: how the page was left folded is a
        /// reading position, not progress.
        /// </summary>
        internal Dictionary<string, bool> ZoneOpen { get; } = new Dictionary<string, bool>();

        /// <summary>
        /// The journal's folding cards the player has opened or shut, by id —
        /// the Record page's four and the Trail's keeping. Held and defaulted
        /// exactly as <see cref="ZoneOpen"/> is, against
        /// <see cref="JournalCardFolds.IsOpen"/>'s rule rather than the
        /// Trail's positional one.
        /// </summary>
        internal Dictionary<string, bool> CardOpen { get; } = new Dictionary<string, bool>();

        /// <summary>
        /// The id of the thing the page is being rebuilt around, and the rect
        /// itself once the page has drawn it again — the landmark that keeps a
        /// pressed card under the finger that pressed it (see
        /// <see cref="KeepInPlace"/>, and the folds that go through it,
        /// <see cref="FoldZone"/> and <see cref="FoldCard"/>).
        /// </summary>
        internal string PendingAnchor { get; private set; }

        internal RectTransform AnchoredHeading { get; private set; }
        internal JournalText Labels => _labels;
        internal JournalSheets Sheets => _sheets;

        private void OnEnable()
        {
            Font.textureRebuilt += OnFontTextureRebuilt;
        }

        private void OnDisable()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
        }

        /// <summary>
        /// Note the repack; the text is re-generated a frame later, in
        /// <see cref="RefreshRebuiltFonts"/>, and deliberately not from here.
        /// See that method for why the frame matters.
        /// </summary>
        private void OnFontTextureRebuilt(Font font)
        {
            if (font != null && !_atlasRebuilt.Contains(font))
            {
                _atlasRebuilt.Add(font);
            }
        }

        private void Awake()
        {
            _loop = GetComponent<GameLoop>();
            _input = new InputSystemGameInput();
            _world = GetComponent<WorldView>();
            var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _font = LoadFont("Lora", builtin);
            _serif = LoadFont("IMFellEnglish", _font);
            _smallCaps = LoadFont("IMFellEnglishSC", _serif);
            _hand = LoadFont("Caveat", _font);
            JournalWidgets.Init(_font, _serif, _smallCaps, _hand);

            _labels = new JournalText(_loop);
            _trail = new TrailPage(this);
            _camp = new CampPage(this);
            _stores = new StoresPage(this);
            _warden = new WardenPage(this);
            _record = new RecordPage(this);
            _sheets = new JournalSheets(this);

            EnsureEventSystem();
            BuildChrome();
            _hintCatchDone = PlayerPrefs.GetInt(HintCaughtKey, 0) == 1;
            var hint = HintText();
            if (hint != null)
            {
                ShowNote(hint);
            }
        }

        /// <summary>
        /// Re-generate every label drawn from a font whose dynamic atlas was
        /// repacked since the last frame, so none of them keeps drawing through
        /// the old glyph rects.
        /// <para>
        /// uGUI has its own version of this (FontUpdateTracker calls
        /// FontTextureChanged on each live Text from inside the rebuild event),
        /// and it cannot help the label that CAUSED the rebuild: generating a
        /// mesh is what requests glyphs, requesting a glyph the atlas has no
        /// room for is what repacks it, and a Text mid-generation has its own
        /// rebuild callback suppressed against re-entry — so it finishes against
        /// the layout that has just been thrown away and keeps that mesh until
        /// something dirties it again. A label whose string is re-assigned on
        /// the refresh cadence (the title, the ledger, the tracker) is dirtied
        /// within a quarter-second and heals itself; the chrome's write-once
        /// labels are not, and the five tab names and the strip's slot counter
        /// sat in glyph soup indefinitely.
        /// </para>
        /// <para>
        /// Deferring a frame is the whole fix: outside the event, no suppression
        /// flag is set, and a regeneration that repacks the atlas again is
        /// simply noted for the next frame. It converges because the atlas only
        /// grows — but see todo.md on the type scale: sixteen authored sizes
        /// across four faces is what makes the repacks frequent enough to matter.
        /// </para>
        /// </summary>
        private void RefreshRebuiltFonts()
        {
            if (_atlasRebuilt.Count == 0 || _canvas == null)
            {
                return;
            }

            // Inactive included: a Text is only on uGUI's tracked list while its
            // object is enabled, so a closed sheet's labels never hear about the
            // repack at all and come back wearing the old atlas. FontTextureChanged
            // invalidates their cached generator, which is what makes the
            // regeneration on re-enable a real one.
            _canvas.GetComponentsInChildren(true, _textBuffer);
            foreach (var text in _textBuffer)
            {
                if (_atlasRebuilt.Contains(text.font))
                {
                    text.FontTextureChanged();
                }
            }

            _textBuffer.Clear();
            _atlasRebuilt.Clear();
        }

        /// <summary>
        /// The teaching line for whichever core gesture is still unlearned, or
        /// null once both are. Keyboard/gamepad tail only where one can exist —
        /// on a phone the margin note is flavour, not a manual for keys it
        /// doesn't have.
        /// </summary>
        private string HintText()
        {
            // Play Games on PC is an Android build, so isMobilePlatform reports
            // true there and hid the key hint from the one player with nothing
            // but keys — DeviceForm asks the question that actually matters.
            var catchTail = DeviceForm.IsDesktopLike ? " · space / (A) catches one" : string.Empty;
            var press = DeviceForm.PressVerb;
            if (!_hintPostDone)
            {
                return _hintCatchDone
                    ? press + " a plate to post someone · the land only gives to the posted."
                    : press + " a plate to post someone · catch the windfalls drifting up the strip" + catchTail;
            }

            return _hintCatchDone
                ? null
                : "catch the windfalls drifting up the strip · each pays a burst of goods" + catchTail;
        }

        /// <summary>
        /// Un-learn the teaching, for a book started again. The catch flag is a
        /// PlayerPref rather than run state, so without this a fresh camp would
        /// open with no instruction at all — the one place in the game where
        /// there is any.
        /// </summary>
        internal void ForgetTeaching()
        {
            _hintCatchDone = false;
            _hintPostDone = false;
            PlayerPrefs.DeleteKey(HintCaughtKey);
            var hint = HintText();
            if (hint != null && _note != null)
            {
                ShowNote(hint);
                _noteRevert = 0f;
            }
        }

        private void Update()
        {
            // Before the state guard: the chrome is on screen while the welcome
            // sheet holds the run back, and garbled tabs there are the first
            // thing anyone sees.
            RefreshRebuiltFonts();

            if (_loop == null || _loop.State == null)
            {
                return;
            }

            FitLayoutToScreen();
            ReportWorldStrip();

            // A lit text field owns the keyboard, and the journal stands down.
            var typing = TextEntryActive;
            SendNavigationEvents(!typing);
            if (typing)
            {
                LeaveFieldOnBack();
            }
            else
            {
                HandleBack();
                HandleTabStep();
            }

            HandleWorldTap(typing);
            if (!typing)
            {
                HandleFocus();
            }

            for (var i = 0; i < _frameUpdaters.Count; i++)
            {
                _frameUpdaters[i]();
            }

            // An outcome note drifts back to the teaching line while a core
            // gesture is still unlearned — the one instruction in the game
            // must survive its reader's first tap.
            if (_noteRevert > 0f)
            {
                _noteRevert -= Time.deltaTime;
                if (_noteRevert <= 0f)
                {
                    var hint = HintText();
                    if (hint != null && _note != null)
                    {
                        ShowNote(hint);
                    }
                }
            }

            _refreshCountdown -= Time.deltaTime;
            if (_refreshCountdown <= 0f)
            {
                _refreshCountdown = RefreshInterval;
                // On the cadence, not per-frame: PumpSheets scans all zones for
                // the next unread waystone — a quarter-second delay to raise a
                // sheet is imperceptible and keeps that scan off the hot path.
                _sheets.PumpSheets();

                // A run adopted from another device changed everything under
                // the page; the note is the quiet way to say so.
                var cloudNotice = _loop.TakeCloudNotice();
                if (cloudNotice != null)
                {
                    SetNote(cloudNotice);
                }

                RefreshChrome(); // cadence, not per-frame — avoids string allocs every frame
                var signature = StructureSignature();
                if (_dirty || signature != _structureSignature)
                {
                    _dirty = false;
                    _structureSignature = signature;
                    // Runs the live pass itself, before it measures the fresh
                    // page — a page is as tall as its words, and most of them
                    // are written by that pass.
                    RebuildBody();
                }
                else
                {
                    RunLiveUpdaters();
                }
            }
        }

        /// <summary>
        /// Open a journal tab by name. Legacy store-capture page names from the
        /// pre-journal HUD map onto the tab that now carries that content.
        /// </summary>
        public void OpenTab(string tab)
        {
            switch (tab)
            {
                case "gather":
                case "rite":
                    tab = TabTrail;
                    break;
                case "craft":
                case "build":
                    tab = TabCamp;
                    break;
                case "collect":
                    tab = TabRecord;
                    break;
            }

            // On a spread the Trail is already open on the right page, so
            // asking for it means "somewhere that isn't here" — the Camp, as
            // the mock does. Deep links (ScrollToOnTrail) route through here
            // too, and land on a Trail that never left the screen.
            if (_wide && tab == TabTrail)
            {
                tab = TabCamp;
            }

            if (Array.IndexOf(Tabs, tab) < 0 || tab == _tab)
            {
                return;
            }

            _tab = tab;
            Dirty = true;
            foreach (var id in Tabs)
            {
                StyleTab(id, id == _tab);
            }
        }

        /// <summary>
        /// The open tab wears the page: paper background, full-ink bold label,
        /// the cards' outer rule, and the merge strip — the mock's raised tab
        /// physically fuses with the page, and that continuity cue reads
        /// pre-attentively where a lighter tint alone never did.
        /// </summary>
        private void StyleTab(string id, bool active)
        {
            _tabButtons[id].GetComponent<Image>().color = active ? PagePaper : DeepPaper;
            var label = _tabLabels[id];
            label.color = active ? Ink : Ink2;
            label.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            _tabOuterRules[id].SetActive(active);
            _tabMerges[id].SetActive(active);
        }
    }
}
