using System;
using System.Globalization;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>Projects one selected machine's immutable application inspection into the development HUD.</summary>
    public sealed class MachineInspectorPresenter : MonoBehaviour
    {
        private const long CoreTicksPerSecond = TimeSpan.TicksPerSecond;
        private const float InspectionPollIntervalSeconds = 0.1f;

        private CoreGameFoundationBootstrap bootstrap;
        private FactoryRuntimeHost runtimeHost;
        private FactoryView factoryView;
        private ProductionCommands commands;
        private DefinitionCatalog definitions;
        private UIDocument uiDocument;
        private VisualElement root;
        private VisualElement selectedContent;
        private Label emptyLabel;
        private Label machineLabel;
        private Label stateLabel;
        private Label processLabel;
        private Label progressLabel;
        private Label inputBufferLabel;
        private Label outputBufferLabel;
        private Label blockersLabel;
        private Label commandFeedbackLabel;
        private Button supplyInputButton;
        private Button removeOutputButton;
        private Button toggleActiveButton;
        private bool configured;
        private long lastRenderedRevision = -1;
        private float nextInspectionPoll;
        private ResourceId? selectedInputResource;
        private ResourceId? selectedOutputResource;
        private BuildingInstanceId? renderedInstanceId;

        public bool IsConfigured => configured;
        public long LastRenderedRevision => lastRenderedRevision;
        public string Error { get; private set; } = string.Empty;

        public void RefreshLifecycleGate()
        {
            if (!configured)
            {
                return;
            }

            RefreshRuntimeHostState();
            RefreshSelectedInspection();
        }

        private void Start()
        {
            ConfigureScene();
        }

        private void Update()
        {
            if (!configured || bootstrap.Session == null || Time.unscaledTime < nextInspectionPoll)
            {
                return;
            }

            nextInspectionPoll = Time.unscaledTime + InspectionPollIntervalSeconds;
            RefreshRuntimeHostState();
            if (CurrentRevision() != lastRenderedRevision)
            {
                RefreshSelectedInspection();
            }
        }

        private void OnDestroy()
        {
            if (factoryView != null)
            {
                factoryView.SelectionChanged -= HandleSelectionChanged;
            }

            if (supplyInputButton != null)
            {
                supplyInputButton.clicked -= SupplySelectedInput;
                UnbindFocusOutline(supplyInputButton);
            }

            if (removeOutputButton != null)
            {
                removeOutputButton.clicked -= RemoveSelectedOutput;
                UnbindFocusOutline(removeOutputButton);
            }

            if (toggleActiveButton != null)
            {
                toggleActiveButton.clicked -= ToggleSelectedActive;
                UnbindFocusOutline(toggleActiveButton);
            }
        }

        public void SupplySelectedInput()
        {
            if (!CanMutate())
            {
                RefreshLifecycleGate();
                return;
            }

            var selected = CurrentInspection();
            if (selected == null || !selectedInputResource.HasValue)
            {
                return;
            }

            var result = commands.AddResource(
                selected.InstanceId,
                BufferRole.Input,
                selectedInputResource.Value,
                1L);
            HandleCommandResult(
                result.Succeeded,
                result.Message,
                result.Succeeded ? "Added 1 unit to this machine's input buffer." : string.Empty);
        }

        public void RemoveSelectedOutput()
        {
            if (!CanMutate())
            {
                RefreshLifecycleGate();
                return;
            }

            var selected = CurrentInspection();
            if (selected == null || !selectedOutputResource.HasValue)
            {
                return;
            }

            var result = commands.RemoveResource(
                selected.InstanceId,
                BufferRole.Output,
                selectedOutputResource.Value,
                1L);
            HandleCommandResult(
                result.Succeeded,
                result.Message,
                result.Succeeded ? "Removed 1 unit from this machine's output buffer." : string.Empty);
        }

        public void ToggleSelectedActive()
        {
            if (!CanMutate())
            {
                RefreshLifecycleGate();
                return;
            }

            var selected = CurrentInspection();
            if (selected == null)
            {
                return;
            }

            var activating = !selected.IsActive;
            var result = commands.SetBuildingActive(selected.InstanceId, activating);
            HandleCommandResult(
                result.Succeeded,
                result.Message,
                result.Succeeded
                    ? activating ? "Machine activated." : "Machine deactivated. Resources and progress are preserved."
                    : string.Empty);
        }

        private void ConfigureScene()
        {
            bootstrap = GetComponent<CoreGameFoundationBootstrap>();
            runtimeHost = GetComponent<FactoryRuntimeHost>();
            factoryView = bootstrap != null ? bootstrap.FactoryView : null;
            uiDocument = bootstrap != null ? bootstrap.UiDocument : null;

            if (bootstrap == null || !bootstrap.IsInitialized || bootstrap.Session == null ||
                runtimeHost == null || factoryView == null || uiDocument == null)
            {
                Fail("Machine inspection is unavailable because the shared factory runtime is not ready.");
                return;
            }

            try
            {
                root = uiDocument.rootVisualElement.Q<VisualElement>("factory-hud");
                selectedContent = root?.Q<VisualElement>("inspector-selected");
                emptyLabel = root?.Q<Label>("inspector-empty");
                machineLabel = root?.Q<Label>("inspector-machine");
                stateLabel = root?.Q<Label>("inspector-state");
                processLabel = root?.Q<Label>("inspector-process");
                progressLabel = root?.Q<Label>("inspector-progress");
                inputBufferLabel = root?.Q<Label>("inspector-input-buffer");
                outputBufferLabel = root?.Q<Label>("inspector-output-buffer");
                blockersLabel = root?.Q<Label>("inspector-blockers");
                commandFeedbackLabel = root?.Q<Label>("inspector-feedback");
                supplyInputButton = root?.Q<Button>("inspector-supply-input");
                removeOutputButton = root?.Q<Button>("inspector-remove-output");
                toggleActiveButton = root?.Q<Button>("inspector-toggle-active");

                if (root == null || selectedContent == null || emptyLabel == null || machineLabel == null ||
                    stateLabel == null || processLabel == null || progressLabel == null || inputBufferLabel == null ||
                    outputBufferLabel == null || blockersLabel == null || commandFeedbackLabel == null ||
                    supplyInputButton == null || removeOutputButton == null || toggleActiveButton == null)
                {
                    Fail("Machine inspector UXML is missing a required named element.");
                    return;
                }

                definitions = DefinitionCatalogBuilder.Build(bootstrap.DevelopmentCatalog);
                commands = bootstrap.ProductionCommands;
                if (commands == null)
                {
                    Fail("Machine inspection is unavailable because the shared production commands are not ready.");
                    return;
                }

                supplyInputButton.clicked += SupplySelectedInput;
                removeOutputButton.clicked += RemoveSelectedOutput;
                toggleActiveButton.clicked += ToggleSelectedActive;
                BindFocusOutline(supplyInputButton);
                BindFocusOutline(removeOutputButton);
                BindFocusOutline(toggleActiveButton);
                factoryView.SelectionChanged += HandleSelectionChanged;
                configured = true;
                RefreshSelectedInspection();
            }
            catch (Exception exception)
            {
                Fail($"Machine inspection could not start: {exception.Message}");
            }
        }

        private void HandleSelectionChanged(SelectionResolution resolution)
        {
            if (resolution == null || resolution.Kind != SelectionResolutionKind.Selected)
            {
                ShowEmptySelection();
                return;
            }

            RenderInspection(resolution.Inspection);
        }

        private static void BindFocusOutline(Button button)
        {
            button.RegisterCallback<FocusInEvent>(HandleControlFocusIn);
            button.RegisterCallback<FocusOutEvent>(HandleControlFocusOut);
        }

        private static void UnbindFocusOutline(Button button)
        {
            button.UnregisterCallback<FocusInEvent>(HandleControlFocusIn);
            button.UnregisterCallback<FocusOutEvent>(HandleControlFocusOut);
        }

        private static void HandleControlFocusIn(FocusInEvent evt)
        {
            (evt.target as VisualElement)?.AddToClassList("machine-inspector__control--focused");
        }

        private static void HandleControlFocusOut(FocusOutEvent evt)
        {
            (evt.target as VisualElement)?.RemoveFromClassList("machine-inspector__control--focused");
        }

        private void RefreshSelectedInspection()
        {
            if (!configured || !factoryView.SelectedInstanceId.HasValue)
            {
                ShowEmptySelection();
                return;
            }

            var result = commands.GetMachineInspection(factoryView.SelectedInstanceId.Value);
            if (!result.Succeeded)
            {
                ShowEmptySelection("Selected machine is no longer available.");
                return;
            }

            RenderInspection(result.Value);
        }

        private MachineInspectionSnapshot CurrentInspection()
        {
            if (!configured || !factoryView.SelectedInstanceId.HasValue)
            {
                return null;
            }

            var result = commands.GetMachineInspection(factoryView.SelectedInstanceId.Value);
            return result.Succeeded ? result.Value : null;
        }

        private void RenderInspection(MachineInspectionSnapshot inspection)
        {
            if (inspection == null || !IsUiReady())
            {
                return;
            }

            if (!renderedInstanceId.HasValue || renderedInstanceId.Value != inspection.InstanceId)
            {
                ClearCommandFeedback();
                renderedInstanceId = inspection.InstanceId;
            }

            selectedContent.style.display = DisplayStyle.Flex;
            emptyLabel.style.display = DisplayStyle.None;
            machineLabel.text = $"Machine {inspection.DefinitionId.Value} · instance {inspection.InstanceId.Value}";

            var hasInactiveBlocker = inspection.Blockers.Any(blocker => blocker.Kind == ProductionBlockerKind.Inactive);
            var isProducing = inspection.IsActive && inspection.Blockers.Count == 0;
            stateLabel.text = isProducing
                ? "State: Producing"
                : hasInactiveBlocker
                    ? "State: Inactive"
                    : "State: Blocked";
            selectedContent.EnableInClassList("machine-inspector--producing", isProducing);
            selectedContent.EnableInClassList("machine-inspector--inactive", hasInactiveBlocker);
            selectedContent.EnableInClassList("machine-inspector--blocked", !isProducing && !hasInactiveBlocker);

            var recipe = ResolveRecipe(inspection);
            processLabel.text = recipe == null
                ? $"Process: {(inspection.RecipeId.HasValue ? inspection.RecipeId.Value.Value : "None")}"
                : $"Process: {recipe.Id.Value}";
            progressLabel.text = FormatProgress(inspection.ProgressTicks, inspection.DurationTicks);

            var buildingDefinition = ResolveBuilding(inspection);
            selectedInputResource = RenderInputBuffer(inspection, buildingDefinition, recipe);
            selectedOutputResource = RenderOutputBuffer(inspection, buildingDefinition);
            blockersLabel.text = FormatBlockers(inspection.Blockers);
            toggleActiveButton.text = inspection.IsActive ? "Deactivate machine" : "Activate machine";
            SetSelectedActionsVisible(true);
            lastRenderedRevision = inspection.Revision;
            nextInspectionPoll = Time.unscaledTime + InspectionPollIntervalSeconds;
        }

        private ResourceId? RenderInputBuffer(
            MachineInspectionSnapshot inspection,
            BuildingDefinition building,
            RecipeDefinition recipe)
        {
            if (inspection.Inputs.Count == 0)
            {
                inputBufferLabel.text = "This machine's input buffer: none.";
                supplyInputButton.SetEnabled(false);
                return null;
            }

            var buffer = building?.InputBuffer;
            var lines = inspection.Inputs.Select(input =>
            {
                var capacity = buffer != null && buffer.Capacities.TryGetValue(input.ResourceId, out var max)
                    ? max
                    : 0L;
                var required = recipe?.Inputs.FirstOrDefault(amount => amount.ResourceId == input.ResourceId)?.Amount ?? 0L;
                return $"{ResourceLabel(input.ResourceId)}: {input.Quantity} / {capacity} stored · recipe needs {required}";
            });
            inputBufferLabel.text = "This machine's input buffer — " + string.Join("\n", lines);
            supplyInputButton.SetEnabled(true);
            return inspection.Inputs[0].ResourceId;
        }

        private ResourceId? RenderOutputBuffer(MachineInspectionSnapshot inspection, BuildingDefinition building)
        {
            if (inspection.Outputs.Count == 0)
            {
                outputBufferLabel.text = "This machine's output buffer: none.";
                removeOutputButton.SetEnabled(false);
                return null;
            }

            var buffer = building?.OutputBuffer;
            var lines = inspection.Outputs.Select(output =>
            {
                var capacity = buffer != null && buffer.Capacities.TryGetValue(output.ResourceId, out var max)
                    ? max
                    : checked(output.Quantity + output.RequiredOrFreeCapacity);
                return $"{ResourceLabel(output.ResourceId)}: {output.Quantity} / {capacity} stored · {output.RequiredOrFreeCapacity} free";
            });
            outputBufferLabel.text = "This machine's output buffer — " + string.Join("\n", lines);
            removeOutputButton.SetEnabled(true);
            return inspection.Outputs[0].ResourceId;
        }

        private string FormatBlockers(System.Collections.Generic.IReadOnlyList<ProductionBlockerSnapshot> blockers)
        {
            if (blockers.Count == 0)
            {
                return "Blockers: none. Ready to produce.";
            }

            var descriptions = blockers.Select(blocker =>
            {
                switch (blocker.Kind)
                {
                    case ProductionBlockerKind.Inactive:
                        return "Inactive: this machine is switched off.";
                    case ProductionBlockerKind.MissingInput:
                        return $"Missing input {ResourceLabel(blocker.ResourceId)}: need {blocker.Required}, have {blocker.Available}.";
                    case ProductionBlockerKind.InsufficientOutputCapacity:
                        return $"Output full {ResourceLabel(blocker.ResourceId)}: need {blocker.Required} free, have {blocker.Available}.";
                    default:
                        return $"Blocked: {blocker.Kind}.";
                }
            });

            return "Blockers:\n• " + string.Join("\n• ", descriptions);
        }

        private string ResourceLabel(ResourceId resourceId)
        {
            return definitions != null && definitions.Resources.TryGetValue(resourceId, out var resource)
                ? resource.DevelopmentLabel
                : resourceId.Value;
        }

        private string ResourceLabel(ResourceId? resourceId)
        {
            return resourceId.HasValue ? ResourceLabel(resourceId.Value) : "unknown resource";
        }

        private BuildingDefinition ResolveBuilding(MachineInspectionSnapshot inspection)
        {
            return definitions != null && definitions.Buildings.TryGetValue(inspection.DefinitionId, out var building)
                ? building
                : null;
        }

        private RecipeDefinition ResolveRecipe(MachineInspectionSnapshot inspection)
        {
            return definitions != null && inspection.RecipeId.HasValue &&
                   definitions.Recipes.TryGetValue(inspection.RecipeId.Value, out var recipe)
                ? recipe
                : null;
        }

        private void HandleCommandResult(bool succeeded, string message, string successMessage)
        {
            if (succeeded)
            {
                commandFeedbackLabel.text = successMessage;
                commandFeedbackLabel.EnableInClassList("machine-inspector__feedback--error", false);
                factoryView.Refresh();
                RefreshSelectedInspection();
                RefreshRuntimeHostState();
                return;
            }

            commandFeedbackLabel.text = "Not applied. " + (string.IsNullOrWhiteSpace(message) ? "No change was made." : message);
            commandFeedbackLabel.EnableInClassList("machine-inspector__feedback--error", true);
        }

        private void ClearCommandFeedback()
        {
            if (commandFeedbackLabel == null)
            {
                return;
            }

            commandFeedbackLabel.text = "No recent command for this machine.";
            commandFeedbackLabel.EnableInClassList("machine-inspector__feedback--error", false);
        }

        private void ShowEmptySelection(string message = null)
        {
            if (!IsUiReady())
            {
                return;
            }

            selectedContent.style.display = DisplayStyle.None;
            emptyLabel.style.display = DisplayStyle.Flex;
            emptyLabel.text = message ?? "No machine selected. Tap or focus a machine to inspect it.";
            SetSelectedActionsVisible(false);
            selectedInputResource = null;
            selectedOutputResource = null;
            renderedInstanceId = null;
            ClearCommandFeedback();
            lastRenderedRevision = CurrentRevision();
        }

        private long CurrentRevision()
        {
            var snapshot = bootstrap != null ? bootstrap.Snapshot : null;
            return snapshot != null ? snapshot.Revision : -1L;
        }

        private void RefreshRuntimeHostState()
        {
            if (!configured || runtimeHost == null || bootstrap.Snapshot == null)
            {
                return;
            }

            if (!CanMutate())
            {
                if (runtimeHost.enabled)
                {
                    runtimeHost.enabled = false;
                }

                return;
            }

            var anyMachineCanProduce = bootstrap.Snapshot.Buildings.Any(building =>
            {
                var result = commands.GetMachineInspection(building.InstanceId);
                return result.Succeeded && result.Value.IsActive && result.Value.RecipeId.HasValue &&
                       result.Value.Blockers.Count == 0;
            });

            // Avoid no-op revisions while every machine is stopped or blocked. The host still owns all
            // elapsed-time advancement as soon as at least one machine is eligible to make progress.
            if (runtimeHost.enabled != anyMachineCanProduce)
            {
                runtimeHost.enabled = anyMachineCanProduce;
            }
        }

        private void SetSelectedActionsVisible(bool visible)
        {
            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            var canMutate = visible && CanMutate();
            supplyInputButton.style.display = display;
            removeOutputButton.style.display = display;
            toggleActiveButton.style.display = display;
            supplyInputButton.SetEnabled(canMutate && selectedInputResource.HasValue);
            removeOutputButton.SetEnabled(canMutate && selectedOutputResource.HasValue);
            toggleActiveButton.SetEnabled(canMutate);
        }

        private bool CanMutate() => bootstrap != null && bootstrap.Session != null &&
            bootstrap.Session.LifecycleState == LifecycleState.Running;

        private void Fail(string message)
        {
            Error = message;
            if (root == null && uiDocument != null)
            {
                root = uiDocument.rootVisualElement.Q<VisualElement>("factory-hud");
            }

            var feedback = root?.Q<Label>("inspector-feedback");
            if (feedback != null)
            {
                feedback.text = message;
                feedback.EnableInClassList("machine-inspector__feedback--error", true);
            }

            var inspector = root?.Q<VisualElement>("inspector-selected");
            if (inspector != null)
            {
                inspector.SetEnabled(false);
            }
        }

        private bool IsUiReady() => root != null && selectedContent != null && emptyLabel != null &&
            machineLabel != null && stateLabel != null && processLabel != null && progressLabel != null &&
            inputBufferLabel != null && outputBufferLabel != null && blockersLabel != null &&
            commandFeedbackLabel != null && supplyInputButton != null && removeOutputButton != null &&
            toggleActiveButton != null;

        private static string FormatProgress(long progressTicks, long durationTicks)
        {
            if (durationTicks <= 0)
            {
                return "Progress: not applicable.";
            }

            var elapsedSeconds = progressTicks / (double)CoreTicksPerSecond;
            var durationSeconds = durationTicks / (double)CoreTicksPerSecond;
            var progressPercent = Math.Min(100d, progressTicks * 100d / durationTicks);
            return string.Format(
                CultureInfo.InvariantCulture,
                "Progress: {0:0.00} / {1:0.00} s ({2:0}%).",
                elapsedSeconds,
                durationSeconds,
                progressPercent);
        }
    }
}
