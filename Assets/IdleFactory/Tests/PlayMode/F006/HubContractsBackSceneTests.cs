#if UNITY_EDITOR
using System.Collections;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T561 (C-85): system Back closes the Contracts sheet opened from the hub, in the shipped scene with the real panels (the locked state of a fresh run, where
    /// the T542 device runs saw Back do nothing). The existing shell back rules apply: one Back from a system view returns to the factory (HubPanelTests).
    /// </summary>
    public sealed class HubContractsBackSceneTests : InputTestFixture
    {
        private string root;
        private RunSceneClock clock;
        private RunSceneRun run;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            root = RunSceneRun.NewRoot();
            clock = new RunSceneClock();
            run = null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (run != null)
            {
                yield return run.Stop();
            }

            RunSceneRun.DeleteRoot(root);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator BackOnTheLockedContractsSheetOpenedFromTheHubClosesItAndOneMoreBackIsLeftForTheApp()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            yield return null;
            yield return null;
            var shell = run.Bootstrap.RunShell;
            Assert.That(shell.IsReady, Is.True, shell.Error);

            shell.OpenHub();
            yield return null;
            yield return null;
            Assert.That(shell.IsHubOpen, Is.True);
            var card = shell.Root.Q<Button>("hub-card-contracts");
            Assert.That(card, Is.Not.Null, "the hub lists Contracts");
            CardHarness.Press(card);
            yield return null;
            yield return null;
            Assert.That(shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Contracts), "the Contracts sheet is open");

            var consumed = shell.HandleBack();
            yield return null;
            Assert.That(consumed, Is.True, "Back is consumed by the open Contracts sheet");
            Assert.That(shell.IsSheetOpen, Is.False, "one Back from the Contracts sheet returns to the factory");
            Assert.That(shell.ActivePanelId, Is.Null);
        }

        // C-85 root cause (measured on the emulator, 2026-11-25): the first Android Back after a launch was recorded on a Keyboard device that was not yet Keyboard.current
        // (the AVD exposes 16 keyboard devices), so polling only the current keyboard never saw it. Back is now read from every Keyboard device.
        [UnityTest]
        public IEnumerator BackOnAKeyboardThatIsNotTheCurrentOneStillClosesTheContractsSheet()
        {
            var current = InputSystem.AddDevice<Keyboard>();
            var other = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(current, new KeyboardState());
            InputSystem.Update();

            run = new RunSceneRun(root, clock);
            yield return run.Start();
            yield return null;
            yield return null;
            var shell = run.Bootstrap.RunShell;
            Assert.That(shell.IsReady, Is.True, shell.Error);
            shell.OpenHub();
            yield return null;
            var card = shell.Root.Q<Button>("hub-card-contracts");
            CardHarness.Press(card);
            yield return null;
            yield return null;
            Assert.That(shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Contracts));

            Press(other.escapeKey);
            yield return null;
            yield return null;
            Assert.That(shell.IsSheetOpen, Is.False, "Back on a keyboard that is not Keyboard.current closed the Contracts sheet");
            Release(other.escapeKey);
        }
    }
}
#endif
