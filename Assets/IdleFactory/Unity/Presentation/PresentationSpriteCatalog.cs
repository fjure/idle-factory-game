using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// One key of the presentation catalog: an ordered, non-empty list of frames and an optional frames-per-second
    /// value. A single sprite is a one-frame entry (the default, and the shape the F001 scene serializes);
    /// consumers that do not animate use <see cref="Sprite"/>, which is frame 0. Keys are never renamed.
    /// </summary>
    [Serializable]
    public sealed class PresentationSpriteEntry
    {
        [SerializeField] private string key;
        [SerializeField] private Sprite sprite;
        [SerializeField] private List<Sprite> frames = new List<Sprite>();
        [SerializeField] private bool hasFramesPerSecond;
        [SerializeField] private float framesPerSecond;

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
    /// Explicit runtime registry of presentation sprites keyed by the stable presentation keys of the content
    /// documents (the <c>F001-*</c> development placeholders and the <c>F003-*</c> keys; Feature 004 may restyle
    /// them). It is resolution-agnostic: every requirement validates a sprite's world size (pixel size divided by
    /// pixels per unit; one cell is one unit, one slot is two) and its normalized pivot, never fixed pixel
    /// dimensions, so one key accepts art at 64 PPU or 32 PPU unchanged.
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

        private const float Tolerance = 0.001f;

        /// <summary>
        /// Keys of the first Iron content document that T111 has not yet registered with imported art. Until T111, the
        /// catalog serves a generated placeholder for each so that the bootstrap's presentation-key resolution
        /// (<c>presentation.key.unresolved</c>) passes for the shipped content; any other unregistered key stays
        /// unresolved. T111 registers real entries, which always win over a placeholder.
        /// </summary>
        private static readonly string[] InterimPlaceholderKeys =
        {
            "F003-MACHINE-MINER", "F003-MACHINE-SMELTER", "F003-MACHINE-PRESS", "F003-MACHINE-ASSEMBLER",
            "F003-RES-IRON-ORE", "F003-RES-IRON-BAR", "F003-RES-IRON-PLATE", "F003-RES-GEAR",
            "F003-ITEM-OVERCLOCK-CORE", "F003-ITEM-DIAMOND-DRILL-HEAD", "F003-ITEM-FLUX-CATALYST",
        };

        // World size in world units (1 cell = 1 unit) and the normalized pivot of each approved F001 sprite.
        private static readonly SpriteRequirement[] Requirements =
        {
            new SpriteRequirement(GridTile, "f001-grid-tile-1x1-v01", 1f, 1f, 0.5f, 0.5f),
            new SpriteRequirement(BoundaryEdge, "f001-grid-boundary-edge-v01", 1f, 1f, 0.5f, 0.5f),
            new SpriteRequirement(BoundaryCorner, "f001-grid-boundary-corner-v01", 1f, 1f, 0.5f, 0.5f),
            new SpriteRequirement(D001Normal, "f001-machine-d001-normal-v01", 2f, 2f, 0f, 0f),
            new SpriteRequirement(D001Selected, "f001-machine-d001-selected-v01", 2f, 2f, 0f, 0f),
            new SpriteRequirement(AlternateNormal, "f001-machine-alt-3x1-normal-v01", 3f, 1f, 0f, 0f),
            new SpriteRequirement(AlternateSelected, "f001-machine-alt-3x1-selected-v01", 3f, 1f, 0f, 0f),
            new SpriteRequirement(D001GhostValid, "f001-ghost-d001-valid-v01", 2f, 2f, 0f, 0f),
            new SpriteRequirement(D001GhostInvalid, "f001-ghost-d001-invalid-v01", 2f, 2f, 0f, 0f),
            new SpriteRequirement(AlternateGhostValid, "f001-ghost-alt-3x1-valid-v01", 3f, 1f, 0f, 0f),
            new SpriteRequirement(AlternateGhostInvalid, "f001-ghost-alt-3x1-invalid-v01", 3f, 1f, 0f, 0f),
            new SpriteRequirement(ResourceInput, "f001-resource-dev-input-v01", 0.5f, 0.5f, 0.5f, 0.5f),
            new SpriteRequirement(ResourceOutput, "f001-resource-dev-output-v01", 0.5f, 0.5f, 0.5f, 0.5f),
            new SpriteRequirement(BadgeNormal, "f001-badge-normal-v01", 1f, 0.5f, 0.5f, 0.5f),
            new SpriteRequirement(BadgeSelected, "f001-badge-selected-v01", 1f, 0.5f, 0.5f, 0.5f),
            new SpriteRequirement(BadgePlacementValid, "f001-badge-placement-valid-v01", 1f, 0.5f, 0.5f, 0.5f),
            new SpriteRequirement(BadgePlacementInvalid, "f001-badge-placement-invalid-v01", 1f, 0.5f, 0.5f, 0.5f),
            new SpriteRequirement(BadgeProducing, "f001-badge-producing-v01", 1f, 0.5f, 0.5f, 0.5f),
            new SpriteRequirement(BadgeMissingInput, "f001-badge-missing-input-v01", 1f, 0.5f, 0.5f, 0.5f),
            new SpriteRequirement(BadgeFullOutput, "f001-badge-full-output-v01", 1f, 0.5f, 0.5f, 0.5f),
            new SpriteRequirement(BadgeInactive, "f001-badge-inactive-v01", 1f, 0.5f, 0.5f, 0.5f),
        };

        [SerializeField] private List<PresentationSpriteEntry> entries = new List<PresentationSpriteEntry>();
        private Dictionary<string, PresentationSpriteEntry> byKey;
        private Dictionary<string, Sprite> interimPlaceholders;

        public bool IsValid { get; private set; }
        public string Error { get; private set; } = "Sprite catalog has not been validated.";
        public int Count => byKey?.Count ?? 0;

        public void Configure(IEnumerable<PresentationSpriteEntry> configuredEntries)
        {
            entries = configuredEntries == null
                ? new List<PresentationSpriteEntry>()
                : new List<PresentationSpriteEntry>(configuredEntries);
            Validate();
        }

        public bool Validate()
        {
            byKey = new Dictionary<string, PresentationSpriteEntry>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    return Fail("Every presentation mapping requires an explicit key and at least one Sprite frame.");
                }

                if (entry.FrameCount == 0)
                {
                    return Fail($"Presentation mapping '{entry.Key}' has no frame.");
                }

                for (var frame = 0; frame < entry.FrameCount; frame++)
                {
                    if (entry.GetFrame(frame) == null)
                    {
                        return Fail($"Presentation mapping '{entry.Key}' has a null frame.");
                    }
                }

                var rate = entry.FramesPerSecond;
                if (rate.HasValue && !(rate.Value > 0f && !float.IsInfinity(rate.Value)))
                {
                    return Fail($"Presentation mapping '{entry.Key}' has a non-positive frames-per-second value.");
                }

                if (!byKey.TryAdd(entry.Key, entry))
                {
                    return Fail($"Duplicate presentation mapping '{entry.Key}'.");
                }
            }

            foreach (var requirement in Requirements)
            {
                if (!byKey.TryGetValue(requirement.Key, out var entry))
                {
                    return Fail($"Missing required presentation mapping '{requirement.Key}'.");
                }

                for (var frame = 0; frame < entry.FrameCount; frame++)
                {
                    var sprite = entry.GetFrame(frame);
                    if (!string.Equals(sprite.name, requirement.SpriteName, StringComparison.Ordinal))
                    {
                        return Fail($"Presentation mapping '{requirement.Key}' references the wrong approved sprite.");
                    }

                    var worldWidth = sprite.rect.width / sprite.pixelsPerUnit;
                    var worldHeight = sprite.rect.height / sprite.pixelsPerUnit;
                    if (Mathf.Abs(worldWidth - requirement.WorldWidth) > Tolerance ||
                        Mathf.Abs(worldHeight - requirement.WorldHeight) > Tolerance)
                    {
                        return Fail($"Presentation mapping '{requirement.Key}' has the wrong world size.");
                    }

                    var pivotX = sprite.pivot.x / sprite.rect.width;
                    var pivotY = sprite.pivot.y / sprite.rect.height;
                    if (Mathf.Abs(pivotX - requirement.PivotX) > Tolerance ||
                        Mathf.Abs(pivotY - requirement.PivotY) > Tolerance)
                    {
                        return Fail($"Presentation mapping '{requirement.Key}' has the wrong pivot.");
                    }
                }
            }

            IsValid = true;
            Error = string.Empty;
            return true;
        }

        /// <summary>Frame 0 of the key (a generated placeholder for an interim F003 key without art).</summary>
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

            return TryGetInterimPlaceholder(key, out sprite);
        }

        /// <summary>The entry of a registered key (all frames and the frame rate); false for a placeholder or an unknown key.</summary>
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

        private bool TryGetInterimPlaceholder(string key, out Sprite sprite)
        {
            sprite = null;
            if (Array.IndexOf(InterimPlaceholderKeys, key) < 0)
            {
                return false;
            }

            interimPlaceholders = interimPlaceholders ?? new Dictionary<string, Sprite>(StringComparer.Ordinal);
            if (!interimPlaceholders.TryGetValue(key, out sprite) || sprite == null)
            {
                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    name = "interim-placeholder-texture",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var pixels = new Color32[16];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(176, 176, 176, 255);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                sprite.name = key.ToLowerInvariant() + "-interim-placeholder";
                sprite.hideFlags = HideFlags.HideAndDontSave;
                interimPlaceholders[key] = sprite;
            }

            return true;
        }

        private bool Fail(string error)
        {
            IsValid = false;
            Error = error;
            return false;
        }

        private readonly struct SpriteRequirement
        {
            public SpriteRequirement(string key, string spriteName, float worldWidth, float worldHeight, float pivotX, float pivotY)
            {
                Key = key;
                SpriteName = spriteName;
                WorldWidth = worldWidth;
                WorldHeight = worldHeight;
                PivotX = pivotX;
                PivotY = pivotY;
            }

            public string Key { get; }
            public string SpriteName { get; }
            public float WorldWidth { get; }
            public float WorldHeight { get; }
            public float PivotX { get; }
            public float PivotY { get; }
        }
    }
}
