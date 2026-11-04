using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>The six tabs of the run shell (contracts/ui-surfaces.md, Global).</summary>
    public enum ShellTab
    {
        Factory = 0,
        Market = 1,
        Contracts = 2,
        Items = 3,
        Research = 4,
        Prestige = 5,
    }

    /// <summary>
    /// The panel IDs that T113 to T118 and T433 mount into. <see cref="Build"/> and <see cref="Machine"/> are the slot sheets of the
    /// Factory tab (an empty or an occupied slot was tapped); the next five are the tab panels; <see cref="Settings"/> is the gear's sheet.
    /// </summary>
    public static class ShellPanelIds
    {
        public const string Build = "build";
        public const string Machine = "machine";
        public const string Market = "market";
        public const string Contracts = "contracts";
        public const string Items = "items";
        public const string Research = "research";
        public const string Prestige = "prestige";

        /// <summary>The Settings sheet (T433, OD-8): opened by the gear in the top strip, not by a tab or a slot.</summary>
        public const string Settings = "settings";

        public static string ForTab(ShellTab tab)
        {
            switch (tab)
            {
                case ShellTab.Market: return Market;
                case ShellTab.Contracts: return Contracts;
                case ShellTab.Items: return Items;
                case ShellTab.Research: return Research;
                case ShellTab.Prestige: return Prestige;
                default: return null;
            }
        }

        public static bool IsKnown(string id) =>
            id == Build || id == Machine || id == Market || id == Contracts || id == Items || id == Research || id == Prestige || id == Settings;
    }

    /// <summary>The section IDs a panel can be opened at (<see cref="IShellSectionPanel"/>).</summary>
    public static class ShellSections
    {
        /// <summary>The Industry section of the Research panel (R-01).</summary>
        public const string Industry = "industry";
    }

    /// <summary>
    /// A panel whose content has named sections that another surface can point to (the Prestige panel opens Research at Industry). The shell
    /// asks it to scroll the section into view once the panel is laid out; <paramref name="focus"/> also moves focus into the section.
    /// </summary>
    public interface IShellSectionPanel : IShellPanel
    {
        /// <summary>Scrolls the section to the top of the sheet. Returns false while the layout cannot place it yet or the section does not exist.</summary>
        bool RevealSection(string sectionId, bool focus);
    }

    /// <summary>What the shell tells a panel when it is shown.</summary>
    public sealed class ShellPanelContext
    {
        public ShellPanelContext(RunShellDocument shell, int slotIndex)
        {
            Shell = shell;
            SlotIndex = slotIndex;
        }

        /// <summary>The shell, for the shared confirmation dialog, the notice line and the exact-value detail line.</summary>
        public RunShellDocument Shell { get; }

        /// <summary>The selected slot of a <see cref="ShellPanelIds.Build"/> or <see cref="ShellPanelIds.Machine"/> sheet, otherwise -1.</summary>
        public int SlotIndex { get; }
    }

    /// <summary>
    /// A panel that mounts into the shell. The panel builds its <see cref="Root"/> once and the shell shows it in the sheet or
    /// the side panel; the shell never edits a panel's tree. Panels read their state through the run application contracts.
    /// </summary>
    public interface IShellPanel
    {
        string Id { get; }
        string Title { get; }

        /// <summary>The element mounted into the shell. Built once; the shell toggles its display.</summary>
        VisualElement Root { get; }

        /// <summary>Called each time the panel becomes the visible one, with the slot (for the slot sheets) and the shell services.</summary>
        void OnShown(ShellPanelContext context);

        /// <summary>Called when the run read model changed while the panel is visible.</summary>
        void Refresh();

        /// <summary>Called when the panel stops being the visible one, including when the shell closes it.</summary>
        void OnHidden();
    }

    /// <summary>
    /// The panel mount registry. A panel registers once and is shown by ID, so T113 to T118 add panels without editing the
    /// shell, its UXML or its USS. An ID without a registered panel shows the shell's "not available yet" placeholder.
    /// </summary>
    public sealed class ShellPanelRegistry
    {
        private readonly Dictionary<string, IShellPanel> panels = new Dictionary<string, IShellPanel>(StringComparer.Ordinal);

        /// <summary>Raised after a panel was registered or unregistered, with its ID.</summary>
        public event Action<string> Changed;

        public int Count => panels.Count;

        public bool IsRegistered(string id) => id != null && panels.ContainsKey(id);

        public bool TryGet(string id, out IShellPanel panel)
        {
            panel = null;
            return id != null && panels.TryGetValue(id, out panel);
        }

        /// <summary>Registers a panel and returns the handle that unregisters it. A second panel with the same ID throws.</summary>
        public IDisposable Register(IShellPanel panel)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            if (!ShellPanelIds.IsKnown(panel.Id))
            {
                throw new ArgumentException("The panel ID '" + panel.Id + "' is not one of the ShellPanelIds.", nameof(panel));
            }

            if (panel.Root == null)
            {
                throw new ArgumentException("A panel needs a root element.", nameof(panel));
            }

            if (panels.ContainsKey(panel.Id))
            {
                throw new InvalidOperationException("A panel is already registered for '" + panel.Id + "'.");
            }

            panels.Add(panel.Id, panel);
            Changed?.Invoke(panel.Id);
            return new Registration(this, panel);
        }

        private void Unregister(IShellPanel panel)
        {
            if (panels.TryGetValue(panel.Id, out var current) && ReferenceEquals(current, panel))
            {
                panels.Remove(panel.Id);
                Changed?.Invoke(panel.Id);
            }
        }

        private sealed class Registration : IDisposable
        {
            private readonly ShellPanelRegistry owner;
            private readonly IShellPanel panel;
            private bool disposed;

            public Registration(ShellPanelRegistry owner, IShellPanel panel)
            {
                this.owner = owner;
                this.panel = panel;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                owner.Unregister(panel);
            }
        }
    }

    /// <summary>One request for the shell's shared confirmation dialog (UX-2). Every confirmation in the game uses it.</summary>
    public sealed class ConfirmationRequest
    {
        public string Title { get; set; } = "Confirm";
        public string Message { get; set; } = string.Empty;
        public string ConfirmLabel { get; set; } = "Confirm";
        public string CancelLabel { get; set; } = "Cancel";

        /// <summary>A destructive confirmation (Remove, Prestige, Abandon): the confirm button uses the danger fill and the warning icon.</summary>
        public bool Destructive { get; set; }

        /// <summary>The control that opened the dialog; focus returns to it when the dialog closes. Null returns focus to the focused control at open time.</summary>
        public VisualElement Invoker { get; set; }

        public Action OnConfirm { get; set; }

        /// <summary>Called for Cancel, Escape, Android Back and a rotation cancel; the argument says why the dialog closed.</summary>
        public Action<ConfirmationCloseReason> OnCancel { get; set; }
    }

    public enum ConfirmationCloseReason
    {
        Confirmed = 0,
        Cancelled = 1,
        Back = 2,
        Rotation = 3,
        Replaced = 4,
    }
}
