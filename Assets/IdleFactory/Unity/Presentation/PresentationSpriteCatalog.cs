using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// How a key's sprite is placed in a slot (T440). A canvas layer is a full 80 x 64 art-pixel layer with the footprint anchor baked into
    /// its pixels; a tight sprite (a 64 PPU development or unit-test catalog sprite) is placed by the view on the derived anchor.
    /// <see cref="Inferred"/> (the default) lets the catalog decide from the requirement row the frames validated against, and falls back
    /// to the sprite size only for a key without a row.
    /// </summary>
    public enum SpritePlacement
    {
        Inferred = 0,
        Canvas = 1,
        Tight = 2,
    }

    /// <summary>
    /// One key of the presentation catalog: an ordered, non-empty list of frames and an optional frames-per-second
    /// value. A single sprite is a one-frame entry (the default, and the shape the F001 scene serializes);
    /// consumers that do not animate use <see cref="Sprite"/>, which is frame 0. Keys are never renamed. An entry has no fallback
    /// frames (T476, owner decision 2026-11-05): frames that fail validation fail the catalog closed.
    /// </summary>
    [Serializable]
    public sealed class PresentationSpriteEntry
    {
        [SerializeField] private string key;
        [SerializeField] private Sprite sprite;
        [SerializeField] private List<Sprite> frames = new List<Sprite>();
        [SerializeField] private bool hasFramesPerSecond;
        [SerializeField] private float framesPerSecond;
        [SerializeField] private SpritePlacement placement;
        [NonSerialized] private SpritePlacement resolvedPlacement;

        /// <summary>A one-frame entry.</summary>
        public PresentationSpriteEntry(string key, Sprite sprite)
        {
            this.key = key;
            this.sprite = sprite;
        }

        /// <summary>An animated entry. An empty list, a null frame or a non-positive rate fails catalog validation.</summary>
        public PresentationSpriteEntry(string key, IEnumerable<Sprite> frames, float? framesPerSecond)
        {
            this.key = key;
            this.frames = frames == null ? new List<Sprite>() : new List<Sprite>(frames);
            hasFramesPerSecond = framesPerSecond.HasValue;
            this.framesPerSecond = framesPerSecond ?? 0f;
        }

        public string Key => key;

        /// <summary>The explicit placement of this entry; <see cref="SpritePlacement.Inferred"/> unless a producer states it.</summary>
        public SpritePlacement Placement => placement;

        public PresentationSpriteEntry WithPlacement(SpritePlacement value)
        {
            placement = value;
            return this;
        }

        /// <summary>
        /// The placement after catalog validation: the explicit <see cref="Placement"/>, else the shape of the requirement row that the
        /// frames matched (canvas layer or legacy tight sprite), else <see cref="SpritePlacement.Inferred"/> for a key without a row.
        /// </summary>
        public SpritePlacement ResolvedPlacement => placement != SpritePlacement.Inferred ? placement : resolvedPlacement;

        internal void SetResolvedPlacement(SpritePlacement value) => resolvedPlacement = value;

        /// <summary>
        /// True when <paramref name="frame"/> of this entry is a full canvas layer: the explicit or row-derived placement, and only for an
        /// unresolved entry the legacy inference from the sprite size (<see cref="StateCueGeometry.IsCanvasLayer"/>).
        /// </summary>
        public bool IsCanvasLayer(Sprite frame)
        {
            switch (ResolvedPlacement)
            {
                case SpritePlacement.Canvas: return frame != null;
                case SpritePlacement.Tight: return false;
                default: return StateCueGeometry.IsCanvasLayer(frame);
            }
        }

        /// <summary>Frame 0, or null when the entry has no frame.</summary>
        public Sprite Sprite => FrameCount == 0 ? null : GetFrame(0);

        /// <summary>The number of frames; a legacy single-sprite entry counts as one frame, a null one as zero.</summary>
        public int FrameCount => frames != null && frames.Count > 0 ? frames.Count : (sprite != null ? 1 : 0);

        /// <summary>The frame at an index; may be null for a malformed entry, which catalog validation rejects.</summary>
        public Sprite GetFrame(int index)
        {
            if (index < 0 || index >= FrameCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return frames != null && frames.Count > 0 ? frames[index] : sprite;
        }

        /// <summary>The configured frame rate, or null when the entry does not animate.</summary>
        public float? FramesPerSecond => hasFramesPerSecond ? framesPerSecond : (float?)null;
    }

    /// <summary>
    /// One row of the requirement registry: the world size (in world units, one cell is one unit), normalized pivot and
    /// allowed frame count that every frame of a key must satisfy. It never names a sprite.
    /// </summary>
    public readonly struct PresentationSpriteRequirement
    {
        public PresentationSpriteRequirement(
            string key, float worldWidth, float worldHeight, float pivotX, float pivotY, int minFrames, int maxFrames, bool required)
            : this(key, worldWidth, worldHeight, pivotX, pivotY, minFrames, maxFrames, required, false, false, 0f, 0f, 0f, 0f)
        {
        }

        /// <summary>A row whose primary shape is (or is not) a full canvas layer and that has no legacy shape.</summary>
        public PresentationSpriteRequirement(
            string key, float worldWidth, float worldHeight, float pivotX, float pivotY, int minFrames, int maxFrames, bool required,
            bool canvasLayer)
            : this(key, worldWidth, worldHeight, pivotX, pivotY, minFrames, maxFrames, required, canvasLayer, false, 0f, 0f, 0f, 0f)
        {
        }

        /// <summary>
        /// A row with a second accepted shape. The primary shape is the F004 art; the legacy (tight, 64 PPU) shape is what a development or
        /// unit-test catalog may carry for the key (T440). The shipped scene catalog never does (T476). <paramref name="canvasLayer"/> states
        /// that the primary shape is a full canvas layer.
        /// </summary>
        public PresentationSpriteRequirement(
            string key, float worldWidth, float worldHeight, float pivotX, float pivotY, int minFrames, int maxFrames, bool required,
            bool canvasLayer, float legacyWorldWidth, float legacyWorldHeight, float legacyPivotX, float legacyPivotY)
            : this(key, worldWidth, worldHeight, pivotX, pivotY, minFrames, maxFrames, required, canvasLayer, true,
                legacyWorldWidth, legacyWorldHeight, legacyPivotX, legacyPivotY)
        {
        }

        private PresentationSpriteRequirement(
            string key, float worldWidth, float worldHeight, float pivotX, float pivotY, int minFrames, int maxFrames, bool required,
            bool canvasLayer, bool hasLegacyShape, float legacyWorldWidth, float legacyWorldHeight, float legacyPivotX, float legacyPivotY)
        {
            Key = key;
            WorldWidth = worldWidth;
            WorldHeight = worldHeight;
            PivotX = pivotX;
            PivotY = pivotY;
            MinFrames = minFrames;
            MaxFrames = maxFrames;
            Required = required;
            CanvasLayer = canvasLayer;
            HasLegacyShape = hasLegacyShape;
            LegacyWorldWidth = legacyWorldWidth;
            LegacyWorldHeight = legacyWorldHeight;
            LegacyPivotX = legacyPivotX;
            LegacyPivotY = legacyPivotY;
        }

        /// <summary>True when the primary shape is a full canvas layer (80 x 64 art pixels at 24 PPU, footprint pivot).</summary>
        public bool CanvasLayer { get; }

        public bool HasLegacyShape { get; }
        public float LegacyWorldWidth { get; }
        public float LegacyWorldHeight { get; }
        public float LegacyPivotX { get; }
        public float LegacyPivotY { get; }

        public string Key { get; }
        public float WorldWidth { get; }
        public float WorldHeight { get; }
        public float PivotX { get; }
        public float PivotY { get; }
        public int MinFrames { get; }
        public int MaxFrames { get; }

        /// <summary>True when a missing key fails the catalog with <c>presentation.key.unresolved</c>.</summary>
        public bool Required { get; }
    }

    /// <summary>
    /// Explicit runtime registry of presentation sprites keyed by the stable presentation keys of the content
    /// documents (the <c>F001-*</c> development placeholders and the <c>F003-*</c> keys, which keep their identifiers while the shipped
    /// art behind them is the restyled F004 set; Feature 004 adds <c>F004-</c> keys). It is resolution-agnostic: every requirement validates a sprite's world size (pixel
    /// size divided by pixels per unit; one cell is one unit, one slot is two) and its normalized pivot, never fixed
    /// pixel dimensions or sprite names, so one key accepts art at 64 PPU, 32 PPU or 24 PPU unchanged.
    /// </summary>
    [Serializable]
    public sealed class PresentationSpriteCatalog
    {
        public const string GridTile = "F001-GRID-TILE-1X1";
        public const string BoundaryEdge = "F001-GRID-BOUNDARY-EDGE";
        public const string BoundaryCorner = "F001-GRID-BOUNDARY-CORNER";
        public const string D001Normal = "F001-MACHINE-D001-NORMAL";
        public const string D001Selected = "F001-MACHINE-D001-SELECTED";
        public const string AlternateNormal = "F001-MACHINE-ALT-3X1-NORMAL";
        public const string AlternateSelected = "F001-MACHINE-ALT-3X1-SELECTED";
        public const string D001GhostValid = "F001-GHOST-D001-VALID";
        public const string D001GhostInvalid = "F001-GHOST-D001-INVALID";
        public const string AlternateGhostValid = "F001-GHOST-ALT-3X1-VALID";
        public const string AlternateGhostInvalid = "F001-GHOST-ALT-3X1-INVALID";
        public const string ResourceInput = "F001-RESOURCE-DEV-INPUT";
        public const string ResourceOutput = "F001-RESOURCE-DEV-OUTPUT";
        public const string BadgeNormal = "F001-BADGE-NORMAL";
        public const string BadgeSelected = "F001-BADGE-SELECTED";
        public const string BadgePlacementValid = "F001-BADGE-PLACEMENT-VALID";
        public const string BadgePlacementInvalid = "F001-BADGE-PLACEMENT-INVALID";
        public const string BadgeProducing = "F001-BADGE-PRODUCING";
        public const string BadgeMissingInput = "F001-BADGE-MISSING-INPUT";
        public const string BadgeFullOutput = "F001-BADGE-FULL-OUTPUT";
        public const string BadgeInactive = "F001-BADGE-INACTIVE";

        /// <summary>The prefix of the additive Feature 004 keys (cue icons, body variants, markers).</summary>
        public const string F004Prefix = "F004-";

        /// <summary>The key prefix of the per-machine lighting accent layers (<c>F004-ACCENT-MINER</c> and so on).</summary>
        public const string AccentKeyPrefix = "F004-ACCENT-";

        /// <summary>The key prefix of the locked-plate digit labels (<c>F004-LABEL-LOCK-L2</c> to <c>-L5</c>).</summary>
        public const string LockLabelKeyPrefix = "F004-LABEL-LOCK-L";

        /// <summary>The prefix of the additive Feature 006 world-art keys (<c>F006-GROUND-BASE</c> and so on, the upper-cased asset id).</summary>
        public const string F006Prefix = "F006-";

        /// <summary>The additive F004 icon without a Feature 003 key.</summary>
        public const string SettingsIconKey = "F004-UI-ICON-SETTINGS";

        /// <summary>
        /// The floor tile (T471): one 70 x 54 art-pixel tile at PPU 24 (2.9167 x 2.25 cells, the lattice pitch of Art Bible 3.5), pivot (35, 27) on the
        /// slot footprint centre. It is not a canvas layer: a view places it on the slot pivot, one tile per lattice cell.
        /// </summary>
        public const string FloorTileKey = "F004-FLOOR-TILE";

        /// <summary>
        /// The running badge key. Its F004 cue is intentionally empty (owner decision R8): the shipped scene catalog has no entry for it and the
        /// view never draws it, so its registry row is the one F003 row that is not required (T476). Development catalogs may still carry it.
        /// </summary>
        public const string RunningBadgeKey = "F003-BADGE-RUNNING";

        public const string PlacementMismatchCode = "presentation.placement.mismatch";

        public const string KeyUnresolvedCode = "presentation.key.unresolved";

        private const float Tolerance = 0.001f;
        private const int MaxAnimatedFrames = 16;

        // T428 requirement registry. Architect decision (2026-10-30, confirming the 2026-10-27 re-validation), amended by T476
        // (owner decision 2026-11-05, option D: the Feature 003 placeholder sprites are deleted):
        //  * Every row validates world size (pixels / PPU; one cell is one unit), normalized pivot and frame count and
        //    never pins a sprite name, so F004 art at PPU = D = 24 and a 64 PPU tight sprite in a development or unit-test
        //    catalog both satisfy one row.
        //  * F001 keys are NOT start-up requirements: no runtime view uses them. Their rows stay as an optional table,
        //    validated only when an entry is present (development catalog, legacy scene entries).
        //  * F003 keys (what the views and content use) are validated when present and, in a catalog that sets RequireRegistryRows
        //    (the shipped scene), REQUIRED. There is no placeholder to fall back to, so art that is missing or fails validation fails
        //    the catalog closed with presentation.key.unresolved, and start-up reports presentation.catalog.invalid. Nothing is drawn
        //    blank and nothing is substituted. The one exception is the running badge, an intentionally empty cue (owner decision R8):
        //    the view never draws it, so its row is optional. Development and unit-test catalogs stay legal when smaller.
        //  * Additive F004- keys carry their rows via Configure(entries, requirements); without a row they get the
        //    structural checks only. A Required row whose key is missing fails closed with presentation.key.unresolved.
        private static readonly Dictionary<string, PresentationSpriteRequirement> Registry = BuildRegistry();

        [SerializeField] private List<PresentationSpriteEntry> entries = new List<PresentationSpriteEntry>();

        // T440: the shipped scene catalog sets this, so every required registry row (the F004 accent layers and lock labels) must be
        // registered, as it is for rows passed through Configure. Development and unit-test catalogs leave it off and stay legal without them.
        [SerializeField] private bool requireRegistryRows;
        private Dictionary<string, PresentationSpriteEntry> byKey;
        private readonly Dictionary<string, PresentationSpriteRequirement> extraRequirements =
            new Dictionary<string, PresentationSpriteRequirement>(StringComparer.Ordinal);
        private readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> diagnostics = new List<string>();

        public bool IsValid { get; private set; }
        public string Error { get; private set; } = "Sprite catalog has not been validated.";
        public int Count => byKey?.Count ?? 0;

        /// <summary>True when a missing key of a required registry row fails validation (the shipped scene catalog).</summary>
        public bool RequireRegistryRows
        {
            get => requireRegistryRows;
            set => requireRegistryRows = value;
        }

        /// <summary>
        /// Coded diagnostics, one per key and code, in the order raised: <c>presentation.key.unresolved: KEY</c> when the frames of a
        /// key fail validation or a required key is not registered (the catalog then fails closed; there is no fallback frame), and
        /// <c>presentation.placement.mismatch: KEY</c> when an explicit placement contradicts the matched shape.
        /// A diagnostic never contains an asset path. Each is also logged once.
        /// </summary>
        public IReadOnlyList<string> Diagnostics => diagnostics;

        public void Configure(IEnumerable<PresentationSpriteEntry> configuredEntries)
        {
            Configure(configuredEntries, null);
        }

        /// <summary>
        /// Configures and validates the catalog. <paramref name="requirements"/> adds or overrides registry rows, typically
        /// for additive <c>F004-</c> keys; a row marked required fails the catalog when its key is not registered.
        /// </summary>
        public void Configure(IEnumerable<PresentationSpriteEntry> configuredEntries, IEnumerable<PresentationSpriteRequirement> requirements)
        {
            entries = configuredEntries == null
                ? new List<PresentationSpriteEntry>()
                : new List<PresentationSpriteEntry>(configuredEntries);
            extraRequirements.Clear();
            if (requirements != null)
            {
                foreach (var requirement in requirements)
                {
                    extraRequirements[requirement.Key] = requirement;
                }
            }

            Validate();
        }

        public bool Validate()
        {
            byKey = new Dictionary<string, PresentationSpriteEntry>(StringComparer.Ordinal);
            IsValid = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    return Fail("Every presentation mapping requires an explicit key and at least one Sprite frame.");
                }

                if (!seen.Add(entry.Key))
                {
                    return Fail($"Duplicate presentation mapping '{entry.Key}'.");
                }
            }

            foreach (var entry in entries)
            {
                var hasRequirement = TryGetRequirement(entry.Key, out var requirement);
                var primaryError = ValidateFrames(entry.Key, entry.FrameCount, entry.GetFrame, hasRequirement, requirement, out var primaryShape) ??
                                   ValidateRate(entry);
                if (primaryError == null)
                {
                    var matchedPlacement = PlacementOf(primaryShape, requirement);
                    if (entry.Placement != SpritePlacement.Inferred && matchedPlacement != SpritePlacement.Inferred && entry.Placement != matchedPlacement)
                    {
                        // T471 (T440 review): an explicit placement still wins, but it contradicts the shape the frames matched; say so.
                        Report(PlacementMismatchCode, entry.Key);
                    }

                    entry.SetResolvedPlacement(matchedPlacement);
                    byKey[entry.Key] = entry;
                    continue;
                }

                // T476: no fallback frame exists. The key fails loudly, once, with a code and no path; the catalog fails closed.
                Report(KeyUnresolvedCode, entry.Key);
                return Fail(primaryError, true);
            }

            foreach (var requirement in extraRequirements.Values)
            {
                if (requirement.Required && !byKey.ContainsKey(requirement.Key))
                {
                    Report(KeyUnresolvedCode, requirement.Key);
                    return Fail($"Missing required presentation mapping '{requirement.Key}'.", true);
                }
            }

            if (requireRegistryRows)
            {
                foreach (var requirement in Registry.Values)
                {
                    if (requirement.Required && !extraRequirements.ContainsKey(requirement.Key) && !byKey.ContainsKey(requirement.Key))
                    {
                        Report(KeyUnresolvedCode, requirement.Key);
                        return Fail($"Missing required presentation mapping '{requirement.Key}'.", true);
                    }
                }
            }

            IsValid = true;
            Error = string.Empty;
            return true;
        }

        /// <summary>Frame 0 of the registered key; false for an unknown key.</summary>
        public bool TryGet(string key, out Sprite sprite)
        {
            sprite = null;
            if (!IsValid || key == null)
            {
                return false;
            }

            if (byKey.TryGetValue(key, out var entry))
            {
                sprite = entry.Sprite;
                return true;
            }

            return false;
        }

        /// <summary>The entry of a registered key (all frames and the frame rate); false for an unknown key.</summary>
        public bool TryGetEntry(string key, out PresentationSpriteEntry entry)
        {
            entry = null;
            return IsValid && key != null && byKey.TryGetValue(key, out entry);
        }

        public Sprite Get(string key)
        {
            if (!TryGet(key, out var sprite))
            {
                throw new InvalidOperationException(IsValid ? $"Unknown presentation key '{key}'." : Error);
            }

            return sprite;
        }

        private bool TryGetRequirement(string key, out PresentationSpriteRequirement requirement) =>
            extraRequirements.TryGetValue(key, out requirement) || Registry.TryGetValue(key, out requirement);

        private static string ValidateRate(PresentationSpriteEntry entry)
        {
            var rate = entry.FramesPerSecond;
            return rate.HasValue && !(rate.Value > 0f && !float.IsInfinity(rate.Value))
                ? $"Presentation mapping '{entry.Key}' has a non-positive frames-per-second value."
                : null;
        }

        private enum MatchedShape
        {
            None = 0,
            Primary = 1,
            Legacy = 2,
        }

        private static SpritePlacement PlacementOf(MatchedShape shape, PresentationSpriteRequirement requirement)
        {
            switch (shape)
            {
                case MatchedShape.Primary: return requirement.CanvasLayer ? SpritePlacement.Canvas : SpritePlacement.Inferred;
                case MatchedShape.Legacy: return SpritePlacement.Tight;
                default: return SpritePlacement.Inferred;
            }
        }

        private static string ValidateFrames(
            string key,
            int frameCount,
            Func<int, Sprite> frameAt,
            bool hasRequirement,
            PresentationSpriteRequirement requirement,
            out MatchedShape matched)
        {
            matched = MatchedShape.None;
            if (frameCount == 0)
            {
                return $"Presentation mapping '{key}' has no frame.";
            }

            for (var frame = 0; frame < frameCount; frame++)
            {
                if (frameAt(frame) == null)
                {
                    return $"Presentation mapping '{key}' has a null frame.";
                }
            }

            if (!hasRequirement)
            {
                return null;
            }

            if (frameCount < requirement.MinFrames || frameCount > requirement.MaxFrames)
            {
                return $"Presentation mapping '{key}' has {frameCount} frame(s); allowed {requirement.MinFrames} to {requirement.MaxFrames}.";
            }

            // Every frame of a key shares one shape: the row's primary (F004) shape or, when the row has one, its legacy (tight, 64 PPU) shape.
            var primaryMatches = true;
            var legacyMatches = requirement.HasLegacyShape;
            string firstFailure = null;
            for (var frame = 0; frame < frameCount; frame++)
            {
                var sprite = frameAt(frame);
                var worldWidth = sprite.rect.width / sprite.pixelsPerUnit;
                var worldHeight = sprite.rect.height / sprite.pixelsPerUnit;
                var pivotX = sprite.pivot.x / sprite.rect.width;
                var pivotY = sprite.pivot.y / sprite.rect.height;

                var primarySize = SizeMatches(worldWidth, worldHeight, requirement.WorldWidth, requirement.WorldHeight);
                var primaryFrame = primarySize && PivotMatches(pivotX, pivotY, requirement.PivotX, requirement.PivotY);
                var legacySize = false;
                var legacyFrame = false;
                if (requirement.HasLegacyShape)
                {
                    legacySize = SizeMatches(worldWidth, worldHeight, requirement.LegacyWorldWidth, requirement.LegacyWorldHeight);
                    legacyFrame = legacySize && PivotMatches(pivotX, pivotY, requirement.LegacyPivotX, requirement.LegacyPivotY);
                }

                primaryMatches &= primaryFrame;
                legacyMatches &= legacyFrame;
                if (!primaryFrame && !legacyFrame)
                {
                    // The first frame that fits no shape names the reason: a right size with a wrong pivot, or a wrong size.
                    firstFailure = firstFailure ?? (primarySize || legacySize
                        ? $"Presentation mapping '{key}' has the wrong pivot."
                        : $"Presentation mapping '{key}' has the wrong world size.");
                }
            }

            if (primaryMatches)
            {
                matched = MatchedShape.Primary;
                return null;
            }

            if (legacyMatches)
            {
                matched = MatchedShape.Legacy;
                return null;
            }

            // Frames that each fit a shape but not the same one are reported as a size failure.
            return firstFailure ?? $"Presentation mapping '{key}' has the wrong world size.";
        }

        private static bool SizeMatches(float width, float height, float expectedWidth, float expectedHeight) =>
            Mathf.Abs(width - expectedWidth) <= Tolerance && Mathf.Abs(height - expectedHeight) <= Tolerance;

        private static bool PivotMatches(float x, float y, float expectedX, float expectedY) =>
            Mathf.Abs(x - expectedX) <= Tolerance && Mathf.Abs(y - expectedY) <= Tolerance;

        // Once per code and key for the lifetime of the catalog instance; never includes a path. Always logged as a warning:
        // the diagnostic is data (Diagnostics), and LogError is fatal in the test runner and in the console gate, so even the
        // fail-closed presentation.key.unresolved path reports through Error/Diagnostics and not through the error log.
        private void Report(string code, string key)
        {
            var line = code + ": " + key;
            if (!reported.Add(line))
            {
                return;
            }

            diagnostics.Add(line);
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", line);
        }

        private bool Fail(string error, bool unresolved = false)
        {
            IsValid = false;
            Error = unresolved ? KeyUnresolvedCode + ": " + error : error;
            return false;
        }

        private static Dictionary<string, PresentationSpriteRequirement> BuildRegistry()
        {
            var map = new Dictionary<string, PresentationSpriteRequirement>(StringComparer.Ordinal);

            void Add(string key, float width, float height, float pivot, int maxFrames)
            {
                map[key] = new PresentationSpriteRequirement(key, width, height, pivot, pivot, 1, maxFrames, false);
            }

            // Optional F001 table (formerly the hard-coded start-up table).
            Add(GridTile, 1f, 1f, 0.5f, MaxAnimatedFrames);
            Add(BoundaryEdge, 1f, 1f, 0.5f, MaxAnimatedFrames);
            Add(BoundaryCorner, 1f, 1f, 0.5f, MaxAnimatedFrames);
            foreach (var key in new[] { D001Normal, D001Selected, D001GhostValid, D001GhostInvalid })
            {
                Add(key, 2f, 2f, 0f, MaxAnimatedFrames);
            }

            foreach (var key in new[] { AlternateNormal, AlternateSelected, AlternateGhostValid, AlternateGhostInvalid })
            {
                Add(key, 3f, 1f, 0f, MaxAnimatedFrames);
            }

            Add(ResourceInput, 0.5f, 0.5f, 0.5f, MaxAnimatedFrames);
            Add(ResourceOutput, 0.5f, 0.5f, 0.5f, MaxAnimatedFrames);
            foreach (var key in new[]
                     {
                         BadgeNormal, BadgeSelected, BadgePlacementValid, BadgePlacementInvalid, BadgeProducing,
                         BadgeMissingInput, BadgeFullOutput, BadgeInactive,
                     })
            {
                Add(key, 1f, 0.5f, 0.5f, MaxAnimatedFrames);
            }

            // F003 keys (T440). The primary shape of every row is the F004 art at PPU = D = 24 (Art Bible 3.2: full 80 x 64 art-pixel canvas
            // layers for plates, machines, badges and markers; 32 x 24 resources, 32 x 32 items, 32 x 16 tier chips, 24 x 24 icons). The
            // legacy shape is the tight 64 PPU sprite that a development catalog or a unit-test catalog may carry (the F003 placeholder
            // files themselves are deleted, T476). A frame set must match one shape as a whole, so a wrong size or pivot still fails the row.
            // Every row is required (T476): only a catalog that sets RequireRegistryRows, the shipped scene, enforces it.
            const float CanvasWidth = 80f / 24f;
            const float CanvasHeight = 64f / 24f;
            const float CanvasPivotX = 40f / 80f;
            const float CanvasPivotY = 15f / 64f;

            void AddCanvas(string key, int maxFrames, float legacyWidth, float legacyHeight, float legacyPivotX, float legacyPivotY)
            {
                map[key] = new PresentationSpriteRequirement(
                    key, CanvasWidth, CanvasHeight, CanvasPivotX, CanvasPivotY, 1, maxFrames, key != RunningBadgeKey,
                    true, legacyWidth, legacyHeight, legacyPivotX, legacyPivotY);
            }

            void AddCentred(string key, float width, float height, float pivotY, int maxFrames, float legacyWidth, float legacyHeight)
            {
                map[key] = new PresentationSpriteRequirement(
                    key, width, height, 0.5f, pivotY, 1, maxFrames, true,
                    false, legacyWidth, legacyHeight, 0.5f, 0.5f);
            }

            // Slot plates and machine bodies: the legacy tight shape is 2x2 cells with a bottom-left pivot.
            foreach (var key in new[]
                     {
                         "F003-SLOT-EMPTY", "F003-SLOT-LOCKED-L2", "F003-SLOT-LOCKED-L3", "F003-SLOT-LOCKED-L4",
                         "F003-SLOT-LOCKED-L5", "F003-SLOT-OCCUPIED-BASE", "F003-SLOT-SELECTED-FRAME",
                     })
            {
                AddCanvas(key, 1, 2f, 2f, 0f, 0f);
            }

            foreach (var key in new[] { "F003-MACHINE-MINER", "F003-MACHINE-SMELTER", "F003-MACHINE-PRESS", "F003-MACHINE-ASSEMBLER" })
            {
                AddCanvas(key, MaxAnimatedFrames, 2f, 2f, 0f, 0f);
            }

            foreach (var key in new[]
                     {
                         RunningBadgeKey, "F003-BADGE-WAITING", "F003-BADGE-MISSING-INPUT", "F003-BADGE-STORAGE-FULL",
                         "F003-BADGE-PAUSED", "F003-BADGE-INACTIVE",
                     })
            {
                AddCanvas(key, MaxAnimatedFrames, 72f / 64f, 0.5f, 0.5f, 0.5f);
            }

            foreach (var key in new[] { "F003-MARKER-BOOSTED", "F003-MARKER-OVERDRIVE" })
            {
                AddCanvas(key, MaxAnimatedFrames, 28f / 64f, 28f / 64f, 0.5f, 0.5f);
            }

            // Resources 32 x 24 px with the pivot at the footprint centre (16, 5).
            foreach (var key in new[] { "F003-RES-IRON-ORE", "F003-RES-IRON-BAR", "F003-RES-IRON-PLATE", "F003-RES-GEAR" })
            {
                AddCentred(key, 32f / 24f, 1f, 5f / 24f, 1, 0.75f, 0.75f);
            }

            foreach (var key in new[] { "F003-ITEM-DIAMOND-DRILL-HEAD", "F003-ITEM-FLUX-CATALYST", "F003-ITEM-OVERCLOCK-CORE" })
            {
                AddCentred(key, 32f / 24f, 32f / 24f, 0.5f, 1, 1f, 1f);
            }

            foreach (var key in new[] { "F003-ITEM-TIER-1", "F003-ITEM-TIER-2", "F003-ITEM-TIER-3" })
            {
                AddCentred(key, 32f / 24f, 16f / 24f, 0.5f, 1, 0.75f, 0.375f);
            }

            foreach (var suffix in new[]
                     {
                         "CASH", "RP", "IP", "FV", "STORAGE", "EXPAND-FACTORY", "TAB-FACTORY", "TAB-MARKET", "TAB-CONTRACTS",
                         "TAB-ITEMS", "TAB-RESEARCH", "TAB-PRESTIGE", "INDUSTRY", "BUILD", "UPGRADE", "REMOVE", "TOGGLE-ON",
                         "TOGGLE-OFF", "AUTOSELL", "PRIORITY-UP", "PRIORITY-DOWN", "DRAG-HANDLE", "ACCEPT", "DELIVER",
                         "ABANDON", "ACTIVATE", "CONFIRM", "CANCEL", "RETRY", "CLOSE", "INFO", "WARNING", "LOCK", "TIMER",
                         "OFFLINE", "IMPORT", "SPINNER", "OVERDRIVE-READY", "OVERDRIVE-ACTIVE", "OVERDRIVE-COOLDOWN",
                         "OVERDRIVE-LOCKED", "RESEARCH-NODE-LOCKED", "RESEARCH-NODE-AVAILABLE", "RESEARCH-NODE-OWNED",
                         "RESEARCH-NODE-MAXED", "EFFECT-SPEED", "EFFECT-OUTPUT", "EFFECT-SALE-PRICE", "EFFECT-UPGRADE-COST",
                     })
            {
                AddCentred("F003-UI-ICON-" + suffix, 1f, 1f, 0.5f, suffix == "SPINNER" ? MaxAnimatedFrames : 1, 0.75f, 0.75f);
            }

            // The one F004-only icon (a settings gear has no Feature 003 key): 24 x 24, centre pivot.
            map[SettingsIconKey] = new PresentationSpriteRequirement(SettingsIconKey, 1f, 1f, 0.5f, 0.5f, 1, 1, false);

            AddF004Rows(map);
            AddF006Rows(map);
            return map;
        }

        // F004 canvas layers (Art Bible 3.2: 80 x 64 art px at PPU = D = 24, footprint pivot (40, 15)). Decision (T429, carry-forward
        // F4 of the #541 Architect review): the rows are static registry rows and not a Configure call at the bootstrap call site.
        // The scene path validates a serialized catalog with the parameterless Validate(), so rows passed only through
        // Configure(entries, requirements) would never reach it; one static table is the single source for the scene, the
        // development catalog and the tests. T440 (art imported) flips the accent and lock-label rows to required = true. A required
        // registry row only fails a catalog that sets RequireRegistryRows (the shipped scene does), so a development or unit-test
        // catalog without the accent or lock-label art stays legal and the views draw nothing for a missing key.
        private static void AddF004Rows(Dictionary<string, PresentationSpriteRequirement> map)
        {
            const float Width = 80f / 24f;
            const float Height = 64f / 24f;
            const float PivotX = 40f / 80f;
            const float PivotY = 15f / 64f;
            foreach (var machine in new[] { "MINER", "SMELTER", "PRESS", "ASSEMBLER" })
            {
                var key = AccentKeyPrefix + machine;
                map[key] = new PresentationSpriteRequirement(key, Width, Height, PivotX, PivotY, 1, MaxAnimatedFrames, true, true);
            }

            for (var level = 2; level <= 5; level++)
            {
                var key = LockLabelKeyPrefix + level.ToString(System.Globalization.CultureInfo.InvariantCulture);
                map[key] = new PresentationSpriteRequirement(key, Width, Height, PivotX, PivotY, 1, 1, true, true);
            }

            // T471: the floor tile, 70 x 54 art px at PPU 24, pivot (35, 27) (centre). Not a canvas layer.
            map[FloorTileKey] = new PresentationSpriteRequirement(FloorTileKey, 70f / 24f, 54f / 24f, 0.5f, 0.5f, 1, 1, true, false);
        }

        // T526 (Feature 006): the imported world art. One row per shipped F006 key (40 static world frames plus the 8 FX entries, Art Bible
        // 21.4), derived from the approved source manifests: world size is canvas px / 24 (PPU = D = 24), the pivot is a whole art pixel
        // (rule P1), and the frame count is the sprite's frame count. None of these is a canvas layer (each is placed on its own anchor)
        // and none is required: the shipped scene catalog does not carry F006 keys until T527 wires the placement, and a required row would
        // fail the scene closed. T527 flips the rows it places to required.
        private static void AddF006Rows(Dictionary<string, PresentationSpriteRequirement> map)
        {
            // key, canvas width px, canvas height px, pivot x px, pivot y px, frames
            var rows = new (string Key, int Width, int Height, int PivotX, int PivotY, int Frames)[]
            {
            ("F006-DEPOSIT-HIGHLIGHT", 48, 34, 24, 12, 1),
            ("F006-DEPOSIT-REST-S1", 48, 34, 24, 12, 1),
            ("F006-DEPOSIT-REST-S2", 48, 34, 24, 12, 1),
            ("F006-DEPOSIT-REST-S3", 48, 34, 24, 12, 1),
            ("F006-DEPOT-S1-EMPTY", 80, 76, 40, 20, 1),
            ("F006-DEPOT-S1-FULL", 80, 76, 40, 20, 1),
            ("F006-DEPOT-S1-HIGH", 80, 76, 40, 20, 1),
            ("F006-DEPOT-S1-LOW", 80, 76, 40, 20, 1),
            ("F006-DEPOT-S2-EMPTY", 80, 76, 40, 20, 1),
            ("F006-DEPOT-S2-FULL", 80, 76, 40, 20, 1),
            ("F006-DEPOT-S2-HIGH", 80, 76, 40, 20, 1),
            ("F006-DEPOT-S2-LOW", 80, 76, 40, 20, 1),
            ("F006-DEPOT-S3-EMPTY", 80, 76, 40, 20, 1),
            ("F006-DEPOT-S3-FULL", 80, 76, 40, 20, 1),
            ("F006-DEPOT-S3-HIGH", 80, 76, 40, 20, 1),
            ("F006-DEPOT-S3-LOW", 80, 76, 40, 20, 1),
            ("F006-EDGE-GANTRY-CORNER", 70, 84, 35, 29, 1),
            ("F006-EDGE-GANTRY-FL", 70, 84, 35, 29, 1),
            ("F006-EDGE-GANTRY-FR", 70, 84, 35, 29, 1),
            ("F006-EDGE-RAILING-CORNER", 70, 66, 35, 29, 1),
            ("F006-EDGE-RAILING-FL", 70, 66, 35, 29, 1),
            ("F006-EDGE-RAILING-FR", 70, 66, 35, 29, 1),
            ("F006-EDGE-RAILING-QUIET-FL", 70, 66, 35, 29, 1),
            ("F006-EDGE-RAILING-QUIET-FR", 70, 66, 35, 29, 1),
            ("F006-EDGE-WALL-CORNER", 70, 78, 35, 29, 1),
            ("F006-EDGE-WALL-FL", 70, 78, 35, 29, 1),
            ("F006-EDGE-WALL-FR", 70, 78, 35, 29, 1),
            ("F006-FOUNDATION-FLAT", 70, 36, 35, 18, 1),
            ("F006-FOUNDATION-OUTLINE", 70, 36, 35, 18, 1),
            ("F006-FX-BRANCH-PIP", 14, 10, 7, 5, 6),
            ("F006-FX-BUILD-DUST", 48, 28, 24, 14, 6),
            ("F006-FX-COMPLETION-HIGHLIGHT", 68, 36, 34, 18, 1),
            ("F006-FX-COMPLETION-POP", 68, 36, 34, 18, 6),
            ("F006-FX-SPARK", 20, 16, 10, 8, 6),
            ("F006-FX-STEAM", 24, 32, 12, 0, 6),
            ("F006-FX-UPGRADE-GLINT", 24, 24, 12, 12, 6),
            ("F006-FX-VEIN-CHIP", 48, 34, 24, 12, 6),
            ("F006-GROUND-BASE", 70, 54, 35, 27, 1),
            ("F006-GROUND-DECK", 70, 54, 35, 27, 1),
            ("F006-GROUND-HATCH", 70, 54, 35, 27, 1),
            ("F006-LINK-BRANCH", 70, 54, 35, 27, 1),
            ("F006-LINK-BRANCH-ACTIVE", 70, 54, 35, 27, 1),
            ("F006-LINK-JUNCTION", 70, 54, 35, 27, 1),
            ("F006-LINK-TRUNK", 70, 54, 35, 27, 1),
            ("F006-PROP-CRATE-SMALL", 24, 20, 12, 5, 1),
            ("F006-PROP-CRATE-STACK", 30, 24, 15, 6, 1),
            ("F006-PROP-LAMP-POST", 14, 24, 7, 2, 1),
            ("F006-PROP-PALLET", 30, 16, 15, 6, 1),
            };
            foreach (var row in rows)
            {
                map[row.Key] = new PresentationSpriteRequirement(
                    row.Key, row.Width / 24f, row.Height / 24f, row.PivotX / (float)row.Width, row.PivotY / (float)row.Height,
                    row.Frames, row.Frames, false, false);
            }
        }
    }
}
