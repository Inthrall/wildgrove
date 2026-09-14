using BreakInfinity;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    // The kith (design §4) — who has arrived and still wants naming, who holds
    // a post, how far each familiar has come, and what the grove gathers and
    // carries between them. See GameLoop.cs for the run this all hangs off.
    public sealed partial class GameLoop
    {
        /// <summary>The next familiar awaiting a name (without dequeuing), or null.</summary>
        public Familiar PeekPendingArrival()
        {
            return _announce.PeekArrival();
        }

        /// <summary>Claim the next arrival (the naming sheet dismisses it once named/accepted).</summary>
        public Familiar TakePendingArrival()
        {
            return _announce.TakeArrival();
        }

        /// <summary>
        /// Watch the slot ladder for a rung the player just earned, and say so.
        /// The count is derived, so nothing raises an event when it widens — the
        /// mark lives in <see cref="Announcements"/>, which catches a verse sung
        /// past a milestone and a slot bought with the one look.
        /// </summary>
        private void NoticeKithSlots()
        {
            var opened = _announce.NoticeKithSlots(KithSlots());
            if (opened > 0)
            {
                Telemetry.LogEvent("kith_slot_opened", ("slots", opened));
            }
        }

        /// <summary>The slot count just reached, awaiting its celebration; 0 when none is pending.</summary>
        public int PendingSlotCelebration => _announce.PendingSlotCelebration;

        /// <summary>Claim the pending slot celebration (clears it), or 0.</summary>
        public int TakePendingSlotCelebration()
        {
            return _announce.TakeSlotCelebration();
        }

        /// <summary>Active kith slots on the ladder (design §4): one to start, verses sung earn three more, the store opens the last two.</summary>
        public int KithSlots()
        {
            return Kith.Slots(State, Data);
        }

        /// <summary>Companions in the collection — the whole roster, walking or resting.</summary>
        public int KithCount()
        {
            return Kith.Count(State);
        }

        /// <summary>Familiars currently holding a post — the held slots.</summary>
        public int KithWalking()
        {
            return Kith.Walking(State);
        }

        /// <summary>Companions idle at camp with no post — who an opening slot is for.</summary>
        public int KithResting()
        {
            return Kith.Resting(State);
        }

        /// <summary>The zone whose verse opens the next place (design §4 ladder), or null when every earned place is open.</summary>
        public string NextKithSlotVerseZone()
        {
            return Kith.NextSlotVerseZone(State, Data);
        }

        /// <summary>Lifetime verses sung (design §4 ladder) — folded runs plus this one.</summary>
        public int TotalVersesSung()
        {
            return Kith.TotalVersesSung(State, Data);
        }

        /// <summary>A familiar's species trait (design §4) — null when the species is unknown.</summary>
        public TraitData FamiliarTrait(Familiar familiar)
        {
            return Traits.Of(Data, familiar);
        }

        /// <summary>The Amber a rename asks (design §4), or 0 when the amber system is inert — drives the rename button's price label.</summary>
        public double RenameCost()
        {
            return Amber.RenameCost(Data);
        }

        /// <summary>Whether a rename is affordable right now — the "Save" button's enabled state.</summary>
        public bool CanRenameFamiliar()
        {
            return Amber.CanRename(State, Data);
        }

        /// <summary>
        /// Rename a familiar for its Amber price (design §4) — the same price
        /// whether it's the arrival naming or a later change. Returns false when
        /// the name is blank, unchanged, or unaffordable; the cost is spent only
        /// when the name actually changes (keeping the suggested name is free).
        /// </summary>
        public bool RenameFamiliar(Familiar familiar, string name)
        {
            var cost = Amber.RenameCost(Data);
            var renamed = Amber.TryRename(State, Data, familiar, name);
            if (renamed)
            {
                Telemetry.LogEvent("familiar_renamed", ("amber_cost", cost));
            }

            return renamed;
        }

        /// <summary>What to call the warden on the page — their bought name, or "the warden" while they have none.</summary>
        public string WardenName()
        {
            return Warden.DisplayName(State);
        }

        /// <summary>The warden's name in the possessive — "the warden's own hands", or "Rowan's".</summary>
        public string WardenNamePossessive()
        {
            return Warden.PossessiveName(State);
        }

        /// <summary>Whether the warden has been named — the sheet asks, to head a first naming differently from a change.</summary>
        public bool IsWardenNamed()
        {
            return Warden.IsNamed(State);
        }

        /// <summary>The Amber naming the warden asks, or 0 when the amber system is inert — drives the sheet's price label.</summary>
        public double WardenRenameCost()
        {
            return Amber.WardenRenameCost(Data);
        }

        /// <summary>Whether naming the warden is affordable right now — the "Save" button's enabled state.</summary>
        public bool CanRenameWarden()
        {
            return Amber.CanRenameWarden(State, Data);
        }

        /// <summary>
        /// Name the warden for its Amber price. Returns false when the name is
        /// blank, unchanged, or unaffordable; the cost is spent only when the
        /// name actually changes.
        /// </summary>
        public bool RenameWarden(string name)
        {
            var cost = Amber.WardenRenameCost(Data);
            var named = Amber.TryRenameWarden(State, Data, name);
            if (named)
            {
                Telemetry.LogEvent("warden_renamed", ("amber_cost", cost));
            }

            return named;
        }

        /// <summary>
        /// Station a familiar at a post — a node id, "trail", a "sketch:{zone}"
        /// site, or null to rest at camp (design §2). Returns false when a
        /// resting familiar wants a post and every slot is walked (§4 ladder).
        /// </summary>
        public bool StationFamiliar(Familiar familiar, string stationId)
        {
            return Roster.Station(State, Data, familiar, stationId);
        }

        /// <summary>Walk the warden to a node — one body per post, so a familiar holding it steps back to camp (design §2).</summary>
        public void PostWarden(NodeState node)
        {
            Warden.Post(State, node);
        }

        /// <summary>Stand the warden at a site's sketching post — drawing there, gathering nothing (design §2/§6), evicting any familiar holding it.</summary>
        public void SketchWarden(string zoneId)
        {
            Warden.Sketch(State, zoneId);
        }

        /// <summary>Send the warden back to camp — no post, no picking.</summary>
        public void RestWarden()
        {
            Warden.Rest(State);
        }

        /// <summary>A familiar's current run level (design §4).</summary>
        public int FamiliarLevel(Familiar familiar)
        {
            return Familiars.Level(familiar, Data);
        }

        /// <summary>Fraction of the way to the familiar's next level.</summary>
        public double FamiliarLevelProgress(Familiar familiar)
        {
            return Familiars.ProgressToNextLevel(familiar, Data);
        }

        /// <summary>True when the familiar has climbed the whole curve — the progress above then reads 0, and a band drawing it would read as empty rather than as done.</summary>
        public bool FamiliarAtMaxLevel(Familiar familiar)
        {
            return Familiars.AtMaxLevel(familiar, Data);
        }

        /// <summary>The familiar's permanent Kinship level (design §4).</summary>
        public int FamiliarKinship(Familiar familiar)
        {
            return Kinship.Level(familiar);
        }

        /// <summary>
        /// XP this familiar earns each second at a post right now — base rate,
        /// roosts comfort and its own Kinship rate perk, as the sim credits it.
        /// 0 while it rests.
        /// </summary>
        public double FamiliarXpPerSecond(Familiar familiar)
        {
            return Familiars.XpPerSecond(State, Data, familiar);
        }

        /// <summary>The base XP rate before any familiar's own perks — the denominator the Post sheet's breakdown reads against.</summary>
        public double FamiliarBaseXpPerSecond()
        {
            return Data.economy?.familiarXp?.xpPerSecond ?? 0.0;
        }

        /// <summary>The roosts' comfort multiplier on posted familiars' XP (design §4) — 1 while nothing is built.</summary>
        public double ComfortXpMultiplier()
        {
            return Buildings.ComfortXpMultiplier(State, Data);
        }

        /// <summary>The multiplier this familiar's Kinship adds to its XP rate (design §4).</summary>
        public double FamiliarKinshipXpRate(Familiar familiar)
        {
            return Kinship.XpRateMultiplier(familiar, Data.economy?.familiarXp?.kinshipXpRatePerLevel ?? 0.0);
        }

        /// <summary>The factor this familiar's passed Kinship signature milestones scale its species trait by (design §4).</summary>
        public double FamiliarTraitDeepening(Familiar familiar)
        {
            return Traits.DeepeningFactor(Data, familiar);
        }

        /// <summary>The run level this familiar begins each new run at — its Kinship carried forward (design §4).</summary>
        public int FamiliarStartingLevel(Familiar familiar)
        {
            return 1 + Kinship.Level(familiar);
        }
    }
}
