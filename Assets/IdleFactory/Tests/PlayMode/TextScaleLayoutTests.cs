using System.Collections;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

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
                AssertCloseUsable(shell, $"{screen} @{percent}% tab {tab}");
            }

            Assert.That(shell.OpenSlot(1, SlotCondition.Occupied), Is.True);
            yield return ShellRig.Settle();
            PanelTitleWrapTests.AssertNoMidWordBreak(title, $"{screen} @{percent}% machine sheet");
            AssertCloseUsable(shell, $"{screen} @{percent}% machine sheet");
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
