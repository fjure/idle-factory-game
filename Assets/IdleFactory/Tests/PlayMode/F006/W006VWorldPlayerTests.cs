using System.Collections;
using System.Globalization;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T531 (B4a, FR-006, SC-008, research R16): the synthetic W006-V world (<see cref="W006VWorld"/>) in Play Mode and in the Windows test player:
    /// the renderer census per shipped layout (Art Bible 2.2a and 21.12, against both the signed 95 and the draft 105), and the 600-frame allocation run (budget 5).
    /// The tests assert robust facts and log every measurement as a <c>B4A-*</c> line; the verdicts are in <c>reviews/performance-b4.md</c>. The world is the real view
    /// code over flat-colour sprites, so the counts of renderers and the allocations are those of the shipped code; the pixels and the texture memory are not
    /// (the real-scene cases of <c>W006VRealScenePlayerTests</c> have the real art).
    /// </summary>
    [Category("StandalonePlayer")]
    public sealed class W006VWorldPlayerTests
    {
        private const int Frames = 600;

        private static void Log(string line)
        {
            TestContext.Out.WriteLine(line);
            Debug.Log(line);
        }

        private static string Num(double value, string format = "F2") => value.ToString(format, CultureInfo.InvariantCulture);

        [Test]
        public void TheFixtureDefinitionHasThePinnedHash()
        {
            Assert.That(W006VFixture.Hash(), Is.EqualTo(W006VFixture.ExpectedHash), "W006-V definition: " + W006VFixture.Canonical());
            Log("B4A-FIXTURE name=" + W006VFixture.Name + " sha256=" + W006VFixture.Hash() + " canonical=" + W006VFixture.Canonical());
        }

        private static IEnumerator Warm(W006VWorld world, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                world.AdvanceRun();
                world.Frame();
                if (i % 20 == 19)
                {
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator TheRendererCensusOfThePortraitAndLandscapeLayouts()
        {
            foreach (var columns in new[] { 3, 4 })
            {
                var layout = LatticeLayoutBuilder.ShippedForColumns(columns);
                var world = W006VWorld.Create(layout, RunPlayerFixtures.IronCatalog());
                try
                {
                    yield return Warm(world, 120);
                    Assert.That(world.Stage, Is.EqualTo(W006VFixture.Stage), "W006-V is the Established stage");
                    Assert.That(world.RunningBranches, Is.EqualTo(W006VFixture.BranchPips), "12 branches carry a running pip");
                    Assert.That(world.Pool.ActiveCount, Is.GreaterThanOrEqualTo(1), "effects are on screen");
                    var census = world.Census();
                    var perLocation = census.Total;
                    Log("B4A-CENSUS layout=" + layout.LayoutId + " columns=" + columns + " locations=" + layout.LocationCount + " " + census +
                        " fxActive=" + world.Pool.ActiveCount + " fxDraining=" + world.Pool.DrainingCount + " vsSigned95=" + (census.Total - 95) + " vsDraft105=" + (census.Total - 105) + " vsTier173=" + (census.Total - 173));
                    Assert.That(census.TilemapRenderers, Is.LessThanOrEqualTo(3), "at most 3 tilemap renderers (base, deck, links)");
                    Assert.That(perLocation, Is.LessThanOrEqualTo(173), "below the 173 single-pass tier of the budget 8 restatement");
                }
                finally
                {
                    world.Dispose();
                }
            }
        }

        [UnityTest]
        public IEnumerator TheWorstCaseSlotCensusHasEveryLocationAtItsOverlayCap()
        {
            // Art Bible 2.2: a running slot is base, machine, accent and 2 markers; any other slot is base, machine, badge and 2 markers; both 5, plus 1 selection frame: 61.
            // Here: all 12 slots carry both markers (boosted and Overdrive), alternate running and waiting, one is selected; the 12 pips run and 3 effects play. The FX pool owns 8
            // renderers (3 active plus 5 draining tails): the 5 tails cannot be held by the public pool API in one frame, so they are added to the total as the design maximum.
            foreach (var columns in new[] { 3, 4 })
            {
                var layout = LatticeLayoutBuilder.ShippedForColumns(columns);
                var world = W006VWorld.Create(layout, RunPlayerFixtures.IronCatalog());
                try
                {
                    yield return Warm(world, 120);
                    var worst = new SlotVisual[W006VFixture.Locations];
                    for (var i = 0; i < worst.Length; i++)
                    {
                        var running = i % 2 == 0;
                        var badge = SlotGridModel.BadgeFor(true, MachineBlocker.None, running ? MachinePhase.Running : MachinePhase.Waiting, out var text, out _);
                        worst[i] = new SlotVisual(i, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, DeckTestRig.MinerKey, badge, text, true, true, i == W004VFixture.SelectedSlot, 0, "Slot " + (i + 1));
                    }

                    world.Grid.Render(worst);
                    world.Frame();
                    yield return null;
                    var census = world.Census();
                    var slots = census.ByGroup.TryGetValue("slots", out var slotCount) ? slotCount : 0;
                    Log("B4A-CENSUS-WORST layout=" + layout.LayoutId + " " + census + " slotRenderers=" + slots + " designCapSlots=61 plusFxDrainingTails=5 totalWithTails=" + (census.Total + 5) +
                        " vsSigned95=" + (census.Total + 5 - 95) + " vsDraft105=" + (census.Total + 5 - 105) + " vsTier173=" + (census.Total + 5 - 173));
                    Assert.That(slots, Is.LessThanOrEqualTo(61), "the slot overlay cap of Art Bible 2.2");
                    Assert.That(census.Total + 5, Is.LessThanOrEqualTo(173), "below the 173 single-pass tier");
                }
                finally
                {
                    world.Dispose();
                }
            }
        }

        private sealed class Result
        {
            public int SteadyFrames;
            public long SteadyAllocations;
            public int RebuildFrames;
            public long RebuildAllocations;
            public int FeedRebuilds;
            public long StructureMoves;
            public long Spawned;
            public int PipFrames;
            public int FxFrames;
            public int MaxFxActive;
        }

        private static IEnumerator Measure(W006VWorld world, Result result)
        {
            yield return Warm(world, 160);
            yield return null;
            var rebuildsAtStart = world.Feed.Rebuilds;
            var spawnedBefore = world.Pool.SpawnedTotal;
            var structureBefore = world.Session.StructureRevision;
            for (var frame = 0; frame < Frames; frame++)
            {
                world.AdvanceRun();
                var before = world.Feed.Rebuilds;
                var counted = GcAllocationCounter.Count(world.Frame);
                result.PipFrames += world.RunningBranches > 0 ? 1 : 0;
                result.FxFrames += world.Pool.ActiveCount > 0 ? 1 : 0;
                result.MaxFxActive = Mathf.Max(result.MaxFxActive, world.Pool.ActiveCount);
                if (world.Feed.Rebuilds != before)
                {
                    result.RebuildFrames++;
                    result.RebuildAllocations += counted;
                    continue;
                }

                result.SteadyFrames++;
                result.SteadyAllocations += counted;
            }

            result.FeedRebuilds = world.Feed.Rebuilds - rebuildsAtStart;
            result.StructureMoves = world.Session.StructureRevision - structureBefore;
            result.Spawned = world.Pool.SpawnedTotal - spawnedBefore;
        }

        [UnityTest]
        public IEnumerator TheW006VWorldAllocatesNothingInTheSteadyStateOver600Frames()
        {
            var world = W006VWorld.Create(LatticeLayoutBuilder.ShippedForColumns(3), RunPlayerFixtures.IronCatalog());
            var result = new Result();
            try
            {
                yield return Measure(world, result);
                Log("B4A-W006V-600 case=no-contract frames=" + Frames + " steadyFrames=" + result.SteadyFrames + " steadyAllocations=" + result.SteadyAllocations + " structuralFrames=" + result.RebuildFrames +
                    " structuralAllocations=" + result.RebuildAllocations + " feedRebuilds=" + result.FeedRebuilds + " structureMoves=" + result.StructureMoves + " spawned=" + result.Spawned +
                    " pipFrames=" + result.PipFrames + " fxFrames=" + result.FxFrames + " maxFxActive=" + result.MaxFxActive + " stage=" + world.Stage + " runtime=" + Application.platform);
                Assert.That(result.PipFrames, Is.EqualTo(Frames), "12 branches ran in every frame");
                Assert.That(result.Spawned, Is.GreaterThan(0), "effects ran");
                Assert.That(result.SteadyAllocations, Is.Zero, "budget 5: every steady frame of the W006-V world allocates nothing");
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator WithAProductionContractTheStructuralRebuildsAreCountedAndLabelled()
        {
            // C-20: the contract board bumps the structure revision on every produced unit, so the feed rebuilds the factory snapshot at most once a simulated second.
            // Those allocations are structural events (about 174 per snapshot), not steady state; the steady frames of the same run still allocate nothing.
            var world = W006VWorld.Create(LatticeLayoutBuilder.ShippedForColumns(3), W003PlayerFixtures.WithProductionContract("contract.produce-ore", 100_000), "contract.produce-ore");
            var result = new Result();
            try
            {
                yield return Measure(world, result);
                var perRebuild = result.RebuildFrames == 0 ? 0 : result.RebuildAllocations / (double)result.RebuildFrames;
                Log("B4A-W006V-600 case=production-contract frames=" + Frames + " steadyFrames=" + result.SteadyFrames + " steadyAllocations=" + result.SteadyAllocations + " structuralFrames=" + result.RebuildFrames +
                    " structuralAllocations=" + result.RebuildAllocations + " allocationsPerRebuild=" + Num(perRebuild, "F1") + " feedRebuilds=" + result.FeedRebuilds + " structureMoves=" + result.StructureMoves +
                    " (structural-event allocations, not steady state) runtime=" + Application.platform);
                Assert.That(result.StructureMoves, Is.GreaterThan(100), "the contract churned the structure revision");
                Assert.That(result.SteadyAllocations, Is.Zero, "the frames that did not rebuild allocate nothing");
            }
            finally
            {
                world.Dispose();
            }
        }
    }
}
