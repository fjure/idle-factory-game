using System;

namespace IdleFactory.Unity.Presentation.Look
{
    /// <summary>
    /// The world views a look profile draws, and the one switch path (contracts/look-profile.md rule 5): <see cref="Apply"/> gives every view the
    /// catalog of the newly active profile and rebinds it to the slot grid, which is the rebuild the world already does for a layout change. The caller
    /// then refits the camera and re-renders from the unchanged authoritative state. It holds views only: it has no reference to a run session, a command
    /// or a save, so a switch cannot change anything but how the same state is drawn.
    /// </summary>
    public sealed class LookProfileWorldViews
    {
        private readonly SlotGridView grid;
        private readonly FactoryDeckView deck;
        private readonly WorldCompositionSettings composition;
        private readonly EffectPoolView effects;
        private readonly EffectPool effectPool;
        private readonly DepotLinkView links;
        private readonly DepositView deposit;
        private readonly AttentionMarkerView markers;
        private readonly Func<string, string> resourceKeyOf;

        public LookProfileWorldViews(
            SlotGridView grid,
            FactoryDeckView deck,
            WorldCompositionSettings composition,
            EffectPoolView effects,
            EffectPool effectPool,
            DepotLinkView links,
            DepositView deposit,
            AttentionMarkerView markers,
            Func<string, string> resourceKeyOf)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.deck = deck;
            this.composition = composition;
            this.effects = effects;
            this.effectPool = effectPool;
            this.links = links;
            this.deposit = deposit;
            this.markers = markers;
            this.resourceKeyOf = resourceKeyOf;
        }

        /// <summary>Configures every view with <paramref name="catalog"/> and binds it to the grid again. The next render shows the new art.</summary>
        public void Apply(PresentationSpriteCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            grid.Configure(catalog);
            if (deck != null)
            {
                deck.Configure(catalog, composition);
                deck.Bind(grid);
            }

            if (effects != null && effectPool != null)
            {
                effects.Configure(catalog, effectPool);
                effects.Bind(grid);
            }

            if (links != null)
            {
                links.Configure(catalog);
                links.Bind(grid);
            }

            if (deposit != null)
            {
                deposit.Configure(catalog);
                deposit.Bind(grid);
            }

            if (markers != null && resourceKeyOf != null)
            {
                markers.Configure(catalog, resourceKeyOf);
                markers.Bind(grid);
            }
        }
    }
}
