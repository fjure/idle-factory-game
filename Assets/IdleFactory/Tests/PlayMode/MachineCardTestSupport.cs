using System;
using System.Collections;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>A world effect sink that records what the card asked for (T529's pool is the production sink).</summary>
    internal sealed class RecordingEffects : IMachineEffects
    {
        public List<(MachineResponseKind Kind, int Location)> Spawned { get; } = new List<(MachineResponseKind, int)>();

        public void Spawn(MachineResponseKind kind, int location) => Spawned.Add((kind, location));
    }

    /// <summary>
    /// The shell, a live run session, the docked machine card and its Details view at one screen class and text scale (T534). The card and the
    /// Details panel are built from the authored assets exactly as the bootstrap does, and registered in the shell.
    /// </summary>
    internal sealed class CardHarness : IDisposable
    {
        private const string UiRoot = "Assets/IdleFactory/Unity/UI/";

        public ShellRig Rig { get; private set; }

        public RunSession Session { get; private set; }

        public RunCommands Commands { get; private set; }

        public MachineCard Card { get; private set; }

        public MachinePanelPresenter Details { get; private set; }

        public MachineFeedback Feedback { get; private set; }

        public RecordingEffects Effects { get; private set; }

        public bool Reduced { get; set; }

        public RunShellDocument Shell => Rig.Shell;

        /// <summary>Builds the harness. <paramref name="reduced"/> is read live through <see cref="Reduced"/>.</summary>
        public static IEnumerator Create(
            ShellScreen screen, int percent, long cash, Action<CardHarness> result, bool reduced = false, bool withEffects = false, int factoryLevel = 1,
            bool unlockEverything = false)
        {
            var harness = new CardHarness { Reduced = reduced };
            harness.Session = RunShellFixtures.NewSession(out var commands, factoryLevel, cash, unlockEverything);
            harness.Commands = commands;
            harness.Rig = ShellRig.Create(screen, null, percent);
            harness.Effects = withEffects ? new RecordingEffects() : null;
            harness.Feedback = new MachineFeedback(() => harness.Reduced, harness.Effects);
            var detailsTree = Load<VisualTreeAsset>("Panels/MachinePanel.uxml");
            var detailsStyles = Load<StyleSheet>("Panels/MachinePanel.uss");
            harness.Details = new MachinePanelPresenter(
                harness.Session, commands, detailsTree, detailsStyles, ContractsPanelTestSupport.Icon, harness.Feedback);
            harness.Card = new MachineCard(
                harness.Session, commands, Load<VisualTreeAsset>("MachineCard.uxml"), Load<StyleSheet>("MachineCard.uss"),
                Load<StyleSheet>("PanelScaffold.uss"), ContractsPanelTestSupport.Icon, harness.Feedback);
            harness.Shell.Panels.Register(harness.Details);
            harness.Shell.Panels.Register(harness.Card);
            harness.Shell.Panels.Register(new TestShellPanel(ShellPanelIds.Build, "Build", 4));
            harness.Shell.Panels.Register(new TestShellPanel(ShellPanelIds.Market, "Market", 6));
            yield return harness.Rig.Layout();
            result(harness);
        }

        private static T Load<T>(string relative) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(UiRoot + relative);
            Assert.That(asset, Is.Not.Null, UiRoot + relative);
            return asset;
        }

        public Guid Build(int slot, string machineId)
        {
            var built = Commands.Build(slot, machineId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            return built.Value.InstanceId;
        }

        /// <summary>Opens the card of an occupied slot the way a tap does and lets the layout settle.</summary>
        public IEnumerator OpenCard(int slot)
        {
            Assert.That(Shell.OpenSlot(slot, SlotCondition.Occupied), Is.True);
            Assert.That(Shell.IsCardOpen, Is.True, "an occupied slot opens the docked card");
            yield return ShellRig.Settle();
            Shell.RefreshCard();
        }

        public MachineSnapshot MachineAt(int slot)
        {
            foreach (var machine in RunSnapshots.Factory(Session).Machines)
            {
                if (machine.SlotIndex == slot)
                {
                    return machine;
                }
            }

            return null;
        }

        public static void Send(VisualElement target, EventBase evt)
        {
            using (evt)
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        public static void Press(VisualElement control) => Send(control, NavigationSubmitEvent.GetPooled());

        public static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        public void Dispose() => Rig?.Dispose();
    }
}
#endif
