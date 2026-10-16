#if UNITY_EDITOR
using System.Collections;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T361 (batch-A Play-Tester finding F6, FR-042, US10 scenario 3): holds the Mine control with real pointer input. The mouse is a
    /// virtual Input System device whose state events go through the Input System update, so UI Toolkit raises its own pointer events
    /// on a panel that renders to the screen. The test presses, waits real frames while the run timeline follows real time, then
    /// releases. <c>OreVeinPanelTests</c> proves the hold schedule with synthetic events; this proves the event path a player uses
    /// (press, capture, no premature leave, release), which the Play-Tester could not tell apart from a harness artefact.
    /// </summary>
    public sealed class OreVeinRealPointerHoldTests : InputTestFixture
    {
        private const float HoldSeconds = 1.5f;
        private Mouse mouse;
        private GameObject host;
        private PanelSettings settings;

        public override void Setup()
        {
            base.Setup();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            try
            {
                if (host != null)
                {
                    Object.DestroyImmediate(host);
                }

                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }
            }
            finally
            {
                base.TearDown();
            }
        }

        private void MouseState(Vector2 screenPosition, bool left)
        {
            var state = new MouseState { position = screenPosition }.WithButton(MouseButton.Left, left);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator HoldingTheMineControlWithARealPointerYieldsTheCappedRateAndReleaseStopsIt()
        {
            var session = RunShellFixtures.NewSession(out var commands, 1, 25);
            var source = new RunOreVeinSource(session, commands);

            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            host = new GameObject("run-shell-real-pointer");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            var shell = host.AddComponent<RunShellDocument>();
            shell.AutoProbeDevice = false;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/IdleFactory/Unity/UI/FactoryRun.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/IdleFactory/Unity/UI/FactoryRun.uss");
            Assert.That(tree, Is.Not.Null);
            Assert.That(styles, Is.Not.Null);
            Assert.That(shell.Configure(document, tree, styles, null), Is.True, shell.Error);
            yield return null;
            shell.ApplyScreenLayout(Screen.width, Screen.height, new Rect(0, 0, Screen.width, Screen.height));
            shell.BindOreVein(source);
            yield return ShellRig.Settle();

            var vein = shell.OreVein;
            Assert.That(vein, Is.Not.Null);
            Assert.That(vein.IsHidden, Is.False, "the vein is shown for a run with manual mining");
            var bounds = vein.Root.worldBound;
            Assert.That(bounds.width, Is.GreaterThan(0f));
            Assert.That(bounds.height, Is.GreaterThan(0f));

            // Panel points to screen pixels (top-left origin to bottom-left origin), with the panel's own scale.
            var panel = vein.Root.panel;
            var scale = Screen.height / panel.visualTree.worldBound.height;
            var screenPoint = new Vector2(bounds.center.x * scale, Screen.height - bounds.center.y * scale);
            var roundTrip = RuntimePanelUtils.ScreenToPanel(panel, screenPoint);
            Assert.That(Vector2.Distance(roundTrip, bounds.center), Is.LessThan(2f), "the screen point lands on the vein");

            MouseState(screenPoint, false);
            yield return null;
            var cap = source.ActivationsPerSecond;
            var oreBefore = session.State.Inventory.Units("iron.ore") * 10 + session.State.MiningRemainderMilli / 100;

            MouseState(screenPoint, true);
            yield return null;
            Assert.That(vein.IsHolding, Is.True, "a real pointer press starts the hold");

            var startMs = session.TimelineUtcMs;
            var elapsed = 0f;
            while (elapsed < HoldSeconds)
            {
                var delta = Time.unscaledDeltaTime;
                elapsed += delta;
                Assert.That(session.Advance((long)(delta * 1000f) * RunSession.TicksPerMs).IsSuccess, Is.True);
                yield return null;
                Assert.That(vein.IsHolding, Is.True, "the hold survives the wait; ended by " + vein.LastHoldEnd);
            }

            MouseState(screenPoint, false);
            yield return null;
            Assert.That(vein.IsHolding, Is.False);
            Assert.That(vein.LastHoldEnd, Is.EqualTo(OreVeinHoldEnd.Released));

            var heldSeconds = (session.TimelineUtcMs - startMs) / 1000.0;
            var requested = vein.RequestedActivations;
            Assert.That(requested, Is.InRange((long)System.Math.Floor(cap * heldSeconds) - 2, (long)System.Math.Ceiling(cap * heldSeconds) + 2),
                "the press plus one activation per 1/cap s over " + heldSeconds.ToString("F2") + " s");
            var counted = session.State.Inventory.Units("iron.ore") * 10 + session.State.MiningRemainderMilli / 100 - oreBefore;
            Assert.That(counted, Is.GreaterThanOrEqualTo(requested - 1), "each activation adds 0.1 Ore at the base yield");
            Assert.That(counted, Is.LessThanOrEqualTo(cap * ((long)System.Math.Ceiling(heldSeconds) + 1)), "never above the cap per second");

            var frozen = requested;
            for (var i = 0; i < 5; i++)
            {
                session.Advance(200 * RunSession.TicksPerMs);
                yield return null;
            }

            Assert.That(vein.RequestedActivations, Is.EqualTo(frozen), "nothing accrues after the release");
        }
    }
}
#endif
