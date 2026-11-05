#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.OfflineSummaryPanelTestSupport;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T441 (FR-014, US5): the restyled icons on every Feature 003 surface by key. A surface shows icons of one style only (no Feature 003
    /// placeholder beside a restyled icon), keeps its icons inside their boxes at 100 and 200 percent text, and keeps the tinted contrast; the
    /// start-up spinner stops under reduced motion; the HUD gear is the restyled glyph at the unchanged 44 pt target; a 420 dpi phone renders
    /// the 2 art px strokes at 2 device px or more.
    /// </summary>
    public sealed class UiIconIntegrationTests
    {
        private static readonly string[] TabKeys =
        {
            "F003-UI-ICON-TAB-FACTORY", "F003-UI-ICON-TAB-MARKET", "F003-UI-ICON-TAB-CONTRACTS",
            "F003-UI-ICON-TAB-ITEMS", "F003-UI-ICON-TAB-RESEARCH", "F003-UI-ICON-TAB-PRESTIGE",
        };

        private readonly List<IDisposable> owned = new List<IDisposable>();
        private string root;

        public static IEnumerable<UiIconSurface> Surfaces() => UiIconSurfaceRig.Surfaces();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        public static IEnumerable<ShellScreen> Representative()
        {
            var all = UiIconSurfaceRig.ScreenClasses().ToList();
            yield return all.First(screen => screen.Name.StartsWith("phone-portrait 360", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("phone-landscape 640", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("scene 640x480", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("tablet-portrait", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("desktop 1280", StringComparison.Ordinal));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned)
            {
                item.Dispose();
            }

            owned.Clear();
            if (root != null)
            {
                RunSceneRun.DeleteRoot(root);
                root = null;
            }
        }

        private UiIconSurfaceRig NewRig(ShellScreen screen, int percent)
        {
            var rig = UiIconSurfaceRig.Create(screen, percent);
            owned.Add(rig);
            return rig;
        }

        // ---------------------------------------------------------------------------------------------------
        // One style per surface
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EverySurfaceShowsOnlyRestyledIconsAndItsOwnKeys([ValueSource(nameof(Surfaces))] UiIconSurface surface, [ValueSource(nameof(Percents))] int percent)
        {
            var screen = UiIconSurfaceRig.ScreenClasses().First(candidate => candidate.Name.StartsWith("scene 640x480", StringComparison.Ordinal));
            var rig = NewRig(screen, percent);
            yield return rig.Rig.Layout();
            yield return rig.Show(surface);

            var hits = UiIconScan.Scan(rig.Rig.Root, includePlaceholders: true);
            var context = $"{surface} @{percent}%";
            Assert.That(hits.Where(hit => hit.Key.StartsWith("PLACEHOLDER", StringComparison.Ordinal)).Select(hit => hit.Describe()), Is.Empty,
                context + ": no Feature 003 placeholder beside a restyled icon");
            var keys = new HashSet<string>(hits.Select(hit => hit.Key));
            foreach (var expected in new[] { "F003-UI-ICON-CASH", "F003-UI-ICON-STORAGE", "F004-UI-ICON-SETTINGS" }.Concat(TabKeys))
            {
                Assert.That(keys, Does.Contain(expected), context + ": the HUD and tab bar carry " + expected);
            }

            switch (surface)
            {
                case UiIconSurface.Research:
                    Assert.That(keys.Any(key => key.StartsWith("F003-UI-ICON-RESEARCH-NODE-", StringComparison.Ordinal)), Is.True, context + ": the graph nodes carry state icons");
                    break;
                case UiIconSurface.Contracts:
                    if (rig.TimedContractAccepted && percent == 100)
                    {
                        Assert.That(keys, Does.Contain("F003-UI-ICON-TIMER"), context + ": a timed active contract shows the timer icon");
                    }

                    break;
            }
        }

        [UnityTest]
        public IEnumerator ResearchEffectLinesCarryTheEffectIconOfTheirKind()
        {
            var screen = UiIconSurfaceRig.ScreenClasses().First(candidate => candidate.Name.StartsWith("scene 640x480", StringComparison.Ordinal));
            var rig = NewRig(screen, 100);
            yield return rig.Rig.Layout();
            yield return rig.Show(UiIconSurface.Research);

            var expectedByNode = new Dictionary<string, string>();
            foreach (var button in rig.Research.NodeButtons)
            {
                var id = button.name.Substring("research-node-".Length);
                ResearchPanelTestSupport.Submit(button);
                yield return ShellRig.Settle();
                var effectKind = rig.Session.Catalog.Research[id].Effect.Kind;
                var expectedKey = ResearchEffectCopy.IconKey(effectKind);
                var effectLabel = rig.Research.DetailEffect;
                var key = UiIconSprites.KeyOf(effectLabel.resolvedStyle.backgroundImage.sprite);
                Assert.That(key, Is.EqualTo(expectedKey), id + " (" + effectKind + ") effect icon");
                if (expectedKey != null)
                {
                    expectedByNode[id] = expectedKey;
                }
            }

            Assert.That(expectedByNode.Values.Distinct().Count(), Is.GreaterThanOrEqualTo(2), "the shipped research covers at least two effect icons");
        }

        // ---------------------------------------------------------------------------------------------------
        // Contrast after tint
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryIconKeepsThreeToOneAfterTheTokenTint([ValueSource(nameof(Surfaces))] UiIconSurface surface, [ValueSource(nameof(Percents))] int percent)
        {
            var screen = UiIconSurfaceRig.ScreenClasses().First(candidate => candidate.Name.StartsWith("scene 640x480", StringComparison.Ordinal));
            var rig = NewRig(screen, percent);
            yield return rig.Rig.Layout();
            yield return rig.Show(surface);
            var hits = UiIconScan.Scan(rig.Rig.Root);
            Assert.That(hits, Is.Not.Empty);
            var rows = UiIconScan.AssertTintedContrast(hits, $"{surface}@{percent}");
            var folder = EvidenceFolder();
            File.AppendAllLines(Path.Combine(folder, "tinted-contrast-in-context.txt"), rows.Distinct());
        }

        // ---------------------------------------------------------------------------------------------------
        // The HUD gear
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheGearIsTheRestyledGlyphAtTheUnchangedTouchSizeWithItsName([ValueSource(nameof(Representative))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            var rig = NewRig(screen, percent);
            yield return rig.Rig.Layout();
            var shell = rig.Rig.Shell;
            var gear = shell.SettingsButton;
            Assert.That(shell.SettingsUsesSprite, Is.True, "the restyled sprite replaces the hand-drawn USS gear");
            Assert.That(gear.worldBound.width, Is.GreaterThanOrEqualTo(44f - Tolerance));
            Assert.That(gear.worldBound.height, Is.GreaterThanOrEqualTo(44f - Tolerance));
            Assert.That(gear.tooltip, Is.EqualTo(RunShellDocument.SettingsName));
            var glyph = shell.Root.Q<VisualElement>("settings-icon");
            Assert.That(UiIconSprites.KeyOf(glyph.resolvedStyle.backgroundImage.sprite), Is.EqualTo("F004-UI-ICON-SETTINGS"));
            Assert.That(glyph.Children().Any(child => child.resolvedStyle.display != DisplayStyle.None), Is.False, "no USS tooth or hole is drawn beside the sprite");
            Assert.That(glyph.worldBound.width, Is.EqualTo(24f).Within(1f));
            UiIconScan.AssertInBounds(new[] { new UiIconHit { Element = glyph, Key = "F004-UI-ICON-SETTINGS", IconRect = glyph.worldBound } }, shell.Root.worldBound, screen.Name);
        }

        // ---------------------------------------------------------------------------------------------------
        // The Offline and Start-up surfaces
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheOfflineSummaryHeadlineCarriesTheOfflineIcon([ValueSource(nameof(Percents))] int percent)
        {
            var screen = UiIconSurfaceRig.ScreenClasses().First(candidate => candidate.Name.StartsWith("phone-portrait 390", StringComparison.Ordinal));
            var catalog = RunShellFixtures.ShippedCatalog;
            var standalone = new StandaloneRig(screen);
            owned.Add(standalone);
            var panel = new OfflineSummaryPanel(catalog, null, Styles("OfflineSummaryPanel.uss"), UiIconSprites.Load);
            yield return standalone.Mount(panel, percent);
            panel.ShowSummary(NormalReturn());
            yield return Frames();
            var headline = panel.Line("offline-headline");
            Assert.That(UiIconSprites.KeyOf(headline.resolvedStyle.backgroundImage.sprite), Is.EqualTo(PanelIcons.Offline));
            var hits = UiIconScan.Scan(standalone.Root);
            Assert.That(hits.Select(hit => hit.Key), Does.Contain(PanelIcons.Offline));
            UiIconScan.AssertInBounds(hits, standalone.Root.worldBound, $"offline @{percent}%");
            UiIconScan.AssertTintedContrast(hits, $"offline@{percent}");
            panel.Unmount();
        }

        [UnityTest]
        public IEnumerator TheStartupSpinnerIsTheRestyledSpriteAndStopsUnderReducedMotion([ValueSource(nameof(Percents))] int percent)
        {
            var clock = new StatusTestClock();
            root = TempRoot();
            var run = StatusPersistenceRig.Create(root, clock);
            var screen = UiIconSurfaceRig.ScreenClasses().First(candidate => candidate.Name.StartsWith("phone-portrait 390", StringComparison.Ordinal));
            var standalone = new StandaloneRig(screen);
            owned.Add(standalone);
            var reduced = false;
            var panel = new StartupStatusPanel(run.Persistence, run.Legacy, Styles("StartupStatusPanel.uss"), UiIconSprites.Load, () => reduced);
            yield return standalone.Mount(panel, percent);

            panel.ShowImporting();
            yield return Frames();
            Assert.That(panel.SpinnerUsesSprite, Is.True, "the spinner is the restyled static frame");
            Assert.That(UiIconSprites.KeyOf(panel.Spinner.resolvedStyle.backgroundImage.sprite), Is.EqualTo(StartupStatusPanel.SpinnerIconKey));
            Assert.That(UiIconSprites.KeyOf(panel.ImportIcon.resolvedStyle.backgroundImage.sprite), Is.EqualTo(StartupStatusPanel.ImportIconKey));
            var hits = UiIconScan.Scan(standalone.Root);
            Assert.That(hits.Select(hit => hit.Key), Is.SupersetOf(new[] { StartupStatusPanel.SpinnerIconKey, StartupStatusPanel.ImportIconKey }));
            UiIconScan.AssertInBounds(hits, standalone.Root.worldBound, $"startup @{percent}%");
            UiIconScan.AssertTintedContrast(hits, $"startup@{percent}");
            Assert.That(panel.Spinner.layout.width, Is.EqualTo(24f).Within(1f));
            Assert.That(panel.Spinner.resolvedStyle.borderTopWidth, Is.EqualTo(0f), "the F003 ring is gone");

            yield return new WaitForSeconds(0.4f);
            Assert.That(panel.IsSpinning, Is.True, "the spinner turns while motion is allowed");

            reduced = true;
            yield return new WaitForSeconds(0.4f);
            Assert.That(panel.IsSpinning, Is.False, "reduced motion stops the spinner");
            Assert.That(panel.Spinner.style.rotate.value.angle.value, Is.EqualTo(0f), "and leaves the static first frame");
            Assert.That(IsDisplayed(panel.Spinner), Is.True, "the spinner is still shown, only still");
            yield return new WaitForSeconds(0.3f);
            Assert.That(panel.Spinner.style.rotate.value.angle.value, Is.EqualTo(0f), "it stays still");

            // A re-show under reduced motion never starts it.
            panel.ShowImporting();
            yield return Frames();
            Assert.That(panel.IsSpinning, Is.False);

            reduced = false;
            panel.ApplyReducedMotion();
            yield return new WaitForSeconds(0.4f);
            Assert.That(panel.IsSpinning, Is.True, "motion allowed again resumes it");
            Assert.That(panel.Spinner.style.rotate.value.angle.value, Is.Not.EqualTo(0f));
            panel.Unmount();
        }

        // ---------------------------------------------------------------------------------------------------
        // 2 px strokes at 420 dpi
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ATwoArtPixelStrokeRendersAtTwoDevicePixelsOrMoreOnA420DpiPhone()
        {
            const int Width = 1080;
            const int Height = 2400;
            var profile = IdleFactory.Unity.Presentation.ShellDeviceProfile.Touch(Width, Height, 420f);
            var screen = new ShellScreen("portrait 1080x2400 @420dpi", Width, Height, 0, 0, 0, 0, IdleFactory.Unity.Presentation.FactoryHudLayout.CompactPortrait);
            var rig = UiIconSurfaceRig.Create(screen, 100, authored: true, profile: profile);
            owned.Add(rig);
            yield return rig.Rig.Layout();
            yield return rig.Show(UiIconSurface.Hud);
            yield return TextScaleLayoutTests.Capture(rig.Rig, "stroke-420dpi-hud", "t441");

            var ppp = rig.Rig.PixelsPerPoint;
            var shell = rig.Rig.Shell;
            var icon = shell.Root.Q<VisualElement>("cash-icon");
            var devicePerArtPx = icon.worldBound.width * ppp / 24f;
            Assert.That(devicePerArtPx, Is.GreaterThanOrEqualTo(1f), "an art pixel is at least one device pixel");
            Assert.That(2f * devicePerArtPx, Is.GreaterThanOrEqualTo(2f), "a 2 art px stroke is at least 2 device px");

            // Measured on the rendered pixels: the widest dark-to-light run of the coin's ring along its centre row.
            var texture = rig.Rig.Texture;
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            try
            {
                var box = icon.worldBound;
                var left = Mathf.RoundToInt(box.xMin * ppp);
                var right = Mathf.RoundToInt(box.xMax * ppp);
                var row = texture.height - 1 - Mathf.RoundToInt(box.center.y * ppp);
                var shortest = int.MaxValue;
                var runs = new List<int>();
                var run = 0;
                for (var x = left; x < right; x++)
                {
                    var pixel = image.GetPixel(x, row);
                    var lit = pixel.grayscale > 0.45f;
                    if (lit)
                    {
                        run++;
                    }
                    else if (run > 0)
                    {
                        runs.Add(run);
                        run = 0;
                    }
                }

                if (run > 0)
                {
                    runs.Add(run);
                }

                Assert.That(runs, Is.Not.Empty, "the coin is drawn");
                shortest = runs.Min();
                File.WriteAllText(Path.Combine(EvidenceFolder(), "stroke-420dpi.txt"),
                    $"pixelsPerPoint={ppp:0.000} iconPt={box.width:0.0} devicePxPerArtPx={devicePerArtPx:0.000} expectedStrokePx={2f * devicePerArtPx:0.00} runsOnCentreRow={string.Join(",", runs)} shortestRun={shortest}\n");
                Assert.That(shortest, Is.GreaterThanOrEqualTo(2), "every drawn stroke on the coin's centre row is at least 2 device px wide: " + string.Join(",", runs));
            }
            finally
            {
                UnityEngine.Object.Destroy(image);
            }
        }

        public static string EvidenceFolder()
        {
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "test-results", "t441"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
#endif
