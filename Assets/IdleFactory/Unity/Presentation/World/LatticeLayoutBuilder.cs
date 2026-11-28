using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// Builds the <see cref="FactoryWorldLayout"/> of the isometric lattice (Feature 006, R04): <c>columns</c> locations per row in reading
    /// order, 3 columns on a portrait screen and 4 on a landscape one, location 0 at the top left. For the 12 shipped locations it reproduces
    /// the F004 geometry exactly (<see cref="SlotGridLayout"/>). The location count is a parameter: nothing here assumes 12.
    /// </summary>
    public sealed class LatticeLayoutBuilder
    {
        /// <summary>The number of locations the shipped game has today (F004). The only place that names it for the layout seam.</summary>
        public const int ShippedLocationCount = SlotGridLayout.SlotCount;

        private static readonly FactoryWorldLayout[] ShippedLayouts = new FactoryWorldLayout[2];
        private static readonly LayoutComposition[] ShippedCompositions = new LayoutComposition[2];
        private static WorldCompositionSettings activeSettings;

        private static LayoutPitch? activePitch;

        private readonly int locationCount;
        private readonly WorldOrientation orientation;
        private readonly WorldCompositionSettings settings;
        private readonly LayoutPitch? pitchOverride;

        public LatticeLayoutBuilder(int locationCount, WorldOrientation orientation, WorldCompositionSettings settings = null, LayoutPitch? pitchOverride = null)
        {
            this.pitchOverride = pitchOverride;
            if (locationCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(locationCount), locationCount, "A layout needs at least one location.");
            }

            this.locationCount = locationCount;
            this.orientation = orientation;
            this.settings = settings;
        }

        /// <summary>The lattice columns for an orientation: 3 in portrait, 4 in landscape (F004).</summary>
        public static int ColumnsFor(WorldOrientation orientation) => orientation == WorldOrientation.Landscape ? 4 : 3;

        /// <summary>The orientation of a screen or panel: landscape when it is wider than tall (same rule as <see cref="SlotGridLayout.ColumnsFor"/>).</summary>
        public static WorldOrientation OrientationFor(float width, float height) => width > height ? WorldOrientation.Landscape : WorldOrientation.Portrait;

        /// <summary>
        /// The authored composition the shell and the world ask for (P1-F1, T527): the validated <c>WorldCompositionSettings</c> asset, or null for
        /// the F004-reproducing defaults. The bootstrap sets it once at start-up (<see cref="TryUseSettings"/>) and clears it on destroy; every
        /// <see cref="Shipped(WorldOrientation)"/> caller, the run shell's orientation fallback among them, then reads the same builder.
        /// </summary>
        public static WorldCompositionSettings ActiveSettings => activeSettings;

        /// <summary>
        /// Makes <paramref name="settings"/> the composition of the shipped layouts when it is valid; an invalid asset keeps the previous
        /// composition (the defaults at start-up) and its problems are returned, never thrown. Null restores the defaults.
        /// </summary>
        public static bool TryUseSettings(WorldCompositionSettings settings, out IReadOnlyList<string> problems)
        {
            problems = settings == null ? System.Array.Empty<string>() : settings.Validate();
            if (problems.Count > 0)
            {
                return false;
            }

            activeSettings = settings;
            return true;
        }

        /// <summary>
        /// The lattice pitch of the active look profile (F007 T610): the shipped layouts use it instead of the pitch of the composition asset. The bootstrap sets it from
        /// <c>LookProfileResolver.LayoutPitch</c> whenever the world is rebuilt for a profile, before it asks for the layout; null clears it (the composition's own pitch,
        /// F006 by default). The pitch is part of the composition identity, so the shipped layout and the camera refit on a change.
        /// </summary>
        public static LayoutPitch? ActivePitch => activePitch;

        /// <summary>Sets (or with null clears) the pitch of the active look profile. See <see cref="ActivePitch"/>.</summary>
        public static void UsePitch(LayoutPitch? pitch) => activePitch = pitch;

        /// <summary>The layout of the shipped locations with the active composition (the F004 defaults when none is set). Cached per composition; the layout is immutable.</summary>
        public static FactoryWorldLayout Shipped(WorldOrientation orientation) => Shipped(orientation, activeSettings);

        /// <summary>The layout of the shipped locations for an explicit composition (null: the F004 defaults). The cache rebuilds when the composition values change.</summary>
        public static FactoryWorldLayout Shipped(WorldOrientation orientation, WorldCompositionSettings settings)
        {
            var index = (int)orientation;
            var composition = settings != null ? settings.ComposeFor(orientation) : WorldCompositionSettings.DefaultComposition(orientation);
            if (activePitch.HasValue)
            {
                composition = composition.WithPitch(activePitch.Value);
            }

            if (ShippedLayouts[index] == null || !ShippedCompositions[index].Equals(composition))
            {
                ShippedLayouts[index] = new LatticeLayoutBuilder(ShippedLocationCount, orientation, settings, activePitch).Build();
                ShippedCompositions[index] = composition;
            }

            return ShippedLayouts[index];
        }

        /// <summary>The F004 layout for a column count: 3 is portrait, 4 is landscape; any other count throws.</summary>
        public static FactoryWorldLayout ShippedForColumns(int columns)
        {
            if (columns != 3 && columns != 4)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }

            return Shipped(columns == 4 ? WorldOrientation.Landscape : WorldOrientation.Portrait);
        }

        /// <summary>The F004 layout for a screen size.</summary>
        public static FactoryWorldLayout Shipped(float width, float height) => Shipped(OrientationFor(width, height));

        public int LocationCount => locationCount;

        public WorldOrientation Orientation => orientation;

        public FactoryWorldLayout Build()
        {
            var columns = ColumnsFor(orientation);
            var rows = (locationCount + columns - 1) / columns;
            var cells = new List<Vector2Int>(locationCount);
            for (var i = 0; i < locationCount; i++)
            {
                cells.Add(new Vector2Int(i % columns, rows - 1 - i / columns));
            }

            var composition = settings != null ? settings.ComposeFor(orientation) : WorldCompositionSettings.DefaultComposition(orientation);
            if (pitchOverride.HasValue)
            {
                composition = composition.WithPitch(pitchOverride.Value);
            }

            return new FactoryWorldLayout("lattice-" + locationCount + "x" + columns, cells, orientation, composition);
        }
    }
}
