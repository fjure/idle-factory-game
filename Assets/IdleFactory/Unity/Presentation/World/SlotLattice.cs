using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// The fixed 12-slot isometric lattice (T456, Art Bible v1.0 sections 3.2a and 3.5, owner decisions S1 and S2): 3 columns by 4 rows
    /// on a portrait screen and 4 by 3 on a landscape one. Plates are 2:1 diamonds (70 x 35 art px at D = 24) placed on a lattice with a
    /// pitch of 70 x 54 art px (2.9167 x 2.25 cells). Pitch 70 is the plate width: neighbouring plates touch at their side apexes and never
    /// overlap, and it is the widest pitch at which fit-all still holds on the 1536 x 2048 tablet (FR-011; 72 misses n_min by 1.6 %). The
    /// badge band (24 px above the footprint) stays clear of every other slot's overlay and frame ring; 54 is the smallest row pitch that
    /// does (52 touches the frame of the row behind). Every position is a whole number of art pixels (rule P3). Pure. World origin: the lower-left corner of the
    /// lattice content (see <see cref="Extent"/>).
    /// </summary>
    public static class SlotGridLayout
    {
        public const int SlotCount = 12;

        /// <summary>The art density the lattice is authored for (pixels per cell).</summary>
        public const int Density = 24;

        public const int PitchXPixels = 70;
        public const int PitchYPixels = 54;
        public const int PlateWidthPixels = 70;
        public const int PlateHeightPixels = 35;

        /// <summary>The pivot of a slot sits 15 art pixels above the bottom of the plate; the plate centre is 2.5 px above the pivot.</summary>
        public const int PivotAbovePlateBottomPixels = 15;

        /// <summary>The badge top is 39 art pixels above the pivot (24 px above the footprint top at 15 px, Art Bible 3.2).</summary>
        public const int ContentAbovePivotPixels = 39;

        /// <summary>Tap target in art pixels: the plate diamond grown to a 90 x 45 diamond (at least 44 points at every allowed scale), clipped to the lattice cell.</summary>
        public const int HitWidthPixels = 90;
        public const int HitHeightPixels = 45;

        public const float PitchX = PitchXPixels / (float)Density;
        public const float PitchY = PitchYPixels / (float)Density;
        public const float HalfCellMargin = 0.5f;

        /// <summary>The plate centre above the slot pivot (half an art pixel is accepted for the hit shape only, Art Bible 3.2a).</summary>
        public const float PlateCentreAbovePivot = 2.5f / Density;

        /// <summary>The column count for a screen: 3 on a portrait screen, 4 on a landscape one.</summary>
        public static int ColumnsFor(float screenWidth, float screenHeight) => screenWidth > screenHeight ? 4 : 3;

        public static int RowsFor(int columns) => (SlotCount + columns - 1) / columns;

        /// <summary>The sorting depth of a slot: its row counted from the back (top) row; a front row draws later (research R04).</summary>
        public static int RowOf(int index, int columns) => index / columns;

        /// <summary>
        /// The content extent that fit-all and the pan limits use: every plate plus the badge band above the back row. Its lower-left corner
        /// is the world origin.
        /// </summary>
        public static Rect Extent(int columns)
        {
            var rows = RowsFor(columns);
            var widthPixels = (columns - 1) * PitchXPixels + PlateWidthPixels;
            var heightPixels = (rows - 1) * PitchYPixels + PivotAbovePlateBottomPixels + ContentAbovePivotPixels;
            return new Rect(0f, 0f, widthPixels / (float)Density, heightPixels / (float)Density);
        }

        /// <summary>
        /// The footprint pivot of a slot in world units (slot 0 is top-left in reading order): the origin of the slot's canvas layers and of every
        /// overlay anchor. Replaces the former constant pivot-in-slot (T429 carry-forward).
        /// </summary>
        public static Vector2 PivotOf(int index, int columns)
        {
            var rows = RowsFor(columns);
            var column = index % columns;
            var rowFromTop = index / columns;
            return new Vector2(
                (PlateWidthPixels / 2 + column * PitchXPixels) / (float)Density,
                (PivotAbovePlateBottomPixels + (rows - 1 - rowFromTop) * PitchYPixels) / (float)Density);
        }

        /// <summary>The centre of the plate diamond (and of the slot's lattice cell).</summary>
        public static Vector2 PlateCentreOf(int index, int columns) => PivotOf(index, columns) + new Vector2(0f, PlateCentreAbovePivot);

        /// <summary>The bounding box of the plate diamond (70 x 35 art px).</summary>
        public static Rect SlotRect(int index, int columns)
        {
            var size = new Vector2(PlateWidthPixels, PlateHeightPixels) / Density;
            return new Rect(PlateCentreOf(index, columns) - size * 0.5f, size);
        }

        /// <summary>
        /// The slot under a world point, or -1. The tap shape is the plate diamond grown to 90 x 45 art px (a thumb-sized target) and clipped
        /// to the slot's 70 x 54 lattice cell, so cells never overlap and art above the footprint (machine overhang, badge band) never
        /// captures a tap. The shape is a diamond (<c>nx + ny &lt;= 1</c>) inside the cell, so the four corners of a cell lie outside it and
        /// belong to no slot: a tap there hits nothing.
        /// </summary>
        public static int HitTest(Vector2 world, int columns)
        {
            var rows = RowsFor(columns);
            var first = PlateCentreOf((rows - 1) * columns, columns); // bottom-left cell centre
            var column = Mathf.FloorToInt((world.x - first.x) / PitchX + 0.5f);
            var rowFromBottom = Mathf.FloorToInt((world.y - first.y) / PitchY + 0.5f);
            if (column < 0 || column >= columns || rowFromBottom < 0 || rowFromBottom >= rows)
            {
                return -1;
            }

            var index = (rows - 1 - rowFromBottom) * columns + column;
            if (index >= SlotCount)
            {
                return -1;
            }

            var centre = PlateCentreOf(index, columns);
            var nx = Mathf.Abs(world.x - centre.x) / (HitWidthPixels * 0.5f / Density);
            var ny = Mathf.Abs(world.y - centre.y) / (HitHeightPixels * 0.5f / Density);
            return nx + ny <= 1f ? index : -1;
        }

        /// <summary>The pixels-per-cell (screen points per cell) at which the whole lattice plus a half-cell margin fits an area.</summary>
        public static float FitAllPixelsPerCell(float areaWidthPoints, float areaHeightPoints, int columns)
        {
            var extent = Extent(columns);
            var widthCells = extent.width + 2f * HalfCellMargin;
            var heightCells = extent.height + 2f * HalfCellMargin;
            return Mathf.Min(areaWidthPoints / widthCells, areaHeightPoints / heightCells);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // The floor field (T472, Art Bible v1.0.3 section 3.5): one 70 x 54 tile per lattice cell, extended over the whole field.
        // The cell (column, row) has its tile pivot on the footprint centre (35 + 70 * column, 15 + 54 * row) art px, the row counted
        // from the bottom row of the lattice (row 0 is the front row); the columns and rows outside the 12 slots continue the same lattice, so
        // every slot and every tile sees the same floor phase and a whole-art-pixel position (P3).
        // ---------------------------------------------------------------------------------------------------------------------

        public const int FloorTileWidthPixels = PitchXPixels;
        public const int FloorTileHeightPixels = PitchYPixels;

        /// <summary>The tile pivot is the tile centre; the tile spans 27 art px below and above its footprint centre.</summary>
        public const int FloorTilePivotYPixels = FloorTileHeightPixels / 2;

        /// <summary>The art pixels the floor extends past the visible rectangle on each side, so a one-frame camera lag shows no bare edge.</summary>
        public const int FloorMarginPixels = 2;

        /// <summary>The most tiles the floor ever draws; a larger visible area (an absurd camera) draws no floor instead of an unbounded pool.</summary>
        public const int FloorMaxTiles = 2048;

        /// <summary>The footprint centre of the floor cell (<paramref name="column"/>, <paramref name="row"/>) in world units; the same pivot a slot has.</summary>
        public static Vector2 FloorPivotOf(int column, int row) =>
            new Vector2((PlateWidthPixels / 2 + column * PitchXPixels) / (float)Density, (PivotAbovePlateBottomPixels + row * PitchYPixels) / (float)Density);

        /// <summary>
        /// The block of floor cells that covers a visible world rectangle (plus <see cref="FloorMarginPixels"/>): every cell whose tile touches
        /// the rectangle. Pure and allocation-free. A rectangle that is empty, not finite or needs more than <see cref="FloorMaxTiles"/> tiles
        /// gives <see cref="FloorRange.Empty"/>.
        /// </summary>
        public static FloorRange FloorCellsFor(float minX, float minY, float maxX, float maxY)
        {
            if (float.IsNaN(minX) || float.IsNaN(minY) || float.IsNaN(maxX) || float.IsNaN(maxY) ||
                float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY) || maxX <= minX || maxY <= minY)
            {
                return FloorRange.Empty;
            }

            double x0 = minX * (double)Density - FloorMarginPixels;
            double x1 = maxX * (double)Density + FloorMarginPixels;
            double y0 = minY * (double)Density - FloorMarginPixels;
            double y1 = maxY * (double)Density + FloorMarginPixels;

            // Column c covers [70 c, 70 c + 70); row r covers [54 r - 12, 54 r + 42): the footprint centre is 15 px above the cell's lattice
            // origin and the tile reaches 27 px either side of it.
            var rowOrigin = PivotAbovePlateBottomPixels - FloorTilePivotYPixels;
            var minColumn = (long)Math.Floor(x0 / PitchXPixels);
            var maxColumn = (long)Math.Ceiling(x1 / PitchXPixels) - 1;
            var minRow = (long)Math.Floor((y0 - rowOrigin) / PitchYPixels);
            var maxRow = (long)Math.Ceiling((y1 - rowOrigin) / PitchYPixels) - 1;
            var columnCount = maxColumn - minColumn + 1;
            var rowCount = maxRow - minRow + 1;
            if (columnCount <= 0 || rowCount <= 0 || columnCount * rowCount > FloorMaxTiles)
            {
                return FloorRange.Empty;
            }

            return new FloorRange((int)minColumn, (int)maxColumn, (int)minRow, (int)maxRow);
        }
    }
}
