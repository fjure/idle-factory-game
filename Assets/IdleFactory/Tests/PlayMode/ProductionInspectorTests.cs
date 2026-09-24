using System.Collections;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace IdleFactory.Tests.PlayMode
{
    public sealed class ProductionInspectorTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string InputResourceId = "dev.input";
        private const string OutputResourceId = "dev.output";

        private Scene loadedScene;
        private CoreGameFoundationBootstrap bootstrap;
        private MachineInspectorPresenter presenter;
        private ProductionCommands commands;
        private BuildingInstanceId selectedMachineId;

        [UnitySetUp]
        public IEnumerator LoadInspectorSceneAndSelectMachine()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development scene is loaded directly by the Editor test harness.");
            yield break;
#else
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                yield return null;
            }
            yield return null;

            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            var components = loadedScene.GetRootGameObjects()
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<CoreGameFoundationBootstrap>(true))
                .ToArray();
            Assert.That(components, Has.Length.EqualTo(1), "The scene must keep one factory state owner.");
            bootstrap = components[0];
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.Session, Is.Not.Null);

            var presenters = loadedScene.GetRootGameObjects()
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<MachineInspectorPresenter>(true))
                .ToArray();
            Assert.That(presenters, Has.Length.EqualTo(1));
            presenter = presenters[0];
            Assert.That(presenter.IsConfigured, Is.True, presenter.Error);

            commands = new ProductionCommands(bootstrap.Session);
            Assert.That(bootstrap.PlacementController.StartPlacement(
                new BuildingDefinitionId("dev.machine.basic"), new GridPosition(1, 1)), Is.True);
            bootstrap.PlacementController.Confirm();
            selectedMachineId = bootstrap.FactoryView.SelectedInstanceId.Value;
            yield return null;
#endif
        }

        [UnityTearDown]
        public IEnumerator UnloadInspectorScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(loadedScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            loadedScene = default;
            bootstrap = null;
            presenter = null;
            commands = null;
        }

        [UnityTest]
        public IEnumerator SelectionNamesMachineOwnedBuffersAndShowsCapacityProgressAndEveryBlocker()
        {
            var root = bootstrap.UiDocument.rootVisualElement;
            Assert.That(root.Q<Label>("inspector-machine").text, Does.Contain(selectedMachineId.Value));
            Assert.That(root.Q<Label>("inspector-state").text, Does.Contain("State: Blocked"));
            Assert.That(root.Q<Label>("inspector-process").text, Does.Contain("dev.recipe.basic"));
            Assert.That(root.Q<Label>("inspector-progress").text, Does.Contain("0.00 / 5.00 s"));
            Assert.That(root.Q<Label>("inspector-input-buffer").text, Does.Contain("This machine's input buffer"));
            Assert.That(root.Q<Label>("inspector-input-buffer").text, Does.Contain("0 / 4 stored"));
            Assert.That(root.Q<Label>("inspector-input-buffer").text, Does.Contain("recipe needs 2"));
            Assert.That(root.Q<Label>("inspector-output-buffer").text, Does.Contain("This machine's output buffer"));
            Assert.That(root.Q<Label>("inspector-output-buffer").text, Does.Contain("0 / 1 stored"));
            Assert.That(root.Q<Label>("inspector-output-buffer").text, Does.Contain("1 free"));
            Assert.That(root.Q<Label>("inspector-blockers").text, Does.Contain("Missing input Input: need 2, have 0"));

            AssertCommand(commands.AddResource(
                selectedMachineId, BufferRole.Output, new ResourceId(OutputResourceId), 1L));
            AssertCommand(commands.SetBuildingActive(selectedMachineId, false));
            yield return WaitForInspectorRefresh();

            var blockers = root.Q<Label>("inspector-blockers").text;
            Assert.That(blockers, Does.Contain("Inactive: this machine is switched off"));
            Assert.That(blockers, Does.Contain("Missing input Input: need 2, have 0"));
            Assert.That(blockers, Does.Contain("Output full Output: need 1 free, have 0"));
            Assert.That(blockers.Split('\n').Count(line => line.TrimStart().StartsWith("•")), Is.EqualTo(3),
                "Every simultaneous blocker must be shown instead of being collapsed to one summary.");
            Assert.That(root.Q<Label>("inspector-output-buffer").text, Does.Contain("1 / 1 stored"));
        }

        [UnityTest]
        public IEnumerator ResourceAndActiveButtonsSupportKeyboardAndRefreshAcceptedCommands()
        {
            var root = bootstrap.UiDocument.rootVisualElement;
            var supply = root.Q<Button>("inspector-supply-input");
            var remove = root.Q<Button>("inspector-remove-output");
            var toggle = root.Q<Button>("inspector-toggle-active");
            foreach (var button in new[] { supply, remove, toggle })
            {
                Assert.That(button, Is.Not.Null);
                Assert.That(button.focusable, Is.True);
                Assert.That(button.text, Is.Not.Empty);
                Assert.That(button.resolvedStyle.minWidth.value, Is.GreaterThanOrEqualTo(44f));
                Assert.That(button.resolvedStyle.minHeight.value, Is.GreaterThanOrEqualTo(44f));
            }

            Assert.That(toggle.text, Is.EqualTo("Deactivate machine"));
            Submit(supply);
            yield return WaitForInspectorRefresh();
            Assert.That(root.Q<Label>("inspector-input-buffer").text, Does.Contain("1 / 4 stored"));
            Assert.That(root.Q<Label>("inspector-feedback").text, Does.Contain("Added 1 unit"));

            AssertCommand(commands.AddResource(
                selectedMachineId, BufferRole.Output, new ResourceId(OutputResourceId), 1L));
            yield return WaitForInspectorRefresh();
            Submit(remove);
            yield return WaitForInspectorRefresh();
            Assert.That(root.Q<Label>("inspector-output-buffer").text, Does.Contain("0 / 1 stored"));
            Assert.That(root.Q<Label>("inspector-feedback").text, Does.Contain("Removed 1 unit"));

            toggle.Focus();
            Assert.That(toggle.panel.focusController.focusedElement, Is.SameAs(toggle));
            yield return null;
            Assert.That(toggle.ClassListContains("machine-inspector__control--focused"), Is.True,
                "Keyboard focus is surfaced with a dedicated visual state.");
            Assert.That(toggle.resolvedStyle.borderLeftColor, Is.EqualTo(Color.white),
                "Focused controls retain an explicit visible focus outline.");
            remove.Focus();
            yield return null;
            Assert.That(toggle.ClassListContains("machine-inspector__control--focused"), Is.False,
                "The visual focus state clears after focus moves away.");
            Assert.That(remove.ClassListContains("machine-inspector__control--focused"), Is.True);
            Submit(toggle);
            yield return WaitForInspectorRefresh();
            Assert.That(ReadInspection().IsActive, Is.False);
            Assert.That(root.Q<Button>("inspector-toggle-active").text, Is.EqualTo("Activate machine"));
        }

        [UnityTest]
        public IEnumerator EmptySelectionHidesMachineActionsAndRejectedCommandIsClearlyNotApplied()
        {
            var root = bootstrap.UiDocument.rootVisualElement;
            var panel = root.Q<VisualElement>("inspector-selected");
            var empty = root.Q<Label>("inspector-empty");
            var remove = root.Q<Button>("inspector-remove-output");
            bootstrap.FactoryView.Select(null);
            yield return null;

            Assert.That(empty.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(empty.text, Does.Contain("No machine selected"));
            Assert.That(panel.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(remove.enabledSelf, Is.False);
            Assert.That(remove.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            bootstrap.FactoryView.Select(selectedMachineId);
            yield return null;
            var before = ReadInspection();
            Submit(root.Q<Button>("inspector-remove-output"));
            yield return WaitForInspectorRefresh();
            var after = ReadInspection();
            Assert.That(after.Outputs[0].Quantity, Is.EqualTo(before.Outputs[0].Quantity));
            Assert.That(root.Q<Label>("inspector-feedback").text, Does.Contain("Not applied"));
            Assert.That(root.Q<Label>("inspector-feedback").ClassListContains("machine-inspector__feedback--error"), Is.True);
        }

        [UnityTest]
        public IEnumerator RejectedCommandFeedbackIsScopedToTheMachineThatReceivedIt()
        {
            var firstMachineId = selectedMachineId;
            Assert.That(bootstrap.PlacementController.StartPlacement(
                new BuildingDefinitionId("dev.machine.basic"), new GridPosition(6, 1)), Is.True);
            bootstrap.PlacementController.Confirm();
            var secondMachineId = bootstrap.FactoryView.SelectedInstanceId.Value;
            Assert.That(secondMachineId, Is.Not.EqualTo(firstMachineId));
            yield return null;

            var root = bootstrap.UiDocument.rootVisualElement;
            bootstrap.FactoryView.Select(firstMachineId);
            yield return null;
            Assert.That(root.Q<Label>("inspector-machine").text, Does.Contain(firstMachineId.Value));

            Submit(root.Q<Button>("inspector-remove-output"));
            yield return WaitForInspectorRefresh();
            var feedback = root.Q<Label>("inspector-feedback");
            Assert.That(feedback.text, Does.Contain("Not applied"));
            Assert.That(feedback.ClassListContains("machine-inspector__feedback--error"), Is.True);

            bootstrap.FactoryView.Select(secondMachineId);
            yield return null;
            Assert.That(root.Q<Label>("inspector-machine").text, Does.Contain(secondMachineId.Value));
            Assert.That(feedback.text, Does.Not.Contain("Not applied"),
                "A rejected command for the previous machine must not appear to describe this machine.");
            Assert.That(feedback.ClassListContains("machine-inspector__feedback--error"), Is.False);
            Assert.That(feedback.text, Does.Contain("No recent command for this machine"));
        }

        [UnityTest]
        public IEnumerator DeactivationPreservesResourcesAndProgressAndStopsAdvancement()
        {
            AssertCommand(commands.AddResource(
                selectedMachineId, BufferRole.Input, new ResourceId(InputResourceId), 2L));
            yield return WaitForProgressGreaterThan(0L, 2f);

            var beforeStop = ReadInspection();
            Assert.That(beforeStop.ProgressTicks, Is.GreaterThan(0L));
            var toggle = bootstrap.UiDocument.rootVisualElement.Q<Button>("inspector-toggle-active");
            Submit(toggle);
            yield return WaitForInspectorRefresh();

            var stopped = ReadInspection();
            Assert.That(stopped.IsActive, Is.False);
            Assert.That(stopped.ProgressTicks, Is.EqualTo(beforeStop.ProgressTicks));
            Assert.That(stopped.Inputs[0].Quantity, Is.EqualTo(beforeStop.Inputs[0].Quantity));
            Assert.That(stopped.Outputs[0].Quantity, Is.EqualTo(beforeStop.Outputs[0].Quantity));

            yield return new WaitForSecondsRealtime(0.3f);
            var afterStoppedTime = ReadInspection();
            Assert.That(afterStoppedTime.ProgressTicks, Is.EqualTo(stopped.ProgressTicks));
            Assert.That(afterStoppedTime.Inputs[0].Quantity, Is.EqualTo(stopped.Inputs[0].Quantity));
            Assert.That(afterStoppedTime.Outputs[0].Quantity, Is.EqualTo(stopped.Outputs[0].Quantity));

            Submit(bootstrap.UiDocument.rootVisualElement.Q<Button>("inspector-toggle-active"));
            yield return WaitForProgressGreaterThan(stopped.ProgressTicks, 2f);
            Assert.That(ReadInspection().IsActive, Is.True);
        }

        [UnityTest]
        public IEnumerator ActiveMachineWaitsForOutputSpaceBeforeResumingProduction()
        {
            AssertCommand(commands.AddResource(
                selectedMachineId, BufferRole.Input, new ResourceId(InputResourceId), 2L));
            AssertCommand(commands.AddResource(
                selectedMachineId, BufferRole.Output, new ResourceId(OutputResourceId), 1L));

            var toggle = bootstrap.UiDocument.rootVisualElement.Q<Button>("inspector-toggle-active");
            Submit(toggle);
            yield return WaitForInspectorRefresh();
            Assert.That(ReadInspection().IsActive, Is.False);

            Submit(bootstrap.UiDocument.rootVisualElement.Q<Button>("inspector-toggle-active"));
            yield return WaitForInspectorRefresh();
            Assert.That(ReadInspection().IsActive, Is.True);
            Assert.That(bootstrap.UiDocument.rootVisualElement.Q<Label>("inspector-blockers").text,
                Does.Contain("Output full Output"));
            var whileBlocked = ReadInspection().ProgressTicks;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(ReadInspection().ProgressTicks, Is.EqualTo(whileBlocked),
                "Reactivation must not create progress while the output buffer is full.");

            Submit(bootstrap.UiDocument.rootVisualElement.Q<Button>("inspector-remove-output"));
            yield return WaitForProgressGreaterThan(whileBlocked, 2f);
            Assert.That(bootstrap.UiDocument.rootVisualElement.Q<Label>("inspector-blockers").text,
                Does.Contain("Blockers: none"));
        }

        [UnityTest]
        public IEnumerator InspectorPollsLiveRuntimeAdvancementWithoutLoadingCheckpointBehavior()
        {
            AssertCommand(commands.AddResource(
                selectedMachineId, BufferRole.Input, new ResourceId(InputResourceId), 2L));
            var initialDisplayedRevision = presenter.LastRenderedRevision;
            yield return WaitForDisplayedProgress(2f);
            yield return WaitForInspectorRefresh();

            var root = bootstrap.UiDocument.rootVisualElement;
            Assert.That(presenter.LastRenderedRevision, Is.GreaterThan(initialDisplayedRevision));
            Assert.That(root.Q<Label>("inspector-progress").text, Does.Not.Contain("0.00 / 5.00 s"));
            Assert.That(root.Q<Label>("inspector-state").text, Does.Contain("State: Producing"));
            Assert.That(root.Q<Label>("inspector-blockers").text, Does.Contain("Blockers: none"));
        }

        [UnityTest]
        public IEnumerator ControlsAffectOnlyTheSelectedMachinesOwnBuffers()
        {
            var firstMachineId = selectedMachineId;
            Assert.That(bootstrap.PlacementController.StartPlacement(
                new BuildingDefinitionId("dev.machine.basic"), new GridPosition(6, 1)), Is.True);
            bootstrap.PlacementController.Confirm();
            var secondMachineId = bootstrap.FactoryView.SelectedInstanceId.Value;
            Assert.That(secondMachineId, Is.Not.EqualTo(firstMachineId));
            yield return null;

            Submit(bootstrap.UiDocument.rootVisualElement.Q<Button>("inspector-supply-input"));
            yield return WaitForInspectorRefresh();
            Assert.That(ReadInspection(secondMachineId).Inputs[0].Quantity, Is.EqualTo(1L));
            Assert.That(ReadInspection(firstMachineId).Inputs[0].Quantity, Is.EqualTo(0L));

            bootstrap.FactoryView.Select(firstMachineId);
            yield return null;
            Submit(bootstrap.UiDocument.rootVisualElement.Q<Button>("inspector-supply-input"));
            yield return WaitForInspectorRefresh();
            Assert.That(ReadInspection(firstMachineId).Inputs[0].Quantity, Is.EqualTo(1L));
            Assert.That(ReadInspection(secondMachineId).Inputs[0].Quantity, Is.EqualTo(1L));
        }

        [UnityTest]
        public IEnumerator ResponsiveClassesSafeAreaAndControlAccessibilityCoverSupportedViewports()
        {
            var originalEnabled = bootstrap.enabled;
            var documentRoot = bootstrap.UiDocument.rootVisualElement;
            var originalDocumentWidth = documentRoot.style.width;
            var originalDocumentHeight = documentRoot.style.height;
            bootstrap.enabled = false;
            var cases = new[]
            {
                (360, 640, FactoryHudLayout.CompactPortrait, "factory-hud--compact-portrait"),
                (390, 844, FactoryHudLayout.CompactPortrait, "factory-hud--compact-portrait"),
                (768, 1024, FactoryHudLayout.TabletPortrait, "factory-hud--tablet-portrait"),
                (640, 360, FactoryHudLayout.CompactLandscape, "factory-hud--compact-landscape"),
                (844, 390, FactoryHudLayout.CompactLandscape, "factory-hud--compact-landscape"),
                (1024, 768, FactoryHudLayout.TabletLandscape, "factory-hud--tablet-landscape"),
                (1280, 720, FactoryHudLayout.Desktop, "factory-hud--desktop"),
                (1920, 1080, FactoryHudLayout.Desktop, "factory-hud--desktop"),
            };

            foreach (var item in cases)
            {
                var safeArea = new Rect(18f, 14f, item.Item1 - 36f, item.Item2 - 28f);
                documentRoot.style.width = item.Item1;
                documentRoot.style.height = item.Item2;
                bootstrap.Hud.ApplyLayout(item.Item1, item.Item2, safeArea);
                yield return null;
                yield return null;

                Assert.That(bootstrap.Hud.Layout, Is.EqualTo(item.Item3));
                Assert.That(bootstrap.Hud.Root.ClassListContains(item.Item4), Is.True);
                Assert.That(bootstrap.Hud.Root.style.paddingLeft.value.value, Is.EqualTo(18f).Within(0.01f));
                Assert.That(bootstrap.Hud.Root.style.paddingRight.value.value, Is.EqualTo(18f).Within(0.01f));
                Assert.That(bootstrap.Hud.Root.style.paddingTop.value.value, Is.EqualTo(14f).Within(0.01f));
                Assert.That(bootstrap.Hud.Root.style.paddingBottom.value.value, Is.EqualTo(14f).Within(0.01f));

                var root = bootstrap.Hud.Root;
                Assert.That(root.worldBound.width, Is.EqualTo(item.Item1).Within(1f),
                    "Resolved HUD bounds must use the viewport under test.");
                Assert.That(root.worldBound.height, Is.EqualTo(item.Item2).Within(1f),
                    "Resolved HUD bounds must use the viewport under test.");

                var usableBounds = new Rect(
                    root.worldBound.xMin + 18f,
                    root.worldBound.yMin + 14f,
                    item.Item1 - 36f,
                    item.Item2 - 28f);
                var sidePanels = root.Q<VisualElement>("side-panels");
                var actions = root.Q<VisualElement>("actions");
                var scroll = root.Q<ScrollView>("inspector-scroll");
                var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
                Assert.That(sidePanels.worldBound.width, Is.GreaterThan(0f));
                Assert.That(actions.worldBound.width, Is.GreaterThan(0f));
                AssertRectWithin(usableBounds, sidePanels.worldBound, "Side panels must stay inside the safe area.");
                AssertRectWithin(usableBounds, actions.worldBound, "HUD controls must stay inside the safe area.");
                AssertRectWithin(usableBounds, scroll.worldBound, "Inspector scroll viewport must stay inside the safe area.");
                Assert.That(sidePanels.worldBound.Overlaps(actions.worldBound), Is.False,
                    $"HUD panels overlap at {item.Item1}x{item.Item2}.");
                Assert.That(viewport, Is.Not.Null);
                Assert.That(scroll.contentContainer.worldBound.height, Is.GreaterThan(0f));

                foreach (var name in new[]
                         {
                             "inspector-supply-input",
                             "inspector-remove-output",
                             "inspector-toggle-active",
                         })
                {
                    var button = bootstrap.Hud.Root.Q<Button>(name);
                    Assert.That(button.focusable, Is.True, name);
                    Assert.That(button.resolvedStyle.minWidth.value, Is.GreaterThanOrEqualTo(44f), name);
                    Assert.That(button.resolvedStyle.minHeight.value, Is.GreaterThanOrEqualTo(44f), name);
                    Assert.That(button.text, Is.Not.Empty, name);

                    scroll.ScrollTo(button);
                    yield return null;
                    yield return null;
                    AssertRectWithin(viewport.worldBound, button.worldBound,
                        $"{name} must be reachable by scrolling at {item.Item1}x{item.Item2}.");
                }

                if (item.Item3 == FactoryHudLayout.CompactLandscape)
                {
                    Assert.That(scroll.contentContainer.worldBound.height, Is.GreaterThan(viewport.worldBound.height),
                        "Compact landscape should exercise real inspector overflow through its scroll container.");
                }
            }

            documentRoot.style.width = originalDocumentWidth;
            documentRoot.style.height = originalDocumentHeight;
            bootstrap.enabled = originalEnabled;
            bootstrap.ApplySafeAreaLayout(true);
            yield return null;
            Assert.That(bootstrap.Hud.Root.Q<ScrollView>("inspector-scroll"), Is.Not.Null,
                "The inspector remains scrollable in compact landscape when available height is constrained.");
        }

        private static void AssertRectWithin(Rect outer, Rect inner, string message)
        {
            const float tolerance = 1f;
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - tolerance), message + " (left)");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - tolerance), message + " (top)");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + tolerance), message + " (right)");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + tolerance), message + " (bottom)");
        }

        private IEnumerator WaitForProgressGreaterThan(long previousTicks, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (Time.realtimeSinceStartupAsDouble < deadline && ReadInspection().ProgressTicks <= previousTicks)
            {
                yield return new WaitForSecondsRealtime(0.05f);
            }

            Assert.That(ReadInspection().ProgressTicks, Is.GreaterThan(previousTicks),
                "The configured runtime host must advance the selected unblocked machine.");
        }

        private IEnumerator WaitForDisplayedProgress(float timeoutSeconds)
        {
            var progressLabel = bootstrap.UiDocument.rootVisualElement.Q<Label>("inspector-progress");
            const string zeroProgress = "0.00 / 5.00 s";
            var deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (Time.realtimeSinceStartupAsDouble < deadline && progressLabel.text.Contains(zeroProgress))
            {
                yield return new WaitForSecondsRealtime(0.05f);
            }

            Assert.That(progressLabel.text, Does.Not.Contain(zeroProgress),
                "The live inspection must refresh to a visibly advanced progress value.");
        }

        private IEnumerator WaitForInspectorRefresh()
        {
            yield return new WaitForSecondsRealtime(0.15f);
            yield return null;
        }

        private MachineInspectionSnapshot ReadInspection() => ReadInspection(selectedMachineId);

        private MachineInspectionSnapshot ReadInspection(BuildingInstanceId instanceId)
        {
            var result = commands.GetMachineInspection(instanceId);
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private static void AssertCommand(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static void AssertCommand<T>(CommandResult<T> result)
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            button.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                button.SendEvent(submit);
            }
        }
    }
}
