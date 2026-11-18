using System.Collections.Generic;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>A build preview sink that records what the card asked the world for (the slot grid is the production sink).</summary>
    internal sealed class RecordingPreview : IBuildPreview
    {
        public List<int> Available { get; } = new List<int>();

        public int PreviewSlot { get; private set; } = -1;

        public string PreviewKey { get; private set; }

        public int Clears { get; private set; }

        public void ShowAvailable(IReadOnlyList<int> locations)
        {
            Available.Clear();
            Available.AddRange(locations);
        }

        public void ShowPreview(int location, string machineKey)
        {
            PreviewSlot = machineKey == null ? -1 : location;
            PreviewKey = machineKey;
        }

        public void ClearBuildPreview()
        {
            Clears++;
            Available.Clear();
            PreviewSlot = -1;
            PreviewKey = null;
        }
    }

    /// <summary>Adds the docked build card (T539) to a card harness the way the bootstrap composes it.</summary>
    internal static class BuildCardTestSupport
    {
        private const string UiRoot = "Assets/IdleFactory/Unity/UI/";

        public static BuildCard AttachBuildCard(this CardHarness harness, IBuildPreview preview)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiRoot + "Panels/BuildCard.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(UiRoot + "Panels/BuildCard.uss");
            var scaffold = AssetDatabase.LoadAssetAtPath<StyleSheet>(UiRoot + "PanelScaffold.uss");
            Assert.That(tree, Is.Not.Null, "BuildCard.uxml");
            Assert.That(styles, Is.Not.Null, "BuildCard.uss");
            var session = harness.Session;
            var card = new BuildCard(session, harness.Commands, tree, styles, scaffold, ContractsPanelTestSupport.Icon, harness.Feedback, () => preview,
                id => session.Catalog.Machines.TryGetValue(id, out var machine) ? machine.PresentationKey : string.Empty);
            harness.Shell.Panels.Register(card);
            return card;
        }
    }
}
#endif
