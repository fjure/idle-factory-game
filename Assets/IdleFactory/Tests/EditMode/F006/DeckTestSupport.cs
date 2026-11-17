using System;
using System.Collections.Generic;
using System.IO;
using IdleFactory.Unity.Presentation;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>The pixels of an imported world sprite (lower-left origin), read from its PNG: the deck proofs measure real opaque pixels.</summary>
    internal sealed class SpritePixels
    {
        public SpritePixels(string path)
        {
            Path = path;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
                {
                    throw new InvalidDataException(path);
                }

                Width = texture.width;
                Height = texture.height;
                Pixels = texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        public string Path { get; }
        public int Width { get; }
        public int Height { get; }
        public Color32[] Pixels { get; }

        public bool IsOpaque(int x, int y) => Pixels[y * Width + x].a > 0;

        /// <summary>The highest opaque row above the bottom edge plus one (0 when empty).</summary>
        public int OpaqueTop()
        {
            for (var y = Height - 1; y >= 0; y--)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (IsOpaque(x, y))
                    {
                        return y + 1;
                    }
                }
            }

            return 0;
        }
    }

    /// <summary>Shared layouts, settings and art paths of the T527 deck tests.</summary>
    internal static class DeckTestSupport
    {
        public const string ArtRoot = "Assets/IdleFactory/Art/F006";

        public static readonly string[] LayoutNames = { "3x4", "4x3", TestLayouts.SixName, TestLayouts.TwentyName, TestLayouts.LName };

        public static FactoryWorldLayout Layout(string name, WorldCompositionSettings settings = null)
        {
            switch (name)
            {
                case "3x4": return new LatticeLayoutBuilder(LatticeLayoutBuilder.ShippedLocationCount, WorldOrientation.Portrait, settings).Build();
                case "4x3": return new LatticeLayoutBuilder(LatticeLayoutBuilder.ShippedLocationCount, WorldOrientation.Landscape, settings).Build();
                default: return TestLayouts.ByName(name, settings);
            }
        }

        public static WorldCompositionSettings Settings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<WorldCompositionSettings>(WorldCompositionSettings.AssetPath);
            if (asset == null)
            {
                throw new FileNotFoundException(WorldCompositionSettings.AssetPath);
            }

            return asset;
        }

        /// <summary>The asset path of an F006 key (<c>F006-EDGE-RAILING-FL</c> gives <c>edge/f006-edge-railing-fl_00.png</c>).</summary>
        public static string ArtPathOf(string key, int frame = 0)
        {
            var parts = key.Split('-');
            var family = parts[1].ToLowerInvariant();
            return ArtRoot + "/" + family + "/" + key.ToLowerInvariant() + "_" + frame.ToString("00") + ".png";
        }

        private static readonly Dictionary<string, SpritePixels> Cache = new Dictionary<string, SpritePixels>(StringComparer.Ordinal);

        public static SpritePixels Pixels(string assetPath)
        {
            if (!Cache.TryGetValue(assetPath, out var pixels))
            {
                pixels = new SpritePixels(assetPath);
                Cache[assetPath] = pixels;
            }

            return pixels;
        }

        public static SpritePixels PixelsOfKey(string key, int frame = 0) => Pixels(ArtPathOf(key, frame));

        public static IReadOnlyList<SlotCondition> All(int count, SlotCondition condition)
        {
            var result = new SlotCondition[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = condition;
            }

            return result;
        }
    }
}
