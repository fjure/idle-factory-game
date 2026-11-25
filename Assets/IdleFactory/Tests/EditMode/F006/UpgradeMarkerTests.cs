using System;
using System.IO;
using IdleFactory.Run.Application;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T563 (Feature 006, FR-027, contracts/presentation-model.md section 2): the world's upgrade-available marker. The marker is a per-location overlay that
    /// reuses the approved 24 x 24 upgrade icon; these cases prove the rule that decides it (affordable, not under a problem badge), its cost seam for the
    /// presenter, and the properties of the art that make it unobtrusive (never red, smaller than a badge, clear of the other cues).
    /// </summary>
    public sealed class UpgradeMarkerTests
    {
        private const string IconPath = "Assets/IdleFactory/Art/F004/ui-icons/f003-ui-icon-upgrade_00.png";

        private static SlotVisual Describe(Action<SynthMachine> set, bool boosted = false)
        {
            var machine = new SynthMachine { Slot = 0 };
            set?.Invoke(machine);
            var built = PresentationModelSynth.Build(1, 1, new[] { machine });
            var factory = new RunFactorySnapshot(
                1, 1, 0, 1, 1, new[] { new SlotSnapshot(0, SlotStatus.Occupied, 0, 0) }, new[] { built.Factory.Machines[0] },
                new Guid[0], new MachineTypeSnapshot[0], null, null);
            var boostedIds = boosted ? new System.Collections.Generic.HashSet<string> { machine.MachineId } : null;
            return SlotGridModel.Describe(0, factory, key => key, false, boostedIds, -1);
        }

        [Test]
        public void AnAffordableUpgradeOnARunningMachineShowsTheMarker()
        {
            var visual = Describe(m => { m.UpgradeAvailable = true; m.UpgradeAffordable = true; });
            Assert.That(visual.UpgradeMarker, Is.True);
            Assert.That(visual.UpgradeCost, Is.EqualTo(10L), "the presenter compares this cost with the cash");
            Assert.That(visual.Description, Does.Contain("upgrade available"), "no state depends on the icon alone");
            Assert.That(visual.BadgeKey, Is.EqualTo(SlotGridModel.BadgeRunningKey), "a running machine has no badge sprite, so the marker is the only cue");
        }

        [TestCase(MachineBlocker.MissingInput, MachinePhase.Waiting)]
        [TestCase(MachineBlocker.StorageFull, MachinePhase.Waiting)]
        [TestCase(MachineBlocker.None, MachinePhase.Waiting)]
        public void AProblemBadgeHidesTheMarker(MachineBlocker blocker, MachinePhase phase)
        {
            var visual = Describe(m => { m.UpgradeAvailable = true; m.UpgradeAffordable = true; m.Blocker = blocker; m.Phase = phase; });
            Assert.That(visual.UpgradeMarker, Is.False, "lower priority than every problem cue");
            Assert.That(visual.UpgradeCost, Is.EqualTo(10L), "the cost still follows the machine, so the marker can return when the problem clears");
            Assert.That(visual.Description, Does.Not.Contain("upgrade available"));
        }

        [Test]
        public void ANeutralBadgeDoesNotHideTheMarker()
        {
            var paused = Describe(m => { m.UpgradeAvailable = true; m.UpgradeAffordable = true; m.Phase = MachinePhase.Paused; });
            var inactive = Describe(m => { m.UpgradeAvailable = true; m.UpgradeAffordable = true; m.Active = false; m.Blocker = MachineBlocker.Inactive; });
            Assert.That(paused.UpgradeMarker, Is.True, "paused is a neutral badge (Art Bible 6.2)");
            Assert.That(inactive.UpgradeMarker, Is.True, "inactive is a neutral badge (Art Bible 6.2)");
        }

        [Test]
        public void TheSlotRuleIsTheMachinePresentationRuleForEveryState()
        {
            foreach (var blocker in new[] { MachineBlocker.None, MachineBlocker.MissingInput, MachineBlocker.StorageFull, MachineBlocker.Inactive })
            {
                foreach (var phase in new[] { MachinePhase.Running, MachinePhase.Waiting, MachinePhase.Paused })
                {
                    foreach (var active in new[] { true, false })
                    {
                        var machine = new SynthMachine { Slot = 0, UpgradeAvailable = true, UpgradeAffordable = true, Blocker = blocker, Phase = phase, Active = active };
                        var visual = Describe(m => { m.UpgradeAvailable = true; m.UpgradeAffordable = true; m.Blocker = blocker; m.Phase = phase; m.Active = active; });
                        var model = new MachinePresentationModel();
                        var built = PresentationModelSynth.Build(1, 1, new[] { machine });
                        model.Refresh(TestLayouts.Six(), built.Factory, built.Views, built.Views.Length, built.Rates);
                        Assert.That(visual.UpgradeMarker, Is.EqualTo(model[0].UpgradeMarkerVisible), blocker + " " + phase + " active=" + active);
                    }
                }
            }
        }

        [Test]
        public void AnUnaffordableOrMissingUpgradeNeverShowsTheMarker()
        {
            var unaffordable = Describe(m => { m.UpgradeAvailable = true; m.UpgradeAffordable = false; });
            Assert.That(unaffordable.UpgradeMarker, Is.False);
            Assert.That(unaffordable.UpgradeCost, Is.EqualTo(10L), "an unaffordable upgrade keeps its cost: the presenter rebuilds when the cash reaches it");

            var maxed = Describe(m => { m.UpgradeAvailable = false; m.UpgradeAffordable = false; });
            Assert.That(maxed.UpgradeMarker, Is.False);
            Assert.That(maxed.UpgradeCost, Is.EqualTo(SlotGridModel.NoUpgradeCost), "no next upgrade, nothing to compare");
        }

        [Test]
        public void EmptyAndLockedLocationsCarryNoMarker()
        {
            var factory = new RunFactorySnapshot(
                1, 1, 0, 1, 2, new[] { new SlotSnapshot(0, SlotStatus.Empty, -1, 0), new SlotSnapshot(1, SlotStatus.Locked, -1, 3) },
                new MachineSnapshot[0], new Guid[0], new MachineTypeSnapshot[0], null, null);
            foreach (var visual in SlotGridModel.DescribeAll(factory, key => key, false, null, -1))
            {
                Assert.That(visual.UpgradeMarker, Is.False);
                Assert.That(visual.UpgradeCost, Is.EqualTo(SlotGridModel.NoUpgradeCost));
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // The art: the approved upgrade icon, drawn as is
        // ---------------------------------------------------------------------------------------------------

        private static Texture2D LoadIcon()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", IconPath));
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
            return texture;
        }

        private static void OpaqueBounds(Texture2D icon, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = icon.width;
            minY = icon.height;
            maxX = -1;
            maxY = -1;
            var pixels = icon.GetPixels32();
            for (var y = 0; y < icon.height; y++)
            {
                for (var x = 0; x < icon.width; x++)
                {
                    if (pixels[y * icon.width + x].a == 0)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
        }

        [Test]
        public void TheMarkerKeyIsTheApprovedUpgradeIconOfTheRegistry()
        {
            Assert.That(SlotGridModel.UpgradeMarkerKey, Is.EqualTo("F003-UI-ICON-UPGRADE"));
            var manifest = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Assets/IdleFactory/Art/F004/import-manifest.json")));
            Assert.That(manifest, Does.Contain("\"key\": \"" + SlotGridModel.UpgradeMarkerKey + "\""), "the approved F004 import manifest already holds the icon: no new art, no new key");
        }

        [Test]
        public void TheMarkerIsNeverRed()
        {
            var icon = LoadIcon();
            try
            {
                foreach (var pixel in icon.GetPixels())
                {
                    if (pixel.a <= 0f)
                    {
                        continue;
                    }

                    Color.RGBToHSV(pixel, out var hue, out var saturation, out var value);
                    var redHue = hue < 20f / 360f || hue > 340f / 360f;
                    Assert.That(redHue && saturation >= 0.35f && value >= 0.35f, Is.False, "an opaque pixel is red-saturated: " + pixel);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(icon);
            }
        }

        [Test]
        public void TheMarkerIsSmallerThanABadgeAtScaleOne()
        {
            var icon = LoadIcon();
            try
            {
                OpaqueBounds(icon, out var minX, out var minY, out var maxX, out var maxY);
                var width = maxX - minX + 1;
                var height = maxY - minY + 1;
                Assert.That(icon.width, Is.EqualTo(24));
                Assert.That(width, Is.LessThan(StateCueGeometry.BadgeSizePixels), "narrower than the 23 px badge box");
                Assert.That(height, Is.LessThan(StateCueGeometry.BadgeSizePixels + StateCueGeometry.BadgeTailPixels), "lower than the badge with its tail");
                Assert.That(width * height, Is.LessThan(StateCueGeometry.BadgeSizePixels * StateCueGeometry.BadgeSizePixels), "less area than the badge box");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(icon);
            }
        }

        [Test]
        public void TheMarkerKeepsClearOfTheBadgeAndTheSecondMarkerBox()
        {
            var icon = LoadIcon();
            try
            {
                OpaqueBounds(icon, out var minX, out var minY, out var maxX, out var maxY);
                var pixel = 1f / StateCueGeometry.ArtPixelsPerCell;
                var centre = StateCueGeometry.UpgradeMarkerOffset;
                // The icon has a centre pivot: its pixel (x, y) from the bottom-left lies (x + 0.5 - 12) px right of the marker position.
                var content = new Rect(
                    centre.x + (minX - icon.width * 0.5f) * pixel, centre.y + (minY - icon.height * 0.5f) * pixel,
                    (maxX - minX + 1) * pixel, (maxY - minY + 1) * pixel);
                Assert.That(FactoryWorldLayout.Overlaps(content, StateCueGeometry.BadgeExtentBox()), Is.False, "the marker never covers the badge, a neutral one included");
                Assert.That(FactoryWorldLayout.Overlaps(content, StateCueGeometry.MarkerBox(1)), Is.False, "the marker never covers the Overdrive marker");
                var plateLeft = -StateCueGeometry.PivotXPixels * pixel;
                Assert.That(content.xMin, Is.GreaterThanOrEqualTo(plateLeft), "the marker stays over its own machine footprint");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(icon);
            }
        }
    }
}
