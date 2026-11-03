using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// One key of the presentation catalog: an ordered, non-empty list of frames and an optional frames-per-second
    /// value. A single sprite is a one-frame entry (the default, and the shape the F001 scene serializes);
    /// consumers that do not animate use <see cref="Sprite"/>, which is frame 0. Keys are never renamed. An entry may
    /// also carry retained fallback frames (T428): the F003 placeholder with its own PPU, pivot and world size.
    /// </summary>
    [Serializable]
    public sealed class PresentationSpriteEntry
    {
        [SerializeField] private string key;
        [SerializeField] private Sprite sprite;
        [SerializeField] private List<Sprite> frames = new List<Sprite>();
        [SerializeField] private bool hasFramesPerSecond;
        [SerializeField] private float framesPerSecond;
        [SerializeField] private List<Sprite> fallbackFrames = new List<Sprite>();

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

        /// <summary>
        /// Attaches the retained fallback frames. The catalog uses them only when the primary frames fail validation
        /// and the fallback itself validates.
        /// </summary>
        public PresentationSpriteEntry WithFallback(params Sprite[] fallback) => WithFallback((IEnumerable<Sprite>)fallback);

        public PresentationSpriteEntry WithFallback(IEnumerable<Sprite> fallback)
        {
            fallbackFrames = fallback == null ? new List<Sprite>() : new List<Sprite>(fallback);
            return this;
        }

        /// <summary>The retained fallback frames in order; empty when the entry has none.</summary>
        public IReadOnlyList<Sprite> FallbackFrames => fallbackFrames ?? (IReadOnlyList<Sprite>)Array.Empty<Sprite>();

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
        {
            Key = key;
            WorldWidth = worldWidth;
            WorldHeight = worldHeight;
            PivotX = pivotX;
            PivotY = pivotY;
            MinFrames = minFrames;
            MaxFrames = maxFrames;
            Required = required;
        }

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
    /// documents (the <c>F001-*</c> development placeholders and the <c>F003-*</c> keys; Feature 004 adds <c>F004-</c>
    /// keys and restyles the rest). It is resolution-agnostic: every requirement validates a sprite's world size (pixel
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

        public const string ArtFallbackCode = "presentation.art.fallback";
        public const string KeyUnresolvedCode = "presentation.key.unresolved";

        private const float Tolerance = 0.001f;
        private const int MaxAnimatedFrames = 16;

        // T428 requirement registry. Architect decision (2026-10-30, confirming the 2026-10-27 re-validation):
        //  * Every row validates world size (pixels / PPU; one cell is one unit), normalized pivot and frame count and
        //    never pins a sprite name, so F004 art at PPU = D = 24 and the retained F003 fallback at 64 PPU both
        //    satisfy one row (a fallback keeps its own PPU, pivot and world size).
        //  * F001 keys are NOT start-up requirements: no runtime view uses them. Their rows stay as an optional table,
        //    validated only when an entry is present (development catalog, legacy scene entries).
        //  * F003 keys (what the views and content use) are validated when present. Their presence is enforced by the
        //    content resolver (presentation.key.unresolved) and F003AssetContractTests, so a smaller catalog stays legal.
        //  * Additive F004- keys carry their rows via Configure(entries, requirements); without a row they get the
        //    structural checks only. A Required row whose key is missing fails closed with presentation.key.unresolved.
        private static readonly Dictionary<string, PresentationSpriteRequirement> Registry = BuildRegistry();

        [SerializeField] private List<PresentationSpriteEntry> entries = new List<PresentationSpriteEntry>();
        private Dictionary<string, PresentationSpriteEntry> byKey;
        private readonly Dictionary<string, PresentationSpriteRequirement> extraRequirements =
            new Dictionary<string, PresentationSpriteRequirement>(StringComparer.Ordinal);
        private readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> fallbackKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> diagnostics = new List<string>();

        public bool IsValid { get; private set; }
        public string Error { get; private set; } = "Sprite catalog has not been validated.";
        public int Count => byKey?.Count ?? 0;

        /// <summary>
        /// Coded diagnostics, one per key and code, in the order raised: <c>presentation.art.fallback: KEY</c> when a key
        /// runs on its retained fallback frames and <c>presentation.key.unresolved: KEY</c> when neither set validates.
        /// A diagnostic never contains an asset path. Each is also logged once.
        /// </summary>
        public IReadOnlyList<string> Diagnostics => diagnostics;

        /// <summary>The keys that currently resolve to their retained fallback frames.</summary>
        public IReadOnlyCollection<string> FallbackKeys => fallbackKeys;

        public bool IsFallback(string key) => key != null && fallbackKeys.Contains(key);

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
            fallbackKeys.Clear();
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
                var primaryError = ValidateFrames(entry.Key, entry.FrameCount, entry.GetFrame, hasRequirement, requirement) ??
                                   ValidateRate(entry);
                if (primaryError == null)
                {
                    byKey[entry.Key] = entry;
                    continue;
                }

                var fallback = entry.FallbackFrames;
                if (fallback.Count > 0 &&
                    ValidateFrames(entry.Key, fallback.Count, index => fallback[index], hasRequirement, requirement) == null)
                {
                    // The primary art failed but the retained placeholder validates: run on it (a failure path, never shipped).
                    byKey[entry.Key] = new PresentationSpriteEntry(entry.Key, fallback, null);
                    fallbackKeys.Add(entry.Key);
                    Report(ArtFallbackCode, entry.Key);
                    continue;
                }

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

        private static string ValidateFrames(
            string key,
            int frameCount,
            Func<int, Sprite> frameAt,
            bool hasRequirement,
            PresentationSpriteRequirement requirement)
        {
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

            for (var frame = 0; frame < frameCount; frame++)
            {
                var sprite = frameAt(frame);
                var worldWidth = sprite.rect.width / sprite.pixelsPerUnit;
                var worldHeight = sprite.rect.height / sprite.pixelsPerUnit;
                if (Mathf.Abs(worldWidth - requirement.WorldWidth) > Tolerance ||
                    Mathf.Abs(worldHeight - requirement.WorldHeight) > Tolerance)
                {
                    return $"Presentation mapping '{key}' has the wrong world size.";
                }

                var pivotX = sprite.pivot.x / sprite.rect.width;
                var pivotY = sprite.pivot.y / sprite.rect.height;
                if (Mathf.Abs(pivotX - requirement.PivotX) > Tolerance ||
                    Mathf.Abs(pivotY - requirement.PivotY) > Tolerance)
                {
                    return $"Presentation mapping '{key}' has the wrong pivot.";
                }
            }

            return null;
        }

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

            // F003 keys. Slot plates and machine bodies are 2x2 cells with a bottom-left pivot.
            foreach (var key in new[]
                     {
                         "F003-SLOT-EMPTY", "F003-SLOT-LOCKED-L2", "F003-SLOT-LOCKED-L3", "F003-SLOT-LOCKED-L4",
                         "F003-SLOT-LOCKED-L5", "F003-SLOT-OCCUPIED-BASE", "F003-SLOT-SELECTED-FRAME",
                     })
            {
                Add(key, 2f, 2f, 0f, 1);
            }

            foreach (var key in new[] { "F003-MACHINE-MINER", "F003-MACHINE-SMELTER", "F003-MACHINE-PRESS", "F003-MACHINE-ASSEMBLER" })
            {
                Add(key, 2f, 2f, 0f, MaxAnimatedFrames);
            }

            foreach (var key in new[]
                     {
                         "F003-BADGE-RUNNING", "F003-BADGE-WAITING", "F003-BADGE-MISSING-INPUT", "F003-BADGE-STORAGE-FULL",
                         "F003-BADGE-PAUSED", "F003-BADGE-INACTIVE",
                     })
            {
                Add(key, 72f / 64f, 0.5f, 0.5f, MaxAnimatedFrames);
            }

            foreach (var key in new[] { "F003-MARKER-BOOSTED", "F003-MARKER-OVERDRIVE" })
            {
                Add(key, 28f / 64f, 28f / 64f, 0.5f, MaxAnimatedFrames);
            }

            foreach (var key in new[] { "F003-RES-IRON-ORE", "F003-RES-IRON-BAR", "F003-RES-IRON-PLATE", "F003-RES-GEAR" })
            {
                Add(key, 0.75f, 0.75f, 0.5f, 1);
            }

            foreach (var key in new[] { "F003-ITEM-DIAMOND-DRILL-HEAD", "F003-ITEM-FLUX-CATALYST", "F003-ITEM-OVERCLOCK-CORE" })
            {
                Add(key, 1f, 1f, 0.5f, 1);
            }

            foreach (var key in new[] { "F003-ITEM-TIER-1", "F003-ITEM-TIER-2", "F003-ITEM-TIER-3" })
            {
                Add(key, 0.75f, 0.375f, 0.5f, 1);
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
                Add("F003-UI-ICON-" + suffix, 0.75f, 0.75f, 0.5f, suffix == "SPINNER" ? MaxAnimatedFrames : 1);
            }

            AddF004Rows(map);
            return map;
        }

        // F004 canvas layers (Art Bible 3.2: 80 x 64 art px at PPU = D = 24, footprint pivot (40, 15)). Decision (T429, carry-forward
        // F4 of the #541 Architect review): the rows are static registry rows and not a Configure call at the bootstrap call site.
        // The scene path validates a serialized catalog with the parameterless Validate(), so rows passed only through
        // Configure(entries, requirements) would never reach it; one static table is the single source for the scene, the
        // development catalog and the tests. The rows are optional (required = false) until the F004 art is integrated (T437,
        // T442): a scene catalog without the accent or lock-label art stays legal and the views draw nothing for a missing key.
        private static void AddF004Rows(Dictionary<string, PresentationSpriteRequirement> map)
        {
            const float Width = 80f / 24f;
            const float Height = 64f / 24f;
            const float PivotX = 40f / 80f;
            const float PivotY = 15f / 64f;
            foreach (var machine in new[] { "MINER", "SMELTER", "PRESS", "ASSEMBLER" })
            {
                var key = AccentKeyPrefix + machine;
                map[key] = new PresentationSpriteRequirement(key, Width, Height, PivotX, PivotY, 1, MaxAnimatedFrames, false);
            }

            for (var level = 2; level <= 5; level++)
            {
                var key = LockLabelKeyPrefix + level.ToString(System.Globalization.CultureInfo.InvariantCulture);
                map[key] = new PresentationSpriteRequirement(key, Width, Height, PivotX, PivotY, 1, 1, false);
            }
        }
    }
}
