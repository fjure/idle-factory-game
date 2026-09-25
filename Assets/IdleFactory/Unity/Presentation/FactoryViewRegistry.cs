using System;
using IdleFactory.Domain;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// Coordinates full rebuilds and recreation of the presentation adapter for one factory.
    /// The registry owns no simulation state; selection is retained by instance ID and resolved
    /// by the configured FactoryView against the current application snapshot.
    /// </summary>
    public sealed class FactoryViewRegistry
    {
        private readonly Func<FactoryView> createView;
        private readonly Action<FactoryView> configureView;
        private readonly Action<FactoryView> destroyView;
        private readonly Func<FactoryView, BuildingInstanceId?> readSelection;
        private readonly Action<FactoryView, BuildingInstanceId?> applySelection;
        private FactoryView currentView;

        public FactoryViewRegistry(
            Func<FactoryView> createView,
            Action<FactoryView> configureView,
            Action<FactoryView> destroyView,
            Func<FactoryView, BuildingInstanceId?> readSelection,
            Action<FactoryView, BuildingInstanceId?> applySelection,
            FactoryView initialView = null)
        {
            this.createView = createView ?? throw new ArgumentNullException(nameof(createView));
            this.configureView = configureView ?? throw new ArgumentNullException(nameof(configureView));
            this.destroyView = destroyView ?? throw new ArgumentNullException(nameof(destroyView));
            this.readSelection = readSelection ?? throw new ArgumentNullException(nameof(readSelection));
            this.applySelection = applySelection ?? throw new ArgumentNullException(nameof(applySelection));
            currentView = initialView;
        }

        /// <summary>The currently registered view, or null before its first rebuild.</summary>
        public FactoryView CurrentView => currentView;

        /// <summary>
        /// Rebuilds all visuals from the current immutable application snapshot. Repeating the
        /// operation leaves factory state and the registered view identity unchanged.
        /// </summary>
        public FactoryView Rebuild()
        {
            if (currentView == null)
            {
                return Recreate();
            }

            currentView.Refresh();
            return currentView;
        }

        /// <summary>
        /// Creates and configures a replacement view, re-resolves the previous selection by
        /// persistent instance ID, then destroys the old view after the replacement is ready.
        /// </summary>
        public FactoryView Recreate()
        {
            var previous = currentView;
            var selection = previous == null ? (BuildingInstanceId?)null : readSelection(previous);
            var replacement = createView();
            if (replacement == null)
            {
                throw new InvalidOperationException("The factory view factory returned no view.");
            }

            if (ReferenceEquals(previous, replacement))
            {
                throw new InvalidOperationException("The factory view factory must create a distinct replacement.");
            }

            try
            {
                configureView(replacement);
                applySelection(replacement, selection);
            }
            catch
            {
                if (replacement != null)
                {
                    destroyView(replacement);
                }

                throw;
            }

            currentView = replacement;
            if (previous != null)
            {
                destroyView(previous);
            }

            return currentView;
        }
    }
}
