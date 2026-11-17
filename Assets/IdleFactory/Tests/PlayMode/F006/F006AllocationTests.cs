using System.Collections;
using System.Collections.Generic;
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
