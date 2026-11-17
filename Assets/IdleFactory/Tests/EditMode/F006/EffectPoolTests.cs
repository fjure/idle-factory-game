using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T529 (Feature 006, FR-006, FR-029, FR-034; research R12; contracts/world-presentation.md section 4): the effect pool. The rules are proven on
    /// the plain scheduler without a scene: 8 renderers as 3 active plus 5 draining tails, the priority of the completion pop, the 250 ms window per
    /// location and kind with timestamps sized from the location count, no spawn under reduced motion, for a baseline reset or for an offline delta,
    /// the family pace cap, and a pace that applies from the next frame 0. The wiring cases run on the real model and on a real session.
    /// </summary>
    public sealed class EffectPoolTests
    {
        private const int Frames = 6;
        private const long Second = 10_000_000;
        private static readonly FactoryWorldLayout Layout = TestLayouts.Six();

        private static EffectPool NewPool(int locations = 6, int fps = 10)
        {
            var pool = new EffectPool();
            for (var kind = 0; kind < EffectPool.KindCount; kind++)
            {
                pool.SetKindArt((EffectKind)kind, Frames, fps);
            }

            pool.EnsureLocations(locations);
            return pool;
        }

        private static SynthMachine Machine(int slot, long completed, Action<SynthMachine> set = null)
        {
            var machine = new SynthMachine { Slot = slot, Completed = completed };
            set?.Invoke(machine);
            return machine;
        }

        private static PresentationModelSynth.Models Step(MachinePresentationModel model, long revision, params long[] completed)
        {
            var built = PresentationModelSynth.Build(1, revision, completed.Select((c, i) => Machine(i, c)).ToList());
            model.Refresh(Layout, built.Factory, built.Views, built.Views.Length, built.Rates);
            return built;
        }

        private static int SlotOf(EffectPool pool, EffectKind kind, int location)
        {
            for (var i = 0; i < EffectPool.RendererCount; i++)
            {
                if (pool.StateOf(i) != EffectSlotState.Free && pool.KindOf(i) == kind && pool.LocationOf(i) == location)
                {
                    return i;
                }
            }

            return -1;
        }

        // ---- The pool: 8 renderers = 3 active + 5 draining tails ---------------------------------------------------------------------

        [Test]
        public void ThePoolHasEightRenderersAsThreeActiveAndFiveDrainingTails()
        {
            Assert.That(EffectPool.RendererCount, Is.EqualTo(8));
            Assert.That(EffectPool.ActiveLimit, Is.EqualTo(3));
            Assert.That(EffectPool.DrainingLimit, Is.EqualTo(5));
            Assert.That(EffectPool.CoalesceMilliseconds, Is.EqualTo(250));
        }

        [Test]
        public void AnEffectPlaysItsFramesAtItsLatchedRateAndThenFreesItsRenderer()
        {
            var pool = NewPool();
            pool.Advance(1_000);
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 2), Is.EqualTo(EffectSpawnResult.Spawned));
            var slot = SlotOf(pool, EffectKind.CompletionPop, 2);
            Assert.That(pool.StateOf(slot), Is.EqualTo(EffectSlotState.Active));
            Assert.That(pool.FpsOf(slot), Is.EqualTo(10), "the kind's rate without a location rate");
            for (var frame = 0; frame < Frames; frame++)
            {
                pool.Advance(1_000 + frame * 100);
                Assert.That(pool.FrameOf(slot), Is.EqualTo(frame));
                Assert.That(pool.AlphaOf(slot), Is.EqualTo(1f));
            }

            pool.Advance(1_000 + Frames * 100);
            Assert.That(pool.StateOf(slot), Is.EqualTo(EffectSlotState.Free), "the pop lasts 6 frames");
            Assert.That(pool.ActiveCount + pool.DrainingCount, Is.Zero);
        }

        [Test]
        public void AFourthDecorativeEffectIsDroppedWhileThreePlay()
        {
            var pool = NewPool();
            for (var location = 0; location < 3; location++)
            {
                Assert.That(pool.TrySpawn(EffectKind.Spark, location), Is.EqualTo(EffectSpawnResult.Spawned));
            }

            Assert.That(pool.TrySpawn(EffectKind.Spark, 3), Is.EqualTo(EffectSpawnResult.NoCapacity));
            Assert.That(pool.TrySpawn(EffectKind.UpgradeGlint, 4), Is.EqualTo(EffectSpawnResult.NoCapacity), "a decorative effect never cancels another one");
            Assert.That(pool.ActiveCount, Is.EqualTo(3));
            Assert.That(pool.DrainingCount, Is.Zero);
        }

        [Test]
        public void ACompletionPopOutranksDecorativeEffectsAndCancelsTheOldestIntoADrainingTail()
        {
            var pool = NewPool();
            pool.Advance(0);
            pool.TrySpawn(EffectKind.Spark, 0);
            pool.Advance(50);
            pool.TrySpawn(EffectKind.Steam, 1);
            pool.Advance(60);
            pool.TrySpawn(EffectKind.BuildDust, 2);
            pool.Advance(150);

            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 3), Is.EqualTo(EffectSpawnResult.Spawned), "the pop wins a renderer");
            Assert.That(pool.ActiveCount, Is.EqualTo(3), "still at most 3 play");
            Assert.That(pool.DrainingCount, Is.EqualTo(1));
            var tail = SlotOf(pool, EffectKind.Spark, 0);
            Assert.That(pool.StateOf(tail), Is.EqualTo(EffectSlotState.Draining), "the oldest decorative effect drains");
            Assert.That(SlotOf(pool, EffectKind.Steam, 1), Is.GreaterThanOrEqualTo(0));
            Assert.That(pool.StateOf(SlotOf(pool, EffectKind.Steam, 1)), Is.EqualTo(EffectSlotState.Active));
            Assert.That(pool.StateOf(SlotOf(pool, EffectKind.CompletionPop, 3)), Is.EqualTo(EffectSlotState.Active));
        }

        [Test]
        public void ADrainingTailPlaysItsRemainingFramesFadingAndNeverPopsOut()
        {
            var pool = NewPool();
            pool.TrySpawn(EffectKind.Spark, 0);
            pool.TrySpawn(EffectKind.Spark, 1);
            pool.TrySpawn(EffectKind.Spark, 2);
            pool.Advance(150);
            var cancelled = SlotOf(pool, EffectKind.Spark, 0);
            var frameAtCancel = pool.FrameOf(cancelled);
            Assert.That(frameAtCancel, Is.EqualTo(1));
            pool.TrySpawn(EffectKind.CompletionPop, 3);

            Assert.That(pool.StateOf(cancelled), Is.EqualTo(EffectSlotState.Draining));
            Assert.That(pool.FrameOf(cancelled), Is.EqualTo(frameAtCancel), "the tail continues at the frame it was cancelled on");
            Assert.That(pool.AlphaOf(cancelled), Is.EqualTo(1f).Within(1e-4f), "it starts fully visible");
            var last = 1f;
            for (var step = 1; step <= 4; step++)
            {
                pool.Advance(150 + step * 100);
                if (pool.StateOf(cancelled) == EffectSlotState.Free)
                {
                    break;
                }

                Assert.That(pool.FrameOf(cancelled), Is.EqualTo(frameAtCancel + step));
                Assert.That(pool.AlphaOf(cancelled), Is.LessThan(last), "it fades with every frame");
                last = pool.AlphaOf(cancelled);
            }

            pool.Advance(150 + (Frames - frameAtCancel) * 100);
            Assert.That(pool.StateOf(cancelled), Is.EqualTo(EffectSlotState.Free), "the tail is gone after its last frame");
        }

        [Test]
        public void APopIsDroppedOnlyWhenAllThreeActiveEffectsArePops()
        {
            var pool = NewPool();
            for (var location = 0; location < 3; location++)
            {
                Assert.That(pool.TrySpawn(EffectKind.CompletionPop, location), Is.EqualTo(EffectSpawnResult.Spawned));
            }

            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 3), Is.EqualTo(EffectSpawnResult.NoCapacity), "a burst the world cannot show anyway");
            Assert.That(pool.DrainingCount, Is.Zero, "a pop never cancels a pop");
            Assert.That(pool.DroppedTotal, Is.EqualTo(1));
        }

        [Test]
        public void ChurnNeverExceedsThreeActiveFiveTailsOrEightRenderers()
        {
            var pool = NewPool(20);
            var random = new System.Random(529);
            long now = 0;
            for (var step = 0; step < 4000; step++)
            {
                now += random.Next(0, 90);
                pool.Advance(now);
                var kind = (EffectKind)random.Next(0, EffectPool.KindCount);
                var result = pool.TrySpawn(kind, random.Next(0, 20), random.Next(0, 4) * 2 + 4);
                Assert.That(pool.ActiveCount, Is.LessThanOrEqualTo(EffectPool.ActiveLimit), "step " + step);
                Assert.That(pool.DrainingCount, Is.LessThanOrEqualTo(EffectPool.DrainingLimit), "step " + step);
                Assert.That(pool.ActiveCount + pool.DrainingCount, Is.LessThanOrEqualTo(EffectPool.RendererCount), "step " + step);
                if (result == EffectSpawnResult.NoCapacity)
                {
                    var pops = Enumerable.Range(0, EffectPool.RendererCount)
                        .Count(i => pool.StateOf(i) == EffectSlotState.Active && pool.KindOf(i) == EffectKind.CompletionPop);
                    Assert.That(pool.ActiveCount, Is.EqualTo(3), "a drop needs a full active set");
                    Assert.That(EffectPool.PriorityOf(kind) == 0 || pops == 3, Is.True, "a pop is dropped only behind 3 pops, step " + step);
                }

                for (var slot = 0; slot < EffectPool.RendererCount; slot++)
                {
                    if (pool.StateOf(slot) != EffectSlotState.Free)
                    {
                        Assert.That(pool.FrameOf(slot), Is.InRange(0, Frames - 1), "an occupied renderer always shows a real frame, step " + step);
                        Assert.That(pool.AlphaOf(slot), Is.InRange(0f, 1f));
                    }
                }
            }
        }

        // ---- Coalescing: 250 ms per location and kind, timestamps sized from LocationCount ---------------------------------------------

        [Test]
        public void TheSameKindAtTheSameLocationCoalescesWithin250Milliseconds()
        {
            var pool = NewPool();
            pool.Advance(1_000);
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 1), Is.EqualTo(EffectSpawnResult.Spawned));
            pool.Advance(1_249);
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 1), Is.EqualTo(EffectSpawnResult.Coalesced));
            Assert.That(pool.CoalescedTotal, Is.EqualTo(1));
            pool.Advance(1_250);
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 1), Is.EqualTo(EffectSpawnResult.Spawned), "the window is exactly 250 ms");
            Assert.That(pool.SpawnedTotal, Is.EqualTo(2));
        }

        [Test]
        public void AnotherLocationOrAnotherKindDoesNotCoalesce()
        {
            var pool = NewPool();
            pool.Advance(500);
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 1), Is.EqualTo(EffectSpawnResult.Spawned));
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 2), Is.EqualTo(EffectSpawnResult.Spawned), "another location");
            Assert.That(pool.TrySpawn(EffectKind.UpgradeGlint, 1), Is.EqualTo(EffectSpawnResult.Spawned), "another kind");
        }

        [Test]
        public void TheTimestampsAreSizedFromTheLocationCountAndKeepTheirValuesWhenTheLayoutGrows()
        {
            var pool = NewPool(6);
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 6), Is.EqualTo(EffectSpawnResult.InvalidLocation), "6 locations: id 6 is not one");
            pool.Advance(1_000);
            pool.TrySpawn(EffectKind.CompletionPop, 5);
            pool.EnsureLocations(20);
            Assert.That(pool.LocationCount, Is.EqualTo(20));
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 19), Is.EqualTo(EffectSpawnResult.Spawned), "a 20-location layout is not capped at 12");
            pool.Advance(1_100);
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 5), Is.EqualTo(EffectSpawnResult.Coalesced), "the old stamp survived the growth");
            pool.EnsureLocations(3);
            Assert.That(pool.LocationCount, Is.EqualTo(20), "the table never shrinks");
        }

        // ---- Reduced motion, missing art -----------------------------------------------------------------------------------------------

        [Test]
        public void ReducedMotionSpawnsNothingAndClearsWhatPlays()
        {
            var pool = NewPool();
            pool.TrySpawn(EffectKind.CompletionPop, 0);
            pool.TrySpawn(EffectKind.Spark, 1);
            pool.SetReducedMotion(true);
            Assert.That(pool.ActiveCount + pool.DrainingCount, Is.Zero, "no moving pixel under reduced motion");
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 2), Is.EqualTo(EffectSpawnResult.Reduced));
            pool.Spawn(MachineResponseKind.UpgradeGlint, 2);
            Assert.That(pool.ActiveCount, Is.Zero, "the card's response is the static highlight instead");
            pool.SetReducedMotion(false);
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 2), Is.EqualTo(EffectSpawnResult.Spawned), "motion on again");
        }

        [Test]
        public void AKindWithoutArtIsUnavailableAndNeverThrows()
        {
            var pool = NewPool();
            pool.SetKindArt(EffectKind.Steam, 0, 0);
            Assert.That(pool.TrySpawn(EffectKind.Steam, 0), Is.EqualTo(EffectSpawnResult.Unavailable));
            Assert.That(new EffectPool().TrySpawn(EffectKind.CompletionPop, 0), Is.EqualTo(EffectSpawnResult.InvalidLocation), "an unsized pool has no location");
        }

        [Test]
        public void TheMachineResponsesMapToTheGlintAndTheSpark()
        {
            var pool = NewPool();
            pool.Spawn(MachineResponseKind.UpgradeGlint, 1);
            pool.Spawn(MachineResponseKind.SwitchOn, 2);
            Assert.That(SlotOf(pool, EffectKind.UpgradeGlint, 1), Is.GreaterThanOrEqualTo(0));
            Assert.That(SlotOf(pool, EffectKind.Spark, 2), Is.GreaterThanOrEqualTo(0));
            IMachineEffects sink = pool;
            Assert.That(sink, Is.Not.Null, "the pool is the card's effect sink");
        }

        // ---- Pace: the family cap and the next frame 0 ------------------------------------------------------------------------------

        [Test]
        public void APaceChangeAppliesFromTheNextSpawnNeverInTheMiddleOfAnEffect()
        {
            var pool = NewPool();
            pool.SetLocationFps(0, 10);
            pool.Advance(0);
            pool.TrySpawn(EffectKind.CompletionPop, 0);
            var first = SlotOf(pool, EffectKind.CompletionPop, 0);
            pool.SetLocationFps(0, 6);
            pool.Advance(200);
            Assert.That(pool.FpsOf(first), Is.EqualTo(10), "the running effect keeps its rate");
            Assert.That(pool.FrameOf(first), Is.EqualTo(2));
            pool.Advance(500);
            Assert.That(pool.TrySpawn(EffectKind.CompletionPop, 0), Is.EqualTo(EffectSpawnResult.Spawned));
            var second = Enumerable.Range(0, EffectPool.RendererCount).First(i => i != first && pool.StateOf(i) == EffectSlotState.Active && pool.KindOf(i) == EffectKind.CompletionPop);
            Assert.That(pool.FpsOf(second), Is.EqualTo(6), "the next effect starts at frame 0 with the new rate");
            Assert.That(pool.FrameOf(second), Is.Zero);
        }

        [Test]
        public void ThePopOfAFastAssemblerPlaysAtTheFamilyCapNotAtTenFramesPerSecond()
        {
            var model = new MachinePresentationModel(WorldCompositionSettings.BuildFamilyFpsCaps(WorldCompositionSettings.NewDefaultFamilyFpsCaps()));
            var pool = NewPool();
            long revision = 1;
            PresentationModelSynth.Models Frame(long completed) => PresentationModelSynth.Build(
                1, revision++, new[] { Machine(0, completed, m => { m.MachineId = RunCommandFixtures.Assembler; m.CycleTicks = Second / 2; }) });

            var a = Frame(0);
            model.Refresh(Layout, a.Factory, a.Views, a.Views.Length);
            var b = Frame(1);
            model.Refresh(Layout, b.Factory, b.Views, b.Views.Length);
            Assert.That(model[0].Pace, Is.EqualTo(ActivityPace.Fast));
            Assert.That(model[0].ActivityFps, Is.EqualTo(8), "the Assembler never runs above 8 fps");
            Assert.That(pool.ConsumeCompletions(model), Is.EqualTo(1));
            Assert.That(pool.FpsOf(SlotOf(pool, EffectKind.CompletionPop, 0)), Is.EqualTo(8), "the pop follows the capped rate of its machine");
        }

        // ---- Wiring: CompletionsDelta spawns a pop, nothing else does -----------------------------------------------------------------

        [Test]
        public void ACompletionDeltaSpawnsOnePopAtItsLocation()
        {
            var model = new MachinePresentationModel();
            var pool = NewPool();
            Step(model, 1, 0, 0);
            Assert.That(pool.ConsumeCompletions(model), Is.Zero, "the first refresh is a baseline reset");
            Step(model, 2, 1, 0);
            Assert.That(pool.ConsumeCompletions(model), Is.EqualTo(1));
            Assert.That(SlotOf(pool, EffectKind.CompletionPop, 0), Is.GreaterThanOrEqualTo(0));
            Assert.That(SlotOf(pool, EffectKind.CompletionPop, 1), Is.EqualTo(-1), "a machine without a completion shows no pop");
        }

        [Test]
        public void AnOfflineDeltaAndABaselineResetSpawnNothing()
        {
            var model = new MachinePresentationModel();
            var pool = NewPool();
            Step(model, 1, 0, 0);
            pool.ConsumeCompletions(model);

            // The player was away: 40 cycles passed. The refresh after the return suppresses the deltas.
            var offline = PresentationModelSynth.Build(1, 2, new[] { Machine(0, 40), Machine(1, 12) });
            model.Refresh(Layout, offline.Factory, offline.Views, offline.Views.Length, offline.Rates, suppressDeltas: true);
            Assert.That(pool.ConsumeCompletions(model), Is.Zero, "no effect for offline arrivals");

            // A session replace: the baseline is forgotten, so even a counter above the old one is a first sight.
            model.ResetBaseline();
            Step(model, 3, 100, 100);
            Assert.That(pool.ConsumeCompletions(model), Is.Zero, "a baseline reset spawns nothing");
            Assert.That(pool.SpawnedTotal, Is.Zero);
            Step(model, 4, 101, 100);
            Assert.That(pool.ConsumeCompletions(model), Is.EqualTo(1), "the next real completion does");
        }

        [Test]
        public void ReadingTheModelInEveryFrameSpawnsAPopOnce()
        {
            var model = new MachinePresentationModel();
            var pool = NewPool();
            Step(model, 1, 0, 0);
            pool.ConsumeCompletions(model);
            var built = Step(model, 2, 1, 0);
            Assert.That(pool.ConsumeCompletions(model), Is.EqualTo(1));
            var generation = model.Generation;

            // P2-F10: the same buffer read twice without a refresh, then 60 frames refreshed from the cached snapshot (the revisions did not move).
            Assert.That(pool.ConsumeCompletions(model), Is.Zero, "the same generation is consumed once");
            for (var frame = 1; frame <= 60; frame++)
            {
                model.Refresh(Layout, built.Factory, built.Views, built.Views.Length, built.Rates);
                pool.Advance(frame * 16L);
                Assert.That(pool.ConsumeCompletions(model), Is.Zero, "frame " + frame);
            }

            Assert.That(model.Generation, Is.EqualTo(generation), "a skipped refresh does not rebuild the buffer");
            Assert.That(pool.SpawnedTotal, Is.EqualTo(1), "one completion, one pop, over 60 frames");
        }

        [Test]
        public void AForcedRefreshRebuildsWhenTheRevisionsDidNotMove()
        {
            var model = new MachinePresentationModel();
            Step(model, 1, 0);
            var same = PresentationModelSynth.Build(1, 1, new[] { Machine(0, 2) });
            Assert.That(model.Refresh(Layout, same.Factory, same.Views, same.Views.Length), Is.False);
            Assert.That(model.Refresh(Layout, same.Factory, same.Views, same.Views.Length, null, false, true), Is.True);
            Assert.That(model[0].CompletionsDelta, Is.EqualTo(2), "the caller saw the counter move although no quantity changed");
        }

        // ---- A real session --------------------------------------------------------------------------------------------------------------

        [Test]
        public void ARealCycleCompletionBumpsTheQuantityRevisionAndSpawnsOnePopThroughTheFeed()
        {
            var rig = RunCommandFixtures.NewRig();
            Assert.That(rig.Commands.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            var pool = NewPool();
            var feed = new WorldEffectsFeed(rig.Session, pool);
            feed.Update(Layout, 0, false);
            Assert.That(pool.SpawnedTotal, Is.Zero, "the first frame takes the baseline");

            var before = rig.Session.QuantityRevision;
            rig.Advance(2 * Second); // one cycle of the 2 s miner
            Assert.That(rig.Session.QuantityRevision, Is.GreaterThan(before), "a cycle completion bumps the quantity revision (P2-F12)");

            feed.Update(Layout, 200, false);
            Assert.That(pool.SpawnedTotal, Is.EqualTo(1), "the completion spawned one pop");
            Assert.That(SlotOf(pool, EffectKind.CompletionPop, 0), Is.GreaterThanOrEqualTo(0));
            for (var frame = 1; frame <= 90; frame++)
            {
                feed.Update(Layout, 200 + frame * 16L, false);
            }

            Assert.That(pool.SpawnedTotal, Is.EqualTo(1), "90 more frames spawn no second pop");

            rig.Advance(2 * Second);
            feed.Update(Layout, 2_000, false);
            Assert.That(pool.SpawnedTotal, Is.EqualTo(2), "the next cycle shows its own pop");
        }

        [Test]
        public void AnOfflineReturnThroughTheFeedSpawnsNothingForTheCyclesThatPassed()
        {
            var rig = RunCommandFixtures.NewRig();
            Assert.That(rig.Commands.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            var pool = NewPool();
            var feed = new WorldEffectsFeed(rig.Session, pool);
            feed.Update(Layout, 0, false);

            rig.Advance(30 * Second); // the player was away for 15 cycles
            feed.ResetModels(true);
            feed.Update(Layout, 500, false);
            Assert.That(pool.SpawnedTotal, Is.Zero, "no effect for offline arrivals");

            rig.Advance(2 * Second);
            feed.Update(Layout, 900, false);
            Assert.That(pool.SpawnedTotal, Is.EqualTo(1), "the first live completion after the return does");
        }

        [Test]
        public void AReplacedSessionResetsTheBaselineRaisesTheEventAndSpawnsNothing()
        {
            var rig = RunCommandFixtures.NewRig();
            Assert.That(rig.Commands.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            var pool = NewPool();
            var feed = new WorldEffectsFeed(rig.Session, pool);
            var replaced = 0;
            feed.SessionReplaced += () => replaced++;
            feed.Update(Layout, 0, false);
            rig.Advance(6 * Second);
            feed.Update(Layout, 300, false);
            Assert.That(pool.SpawnedTotal, Is.EqualTo(1));

            // A new run (prestige, a restore of another run): a different run id, the same instance id and slot.
            var loaded = new RunState(rig.Catalog, RunSimHarness.Id(9001), 1, null, 100_000, 0, 1, 1,
                new[] { "iron.ore", "iron.bar", "iron.plate", "iron.gear", RunCommandFixtures.Miner });
            RunSimHarness.Add(loaded, RunCommandFixtures.Miner, 0, 1);
            rig.Session.ReplaceLoaded(loaded, rig.Session.Permanent, rig.Session.TimelineUtcMs);
            feed.Update(Layout, 800, false);

            Assert.That(replaced, Is.EqualTo(1), "the owner is told once so it can invalidate the attention model");
            Assert.That(pool.SpawnedTotal, Is.EqualTo(1), "no effect for the replaced session");
            Assert.That(pool.ActiveCount + pool.DrainingCount, Is.Zero, "the pool was emptied");
            feed.Update(Layout, 900, false);
            Assert.That(replaced, Is.EqualTo(1));
        }

        [Test]
        public void ReducedMotionThroughTheFeedSpawnsNothing()
        {
            var rig = RunCommandFixtures.NewRig();
            Assert.That(rig.Commands.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            var pool = NewPool();
            var feed = new WorldEffectsFeed(rig.Session, pool);
            feed.Update(Layout, 0, true);
            rig.Advance(2 * Second);
            feed.Update(Layout, 300, true);
            Assert.That(pool.SpawnedTotal, Is.Zero);
            Assert.That(pool.ReducedMotion, Is.True);
        }


        [Test]
        public void TheFeedsSteadyFramesAllocateNothingOnARealSession()
        {
            var rig = RunCommandFixtures.NewRig();
            Assert.That(rig.Commands.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(rig.Commands.Build(1, RunCommandFixtures.Miner).IsSuccess, Is.True);
            var pool = NewPool();
            var feed = new WorldEffectsFeed(rig.Session, pool);
            for (var frame = 0; frame < 5; frame++)
            {
                feed.Update(Layout, frame * 16L, false);
            }

            long now = 1_000;
            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var frame = 0; frame < 300; frame++)
                {
                    now += 16;
                    feed.Update(Layout, now, false);
                }
            });
            Assert.That(allocations, Is.Zero, "with nothing changed a frame reads the machine rows and refreshes the model from the cached snapshot");
        }

        [Test]
        public void EveryEffectKindHasItsRegistryRowWithSixFrames()
        {
            for (var kind = 0; kind < EffectPool.KindCount; kind++)
            {
                var key = EffectPool.SpriteKeyOf((EffectKind)kind);
                Assert.That(PresentationSpriteCatalog.TryGetRegistryRequirement(key, out var row), Is.True, key);
                Assert.That(row.MinFrames, Is.EqualTo(Frames), key + ": the effect lasts 6 frames");
                Assert.That(row.MaxFrames, Is.EqualTo(Frames), key);
            }
        }

        // ---- Allocations -------------------------------------------------------------------------------------------------------------------

        [Test]
        public void SpawnCoalesceAndAdvanceAllocateNothingInTheSteadyState()
        {
            var pool = NewPool(12);
            var model = new MachinePresentationModel();
            var snapshots = new List<PresentationModelSynth.Models>();
            for (var q = 1; q <= 8; q++)
            {
                snapshots.Add(PresentationModelSynth.Build(1, q, new[] { Machine(0, q), Machine(1, q), Machine(2, q) }));
            }

            foreach (var s in snapshots)
            {
                model.Refresh(Layout, s.Factory, s.Views, s.Views.Length, s.Rates);
                pool.ConsumeCompletions(model);
            }

            long now = 10_000;
            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var round = 0; round < 40; round++)
                {
                    foreach (var s in snapshots)
                    {
                        now += 70;
                        pool.Advance(now);
                        model.Refresh(Layout, s.Factory, s.Views, s.Views.Length, s.Rates, false, true);
                        pool.ConsumeCompletions(model);
                        pool.TrySpawn(EffectKind.Spark, (int)(now % 6));
                        pool.Spawn(MachineResponseKind.UpgradeGlint, (int)(now % 5));
                        _ = pool.FrameOf(0) + pool.AlphaOf(0);
                    }
                }
            });
            Assert.That(allocations, Is.Zero, "the pool and the model reuse their buffers");
            Assert.That(pool.CoalescedTotal + pool.DroppedTotal, Is.GreaterThan(0), "the run exercised coalescing and the cap");
        }
    }
}
