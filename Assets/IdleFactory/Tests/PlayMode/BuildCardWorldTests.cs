using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Tests.PlayMode.F006;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

#if UNITY_EDITOR
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T539 (FR-033): the world side. The real slot grid draws the ghost (frame 0, 60 % alpha) and the highlight of the available locations from the real F004 art,
    /// and the whole flow runs through the run presenter exactly as a tap does.
    /// </summary>
    public sealed class BuildCardWorldTests
    {
        private BaselineRig rig;
        private BuildCard card;
        private RecordingEffects effects;
        private readonly List<IDisposable> registrations = new List<IDisposable>();

        [TearDown]
        public void TearDown()
        {
            foreach (var registration in registrations)
            {
                registration.Dispose();
            }

            registrations.Clear();
            rig?.Dispose();
            rig = null;
            card = null;
            effects = null;
        }

        private IEnumerator Make()
        {
            var screen = ShellScreens.All().First(item => item.Name.StartsWith("phone-portrait 390", StringComparison.Ordinal));
            yield return BaselineRig.Create(screen, 100, null, made => rig = made);
            var shell = rig.ShellDocument;
            effects = new RecordingEffects();
            var feedback = new MachineFeedback(() => false, effects);
            const string root = "Assets/IdleFactory/Unity/UI/";
            var scaffold = AssetDatabase.LoadAssetAtPath<StyleSheet>(root + "PanelScaffold.uss");
            var catalog = rig.Session.Catalog;
            Func<string, string> keyOf = id => catalog.Machines.TryGetValue(id, out var machine) ? machine.PresentationKey : string.Empty;
            card = new BuildCard(rig.Session, rig.Commands, AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(root + "Panels/BuildCard.uxml"),
                AssetDatabase.LoadAssetAtPath<StyleSheet>(root + "Panels/BuildCard.uss"), scaffold, ContractsPanelTestSupport.Icon, feedback, () => rig.Grid, keyOf);
            registrations.Add(shell.Panels.Register(card));
            registrations.Add(shell.Panels.Register(new MachineCard(rig.Session, rig.Commands,
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(root + "MachineCard.uxml"), AssetDatabase.LoadAssetAtPath<StyleSheet>(root + "MachineCard.uss"),
                scaffold, ContractsPanelTestSupport.Icon, feedback, () => rig.Grid.Layout)));
            yield return rig.Settle();
        }

        // C-44 (T560, FR-032): while the card is open an occupied or locked location is muted, an explicit mark that does not rely on one frame's alpha; closing restores it.
        [UnityTest]
        public IEnumerator OccupiedAndLockedLocationsAreMutedWhileTheBuildCardIsOpen()
        {
            yield return Make();
            var grid = rig.Grid;
            var slot = BaselineFixture.EmptySlot;
            Assert.That(grid.IsMutedForBuild(0), Is.False, "nothing is muted without a build preview");
            Assert.That(rig.Presenter.OpenSlot(slot), Is.True);
            yield return rig.Settle();

            Assert.That(grid.BuildPreviewActive, Is.True);
            Assert.That(grid.IsMutedForBuild(0), Is.True, "an occupied location is muted");
            Assert.That(grid.RendererOf(0, SlotGridView.SlotRendererRole.Plate).color, Is.EqualTo(SlotRenderers.MutedTint), "its plate is dimmed");
            Assert.That(grid.RendererOf(0, SlotGridView.SlotRendererRole.Machine).color.r, Is.EqualTo(SlotRenderers.MutedTint.r).Within(1e-4f), "and its machine");
            Assert.That(grid.IsMutedForBuild(slot), Is.False, "the selected free location is not muted");
            Assert.That(grid.RendererOf(slot, SlotGridView.SlotRendererRole.Plate).color, Is.EqualTo(Color.white));

            Assert.That(rig.ShellDocument.HandleBack(), Is.True);
            yield return rig.Settle();
            Assert.That(grid.BuildPreviewActive, Is.False);
            Assert.That(grid.IsMutedForBuild(0), Is.False);
            Assert.That(grid.RendererOf(0, SlotGridView.SlotRendererRole.Plate).color, Is.EqualTo(Color.white), "closing the card restores the tint");
        }

        [UnityTest]
        public IEnumerator TheGridDrawsTheGhostAtSixtyPercentAndOutlinesTheAvailableLocations()
        {
            yield return Make();
            var slot = BaselineFixture.EmptySlot;
            var grid = rig.Grid;
            Assert.That(rig.Presenter.OpenSlot(slot), Is.True);
            yield return rig.Settle();

            Assert.That(rig.ShellDocument.IsBuildCardOpen, Is.True);
            Assert.That(grid.PreviewSlot, Is.EqualTo(slot));
            var machine = grid.RendererOf(slot, SlotGridView.SlotRendererRole.Machine);
            Assert.That(machine.enabled, Is.True, "the ghost is drawn at the empty location");
            Assert.That(machine.color.a, Is.EqualTo(SlotGridView.PreviewAlpha).Within(1e-4f), "at 60 % alpha");
            Assert.That(SlotGridView.PreviewAlpha, Is.EqualTo(0.6f));
            Assert.That(BaselineArt.Sprites.TryGet(grid.PreviewKey, out var frameZero), Is.True);
            Assert.That(machine.sprite, Is.SameAs(frameZero), "frame 0 of the selected type");
            Assert.That(grid.RendererOf(slot, SlotGridView.SlotRendererRole.Accent).enabled, Is.False, "the ghost never animates and has no running accent");

            // The preview location is not dimmed: its frame is the full selection frame. The other free locations are outlined at reduced alpha.
            Assert.That(grid.RendererOf(slot, SlotGridView.SlotRendererRole.Frame).enabled, Is.True);
            Assert.That(grid.RendererOf(slot, SlotGridView.SlotRendererRole.Frame).color.a, Is.EqualTo(1f).Within(1e-4f), "the preview location stays 100 % visible");
            for (var free = BaselineFixture.EmptySlot; free < SlotGridLayout.SlotCount; free++)
            {
                Assert.That(grid.IsAvailable(free), Is.True, "free location " + free);
                Assert.That(grid.RendererOf(free, SlotGridView.SlotRendererRole.Frame).enabled, Is.True, "outlined " + free);
                if (free != slot)
                {
                    Assert.That(grid.RendererOf(free, SlotGridView.SlotRendererRole.Frame).color.a, Is.EqualTo(SlotGridView.HighlightAlpha).Within(1e-4f));
                    Assert.That(grid.RendererOf(free, SlotGridView.SlotRendererRole.Machine).enabled, Is.False, "only the previewed location shows a ghost");
                }
            }

            Assert.That(grid.IsAvailable(0), Is.False, "an occupied location is not available");
            Assert.That(grid.RendererOf(0, SlotGridView.SlotRendererRole.Machine).color.a, Is.EqualTo(1f).Within(1e-4f), "a real machine is never drawn as a ghost");

            // A new render of the world keeps the preview.
            rig.Presenter.Invalidate();
            rig.Presenter.Refresh();
            Assert.That(grid.RendererOf(slot, SlotGridView.SlotRendererRole.Machine).color.a, Is.EqualTo(SlotGridView.PreviewAlpha).Within(1e-4f));

            // Back clears both.
            Assert.That(rig.ShellDocument.HandleBack(), Is.True);
            yield return rig.Settle();
            Assert.That(grid.PreviewSlot, Is.EqualTo(-1));
            Assert.That(grid.RendererOf(slot, SlotGridView.SlotRendererRole.Machine).enabled, Is.False);
            Assert.That(grid.IsAvailable(slot), Is.False);
            Assert.That(grid.RendererOf(slot + 1, SlotGridView.SlotRendererRole.Frame).enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator ConfirmTurnsTheGhostIntoTheRealMachineAndHandsOverToTheMachineCard()
        {
            yield return Make();
            var slot = BaselineFixture.EmptySlot;
            Assert.That(rig.Presenter.OpenSlot(slot), Is.True);
            yield return rig.Settle();
            var before = RunSnapshots.Factory(rig.Session).Machines.Count;

            CardHarness.Press(card.ConfirmButton);
            yield return rig.Settle();

            Assert.That(RunSnapshots.Factory(rig.Session).Machines.Count, Is.EqualTo(before + 1));
            Assert.That(effects.Spawned, Is.EqualTo(new[] { (MachineResponseKind.BuildDust, slot) }), "the dust plays at the built location");
            Assert.That(rig.ShellDocument.IsCardOpen, Is.True, "the machine card of the new machine");
            Assert.That(rig.ShellDocument.CardSlot, Is.EqualTo(slot));
            var machine = rig.Grid.RendererOf(slot, SlotGridView.SlotRendererRole.Machine);
            Assert.That(machine.enabled, Is.True, "the foundation: the real machine stands there");
            Assert.That(machine.color.a, Is.EqualTo(1f).Within(1e-4f), "no longer a ghost");
            Assert.That(rig.Grid.PreviewSlot, Is.EqualTo(-1));
            Assert.That(rig.Grid.IsAvailable(slot + 1), Is.False, "the highlight is gone");
        }
    }
}
#endif
