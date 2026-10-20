#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T361 (batch-A Play-Tester finding F6, FR-042, US10 scenario 3): holds the Mine control of the authored shell on a screen panel
    /// with pointer down, a real-time wait and pointer up, and asserts the hold yield. The run timeline follows real elapsed time, so
    /// the number of activations is the content cap times the time held, never above it, and nothing accrues after the release.
    /// The pointer events are raised on the control as UI Toolkit's own event system would: a mouse pointer with id 0, a press,
    /// then a release, while the panel keeps pointer capture between them. <c>OreVeinPanelTests</c> proves the hold schedule
    /// and every end cause with a fake source; this fixture proves it for the real run session and the real shell document.
    /// A batch-mode editor never reports application focus, and UI Toolkit then drops every device event (verified 2026-10-20 with
    /// a virtual Input System mouse, the Input System UI actions enabled and bound, and the event system forced to update), so a
    /// device-level hold can only be exercised in a player or a focused editor. The Windows development build covers that leg.
    /// </summary>
    public sealed class OreVeinRealPointerHoldTests
    {
        private const float HoldSeconds = 1.5f;
        private GameObject host;
        private PanelSettings settings;

        [TearDown]
        public void TearDown()
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

        private static void SetProperty(EventBase evt, string name, object value)
        {
            for (var type = evt.GetType(); type != null; type = type.BaseType)
            {
                var setter = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?.GetSetMethod(true);
                if (setter != null)
                {
                    setter.Invoke(evt, new[] { value });
                    return;
                }
            }

            Assert.Fail("The event has no settable " + name + ".");
        }

        private static void Pointer<T>(VisualElement target, EventType type, Vector2 position)
            where T : PointerEventBase<T>, new()
        {
            var evt = PointerEventBase<T>.GetPooled(new Event { type = type, mousePosition = position, button = 0 });
            SetProperty(evt, "pointerId", 0);
            SetProperty(evt, "pointerType", UnityEngine.UIElements.PointerType.mouse);
            using (evt)
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        [UnityTest]
        public IEnumerator HoldingTheMineControlYieldsTheCappedRateAndReleaseStopsIt()
        {
            var session = RunShellFixtures.NewSession(out var commands, 1, 25);
            var source = new RunOreVeinSource(session, commands);

            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            host = new GameObject("run-shell-pointer-hold");
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

            var cap = source.ActivationsPerSecond;
            var oreBefore = session.State.Inventory.Units("iron.ore") * 10 + session.State.MiningRemainderMilli / 100;

            Pointer<PointerDownEvent>(vein.Root, EventType.MouseDown, bounds.center);
            yield return null;
            Assert.That(vein.IsHolding, Is.True, "a pointer press starts the hold");

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

            Pointer<PointerUpEvent>(vein.Root, EventType.MouseUp, bounds.center);
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
