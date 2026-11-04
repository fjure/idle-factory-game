using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    /// The floor family is not imported: its candidate tile (60 x 30) does not match the T456 floor period (70 x 54, Art Bible 3.5).
    /// </summary>
    public static class F004ArtIntegration
    {
        public const string SourceRoot = "art-source/f004";
        public const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";

        /// <summary>Families that T440 imports, with their source manifests (merged in this order), in import order.</summary>
        public static readonly string[] FamilyNames =
        {
            "machines", "slots", "cues", "markers", "items", "resources", "ui-icons",
        };

        /// <summary>Families whose sprites are drawn in the world and therefore packed into a Sprite Atlas v2 (Art Bible budget 7).</summary>
        public static readonly string[] AtlasFamilies = { "machines", "slots", "cues", "markers" };

        private static readonly Dictionary<string, string[]> SourceManifests = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "machines", new[] { "manifest-candidates.json" } },
            { "slots", new[] { "manifest.json", "manifest-t437.json" } },
            { "cues", new[] { "manifest.json" } },
            { "markers", new[] { "manifest.json" } },
            { "items", new[] { "manifest.json", "manifest-t464.json" } },
            { "resources", new[] { "manifest.json" } },
            { "ui-icons", new[] { "manifest.json" } },
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
                Run(families, steps);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Runs the named steps (<c>import</c>, <c>atlas</c>, <c>catalog</c>) for the families, merging into the existing manifest.</summary>
        public static void Run(IEnumerable<string> families, IEnumerable<string> steps)
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
                ImportFamilies(familyList);
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
        public static void ImportFamilies(IReadOnlyList<string> families)
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

            // The manifest first, so the postprocessor finds it when the PNGs arrive.
            File.WriteAllText(F004ImportRules.ManifestPath, WriteManifest(entries), new UTF8Encoding(false));
            foreach (var copy in copies)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(copy.Value));
                File.Copy(copy.Key, copy.Value, true);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Debug.Log("[F004ArtIntegration] imported " + copies.Count + " PNGs in " + keep.Count + " families.");
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
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var host = FindCatalogHost(scene);
            var serialized = new SerializedObject(host);
            var entries = serialized.FindProperty("spriteCatalog.entries");
            var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < entries.arraySize; i++)
            {
                indexByKey[entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue] = i;
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
