using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

#if UNITY_EDITOR
namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T459 (N-6, polish-review): on every phone class at 100 % and 200 % text the sheet title breaks only between words, also at
    /// phone landscape 640x360 where the Close control shares the one-row phone header. Close keeps its touch size and a name.
    /// </summary>
    public sealed class TextScaleLayoutTests
    {
        private const float MinTouch = 44f;
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.Phones();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
        }

        [UnityTest]
        public IEnumerator ThePhoneSheetTitleBreaksOnlyBetweenWordsAndCloseKeepsItsTouchSize([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            var shell = rig.Shell;
            var title = shell.Root.Q<Label>("panel-title");
            foreach (var tab in new[] { ShellTab.Contracts, ShellTab.Items, ShellTab.Market, ShellTab.Prestige })
            {
                shell.SelectTab(tab);
                yield return ShellRig.Settle();
                PanelTitleWrapTests.AssertNoMidWordBreak(title, $"{screen} @{percent}% tab {tab}");
                if (percent == 200 && tab == ShellTab.Contracts && screen.Width == 640 && screen.Height == 360)
                {
                    yield return Capture(rig, "contracts-640x360-200");
                }

                AssertCloseUsable(shell, $"{screen} @{percent}% tab {tab}");
            }

            Assert.That(shell.OpenSlot(1, SlotCondition.Occupied), Is.True);
            yield return ShellRig.Settle();
            PanelTitleWrapTests.AssertNoMidWordBreak(title, $"{screen} @{percent}% machine sheet");
            AssertCloseUsable(shell, $"{screen} @{percent}% machine sheet");
        }

        public static IEnumerable<ShellScreen> AllClasses() => UiIconSurfaceRig.ScreenClasses();

        /// <summary>
        /// T441 (FR-014): on the shell and on each panel, at every screen class and at 100 and 200 % text, every restyled icon stays inside its own
        /// element, its parent and the panel, keeps its 24 pt size, and no Feature 003 placeholder shows beside it.
        /// </summary>
        [UnityTest]
        public IEnumerator IconsStayInBoundsOnTheShellAndOnEachPanel([ValueSource(nameof(AllClasses))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            var surfaces = UiIconSurfaceRig.Create(screen, percent);
            rigs.Add(surfaces.Rig);
            yield return surfaces.Rig.Layout();
            foreach (var surface in UiIconSurfaceRig.Surfaces())
            {
                yield return surfaces.Show(surface);
                var context = $"{screen.Name} @{percent}% {surface}";
                var hits = UiIconScan.Scan(surfaces.Rig.Root, includePlaceholders: true);
                Assert.That(hits, Is.Not.Empty, context + ": the surface shows icons");
                Assert.That(hits.Where(hit => hit.Key.StartsWith("PLACEHOLDER", System.StringComparison.Ordinal)).Select(hit => hit.Describe()), Is.Empty, context);
                UiIconScan.AssertInBounds(hits, surfaces.Rig.Root.worldBound, context);
                foreach (var hit in hits)
                {
                    Assert.That(hit.IconRect.width, Is.EqualTo(hit.IconRect.height).Within(1f), context + ": " + hit.Describe() + " stays square");
                }

                AssertShellLayout(surfaces.Rig, context);
            }
        }

        /// <summary>Saves the rig's panel texture as test-results/t459/&lt;name&gt;.png (evidence only, not committed). Clears the texture first (workstation pitfall).</summary>
        public static IEnumerator Capture(ShellRig target, string name, string folderName = "t459")
        {
            target.Document.panelSettings.clearColor = true;
            target.Document.panelSettings.colorClearValue = new UnityEngine.Color(0.05f, 0.06f, 0.08f, 1f);
            yield return ShellRig.Settle();
            yield return ShellRig.Settle();
            var texture = target.Texture;
            var previous = UnityEngine.RenderTexture.active;
            UnityEngine.RenderTexture.active = texture;
            var image = new UnityEngine.Texture2D(texture.width, texture.height, UnityEngine.TextureFormat.RGBA32, false);
            image.ReadPixels(new UnityEngine.Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            UnityEngine.RenderTexture.active = previous;
            var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "test-results", folderName));
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), UnityEngine.ImageConversion.EncodeToPNG(image));
            UnityEngine.Object.Destroy(image);
        }

        private static void AssertCloseUsable(RunShellDocument shell, string context)
        {
            var close = shell.CloseButton;
            Assert.That(close.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None), context + ": Close is shown on a phone");
            Assert.That(close.worldBound.width, Is.GreaterThanOrEqualTo(MinTouch - RunShellLayoutAssertions.Tolerance), context + ": Close is at least 44 pt wide");
            Assert.That(close.worldBound.height, Is.GreaterThanOrEqualTo(MinTouch - RunShellLayoutAssertions.Tolerance), context + ": Close is at least 44 pt tall");
            Assert.That(close.tooltip, Is.EqualTo("Close"), context + ": Close has an accessible name even when its label is hidden");
        }
    }
}
#endif
