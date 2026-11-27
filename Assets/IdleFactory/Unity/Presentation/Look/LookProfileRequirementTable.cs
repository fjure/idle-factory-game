using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation.Look
{
    /// <summary>Where a <see cref="LookProfileRequirementTable"/> takes its rows from.</summary>
    public enum LookRequirementSource
    {
        /// <summary>The rows are authored in the profile asset (every profile except F006).</summary>
        Authored = 0,

        /// <summary>The rows are the static registry of <see cref="PresentationSpriteCatalog"/>, which is the F006 table (ARCH-5).</summary>
        F006Registry = 1,
    }

    /// <summary>One authored row of a requirement table: the key, the world size, the normalized pivot and the frame range of every frame of that key.</summary>
    [Serializable]
    public sealed class LookRequirementRow
    {
        public string key;
        public float worldWidth;
        public float worldHeight;
        public float pivotX = 0.5f;
        public float pivotY = 0.5f;
        public int minFrames = 1;
        public int maxFrames = 1;
        public bool required;
        public bool canvasLayer;

        public PresentationSpriteRequirement ToRequirement() =>
            new PresentationSpriteRequirement(key, worldWidth, worldHeight, pivotX, pivotY, minFrames, maxFrames, required, canvasLayer);
    }

    /// <summary>
    /// The requirement table of one look profile (contracts/look-profile.md rule 3, ARCH-5): the keys a profile must supply, with their sizes and pivots.
    /// A catalog validates against it at start-up and fails closed when a required key is missing or a frame has the wrong size or pivot; there is no
    /// fallback to another profile's table. The F006 table is the static registry that <see cref="PresentationSpriteCatalog"/> always carried, so the
    /// shipped look validates exactly as it did before profiles existed. Read-only after construction; lookups allocate nothing.
    /// </summary>
    [Serializable]
    public sealed class LookProfileRequirementTable
    {
        [SerializeField] private LookRequirementSource source;
        [SerializeField] private List<LookRequirementRow> rows = new List<LookRequirementRow>();

        [NonSerialized] private Dictionary<string, PresentationSpriteRequirement> authored;

        /// <summary>The F006 table (the static registry). Cheap: the rows are shared, nothing is copied.</summary>
        public static LookProfileRequirementTable F006() => new LookProfileRequirementTable { source = LookRequirementSource.F006Registry };

        /// <summary>An authored table from explicit rows (a prototype or a test profile).</summary>
        public static LookProfileRequirementTable FromRows(IEnumerable<PresentationSpriteRequirement> requirements)
        {
            if (requirements == null)
            {
                throw new ArgumentNullException(nameof(requirements));
            }

            var table = new LookProfileRequirementTable { source = LookRequirementSource.Authored };
            foreach (var row in requirements)
            {
                table.rows.Add(new LookRequirementRow
                {
                    key = row.Key,
                    worldWidth = row.WorldWidth,
                    worldHeight = row.WorldHeight,
                    pivotX = row.PivotX,
                    pivotY = row.PivotY,
                    minFrames = row.MinFrames,
                    maxFrames = row.MaxFrames,
                    required = row.Required,
                    canvasLayer = row.CanvasLayer,
                });
            }

            return table;
        }

        public LookRequirementSource Source => source;

        /// <summary>Every row of the table.</summary>
        public IReadOnlyCollection<PresentationSpriteRequirement> Rows =>
            source == LookRequirementSource.F006Registry ? PresentationSpriteCatalog.F006RegistryRows : Authored().Values;

        public int Count => Rows.Count;

        public bool TryGet(string key, out PresentationSpriteRequirement requirement)
        {
            if (key == null)
            {
                requirement = default;
                return false;
            }

            return source == LookRequirementSource.F006Registry
                ? PresentationSpriteCatalog.TryGetRegistryRequirement(key, out requirement)
                : Authored().TryGetValue(key, out requirement);
        }

        /// <summary>The first problem of the table (a blank or duplicate key, a non-positive size, an empty frame range), or null when it is sound.</summary>
        public string FirstProblem()
        {
            if (source == LookRequirementSource.F006Registry)
            {
                return null;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.key))
                {
                    return "A requirement row has no key.";
                }

                if (!seen.Add(row.key))
                {
                    return "Duplicate requirement row '" + row.key + "'.";
                }

                if (!(row.worldWidth > 0f) || !(row.worldHeight > 0f))
                {
                    return "Requirement row '" + row.key + "' has a non-positive world size.";
                }

                if (row.minFrames < 1 || row.maxFrames < row.minFrames)
                {
                    return "Requirement row '" + row.key + "' has an invalid frame range.";
                }
            }

            return null;
        }

        private Dictionary<string, PresentationSpriteRequirement> Authored()
        {
            if (authored == null)
            {
                var map = new Dictionary<string, PresentationSpriteRequirement>(StringComparer.Ordinal);
                foreach (var row in rows)
                {
                    if (row != null && !string.IsNullOrWhiteSpace(row.key))
                    {
                        map[row.key] = row.ToRequirement();
                    }
                }

                authored = map;
            }

            return authored;
        }
    }
}
