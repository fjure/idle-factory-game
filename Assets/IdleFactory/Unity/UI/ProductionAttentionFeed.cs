using System;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// Feeds the production attention indicator of the persistent layer (T537, R09, FR-026): it refreshes the <see cref="AttentionModel"/> from the run
    /// rates and shows its strip (the top shortage resource, the affected count and "+N", or store full). It reads existing state only and changes
    /// nothing. The refresh runs only when the structure or quantity revision moved, and at most every <see cref="ReadSeconds"/>, so the per-frame path
    /// is a revision compare. Without a world layout (a bare test shell) it does nothing and the indicator stays hidden.
    /// </summary>
    public sealed class ProductionAttentionFeed
    {
        /// <summary>The most often the rates are re-read, in real seconds.</summary>
        public const float ReadSeconds = 0.25f;

        private readonly RunSession session;
        private readonly RunShellDocument shell;
        private readonly AttentionModel model = new AttentionModel();
        private float lastReadTime = float.NegativeInfinity;

        public ProductionAttentionFeed(RunSession session, RunShellDocument shell)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        }

        /// <summary>The model the indicator shows (read-only use: tests compare it with the shell's indicator).</summary>
        public AttentionModel Model => model;

        /// <summary>Forgets the cached revisions so the next update recomputes (a session replace or a layout change).</summary>
        public void Invalidate()
        {
            model.Invalidate();
            lastReadTime = float.NegativeInfinity;
        }

        public void Update(float realtime)
        {
            var layout = shell.CurrentWorldLayout();
            if (layout == null)
            {
                return;
            }

            if (!model.NeedsRefresh(session.StructureRevision, session.QuantityRevision, layout, SystemAttentionFlags.None) ||
                realtime - lastReadTime < ReadSeconds)
            {
                return;
            }

            lastReadTime = realtime;
            model.Refresh(layout, RunSnapshots.Rates(session));
            var strip = model.Strip;
            if (!strip.IsVisible)
            {
                shell.SetProductionAttention(strip, string.Empty, null);
                return;
            }

            string cause;
            string iconKey;
            if (strip.Kind == AttentionKind.Shortage && strip.ResourceId != null)
            {
                cause = AttentionModel.ShortOnCopy(SlotSheetText.ResourceLabel(session, strip.ResourceId));
                iconKey = session.Catalog.Resources.TryGetValue(strip.ResourceId, out var def) ? def.PresentationKey : "F003-UI-ICON-WARNING";
            }
            else
            {
                cause = MachineCardCopy.StoreFull;
                iconKey = "F003-UI-ICON-STORAGE";
            }

            shell.SetProductionAttention(strip, cause, iconKey);
        }
    }
}
