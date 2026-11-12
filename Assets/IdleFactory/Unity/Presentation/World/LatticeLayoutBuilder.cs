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

        private readonly int locationCount;
        private readonly WorldOrientation orientation;
        private readonly WorldCompositionSettings settings;

        public LatticeLayoutBuilder(int locationCount, WorldOrientation orientation, WorldCompositionSettings settings = null)
        {
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

        /// <summary>The layout of the shipped locations with the default composition values: the F004 lattice. Cached; the layout is immutable.</summary>
        public static FactoryWorldLayout Shipped(WorldOrientation orientation)
        {
            var index = (int)orientation;
            return ShippedLayouts[index] ?? (ShippedLayouts[index] = new LatticeLayoutBuilder(ShippedLocationCount, orientation).Build());
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

            return new FactoryWorldLayout("lattice-" + locationCount + "x" + columns, cells, orientation, settings);
        }
    }
}
