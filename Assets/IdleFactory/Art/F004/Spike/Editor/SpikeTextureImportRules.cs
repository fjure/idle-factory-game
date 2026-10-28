using UnityEditor;
using UnityEngine;

namespace IdleFactory.Art.F004.Spike.Editor
{
    /// <summary>
    /// Per-density importer rules of the F004 style spike (research R16, FR-007). Any PNG placed under
    /// Assets/IdleFactory/Art/F004/Spike/D&lt;k&gt;/ is imported as a single sprite with pixels per unit = k, point filter,
    /// no mipmaps and uncompressed RGBA32, on every (re)import. This makes D16 and D32 correct before they hold any asset.
    /// The pivot is not touched here: it is a per-asset value from the asset manifest and lives in each .meta file.
    /// The spike path is deleted or promoted only by an explicit decision.
    /// </summary>
    public sealed class SpikeTextureImportRules : AssetPostprocessor
    {
        public const string SpikeRoot = "Assets/IdleFactory/Art/F004/Spike/";
        public static readonly int[] Densities = { 16, 24, 32 };

        /// <summary>The density k of a path under Spike/D&lt;k&gt;/, or 0 when the path is not under a density folder.</summary>
        public static int DensityOf(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(SpikeRoot, System.StringComparison.Ordinal))
            {
                return 0;
            }

            foreach (var density in Densities)
            {
                if (assetPath.StartsWith(SpikeRoot + "D" + density + "/", System.StringComparison.Ordinal))
                {
                    return density;
                }
            }

            return 0;
        }

        /// <summary>Applies the per-density settings to an importer.</summary>
        public static void Apply(TextureImporter importer, int density)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = density;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        private void OnPreprocessTexture()
        {
            var density = DensityOf(assetPath);
            if (density > 0)
            {
                Apply((TextureImporter)assetImporter, density);
            }
        }
    }
}
