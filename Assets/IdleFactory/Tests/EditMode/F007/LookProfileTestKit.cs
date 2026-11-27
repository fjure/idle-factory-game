using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.Presentation.Look;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.EditMode.F007
{
    /// <summary>
    /// T609 test support: flat-colour sprites sized and pivoted by requirement rows, catalogs built from a table, and profiles over them. Everything it
    /// creates is destroyed by <see cref="Dispose"/>.
    /// </summary>
    internal sealed class LookProfileTestKit : IDisposable
    {
        private readonly List<Object> owned = new List<Object>();

        public Sprite MadeSprite(int width, int height, float pivotX, float pivotY, float pixelsPerUnit = 24f)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(pivotX, pivotY), pixelsPerUnit);
            owned.Add(texture);
            owned.Add(sprite);
            return sprite;
        }

        /// <summary>The frames a key needs to satisfy its row: <c>MinFrames</c> sprites of the row's world size and pivot at 24 pixels per unit.</summary>
        public PresentationSpriteEntry EntryFor(PresentationSpriteRequirement row, Func<PresentationSpriteRequirement, Sprite> spriteOf = null)
        {
            spriteOf = spriteOf ?? (r => MadeSprite(Mathf.RoundToInt(r.WorldWidth * 24f), Mathf.RoundToInt(r.WorldHeight * 24f), r.PivotX, r.PivotY));
            var frames = Enumerable.Range(0, row.MinFrames).Select(_ => spriteOf(row)).ToArray();
            return frames.Length == 1
                ? new PresentationSpriteEntry(row.Key, frames[0])
                : new PresentationSpriteEntry(row.Key, frames, 8f);
        }

        /// <summary>A catalog that registers every row of <paramref name="rows"/> except the keys <paramref name="skip"/> names, validated against the table.</summary>
        public PresentationSpriteCatalog CatalogFor(
            LookProfileRequirementTable table, bool requireRows, ICollection<string> skip = null, Func<PresentationSpriteRequirement, Sprite> spriteOf = null)
        {
            var catalog = new PresentationSpriteCatalog { RequireRegistryRows = requireRows };
            catalog.UseRequirementTable(table);
            var entries = table.Rows.Where(r => skip == null || !skip.Contains(r.Key)).Select(r => EntryFor(r, spriteOf)).ToList();
            catalog.Configure(entries);
            return catalog;
        }

        public LookProfile Profile(string id, bool prototype, PresentationSpriteCatalog catalog, LookProfileRequirementTable table, int pixelsPerUnit = 24)
        {
            var profile = LookProfile.Create(id, prototype, catalog, table, pixelsPerUnit);
            owned.Add(profile);
            return profile;
        }

        public LookProfile F006()
        {
            var profile = LookProfile.CreateF006();
            owned.Add(profile);
            return profile;
        }

        /// <summary>A three-row authored table (a machine canvas, a depot sprite and a glyph), all required.</summary>
        public static LookProfileRequirementTable SmallTable() => LookProfileRequirementTable.FromRows(new[]
        {
            new PresentationSpriteRequirement("T-MACHINE", 80f / 24f, 64f / 24f, 0.5f, 15f / 64f, 1, 4, true, true),
            new PresentationSpriteRequirement("T-DEPOT", 70f / 24f, 54f / 24f, 0.5f, 0.5f, 1, 1, true, false),
            new PresentationSpriteRequirement("T-GLYPH", 1f, 1f, 0.5f, 0.5f, 1, 1, true, false),
        });

        public void Dispose()
        {
            foreach (var item in owned)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            owned.Clear();
        }
    }

    /// <summary>A look-profile source the test drives by hand.</summary>
    internal sealed class FakeLookProfileSource : ILookProfileSource
    {
        private readonly Dictionary<string, LookProfile> held = new Dictionary<string, LookProfile>(StringComparer.Ordinal);

        public bool Ready { get; set; } = true;

        public string Selected { get; set; }

        public bool IsReady => Ready;

        public string SelectedProfileId => Selected;

        public event Action Changed;

        public FakeLookProfileSource Hold(LookProfile profile)
        {
            held[profile.Id] = profile;
            return this;
        }

        public bool TryGetProfile(string id, out LookProfile profile) => held.TryGetValue(id ?? string.Empty, out profile);

        public void Select(string id)
        {
            Selected = id;
            Changed?.Invoke();
        }

        public void MakeReady()
        {
            Ready = true;
            Changed?.Invoke();
        }
    }
}
