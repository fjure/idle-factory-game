#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T359 (US10, FR-040 to FR-044, F5-R20 VEIN-1 to VEIN-12, QA-12 "Ore vein"): the ore vein overlay. Click, tap, Space and Enter
    /// and the hold of each at the capped rate on the injected run timeline (the first press counts at once, one activation per
    /// 1/cap s, at most one per frame); the hold ends on release, leaving the vein, a second pointer, a rotation, focus loss, a
    /// dialog, a covering sheet or Tab and grants nothing after it; OS key repeat adds nothing; a KeyDown and its NavigationSubmit
    /// count once; a 600 ms touch hold opens no peek; the exact accessible name; the pulse, the "max rate" cue, the progress in
    /// tenths and "Store full" that clears without moving the layout; a soft-disabled vein under a dialog; hidden and out of the
    /// focus order under a phone sheet; and the layout at every screen class, in both rotations, at 100 % and 200 % text.
    /// Input tests drive a fake source with an injected timeline; the cap itself is proved against the real <c>Mine</c> command.
    /// </summary>
    public sealed class OreVeinPanelTests
    {
        private const string Ore = "iron.ore";
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunShellDocument shell;
        private OreVeinControl vein;
        private RunSession session;
        private RunCommands commands;

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
            shell = null;
            vein = null;
            session = null;
            commands = null;
        }

        private static IEnumerable<ShellScreen> Screens() =>
            ShellScreens.All().SelectMany(screen => new[] { screen, ShellScreens.Rotated(screen) });

        private static IEnumerable<ShellScreen> SidePanelScreens() => ShellScreens.SidePanels();

        // ---------------------------------------------------------------------------------------------------
        // Rig, sources and input
        // ---------------------------------------------------------------------------------------------------

        /// <summary>A source with an injected timeline that counts calls and simulates the milli-yield remainder, without a rate cap.</summary>
        private sealed class FakeSource : IOreVeinSource
        {
            public bool IsAvailable { get; set; } = true;
            public string ResourceLabel { get; set; } = "Iron Ore";
            public long ActivationsPerSecond { get; set; } = 10;
            public long YieldMilli { get; set; } = 100;
            public long TimelineMs { get; set; } = 5_000_000;
            public object RunToken { get; set; } = new object();
            public bool HasRoom { get; set; } = true;
            public bool IsLifecycleBlocked { get; set; }

            /// <summary>When set, <see cref="Mine"/> rejects with this code and changes nothing.</summary>
            public string Reject { get; set; }

            /// <summary>When true, every activation is over the cap: ignored, success with nothing counted.</summary>
            public bool AtCap { get; set; }

            public int Calls { get; private set; }
            public long Counted { get; private set; }
            public long Remainder { get; private set; }
            public long OreStock { get; private set; }
            public long RemainderMilli => Remainder;

            /// <summary>A new run starts with no remainder.</summary>
            public void ResetRun()
            {
                Remainder = 0;
                RunToken = new object();
            }

            public OreVeinMineOutcome Mine(long activations)
            {
                Calls++;
                if (Reject != null)
                {
                    return new OreVeinMineOutcome(false, Reject, 0, 0, 0, 0, OreStock);
                }

                if (AtCap)
                {
                    return new OreVeinMineOutcome(true, null, 0, activations, 0, 0, OreStock);
                }

                Counted += activations;
                var total = Remainder + activations * YieldMilli;
                var units = total / 1000;
                Remainder = total % 1000;
                OreStock += units;
                return new OreVeinMineOutcome(true, null, activations, 0, 0, units, OreStock);
            }
        }

        private IEnumerator OpenWith(IOreVeinSource source, ShellScreen screen = null, int percent = 100)
        {
            rig = ShellRig.Create(screen ?? ShellScreens.All().First(), null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            shell = rig.Shell;
            vein = shell.OreVein;
            Assert.That(vein, Is.Not.Null, "the shell builds the ore vein from the UXML");
            shell.BindOreVein(source);
            yield return ShellRig.Settle();
        }

        private IEnumerator OpenReal(ShellScreen screen = null, int percent = 100, long cash = 25)
        {
            session = RunShellFixtures.NewSession(out commands, 1, cash);
            yield return OpenWith(new RunOreVeinSource(session, commands), screen, percent);
        }

        private static void SetProperty(EventBase evt, string name, object value)
        {
            for (var type = evt.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                var setter = property?.GetSetMethod(true);
                if (setter != null)
                {
                    setter.Invoke(evt, new[] { value });
                    return;
                }
            }

            Assert.Fail("The event has no settable " + name + ".");
        }

        private static void Send(VisualElement target, EventBase evt)
        {
            using (evt)
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static void Pointer<T>(VisualElement target, EventType type, Vector2 position, int id, string pointerType)
            where T : PointerEventBase<T>, new()
        {
            var systemEvent = new Event { type = type, mousePosition = position, button = 0 };
            var evt = PointerEventBase<T>.GetPooled(systemEvent);
            SetProperty(evt, "pointerId", id);
            SetProperty(evt, "pointerType", pointerType ?? UnityEngine.UIElements.PointerType.mouse);
            Send(target, evt);
        }

        private Vector2 Center => vein.Root.worldBound.center;

        private void Press(int id = 0, string pointerType = null) =>
            Pointer<PointerDownEvent>(vein.Root, EventType.MouseDown, Center, id, pointerType);

        private void Release(int id = 0, string pointerType = null) =>
            Pointer<PointerUpEvent>(vein.Root, EventType.MouseUp, Center, id, pointerType);

        private void MoveTo(Vector2 position, int id = 0, string pointerType = null) =>
            Pointer<PointerMoveEvent>(vein.Root, EventType.MouseMove, position, id, pointerType);

        private void KeyDown(KeyCode key) => Send(vein.Root, KeyDownEvent.GetPooled('\0', key, EventModifiers.None));

        private void KeyUp(KeyCode key) => Send(vein.Root, KeyUpEvent.GetPooled('\0', key, EventModifiers.None));

        private void Submit() => Send(vein.Root, NavigationSubmitEvent.GetPooled());

        /// <summary>Moves the injected timeline and ticks the control once, as one frame would.</summary>
        private void Step(FakeSource source, long milliseconds)
        {
            source.TimelineMs += milliseconds;
            vein.Tick();
        }

        // ---------------------------------------------------------------------------------------------------
        // Name, activation and hold accrual
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheAccessibleNameIsExactAndNoClickChangesIt()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            Assert.That(vein.AccessibleName, Is.EqualTo("Mine Iron Ore, +0.1 per click"));
            Assert.That(vein.Root.tooltip, Is.EqualTo(vein.AccessibleName));
            var changes = vein.AccessibleNameChanges;
            for (var i = 0; i < 5; i++)
            {
                Press();
                Release();
            }

            Assert.That(fake.Calls, Is.EqualTo(5));
            Assert.That(vein.ProgressTenths, Is.EqualTo(5), "the progress shows the remainder in tenths");
            Assert.That(vein.AccessibleName, Is.EqualTo("Mine Iron Ore, +0.1 per click"));
            Assert.That(vein.AccessibleNameChanges, Is.EqualTo(changes), "nothing is announced per click");
            for (var i = 0; i < 5; i++)
            {
                Press();
                Release();
            }

            Assert.That(fake.OreStock, Is.EqualTo(1));
            Assert.That(vein.ProgressTenths, Is.Zero, "ten tenths make one Ore and the progress starts over");
            Assert.That(vein.AccessibleNameChanges, Is.EqualTo(changes));
        }

        [UnityTest]
        public IEnumerator TheNameFollowsAnUpgradeOfTheYield()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            fake.YieldMilli = 150;
            vein.Tick();
            Assert.That(vein.AccessibleName, Is.EqualTo("Mine Iron Ore, +0.15 per click"), "the label shows the current yield after an upgrade");
            fake.YieldMilli = 2000;
            vein.Tick();
            Assert.That(vein.AccessibleName, Is.EqualTo("Mine Iron Ore, +2 per click"));
        }

        [UnityTest]
        public IEnumerator AClickATapAndSpaceAndEnterEachMineOnce()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            Press();
            Release();
            Assert.That(fake.Calls, Is.EqualTo(1), "a pointer click");
            Press(1, UnityEngine.UIElements.PointerType.touch);
            Release(1, UnityEngine.UIElements.PointerType.touch);
            Assert.That(fake.Calls, Is.EqualTo(2), "a touch tap");
            yield return null;
            KeyDown(KeyCode.Space);
            KeyUp(KeyCode.Space);
            Assert.That(fake.Calls, Is.EqualTo(3), "Space");
            yield return null;
            KeyDown(KeyCode.Return);
            KeyUp(KeyCode.Return);
            Assert.That(fake.Calls, Is.EqualTo(4), "Enter");
            yield return null;
            Submit();
            Assert.That(fake.Calls, Is.EqualTo(5), "a navigation submit on its own (a gamepad) is one activation");
            Assert.That(vein.IsHolding, Is.False);
        }

        [UnityTest]
        public IEnumerator APressThatStartsElsewhereNeverMinesAndThePointerIsCapturedByAPressOnTheVein()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            Pointer<PointerEnterEvent>(vein.Root, EventType.MouseMove, Center, 0, UnityEngine.UIElements.PointerType.mouse);
            MoveTo(Center);
            Release();
            Assert.That(fake.Calls, Is.Zero, "entering or releasing over the vein without a press on it mines nothing");
            Press();
            Assert.That(vein.Root.HasPointerCapture(0), Is.True, "the press captures the pointer, so pan and slot taps ignore it");
            Release();
            Assert.That(vein.Root.HasPointerCapture(0), Is.False);
        }

        [UnityTest]
        public IEnumerator AHoldCountsTheFirstPressAtOnceThenOneActivationPerCapIntervalWithAtMostOneCatchUpPerFrame()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            fake.TimelineMs = 10_000;
            Press();
            Assert.That(fake.Calls, Is.EqualTo(1), "the first press counts immediately");
            Step(fake, 50);
            Assert.That(fake.Calls, Is.EqualTo(1), "50 ms is before the first 100 ms interval");
            Step(fake, 50);
            Assert.That(fake.Calls, Is.EqualTo(2), "100 ms: one activation");
            Step(fake, 100);
            Assert.That(fake.Calls, Is.EqualTo(3));
            Step(fake, 1000);
            Assert.That(fake.Calls, Is.EqualTo(4), "a long frame yields one activation");
            vein.Tick();
            Assert.That(fake.Calls, Is.EqualTo(5), "and one catch-up on the next frame");
            vein.Tick();
            Assert.That(fake.Calls, Is.EqualTo(5), "the rest of the backlog is dropped, never a burst");
            Release();
            Step(fake, 5000);
            Assert.That(fake.Calls, Is.EqualTo(5), "nothing accrues after the release");
        }

        [UnityTest]
        public IEnumerator AHoldFollowsTheContentCapWhateverTheFrameRate()
        {
            var fake = new FakeSource { ActivationsPerSecond = 3 };
            yield return OpenWith(fake);
            fake.TimelineMs = 20_000;
            Press();
            for (var frame = 0; frame < 100; frame++)
            {
                Step(fake, 10);
            }

            Release();
            // 1,000 ms of hold at 3 per second: the press plus the activations due at 333, 666 and 1,000 ms.
            Assert.That(fake.Calls, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator KeyboardRepeatAddsNothingAndTheHoldRunsOnTheTimeline()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            vein.Root.Focus();
            fake.TimelineMs = 30_000;
            KeyDown(KeyCode.Space);
            for (var repeat = 0; repeat < 50; repeat++)
            {
                KeyDown(KeyCode.Space);
                KeyDown(KeyCode.Return);
            }

            Assert.That(fake.Calls, Is.EqualTo(1), "OS key repeat and a second key add nothing");
            for (var step = 0; step < 10; step++)
            {
                Step(fake, 100);
            }

            Assert.That(fake.Calls, Is.EqualTo(11), "one second of a held key is the press plus the cap");
            KeyUp(KeyCode.Return);
            Assert.That(vein.IsHolding, Is.True, "the release of another key does not end the hold");
            KeyUp(KeyCode.Space);
            Assert.That(vein.IsHolding, Is.False);
            Step(fake, 5000);
            Assert.That(fake.Calls, Is.EqualTo(11));
        }

        [UnityTest]
        public IEnumerator AKeyDownAndItsNavigationSubmitCountOnce()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            KeyDown(KeyCode.Space);
            Submit();
            Assert.That(fake.Calls, Is.EqualTo(1), "the same press arrives as a key event and a navigation event");
            Submit();
            yield return null;
            Submit();
            Assert.That(fake.Calls, Is.EqualTo(1), "while the key is held, a submit is the same press");
            KeyUp(KeyCode.Space);
            yield return null;
            Submit();
            Assert.That(fake.Calls, Is.EqualTo(2));
        }

        // ---------------------------------------------------------------------------------------------------
        // The real command owns the cap
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheRateCapLivesInTheCommandAndExcessClicksShowTheMaxRateCue()
        {
            yield return OpenReal();
            var state = session.State;
            for (var i = 0; i < 15; i++)
            {
                Press();
                Release();
            }

            Assert.That(state.MiningCountedInSecond, Is.EqualTo(10), "the command counted the cap and ignored the rest");
            Assert.That(state.Inventory.Units(Ore), Is.EqualTo(1), "ten counted activations of 0.1 are one Ore");
            Assert.That(vein.IsMaxRateShown, Is.True);
            Assert.That(vein.PulseText, Is.EqualTo("Max rate"));
            Assert.That(vein.ProgressTenths, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AHoldOfOneSecondOnTheRealTimelineGrantsAtMostTheCap()
        {
            yield return OpenReal();
            var state = session.State;
            Press();
            for (var step = 0; step < 10; step++)
            {
                Advance(100);
                vein.Tick();
            }

            Release();
            var counted = state.Inventory.Units(Ore) * 10 + state.MiningRemainderMilli / 100;
            Assert.That(counted, Is.InRange(10L, 11L), "the press plus ten ticks over two timeline seconds, never more than the cap per second");
            var before = state.MiningCountedInSecond;
            Advance(3000);
            vein.Tick();
            Assert.That(state.MiningCountedInSecond, Is.EqualTo(before), "nothing accrues after the release");
        }

        private void Advance(long milliseconds) =>
            Assert.That(session.Advance(milliseconds * RunSession.TicksPerMs).IsSuccess, Is.True);

        [UnityTest]
        public IEnumerator ARotationFocusLossAndASecondPointerEndAHoldAndGrantNoExtraOre(
            [Values("rotation", "focus-loss", "second-pointer", "dialog")] string cause)
        {
            yield return OpenReal();
            var state = session.State;
            Press();
            Advance(100);
            vein.Tick();
            var activations = state.MiningCountedInSecond;
            var remainder = state.MiningRemainderMilli;
            var ore = state.Inventory.Units(Ore);
            Assert.That(activations, Is.EqualTo(2));
            switch (cause)
            {
                case "rotation":
                    var rotated = ShellScreens.Rotated(rig.Screen);
                    shell.ApplyScreenLayout(rotated.Width, rotated.Height, rotated.SafeAreaPx);
                    break;
                case "focus-loss":
                    shell.NotifyApplicationFocusChanged(false);
                    break;
                case "second-pointer":
                    Press(2, UnityEngine.UIElements.PointerType.touch);
                    break;
                default:
                    Assert.That(shell.RequestConfirmation(new ConfirmationRequest { Title = "Test", Message = "A dialog." }), Is.True);
                    break;
            }

            Assert.That(vein.IsHolding, Is.False, cause);
            Advance(5000);
            vein.Tick();
            yield return null;
            Release();
            Release(2, UnityEngine.UIElements.PointerType.touch);
            Assert.That(state.MiningCountedInSecond, Is.EqualTo(activations), cause + ": no activation after the hold ended");
            Assert.That(state.MiningRemainderMilli, Is.EqualTo(remainder), cause);
            Assert.That(state.Inventory.Units(Ore), Is.EqualTo(ore), cause + ": no extra Ore");
        }

        // ---------------------------------------------------------------------------------------------------
        // How a hold ends
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AHoldEndsOnEveryCauseAndGrantsNothingAfterIt(
            [Values("release", "leave", "second-pointer", "rotation", "focus-loss", "dialog", "sheet", "key-up", "tab")] string cause)
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            var keyboard = cause == "key-up" || cause == "tab";
            if (keyboard)
            {
                vein.Root.Focus();
                KeyDown(KeyCode.Space);
            }
            else
            {
                Press();
            }

            Assert.That(vein.IsHolding, Is.True);
            OreVeinHoldEnd expected;
            switch (cause)
            {
                case "release":
                    Release();
                    expected = OreVeinHoldEnd.Released;
                    break;
                case "leave":
                    MoveTo(new Vector2(1f, 1f));
                    expected = OreVeinHoldEnd.Left;
                    break;
                case "second-pointer":
                    Press(2, UnityEngine.UIElements.PointerType.touch);
                    expected = OreVeinHoldEnd.SecondPointer;
                    break;
                case "rotation":
                    var rotated = ShellScreens.Rotated(rig.Screen);
                    shell.ApplyScreenLayout(rotated.Width, rotated.Height, rotated.SafeAreaPx);
                    expected = OreVeinHoldEnd.Rotation;
                    break;
                case "focus-loss":
                    shell.NotifyApplicationFocusChanged(false);
                    expected = OreVeinHoldEnd.FocusLoss;
                    break;
                case "dialog":
                    shell.RequestConfirmation(new ConfirmationRequest { Title = "Test", Message = "A dialog." });
                    expected = OreVeinHoldEnd.Dialog;
                    break;
                case "sheet":
                    shell.SelectTab(ShellTab.Market);
                    expected = OreVeinHoldEnd.Hidden;
                    break;
                case "key-up":
                    KeyUp(KeyCode.Space);
                    expected = OreVeinHoldEnd.Released;
                    break;
                default:
                    shell.TabButton(ShellTab.Market).Focus();
                    expected = OreVeinHoldEnd.FocusLoss;
                    break;
            }

            Assert.That(vein.IsHolding, Is.False, cause);
            Assert.That(vein.LastHoldEnd, Is.EqualTo(expected), cause);
            var calls = fake.Calls;
            for (var frame = 0; frame < 5; frame++)
            {
                Step(fake, 1000);
                yield return null;
            }

            Assert.That(fake.Calls, Is.EqualTo(calls), cause + ": a hold that ended grants nothing more");
            Assert.That(vein.Root.HasPointerCapture(0), Is.False, cause + ": the pointer capture is released");
        }

        [UnityTest]
        public IEnumerator ASecondPointerStartsNoHoldOfItsOwn()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            Press(1, UnityEngine.UIElements.PointerType.touch);
            Press(2, UnityEngine.UIElements.PointerType.touch);
            Assert.That(fake.Calls, Is.EqualTo(1), "the first finger counted, the second only cancelled");
            Release(2, UnityEngine.UIElements.PointerType.touch);
            Release(1, UnityEngine.UIElements.PointerType.touch);
            Step(fake, 3000);
            Assert.That(fake.Calls, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------------------------------------------
        // No peek, focus order and focus ring
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ASixHundredMillisecondTouchHoldMinesAndOpensNoPeek()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            Press(1, UnityEngine.UIElements.PointerType.touch);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(shell.IsDetailPinned, Is.False, "the vein is excluded from the exact-value long press");
            Assert.That(shell.DetailText, Is.Empty);
            Release(1, UnityEngine.UIElements.PointerType.touch);
            yield return null;
            Assert.That(shell.IsDetailPinned, Is.False, "and from tap-to-pin");
            Assert.That(fake.Calls, Is.EqualTo(1), "the press mined once; the timeline did not move");
            vein.Root.Focus();
            Pointer<PointerEnterEvent>(vein.Root, EventType.MouseMove, Center, 0, UnityEngine.UIElements.PointerType.mouse);
            yield return null;
            Assert.That(shell.DetailText, Is.Empty, "neither focus nor hover shows an exact value on the vein");
        }

        [UnityTest]
        public IEnumerator TheVeinComesAfterTheHudStripAndBeforeTheTabBarWithAVisibleFocusRing()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            var order = shell.GetFocusOrder().ToList();
            var index = order.IndexOf(vein.Root);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "the vein is focusable");
            foreach (var strip in new VisualElement[] { shell.CashButton, shell.StoreButton, shell.OverdriveButton })
            {
                Assert.That(order.IndexOf(strip), Is.LessThan(index), strip.name + " comes before the vein");
            }

            Assert.That(order.IndexOf(shell.TabButton(ShellTab.Factory)), Is.GreaterThan(index), "the tab bar comes after the vein");
            vein.Root.Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.FocusRingTarget, Is.SameAs(vein.Root));
            Assert.That(shell.FocusRing.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "a non-color focus ring surrounds the vein");
            Assert.That(vein.Root.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance));
            Assert.That(vein.Root.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance));
        }

        // ---------------------------------------------------------------------------------------------------
        // Feedback and states
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheOnePooledPulseCoalescesAHoldAndFallsBackToStaticText()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            Assert.That(vein.IsPulseShown, Is.False);
            Press();
            Assert.That(vein.PulseText, Is.EqualTo("+0.1"));
            fake.TimelineMs += 100;
            vein.Tick();
            Assert.That(vein.PulseText, Is.EqualTo("+0.1 ×2"), "a hold updates the same label in place");
            fake.TimelineMs += 100;
            vein.Tick();
            Assert.That(vein.PulseText, Is.EqualTo("+0.1 ×3"));
            Assert.That(vein.Root.Query<Label>(OreVeinControl.PulseName).ToList().Count, Is.EqualTo(1), "one pooled label");
            Assert.That(vein.AnimatePulse, Is.False, "the default is static text: Unity exposes no reduced-motion preference");
            Release();
            yield return new WaitForSecondsRealtime(OreVeinControl.PulseSeconds + 0.3f);
            Assert.That(vein.IsPulseShown, Is.False, "the pulse clears by itself");
        }

        [UnityTest]
        public IEnumerator ThePulseStaysOnOneLineAtTwoHundredPercentOnTheNarrowestScreen()
        {
            var narrowest = ShellScreens.All().OrderBy(screen => screen.Width).First();
            var fake = new FakeSource();
            yield return OpenWith(fake, narrowest, 200);
            var label = vein.Root.Q<Label>(OreVeinControl.PulseName);
            Press();
            for (var i = 0; i < 9; i++)
            {
                fake.TimelineMs += 100;
                vein.Tick();
            }

            Assert.That(vein.PulseText, Is.EqualTo("+0.1 ×10"));
            yield return ShellRig.Settle();
            AssertOneLine(label, "coalesced pulse");
            Release();
            fake.AtCap = true;
            Press();
            Release();
            Assert.That(vein.PulseText, Is.EqualTo("Max rate"));
            yield return ShellRig.Settle();
            AssertOneLine(label, "max rate cue");
        }

        private static void AssertOneLine(Label label, string context)
        {
            var oneLine = label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
            Assert.That(label.contentRect.height, Is.EqualTo(oneLine.y).Within(1.5f), context + ": one line of text, never wrapped");
            Assert.That(label.contentRect.width, Is.GreaterThanOrEqualTo(oneLine.x - 1f), context + ": not clipped");
        }

        [UnityTest]
        public IEnumerator ExcessActivationsShowAMaxRateCueThatClears()
        {
            var fake = new FakeSource { AtCap = true };
            yield return OpenWith(fake);
            Press();
            Release();
            Assert.That(vein.PulseText, Is.EqualTo("Max rate"));
            Assert.That(vein.IsMaxRateShown, Is.True);
            fake.AtCap = false;
            Press();
            Release();
            Assert.That(vein.PulseText, Is.EqualTo("+0.1"));
            Assert.That(vein.IsMaxRateShown, Is.False);
        }

        [UnityTest]
        public IEnumerator StoreFullShowsInsideTheVeinAndClearsWithoutMovingTheLayout()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            yield return ShellRig.Settle();
            var veinBounds = vein.Root.worldBound;
            var reasonBounds = vein.Root.Q<Label>(OreVeinControl.ReasonName).worldBound;
            fake.Reject = RunReasons.StorageFull;
            fake.HasRoom = false;
            Press();
            Release();
            yield return ShellRig.Settle();
            Assert.That(vein.ReasonText, Is.EqualTo("Store full"));
            Assert.That(vein.Root.Q<Label>(OreVeinControl.ReasonName).tooltip, Is.EqualTo(RunRejectionCopy.For(RunReasons.StorageFull)), "the tooltip carries the long copy");
            Assert.That(vein.ReasonCode, Is.EqualTo(RunReasons.StorageFull));
            Assert.That(vein.AccessibleName, Is.EqualTo("Mine Iron Ore, +0.1 per click, store full"));
            AssertSameRect(veinBounds, vein.Root.worldBound, "showing the reason");
            AssertSameRect(reasonBounds, vein.Root.Q<Label>(OreVeinControl.ReasonName).worldBound, "showing the reason");
            AssertWithin(vein.Root.worldBound, vein.Root.Q<Label>(OreVeinControl.ReasonName).worldBound, "the reason sits inside the vein");

            fake.HasRoom = true;
            fake.Reject = null;
            vein.Tick();
            yield return ShellRig.Settle();
            Assert.That(vein.ReasonText, Is.Empty, "it clears as soon as there is room");
            Assert.That(vein.AccessibleName, Is.EqualTo("Mine Iron Ore, +0.1 per click"));
            AssertSameRect(veinBounds, vein.Root.worldBound, "clearing the reason");
            AssertSameRect(reasonBounds, vein.Root.Q<Label>(OreVeinControl.ReasonName).worldBound, "clearing the reason");
        }

        private static void AssertSameRect(Rect expected, Rect actual, string context)
        {
            Assert.That(actual.xMin, Is.EqualTo(expected.xMin).Within(Tolerance), context + " (left)");
            Assert.That(actual.yMin, Is.EqualTo(expected.yMin).Within(Tolerance), context + " (top)");
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(Tolerance), context + " (width)");
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(Tolerance), context + " (height)");
        }

        [UnityTest]
        public IEnumerator StoreFullFromTheRealCommandClearsWhenStorageGrows()
        {
            yield return OpenReal(cash: 50_000_000);
            var state = session.State;
            Assert.That(state.Inventory.TryMine(Ore, state.Inventory.Free), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
            for (var i = 0; i < 10; i++)
            {
                Press();
                Release();
            }

            Assert.That(vein.ReasonCode, Is.EqualTo(RunReasons.StorageFull), "the tenth activation would need room for one more Ore");
            Assert.That(vein.ReasonText, Is.EqualTo("Store full"));
            Assert.That(state.MiningCountedInSecond, Is.EqualTo(9), "the rejected activation did not use the cap");
            Assert.That(vein.AccessibleName, Does.EndWith(", store full"));
            Assert.That(commands.UpgradeStorage().IsSuccess, Is.True);
            vein.Tick();
            Assert.That(vein.ReasonText, Is.Empty);
            Assert.That(vein.AccessibleName, Is.EqualTo("Mine Iron Ore, +0.1 per click"));
        }

        [UnityTest]
        public IEnumerator TheValueLimitAndLifecycleReasonsShowTheirCopyNextToTheVein()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            fake.Reject = RunReasons.ValueLimit;
            Press();
            Release();
            Assert.That(vein.ReasonText, Is.EqualTo(RunRejectionCopy.For(RunReasons.ValueLimit)));
            Assert.That(vein.AccessibleName, Does.EndWith(", limit reached"));
            fake.Reject = null;
            Press();
            Release();
            Assert.That(vein.ReasonText, Is.Empty, "a counted activation clears the limit reason");

            fake.Reject = RunReasons.LifecycleBlocked;
            fake.IsLifecycleBlocked = true;
            Press();
            Release();
            Assert.That(vein.ReasonText, Is.EqualTo(RunRejectionCopy.For(RunReasons.LifecycleBlocked)));
            Assert.That(vein.AccessibleName, Does.EndWith(", unavailable"));
            fake.IsLifecycleBlocked = false;
            vein.Tick();
            Assert.That(vein.ReasonText, Is.Empty, "it clears when the factory is available again");
        }

        [UnityTest]
        public IEnumerator ADialogDisablesTheVeinSoftlyWithAReasonAndKeepsItFocusable()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            shell.RequestConfirmation(new ConfirmationRequest { Title = "Test", Message = "A dialog." });
            yield return ShellRig.Settle();
            Assert.That(vein.IsSoftDisabled, Is.True);
            Assert.That(RunShellDocument.IsSoftDisabled(vein.Root), Is.True);
            Assert.That(vein.Root.focusable, Is.True, "a soft-disabled vein stays focusable");
            Press();
            Release();
            Assert.That(fake.Calls, Is.Zero, "a press under a dialog mines nothing");
            Assert.That(vein.ReasonText, Is.EqualTo(OreVeinCopy.DialogOpen), "and shows the reason");
            Assert.That(vein.IsHolding, Is.False);

            Assert.That(shell.CancelConfirmationForRotation(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(vein.IsSoftDisabled, Is.False);
            Press();
            Release();
            Assert.That(fake.Calls, Is.EqualTo(1), "the vein mines again once the dialog is closed");
        }

        [UnityTest]
        public IEnumerator AnUnavailableFactoryShowsItsReasonOnAPressWithoutCallingTheCommand()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            shell.SetGameplayAvailable(false);
            Press();
            Release();
            Assert.That(fake.Calls, Is.Zero);
            Assert.That(vein.ReasonText, Is.EqualTo(RunRejectionCopy.For(RunReasons.LifecycleBlocked)));
            shell.SetGameplayAvailable(true);
            vein.Tick();
            Press();
            Release();
            Assert.That(fake.Calls, Is.EqualTo(1));
            Assert.That(vein.ReasonText, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ReplacingTheRunDropsTheTransientProgress()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake);
            for (var i = 0; i < 4; i++)
            {
                Press();
                Release();
            }

            Assert.That(vein.ProgressTenths, Is.EqualTo(4));
            fake.ResetRun();
            vein.Tick();
            Assert.That(vein.ProgressTenths, Is.Zero, "a prestige starts a fresh remainder");
        }

        // ---------------------------------------------------------------------------------------------------
        // Visibility
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ContentWithoutManualMiningShowsNoVein()
        {
            yield return OpenWith(new FakeSource { IsAvailable = false });
            Assert.That(vein.Root.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(shell.GetFocusOrder().Contains(vein.Root), Is.False);
        }

        [UnityTest]
        public IEnumerator APhoneSheetHidesTheVeinAndRemovesItFromTheFocusOrderAndSpaceNeverActivatesItThere()
        {
            var fake = new FakeSource();
            yield return OpenWith(fake, ShellScreens.Phones().First());
            Assert.That(IsDisplayed(vein.Root), Is.True);
            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            Assert.That(shell.IsSheetOpen, Is.True);
            Assert.That(IsDisplayed(vein.Root), Is.False, "a sheet covers the world, so the vein is hidden");
            Assert.That(shell.GetFocusOrder().Contains(vein.Root), Is.False);
            KeyDown(KeyCode.Space);
            Submit();
            Press();
            Assert.That(fake.Calls, Is.Zero, "a hidden vein never activates");
            shell.SelectTab(ShellTab.Factory);
            yield return ShellRig.Settle();
            Assert.That(IsDisplayed(vein.Root), Is.True);
            Assert.That(shell.GetFocusOrder().Contains(vein.Root), Is.True);
        }

        [UnityTest]
        public IEnumerator TheSidePanelOfTabletAndDesktopDoesNotHideTheVein(
            [ValueSource(nameof(SidePanelScreens))] ShellScreen screen)
        {
            yield return OpenWith(new FakeSource(), screen);
            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            Assert.That(IsDisplayed(vein.Root), Is.True, screen + ": the side panel sits beside the world");
            Assert.That(Intersects(vein.Root.worldBound, shell.PanelHost.worldBound), Is.False, screen + ": the vein does not overlap the side panel");
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout at every screen class
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheVeinNeverOverlapsSlotsTheStripTheTabBarOrTheSheetAtEveryScreenClass(
            [ValueSource(nameof(Screens))] ShellScreen screen, [Values(100, 200)] int percent)
        {
            var fake = new FakeSource();
            yield return OpenWith(fake, screen, percent);
            yield return CheckVeinLayout(screen, "idle");

            // The worst case: the longest reason and a coalesced pulse are shown at once.
            fake.Reject = RunReasons.ValueLimit;
            Press();
            Release();
            fake.Reject = null;
            Press();
            fake.TimelineMs += 100;
            vein.Tick();
            fake.TimelineMs += 100;
            vein.Tick();
            fake.Reject = RunReasons.ValueLimit;
            Release();
            Press();
            Release();
            yield return ShellRig.Settle();
            Assert.That(vein.IsPulseShown || vein.IsReasonShown, Is.True);
            yield return CheckVeinLayout(screen, "reason shown");
            fake.Reject = null;
            vein.Tick();

            if (screen.IsPhone)
            {
                shell.SelectTab(ShellTab.Market);
                yield return ShellRig.Settle();
                AssertShellLayout(rig, "sheet open");
                Assert.That(IsDisplayed(vein.Root), Is.False, screen + ": a phone sheet hides the vein");
            }
            else
            {
                shell.SelectTab(ShellTab.Market);
                yield return ShellRig.Settle();
                yield return CheckVeinLayout(screen, "side panel on Market");
            }
        }

        private IEnumerator CheckVeinLayout(ShellScreen screen, string state)
        {
            var context = $"{screen} [{state}] @{rig.TextScalePercent}%";
            AssertShellLayout(rig, "ore vein " + state);
            var bounds = vein.Root.worldBound;
            Assert.That(IsDisplayed(vein.Root), Is.True, context);
            Assert.That(bounds.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), context + ": vein width");
            Assert.That(bounds.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), context + ": vein height");
            AssertWithin(rig.SafeRect(), bounds, context + ": the vein stays inside the safe area");
            AssertWithin(shell.WorldColumn.worldBound, bounds, context + ": the vein stays inside the world column");
            foreach (var other in new[]
            {
                ("top strip", shell.TopStrip),
                ("zoom column", shell.Root.Q<VisualElement>("zoom-column")),
                ("detail line", (VisualElement)shell.DetailBar),
                ("notice", shell.NoticeBar),
                ("world hint", shell.WorldHint),
                ("tab bar", shell.TabBar),
                ("panel host", shell.PanelHost),
            })
            {
                if (other.Item2 != null && IsDisplayed(other.Item2) && other.Item2.worldBound.height > 0f)
                {
                    Assert.That(Intersects(bounds, other.Item2.worldBound), Is.False, $"{context}: the vein overlaps the {other.Item1} {other.Item2.worldBound}");
                }
            }

            // The slots are framed in the world area the shell leaves uncovered; the vein must lie outside it.
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True, context + ": the slot area resolves");
            var slotArea = Rect.MinMaxRect(area.xMin, screen.Height - area.yMax, area.xMax, screen.Height - area.yMin);
            Assert.That(slotArea.height, Is.GreaterThan(0f), context + ": slot area " + slotArea);
            Assert.That(Intersects(bounds, slotArea), Is.False, $"{context}: the vein {bounds} overlaps the slot area {slotArea}");
            yield return null;
        }

        // ---------------------------------------------------------------------------------------------------
        // Copy
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheYieldReadsAsTheShortestDecimal()
        {
            Assert.That(OreVeinCopy.Yield(100), Is.EqualTo("0.1"));
            Assert.That(OreVeinCopy.Yield(150), Is.EqualTo("0.15"));
            Assert.That(OreVeinCopy.Yield(1), Is.EqualTo("0.001"));
            Assert.That(OreVeinCopy.Yield(1000), Is.EqualTo("1"));
            Assert.That(OreVeinCopy.Yield(2500), Is.EqualTo("2.5"));
            Assert.That(OreVeinCopy.Pulse(100, 1), Is.EqualTo("+0.1"));
            Assert.That(OreVeinCopy.Pulse(100, 4), Is.EqualTo("+0.1 ×4"));
        }

        [Test]
        public void TheManualYieldEffectHasPlayerFacingCopyInResearchAndIndustry()
        {
            var catalog = RunShellFixtures.ShippedCatalog;
            Assert.That(ResearchEffectCopy.Describe(catalog, "manualYield", "none", null, 1_050_000), Is.EqualTo("Manual mining yield x1.05 per level"));
            Assert.That(ResearchEffectCopy.Describe(catalog, "manualYield", "none", null, 1_250_000, false), Is.EqualTo("Manual mining yield x1.25"));
            Assert.That(ResearchEffectCopy.DescribeExact(catalog, "manualYield", "none", null, 1_050_000), Does.Contain("1,050,000 ppm"));
        }
    }
}
#endif
