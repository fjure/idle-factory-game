using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T529 and T527 (Feature 006, FR-006, FR-015, research R12, contracts/world-presentation.md section 4): once warm, the F006 world hot paths allocate
    /// nothing: the clock tick over a running world, the effect spawn, the coalescing and the draw of the 8 effect renderers, and the deck when nothing
    /// changed (the same layout, the same stage, identical conditions). Measured with the <c>GC.Alloc</c> recorder (<see cref="GcAllocationCounter"/>);
    /// the world is the synthetic rig of the deck tests, so the class also runs in the Windows test player.
    /// </summary>
    [Category("StandalonePlayer")]
    public sealed class F006AllocationTests
    {
        private const int Frames = 600;

        private DeckTestRig rig;

        [SetUp]
        public void SetUp() => rig = new DeckTestRig();

        [TearDown]
        public void TearDown() => rig.Dispose();

        private SlotGridView World(out FactoryDeckView deck, out SlotVisual[] frame)
        {
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var catalog = rig.Catalog();
            var grid = rig.NewGrid(catalog, layout);
            deck = rig.NewDeck(catalog, rig.Settings(), grid);
            deck.SetStage(1);
            grid.SetProjectedPixelsPerCell(24f);
            frame = DeckTestRig.Frame(12, i => i < 8 ? SlotCondition.Occupied : i < 10 ? SlotCondition.Empty : SlotCondition.Locked);
            grid.Render(frame);
            return grid;
        }

        [UnityTest]
        public IEnumerator TheClockTickOverARunningWorldAllocatesNothing()
        {
            var grid = World(out _, out _);
            var clock = new PresentationAnimationClock();
            for (var i = 0; i < 120; i++)
            {
                clock.Drive(i * 16L, grid);
            }

            yield return null;
            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var i = 120; i < 120 + Frames; i++)
                {
                    clock.Drive(i * 16L, grid);
                }
            });
            Assert.That(allocations, Is.Zero, "the shared clock steps every animated entry without allocating");
        }

        [UnityTest]
        public IEnumerator EffectSpawnCoalesceAndDrawAllocateNothing()
        {
            var grid = World(out _, out _);
            var pool = new EffectPool();
            var host = new UnityEngine.GameObject("effect-pool-view");
            rig.Owned.Add(host);
            var view = host.AddComponent<EffectPoolView>();
            view.Configure(rig.Catalog(), pool);
            view.Bind(grid);
            Assert.That(view.HasArt, Is.True, "the rig catalog carries the FX frames");
            Assert.That(view.Renderers.Count, Is.EqualTo(EffectPool.RendererCount));

            var locations = grid.Layout.LocationCount;
            long now = 0;
            void Frame(int i)
            {
                now += 16;
                pool.Advance(now);
                // Pops at 3 locations at once (the 4th is dropped), glints and sparks on top (decorative: dropped or draining), and a repeat within the window.
                pool.TrySpawn(EffectKind.CompletionPop, i % locations);
                pool.TrySpawn(EffectKind.CompletionPop, i % locations);
                pool.TrySpawn(EffectKind.CompletionPop, (i + 5) % locations);
                pool.Spawn(MachineResponseKind.UpgradeGlint, (i + 3) % locations);
                pool.Spawn(MachineResponseKind.SwitchOn, (i + 7) % locations);
                pool.SetLocationFps(i % locations, 6 + 2 * (i % 3));
                view.Present();
            }

            for (var i = 0; i < 240; i++)
            {
                Frame(i);
            }

            yield return null;
            var spawnedBefore = pool.SpawnedTotal;
            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var i = 240; i < 240 + Frames; i++)
                {
                    Frame(i);
                }
            });
            Assert.That(allocations, Is.Zero, "spawn, coalesce, cancel into a draining tail and draw allocate nothing in the steady state");
            Assert.That(pool.SpawnedTotal, Is.GreaterThan(spawnedBefore), "the run spawned effects");
            Assert.That(pool.CoalescedTotal, Is.GreaterThan(0), "the run coalesced repeats");
            Assert.That(pool.ActiveCount, Is.LessThanOrEqualTo(EffectPool.ActiveLimit));
            Assert.That(pool.DrainingCount, Is.LessThanOrEqualTo(EffectPool.DrainingLimit));
        }

        [UnityTest]
        public IEnumerator TheWorldEffectsFeedAllocatesNothingWhileEightMachinesCompleteRealCycles()
        {
            // A real session over the packaged Iron content (no editor API): 8 miners complete cycles for 600 frames while the feed and the view run.
            var catalog = RunPlayerFixtures.IronCatalog();
            var permanent = new PermanentState(catalog);
            var start = NextRunStart.Compute(catalog, permanent);
            var state = new RunState(catalog, System.Guid.NewGuid(), 1, null, 1_000_000_000_000L, 0, start.StorageLevel, 1, catalog.RunStart.InitialUnlocks);
            Assert.That(new RunSimulator(catalog).Advance(state, permanent, 0, RunPlayerFixtures.StartUtcMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            var session = new RunSession(catalog, state, permanent, RunPlayerFixtures.StartUtcMs, null);
            var commands = new RunCommands(session);
            var built = 0;
            for (var slot = 0; slot < 8; slot++)
            {
                var result = commands.Build(slot, "machine.miner");
                if (!result.IsSuccess)
                {
                    commands.UpgradeFactory();
                    result = commands.Build(slot, "machine.miner");
                }

                Assert.That(result.IsSuccess, Is.True, "slot " + slot + ": " + result.ReasonCode);
                built++;
            }

            Assert.That(built, Is.EqualTo(8), "8 machines are running");

            var grid = World(out _, out _);
            var pool = new EffectPool();
            var host = new UnityEngine.GameObject("effect-pool-view-real");
            rig.Owned.Add(host);
            var view = host.AddComponent<EffectPoolView>();
            view.Configure(rig.Catalog(), pool);
            view.Bind(grid);
            var feed = new WorldEffectsFeed(session, pool);
            var layout = grid.Layout;
            long now = 0;
            for (var i = 0; i < 30; i++)
            {
                now += 16;
                feed.Update(layout, now, false);
                view.Present();
            }

            yield return null;
            long allocations = 0;
            var steadyFrames = 0;
            var structuralFrames = 0;
            System.Action frameWork = () =>
            {
                feed.Update(layout, now, false);
                view.Present();
            };
            var spawnedBefore = pool.SpawnedTotal;
            for (var frame = 0; frame < Frames; frame++)
            {
                now += 100;
                Assert.That(session.Advance(1_000_000).IsSuccess, Is.True); // 100 ms of run time per frame: the session itself is not measured
                var rebuildsBefore = feed.Rebuilds;
                var counted = GcAllocationCounter.Count(frameWork);
                if (feed.Rebuilds != rebuildsBefore)
                {
                    // A structure revision change (a contract offer, an unlock) builds one full snapshot: the rare, allocating path, not the steady state.
                    structuralFrames++;
                    continue;
                }

                steadyFrames++;
                allocations += counted;
            }

            Assert.That(pool.SpawnedTotal, Is.GreaterThan(spawnedBefore), "completions happened inside the measured window");
            Assert.That(structuralFrames, Is.LessThanOrEqualTo(5), "structure changes are rare events, not the steady state");
            Assert.That(steadyFrames, Is.GreaterThanOrEqualTo(Frames - 5));
            Assert.That(allocations, Is.Zero, "SC-008: every steady frame of the world effects allocates nothing while cycles complete");
        }

        [UnityTest]
        public IEnumerator AWarmDeckWithNothingChangedAllocatesNothing()
        {
            var grid = World(out var deck, out var frame);
            var conditions = new SlotCondition[frame.Length];
            for (var i = 0; i < conditions.Length; i++)
            {
                conditions[i] = frame[i].Condition;
            }

            var layout = grid.Layout;
            var list = (IReadOnlyList<SlotCondition>)conditions;
            for (var i = 0; i < 5; i++)
            {
                deck.SetLayout(layout);
                deck.SetStage(1);
                deck.SetConditions(list);
                deck.Refresh();
                grid.Render(frame);
            }

            yield return null;
            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var i = 0; i < Frames; i++)
                {
                    deck.SetLayout(layout);
                    deck.SetStage(1);
                    deck.SetConditions(list);
                    deck.Refresh();
                }
            });
            Assert.That(allocations, Is.Zero, "the same layout, the same stage and identical conditions redraw nothing and allocate nothing");

            var renderAllocations = GcAllocationCounter.Count(() =>
            {
                for (var i = 0; i < Frames; i++)
                {
                    grid.Render(frame);
                }
            });
            Assert.That(renderAllocations, Is.Zero, "a repeated render of the same visuals reaches the deck as identical conditions and allocates nothing");
        }
    }
}
