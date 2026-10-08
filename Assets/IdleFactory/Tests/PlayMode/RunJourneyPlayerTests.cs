using System;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T121 (Issue #203), SC-001, SC-006, FR-024: the first-factory journey on real components in the built Windows player.
    /// The test drives the production <see cref="RunSession"/>, <see cref="RunCommands"/>, <see cref="RunPersistence"/> and the
    /// real <c>JsonRunCheckpointStore</c> file protocol (no mock of the run authority), over the packaged Iron content and an
    /// isolated temporary root. The prestige threshold is the scaled one of the balance trace (ECO-3), because content
    /// revision 1 does not reach the real threshold within six simulated hours (balance-trace-r1.md); the commands, content
    /// and persistence are otherwise the shipped ones.
    /// </summary>
    public sealed class RunJourneyPlayerTests
    {
        private string root;

        [SetUp]
        public void CreateRoot() => root = RunPlayerFixtures.NewRoot();

        [TearDown]
        public void DeleteRoot() => RunPlayerFixtures.DeleteRoot(root);

        [Category("StandalonePlayer")]
        [Test]
        public void TheIronChainContractItemResearchPrestigeAndIndustryJourneyMakesTheSecondRunFaster()
        {
            var catalog = RunPlayerFixtures.ScaledCatalog();
            var rig = RunRig.Create(root, catalog, RunPlayerFixtures.StartUtcMs);
            var cold = rig.ColdStartOk(null);
            Assert.That(cold.Imported, Is.False);
            Assert.That(rig.Session.State.RunNumber, Is.EqualTo(1));
            Assert.That(rig.Session.State.Cash, Is.EqualTo(catalog.RunStart.StartingCash));

            // ---- Run 1: Iron chain, Contract, Item, Research --------------------------------------------------
            var policy = new RunJourneyPolicy(rig);
            long firstRunSeconds = policy.PlayUntilEligible();
            var chain = policy.Tally;
            var run1 = rig.Session.State;

            Assert.That(chain.GearProducedAtSec, Is.GreaterThan(0), "The Iron chain made a Gear (the Gear Contract unlocks on production).");
            Assert.That(run1.IsUnlocked("iron.gear"), Is.True);
            Assert.That(run1.Machines.Select(m => m.MachineId).Distinct().OrderBy(id => id, StringComparer.Ordinal),
                Is.EqualTo(new[] { "machine.assembler", "machine.miner", "machine.press", "machine.smelter" }), "All four chain machines are built.");
            Assert.That(chain.ContractsDelivered, Is.GreaterThan(0), "A Contract was delivered.");
            Assert.That(chain.ItemsGranted, Is.GreaterThan(0), "A Contract granted an Item.");
            Assert.That(chain.ItemsActivated, Is.GreaterThan(0), "An Item was activated.");
            Assert.That(chain.ResearchPurchases, Is.GreaterThan(0), "Research was purchased.");
            Assert.That(chain.OverdriveActivations, Is.GreaterThan(0), "Overdrive was activated.");

            // ---- Prestige through the real durable path -------------------------------------------------------
            rig.SyncClock();
            var preview = rig.Commands.GetPrestigePreview();
            Assert.That(preview.IsSuccess && preview.Value.IsEligible, Is.True);
            var award = preview.Value.IpAward;
            Assert.That(award, Is.GreaterThan(0));
            var oldRunId = run1.RunId;
            var itemsBefore = rig.Session.Permanent.ItemStacks().Sum(s => s.Count);

            var confirmed = rig.Commands.ConfirmPrestige();
            Assert.That(confirmed.IsSuccess, Is.True, confirmed.ReasonCode);
            var promoted = rig.Commands.PromotePrestige();
            Assert.That(promoted.IsSuccess, Is.True, promoted.ReasonCode);

            var run2 = rig.Session.State;
            Assert.That(run2.RunNumber, Is.EqualTo(2));
            Assert.That(run2.PreviousRunId, Is.EqualTo(oldRunId));
            Assert.That(run2.Machines, Is.Empty);
            Assert.That(run2.Rp, Is.Zero);
            Assert.That(rig.Session.Permanent.Ip, Is.EqualTo(award), "The award was paid exactly once.");
            Assert.That(rig.Session.Permanent.PrestigeCount, Is.EqualTo(1));
            Assert.That(rig.Session.Permanent.ItemStacks().Sum(s => s.Count), Is.EqualTo(itemsBefore), "Items survive the prestige.");
            var durable = rig.Store.ReadActive();
            Assert.That(durable.Succeeded, Is.True, durable.Message);
            Assert.That(durable.Checkpoint.Run.RunId, Is.EqualTo(run2.RunId), "The new run is durable before it is live.");
            Assert.That(durable.Checkpoint.Run.PreviousRunId, Is.EqualTo(oldRunId));
            Assert.That(durable.Checkpoint.Permanent.Ip, Is.EqualTo(award));

            // ---- Industry, then run 2 ------------------------------------------------------------------------
            int purchases = policy.SpendIndustry();
            Assert.That(purchases, Is.GreaterThan(0), "Industry upgrades were bought with the Influence Points.");
            Assert.That(chain.IndustryPurchases, Is.EqualTo(purchases));
            Assert.That(rig.Session.Permanent.Ip, Is.LessThan(award));

            long secondRunSeconds = policy.PlayUntilEligible();
            Debug.Log("T121 journey (scaled threshold " + RunPlayerFixtures.ScaledThresholdFv + "): run 1 eligible after " + firstRunSeconds +
                " s, IP " + award + ", Industry purchases " + purchases + ", run 2 eligible after " + secondRunSeconds + " s.");
            Assert.That(secondRunSeconds, Is.LessThan(firstRunSeconds), "The second run is measurably faster (Industry upgrades).");

            // ---- The second run survives a restart ----------------------------------------------------------
            rig.SyncClock();
            var saved = rig.Persistence.SaveNow();
            Assert.That(saved.IsSuccess, Is.True, saved.ReasonCode);
            var expected = RunPlayerFixtures.Fingerprint(rig.Session);

            var restarted = RunRig.Create(root, catalog, rig.Clock.UnixMs);
            var resumed = restarted.ColdStartOk(null);
            Assert.That(resumed.Imported, Is.False);
            Assert.That(resumed.StateReplaced, Is.True);
            Assert.That(RunPlayerFixtures.Fingerprint(restarted.Session), Is.EqualTo(expected), "The restart restores the saved second run exactly.");
        }
    }
}
