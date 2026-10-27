#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>A clock a Play Mode test moves by hand; <see cref="Fail"/> makes it throw like an unavailable device clock.</summary>
    internal sealed class StatusTestClock : IUtcClock
    {
        public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        public bool Fail { get; set; }

        public DateTimeOffset GetUtcNow()
        {
            if (Fail)
            {
                throw new InvalidOperationException("The device clock is unavailable.");
            }

            return Now;
        }
    }

    /// <summary>A store that forwards to the real one and can fail every read like unavailable storage (a transient failure of the cold start).</summary>
    internal sealed class FlakyRunStore : IRunCheckpointStore
    {
        private readonly IRunCheckpointStore inner;

        public FlakyRunStore(IRunCheckpointStore inner)
        {
            this.inner = inner;
        }

        public bool FailReads { get; set; }

        private RunCheckpointReadResult Gate(Func<RunCheckpointReadResult> read) =>
            FailReads ? RunCheckpointReadResult.Rejected(CommandOutcome.PersistenceFailed, "Checkpoint storage is unavailable.") : read();

        public RunCheckpointReadResult ReadActive() => Gate(inner.ReadActive);
        public RunCheckpointReadResult ReadPrevious() => Gate(inner.ReadPrevious);
        public RunCheckpointReadResult Read(CheckpointId checkpointId) => Gate(() => inner.Read(checkpointId));
        public CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint) => inner.Promote(checkpoint);
        public CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint recoveryCandidate) => inner.RecoverPrevious(recoveryCandidate);
        public CommandResult<long> NextSupersedeGeneration() => inner.NextSupersedeGeneration();
        public CommandResult<CheckpointId> SupersedeDamaged(RunCheckpoint checkpoint) => inner.SupersedeDamaged(checkpoint);
        public CommandResult<RunCheckpointPresence> ProbeFiles() => inner.ProbeFiles();
    }

    /// <summary>
    /// A real run persistence over a temporary checkpoint root with the real Iron content and a hand-moved clock, so the status surfaces
    /// are driven only through the application seams (T118): <c>ColdStart</c>, <c>ResumeAndReconcile</c>, <c>RetryReturn</c>,
    /// <c>StartNewRunAfterBlockedImport</c>, <c>RecoverPreviousCheckpoint</c> and <c>DiscardDamagedSaveAndRestart</c>.
    /// </summary>
    internal sealed class StatusPersistenceRig
    {
        private StatusPersistenceRig()
        {
        }

        public string Root { get; private set; }
        public StatusTestClock Clock { get; private set; }
        public RunSession Session { get; private set; }
        public RunCommands Commands { get; private set; }
        public RunPersistence Persistence { get; private set; }
        public ILegacyCheckpointSource Legacy { get; private set; }
        public RunCatalog Catalog => Session.Catalog;

        /// <summary>The store the persistence reads, which can fail its reads.</summary>
        public FlakyRunStore Store { get; private set; }

        /// <summary>A session at the same instant as the clock; the persistence has not started yet (it is Bootstrapping).</summary>
        public static StatusPersistenceRig Create(string root, StatusTestClock clock)
        {
            var rig = new StatusPersistenceRig { Root = root, Clock = clock };
            rig.Session = RunShellFixtures.NewSession(out var commands, factoryLevel: 1, cash: 500, unlockEverything: true);
            rig.Commands = commands;
            rig.Store = new FlakyRunStore(new JsonRunCheckpointStore(root, rig.Session.Catalog));
            rig.Persistence = new RunPersistence(rig.Session, rig.Store, clock);
            var legacyCatalog = GameplayContentLoader.Load(RunSceneRun.DevelopmentCatalogBytes()).Catalog;
            rig.Legacy = new SaveV1CheckpointSource(root, legacyCatalog);
            return rig;
        }

        /// <summary>Starts the run: a fresh run when no checkpoint exists.</summary>
        public StatusPersistenceRig Start()
        {
            var started = Persistence.ColdStart(Legacy);
            Assert.That(started.IsSuccess, Is.True, started.ReasonCode);
            Assert.That(Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            return this;
        }

        public RunCommandResult<RunPersistenceOutcome> Away(TimeSpan absence, bool clockFailsOnReturn = false)
        {
            Assert.That(Persistence.RecordSuspension().IsSuccess, Is.True);
            Clock.Now += absence;
            Clock.Fail = clockFailsOnReturn;
            return Persistence.ResumeAndReconcile();
        }

        public RunCommandResult<RunPersistenceOutcome> Reconcile() => Persistence.ResumeAndReconcile();

        public Guid BuildMiner(int slot = 0)
        {
            var built = Commands.Build(slot, "machine.miner");
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            return built.Value.InstanceId;
        }
    }

    /// <summary>Shared fixtures and layout checks of the T118 surface tests (the offline summary and the start-up status).</summary>
    internal static class OfflineSummaryPanelTestSupport
    {
        private const string PanelRoot = "Assets/IdleFactory/Unity/UI/Panels/";

        public static StyleSheet Styles(string fileName) => AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + fileName);

        public static VisualElement Focused(VisualElement any) => any.focusController?.focusedElement as VisualElement;

        /// <summary>Enter or Space on a focused Button reaches it as the input system's Submit navigation event.</summary>
        public static void Submit(VisualElement target)
        {
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        /// <summary>A key press on the focused element (Escape and Android Back are the same key).</summary>
        public static void Key(VisualElement target, KeyCode key, EventModifiers modifiers = EventModifiers.None)
        {
            using (var evt = KeyDownEvent.GetPooled('\0', key, modifiers))
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        public static IEnumerator Frames(int count = 3)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        public static string TempRoot()
        {
            var root = RunSceneRun.NewRoot();
            return root;
        }

        // ---------------------------------------------------------------------------------------------------
        // Offline summaries (their setters are internal to Core, so the tests fill them by reflection)
        // ---------------------------------------------------------------------------------------------------

        public static RunOfflineSummary NewSummary(params (string Property, object Value)[] values)
        {
            var summary = (RunOfflineSummary)Activator.CreateInstance(typeof(RunOfflineSummary), true);
            var policy = (Outcome: RunOfflineOutcome.Applied, Cap: 8L * 3_600_000L * RunSession.TicksPerMs);
            Set(summary, nameof(RunOfflineSummary.Outcome), policy.Outcome);
            Set(summary, nameof(RunOfflineSummary.CapTicks), policy.Cap);
            Set(summary, nameof(RunOfflineSummary.EfficiencyNumerator), 4L);
            Set(summary, nameof(RunOfflineSummary.EfficiencyDenominator), 5L);
            foreach (var (property, value) in values)
            {
                Set(summary, property, value);
            }

            return summary;
        }

        private static void Set(RunOfflineSummary summary, string property, object value)
        {
            var info = typeof(RunOfflineSummary).GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, property);
            info.GetSetMethod(true).Invoke(summary, new[] { value });
        }

        /// <summary>A 45 second absence: nothing was made, sold or changed.</summary>
        public static RunOfflineSummary ShortAbsence() => NewSummary(
            (nameof(RunOfflineSummary.MeasuredMs), 45_000L),
            (nameof(RunOfflineSummary.CreditedTicks), 45_000L * RunSession.TicksPerMs),
            (nameof(RunOfflineSummary.EffectiveTicks), 36_000L * RunSession.TicksPerMs),
            (nameof(RunOfflineSummary.CashBefore), 25L),
            (nameof(RunOfflineSummary.CashAfter), 25L));

        /// <summary>A normal return with every section populated by moderate numbers.</summary>
        public static RunOfflineSummary NormalReturn() => NewSummary(
            (nameof(RunOfflineSummary.MeasuredMs), 9L * 3_600_000L + 30L * 60_000L),
            (nameof(RunOfflineSummary.CreditedTicks), 8L * 3_600_000L * RunSession.TicksPerMs),
            (nameof(RunOfflineSummary.EffectiveTicks), (6L * 3_600_000L + 24L * 60_000L) * RunSession.TicksPerMs),
            (nameof(RunOfflineSummary.CapReached), true),
            (nameof(RunOfflineSummary.CashBefore), 100L),
            (nameof(RunOfflineSummary.CashAfter), 1_380L),
            (nameof(RunOfflineSummary.SalesIncome), 1_280L),
            (nameof(RunOfflineSummary.StockDeltas), new[] { new RunResourceDelta("iron.ore", 12, 52), new RunResourceDelta("iron.bar", 40, 10) }),
            (nameof(RunOfflineSummary.ProducedUnits), new[] { new RunResourceUnits("iron.ore", 400) }),
            (nameof(RunOfflineSummary.ConsumedUnits), new[] { new RunResourceUnits("iron.ore", 360) }),
            (nameof(RunOfflineSummary.SoldUnits), new[] { new RunResourceUnits("iron.bar", 128) }),
            (nameof(RunOfflineSummary.Completions), 400L),
            (nameof(RunOfflineSummary.PartialBatchCompletions), 3L),
            (nameof(RunOfflineSummary.ExpiredEffects), new[] { new RunExpiredEffect("item.flux-catalyst", "speed", "factory", 1_000L) }),
            (nameof(RunOfflineSummary.OverdriveActiveEnded), true),
            (nameof(RunOfflineSummary.Contract), new RunOfflineContractOutcome(RunOfflineContractKind.Expired, "contract.timed-bar", 100L, 5_000L)),
            (nameof(RunOfflineSummary.BlockedMachinesAtReturn), new[] { new RunOfflineBlockedMachine(Guid.NewGuid(), 1, "machine.smelter", MachineBlocker.MissingInput) }));

        /// <summary>The worst case: every list full, every number at the bound and every effect and limit present.</summary>
        public static RunOfflineSummary WorstCase(RunCatalog catalog)
        {
            const long Bound = 1_000_000_000_000_000_000L;
            var resources = catalog.Resources.Keys.ToArray();
            var machines = catalog.Machines.Keys.ToArray();
            var items = catalog.Items.Keys.ToArray();
            var effects = new List<RunExpiredEffect>();
            for (var i = 0; i < 8; i++)
            {
                effects.Add(new RunExpiredEffect(items[i % items.Length], "speed", i % 2 == 0 ? "factory" : machines[i % machines.Length], 1_000L + i));
            }

            var blocked = new List<RunOfflineBlockedMachine>();
            for (var i = 0; i < 8; i++)
            {
                blocked.Add(new RunOfflineBlockedMachine(Guid.NewGuid(), i, machines[i % machines.Length], i % 2 == 0 ? MachineBlocker.MissingInput : MachineBlocker.StorageFull));
            }

            return NewSummary(
                (nameof(RunOfflineSummary.MeasuredMs), 29L * 86_400_000L + 23L * 3_600_000L + 59L * 60_000L),
                (nameof(RunOfflineSummary.CreditedTicks), 8L * 3_600_000L * RunSession.TicksPerMs),
                (nameof(RunOfflineSummary.EffectiveTicks), (6L * 3_600_000L + 24L * 60_000L) * RunSession.TicksPerMs),
                (nameof(RunOfflineSummary.CapReached), true),
                (nameof(RunOfflineSummary.CashBefore), 1L),
                (nameof(RunOfflineSummary.CashAfter), Bound),
                (nameof(RunOfflineSummary.SalesIncome), Bound - 1),
                (nameof(RunOfflineSummary.StockDeltas), resources.Select(id => new RunResourceDelta(id, 1, Bound / 2)).ToArray()),
                (nameof(RunOfflineSummary.ProducedUnits), resources.Select(id => new RunResourceUnits(id, Bound / 3)).ToArray()),
                (nameof(RunOfflineSummary.ConsumedUnits), resources.Select(id => new RunResourceUnits(id, Bound / 4)).ToArray()),
                (nameof(RunOfflineSummary.SoldUnits), resources.Select(id => new RunResourceUnits(id, Bound / 5)).ToArray()),
                (nameof(RunOfflineSummary.Completions), Bound / 7),
                (nameof(RunOfflineSummary.PartialBatchCompletions), Bound / 9),
                (nameof(RunOfflineSummary.ExpiredEffects), effects.ToArray()),
                (nameof(RunOfflineSummary.OverdriveActiveEnded), true),
                (nameof(RunOfflineSummary.OverdriveCooldownEnded), true),
                (nameof(RunOfflineSummary.Contract), new RunOfflineContractOutcome(RunOfflineContractKind.Expired, catalog.ContractOffers.Keys.First(), Bound, 5_000L)),
                (nameof(RunOfflineSummary.NewUnlocks), resources.Concat(machines).ToArray()),
                (nameof(RunOfflineSummary.BlockedMachinesAtReturn), blocked.ToArray()));
        }

        // ---------------------------------------------------------------------------------------------------
        // A surface mounted outside the shell, as at a blocked start (there is no shell yet)
        // ---------------------------------------------------------------------------------------------------

        /// <summary>An off-screen UIDocument at a declared screen class with no run shell in it.</summary>
        public sealed class StandaloneRig : IDisposable
        {
            private readonly List<Object> owned = new List<Object>();

            public StandaloneRig(ShellScreen screen)
            {
                Screen = screen;
                Texture = new RenderTexture(screen.Width, screen.Height, 24);
                Texture.Create();
                owned.Add(Texture);
                var settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                settings.scale = 1f;
                settings.targetTexture = Texture;
                owned.Add(settings);
                var host = new GameObject("status-standalone-rig");
                owned.Add(host);
                Document = host.AddComponent<UIDocument>();
                Document.panelSettings = settings;
            }

            public ShellScreen Screen { get; private set; }
            public UIDocument Document { get; }
            public RenderTexture Texture { get; }
            public VisualElement Root => Document.rootVisualElement;

            /// <summary>Mounts a surface the way a blocked start does: into the UIDocument root, with its text scale and safe area.</summary>
            public IEnumerator Mount(StatusSurface surface, int percent)
            {
                yield return null;
                var root = Root;
                root.style.position = Position.Absolute;
                root.style.left = 0f;
                root.style.top = 0f;
                root.style.right = 0f;
                root.style.bottom = 0f;
                surface.Mount(root);
                surface.OverrideTextScalePercent(percent);
                Apply(surface, Screen);
                yield return Settle();
            }

            public void Apply(StatusSurface surface, ShellScreen screen)
            {
                Screen = screen;
                surface.ApplySafeInsets(screen.Left, screen.Top, screen.Right, screen.Bottom);
            }

            public static IEnumerator Settle()
            {
                yield return null;
                yield return null;
                yield return null;
            }

            public Rect SafeRect()
            {
                var bounds = Root.panel.visualTree.worldBound;
                return Rect.MinMaxRect(bounds.xMin + Screen.Left, bounds.yMin + Screen.Top, bounds.xMax - Screen.Right, bounds.yMax - Screen.Bottom);
            }

            public void Dispose()
            {
                foreach (var item in owned)
                {
                    if (item is RenderTexture texture)
                    {
                        RenderTexture.active = null;
                        texture.Release();
                    }

                    if (item != null)
                    {
                        Object.DestroyImmediate(item);
                    }
                }

                owned.Clear();
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout (UX-3, F-4)
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// The worst-case layout checks of an open surface at one screen class and text scale: the card stays in the safe area, its title, body
        /// and action row do not overlap, text is not squeezed or below the scaled minimum, targets are 44 pt, clipped body content stays clipped
        /// at the top, the middle and the end, the last body line is reachable, and every action takes focus with its ring.
        /// </summary>
        public static IEnumerator AssertSurfaceLayout(StatusSurface surface, Rect safe, ShellScreen screen, string state, int percent)
        {
            yield return Frames();
            var context = $"{screen} @{percent}% [{state}]";
            AssertWithin(safe, surface.Card.worldBound, context + ": the card stays inside the safe area");
            Assert.That(surface.Card.worldBound.height, Is.GreaterThan(0f), context + ": the card is collapsed");

            var scroll = surface.Scroll;
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            AssertWithin(surface.Card.worldBound, scroll.worldBound, context + ": the body stays in the card");
            AssertWithin(surface.Card.worldBound, surface.Actions.worldBound, context + ": the actions stay in the card");
            Assert.That(Intersects(scroll.worldBound, surface.Actions.worldBound), Is.False, context + ": the body overlaps the actions");
            Assert.That(Intersects(surface.TitleLabel.worldBound, surface.Actions.worldBound), Is.False, context + ": the title overlaps the actions");
            Assert.That(Intersects(surface.TitleLabel.worldBound, scroll.worldBound), Is.False, context + ": the title overlaps the body");

            AssertTextAndTargets(surface.Root, safe, context, MinimumFontSize * percent / 100f);

            var range = Mathf.Max(0f, scroll.contentContainer.layout.height - viewport.layout.height);
            foreach (var offset in new[] { 0f, 0.5f, 1f })
            {
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, range * offset);
                yield return Frames();
                AssertScrollContentClipped(scroll, $"{context} scrolled to {offset:P0}");
            }

            // The last line is reachable: scrolled to the end it lies inside the viewport.
            scroll.scrollOffset = new Vector2(0f, range);
            yield return Frames();
            var lastLine = surface.Content.Children().OfType<Label>().LastOrDefault(label => IsDisplayed(label) && !string.IsNullOrEmpty(label.text));
            if (lastLine != null)
            {
                AssertWithin(viewport.worldBound, lastLine.worldBound, context + ": the last body line is not reachable by scrolling");
            }

            var buttons = surface.Actions.Query<Button>().ToList().Where(button => IsDisplayed(button)).ToList();
            Assert.That(buttons.Count, Is.GreaterThan(0), context + ": the surface has actions");
            foreach (var first in buttons)
            {
                for (var i = 0; i < buttons.Count; i++)
                {
                    if (buttons[i] != first && buttons.IndexOf(first) < i)
                    {
                        Assert.That(Intersects(first.worldBound, buttons[i].worldBound), Is.False, $"{context}: '{first.name}' overlaps '{buttons[i].name}'");
                    }
                }
            }

            foreach (var button in new[] { buttons.First(), buttons[buttons.Count / 2], buttons.Last() })
            {
                button.Focus();
                yield return Frames();
                Assert.That(Focused(button), Is.SameAs(button), $"{context}: '{button.name}' did not take focus");
                AssertWithin(safe, button.worldBound, $"{context}: focused '{button.name}' leaves the safe area");
                Assert.That(button.ClassListContains("run-control--focused"), Is.True, $"{context}: '{button.name}' shows its focus ring");
            }
        }
    }
}
#endif
