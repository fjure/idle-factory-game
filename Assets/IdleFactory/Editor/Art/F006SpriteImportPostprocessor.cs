using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Editor.Art
{
    /// <summary>
    /// T526 (Feature 006, FR-044): applies the F004 sprite import rules (pixels per unit = D = 24, the manifest pivot, point filter, no
    /// mipmaps, clamp, uncompressed RGBA32, full-rect mesh) to every PNG that
    /// <c>Assets/IdleFactory/Art/F006/import-manifest.json</c> lists, and only to those. The manifest has the F004 schema, so the F004
    /// rules (<see cref="F004ImportRules"/>) parse and validate it and check each PNG hash and canvas. An invalid manifest, entry, pivot or
    /// hash is a logged warning and applies nothing, exactly as for F004.
    /// </summary>
    public sealed class F006SpriteImportPostprocessor : AssetPostprocessor
    {
        /// <summary>Raise on every rule change so Unity re-imports every governed PNG.</summary>
        public const uint PostprocessorVersion = 1;

        public override uint GetVersion() => PostprocessorVersion;

        /// <summary>True for a PNG under the F006 art path.</summary>
        public static bool IsGovernedPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                && assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)
                && assetPath.StartsWith(F006ArtIntegration.ArtRoot + "/", System.StringComparison.Ordinal);
        }

        private void OnPreprocessTexture()
        {
            if (!IsGovernedPath(assetPath))
            {
                return;
            }

            var manifestPath = F006ArtIntegration.ManifestPath;
            context.DependsOnSourceAsset(manifestPath);
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("F006 import skipped " + assetPath + ": no import-manifest.json found.");
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
                    F004SpriteImportPostprocessor.Apply((TextureImporter)assetImporter, decision);
                    break;
                case F004ImportDecisionKind.Rejected:
                    Debug.LogWarning("F006 import rejected " + assetPath + ": " + decision.Message);
                    break;
                default:
                    Debug.LogWarning("F006 import skipped " + assetPath + ": " + decision.Message + ".");
                    break;
            }
        }
    }
}
