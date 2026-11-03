using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>The camera presets of the W004-V visual worst case (research R12).</summary>
    internal enum W004VCameraPreset
    {
        /// <summary>
        /// The smallest readable zoom. Until the integer scale model of Feature 004 exists, this is the Feature 003 readability
        /// floor (<see cref="LegacyCameraFraming.FitAllFloorPixelsPerCell"/>, 24 points per cell) centred on the grid; the label
        /// <c>n_min</c> is kept so the fixture and its baselines need no rename when the scale model replaces the constant.
        /// </summary>
        NMin,

        /// <summary>The fit-all framing of <see cref="LegacyCameraFraming.FitAll"/> over the area the shell leaves uncovered.</summary>
        FitAll,
    }

    /// <summary>
    /// W004-V (T409, research R12): the Feature 004 visual worst case as a Play Mode fixture over the W003-S checkpoint
    /// (<c>06-w003-s-stress</c>, hash in <see cref="W003PlayerFixtures.Stress"/>). The checkpoint already has all 12 slots
    /// occupied; the fixture only FORCES the presentation of each slot (state badge, the two markers, the selection frame) by
    /// replacing the <see cref="SlotVisual"/> values the grid draws. It touches no Core state and no save, so the run keeps
    /// the W003-S economy while the picture shows every machine state, the densest overlay (one badge, two markers and the
    /// selection frame on one slot) and, in a running player, animating machines.
    /// </summary>
    internal static class W004VFixture
    {
        public const string Name = "W004-V";

        /// <summary>The slot that carries the densest overlay: a badge, both markers and the selection frame.</summary>
        public const int SelectedSlot = 6;

        /// <summary>SHA-256 of <see cref="Canonical"/>; any change of the forced table, the base checkpoint or the presets changes it.</summary>
        public const string ExpectedHash = "1920dfafb75e69131fe43bb38f66c088762b195c46aa7916c568747c7ecca6fc";

        /// <summary>The six machine states forced onto the slots in rotation (slot index modulo 6).</summary>
        internal static readonly ForcedState[] States =
        {
            new ForcedState("running", true, MachineBlocker.None, MachinePhase.Running),
            new ForcedState("waiting", true, MachineBlocker.None, MachinePhase.Waiting),
            new ForcedState("missing-input", true, MachineBlocker.MissingInput, MachinePhase.Paused),
            new ForcedState("storage-full", true, MachineBlocker.StorageFull, MachinePhase.Paused),
            new ForcedState("paused", true, MachineBlocker.None, MachinePhase.Paused),
            new ForcedState("inactive", false, MachineBlocker.Inactive, MachinePhase.Paused),
        };

        internal static ForcedState StateOf(int slot) => States[slot % States.Length];

        /// <summary>Even slots carry the boosted marker.</summary>
        public static bool BoostedAt(int slot) => slot % 2 == 0;

        /// <summary>Every third slot carries the Overdrive marker.</summary>
        public static bool OverdriveAt(int slot) => slot % 3 == 0;

        /// <summary>The forced visuals over the base visuals the W003-S run produced (12 occupied slots).</summary>
        public static SlotVisual[] Apply(IReadOnlyList<SlotVisual> baseVisuals)
        {
            if (baseVisuals == null) throw new ArgumentNullException(nameof(baseVisuals));
            if (baseVisuals.Count != SlotGridLayout.SlotCount) throw new ArgumentException("The base run must show 12 slots.", nameof(baseVisuals));
            var forced = new SlotVisual[baseVisuals.Count];
            for (var i = 0; i < forced.Length; i++)
            {
                var basis = baseVisuals[i];
                if (basis.Condition != SlotCondition.Occupied)
                {
                    throw new InvalidOperationException("W004-V needs the W003-S checkpoint with 12 occupied slots; slot " + (i + 1) + " is not occupied.");
                }

                var state = StateOf(i);
                var badge = SlotGridModel.BadgeFor(state.IsActive, state.Blocker, state.Phase, out var text, out var word);
                var boosted = BoostedAt(i);
                var overdrive = OverdriveAt(i);
                var description = "Slot " + (i + 1).ToString(CultureInfo.InvariantCulture) + ", " + word +
                    (boosted ? ", boosted by an Item" : string.Empty) + (overdrive ? ", Overdrive" : string.Empty);
                forced[i] = new SlotVisual(i, SlotCondition.Occupied, basis.BaseKey, basis.MachineKey, badge, text, boosted, overdrive,
                    i == SelectedSlot, 0, description);
            }

            return forced;
        }

        /// <summary>The fixture definition as stable text: base checkpoint hash, forced table and camera presets.</summary>
        public static string Canonical()
        {
            var b = new StringBuilder();
            b.Append(Name).Append("|base=").Append(W003PlayerFixtures.Stress.Scenario).Append(':').Append(W003PlayerFixtures.Stress.Sha256);
            b.Append("|selected=").Append(SelectedSlot);
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var s = StateOf(i);
                b.Append("|slot").Append(i).Append('=').Append(s.Name).Append(BoostedAt(i) ? "+boost" : string.Empty).Append(OverdriveAt(i) ? "+od" : string.Empty);
            }

            b.Append("|presets=nmin@").Append(LegacyCameraFraming.FitAllFloorPixelsPerCell.ToString("F1", CultureInfo.InvariantCulture)).Append("ppc,fitall");
            return b.ToString();
        }

        public static string Hash()
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Canonical()))).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static string PresetStem(W004VCameraPreset preset) => preset == W004VCameraPreset.NMin ? "nmin" : "fitall";

        internal readonly struct ForcedState
        {
            public ForcedState(string name, bool isActive, MachineBlocker blocker, MachinePhase phase)
            {
                Name = name;
                IsActive = isActive;
                Blocker = blocker;
                Phase = phase;
            }

            public string Name { get; }
            public bool IsActive { get; }
            public MachineBlocker Blocker { get; }
            public MachinePhase Phase { get; }
        }
    }

    /// <summary>
    /// The Feature 003 continuous camera framing (16 to 64 points per cell, fit-all floor 24) kept inside this fixture so the phase-0
    /// render parity frames (research R13) stay comparable after T427 replaced the production camera by the integer scale model. It
    /// writes only the fixture's own off-screen camera.
    /// </summary>
    internal sealed class LegacyCameraFraming
    {
        public const float MinimumPixelsPerCell = 16f;
        public const float MaximumPixelsPerCell = 64f;
        public const float FitAllFloorPixelsPerCell = 24f;

        private readonly Camera camera;
        private Rect worldBounds;
        private Rect viewport = new Rect(0f, 0f, 1f, 1f);

        public LegacyCameraFraming(Camera camera)
        {
            this.camera = camera;
        }

        public void ConfigureLimits(int columns, float pixelsPerPoint)
        {
            worldBounds = SlotGridLayout.Extent(columns);
            camera.orthographic = true;
        }

        public float FitAll(Rect visibleScreenArea, int columns, float pixelsPerPoint)
        {
            var fit = SlotGridLayout.FitAllPixelsPerCell(visibleScreenArea.width / pixelsPerPoint, visibleScreenArea.height / pixelsPerPoint, columns);
            var ppc = Mathf.Clamp(Mathf.Max(fit, FitAllFloorPixelsPerCell), MinimumPixelsPerCell, MaximumPixelsPerCell);
            camera.orthographicSize = camera.pixelHeight / pixelsPerPoint / (2f * ppc);
            SetViewport(visibleScreenArea);
            FrameContent(SlotGridLayout.Extent(columns));
            return ppc;
        }

        /// <summary>Zooms to the readability floor and centres the grid (the pre-scale-model stand-in for n_min).</summary>
        public float ApplyNMin(Rect area, int columns, float pixelsPerPoint)
        {
            var heightPoints = camera.pixelHeight / pixelsPerPoint;
            var ppc = Mathf.Clamp(FitAllFloorPixelsPerCell, MinimumPixelsPerCell, MaximumPixelsPerCell);
            camera.orthographicSize = Mathf.Clamp(heightPoints / (2f * ppc), heightPoints / (2f * MaximumPixelsPerCell), heightPoints / (2f * MinimumPixelsPerCell));
            SetViewport(area);
            FrameContent(SlotGridLayout.Extent(columns));
            return RunCameraFraming.PixelsPerCell(camera, pixelsPerPoint);
        }

        private void SetViewport(Rect screenRect)
        {
            var pixelRect = camera.pixelRect;
            var normalized = Rect.MinMaxRect(
                Mathf.Clamp01((screenRect.xMin - pixelRect.xMin) / pixelRect.width),
                Mathf.Clamp01((screenRect.yMin - pixelRect.yMin) / pixelRect.height),
                Mathf.Clamp01((screenRect.xMax - pixelRect.xMin) / pixelRect.width),
                Mathf.Clamp01((screenRect.yMax - pixelRect.yMin) / pixelRect.height));
            viewport = normalized.width > 0f && normalized.height > 0f ? normalized : new Rect(0f, 0f, 1f, 1f);
        }

        private void FrameContent(Rect content)
        {
            var worldHeight = camera.orthographicSize * 2f;
            var worldWidth = worldHeight * camera.aspect;
            var offset = viewport.center - new Vector2(0.5f, 0.5f);
            var target = content.center - new Vector2(offset.x * worldWidth, offset.y * worldHeight);
            var z = camera.transform.position.z;
            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * camera.aspect;
            var left = viewport.xMin * 2f * halfWidth;
            var right = (1f - viewport.xMax) * 2f * halfWidth;
            var bottom = viewport.yMin * 2f * halfHeight;
            var top = (1f - viewport.yMax) * 2f * halfHeight;
            var padding = 1f;
            var minX = worldBounds.xMin - padding + halfWidth - left;
            var maxX = worldBounds.xMax + padding - halfWidth + right;
            var minY = worldBounds.yMin - padding + halfHeight - bottom;
            var maxY = worldBounds.yMax + padding - halfHeight + top;
            var x = minX <= maxX ? Mathf.Clamp(target.x, minX, maxX) : worldBounds.center.x + (right - left) * 0.5f;
            var y = minY <= maxY ? Mathf.Clamp(target.y, minY, maxY) : worldBounds.center.y + (top - bottom) * 0.5f;
            camera.transform.position = new Vector3(x, y, z);
        }
    }
}
