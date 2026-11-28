using System.Collections.Generic;
using IdleFactory.Unity.Presentation;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode.F007
{
    /// <summary>
    /// T610 test support: the 12 shipped locations on the lattice at a given <see cref="LayoutPitch"/>, built through the explicit-composition constructor of
    /// <see cref="FactoryWorldLayout"/> (the shipped builder is untouched). The dense pitch is 70 x 46 art px: the plate width is the pitch and the rows sit 8 px closer
    /// than the F006 54 px, so the 45 px hit diamond is clipped only by the cell and still reaches 44 pt at the minimum zoom.
    /// </summary>
    internal static class DenseLayouts
    {
        public static readonly LayoutPitch Dense = new LayoutPitch(new Vector2(70f / SlotGridLayout.Density, 46f / SlotGridLayout.Density), string.Empty, LayoutPitch.HitPointsFloor);

        public static readonly LayoutPitch DenseBays = new LayoutPitch(Dense.PitchWorld, LayoutPitch.BaysRule, LayoutPitch.HitPointsFloor);

        /// <summary>The 12 shipped locations in 3 (portrait) or 4 (landscape) columns at <paramref name="pitch"/>, location 0 at the top left.</summary>
        public static FactoryWorldLayout Shipped(int columns, LayoutPitch pitch)
        {
            var orientation = columns == 4 ? WorldOrientation.Landscape : WorldOrientation.Portrait;
            var rows = (LatticeLayoutBuilder.ShippedLocationCount + columns - 1) / columns;
            var cells = new List<Vector2Int>();
            for (var i = 0; i < LatticeLayoutBuilder.ShippedLocationCount; i++)
            {
                cells.Add(new Vector2Int(i % columns, rows - 1 - i / columns));
            }

            return new FactoryWorldLayout("lattice-12x" + columns, cells, orientation, WorldCompositionSettings.DefaultComposition(orientation).WithPitch(pitch));
        }
    }
}
