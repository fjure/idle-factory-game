using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// A synthetic world for the T527 deck tests that also runs in the Windows test player (no asset database): flat-colour sprites sized and pivoted by the
    /// registry rows of the real keys, the slot keys of a running Miner, and an authored-like composition. The deck code under test is the real one; only the
    /// art is a stand-in, so a pixel difference between frames can only come from motion in the code path.
    /// </summary>
    internal sealed class DeckTestRig
    {
        public const int D = 24;
        public readonly List<Object> Owned = new List<Object>();
        public const string MinerKey = "F003-MACHINE-MINER";

        public static readonly Color32 BaseColour = new Color32(40, 44, 56, 255);
        public static readonly Color32 DeckColour = new Color32(70, 80, 100, 255);
        public static readonly Color32 HatchColour = new Color32(120, 60, 60, 255);

        /// <summary>The number of cells that hold a tile (<c>Tilemap.GetUsedTilesCount</c> counts distinct tile assets, not cells).</summary>
        public static int TileCount(UnityEngine.Tilemaps.Tilemap tilemap)
        {
            var count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                count += tilemap.HasTile(position) ? 1 : 0;
            }

            return count;
        }

        public void Dispose()
        {
            foreach (var item in Owned)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            Owned.Clear();
        }

        private Sprite Solid(string name, int width, int height, float pivotX, float pivotY, Color32 colour, int ppu = D)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, name = name };
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = colour;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(pivotX / width, pivotY / height), ppu, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            Owned.Add(texture);
            Owned.Add(sprite);
            return sprite;
        }

        private static Color32 ColourOf(string key)
        {
            if (key.StartsWith("F006-EDGE-", StringComparison.Ordinal))
            {
                return new Color32(170, 180, 200, 255);
            }

            if (key.StartsWith("F006-PROP-", StringComparison.Ordinal))
            {
                return new Color32(220, 160, 60, 255);
            }

            return key == FactoryDeckView.DeckTileKey ? DeckColour : key == FactoryDeckView.HatchTileKey ? HatchColour : BaseColour;
        }

        /// <summary>A catalog with every key the grid and the deck draw: the slot keys, a 6-frame Miner and its accent, the badges and markers, and the placed F006 keys.</summary>
        public PresentationSpriteCatalog Catalog()
        {
            var entries = new List<PresentationSpriteEntry>();
            var requirements = new List<PresentationSpriteRequirement>();

            void AddCanvas(string key, int frames, float? fps)
            {
                var sprites = Enumerable.Range(0, frames).Select(f => Solid(key + "-" + f, 80, 64, 40, 15, new Color32((byte)(60 + 30 * f), 200, 200, 255))).ToArray();
                entries.Add(new PresentationSpriteEntry(key, sprites, fps));
                requirements.Add(new PresentationSpriteRequirement(key, 80f / D, 64f / D, 0.5f, 15f / 64f, 1, 16, false));
            }

            AddCanvas(SlotGridModel.SlotEmptyKey, 1, null);
            AddCanvas(SlotGridModel.SlotOccupiedKey, 1, null);
            for (var level = 2; level <= 5; level++)
            {
                AddCanvas(SlotGridModel.LockedKey(level), 1, null);
                AddCanvas(PresentationSpriteCatalog.LockLabelKeyPrefix + level, 1, null);
            }

            AddCanvas(SlotGridModel.SlotSelectedFrameKey, 1, null);
            AddCanvas(MinerKey, 6, 8f);
            AddCanvas(StateCueSet.AccentKeyFor(MinerKey), 6, 8f);
            foreach (MachineVisualState state in Enum.GetValues(typeof(MachineVisualState)))
            {
                AddCanvas(SlotGridModel.BadgeKeyOf(state), 1, null);
            }

            AddCanvas(SlotGridModel.MarkerBoostedKey, 1, null);
            AddCanvas(SlotGridModel.MarkerOverdriveKey, 1, null);
            foreach (var key in PresentationSpriteCatalog.PlacedF006Keys)
            {
                if (!DeckSpriteSize.TryOf(key, out var size))
                {
                    throw new InvalidOperationException("No registry row for " + key);
                }

                entries.Add(new PresentationSpriteEntry(key, Solid(key, size.Width, size.Height, size.PivotX, size.PivotY, ColourOf(key))));
                PresentationSpriteCatalog.TryGetRegistryRequirement(key, out var row);
                requirements.Add(row);
            }

            // T529: the F006 FX frames the effect pool draws (flat colour, the real registry size, pivot and frame count of each effect).
            foreach (EffectKind kind in Enum.GetValues(typeof(EffectKind)))
            {
                var key = EffectPool.SpriteKeyOf(kind);
                if (!PresentationSpriteCatalog.TryGetRegistryRequirement(key, out var row))
                {
                    throw new InvalidOperationException("No registry row for " + key);
                }

                var width = Mathf.RoundToInt(row.WorldWidth * D);
                var height = Mathf.RoundToInt(row.WorldHeight * D);
                var frames = Enumerable.Range(0, row.MinFrames)
                    .Select(f => Solid(key + "-" + f, width, height, row.PivotX * width, row.PivotY * height, new Color32((byte)(90 + 25 * f), 230, 120, 255)))
                    .ToArray();
                entries.Add(new PresentationSpriteEntry(key, frames, 10f));
                requirements.Add(row);
            }

            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(entries, requirements);
            if (!catalog.IsValid)
            {
                throw new InvalidOperationException(catalog.Error);
            }

            return catalog;
        }

        /// <summary>A composition like the authored asset: all four prop kinds at every stage, densities 0.2, 0.3 and 0.4.</summary>
        public WorldCompositionSettings Settings()
        {
            var settings = ScriptableObject.CreateInstance<WorldCompositionSettings>();
            Owned.Add(settings);
            var kinds = new[] { "crate-small", "crate-stack", "pallet", "lamp-post" };
            for (var stage = 0; stage < WorldCompositionSettings.StageCount; stage++)
            {
                settings.propWeightsPerStage[stage].weights = kinds.Select(k => new PropWeight { kind = k, weight = 1f }).ToArray();
                settings.propDensityPerStage[stage] = 0.2f + 0.1f * stage;
            }

            return settings;
        }

        public SlotGridView NewGrid(PresentationSpriteCatalog catalog, FactoryWorldLayout layout)
        {
            var host = new GameObject("deck-test-grid");
            Owned.Add(host);
            var view = host.AddComponent<SlotGridView>();
            view.Configure(catalog);
            view.SetLayout(layout);
            return view;
        }

        public FactoryDeckView NewDeck(PresentationSpriteCatalog catalog, WorldCompositionSettings settings, SlotGridView grid)
        {
            var host = new GameObject("deck-test-deck");
            Owned.Add(host);
            var deck = host.AddComponent<FactoryDeckView>();
            deck.Configure(catalog, settings);
            deck.Bind(grid);
            return deck;
        }

        public static SlotVisual[] Frame(int count, Func<int, SlotCondition> condition, bool running = true)
        {
            return Enumerable.Range(0, count).Select(i =>
            {
                var state = condition(i);
                return state == SlotCondition.Occupied
                    ? new SlotVisual(i, state, SlotGridModel.SlotOccupiedKey, MinerKey, running ? SlotGridModel.BadgeKeyOf(MachineVisualState.Running) : SlotGridModel.BadgeKeyOf(MachineVisualState.Waiting),
                        MachineVisualState.Running.ToString(), false, false, false, 0, "Slot " + (i + 1))
                    : new SlotVisual(i, state, state == SlotCondition.Locked ? SlotGridModel.LockedKey(2) : SlotGridModel.SlotEmptyKey, null, null, null, false, false, false,
                        state == SlotCondition.Locked ? 2 : 0, "Slot " + (i + 1));
            }).ToArray();
        }
    }
}
