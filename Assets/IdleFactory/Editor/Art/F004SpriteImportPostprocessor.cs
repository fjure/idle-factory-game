using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Editor.Art
{
    /// <summary>
    /// T430 (FR-022, research R10): applies pixels per unit = D, the manifest pivot, point filter, no mipmaps, clamp and
    /// uncompressed RGBA32 to every PNG that <c>Assets/IdleFactory/Art/F004/import-manifest.json</c> lists, and only to those:
    /// the F003 placeholders (64 PPU), the style-spike folder and PNGs the manifest does not list are untouched. The manifest
    /// is registered as an import dependency and the postprocessor is versioned, so a manifest edit or a rule change re-imports
    /// the sprites. An invalid schema version, entry, pivot or PNG hash is a logged error and applies nothing.
    /// </summary>
    public sealed class F004SpriteImportPostprocessor : AssetPostprocessor
    {

        public override uint GetVersion()
        {
            return F004ImportRules.PostprocessorVersion;
        }

        /// <summary>Applies an accepted decision to an importer.</summary>
        public static void Apply(TextureImporter importer, F004ImportDecision decision)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = decision.Ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = decision.Pivot;
            importer.SetTextureSettings(settings);
            importer.spritePivot = decision.Pivot;
        }

        private void OnPreprocessTexture()
        {
            if (!F004ImportRules.IsGovernedPath(assetPath))
            {
                return;
            }

            // Re-import when any manifest that could govern this PNG changes or appears.
            string manifestPath = null;
            foreach (var candidate in F004ImportRules.ManifestCandidates(assetPath))
            {
                context.DependsOnSourceAsset(candidate);
                if (manifestPath == null && File.Exists(candidate))
                {
                    manifestPath = candidate;
                }
            }

            if (manifestPath == null)
            {
                Debug.LogWarning("F004 import skipped " + assetPath + ": no " + F004ImportRules.ManifestFileName + " found.");
                return;
            }

            F004ImportDecision decision;
            if (!F004ImportRules.TryParse(File.ReadAllText(manifestPath), out var manifest, out var error))
            {
                decision = new F004ImportDecision { Kind = F004ImportDecisionKind.Rejected, Message = manifestPath + ": " + error };
            }
            else
            {
                decision = F004ImportRules.Resolve(assetPath, manifestPath, manifest, File.ReadAllBytes);
            }

            switch (decision.Kind)
            {
                case F004ImportDecisionKind.Apply:
                    Apply((TextureImporter)assetImporter, decision);
                    break;
                case F004ImportDecisionKind.Rejected:
                    Debug.LogWarning("F004 import rejected " + assetPath + ": " + decision.Message);
                    break;
                default:
                    Debug.LogWarning("F004 import skipped " + assetPath + ": " + decision.Message + ".");
                    break;
            }
        }
    }
}
