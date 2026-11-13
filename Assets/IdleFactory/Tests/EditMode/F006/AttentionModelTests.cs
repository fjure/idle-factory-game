using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T532 (Feature 006, research R09, contracts/presentation-model.md section 3, FR-026, FR-028, FR-030): production attention, the "short on"
    /// signal and system attention. The G-2 and G-3 cases (the shortage is a steady-state coverage ratio, and "no demand" is not 0 %) run on exact
    /// synthetic read models and on a real session.
    /// </summary>
    public sealed class AttentionModelTests
    {
        private const string Ore = "iron.ore";
        private const string Bar = "iron.bar";
        private const string Plate = "iron.plate";
        private const string Gear = "iron.gear";

        private static readonly FactoryWorldLayout Six = TestLayouts.Six();

        private static SynthMachine Blocked(int slot, params string[] missing) =>
            new SynthMachine { Slot = slot, Blocker = MachineBlocker.MissingInput, Phase = MachinePhase.Paused, Missing = missing };

        private static SynthMachine Running(int slot) => new SynthMachine { Slot = slot };

        private static SynthMachine Full(int slot) =>
            new SynthMachine { Slot = slot, Blocker = MachineBlocker.StorageFull, Phase = MachinePhase.Paused };

        private static AttentionModel Refresh(
            IList<SynthMachine> machines, IList<SynthResource> resources = null, FactoryWorldLayout layout = null, SystemAttentionFlags flags = SystemAttentionFlags.None,
            AttentionModel model = null, long revision = 1)
        {
            model = model ?? new AttentionModel();
            var built = PresentationModelSynth.Build(1, revision, machines, resources);
            model.Refresh(layout ?? Six, built.Rates, flags);
            return model;
        }

        // ---------------------------------------------------------------------------------------------------
        // G-2: no MissingInput machine means no signal; a blocked machine shows even at full coverage
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void NoMissingInputMachineMeansNoSignalWhateverTheCoverage()
        {
            var model = Refresh(
                new[] { Running(0), Running(1), new SynthMachine { Slot = 2, Phase = MachinePhase.Waiting } },
                PresentationModelSynth.Resources(200_000, 0, 900_000, 0));
            Assert.That(model.ShortageCount, Is.Zero, "coverage 20 % with nobody blocked shows nothing (G-2)");
            Assert.That(model.MarkerCount, Is.Zero);
            Assert.That(model.Strip.IsVisible, Is.False);
            Assert.That(model.HasProductionAttention, Is.False);
        }

        [Test]
        public void ABlockedMachineShowsTheShortageEvenAtFullOrExcessCoverage()
        {
            var resources = PresentationModelSynth.Resources(1_500_000);
            resources[0].AutoSold = true;
            var model = Refresh(new[] { Blocked(1, Ore) }, resources);
            Assert.That(model.ShortageCount, Is.EqualTo(1), "an auto-sold input with coverage of 150 % and stock 0 is still a shortage (G-3)");
            Assert.That(model.ShortageResourceId(0), Is.EqualTo(Ore));
            Assert.That(model.ShortageCoveragePpm(0), Is.EqualTo(1_500_000));
            Assert.That(model.MarkerCount, Is.EqualTo(1));
            Assert.That(model.Strip.IsVisible, Is.True);
        }

        [Test]
        public void ACoverageBelowOneMillionWithABlockedMachineShows()
        {
            var model = Refresh(new[] { Blocked(0, Bar) }, PresentationModelSynth.Resources(0, 625_000));
            Assert.That(model.ShortageCount, Is.EqualTo(1));
            Assert.That(model.ShortageCoveragePpm(0), Is.EqualTo(625_000));
        }

        // ---------------------------------------------------------------------------------------------------
        // G-3: no demand
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void AResourceWithoutDemandIsNeverAShortageAndNeverReadsAsZeroPercent()
        {
            var resources = PresentationModelSynth.Resources();
            resources[0].HasDemand = false;
            var model = Refresh(new[] { Blocked(0, Ore) }, resources);
            Assert.That(model.ShortageCount, Is.Zero, "the HasDemand filter drops it, whatever blocks the machine");
            Assert.That(model.MarkerCount, Is.Zero);
            Assert.That(model.Strip.IsVisible, Is.False);
        }

        [Test]
        public void ANoDemandResourceDoesNotHideAShortageOfAnother()
        {
            var resources = PresentationModelSynth.Resources();
            resources[0].HasDemand = false;
            var model = Refresh(new[] { Blocked(0, Ore), Blocked(1, Bar) }, resources);
            Assert.That(model.ShortageCount, Is.EqualTo(1));
            Assert.That(model.ShortageResourceId(0), Is.EqualTo(Bar));
        }

        [Test]
        public void AResourceTheRunHasNotUnlockedIsNotAShortage()
        {
            var resources = PresentationModelSynth.Resources().Where(r => r.Id != Plate).ToList();
            var model = Refresh(new[] { Blocked(0, Plate) }, resources);
            Assert.That(model.ShortageCount, Is.Zero, "a locked resource is not in the rates snapshot");
        }

        [Test]
        public void OnlyActiveMissingInputMachinesCount()
        {
            var inactive = new SynthMachine { Slot = 0, Active = false, Blocker = MachineBlocker.MissingInput, Missing = new[] { Ore } };
            var storageFull = Full(1);
            storageFull.Missing = new[] { Bar };
            var model = Refresh(new[] { inactive, storageFull });
            Assert.That(model.ShortageCount, Is.Zero, "an inactive machine and a storage-full machine are not missing-input machines (StateFor precedence)");
        }

        // ---------------------------------------------------------------------------------------------------
        // Ranking and ties
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheOrderIsCoverageAscending()
        {
            // Content order is ore, bar, plate; coverage orders them bar (20 %), ore (70 %), plate (90 %).
            var model = Refresh(
                new[] { Blocked(0, Ore), Blocked(1, Bar), Blocked(2, Plate) },
                PresentationModelSynth.Resources(700_000, 200_000, 900_000));
            Assert.That(Enumerable.Range(0, 3).Select(model.ShortageResourceId), Is.EqualTo(new[] { Bar, Ore, Plate }));
            Assert.That(model.Strip.ResourceId, Is.EqualTo(Bar));
        }

        [Test]
        public void EqualCoverageIsBrokenByAffectedCountDescendingBeforeContentOrder()
        {
            // Ore is first in content order, but plate blocks two machines at the same coverage.
            var model = Refresh(
                new[] { Blocked(0, Ore), Blocked(1, Plate), Blocked(2, Plate) },
                PresentationModelSynth.Resources(300_000, 0, 300_000));
            Assert.That(model.ShortageResourceId(0), Is.EqualTo(Plate));
            Assert.That(model.ShortageAffectedCount(0), Is.EqualTo(2));
            Assert.That(model.ShortageResourceId(1), Is.EqualTo(Ore));
        }

        [Test]
        public void ATieOnCoverageAndCountFallsBackToContentOrder()
        {
            var model = Refresh(
                new[] { Blocked(0, Plate), Blocked(1, Ore), Blocked(2, Bar) },
                PresentationModelSynth.Resources(0, 0, 0));
            Assert.That(Enumerable.Range(0, 3).Select(model.ShortageResourceId), Is.EqualTo(new[] { Ore, Bar, Plate }), "inventory order: ore, bar, plate");
        }

        [Test]
        public void CoverageOutranksTheAffectedCount()
        {
            var model = Refresh(
                new[] { Blocked(0, Ore), Blocked(1, Bar), Blocked(2, Bar), Blocked(3, Bar) },
                PresentationModelSynth.Resources(100_000, 400_000));
            Assert.That(model.ShortageResourceId(0), Is.EqualTo(Ore), "a lower coverage wins even against three affected machines");
        }

        [Test]
        public void TheOrderDoesNotDependOnTheMachineOrder()
        {
            var forward = Refresh(new[] { Blocked(0, Ore), Blocked(1, Bar), Blocked(2, Plate) }, PresentationModelSynth.Resources(500, 500, 500));
            var backward = Refresh(new[] { Blocked(2, Plate), Blocked(1, Bar), Blocked(0, Ore) }, PresentationModelSynth.Resources(500, 500, 500));
            Assert.That(
                Enumerable.Range(0, 3).Select(backward.ShortageResourceId), Is.EqualTo(Enumerable.Range(0, 3).Select(forward.ShortageResourceId)));
        }

        // ---------------------------------------------------------------------------------------------------
        // Shared causes
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TwoOrMoreMachinesBlockedByTheSameResourceFormASharedCause()
        {
            var model = Refresh(new[] { Blocked(0, Ore), Blocked(2, Ore), Blocked(1, Bar) });
            Assert.That(model.SharedCauseCount, Is.EqualTo(1));
            Assert.That(model.SharedCauseResourceId(0), Is.EqualTo(Ore));
            Assert.That(model.SharedCauseAffectedCount(0), Is.EqualTo(2));
            var ore = Enumerable.Range(0, model.MarkerCount).Select(model.GetMarker).Single(m => m.ResourceId == Ore);
            var bar = Enumerable.Range(0, model.MarkerCount).Select(model.GetMarker).Single(m => m.ResourceId == Bar);
            Assert.That(ore.IsSharedCause, Is.True);
            Assert.That(bar.IsSharedCause, Is.False, "one machine is a shortage, not a shared cause");
        }

        [Test]
        public void AMachineShortOfTwoInputsCountsForBoth()
        {
            var model = Refresh(new[] { Blocked(0, Bar, Plate) });
            Assert.That(model.ShortageCount, Is.EqualTo(2));
            Assert.That(model.IsAffectedByShortage(0, new BuildingLocationId(0)), Is.True);
            Assert.That(model.IsAffectedByShortage(1, new BuildingLocationId(0)), Is.True);
            Assert.That(model.IsAffectedByShortage(0, new BuildingLocationId(1)), Is.False);
        }

        // ---------------------------------------------------------------------------------------------------
        // Several simultaneous shortage resources: at most two markers, the strip shows the top plus "+N"
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void OneShortageGivesOneMarkerAndAStripWithoutMore()
        {
            var model = Refresh(new[] { Blocked(1, Ore), Blocked(2, Ore) });
            Assert.That(model.MarkerCount, Is.EqualTo(1));
            Assert.That(model.Strip.AffectedCount, Is.EqualTo(2));
            Assert.That(model.Strip.MoreCount, Is.Zero);
        }

        [Test]
        public void TwoShortagesGiveTwoMarkersAndPlusOne()
        {
            var model = Refresh(new[] { Blocked(0, Ore), Blocked(3, Bar) }, PresentationModelSynth.Resources(100, 200));
            Assert.That(model.ShortageCount, Is.EqualTo(2));
            Assert.That(model.MarkerCount, Is.EqualTo(2));
            Assert.That(model.GetMarker(0).ResourceId, Is.EqualTo(Ore));
            Assert.That(model.GetMarker(1).ResourceId, Is.EqualTo(Bar));
            Assert.That(model.GetMarker(0).Rank, Is.Zero);
            Assert.That(model.GetMarker(1).Rank, Is.EqualTo(1));
            Assert.That(model.Strip.MoreCount, Is.EqualTo(1));
        }

        [Test]
        public void ThreeShortagesShowTheTopTwoMarkersAndTheStripCountsTheOthers()
        {
            var model = Refresh(
                new[] { Blocked(0, Ore), Blocked(1, Bar), Blocked(2, Plate), Blocked(3, Gear) },
                PresentationModelSynth.Resources(400, 300, 200, 100));
            Assert.That(model.ShortageCount, Is.EqualTo(4));
            Assert.That(model.MarkerCount, Is.EqualTo(AttentionModel.MaxShortageMarkers), "at most 2 visible");
            Assert.That(model.GetMarker(0).ResourceId, Is.EqualTo(Gear));
            Assert.That(model.GetMarker(1).ResourceId, Is.EqualTo(Plate));
            Assert.That(model.Strip.ResourceId, Is.EqualTo(Gear), "the strip shows the top resource");
            Assert.That(model.Strip.MoreCount, Is.EqualTo(3), "+3 for the other three resources");
            Assert.That(model.Strip.FirstAffected, Is.EqualTo(new BuildingLocationId(3)));
        }

        [Test]
        public void EveryMarkerNamesItsResourceForTheIcon()
        {
            var model = Refresh(new[] { Blocked(0, Ore), Blocked(3, Bar) });
            for (var i = 0; i < model.MarkerCount; i++)
            {
                Assert.That(model.GetMarker(i).Kind, Is.EqualTo(AttentionKind.Shortage));
                Assert.That(model.GetMarker(i).ResourceId, Is.Not.Null.And.Not.Empty, "a shortage marker shows its resource icon");
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Store full
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void AStorageFullMachineShowsTheDepotMarkerAtTheDepotCell()
        {
            var model = Refresh(new[] { Running(0), Full(4), Full(2) });
            Assert.That(model.StoreFull, Is.True);
            Assert.That(model.StoreFullCount, Is.EqualTo(2));
            Assert.That(model.ShortageCount, Is.Zero);
            Assert.That(model.MarkerCount, Is.EqualTo(1));
            var marker = model.GetMarker(0);
            Assert.That(marker.Kind, Is.EqualTo(AttentionKind.StoreFull));
            Assert.That(marker.ResourceId, Is.Null);
            Assert.That(marker.Position, Is.EqualTo(Six.DepotAnchor), "the depot marker position comes from the layout");
            Assert.That(marker.FirstAffected, Is.EqualTo(new BuildingLocationId(2)), "the first affected location is the lowest id");
            Assert.That(model.Strip.Kind, Is.EqualTo(AttentionKind.StoreFull));
            Assert.That(model.Strip.FirstAffected, Is.EqualTo(new BuildingLocationId(2)));
        }

        [Test]
        public void StoreFullAndAShortageShowTogetherAndTheStripLeadsWithTheShortage()
        {
            var model = Refresh(new[] { Blocked(0, Ore), Full(1) });
            Assert.That(model.MarkerCount, Is.EqualTo(2));
            Assert.That(model.GetMarker(0).Kind, Is.EqualTo(AttentionKind.Shortage));
            Assert.That(model.GetMarker(1).Kind, Is.EqualTo(AttentionKind.StoreFull));
            Assert.That(model.Strip.Kind, Is.EqualTo(AttentionKind.Shortage));
            Assert.That(model.Strip.MoreCount, Is.Zero, "store full is not a resource and is not counted in +N");
        }

        [Test]
        public void TwoShortagesAndStoreFullGiveThreeMarkers()
        {
            var model = Refresh(new[] { Blocked(0, Ore), Blocked(3, Bar), Blocked(4, Plate), Full(5) });
            Assert.That(model.MarkerCount, Is.EqualTo(AttentionModel.MaxMarkers));
            Assert.That(model.GetMarker(2).Kind, Is.EqualTo(AttentionKind.StoreFull));
        }

        // ---------------------------------------------------------------------------------------------------
        // Production versus system attention
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheHubDotComesOnlyFromTheFourSystemFlagsAndMarketHasNone()
        {
            Assert.That(Enum.GetNames(typeof(SystemAttentionFlags)), Is.EquivalentTo(new[] { "None", "Contracts", "Research", "Prestige", "Overdrive" }),
                "Market has no flag (U-4)");
            foreach (var flag in new[] { SystemAttentionFlags.Contracts, SystemAttentionFlags.Research, SystemAttentionFlags.Prestige, SystemAttentionFlags.Overdrive })
            {
                var model = Refresh(new[] { Running(0) }, null, null, flag);
                Assert.That(model.HubDot, Is.True, flag.ToString());
                Assert.That(model.SystemFlags, Is.EqualTo(flag));
            }
        }

        [Test]
        public void SystemAttentionNeverCreatesAMarkerOrAStripIndicator()
        {
            var model = Refresh(
                new[] { Running(0) }, null, null, SystemAttentionFlags.Contracts | SystemAttentionFlags.Research | SystemAttentionFlags.Prestige | SystemAttentionFlags.Overdrive);
            Assert.That(model.HubDot, Is.True);
            Assert.That(model.MarkerCount, Is.Zero);
            Assert.That(model.Strip.IsVisible, Is.False);
            Assert.That(model.HasProductionAttention, Is.False);
        }

        [Test]
        public void ProductionAttentionNeverDrivesTheHubDot()
        {
            var model = Refresh(new[] { Blocked(0, Ore), Full(1) });
            Assert.That(model.HasProductionAttention, Is.True);
            Assert.That(model.HubDot, Is.False);
            Assert.That(model.SystemFlags, Is.EqualTo(SystemAttentionFlags.None));
        }

        [Test]
        public void AFlagChangeAloneRefreshesTheModel()
        {
            var model = new AttentionModel();
            var built = PresentationModelSynth.Build(1, 1, new[] { Running(0) });
            Assert.That(model.Refresh(Six, built.Rates), Is.True);
            Assert.That(model.NeedsRefresh(1, 1, Six, SystemAttentionFlags.None), Is.False);
            Assert.That(model.NeedsRefresh(1, 1, Six, SystemAttentionFlags.Overdrive), Is.True);
            Assert.That(model.Refresh(Six, built.Rates, SystemAttentionFlags.Overdrive), Is.True);
            Assert.That(model.HubDot, Is.True);
            Assert.That(model.Refresh(Six, built.Rates), Is.True);
            Assert.That(model.HubDot, Is.False);
        }

        // ---------------------------------------------------------------------------------------------------
        // Selection target, copy, refresh gating, determinism, allocations
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheFirstAffectedLocationIsTheLowestIdAmongTheBlockedMachines()
        {
            var model = Refresh(new[] { Blocked(5, Ore), Blocked(3, Ore), Blocked(4, Ore) });
            Assert.That(model.GetMarker(0).FirstAffected, Is.EqualTo(new BuildingLocationId(3)), "a tap selects the first affected location");
            Assert.That(model.Strip.FirstAffected, Is.EqualTo(new BuildingLocationId(3)));
        }

        [Test]
        public void TheCopyIsShortOnAndTheResourceNeverBottleneck()
        {
            Assert.That(AttentionModel.ShortOnCopy("Iron Ore"), Is.EqualTo("Short on Iron Ore"));
            Assert.That(AttentionModel.ShortOnCopy("Gear").ToLowerInvariant(), Does.Not.Contain("bottleneck"));
        }

        [Test]
        public void TheSameRevisionIsNotRecomputedAndAChangedOneIs()
        {
            var model = new AttentionModel();
            var first = PresentationModelSynth.Build(1, 1, new[] { Blocked(0, Ore) });
            Assert.That(model.NeedsRefresh(1, 1, Six, SystemAttentionFlags.None), Is.True, "nothing computed yet");
            Assert.That(model.Refresh(Six, first.Rates), Is.True);
            var different = PresentationModelSynth.Build(1, 1, new[] { Running(0) });
            Assert.That(model.Refresh(Six, different.Rates), Is.False, "the same revisions leave the result untouched");
            Assert.That(model.ShortageCount, Is.EqualTo(1));
            Assert.That(model.NeedsRefresh(1, 2, Six, SystemAttentionFlags.None), Is.True);
            Assert.That(model.NeedsRefresh(2, 1, Six, SystemAttentionFlags.None), Is.True);
            Assert.That(model.NeedsRefresh(1, 1, TestLayouts.Twenty(), SystemAttentionFlags.None), Is.True, "another layout moves the markers");
            model.Invalidate();
            Assert.That(model.NeedsRefresh(1, 1, Six, SystemAttentionFlags.None), Is.True);
            Assert.That(model.Refresh(Six, different.Rates), Is.True);
            Assert.That(model.ShortageCount, Is.Zero);
        }

        [Test]
        public void TheSameInputsGiveTheSameMarkers()
        {
            var machines = new[] { Blocked(0, Ore), Blocked(1, Bar), Full(2) };
            var resources = PresentationModelSynth.Resources(100, 100);
            var a = Refresh(machines, resources);
            var b = Refresh(machines, resources);
            Assert.That(b.MarkerCount, Is.EqualTo(a.MarkerCount));
            for (var i = 0; i < a.MarkerCount; i++)
            {
                Assert.That(b.GetMarker(i).Position, Is.EqualTo(a.GetMarker(i).Position));
                Assert.That(b.GetMarker(i).ResourceId, Is.EqualTo(a.GetMarker(i).ResourceId));
                Assert.That(b.GetMarker(i).FirstAffected, Is.EqualTo(a.GetMarker(i).FirstAffected));
            }
        }

        [Test]
        public void ASteadyStateRefreshAllocatesNothing()
        {
            var model = new AttentionModel();
            var snapshots = new List<PresentationModelSynth.Models>();
            for (var q = 1; q <= 6; q++)
            {
                var machines = new List<SynthMachine> { Blocked(0, Ore), Blocked(1, Ore), Blocked(2, Bar), Full(3) };
                snapshots.Add(PresentationModelSynth.Build(1, q, machines, PresentationModelSynth.Resources(100 * q, 700, 50)));
            }

            foreach (var s in snapshots)
            {
                model.Invalidate();
                model.Refresh(Six, s.Rates, SystemAttentionFlags.Research);
            }

            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var round = 0; round < 20; round++)
                {
                    foreach (var s in snapshots)
                    {
                        model.Invalidate();
                        model.Refresh(Six, s.Rates, SystemAttentionFlags.Research);
                        _ = model.GetMarker(0).Position;
                        _ = model.Strip.MoreCount;
                        model.NeedsRefresh(1, 1, Six, SystemAttentionFlags.None);
                    }
                }
            });
            Assert.That(allocations, Is.Zero, "the refresh reuses its buffers");
        }

        // ---------------------------------------------------------------------------------------------------
        // Marker positions come from FactoryWorldLayout and never cover a machine, badge, marker box or hit shape
        // ---------------------------------------------------------------------------------------------------

        private static IEnumerable<FactoryWorldLayout> GeometryLayouts()
        {
            yield return TestLayouts.Six();
            yield return TestLayouts.Twenty();
            yield return TestLayouts.LShape();
            yield return LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait);
            yield return LatticeLayoutBuilder.Shipped(WorldOrientation.Landscape);
            var left = new LayoutComposition(0.5f, 1, new MarginCellRule(MarginSide.Left, 0), new MarginCellRule(MarginSide.Right, -1));
            var cells = new List<Vector2Int>();
            for (var row = 1; row >= 0; row--)
            {
                for (var column = 0; column < 3; column++)
                {
                    cells.Add(new Vector2Int(column, row));
                }
            }

            yield return new FactoryWorldLayout("test-six-left", cells, WorldOrientation.Portrait, left);
        }

        private static void AssertClearOfTheLattice(FactoryWorldLayout layout, Rect box, string what)
        {
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                Assert.That(FactoryWorldLayout.Overlaps(box, layout.FootprintOf(id)), Is.False, what + " covers the footprint of " + id);
                Assert.That(FactoryWorldLayout.Overlaps(box, layout.LocationCellRectOf(id)), Is.False, what + " covers the cell of " + id);
                for (var k = 0; k < FactoryWorldLayout.KeepOutCount; k++)
                {
                    Assert.That(FactoryWorldLayout.Overlaps(box, layout.KeepOutOf(id, k)), Is.False, what + " covers keep-out " + k + " of " + id);
                }

                var extent = StateCueGeometry.BadgeExtentBox();
                var badge = new Rect(layout.AnchorOf(id) + extent.position, extent.size);
                Assert.That(FactoryWorldLayout.Overlaps(box, badge), Is.False, what + " covers the badge and tail of " + id);
            }
        }

        [Test]
        public void EveryJunctionIsOnItsRowTrunkAndComesFromTheLayout()
        {
            foreach (var layout in GeometryLayouts())
            {
                for (var i = 0; i < layout.LocationCount; i++)
                {
                    var id = new BuildingLocationId(i);
                    var junction = AttentionModel.JunctionOf(layout, id);
                    var trunk = layout.TrunkOf(layout.RowOf(id));
                    Assert.That(trunk.Touches(junction), Is.True, layout.LayoutId + " " + id + ": the junction lies on its row trunk");
                    Assert.That(junction, Is.EqualTo(trunk.Points[1]));
                }
            }
        }

        [Test]
        public void AShortageMarkerSitsAtTheJunctionOfItsFirstAffectedRow()
        {
            var model = Refresh(new[] { Blocked(4, Ore) }); // location 4 is in the front row of the six-location layout
            var expected = AttentionModel.JunctionOf(Six, new BuildingLocationId(4));
            Assert.That(model.GetMarker(0).Position, Is.EqualTo(expected));
            Assert.That(Six.TrunkOf(Six.RowOf(new BuildingLocationId(4))).Touches(model.GetMarker(0).Position), Is.True);
        }

        [Test]
        public void NoMarkerBoxCoversAMachineABadgeAMarkerBoxOrAHitShapeOnAnyLayout()
        {
            foreach (var layout in GeometryLayouts())
            {
                var count = layout.LocationCount;
                for (var a = 0; a < count; a++)
                {
                    for (var b = 0; b < count; b++)
                    {
                        if (a == b)
                        {
                            continue;
                        }

                        var machines = new List<SynthMachine> { Blocked(a, Ore), Blocked(b, Bar), Full((a + 1) % count == b ? (b + 1) % count : (a + 1) % count) };
                        var model = Refresh(machines, PresentationModelSynth.Resources(0, 0), layout);
                        var boxes = new List<Rect>();
                        for (var i = 0; i < model.MarkerCount; i++)
                        {
                            var box = AttentionModel.MarkerBoxAt(model.GetMarker(i).Position);
                            AssertClearOfTheLattice(layout, box, layout.LayoutId + " marker " + i + " (a=" + a + ", b=" + b + ")");
                            foreach (var other in boxes)
                            {
                                Assert.That(FactoryWorldLayout.Overlaps(box, other), Is.False, layout.LayoutId + ": two markers overlap (a=" + a + ", b=" + b + ")");
                            }

                            boxes.Add(box);
                        }

                        Assert.That(model.MarkerCount, Is.EqualTo(3));
                    }
                }
            }
        }

        [Test]
        public void TwoShortageMarkersOfOneRowShareNoPoint()
        {
            var model = Refresh(new[] { Blocked(0, Ore), Blocked(1, Bar) }, PresentationModelSynth.Resources(0, 0));
            Assert.That(Six.RowOf(model.GetMarker(0).FirstAffected), Is.EqualTo(Six.RowOf(model.GetMarker(1).FirstAffected)));
            Assert.That(model.GetMarker(1).Position, Is.Not.EqualTo(model.GetMarker(0).Position));
            Assert.That(Six.TrunkOf(0).Touches(model.GetMarker(1).Position), Is.True, "the second marker stays on the trunk");
        }

        // ---------------------------------------------------------------------------------------------------
        // End to end on a real session
        // ---------------------------------------------------------------------------------------------------

        private static Guid Build(RunCommandFixtures.Rig rig, int slot, string machine)
        {
            var built = rig.Commands.Build(slot, machine);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            return built.Value.InstanceId;
        }

        private static AttentionModel FromSession(RunCommandFixtures.Rig rig, SystemAttentionFlags flags = SystemAttentionFlags.None)
        {
            var model = new AttentionModel();
            model.Refresh(Six, RunSnapshots.Rates(rig.Session), flags);
            return model;
        }

        [Test]
        public void AnAutoSoldInputThatNeverReachesTheStockIsAShortageEvenAtFullCoverage()
        {
            var rig = RunCommandFixtures.NewRig();
            Build(rig, 0, RunCommandFixtures.Miner);
            Build(rig, 1, RunCommandFixtures.Smelter);
            Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            rig.Advance(100_000_000);

            var rates = RunSnapshots.Rates(rig.Session);
            var ore = rates.Resources.Single(r => r.ResourceId == Ore);
            Assert.That(ore.SupplyCoveragePpm, Is.EqualTo(1_000_000), "the Miner exactly supplies the Smelter's demand");
            Assert.That(rates.Machines[1].PrimaryBlocker, Is.EqualTo(MachineBlocker.MissingInput));
            Assert.That(rates.Machines[1].Blockers.Any(b => b.ResourceId == Ore && b.AutoSold), Is.True, "auto-sell is the cause");

            var model = FromSession(rig);
            Assert.That(model.ShortageCount, Is.EqualTo(1));
            Assert.That(model.ShortageResourceId(0), Is.EqualTo(Ore));
            Assert.That(model.GetMarker(0).FirstAffected, Is.EqualTo(new BuildingLocationId(1)));
        }

        [Test]
        public void ACoverageBelowOneMillionShowsOnlyWhileAMachineIsActuallyBlocked()
        {
            RunCommandFixtures.Rig Two()
            {
                var built = RunCommandFixtures.NewRig();
                Build(built, 0, RunCommandFixtures.Miner);
                Build(built, 1, RunCommandFixtures.Smelter);
                Build(built, 2, RunCommandFixtures.Smelter);
                return built;
            }

            var stocked = Two();
            RunSimHarness.Stock(stocked.State, Ore, 100);
            var stockedRates = RunSnapshots.Rates(stocked.Session);
            Assert.That(stockedRates.Resources.Single(r => r.ResourceId == Ore).SupplyCoveragePpm, Is.EqualTo(500_000), "two Smelters demand twice the Miner's supply");
            Assert.That(stockedRates.Machines.Any(m => m.PrimaryBlocker == MachineBlocker.MissingInput), Is.False);
            var quiet = new AttentionModel();
            quiet.Refresh(Six, stockedRates);
            Assert.That(quiet.ShortageCount, Is.Zero, "coverage 50 % with stock on hand and nobody blocked shows no signal (G-2)");

            var empty = Two();
            var blocked = FromSession(empty);
            Assert.That(blocked.ShortageCount, Is.EqualTo(1), "the same factory without stock has two blocked Smelters");
            Assert.That(blocked.ShortageResourceId(0), Is.EqualTo(Ore));
            Assert.That(blocked.ShortageCoveragePpm(0), Is.EqualTo(500_000));
            Assert.That(blocked.ShortageAffectedCount(0), Is.EqualTo(2));
            Assert.That(blocked.SharedCauseCount, Is.EqualTo(1));
        }

        [Test]
        public void ThreeSimultaneousShortagesOnARealSessionShowTwoMarkersAndPlusTwo()
        {
            var rig = RunCommandFixtures.NewRig();
            Build(rig, 0, RunCommandFixtures.Smelter);
            Build(rig, 1, RunCommandFixtures.Press);
            Build(rig, 2, RunCommandFixtures.Assembler);

            var model = FromSession(rig);
            Assert.That(model.ShortageCount, Is.EqualTo(3));
            // Ore 0 %, Plate 62.5 % (one Press feeds an Assembler that wants 1.6 times as much), Bar 100 % (the Smelter exactly feeds the Press, but its Bars are not there yet).
            Assert.That(Enumerable.Range(0, 3).Select(model.ShortageResourceId), Is.EqualTo(new[] { Ore, Plate, Bar }), "coverage ascending, not inventory order");
            Assert.That(model.MarkerCount, Is.EqualTo(2));
            Assert.That(model.Strip.MoreCount, Is.EqualTo(2));
        }

        [Test]
        public void AStoreThatCannotTakeAnotherCycleShowsTheDepotMarkerOnARealSession()
        {
            var rig = RunCommandFixtures.NewRig();
            Build(rig, 0, RunCommandFixtures.Miner);
            RunSimHarness.Stock(rig.State, Ore, 999);

            var model = FromSession(rig);
            Assert.That(model.StoreFull, Is.True);
            Assert.That(model.GetMarker(0).Kind, Is.EqualTo(AttentionKind.StoreFull));
            Assert.That(model.GetMarker(0).Position, Is.EqualTo(Six.DepotAnchor));
            Assert.That(model.ShortageCount, Is.Zero);
        }

        [Test]
        public void ReadingTheModelDoesNotChangeTheSession()
        {
            var rig = RunCommandFixtures.NewRig();
            Build(rig, 0, RunCommandFixtures.Smelter);
            var before = RunCommandFixtures.Snapshot(rig.State);
            var revision = (rig.Session.StructureRevision, rig.Session.QuantityRevision);
            FromSession(rig, SystemAttentionFlags.Research);
            Assert.That(RunCommandFixtures.Snapshot(rig.State), Is.EqualTo(before));
            Assert.That((rig.Session.StructureRevision, rig.Session.QuantityRevision), Is.EqualTo(revision));
        }
    }
}
