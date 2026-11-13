using System.Collections.Generic;
using IdleFactory.Unity.UI;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T534 (contracts/ui-surfaces-f006.md section 4): the machine card is the frame after the dialog, the pinned value, Settings and the sheet (Details).
    /// A frames implementer that knows no card (the T511 fake) still compiles and never pops one.
    /// </summary>
    public sealed class CardBackFrameTests
    {
        private sealed class Frames : IShellBackFrames
        {
            public bool HasDialog { get; set; }
            public bool HasPinnedValue { get; set; }
            public bool IsSettingsOpen { get; set; }
            public bool HasClosableSheet { get; set; }
            public bool HasCard { get; set; }
            public readonly List<string> Popped = new List<string>();

            public void PopDialog() { Popped.Add("dialog"); HasDialog = false; }
            public void PopPinnedValue() { Popped.Add("pinned"); HasPinnedValue = false; }
            public void PopSettings() { Popped.Add("settings"); IsSettingsOpen = false; }
            public void PopSheet() { Popped.Add("sheet"); HasClosableSheet = false; }
            public void PopCard() { Popped.Add("card"); HasCard = false; }
        }

        private sealed class OldFrames : IShellBackFrames
        {
            public bool HasDialog => false;
            public bool HasPinnedValue => false;
            public bool IsSettingsOpen => false;
            public bool HasClosableSheet => false;
            public void PopDialog() { }
            public void PopPinnedValue() { }
            public void PopSettings() { }
            public void PopSheet() { }
        }

        [Test]
        public void TheCardPopsAfterTheDialogThePinnedValueSettingsAndTheDetailsSheet()
        {
            var frames = new Frames { HasDialog = true, HasPinnedValue = true, IsSettingsOpen = true, HasClosableSheet = true, HasCard = true };
            var stack = new ShellNavigationStack();
            for (var frame = 1; frame <= 5; frame++)
            {
                Assert.That(stack.HandleBack(frames, frame, true), Is.True);
            }

            Assert.That(frames.Popped, Is.EqualTo(new[] { "dialog", "pinned", "settings", "sheet", "card" }));
            Assert.That(stack.HandleBack(frames, 6, true), Is.False, "nothing is left");
        }

        [Test]
        public void ACardAloneIsOneFramePerPressAndAPressIsConsumedOncePerFrame()
        {
            var frames = new Frames { HasCard = true };
            var stack = new ShellNavigationStack();
            Assert.That(stack.HandleBack(frames, 1, true), Is.True);
            Assert.That(stack.HandleBack(frames, 1, true), Is.True, "the same frame: consumed without acting again");
            Assert.That(frames.Popped, Is.EqualTo(new[] { "card" }));
        }

        [Test]
        public void AnImplementerWithoutCardMembersNeverPopsACard()
        {
            Assert.That(new ShellNavigationStack().HandleBack(new OldFrames(), 1, true), Is.False);
        }
    }
}
