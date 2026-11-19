using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IdleFactory.Unity.Presentation;

namespace IdleFactory.Editor.Art
{
    /// <summary>
    /// T527 (Feature 006; A-13, B-17, P1-F1): the scene wiring of the F006 world art, done through <see cref="SerializedObject"/> and never as
    /// hand-edited YAML (AGENTS.md). One run (1) registers every imported F006 key (the 40 static world frames, the 8 FX entries and the hub glyph
    /// <c>F006-UI-OFFICE</c>) in the scene sprite catalog next to the F004 keys, (2) removes the registration of the F004 floor tile
    /// (<c>F004-FLOOR-TILE</c>, replaced by the deck Tilemap), (3) points the bootstrap's <c>worldComposition</c> field at
    /// <see cref="WorldCompositionSettings.AssetPath"/>, and (4) writes the authored stage tuning of the composition asset: prop weights and
    /// density per stage (the World Designer owns the values; these are the T527 proposal, validated by <see cref="WorldCompositionSettings.Validate"/>).
    /// Each step is idempotent. Batch entry: <c>-executeMethod IdleFactory.Editor.Art.F006SceneWiring.RunFromCommandLine</c> (optional
    /// <c>-f006WiringSteps catalog,settings</c>, default both).
    /// </summary>
    public static class F006SceneWiring
    {
        public const string FloorTileKey = "F004-FLOOR-TILE";

        [Serializable]
        private sealed class ShippedFrame
        {
            public string file;
            public string sha256;
        }

        [Serializable]
        private sealed class ShippedEntry
        {
            public string assetId;
            public string key;
            public string family;
            public int fps;
            public ShippedFrame[] frames;
        }

        [Serializable]
        private sealed class ShippedManifest
        {
            public int schemaVersion;
            public ShippedEntry[] entries;
        }

        public static void RunFromCommandLine()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                var steps = new[] { "catalog", "settings" };
                for (var i = 0; i + 1 < arguments.Length; i++)
                {
                    if (arguments[i] == "-f006WiringSteps")
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
            var set = new HashSet<string>(steps, StringComparer.Ordinal);
            if (set.Contains("settings"))
            {
                WriteCompositionSettings();
            }

            if (set.Contains("catalog"))
            {
                F004ArtIntegration.GuardSceneSwitch(Application.isBatchMode, EditorApplication.isPlayingOrWillChangePlaymode,
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo);
                WireScene();
            }

            if (set.Contains("buildcard"))
            {
                // T539: its own step (run it alone with -f006WiringSteps buildcard): it touches only the two build card fields and never rewrites the composition settings.
                F004ArtIntegration.GuardSceneSwitch(Application.isBatchMode, EditorApplication.isPlayingOrWillChangePlaymode,
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo);
                WireBuildCard();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[F006SceneWiring] done: steps " + string.Join(",", set));
        }

        /// <summary>The prop weights and densities of the three stages (Art Bible 21.1: crate and lamp props, then denser yards), within the 0.5 per eligible cell cap.</summary>
        public static void WriteCompositionSettings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<WorldCompositionSettings>(WorldCompositionSettings.AssetPath);
            if (asset == null)
            {
                throw new FileNotFoundException(WorldCompositionSettings.AssetPath);
            }

            var serialized = new SerializedObject(asset);
            var weights = serialized.FindProperty("propWeightsPerStage");
            // C-4 (T560): the World Designer owns these numbers after the first write. Weights are written only while every stage's list is empty, densities only while
            // every stage is 0 (the defaults), so a re-run never clobbers later tuning. `familyFpsCaps` is serialized by the SetDirty and save below (C-22).
            var weightsEmpty = true;
            for (var stage = 0; stage < weights.arraySize; stage++)
            {
                weightsEmpty &= weights.GetArrayElementAtIndex(stage).FindPropertyRelative("weights").arraySize == 0;
            }

            var density = serialized.FindProperty("propDensityPerStage");
            var densityDefault = true;
            for (var stage = 0; stage < density.arraySize; stage++)
            {
                densityDefault &= density.GetArrayElementAtIndex(stage).floatValue == 0f;
            }

            if (weightsEmpty)
            {
                weights.arraySize = WorldCompositionSettings.StageCount;
            }

            var table = new[]
            {
                new[] { ("crate-small", 3f), ("crate-stack", 1f), ("pallet", 2f), ("lamp-post", 2f) },
                new[] { ("crate-small", 2f), ("crate-stack", 2f), ("pallet", 2f), ("lamp-post", 2f) },
                new[] { ("crate-small", 1f), ("crate-stack", 3f), ("pallet", 2f), ("lamp-post", 3f) },
            };
            for (var stage = 0; weightsEmpty && stage < table.Length; stage++)
            {
                var list = weights.GetArrayElementAtIndex(stage).FindPropertyRelative("weights");
                list.arraySize = table[stage].Length;
                for (var i = 0; i < table[stage].Length; i++)
                {
                    var element = list.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("kind").stringValue = table[stage][i].Item1;
                    element.FindPropertyRelative("weight").floatValue = table[stage][i].Item2;
                }
            }

            if (densityDefault)
            {
                density.arraySize = WorldCompositionSettings.StageCount;
                density.GetArrayElementAtIndex(0).floatValue = 0.20f;
                density.GetArrayElementAtIndex(1).floatValue = 0.30f;
                density.GetArrayElementAtIndex(2).floatValue = 0.40f;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            var problems = asset.Validate();
            if (problems.Count > 0)
            {
                throw new InvalidDataException(WorldCompositionSettings.AssetPath + ": " + string.Join(" ", problems));
            }

            EditorUtility.SetDirty(asset);
            Debug.Log("[F006SceneWiring] settings: weights " + (weightsEmpty ? "written" : "kept") + ", densities " + (densityDefault ? "written" : "kept") + ", familyFpsCaps serialized.");
        }

        private static void WireScene()
        {
            var manifest = JsonUtility.FromJson<ShippedManifest>(File.ReadAllText(F006ArtIntegration.ManifestPath));
            var previousSetup = Application.isBatchMode ? null : EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(F004ArtIntegration.ScenePath, OpenSceneMode.Single);
                var host = FindBootstrap(scene);
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
                    var sprites = new List<Sprite>();
                    foreach (var frame in entry.frames)
                    {
                        var path = F006ArtIntegration.ArtRoot + "/" + frame.file;
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
                        element.FindPropertyRelative("frames").arraySize = 0;
                        indexByKey[entry.key] = index;
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

                // The F004 floor tile and its pool are gone (A-13): drop the key so the deleted sprite is never referenced.
                for (var i = entries.arraySize - 1; i >= 0; i--)
                {
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue == FloorTileKey)
                    {
                        entries.DeleteArrayElementAtIndex(i);
                    }
                }

                var settings = AssetDatabase.LoadAssetAtPath<WorldCompositionSettings>(WorldCompositionSettings.AssetPath);
                if (settings == null)
                {
                    throw new FileNotFoundException(WorldCompositionSettings.AssetPath);
                }

                serialized.FindProperty("worldComposition").objectReferenceValue = settings;
                serialized.FindProperty("spriteCatalog.requireRegistryRows").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("The scene could not be saved: " + F004ArtIntegration.ScenePath);
                }

                Debug.Log("[F006SceneWiring] registered " + registered + " F006 catalog keys, removed " + FloorTileKey + ", wired the world composition.");
            }
            finally
            {
                if (previousSetup != null && previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        public const string BuildCardUxmlPath = "Assets/IdleFactory/Unity/UI/Panels/BuildCard.uxml";
        public const string BuildCardUssPath = "Assets/IdleFactory/Unity/UI/Panels/BuildCard.uss";

        /// <summary>T539: assigns <c>buildCardTree</c> and <c>buildCardStyles</c> on the bootstrap through SerializedObject. Idempotent; nothing else is written.</summary>
        private static void WireBuildCard()
        {
            var previousSetup = Application.isBatchMode ? null : EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(F004ArtIntegration.ScenePath, OpenSceneMode.Single);
                // Loaded after the scene opens: a Single open unloads assets that were loaded before it.
                var tree = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(BuildCardUxmlPath);
                var styles = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(BuildCardUssPath);
                if (tree == null || styles == null)
                {
                    throw new FileNotFoundException(BuildCardUxmlPath + " or " + BuildCardUssPath);
                }

                var host = FindBootstrap(scene);
                var serialized = new SerializedObject(host);
                var treeProperty = serialized.FindProperty("buildCardTree");
                var styleProperty = serialized.FindProperty("buildCardStyles");
                treeProperty.objectReferenceValue = tree;
                styleProperty.objectReferenceValue = styles;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                serialized.Update();
                if (serialized.FindProperty("buildCardTree").objectReferenceValue == null || serialized.FindProperty("buildCardStyles").objectReferenceValue == null)
                {
                    throw new InvalidOperationException("The build card references did not stick (" + tree.GetType().Name + ", " + styles.GetType().Name + ").");
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("The scene could not be saved: " + F004ArtIntegration.ScenePath);
                }

                Debug.Log("[F006SceneWiring] wired the build card tree and styles.");
            }
            finally
            {
                if (previousSetup != null && previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        private static Component FindBootstrap(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && new SerializedObject(behaviour).FindProperty("worldComposition") != null)
                    {
                        return behaviour;
                    }
                }
            }

            throw new InvalidOperationException("No bootstrap with a worldComposition field in " + F004ArtIntegration.ScenePath);
        }
    }
}
