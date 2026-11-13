using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace IdleFactory.Editor.Art
{
    /// <summary>
    /// T430 (FR-023, research R10, Art Bible budget 7): the settings every F004 Sprite Atlas v2 carries. One atlas per family
    /// (<c>Assets/IdleFactory/Art/F004/Atlases/&lt;family&gt;.spriteatlasv2</c>); RGBA32 uncompressed, point filter, no mipmaps,
    /// padding 2, no rotation, pages at most 512 x 512 (T552, see <see cref="MaxPageBytes"/>), with explicit overrides for Default, Standalone and Android (Android
    /// otherwise falls back to block compression, which smears pixel edges). No family has production content yet, so atlas assets
    /// are created by <see cref="CreateFamilyAtlas"/> when the family's art is imported (T440), not before.
    /// </summary>
    public static class F004SpriteAtlasSettings
    {
        public const string AtlasFolder = F004ImportRules.ArtRoot + "/Atlases";
        public const string AtlasExtension = ".spriteatlasv2";
        public const int Padding = 2;
        public const int MaxPageSize = 512;

        /// <summary>
        /// Largest RGBA32 page an atlas may ship (512 x 512). T552: the Android GLES3 player never finished loading a scene that
        /// referenced a 512 x 1024 (2 MiB) page, with the loading thread at 100 % CPU, while pages of 1 MiB loaded in about 3 s.
        /// A family that needs more pixels becomes extra pages of the same atlas (the packer does this at the page limit).
        /// </summary>
        public const int MaxPageBytes = MaxPageSize * MaxPageSize * 4;

        /// <summary>Platform names the importer accepts; the first is the Default override.</summary>
        public static readonly string[] Platforms = { "DefaultTexturePlatform", "Standalone", "Android" };

        /// <summary>Creates (or reconfigures) a v2 atlas asset and packs the given folders or sprites into it.</summary>
        public static SpriteAtlasImporter CreateFamilyAtlas(string atlasAssetPath, params Object[] packables)
        {
            if (AssetImporter.GetAtPath(atlasAssetPath) == null)
            {
                var asset = new SpriteAtlasAsset();
                if (packables != null && packables.Length > 0)
                {
                    asset.Add(packables);
                }

                SpriteAtlasAsset.Save(asset, atlasAssetPath);
                AssetDatabase.ImportAsset(atlasAssetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(atlasAssetPath);
            Configure(importer);
            importer.SaveAndReimport();
            return importer;
        }

        /// <summary>Writes the F004 atlas settings into an importer (does not save).</summary>
        public static void Configure(SpriteAtlasImporter importer)
        {
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                blockOffset = 1,
                padding = Padding,
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = false,
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Point,
                anisoLevel = 0,
            };
            foreach (var platform in Platforms)
            {
                importer.SetPlatformSettings(new TextureImporterPlatformSettings
                {
                    name = platform,
                    overridden = true,
                    maxTextureSize = MaxPageSize,
                    format = TextureImporterFormat.RGBA32,
                    textureCompression = TextureImporterCompression.Uncompressed,
                    crunchedCompression = false,
                });
            }
        }

        /// <summary>Violations of the atlas contract; empty when the importer matches.</summary>
        public static List<string> Validate(SpriteAtlasImporter importer)
        {
            var problems = new List<string>();
            var packing = importer.packingSettings;
            if (packing.padding != Padding)
            {
                problems.Add("padding " + packing.padding + " (expected " + Padding + ")");
            }

            if (packing.enableRotation)
            {
                problems.Add("rotation enabled");
            }

            if (packing.enableTightPacking)
            {
                problems.Add("tight packing enabled");
            }

            var texture = importer.textureSettings;
            if (texture.generateMipMaps)
            {
                problems.Add("mipmaps enabled");
            }

            if (texture.filterMode != FilterMode.Point)
            {
                problems.Add("filter " + texture.filterMode + " (expected Point)");
            }

            if (!texture.sRGB)
            {
                problems.Add("sRGB disabled");
            }

            if (texture.readable)
            {
                problems.Add("read/write enabled");
            }

            foreach (var platform in Platforms)
            {
                var settings = importer.GetPlatformSettings(platform);
                if (!settings.overridden)
                {
                    problems.Add(platform + ": no explicit override");
                    continue;
                }

                if (settings.format != TextureImporterFormat.RGBA32)
                {
                    problems.Add(platform + ": format " + settings.format + " (expected RGBA32)");
                }

                if (settings.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    problems.Add(platform + ": compression " + settings.textureCompression + " (expected Uncompressed)");
                }

                if (settings.maxTextureSize > MaxPageSize)
                {
                    problems.Add(platform + ": max page " + settings.maxTextureSize + " (limit " + MaxPageSize + ")");
                }
            }

            return problems;
        }

        /// <summary>Every F004 atlas asset path outside the style-spike folder.</summary>
        public static List<string> FindAtlasPaths()
        {
            var result = new List<string>();
            if (!System.IO.Directory.Exists(F004ImportRules.ArtRoot))
            {
                return result;
            }

            foreach (var file in System.IO.Directory.GetFiles(F004ImportRules.ArtRoot, "*" + AtlasExtension, System.IO.SearchOption.AllDirectories))
            {
                var path = file.Replace('\\', '/');
                if (!path.StartsWith(F004ImportRules.SpikeRoot + "/", System.StringComparison.Ordinal))
                {
                    result.Add(path);
                }
            }

            result.Sort(System.StringComparer.Ordinal);
            return result;
        }
    }
}
