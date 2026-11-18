using System.Collections.Generic;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T528 (P2-F13): the tap priority between an attention marker and a machine tap shape. The decision (see <see cref="WorldTapResolver"/>): the visible icon of a
    /// marker wins, then a machine shape, then the marker's 48 pt touch circle. The first rule never conflicts with the second, because a marker icon never lies over
    /// a machine shape; the model-level proof is here for every test layout, and the view and presenter tests (Play Mode) cover the contested touch circle.
    /// </summary>
    public sealed class WorldTapPriorityTests
    {
        private static IEnumerable<string> Names => DeckTestSupport.LayoutNames;

        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            owned.Clear();
        }

        [TestCaseSource(nameof(Names))]
        public void AMarkerIconNeverLiesOverAMachineTapShapeOnAnyPositionTheModelUses(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var half = AttentionModel.MarkerSizePixels / 2;
            var centres = new List<Vector2> { layout.DepotAnchor };
            var shift = (layout.DepotOnRight ? -1f : 1f) * AttentionModel.SecondMarkerShiftPixels / SlotGridLayout.Density;
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var junction = AttentionModel.JunctionOf(layout, new BuildingLocationId(i));
                centres.Add(junction);
                centres.Add(new Vector2(junction.x + shift, junction.y));
            }

            foreach (var centre in centres)
            {
                for (var dx = -half; dx <= half; dx++)
                {
                    for (var dy = -half; dy <= half; dy++)
                    {
                        var point = centre + new Vector2(dx, dy) / SlotGridLayout.Density;
                        Assert.That(layout.TryHit(point, out var id), Is.False, name + ": the icon box around " + centre + " reaches the tap shape of " + id + " at " + point);
                    }
                }
            }
        }

        [Test]
        public void TheResolverReturnsEmptyWorldWithoutAMarkerViewAndMachineShapesStillWin()
        {
            var layout = TestLayouts.Six();
            var machineCentre = layout.PlateCentreOf(new BuildingLocationId(1));
            Assert.That(WorldTapResolver.Resolve(layout, null, machineCentre, out var id), Is.EqualTo(WorldTapSource.Machine));
            Assert.That(id, Is.EqualTo(new BuildingLocationId(1)));
            Assert.That(WorldTapResolver.Resolve(layout, null, new Vector2(-100f, -100f), out _), Is.EqualTo(WorldTapSource.None));
            Assert.That(WorldTapResolver.Resolve(layout, null, layout.DepositAnchor, out _), Is.EqualTo(WorldTapSource.None), "the deposit is never a hit target");
            Assert.That(WorldTapResolver.Resolve(layout, null, layout.DepotAnchor, out _), Is.EqualTo(WorldTapSource.None), "the depot is never a hit target");
        }

        [Test]
        public void AMarkerViewWithoutVisibleMarkersCatchesNothing()
        {
            var layout = TestLayouts.Six();
            var host = new GameObject("tap-priority-markers");
            owned.Add(host);
            var view = host.AddComponent<AttentionMarkerView>();
            view.Configure(new PresentationSpriteCatalog(), id => null);
            Assert.That(view.VisibleMarkerCount, Is.Zero);
            Assert.That(WorldTapResolver.Resolve(layout, view, AttentionModel.JunctionOf(layout, new BuildingLocationId(0)), out _), Is.EqualTo(WorldTapSource.None));
        }
    }
}
