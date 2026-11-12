using System;
using System.Collections.Generic;
using IdleFactory.Unity.Presentation;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// The test layouts of Feature 006 (T512, R04): 6 locations, 20 locations and an L shape, built through the explicit-cells constructor of
    /// <see cref="FactoryWorldLayout"/> and independent of <see cref="LatticeLayoutBuilder"/>. A layout is drawn as rows of text from the back
    /// (top) row to the front row: <c>#</c> is a location, <c>.</c> an empty lattice cell. Location ids follow reading order, top-left first.
    /// </summary>
    internal static class TestLayouts
    {
        public const string SixName = "six";
        public const string TwentyName = "twenty";
        public const string LName = "l-shape";

        public static readonly string[] Names = { SixName, TwentyName, LName };

        public static FactoryWorldLayout Six(WorldCompositionSettings settings = null) =>
            FromRows("test-six", WorldOrientation.Portrait, settings, "###", "###");

        public static FactoryWorldLayout Twenty(WorldCompositionSettings settings = null) =>
            FromRows("test-twenty", WorldOrientation.Landscape, settings, "####", "####", "####", "####", "####");

        /// <summary>A vertical bar of 4 and a foot of 4 along the front row: 7 locations, with 9 empty lattice cells.</summary>
        public static FactoryWorldLayout LShape(WorldCompositionSettings settings = null) =>
            FromRows("test-l", WorldOrientation.Portrait, settings, "#...", "#...", "#...", "####");

        public static FactoryWorldLayout ByName(string name, WorldCompositionSettings settings = null)
        {
            switch (name)
            {
                case SixName: return Six(settings);
                case TwentyName: return Twenty(settings);
                case LName: return LShape(settings);
                default: throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown test layout.");
            }
        }

        public static FactoryWorldLayout FromRows(string layoutId, WorldOrientation orientation, WorldCompositionSettings settings, params string[] rows)
        {
            var cells = new List<Vector2Int>();
            for (var rowFromTop = 0; rowFromTop < rows.Length; rowFromTop++)
            {
                for (var column = 0; column < rows[rowFromTop].Length; column++)
                {
                    if (rows[rowFromTop][column] == '#')
                    {
                        cells.Add(new Vector2Int(column, rows.Length - 1 - rowFromTop));
                    }
                }
            }

            return new FactoryWorldLayout(layoutId, cells, orientation, settings);
        }
    }
}
