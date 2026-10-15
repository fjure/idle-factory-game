using System;
using System.Collections.Generic;
using System.Globalization;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The Market tab (<see cref="ShellPanelIds.Market"/>) hosts the goal line (T333) and three surfaces of contracts/ui-surfaces.md in one
    /// scrolling sheet: Inventory and rates, Priorities and Expansion. The shell has one panel ID per tab, so this host registers once and
    /// stacks the three section panels (each is a self-contained <see cref="IShellPanel"/> with its own UXML and USS that tests
    /// also mount alone). The host holds no rule: every section reads the run through the application contracts.
    /// </summary>
    public sealed class MarketPanelHost : IShellPanel
    {
        private readonly VisualElement root;

        /// <summary>Builds the three sections from their assets (null assets fall back to a code-built tree) and registers nothing itself.</summary>
        public MarketPanelHost(
            RunSession session,
            RunCommands commands,
            RunShellDocument shell,
            VisualTreeAsset inventoryTree,
            StyleSheet inventoryStyles,
            VisualTreeAsset prioritiesTree,
            StyleSheet prioritiesStyles,
            VisualTreeAsset expansionTree,
            StyleSheet expansionStyles,
            Func<string, Sprite> icons = null)
        {
            Goals = new GoalsLinePanel(session, shell, inventoryStyles, icons);
            Inventory = new InventoryRatesPanel(session, commands, shell, inventoryTree, inventoryStyles, icons);
            Priorities = new PrioritiesPanel(session, commands, shell, prioritiesTree, prioritiesStyles, icons);
            Expansion = new ExpansionPanel(session, commands, shell, expansionTree, expansionStyles, icons);
            root = new VisualElement { name = "market-panel" };
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexShrink = 0;
            root.Add(Goals.Root);
            root.Add(Inventory.Root);
            root.Add(Priorities.Root);
            root.Add(Expansion.Root);
        }

        /// <summary>The goal line at the top of Market (<c>goals-next</c>).</summary>
        public GoalsLinePanel Goals { get; }

        public InventoryRatesPanel Inventory { get; }
        public PrioritiesPanel Priorities { get; }
        public ExpansionPanel Expansion { get; }

        public string Id => ShellPanelIds.Market;
        public string Title => "Market";
        public VisualElement Root => root;

        public void OnShown(ShellPanelContext context)
        {
            Goals.OnShown(context);
            Inventory.OnShown(context);
            Priorities.OnShown(context);
            Expansion.OnShown(context);
        }

        public void Refresh()
        {
            Goals.Refresh();
            Inventory.Refresh();
            Priorities.Refresh();
            Expansion.Refresh();
        }

        public void OnHidden()
        {
            Goals.OnHidden();
            Inventory.OnHidden();
            Priorities.OnHidden();
            Expansion.OnHidden();
        }

        /// <summary>
        /// Cancels an open drag-to-reorder or Sell-all confirmation without reordering or selling anything. Register it with the bootstrap's
        /// <c>RegisterTransientAction</c> so a rotation cancels the drag and shows "Cancelled by rotation" (UX-9).
        /// </summary>
        public bool CancelForRotation() => Priorities.CancelDragForRotation() | Inventory.CancelForRotation();
    }

    /// <summary>Shared construction and formatting helpers of the Market surfaces. No rule lives here.</summary>
    public static class MarketPanelParts
    {
        /// <summary>Builds a panel root: the optional style sheet and UXML tree, falling back to an empty root of that name.</summary>
        public static VisualElement CreateRoot(VisualTreeAsset tree, StyleSheet styles, string name, string rootClass)
        {
            var root = new VisualElement { name = name };
            root.AddToClassList(rootClass);
            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            if (tree != null)
            {
                tree.CloneTree(root);
            }

            return root;
        }

        /// <summary>Finds a named element of the UXML, or creates it under <paramref name="fallbackParent"/> when the tree did not provide it.</summary>
        public static T Ensure<T>(VisualElement root, string name, string cssClass, VisualElement fallbackParent = null) where T : VisualElement, new()
        {
            var found = root.Q<T>(name);
            if (found != null)
            {
                return found;
            }

            var created = new T { name = name };
            if (!string.IsNullOrEmpty(cssClass))
            {
                created.AddToClassList(cssClass);
            }

            (fallbackParent ?? root).Add(created);
            return created;
        }

        public static Label NewLabel(string name, string cssClass, string text = "")
        {
            var label = new Label(text) { name = name };
            label.AddToClassList(cssClass);
            return label;
        }

        /// <summary>A number the player can focus or tap to read exactly: a 44 pt control that shows a compact value.</summary>
        public static Button NewValueButton(string name, string cssClass)
        {
            var button = new Button { name = name, focusable = true };
            button.AddToClassList("run-control");
            button.AddToClassList(cssClass);
            return button;
        }

        public static void SetIcon(VisualElement element, Func<string, Sprite> icons, string key)
        {
            var sprite = icons == null || key == null ? null : icons(key);
            element.style.backgroundImage = sprite == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(sprite);
        }

        public static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>The exact value of a rate in millionths per minute: up to six decimals, trailing zeros dropped, with its sign.</summary>
        public static string FormatExactRate(long perMinuteMicro, bool plusSign)
        {
            if (perMinuteMicro == 0)
            {
                return "0";
            }

            var negative = perMinuteMicro < 0;
            var magnitude = negative ? unchecked(0UL - (ulong)perMinuteMicro) : (ulong)perMinuteMicro;
            var whole = magnitude / 1_000_000UL;
            var fraction = magnitude % 1_000_000UL;
            var text = QuantityFormatter.FormatExact(whole);
            if (fraction != 0)
            {
                text += "." + fraction.ToString("000000", CultureInfo.InvariantCulture).TrimEnd('0');
            }

            return (negative ? "-" : plusSign ? "+" : string.Empty) + text;
        }

        public static string Money(long value) => "$" + QuantityFormatter.Format(value);

        public static string MoneyExact(long value) => "$" + QuantityFormatter.FormatExact(value);

        public static void DisposeAll(List<IDisposable> handles)
        {
            for (var i = 0; i < handles.Count; i++)
            {
                handles[i].Dispose();
            }

            handles.Clear();
        }
    }
}
