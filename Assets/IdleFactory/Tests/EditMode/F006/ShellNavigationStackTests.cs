using System.Collections.Generic;
using IdleFactory.Unity.UI;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T511 (Feature 006, research R11): the extracted back stack and sheet host in plain C#. Every assertion states what
    /// <c>RunShellDocument.HandleBack</c> did before the move: dialog, then pinned value, then settings, then sheet, one frame per
    /// press, a press consumed at most once per frame.
    /// </summary>
    public sealed class ShellNavigationStackTests
    {
        private sealed class FakeFrames : IShellBackFrames
        {
            public bool HasDialog { get; set; }
            public bool HasPinnedValue { get; set; }
            public bool IsSettingsOpen { get; set; }
            public bool HasClosableSheet { get; set; }
            public readonly List<string> Popped = new List<string>();

            public void PopDialog() { Popped.Add("dialog"); HasDialog = false; }
            public void PopPinnedValue() { Popped.Add("pinned"); HasPinnedValue = false; }
            public void PopSettings() { Popped.Add("settings"); IsSettingsOpen = false; }
            public void PopSheet() { Popped.Add("sheet"); HasClosableSheet = false; }
        }

        [Test]
        public void EmptyStack_IsNotConsumedAndPopsNothing()
        {
            var frames = new FakeFrames();
            Assert.That(new ShellNavigationStack().HandleBack(frames, 1, true), Is.False);
            Assert.That(frames.Popped, Is.Empty);
        }

        [Test]
        public void FrameOrder_IsDialogPinnedSettingsSheet_OnePerPress()
        {
            var frames = new FakeFrames { HasDialog = true, HasPinnedValue = true, IsSettingsOpen = true, HasClosableSheet = true };
            var stack = new ShellNavigationStack();
            for (var frame = 1; frame <= 4; frame++)
            {
                Assert.That(stack.HandleBack(frames, frame, true), Is.True);
            }

            Assert.That(frames.Popped, Is.EqualTo(new[] { "dialog", "pinned", "settings", "sheet" }));
            Assert.That(stack.HandleBack(frames, 5, true), Is.False);
        }

        [Test]
        public void SettingsWinsOverSheet_AndPinnedWinsOverSettings()
        {
            var frames = new FakeFrames { IsSettingsOpen = true, HasClosableSheet = true };
            new ShellNavigationStack().HandleBack(frames, 1, true);
            Assert.That(frames.Popped, Is.EqualTo(new[] { "settings" }));

            frames = new FakeFrames { HasPinnedValue = true, IsSettingsOpen = true };
            new ShellNavigationStack().HandleBack(frames, 1, true);
            Assert.That(frames.Popped, Is.EqualTo(new[] { "pinned" }));
        }

        [Test]
        public void SecondPressInTheSameFrame_IsConsumedWithoutPoppingAgain()
        {
            var frames = new FakeFrames { HasDialog = true, HasPinnedValue = true };
            var stack = new ShellNavigationStack();
            Assert.That(stack.HandleBack(frames, 7, true), Is.True);
            Assert.That(stack.HandleBack(frames, 7, true), Is.True);
            Assert.That(frames.Popped, Is.EqualTo(new[] { "dialog" }));
            Assert.That(stack.HandleBack(frames, 8, true), Is.True);
            Assert.That(frames.Popped, Is.EqualTo(new[] { "dialog", "pinned" }));
        }

        [Test]
        public void FrameGuardOff_PopsOnEveryPress()
        {
            var frames = new FakeFrames { HasDialog = true, HasPinnedValue = true };
            var stack = new ShellNavigationStack();
            stack.HandleBack(frames, 7, false);
            stack.HandleBack(frames, 7, false);
            Assert.That(frames.Popped, Is.EqualTo(new[] { "dialog", "pinned" }));
        }

        [Test]
        public void UnconsumedPress_DoesNotArmTheFrameGuard()
        {
            var frames = new FakeFrames();
            var stack = new ShellNavigationStack();
            Assert.That(stack.HandleBack(frames, 3, true), Is.False);
            frames.HasDialog = true;
            Assert.That(stack.HandleBack(frames, 3, true), Is.True);
            Assert.That(frames.Popped, Is.EqualTo(new[] { "dialog" }));
        }

        [Test]
        public void SheetHost_BackClosesOnlyASlotSheetOrANonFactoryTab()
        {
            var host = new ShellSheetHost();
            Assert.That(host.IsBackClosable(true, true), Is.False, "a closed sheet is not closable");
            host.IsOpen = true;
            Assert.That(host.IsBackClosable(false, false), Is.False, "the always-present side panel is left alone");
            Assert.That(host.IsBackClosable(true, false), Is.True);
            Assert.That(host.IsBackClosable(false, true), Is.True);
        }

        [Test]
        public void SheetHost_ResetClearsOpenAndSettings()
        {
            var host = new ShellSheetHost { IsOpen = true, IsSettingsOpen = true };
            host.Reset();
            Assert.That(host.IsOpen, Is.False);
            Assert.That(host.IsSettingsOpen, Is.False);
        }
    }
}
