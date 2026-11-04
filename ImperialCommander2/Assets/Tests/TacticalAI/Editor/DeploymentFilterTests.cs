// Player-25 M14b-C1 mirror: the deploy-point trait filter law - EN-cache
// vs the EN tokens IsFilteredOut builds, the no-cache fallback, and the
// DG013 DE/EN data truth (EN "Droid" / DE "Droide" in
// Languages/<lang>/DeploymentGroups/enemies.json). The live proof TODAY is
// DeploymentFilterGate in tools/TacticalAiGate (same law, REAL
// DeploymentPointProps.cs compiled against stubs); this fixture is the
// honest mirror against the REAL app types for the later editor CI.
// Repo convention (TacaiFixtures header): no Unity test framework is wired
// today, so [Explicit]; build.sh TESTS compiles this file into the
// Assembly-CSharp-Editor context as the compile pin.
using NUnit.Framework;

namespace TacticalAI.Tests
{
    // The shipping game declares a global `DeploymentCard`; the alias keeps
    // it explicit inside this namespace (shadowing law, MissionOverrideHook
    // precedent). Saga needs its qualification for the same reason.
    using GameCard = DeploymentCard;

    [Explicit("Requires Test Framework")]
    [TestFixture]
    public class DeploymentFilterTests
    {
        static GameCard Card(string[] traits, string[] traitsEn)
        {
            return new GameCard
            {
                faction = "Imperial",
                miniSize = FigureSize.Medium1x2,
                traits = traits,
                traitsEn = traitsEn,
            };
        }

        static Saga.DeploymentPointProps DroidDp()
        {
            return new Saga.DeploymentPointProps { incImperial = true, incMedium = true, incDroid = true };
        }

        static Saga.DeploymentPointProps TrooperDp()
        {
            return new Saga.DeploymentPointProps { incImperial = true, incMedium = true, incTrooper = true };
        }

        [Test]
        public void LocalizedLaneWithEnCachePassesDroidDp()
        {
            Assert.IsFalse(DroidDp().IsFilteredOut(Card(new[] { "Droide" }, new[] { "Droid" })),
                "DE lane 'Droide' + EN cache 'Droid' must pass (Player-25 fix)");
        }

        [Test]
        public void NoEnCacheKeepsOldFiltering()
        {
            Assert.IsTrue(DroidDp().IsFilteredOut(Card(new[] { "Droide" }, null)),
                "imported/pre-cache cards keep the pre-fix comparison");
        }

        [Test]
        public void EnGamesUnmoved()
        {
            Assert.IsFalse(DroidDp().IsFilteredOut(Card(new[] { "Droid" }, null)));
            Assert.IsFalse(DroidDp().IsFilteredOut(Card(new[] { "Droid" }, new[] { "Droid" })));
        }

        [Test]
        public void FilterStaysFilter()
        {
            // EN-vs-EN match must not smuggle a droid onto a trooper-only DP,
            // and the non-trait lanes (faction/size) stay language-blind.
            Assert.IsTrue(TrooperDp().IsFilteredOut(Card(new[] { "Droide" }, new[] { "Droid" })));
            Assert.IsTrue(DroidDp().IsFilteredOut(new GameCard
                { faction = "Imperial", miniSize = FigureSize.Small1x1,
                  traits = new[] { "Droide" }, traitsEn = new[] { "Droid" } }));
        }
    }
}
