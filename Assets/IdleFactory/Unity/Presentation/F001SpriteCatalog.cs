using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    [Serializable]
    public sealed class F001SpriteEntry
    {
        [SerializeField] private string key;
        [SerializeField] private Sprite sprite;

        public F001SpriteEntry(string key, Sprite sprite)
        {
            this.key = key;
            this.sprite = sprite;
        }

        public string Key => key;
        public Sprite Sprite => sprite;
    }

    /// <summary>Explicit runtime registry for the approved F001 sprite set.</summary>
    [Serializable]
    public sealed class F001SpriteCatalog
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

        private static readonly AssetRequirement[] Requirements =
        {
            new AssetRequirement(GridTile, "f001-grid-tile-1x1-v01", 64, 64),
            new AssetRequirement(BoundaryEdge, "f001-grid-boundary-edge-v01", 64, 64),
            new AssetRequirement(BoundaryCorner, "f001-grid-boundary-corner-v01", 64, 64),
            new AssetRequirement(D001Normal, "f001-machine-d001-normal-v01", 128, 128),
            new AssetRequirement(D001Selected, "f001-machine-d001-selected-v01", 128, 128),
            new AssetRequirement(AlternateNormal, "f001-machine-alt-3x1-normal-v01", 192, 64),
            new AssetRequirement(AlternateSelected, "f001-machine-alt-3x1-selected-v01", 192, 64),
            new AssetRequirement(D001GhostValid, "f001-ghost-d001-valid-v01", 128, 128),
            new AssetRequirement(D001GhostInvalid, "f001-ghost-d001-invalid-v01", 128, 128),
            new AssetRequirement(AlternateGhostValid, "f001-ghost-alt-3x1-valid-v01", 192, 64),
            new AssetRequirement(AlternateGhostInvalid, "f001-ghost-alt-3x1-invalid-v01", 192, 64),
            new AssetRequirement(ResourceInput, "f001-resource-dev-input-v01", 32, 32),
            new AssetRequirement(ResourceOutput, "f001-resource-dev-output-v01", 32, 32),
            new AssetRequirement(BadgeNormal, "f001-badge-normal-v01", 64, 32),
            new AssetRequirement(BadgeSelected, "f001-badge-selected-v01", 64, 32),
            new AssetRequirement(BadgePlacementValid, "f001-badge-placement-valid-v01", 64, 32),
            new AssetRequirement(BadgePlacementInvalid, "f001-badge-placement-invalid-v01", 64, 32),
            new AssetRequirement(BadgeProducing, "f001-badge-producing-v01", 64, 32),
            new AssetRequirement(BadgeMissingInput, "f001-badge-missing-input-v01", 64, 32),
            new AssetRequirement(BadgeFullOutput, "f001-badge-full-output-v01", 64, 32),
            new AssetRequirement(BadgeInactive, "f001-badge-inactive-v01", 64, 32),
        };

        [SerializeField] private List<F001SpriteEntry> entries = new List<F001SpriteEntry>();
        private Dictionary<string, Sprite> sprites;

        public bool IsValid { get; private set; }
        public string Error { get; private set; } = "Sprite catalog has not been validated.";
        public int Count => sprites?.Count ?? 0;

        public void Configure(IEnumerable<F001SpriteEntry> configuredEntries)
        {
            entries = configuredEntries == null
                ? new List<F001SpriteEntry>()
                : new List<F001SpriteEntry>(configuredEntries);
            Validate();
        }

        public bool Validate()
        {
            sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Sprite == null)
                {
                    return Fail("Every F001 mapping requires an explicit key and Sprite reference.");
                }

                if (!sprites.TryAdd(entry.Key, entry.Sprite))
                {
                    return Fail($"Duplicate F001 mapping '{entry.Key}'.");
                }
            }

            foreach (var requirement in Requirements)
            {
                if (!sprites.TryGetValue(requirement.Key, out var sprite))
                {
                    return Fail($"Missing required F001 mapping '{requirement.Key}'.");
                }

                if (!string.Equals(sprite.name, requirement.SpriteName, StringComparison.Ordinal) ||
                    sprite.rect.width != requirement.Width || sprite.rect.height != requirement.Height)
                {
                    return Fail($"F001 mapping '{requirement.Key}' references the wrong approved sprite.");
                }
            }

            if (sprites.Count != Requirements.Length)
            {
                return Fail("F001 catalog must contain exactly the 21 approved mappings.");
            }

            IsValid = true;
            Error = string.Empty;
            return true;
        }

        public bool TryGet(string key, out Sprite sprite)
        {
            sprite = null;
            return IsValid && key != null && sprites.TryGetValue(key, out sprite);
        }

        public Sprite Get(string key)
        {
            if (!TryGet(key, out var sprite))
            {
                throw new InvalidOperationException(IsValid ? $"Unknown F001 key '{key}'." : Error);
            }

            return sprite;
        }

        private bool Fail(string error)
        {
            IsValid = false;
            Error = error;
            return false;
        }

        private readonly struct AssetRequirement
        {
            public AssetRequirement(string key, string spriteName, int width, int height)
            {
                Key = key;
                SpriteName = spriteName;
                Width = width;
                Height = height;
            }

            public string Key { get; }
            public string SpriteName { get; }
            public int Width { get; }
            public int Height { get; }
        }
    }
}
