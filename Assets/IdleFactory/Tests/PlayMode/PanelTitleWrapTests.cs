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
    /// T458 (N-1, polish-review): at 200 % text on the side-panel classes the sheet title breaks only between words. The Overdrive
    /// control shares the side panel's header and must not squeeze the title below the width of its longest word.
    /// </summary>
    public sealed class PanelTitleWrapTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.SidePanels();

        public static IEnumerable<int> Percents() => new[] { 100, 150, 200 };

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

        /// <summary>Fails when any word of the title is wider than the title's content box, which means the text must break inside that word.</summary>
        public static void AssertNoMidWordBreak(Label title, string context)
        {
            Assert.That(title.text, Is.Not.Empty, context + ": a title is shown");
            var width = title.contentRect.width;
            foreach (var word in title.text.Split(' '))
            {
                if (word.Length == 0)
                {
                    continue;
                }

                var wordWidth = title.MeasureTextSize(word, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
                Assert.That(wordWidth, Is.LessThanOrEqualTo(width + RunShellLayoutAssertions.Tolerance),
                    $"{context}: the title \"{title.text}\" breaks inside \"{word}\" (word {wordWidth:F1} > title width {width:F1})");
            }
        }

        [UnityTest]
        public IEnumerator TheSheetTitleBreaksOnlyBetweenWords([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            var shell = rig.Shell;
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(OverdriveStage.CoolingDown));
            var title = shell.Root.Q<Label>("panel-title");
            foreach (var tab in new[] { ShellTab.Contracts, ShellTab.Items, ShellTab.Market, ShellTab.Prestige })
            {
                shell.SelectTab(tab);
                yield return ShellRig.Settle();
                AssertNoMidWordBreak(title, $"{screen} @{percent}% tab {tab}");
            }

            Assert.That(shell.OpenSlot(1, SlotCondition.Occupied), Is.True);
            yield return ShellRig.Settle();
            AssertNoMidWordBreak(title, $"{screen} @{percent}% machine sheet");
            var header = shell.Root.Q<VisualElement>("panel-header");
            RunShellLayoutAssertions.AssertWithin(header.worldBound, shell.OverdriveButton.worldBound, $"{screen} @{percent}%: Overdrive stays in the header");
        }
    }
}
#endif
