using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IdleFactory.Editor.Art
{
    /// <summary>
    /// T440 (FR-002, FR-003, FR-022, FR-023, SC-003): the reproducible import of the approved F004 art families into
    /// <c>Assets/IdleFactory/Art/F004/</c>. One run (1) merges the per-family <c>art-source/f004/&lt;family&gt;/manifest*.json</c> files
    /// into the single <see cref="F004ImportRules.ManifestPath"/>, checking every PNG hash, (2) copies the PNGs byte for byte into
    /// <c>Art/F004/&lt;family&gt;/</c> (the <see cref="F004SpriteImportPostprocessor"/> then applies PPU = D, the manifest pivot, point
    /// filter and RGBA32), (3) creates one Sprite Atlas v2 per world family, and (4) registers the primary frames and the additive
    /// <c>F004-</c> keys in the scene sprite catalog through <see cref="SerializedObject"/> (scene YAML is never hand-edited).
    /// The F003 placeholder stays on its key as the retained fallback frame. Batch entry:
    /// <c>-executeMethod IdleFactory.Editor.Art.F004ArtIntegration.RunFromCommandLine -f004Families machines,slots</c>
    /// (optional <c>-f004Steps import,atlas,catalog</c>, default all three).
    /// T471 adds the floor family (one 70 x 54 tile, the T456 floor period, Art Bible 3.5) and hardens the run: it fails before any write on a
    /// duplicate destination path, catalog key or asset id and on an orphan PNG in a family folder (<c>-f004RemoveOrphans</c> removes the orphans
    /// instead), verifies the SHA-256 of every destination file after the copy, and guards the scene switch of the catalog step for interactive use.
    /// </summary>
    public static class F004ArtIntegration
    {
        public const string SourceRoot = "art-source/f004";
        public const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";

        /// <summary>Families that T440 imports, with their source manifests (merged in this order), in import order.</summary>
        public static readonly string[] FamilyNames =
        {
            "machines", "slots", "cues", "markers", "items", "resources", "ui-icons", "floor",
        };

        /// <summary>Families whose sprites are drawn in the world and therefore packed into a Sprite Atlas v2 (Art Bible budget 7).</summary>
        public static readonly string[] AtlasFamilies = { "machines", "slots", "cues", "markers", "floor" };

        private static readonly Dictionary<string, string[]> SourceManifests = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "machines", new[] { "manifest-candidates.json" } },
            { "slots", new[] { "manifest.json", "manifest-t437.json" } },
            { "cues", new[] { "manifest.json" } },
            { "markers", new[] { "manifest.json" } },
            { "items", new[] { "manifest.json", "manifest-t464.json" } },
            { "resources", new[] { "manifest.json" } },
            { "ui-icons", new[] { "manifest.json" } },
            { "floor", new[] { "manifest.json" } },
        };

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
            public string key;
            public string route;
            public int ppu;
            public int fps;
            public int[] canvas;
            public int[] footprintPx;
            public float[] pivot;
            public bool intentionallyEmpty;
            public SourceFrame[] frames;
        }

        [Serializable]
        private sealed class SourceManifest
        {
            public int schemaVersion;
            public SourceEntry[] entries;
        }

        /// <summary>The shipped manifest as the catalog step reads it: the import fields plus <c>key</c> and <c>family</c>.</summary>
        [Serializable]
        private sealed class ShippedEntry
        {
            public string assetId;
            public string key;
            public string family;
            public int fps;
            public bool intentionallyEmpty;
            public SourceFrame[] frames;
        }

        [Serializable]
        private sealed class ShippedManifest
        {
            public int schemaVersion;
            public ShippedEntry[] entries;
        }

        /// <summary>The source manifests of a family under <c>art-source/f004/&lt;family&gt;/</c>, in merge order.</summary>
        public static IReadOnlyList<string> SourceManifestsOf(string family) => SourceManifests[family];

        /// <summary>The fields of a source manifest entry that decide its catalog key and frame hashes (for tests).</summary>
        public readonly struct SourceEntryInfo
        {
            public SourceEntryInfo(string assetId, string key, bool intentionallyEmpty, IReadOnlyList<string> frameHashes)
            {
                AssetId = assetId;
                Key = key;
                IntentionallyEmpty = intentionallyEmpty;
                FrameHashes = frameHashes;
            }

            public string AssetId { get; }
            public string Key { get; }
            public bool IntentionallyEmpty { get; }
            public IReadOnlyList<string> FrameHashes { get; }
        }

        public static List<SourceEntryInfo> ReadSourceEntries(string manifestPath)
        {
            var manifest = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(manifestPath));
            var result = new List<SourceEntryInfo>();
            foreach (var entry in manifest.entries)
            {
                var hashes = new List<string>();
                foreach (var frame in entry.frames ?? Array.Empty<SourceFrame>())
                {
                    hashes.Add(frame.sha256);
                }

                result.Add(new SourceEntryInfo(entry.assetId, entry.key, entry.intentionallyEmpty, hashes));
            }

            return result;
        }

        /// <summary>Batch entry point.</summary>
        public static void RunFromCommandLine()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                var families = ArgumentList(arguments, "-f004Families", FamilyNames);
                var steps = ArgumentList(arguments, "-f004Steps", new[] { "import", "atlas", "catalog" });
                var removeOrphans = Array.IndexOf(arguments, "-f004RemoveOrphans") >= 0;
                Run(families, steps, removeOrphans);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Runs the named steps (<c>import</c>, <c>atlas</c>, <c>catalog</c>) for the families, merging into the existing manifest.</summary>
        public static void Run(IEnumerable<string> families, IEnumerable<string> steps, bool removeOrphans = false)
        {
            var familyList = new List<string>(families);
            var stepSet = new HashSet<string>(steps, StringComparer.Ordinal);
            foreach (var family in familyList)
            {
                if (!SourceManifests.ContainsKey(family))
                {
                    throw new ArgumentException("Unknown or non-importable family '" + family + "'.");
                }
            }

            if (stepSet.Contains("import"))
            {
                ImportFamilies(familyList, removeOrphans);
            }

            if (stepSet.Contains("atlas"))
            {
                CreateAtlases(familyList);
            }

            if (stepSet.Contains("catalog"))
            {
                RegisterCatalog();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[F004ArtIntegration] done: families " + string.Join(",", familyList) + "; steps " + string.Join(",", stepSet));
        }

        /// <summary>Merges the source manifests of the families into the shipped manifest and copies their PNGs.</summary>
        public static void ImportFamilies(IReadOnlyList<string> families, bool removeOrphans = false)
        {
            var entries = new List<ShippedEntryOut>();
            var keep = new HashSet<string>(families, StringComparer.Ordinal);
            // A family imported by an earlier run stays in the manifest.
            foreach (var family in FamilyNames)
            {
                if (!keep.Contains(family) && Directory.Exists(F004ImportRules.ArtRoot + "/" + family))
                {
                    keep.Add(family);
                }
            }

            var copies = new List<KeyValuePair<string, string>>();
            foreach (var family in FamilyNames)
            {
                if (!keep.Contains(family))
                {
                    continue;
                }

                foreach (var manifestName in SourceManifests[family])
                {
                    var sourceFolder = SourceRoot + "/" + family;
                    var manifest = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(sourceFolder + "/" + manifestName));
                    if (manifest == null || manifest.schemaVersion != F004ImportRules.SupportedSchemaVersion)
                    {
                        throw new InvalidDataException(sourceFolder + "/" + manifestName + ": unsupported schema version.");
                    }

                    foreach (var source in manifest.entries)
                    {
                        var shipped = new ShippedEntryOut { Family = family, Source = source, Key = KeyOf(source) };
                        if (!source.intentionallyEmpty)
                        {
                            foreach (var frame in source.frames)
                            {
                                var sourcePath = sourceFolder + "/" + frame.file;
                                var actual = F004ImportRules.Sha256Hex(File.ReadAllBytes(sourcePath));
                                if (!string.Equals(actual, frame.sha256, StringComparison.OrdinalIgnoreCase))
                                {
                                    throw new InvalidDataException(sourcePath + ": hash " + actual + " does not match the source manifest hash " + frame.sha256);
                                }

                                var fileName = Path.GetFileName(frame.file);
                                shipped.FrameFiles.Add(family + "/" + fileName);
                                shipped.FrameHashes.Add(frame.sha256);
                                copies.Add(new KeyValuePair<string, string>(sourcePath, F004ImportRules.ArtRoot + "/" + family + "/" + fileName));
                            }
                        }

                        entries.Add(shipped);
                    }
                }
            }

            // Fail before any write (T471, review findings of T440): duplicate destination, key or asset id, and PNGs the manifest does not list.
            EnsureUnique(copies.Select(copy => copy.Value), StringComparer.OrdinalIgnoreCase, "destination path");
            EnsureUnique(entries.Select(entry => entry.Key), StringComparer.Ordinal, "catalog key");
            EnsureUnique(entries.Select(entry => entry.Source.assetId), StringComparer.Ordinal, "asset id");
            var orphans = new List<string>();
            var expected = new HashSet<string>(copies.Select(copy => copy.Value), StringComparer.OrdinalIgnoreCase);
            foreach (var family in FamilyNames)
            {
                if (keep.Contains(family))
                {
                    orphans.AddRange(FindOrphanPngs(F004ImportRules.ArtRoot + "/" + family, expected));
                }
            }

            if (orphans.Count > 0 && !removeOrphans)
            {
                throw new InvalidOperationException("Orphan PNGs in the F004 family folders (no manifest frame lists them): " + string.Join(", ", orphans)
                    + ". Remove them or run with -f004RemoveOrphans.");
            }

            // Every PNG is copied and hash-verified first; the manifest is written only after all of them passed (T471, Architect A1), so a
            // failure mid-copy leaves the previous manifest in place. Unity imports the PNGs at the Refresh below, after the manifest exists.
            CommitImport(copies, destination => SourceHashOf(entries, destination), F004ImportRules.ManifestPath, WriteManifest(entries));

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            foreach (var orphan in orphans)
            {
                if (!AssetDatabase.DeleteAsset(orphan))
                {
                    throw new IOException("The orphan PNG could not be removed: " + orphan);
                }

                Debug.LogWarning("[F004ArtIntegration] removed orphan PNG " + orphan);
            }

            Debug.Log("[F004ArtIntegration] imported " + copies.Count + " PNGs in " + keep.Count + " families.");
        }

        /// <summary>
        /// Copies every file with a verified hash, then writes the manifest. A throw while copying leaves <paramref name="manifestPath"/> untouched;
        /// the destinations copied so far stay unlisted until a re-run completes (a failed run needs a re-run). <paramref name="copy"/> replaces the copy primitive in tests.
        /// </summary>
        public static void CommitImport(IReadOnlyList<KeyValuePair<string, string>> copies, Func<string, string> hashOf, string manifestPath,
            string manifestText, Action<string, string> copy = null)
        {
            foreach (var pair in copies)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pair.Value));
                CopyVerified(pair.Key, pair.Value, hashOf(pair.Value), copy);
            }

            File.WriteAllText(manifestPath, manifestText, new UTF8Encoding(false));
        }

        private static string SourceHashOf(List<ShippedEntryOut> entries, string destination)
        {
            foreach (var entry in entries)
            {
                for (var i = 0; i < entry.FrameFiles.Count; i++)
                {
                    if (string.Equals(F004ImportRules.ArtRoot + "/" + entry.FrameFiles[i], destination, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.FrameHashes[i];
                    }
                }
            }

            throw new InvalidOperationException("No manifest frame for " + destination);
        }

        /// <summary>Throws when a value occurs twice (<paramref name="what"/> names the kind of value in the message).</summary>
        public static void EnsureUnique(IEnumerable<string> values, IEqualityComparer<string> comparer, string what)
        {
            var seen = new HashSet<string>(comparer);
            foreach (var value in values)
            {
                if (!seen.Add(value))
                {
                    throw new InvalidDataException("Duplicate " + what + " '" + value + "' in the F004 import.");
                }
            }
        }

        /// <summary>The PNG files under <paramref name="folder"/> that are not in <paramref name="expected"/> (asset paths with '/' separators), sorted.</summary>
        public static List<string> FindOrphanPngs(string folder, ISet<string> expected)
        {
            var orphans = new List<string>();
            if (!Directory.Exists(folder))
            {
                return orphans;
            }

            foreach (var file in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
            {
                var path = file.Replace('\\', '/');
                if (!expected.Contains(path))
                {
                    orphans.Add(path);
                }
            }

            orphans.Sort(StringComparer.Ordinal);
            return orphans;
        }

        /// <summary>
        /// Copies a file byte for byte and verifies the SHA-256 of the destination against <paramref name="expectedSha256"/>; a mismatch throws
        /// and removes the bad copy. <paramref name="copy"/> is the copy primitive (a test replaces it to simulate a corrupt copy).
        /// </summary>
        public static void CopyVerified(string source, string destination, string expectedSha256, Action<string, string> copy = null)
        {
            (copy ?? ((from, to) => File.Copy(from, to, true)))(source, destination);
            var actual = F004ImportRules.Sha256Hex(File.ReadAllBytes(destination));
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(destination);
                throw new InvalidDataException(destination + ": hash " + actual + " after the copy does not match the expected hash " + expectedSha256);
            }
        }

        /// <summary>
        /// The guard in front of <c>OpenSceneMode.Single</c> (catalog step): never in Play Mode; in an interactive editor, unsaved scenes are
        /// saved or the run is cancelled, because opening the scene in Single mode would discard them. A batch run has no unsaved user work.
        /// </summary>
        public static void GuardSceneSwitch(bool batchMode, bool playMode, Func<bool> saveModifiedScenes)
        {
            if (playMode)
            {
                throw new InvalidOperationException("The F004 catalog step replaces the open scene and cannot run in or while entering Play Mode.");
            }

            if (!batchMode && !saveModifiedScenes())
            {
                throw new OperationCanceledException("The F004 catalog step was cancelled: unsaved scenes would be lost.");
            }
        }

        /// <summary>One Sprite Atlas v2 per world family, packing the family folder.</summary>
        public static void CreateAtlases(IEnumerable<string> families)
        {
            if (!AssetDatabase.IsValidFolder(F004SpriteAtlasSettings.AtlasFolder))
            {
                AssetDatabase.CreateFolder(F004ImportRules.ArtRoot, "Atlases");
            }

            foreach (var family in families)
            {
                if (Array.IndexOf(AtlasFamilies, family) < 0)
                {
                    continue;
                }

                var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(F004ImportRules.ArtRoot + "/" + family);
                var atlasPath = F004SpriteAtlasSettings.AtlasFolder + "/" + family + F004SpriteAtlasSettings.AtlasExtension;
                F004SpriteAtlasSettings.CreateFamilyAtlas(atlasPath, folder);
            }
        }

        /// <summary>
        /// Registers the shipped primary frames in the scene catalog: an existing F003 key keeps its placeholder as the retained fallback
        /// frame, an <c>F004-</c> key is appended, and the catalog is told to require the registry's required rows.
        /// </summary>
        public static void RegisterCatalog()
        {
            var manifest = JsonUtility.FromJson<ShippedManifest>(File.ReadAllText(F004ImportRules.ManifestPath));
            EnsureUnique(manifest.entries.Select(entry => entry.key), StringComparer.Ordinal, "catalog key");
            GuardSceneSwitch(Application.isBatchMode, EditorApplication.isPlayingOrWillChangePlaymode,
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo);
            var previousSetup = Application.isBatchMode ? null : EditorSceneManager.GetSceneManagerSetup();
            try
            {
                RegisterCatalogInScene(manifest);
            }
            finally
            {
                if (previousSetup != null && previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        private static void RegisterCatalogInScene(ShippedManifest manifest)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var host = FindCatalogHost(scene);
            var serialized = new SerializedObject(host);
            var entries = serialized.FindProperty("spriteCatalog.entries");
            var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < entries.arraySize; i++)
            {
                var existingKey = entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue;
                if (indexByKey.ContainsKey(existingKey))
                {
                    throw new InvalidDataException("The scene sprite catalog already holds the key '" + existingKey + "' twice.");
                }

                indexByKey[existingKey] = i;
            }

            var registered = 0;
            foreach (var entry in manifest.entries)
            {
                if (entry.intentionallyEmpty)
                {
                    continue; // The running badge keeps its F003 placeholder entry; the cue row draws nothing for it.
                }

                if (string.IsNullOrEmpty(entry.key))
                {
                    throw new InvalidDataException(entry.assetId + " has no catalog key.");
                }

                var sprites = new List<Sprite>();
                foreach (var frame in entry.frames)
                {
                    var path = F004ImportRules.ArtRoot + "/" + frame.file;
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        throw new InvalidDataException(path + " did not import as a sprite.");
                    }

                    sprites.Add(sprite);
                }

                SerializedProperty element;
                if (indexByKey.TryGetValue(entry.key, out var index))
                {
                    element = entries.GetArrayElementAtIndex(index);
                }
                else
                {
                    index = entries.arraySize;
                    entries.InsertArrayElementAtIndex(index);
                    element = entries.GetArrayElementAtIndex(index);
                    element.FindPropertyRelative("key").stringValue = entry.key;
                    element.FindPropertyRelative("sprite").objectReferenceValue = null;
                    element.FindPropertyRelative("frames").arraySize = 0;
                    element.FindPropertyRelative("fallbackFrames").arraySize = 0;
                    indexByKey[entry.key] = index;
                }

                var fallback = element.FindPropertyRelative("fallbackFrames");
                var placeholder = element.FindPropertyRelative("sprite").objectReferenceValue;
                if (placeholder != null && fallback.arraySize == 0)
                {
                    fallback.arraySize = 1;
                    fallback.GetArrayElementAtIndex(0).objectReferenceValue = placeholder;
                }

                element.FindPropertyRelative("sprite").objectReferenceValue = null;
                var frames = element.FindPropertyRelative("frames");
                frames.arraySize = sprites.Count;
                for (var i = 0; i < sprites.Count; i++)
                {
                    frames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                }

                element.FindPropertyRelative("hasFramesPerSecond").boolValue = entry.fps > 0;
                element.FindPropertyRelative("framesPerSecond").floatValue = entry.fps > 0 ? entry.fps : 0f;
                registered++;
            }

            serialized.FindProperty("spriteCatalog.requireRegistryRows").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("The scene could not be saved: " + ScenePath);
            }

            Debug.Log("[F004ArtIntegration] registered " + registered + " catalog keys.");
        }

        private static Component FindCatalogHost(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && new SerializedObject(behaviour).FindProperty("spriteCatalog.entries") != null)
                    {
                        return behaviour;
                    }
                }
            }

            throw new InvalidOperationException("No component with a serialized spriteCatalog in " + ScenePath);
        }

        /// <summary>
        /// The catalog key of a source entry: the manifest <c>key</c> when present; otherwise the machine rule (body to
        /// <c>F003-MACHINE-X</c>, accent to <c>F004-ACCENT-X</c>) or the upper-cased asset id (the plates).
        /// </summary>
        private static string KeyOf(SourceEntry entry) => KeyOf(entry.assetId, entry.key);

        public static string KeyOf(string assetId, string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                return key;
            }

            const string Body = "f004-machine-";
            const string Accent = "f004-accent-";
            const string Run = "-run";
            if (assetId.StartsWith(Body, StringComparison.Ordinal) && assetId.EndsWith(Run, StringComparison.Ordinal))
            {
                return "F003-MACHINE-" + assetId.Substring(Body.Length, assetId.Length - Body.Length - Run.Length).ToUpperInvariant();
            }

            if (assetId.StartsWith(Accent, StringComparison.Ordinal) && assetId.EndsWith(Run, StringComparison.Ordinal))
            {
                return "F004-ACCENT-" + assetId.Substring(Accent.Length, assetId.Length - Accent.Length - Run.Length).ToUpperInvariant();
            }

            return assetId.ToUpperInvariant();
        }

        private sealed class ShippedEntryOut
        {
            public string Family;
            public string Key;
            public SourceEntry Source;
            public readonly List<string> FrameFiles = new List<string>();
            public readonly List<string> FrameHashes = new List<string>();
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
                builder.Append("      \"route\": \"").Append(source.route).Append("\"");
                if (source.intentionallyEmpty)
                {
                    builder.Append(",\n      \"intentionallyEmpty\": true\n");
                }
                else
                {
                    builder.Append(",\n      \"ppu\": ").Append(source.ppu);
                    builder.Append(",\n      \"fps\": ").Append(source.fps);
                    builder.Append(",\n      \"canvas\": [").Append(source.canvas[0]).Append(", ").Append(source.canvas[1]).Append(']');
                    builder.Append(",\n      \"footprintPx\": [").Append(source.footprintPx[0]).Append(", ").Append(source.footprintPx[1]).Append(']');
                    builder.Append(",\n      \"pivot\": [").Append(Num(source.pivot[0])).Append(", ").Append(Num(source.pivot[1])).Append(']');
                    builder.Append(",\n      \"frames\": [\n");
                    for (var f = 0; f < entry.FrameFiles.Count; f++)
                    {
                        builder.Append("        { \"file\": \"").Append(entry.FrameFiles[f]).Append("\", \"sha256\": \"").Append(entry.FrameHashes[f]).Append("\" }");
                        builder.Append(f + 1 < entry.FrameFiles.Count ? ",\n" : "\n");
                    }

                    builder.Append("      ]\n");
                }

                builder.Append(e + 1 < entries.Count ? "    },\n" : "    }\n");
            }

            builder.Append("  ]\n}\n");
            return builder.ToString();
        }

        private static string Num(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        private static string[] ArgumentList(string[] arguments, string name, string[] fallback)
        {
            for (var i = 0; i + 1 < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.Ordinal))
                {
                    return arguments[i + 1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }

            return fallback;
        }
    }
}
