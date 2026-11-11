using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// The Art Bible inputs of the integer camera scale (Art Bible v1.0 section 8, <c>contracts/camera-scale.md</c>): the art density
    /// D (sprite pixels per unit), the smallest readable cell in UI points and the largest scale. The old pixels-per-cell limits
    /// (16 to 64, fit-all floor 24) are gone: the allowed scales are <c>[n_min, maxScale]</c> and <c>n_min</c> derives from the
    /// readable cell and the panel scale. The values change here only; the scene's older serialized block is dropped by its next
    /// re-save through Unity tooling (no hand edit).
    /// </summary>
    [Serializable]
    public sealed class RunZoomSettings
    {
        /// <summary>D: art pixels per cell, equal to the sprite pixels per unit (Art Bible v1.0: 24).</summary>
        [Min(1)] public int artPixelsPerCell = 24;

        /// <summary>The smallest cell size in UI points at which machines stay identifiable (the Z24 floor of Feature 003).</summary>
        [Min(1)] public int readableCellPoints = 24;

        /// <summary>The largest allowed integer scale.</summary>
        [Min(1)] public int maxScale = 6;

        public ArtScaleSettings ToScaleSettings() => new ArtScaleSettings(artPixelsPerCell, readableCellPoints, maxScale);
    }
}
