using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
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
    /// The shell, the machine card, and the hub ("Office", T535) with a test panel behind every other registry id. The hub is built from the authored
    /// assets exactly as the bootstrap does.
    /// </summary>
    internal sealed class HubHarness : IDisposable
    {
        private const string UiRoot = "Assets/IdleFactory/Unity/UI/";

        public CardHarness Card { get; private set; }

        public HubPanel Hub { get; private set; }

        public RunShellDocument Shell => Card.Shell;

        public ShellRig Rig => Card.Rig;

        public RunSession Session => Card.Session;

        public static IEnumerator Create(ShellScreen screen, int percent, Action<HubHarness> result, bool registerSystems = true)
        {
            var harness = new HubHarness();
            CardHarness card = null;
            yield return CardHarness.Create(screen, percent, RunShellFixtures.ComfortableCash, built => card = built);
            harness.Card = card;
            if (registerSystems)
            {
                foreach (var id in new[]
                         {
                             ShellPanelIds.Contracts, ShellPanelIds.Items, ShellPanelIds.Research, ShellPanelIds.Industry, ShellPanelIds.Prestige,
                             ShellPanelIds.Settings,
                         })
                {
                    harness.Shell.Panels.Register(new TestShellPanel(id, char.ToUpperInvariant(id[0]) + id.Substring(1), 3));
                }
            }

            harness.Hub = NewHub(harness.Session, card.Commands);
            harness.Shell.Panels.Register(harness.Hub);
            yield return harness.Rig.Layout();
            result(harness);
        }

        public static HubPanel NewHub(RunSession session, RunCommands commands) =>
            new HubPanel(
                session, commands, Load<VisualTreeAsset>("Panels/HubPanel.uxml"), Load<StyleSheet>("Panels/HubPanel.uss"),
                Load<StyleSheet>("PanelScaffold.uss"), ContractsPanelTestSupport.Icon);

        public static T Load<T>(string relative) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(UiRoot + relative);
            Assert.That(asset, Is.Not.Null, UiRoot + relative);
            return asset;
        }

        public void Dispose() => Card?.Dispose();
    }
}
#endif
