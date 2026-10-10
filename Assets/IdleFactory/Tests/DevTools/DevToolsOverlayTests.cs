using System.IO;
using System.Linq;
using IdleFactory.DevTools;
using IdleFactory.Unity.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.DevTools
{
    /// <summary>
    /// T308 (AR-10, OPS-1, SEC-13): the DevTools overlay and dev save root. This assembly compiles only with
    /// <c>IDLEFACTORY_DEV</c>, so the normal Edit Mode and Play Mode suites never build it.
    /// </summary>
    public sealed class DevToolsOverlayTests
    {
        private GameObject host;
        private DevToolsOverlay overlay;
        private DevTimeScale timeScale;

        [SetUp]
        public void SetUp()
        {
            timeScale = new DevTimeScale();
            host = new GameObject("DevTools Overlay Test");
            overlay = DevToolsOverlay.Create(timeScale, host);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        private static void Send(VisualElement target, EventBase evt)
        {
            using (evt)
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        [Test]
        public void OverlayOffersTheFiveFactorsAsFocusableButtonsOfAtLeast44Points()
        {
            var labels = Enumerable.Range(0, 5).Select(i => overlay.ButtonAt(i).text).ToArray();
            Assert.That(labels, Is.EqualTo(new[] { "×1", "×10", "×60", "×600", "Max" }));
            for (var i = 0; i < 5; i++)
            {
                var button = overlay.ButtonAt(i);
                Assert.That(button.focusable, Is.True);
                Assert.That(button.style.minHeight.value.value, Is.GreaterThanOrEqualTo(44f));
                Assert.That(button.style.minWidth.value.value, Is.GreaterThanOrEqualTo(44f));
            }
        }

        [Test]
        public void MarkerIsHiddenAtX1AndShowsTheFactorWithAnAccessibleName()
        {
            Assert.That(overlay.Marker.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(overlay.MarkerAccessibleName, Is.Empty);

            timeScale.Select(3);

            Assert.That(overlay.Marker.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(overlay.Marker.text, Is.EqualTo("ACCELERATED ×600"));
            Assert.That(overlay.MarkerAccessibleName, Is.EqualTo("Accelerated, factor 600"));
            Assert.That(overlay.Marker.pickingMode, Is.EqualTo(PickingMode.Ignore), "The marker is not dismissible.");

            timeScale.Select(0);
            Assert.That(overlay.Marker.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void MaxReportsTheEffectiveMultiplierInTheMarker()
        {
            timeScale.Select(DevTimeScale.MaxIndex);
            Assert.That(timeScale.Scale, Is.EqualTo(double.PositiveInfinity));

            timeScale.ReportEffectiveMultiplier(640d);

            Assert.That(overlay.Marker.text, Is.EqualTo("ACCELERATED ×640"));
            Assert.That(overlay.MarkerAccessibleName, Is.EqualTo("Accelerated, factor 640"));
        }

        [Test]
        public void ClickingAndNavigationSubmitSelectAFactor()
        {
            Send(overlay.ButtonAt(2), new NavigationSubmitEvent());
            Assert.That(timeScale.SelectedIndex, Is.EqualTo(2));
            Assert.That(timeScale.Scale, Is.EqualTo(60d));

            overlay.ButtonAt(1).Focus();
            Send(overlay.ButtonAt(1), new NavigationSubmitEvent());
            Assert.That(timeScale.SelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void NumberKeysSelectAFactorFromTheKeyboard()
        {
            using (var evt = KeyDownEvent.GetPooled('4', KeyCode.Alpha4, EventModifiers.None))
            {
                evt.target = overlay.ButtonAt(0);
                overlay.ButtonAt(0).SendEvent(evt);
            }

            Assert.That(timeScale.SelectedIndex, Is.EqualTo(3));
        }

        [Test]
        public void SelectedButtonCarriesAnAccessibleName()
        {
            timeScale.Select(2);
            Assert.That(overlay.ButtonAt(2).tooltip, Is.EqualTo("Time scale ×60, selected"));
        }

        [Test]
        public void SaveRootIsTheDevelopmentRootUnderTheDefine()
        {
            Assert.That(PersistenceRoots.IsDevelopmentRoot, Is.True);
            var root = PersistenceRoots.Resolve(Application.persistentDataPath);
            Assert.That(Path.GetFileName(root), Is.EqualTo("IdleFactory-Dev"));
            Assert.That(Path.GetDirectoryName(root), Is.EqualTo(Path.GetFullPath(Application.persistentDataPath).TrimEnd('\\', '/')).Or.EqualTo(Application.persistentDataPath));
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator RepeatedInitializationLeavesAtMostOneOverlay()
        {
            var initialize = typeof(DevToolsRuntime).GetMethod("Initialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(initialize, Is.Not.Null);
            initialize.Invoke(null, null);
            initialize.Invoke(null, null);
            yield return null;

            Assert.That(Object.FindObjectsByType<DevToolsOverlay>(FindObjectsInactive.Include).Count(o => o.gameObject == DevToolsRuntime.Host), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<DevToolsOverlay>(FindObjectsInactive.Include).Count(o => o.name == "IdleFactory DevTools"), Is.EqualTo(1));
        }

        [Test]
        public void NothingInTheUnityAssemblyReferencesDevTools()
        {
            var references = typeof(FactoryRuntimeHost).Assembly.GetReferencedAssemblies().Select(name => name.Name);
            Assert.That(references, Does.Not.Contain("IdleFactory.DevTools"));
        }
    }
}
