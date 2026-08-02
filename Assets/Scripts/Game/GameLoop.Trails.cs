using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    // The trails (design §3, §9) — what the player does out at a node: barter
    // at the Exchange, catch a windfall, replant, leave a pile for a newcomer,
    // build a planter. See GameLoop.cs for the run this all hangs off.
    public sealed partial class GameLoop
    {
        // ─────────────────────────── The Exchange (design §9) ────────────────

        /// <summary>The caravan's standing deal right now — null while it has too few known goods to name one.</summary>
        public ExchangeOffer CurrentExchangeOffer()
        {
            return Exchange.OfferAt(State, Data, NowUnixMs());
        }

        /// <summary>Seconds until the caravan names a new deal.</summary>
        public double ExchangeOfferSecondsRemaining()
        {
            return Exchange.OfferSecondsRemaining(Data, NowUnixMs());
        }

        /// <summary>Units of <paramref name="to"/> per one unit of <paramref name="from"/> at the Exchange.</summary>
        public BigDouble ExchangeRate(string from, string to)
        {
            return Exchange.Rate(State, Data, from, to);
        }

        /// <summary>Units of <paramref name="to"/> received for a tier's <paramref name="amount"/> of <paramref name="from"/> — Decent and Choice pay their value multipliers.</summary>
        public BigDouble ExchangeQuote(string from, string to, BigDouble amount, QualityTier quality)
        {
            return Exchange.Quote(State, Data, from, to, amount, quality);
        }

        /// <summary>Barter one tier's goods for plain goods at the Exchange. Returns units received (0 = refused).</summary>
        public BigDouble TradeAtExchange(string from, string to, BigDouble amount, QualityTier quality)
        {
            var received = Exchange.TryTrade(State, Data, from, to, amount, quality);
            if (received > BigDouble.Zero)
            {
                Telemetry.LogEvent("exchange_trade",
                    ("from", from), ("to", to), ("quality", quality.ToString()),
                    ("spent", amount.ToDouble()), ("received", received.ToDouble()));
            }

            return received;
        }

        // ──────────────────── Windfalls, replanting & piles ──────────────────

        /// <summary>
        /// Catch a windfall bubble at its node — the active-play reward that
        /// replaced tap-to-tend: a burst of the node's goods lands as camp
        /// stock and the node is tended (burst + Choice window + Rite deed).
        /// Returns the amount granted (zero = nothing was due, e.g. the node
        /// went fallow while the bubble drifted).
        /// </summary>
        public BigDouble PopBubble(NodeState node)
        {
            var gained = Bubbles.Pop(State, Data, node);
            if (gained > BigDouble.Zero)
            {
                Telemetry.LogEvent("bubble_popped",
                    ("node", node.id), ("resource", node.resourceId), ("gained", gained.ToDouble()));
                Stats.RecordWindfall(node.resourceId);
            }

            return gained;
        }

        /// <summary>The node's next replant cost, in units of its own resource (design §3) — for the button label.</summary>
        public BigDouble ReplantCost(NodeState node)
        {
            return Replanting.ReplantCost(node, Data.economy);
        }

        /// <summary>True when camp stock covers the node's next replant — the button's enabled state.</summary>
        public bool CanReplant(NodeState node)
        {
            return Replanting.CanReplant(State, Data, node);
        }

        /// <summary>Replant a node's own resource to raise its richness (design §3 — the fourth lane). Returns false (no change) when stock is short.</summary>
        public bool Replant(NodeState node)
        {
            if (!Replanting.TryReplant(State, Data, node))
            {
                return false;
            }

            Telemetry.LogEvent("replanted", ("node", node.id), ("richness", node.richnessLevel));
            return true;
        }

        /// <summary>True while a verse-earned pile waits unanswered (design §4) — shows the node plates' pile lines.</summary>
        public bool GiftAvailable()
        {
            return Gifts.IsAvailable(State, Data);
        }

        /// <summary>Units of the node's own resource one pile costs — for the pile line's label.</summary>
        public BigDouble GiftPileCost()
        {
            return Gifts.PileCost(Data.economy);
        }

        /// <summary>The specialist a pile at this node would call (design §4), or null when no one new answers here.</summary>
        public SpeciesData GiftSpeciesFor(NodeState node)
        {
            return Gifts.NodeCanCall(State, Data, node) ? Gifts.SpecialistFor(Data, node) : null;
        }

        /// <summary>True when camp stock covers a pile at this node, someone new would answer, and a slot is open.</summary>
        public bool CanLeaveGift(NodeState node)
        {
            return Gifts.CanLeavePile(State, Data, node);
        }

        /// <summary>
        /// Leave a pile of the node's own resource (design §4) — the
        /// resource's specialist arrives, stationed there, and queues for the
        /// naming sheet like any recruit. Returns the newcomer, or null when
        /// the camp can't spare the pile (or no one new would answer).
        /// </summary>
        public Familiar LeaveGift(NodeState node)
        {
            var familiar = Gifts.LeavePile(State, Data, node);
            if (familiar != null)
            {
                Telemetry.LogEvent("gift_left", ("node", node.id), ("species", familiar.speciesId));
            }

            return familiar;
        }

        /// <summary>True once the Carving Bench has opened Bushcraft and its planter recipes (design §3) — gates the planter UI.</summary>
        public bool PlantersUnlocked()
        {
            return Planters.Unlocked(State, Data);
        }

        /// <summary>The planter types that attach to a gather node (design §3).</summary>
        public List<PlanterData> NodePlanters()
        {
            return PlantersForTarget("node");
        }

        /// <summary>The planter types that attach to a dig site (design §3).</summary>
        public List<PlanterData> DigSitePlanters()
        {
            return PlantersForTarget("digSite");
        }

        private List<PlanterData> PlantersForTarget(string target)
        {
            var matching = new List<PlanterData>();
            foreach (var planter in Data.planters)
            {
                if (planter.target == target)
                {
                    matching.Add(planter);
                }
            }

            return matching;
        }

        /// <summary>True when this planter is already built at the target.</summary>
        public bool PlanterBuilt(PlanterData planter, string targetId)
        {
            return State.HasPlanter(targetId, planter.id);
        }

        /// <summary>True when the planter can be built here (unlocked, absent, stock covers the bundle) — the build button's enabled state.</summary>
        public bool CanBuildPlanter(PlanterData planter, string targetId)
        {
            return Planters.CanBuild(State, Data, planter, targetId);
        }

        /// <summary>Build a planter at a node or dig site (design §3). Returns false (no change) when it can't be built.</summary>
        public bool BuildPlanter(PlanterData planter, string targetId)
        {
            if (!Planters.TryBuild(State, Data, planter, targetId))
            {
                return false;
            }

            Telemetry.LogEvent("planter-built", ("planter", planter.id), ("target", targetId));
            return true;
        }
    }
}
