#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>One screen class of the baseline matrix: a layout-test screen, or a device pixel size with a touch profile (the emulator).</summary>
    public readonly struct BaselineClass
    {
        public BaselineClass(ShellScreen screen, string slug, ShellDeviceProfile? profile)
        {
            Screen = screen;
            Slug = slug;
            Profile = profile;
        }

        public ShellScreen Screen { get; }
        public string Slug { get; }
        public ShellDeviceProfile? Profile { get; }

        public override string ToString() => Slug;
    }

    /// <summary>
    /// The F006 baseline matrix (T502, research R02): for every screen class and the emulator's 1080x2400 in both orientations, at 100 % and
    /// 200 % text, the shell geometry of five states and the interaction counts of the scripted journeys, measured on the current build.
    /// With IDLEFACTORY_F006_BASELINE_WRITE=1 the results and screenshots are written under specs/006-world-first-factory-experience/reviews/baseline/;
    /// otherwise the same run writes to the temporary cache, so the test is always a probe run and never changes the repository by itself.
    /// </summary>
    public sealed class BaselineMatrixTests
    {
        public const string WriteVariable = "IDLEFACTORY_F006_BASELINE_WRITE";
        public const string FeatureDirectory = "specs/006-world-first-factory-experience";
        private const float EmulatorDpi = 420f;

        public static readonly int[] TextScales = { 100, 200 };

        public static IEnumerable<BaselineClass> Classes()
        {
            foreach (var screen in ShellScreens.All())
            {
                yield return new BaselineClass(screen, Slug(screen.Name), null);
            }

            // The emulator is a 1080x2400 touch device at 420 dpi; the authored panel settings and the 7 mm rule give it its real panel scale.
            yield return new BaselineClass(
                new ShellScreen("emulator-portrait 1080x2400", 1080, 2400, 0, 0, 0, 0, FactoryHudLayout.CompactPortrait),
                "emulator-portrait-1080x2400", ShellDeviceProfile.Touch(1080, 2400, EmulatorDpi));
            yield return new BaselineClass(
                new ShellScreen("emulator-landscape 2400x1080", 2400, 1080, 0, 0, 0, 0, FactoryHudLayout.CompactLandscape),
                "emulator-landscape-2400x1080", ShellDeviceProfile.Touch(2400, 1080, EmulatorDpi));
        }

        private static string Slug(string name) => name.Replace(' ', '-');

        private static bool WriteMode => Environment.GetEnvironmentVariable(WriteVariable) == "1";

        private static string RepositoryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string OutputDirectory => WriteMode
            ? Path.Combine(RepositoryRoot, FeatureDirectory, "reviews", "baseline")
            : Path.Combine(Application.temporaryCachePath, "f006-baseline");

        [UnityTest]
        public IEnumerator TheBaselineOfAScreenClassIsMeasuredOnTheCurrentBuild([ValueSource(nameof(Classes))] BaselineClass screenClass)
        {
            var screen = screenClass.Screen;
            var runs = new List<string>();
            var png = new Dictionary<string, byte[]>();

            foreach (var percent in TextScales)
            {
                BaselineRig rig = null;
                yield return BaselineRig.Create(screen, percent, screenClass.Profile, created => rig = created);
                try
                {
                    var states = new List<ShellGeometryResult>();
                    var stopped = BaselineFixture.StoppedSlots(rig.Session);
                    Assert.That(stopped, Is.Not.Empty, "the baseline run has stopped machines");

                    yield return rig.ShowNoPanel();
                    yield return Capture(rig, "no-panel", percent, states, png);
                    yield return rig.ShowMachineSheet(stopped[0]);
                    yield return Capture(rig, "machine-sheet", percent, states, png);
                    yield return rig.ShowTab(ShellTab.Contracts);
                    yield return Capture(rig, "contracts", percent, states, png);
                    yield return rig.ShowBuildSheet(BaselineFixture.EmptySlot);
                    yield return Capture(rig, "build-sheet", percent, states, png);
                    yield return rig.ShowOfflineSummary();
                    yield return Capture(rig, "offline-summary", percent, states, png);

                    var closed = states[0];
                    Assert.That(closed.World.HasValue, Is.True, screenClass + " " + percent + " %: the no-panel state has a world rect");
                    foreach (var state in states)
                    {
                        Assert.That(state.World.HasValue, Is.True, screenClass + " " + percent + " % " + state.State + ": the shell reports a world rect");
                        state.Notes["worldAreaVsNoPanel"] = ShellGeometryProbe.Share(ShellGeometryProbe.Area(state.World.Value), ShellGeometryProbe.Area(closed.World.Value));
                    }

                    var journeys = new List<JourneyResult>();
                    yield return new InteractionCountJourney(rig).RunAll(journeys);
                    foreach (var journey in journeys)
                    {
                        Assert.That(journey.GoalReached, Is.True, screenClass + " " + percent + " %: " + journey.Name + " reached its goal (" + journey.Evidence + ")");
                    }

                    runs.Add(RunJson(rig, percent, states, journeys));
                }
                finally
                {
                    rig?.Dispose();
                }

                yield return null;
            }

            var json = ClassJson(screenClass, runs);
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(Path.Combine(OutputDirectory, screenClass.Slug + ".json"), json, new UTF8Encoding(false));
            var pngDirectory = Path.Combine(OutputDirectory, "png");
            Directory.CreateDirectory(pngDirectory);
            foreach (var pair in png)
            {
                File.WriteAllBytes(Path.Combine(pngDirectory, screenClass.Slug + "-" + pair.Key + ".png"), pair.Value);
            }

        }

        private static IEnumerator Capture(BaselineRig rig, string state, int percent, List<ShellGeometryResult> states, Dictionary<string, byte[]> png)
        {
            // Frames for the layout and the render target to settle before pixels are read.
            yield return rig.Settle();
            yield return null;
            yield return null;
            var result = rig.Measure(state);
            states.Add(result);
            png[state + "-" + percent] = rig.CapturePng();
        }

        private static string RunJson(BaselineRig rig, int percent, List<ShellGeometryResult> states, List<JourneyResult> journeys)
        {
            var builder = new StringBuilder();
            builder.Append("    {\n");
            builder.Append("      \"textScalePercent\": ").Append(percent).Append(",\n");
            builder.Append("      \"pixelsPerPoint\": ").Append(ShellGeometryProbe.Num(rig.Shell.PixelsPerPoint)).Append(",\n");
            builder.Append("      \"layout\": ").Append(ShellGeometryProbe.Quote(rig.ShellDocument.Layout.ToString())).Append(",\n");
            builder.Append("      \"states\": [\n");
            builder.Append(string.Join(",\n", states.Select(state => ShellGeometryProbe.ToJson(state, "        "))));
            builder.Append("\n      ],\n");
            builder.Append("      \"interactions\": [\n");
            builder.Append(string.Join(",\n", journeys.Select(journey => journey.ToJson("        "))));
            builder.Append("\n      ]\n");
            builder.Append("    }");
            return builder.ToString();
        }

        private static string ClassJson(BaselineClass screenClass, List<string> runs)
        {
            var screen = screenClass.Screen;
            var builder = new StringBuilder();
            builder.Append("{\n");
            builder.Append("  \"schema\": \"f006-baseline/1\",\n");
            builder.Append("  \"task\": \"T502\",\n");
            builder.Append("  \"class\": ").Append(ShellGeometryProbe.Quote(screen.Name)).Append(",\n");
            builder.Append("  \"screen\": {\"width\": ").Append(screen.Width).Append(", \"height\": ").Append(screen.Height)
                .Append(", \"safeInsetsPx\": {\"left\": ").Append(screen.Left).Append(", \"top\": ").Append(screen.Top).Append(", \"right\": ").Append(screen.Right).Append(", \"bottom\": ").Append(screen.Bottom).Append("}},\n");
            builder.Append("  \"device\": ").Append(screenClass.Profile.HasValue
                ? "{\"input\": \"touch\", \"dpi\": " + ShellGeometryProbe.Num(screenClass.Profile.Value.Dpi, 1) + "}"
                : "null").Append(",\n");
            builder.Append("  \"fixture\": ").Append(ShellGeometryProbe.Quote(BaselineFixture.Description)).Append(",\n");
            builder.Append("  \"runs\": [\n");
            builder.Append(string.Join(",\n", runs));
            builder.Append("\n  ]\n");
            builder.Append("}\n");
            return builder.ToString().Replace("\r\n", "\n");
        }
    }
}
#endif
