using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// The delivery contract for a Play Games Reward (design §11): grant, tell
    /// the player, then acknowledge. A grant that can't land must answer null so
    /// the order is left unacknowledged and Play refunds the offer — the one
    /// outcome worse than a lost reward is an acknowledged one that never
    /// arrived, because that can never be re-offered.
    /// </summary>
    public class RewardGrantsTests
    {
        private const double Tolerance = 1e-9;
        private const long Now = 1_000_000_000_000L;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                amber = new EconomyData.AmberData { weeklyCacheAmber = 20 },
            };
            _data.species = new List<SpeciesData>
            {
                new SpeciesData
                {
                    id = Familiar.PonySpecies, displayName = "fell pony", roleLean = "carrier",
                    suggestedNames = new List<string> { "Moss" },
                    trait = new TraitData { displayName = "Half-broke", kind = "trailCarryFactor", value = 0.5 },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Apply_TheDroversHalter_StandsThePonyAndNamesTheGift()
        {
            var state = new GameState();

            var grant = RewardGrants.Apply(state, _data, RewardProductIds.DroversHalter, Now);

            Assert.That(grant, Is.Not.Null);
            Assert.That(state.droversHalterOwned, Is.True);
            Assert.That(Roster.OfSpecies(state, Familiar.PonySpecies), Is.Not.Null);
            Assert.That(grant.statement, Does.Contain("Play Games"), "Play requires the source be said out loud");
            Assert.That(grant.statement, Does.Contain(grant.itemName), "and the item named plainly");
        }

        [Test]
        public void Apply_TheDroversHalterTwice_StillAcknowledges()
        {
            // A reinstall or a second device re-delivers the entitlement. There
            // is nothing new to grant, but the order still has to be
            // acknowledged or Play treats it as undelivered.
            var state = new GameState();
            RewardGrants.Apply(state, _data, RewardProductIds.DroversHalter, Now);

            Assert.That(RewardGrants.Apply(state, _data, RewardProductIds.DroversHalter, Now), Is.Not.Null);
            Assert.That(state.roster.Count, Is.EqualTo(1), "and no second pony");
        }

        [Test]
        public void Apply_TheWeeklyCache_CreditsTheAmberAndStatesTheAmount()
        {
            var state = new GameState();

            var grant = RewardGrants.Apply(state, _data, RewardProductIds.WeeklyAmberCache, Now);

            Assert.That(grant, Is.Not.Null);
            Assert.That(state.amber, Is.EqualTo(20.0).Within(Tolerance));
            Assert.That(state.weeklyCacheClaimedUnixMs, Is.EqualTo(Now));
            Assert.That(grant.statement, Does.Contain("Play Games"));
            Assert.That(grant.statement, Does.Contain("20"), "the denomination has to be in the confirmation");
        }

        [Test]
        public void Apply_TheWeeklyCacheUnconfigured_RefusesRatherThanAcknowledgeNothing()
        {
            _data.economy.amber = null;
            var state = new GameState();

            Assert.That(RewardGrants.Apply(state, _data, RewardProductIds.WeeklyAmberCache, Now), Is.Null);
            Assert.That(state.amber, Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Apply_AnIdWithNoGrant_Refuses()
        {
            var state = new GameState();

            // The Wayfarer's Cloak is named but has no cosmetic substrate to
            // land in, which is exactly why it is kept out of the catalogue.
            Assert.That(RewardGrants.Apply(state, _data, RewardProductIds.WayfarersCloak, Now), Is.Null);
            Assert.That(RewardGrants.Apply(state, _data, "reward_nonsense", Now), Is.Null);
            Assert.That(RewardGrants.Apply(state, _data, null, Now), Is.Null);
        }

        [Test]
        public void Apply_BeforeARunIsLoaded_Refuses()
        {
            Assert.That(RewardGrants.Apply(null, _data, RewardProductIds.WeeklyAmberCache, Now), Is.Null);
        }

        [Test]
        public void Catalogue_OnlyOffersRewardsThatCanActuallyBeGranted()
        {
            var state = new GameState();
            foreach (var rewardId in RewardProductIds.All)
            {
                Assert.That(RewardGrants.Apply(state, _data, rewardId, Now), Is.Not.Null,
                    rewardId + " is catalogued, so a delivery of it must land somewhere");
            }

            Assert.That(RewardProductIds.IsReward(RewardProductIds.WayfarersCloak), Is.False,
                "an ungrantable reward must stay out of the catalogue — uncatalogued means never acknowledged");
        }

        [Test]
        public void Catalogue_ClassifiesRewardsSoOwnershipIsTrackedForTheRightOnes()
        {
            Assert.That(RewardProductIds.IsRepeatable(RewardProductIds.WeeklyAmberCache), Is.True,
                "a repeatable reward must be consumable or its second delivery reads as already owned");
            Assert.That(RewardProductIds.IsRepeatable(RewardProductIds.DroversHalter), Is.False,
                "a single-use reward is a durable entitlement, resolved from the owned set");

            Assert.That(StoreCatalogue.IsConsumable(RewardProductIds.WeeklyAmberCache), Is.True);
            Assert.That(StoreCatalogue.IsConsumable(RewardProductIds.DroversHalter), Is.False);
            Assert.That(StoreCatalogue.IsConsumable(StoreProductIds.AmberPackSmall), Is.True,
                "and the bought consumables still read as they did");
        }

        [Test]
        public void Catalogue_CarriesEveryPurchasableProductAndEveryGrantableReward()
        {
            var all = new List<string>(StoreCatalogue.All);

            foreach (var productId in StoreProductIds.All)
            {
                Assert.That(all, Does.Contain(productId), "the store's own catalogue must survive the union");
            }

            foreach (var rewardId in RewardProductIds.All)
            {
                Assert.That(all, Does.Contain(rewardId),
                    "an unfetched reward id can't be resolved when its order arrives");
                Assert.That(StoreProductIds.All, Does.Not.Contain(rewardId), "and a reward is never purchasable");
            }
        }
    }
}
