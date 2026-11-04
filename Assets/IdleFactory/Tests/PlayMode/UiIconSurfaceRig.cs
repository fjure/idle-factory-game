#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.ContractsPanelTestSupport;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>The Feature 003 surfaces that carry icons and live in the shell (the standalone Offline and Start-up surfaces have their own rigs).</summary>
    public enum UiIconSurface
    {
        Hud,
        BuildSheet,
        MachineSheet,
        Contracts,
        Items,
        Research,
        Market,
        Prestige,
    }

    /// <summary>
    /// T441: a shell with every real Feature 003 panel registered, built over a run with everything unlocked, so one rig shows each surface with the
    /// restyled icons (the same panels and sprite resolver the layout suites use). A surface is opened by <see cref="Show"/>.
    /// </summary>
    public sealed class UiIconSurfaceRig : IDisposable
    {
        private const string PanelRoot = "Assets/IdleFactory/Unity/UI/Panels/";

        private UiIconSurfaceRig()
        {
        }

        public ShellRig Rig { get; private set; }
        public RunSession Session { get; private set; }
        public RunCommands Commands { get; private set; }
        public ResearchPanel Research { get; private set; }
        public ContractsPanel ContractsPanel { get; private set; }
        public IndustryPanel IndustryPanel { get; private set; }

        /// <summary>True once the timed Bar contract was accepted by <see cref="Show"/> (the Contracts surface then shows its countdown).</summary>
        public bool TimedContractAccepted { get; private set; }

        public static IEnumerable<UiIconSurface> Surfaces() => (UiIconSurface[])Enum.GetValues(typeof(UiIconSurface));

        /// <summary>The screen classes of the task: the eight shell classes and the 640x480 Game view of the scene suites.</summary>
        public static IEnumerable<ShellScreen> ScreenClasses()
        {
            foreach (var screen in ShellScreens.All())
            {
                yield return screen;
            }

            yield return new ShellScreen("scene 640x480", 640, 480, 0, 0, 0, 0, FactoryHudDocument.Classify(640, 480));
        }

        public static UiIconSurfaceRig Create(ShellScreen screen, int percent, bool authored = false, ShellDeviceProfile? profile = null)
        {
            var result = new UiIconSurfaceRig();
            result.Session = RunShellFixtures.NewSession(out var commands, factoryLevel: 2, cash: RunShellFixtures.ComfortableCash, unlockEverything: true);
            result.Commands = commands;
            result.Rig = authored ? ShellRig.CreateAuthored(screen, profile.Value, percent) : ShellRig.Create(screen, profile, percent);
            var icons = (Func<string, Sprite>)UiIconSprites.Load;
            var shell = result.Rig.Shell;

            shell.Panels.Register(new BuildSheetPresenter(result.Session, commands, Tree("BuildSheetPanel"), Style("BuildSheetPanel"), icons));
            shell.Panels.Register(new MachinePanelPresenter(result.Session, commands, Tree("MachinePanel"), Style("MachinePanel"), icons));
            UnlockContracts(result.Session);
            result.ContractsPanel = new ContractsPanel(result.Session, commands, Style("ContractsPanel"), icons);
            shell.Panels.Register(result.ContractsPanel);
            shell.Panels.Register(new ItemsPanel(result.Session, commands, Style("ItemsPanel"), icons));
            result.IndustryPanel = new IndustryPanel(result.Session, commands, Style("IndustryPanel"), icons);
            result.Research = new ResearchPanel(result.Session, commands, Style("ResearchPanel"), icons, result.IndustryPanel);
            shell.Panels.Register(result.Research);
            var market = new MarketPanelHost(result.Session, commands, shell, Tree("InventoryRatesPanel"), Style("InventoryRatesPanel"),
                Tree("PrioritiesPanel"), Style("PrioritiesPanel"), Tree("ExpansionPanel"), Style("ExpansionPanel"), icons);
            shell.Panels.Register(market);
            shell.Panels.Register(new PrestigePresenter(result.Session, commands, Tree("PrestigePanel"), Style("PrestigePanel"), icons));
            return result;
        }

        private static VisualTreeAsset Tree(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + name + ".uxml");
            Assert.That(asset, Is.Not.Null, name + ".uxml");
            return asset;
        }

        private static StyleSheet Style(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + name + ".uss");
            Assert.That(asset, Is.Not.Null, name + ".uss");
            return asset;
        }

        /// <summary>Opens a surface and lets it settle. A machine and a timed contract exist so the sheets show their icons.</summary>
        public IEnumerator Show(UiIconSurface surface)
        {
            var shell = Rig.Shell;
            switch (surface)
            {
                case UiIconSurface.Hud:
                    shell.SelectTab(ShellTab.Factory);
                    break;
                case UiIconSurface.BuildSheet:
                    Assert.That(shell.OpenSlot(1, SlotCondition.Empty), Is.True);
                    break;
                case UiIconSurface.MachineSheet:
                    if (!RunSnapshots.Factory(Session).Machines.Any(machine => machine.SlotIndex == 0))
                    {
                        Assert.That(Commands.Build(0, "machine.miner").IsSuccess, Is.True);
                    }

                    Assert.That(shell.OpenSlot(0, SlotCondition.Occupied), Is.True);
                    break;
                case UiIconSurface.Contracts:
                    // The timed Bar contract needs Iron Bar stock for nothing: it is accepted so the active card shows its countdown.
                    TimedContractAccepted |= Commands.AcceptContract(TimedBar).IsSuccess;
                    ContractsPanel.Invalidate();
                    shell.SelectTab(ShellTab.Contracts);
                    break;
                case UiIconSurface.Items:
                    shell.SelectTab(ShellTab.Items);
                    break;
                case UiIconSurface.Research:
                    shell.SelectTab(ShellTab.Research);
                    break;
                case UiIconSurface.Market:
                    shell.SelectTab(ShellTab.Market);
                    break;
                case UiIconSurface.Prestige:
                    shell.SelectTab(ShellTab.Prestige);
                    break;
            }

            yield return ShellRig.Settle();
            yield return ShellRig.Settle();
        }

        public void Dispose() => Rig?.Dispose();
    }
}
#endif
