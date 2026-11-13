using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Editor.Art
{
    /// <summary>
    /// T526 (Feature 006, FR-004, FR-044, SC-008; research R06, R17; Art Bible 21.4): the reproducible import of the approved F006 world art
    /// into <c>Assets/IdleFactory/Art/F006/</c>. One run (1) merges the per-family <c>art-source/f006/&lt;family&gt;/manifest.json</c> files
    /// into <see cref="ManifestPath"/> checking every PNG hash, and copies the PNGs byte for byte (the
    /// <see cref="F006SpriteImportPostprocessor"/> applies PPU 24, the manifest pivot, point filter and RGBA32 at the refresh), (2) swaps the
    /// production K4 badge tails (<c>art-source/f006/cue</c>) into the imported F004 cue sprites and updates their hashes in the F004
    /// manifest (P2-F5), and (3) creates the two Sprite Atlas v2 assets: <c>world</c> (the seven static families) and <c>fx</c>.
    /// The vein-chip loop is authored in the deposit source folder but is an FX asset: it is imported into <c>fx/</c> and packed in the FX atlas.
    /// Catalog rows for the keys are in <c>PresentationSpriteCatalog</c> (T527 wires the placement and the scene catalog).
    /// Batch entry: <c>-executeMethod IdleFactory.Editor.Art.F006ArtIntegration.RunFromCommandLine</c> (optional
    /// <c>-f006Steps import,cues,atlas</c>, default all three).
    /// </summary>
    public static class F006ArtIntegration
    {
        public const string ArtRoot = "Assets/IdleFactory/Art/F006";
        public const string ManifestPath = ArtRoot + "/import-manifest.json";
        public const string AtlasFolder = ArtRoot + "/Atlases";
        public const string SourceRoot = "art-source/f006";
        public const string CueSourceFolder = SourceRoot + "/cue";
        public const string F004CueFolder = F004ImportRules.ArtRoot + "/cues";
        public const string FxFamily = "fx";
        public const string WorldAtlasName = "world";
        public const string FxAtlasName = "fx";

        /// <summary>The static families of the world atlas (Art Bible 21.4), in import order.</summary>
        public static readonly string[] WorldFamilies = { "ground", "foundation", "edge", "depot", "link", "prop", "deposit" };

        /// <summary>Every imported family, in manifest order; <c>fx</c> is the FX atlas. Each also names its source folder under <c>art-source/f006</c>.</summary>
        public static readonly string[] FamilyNames = { "ground", "foundation", "edge", "depot", "link", "prop", "deposit", "fx" };

        [Serializable]
        private sealed class SourceFrame
        {
            public string file;
            public string sha256;
        }

        [Serializable]
        private sealed class SourceEntry
        {
            public string assetId;
            public string route;
            public int ppu;
            public int fps;
            public int[] canvas;
            public int[] footprintPx;
            public float[] pivot;
            public SourceFrame[] frames;
        }

        [Serializable]
        private sealed class SourceManifest
        {
            public int schemaVersion;
            public SourceEntry[] entries;
        }

        private sealed class ShippedEntryOut
        {
            public string Family;
            public string SourceFolder;
            public string Key;
            public SourceEntry Source;
            public readonly List<string> FrameFiles = new List<string>();
            public readonly List<string> FrameHashes = new List<string>();
        }

        /// <summary>The catalog key of an asset id: the upper-cased id (<c>f006-ground-base</c> becomes <c>F006-GROUND-BASE</c>).</summary>
        public static string KeyOf(string assetId) => assetId.ToUpperInvariant();

        /// <summary>The imported family of an asset: every <c>f006-fx-*</c> asset is FX, wherever its source folder is.</summary>
        public static string FamilyOf(string assetId, string sourceFolder) =>
            assetId.StartsWith("f006-fx-", StringComparison.Ordinal) ? FxFamily : sourceFolder;

        /// <summary>Batch entry point.</summary>
        public static void RunFromCommandLine()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                var steps = new[] { "import", "cues", "atlas" };
                for (var i = 0; i + 1 < arguments.Length; i++)
                {
                    if (arguments[i] == "-f006Steps")
                    {
                        steps = arguments[i + 1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                }

                Run(steps);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Run(IEnumerable<string> steps)
        {
            var stepSet = new HashSet<string>(steps, StringComparer.Ordinal);
            if (stepSet.Contains("import"))
            {
                ImportFamilies();
            }

            if (stepSet.Contains("cues"))
            {
                SwapCueTails();
            }

            if (stepSet.Contains("atlas"))
            {
                CreateAtlases();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[F006ArtIntegration] done: steps " + string.Join(",", stepSet));
        }

        private static List<ShippedEntryOut> ReadSources()
        {
            var entries = new List<ShippedEntryOut>();
            foreach (var folder in FamilyNames)
            {
                var manifestPath = SourceRoot + "/" + folder + "/manifest.json";
                var manifest = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(manifestPath));
                if (manifest == null || manifest.schemaVersion != F004ImportRules.SupportedSchemaVersion)
                {
                    throw new InvalidDataException(manifestPath + ": unsupported schema version.");
                }

                foreach (var source in manifest.entries)
                {
                    var shipped = new ShippedEntryOut
                    {
                        Family = FamilyOf(source.assetId, folder),
                        SourceFolder = folder,
                        Key = KeyOf(source.assetId),
                        Source = source,
                    };
                    foreach (var frame in source.frames)
                    {
                        var sourcePath = SourceRoot + "/" + folder + "/" + frame.file;
                        var actual = F004ImportRules.Sha256Hex(File.ReadAllBytes(sourcePath));
                        if (!string.Equals(actual, frame.sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(sourcePath + ": hash " + actual + " does not match the source manifest hash " + frame.sha256);
                        }

                        shipped.FrameFiles.Add(shipped.Family + "/" + Path.GetFileName(frame.file));
                        shipped.FrameHashes.Add(frame.sha256);
                    }

                    entries.Add(shipped);
                }
            }

            return entries;
        }

        /// <summary>Merges the source manifests into the shipped manifest and copies their PNGs, hash-verified.</summary>
        public static void ImportFamilies()
        {
            var entries = ReadSources();
            var copies = new List<KeyValuePair<string, string>>();
            foreach (var entry in entries)
            {
                for (var i = 0; i < entry.FrameFiles.Count; i++)
                {
                    var sourcePath = SourceRoot + "/" + entry.SourceFolder + "/" + entry.Source.frames[i].file;
                    copies.Add(new KeyValuePair<string, string>(sourcePath, ArtRoot + "/" + entry.FrameFiles[i]));
                }
            }

            F004ArtIntegration.EnsureUnique(copies.Select(copy => copy.Value), StringComparer.OrdinalIgnoreCase, "destination path");
            F004ArtIntegration.EnsureUnique(entries.Select(entry => entry.Key), StringComparer.Ordinal, "catalog key");
            F004ArtIntegration.EnsureUnique(entries.Select(entry => entry.Source.assetId), StringComparer.Ordinal, "asset id");
            var expected = new HashSet<string>(copies.Select(copy => copy.Value), StringComparer.OrdinalIgnoreCase);
            var orphans = F004ArtIntegration.FindOrphanPngs(ArtRoot, expected);
            if (orphans.Count > 0)
            {
                throw new InvalidOperationException("Orphan PNGs in the F006 art folder (no manifest frame lists them): " + string.Join(", ", orphans));
            }

            F004ArtIntegration.CommitImport(copies, destination => HashOf(entries, destination), ManifestPath, WriteManifest(entries));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Debug.Log("[F006ArtIntegration] imported " + copies.Count + " PNGs, " + entries.Count + " assets.");
        }

        private static string HashOf(List<ShippedEntryOut> entries, string destination)
        {
            foreach (var entry in entries)
            {
                for (var i = 0; i < entry.FrameFiles.Count; i++)
                {
                    if (string.Equals(ArtRoot + "/" + entry.FrameFiles[i], destination, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.FrameHashes[i];
                    }
                }
            }

            throw new InvalidOperationException("No manifest frame for " + destination);
        }

        /// <summary>
        /// P2-F5: replaces the five imported F004 badge PNGs (<c>Art/F004/cues</c>) with the approved K4 tail sprites of
        /// <c>art-source/f006/cue</c> and records the new SHA-256 in <c>Art/F004/import-manifest.json</c>. The files keep their paths and
        /// <c>.meta</c> files, so the scene catalog references stay valid. Idempotent; fails before any write on a missing source or
        /// destination or a manifest that does not list a cue exactly once.
        /// </summary>
        public static void SwapCueTails()
        {
            var manifest = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(CueSourceFolder + "/manifest.json"));
            var manifestText = File.ReadAllText(F004ImportRules.ManifestPath);
            var copies = new List<KeyValuePair<string, string>>();
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.entries)
            {
                foreach (var frame in entry.frames)
                {
                    var sourcePath = CueSourceFolder + "/" + frame.file;
                    var fileName = Path.GetFileName(frame.file);
                    var destination = F004CueFolder + "/" + fileName;
                    var actual = F004ImportRules.Sha256Hex(File.ReadAllBytes(sourcePath));
                    if (!string.Equals(actual, frame.sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(sourcePath + ": hash " + actual + " does not match the source manifest hash " + frame.sha256);
                    }

                    if (!File.Exists(destination) || !File.Exists(destination + ".meta"))
                    {
                        throw new FileNotFoundException("The imported F004 cue sprite (and its .meta) must exist: " + destination);
                    }

                    var pattern = new Regex("(\"file\":\\s*\"cues/" + Regex.Escape(fileName) + "\",\\s*\"sha256\":\\s*\")[0-9a-fA-F]{64}(\")");
                    if (pattern.Matches(manifestText).Count != 1)
                    {
                        throw new InvalidDataException("The F004 import manifest must list " + fileName + " exactly once.");
                    }

                    manifestText = pattern.Replace(manifestText, "${1}" + frame.sha256 + "${2}");
                    copies.Add(new KeyValuePair<string, string>(sourcePath, destination));
                    hashes[destination] = frame.sha256;
                }
            }

            F004ArtIntegration.CommitImport(copies, destination => hashes[destination], F004ImportRules.ManifestPath, manifestText);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Debug.Log("[F006ArtIntegration] swapped " + copies.Count + " K4 cue tail sprites.");
        }

        /// <summary>The world atlas (the seven static family folders) and the FX atlas (the fx folder), Art Bible 21.4 and budget 7.</summary>
        public static void CreateAtlases()
        {
            if (!AssetDatabase.IsValidFolder(AtlasFolder))
            {
                AssetDatabase.CreateFolder(ArtRoot, "Atlases");
            }

            var world = WorldFamilies.Select(family => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ArtRoot + "/" + family)).ToArray();
            F004SpriteAtlasSettings.CreateFamilyAtlas(AtlasFolder + "/" + WorldAtlasName + F004SpriteAtlasSettings.AtlasExtension, world);
            var fx = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ArtRoot + "/" + FxFamily);
            F004SpriteAtlasSettings.CreateFamilyAtlas(AtlasFolder + "/" + FxAtlasName + F004SpriteAtlasSettings.AtlasExtension, fx);
        }

        private static string WriteManifest(List<ShippedEntryOut> entries)
        {
            entries.Sort((a, b) =>
            {
                var family = Array.IndexOf(FamilyNames, a.Family).CompareTo(Array.IndexOf(FamilyNames, b.Family));
                return family != 0 ? family : string.CompareOrdinal(a.Source.assetId, b.Source.assetId);
            });
            var builder = new StringBuilder();
            builder.Append("{\n  \"schemaVersion\": ").Append(F004ImportRules.SupportedSchemaVersion).Append(",\n  \"entries\": [\n");
            for (var e = 0; e < entries.Count; e++)
            {
                var entry = entries[e];
                var source = entry.Source;
                builder.Append("    {\n");
                builder.Append("      \"assetId\": \"").Append(source.assetId).Append("\",\n");
                builder.Append("      \"key\": \"").Append(entry.Key).Append("\",\n");
                builder.Append("      \"family\": \"").Append(entry.Family).Append("\",\n");
                builder.Append("      \"source\": \"").Append(SourceRoot).Append('/').Append(entry.SourceFolder).Append("/manifest.json\",\n");
                builder.Append("      \"route\": \"").Append(source.route).Append("\",\n");
                builder.Append("      \"ppu\": ").Append(source.ppu).Append(",\n");
                builder.Append("      \"fps\": ").Append(source.fps).Append(",\n");
                builder.Append("      \"canvas\": [").Append(source.canvas[0]).Append(", ").Append(source.canvas[1]).Append("],\n");
                builder.Append("      \"footprintPx\": [").Append(source.footprintPx[0]).Append(", ").Append(source.footprintPx[1]).Append("],\n");
                builder.Append("      \"pivot\": [").Append(Num(source.pivot[0])).Append(", ").Append(Num(source.pivot[1])).Append("],\n");
                builder.Append("      \"frames\": [\n");
                for (var f = 0; f < entry.FrameFiles.Count; f++)
                {
                    builder.Append("        { \"file\": \"").Append(entry.FrameFiles[f]).Append("\", \"sha256\": \"").Append(entry.FrameHashes[f]).Append("\" }");
                    builder.Append(f + 1 < entry.FrameFiles.Count ? ",\n" : "\n");
                }

                builder.Append("      ]\n");
                builder.Append(e + 1 < entries.Count ? "    },\n" : "    }\n");
            }

            builder.Append("  ]\n}\n");
            return builder.ToString();
        }

        private static string Num(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
