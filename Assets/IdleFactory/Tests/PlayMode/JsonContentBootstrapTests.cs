using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    public sealed class JsonContentBootstrapTests : InputTestFixture
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string ContentPath = "Assets/IdleFactory/Content/Development/development-catalog.json";
        private Scene loadedScene;
        private IDisposable compositionOverride;
        private string checkpointRoot;
        private readonly List<GameObject> clones = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            checkpointRoot = Path.Combine(Application.temporaryCachePath, "json-content-bootstrap-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(checkpointRoot);
            File.WriteAllBytes(Path.Combine(checkpointRoot, "active.json"), new byte[] { 1, 2, 3, 4 });
            File.WriteAllBytes(Path.Combine(checkpointRoot, "previous.json"), new byte[] { 5, 6, 7 });
        }

        public override void TearDown()
        {
            base.TearDown();
            if (checkpointRoot != null && Directory.Exists(checkpointRoot))
            {
                Directory.Delete(checkpointRoot, true);
            }
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            foreach (var clone in clones)
            {
                if (clone != null) Object.Destroy(clone);
            }
            clones.Clear();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(loadedScene);
                while (unload != null && !unload.isDone) yield return null;
            }
            loadedScene = default;
            compositionOverride?.Dispose();
            compositionOverride = null;
        }

        [UnityTest]
        public IEnumerator PackagedContentPreservesD001PlacementTwoCyclesBlockingAndSaveLoad()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The authored scene composition test runs in the Editor.");
            yield break;
#else
            var store = new IsolatedCheckpointStore();
            compositionOverride = CoreGameFoundationBootstrap.OverrideCompositionDependenciesForTests(
                _ => store,
                () => new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero),
                () => 0d);
            yield return LoadAuthoredScene();
            var bootstrap = FindBootstrap();
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.Definitions, Is.SameAs(bootstrap.Session.Catalog));
            Assert.That(bootstrap.Hud.IsReady, Is.True);

            Assert.That(bootstrap.PlacementController.StartPlacement(
                new BuildingDefinitionId("dev.machine.basic"), new GridPosition(5, 5)), Is.True);
            bootstrap.PlacementController.Confirm();
            var id = bootstrap.FactoryView.SelectedInstanceId.Value;
            var production = bootstrap.ProductionCommands;
            var input = new ResourceId("dev.input");
            var output = new ResourceId("dev.output");
            Assert.That(production.AddResource(id, BufferRole.Input, input, 4).Succeeded, Is.True);
            Assert.That(production.AdvanceSimulation(5 * TimeSpan.TicksPerSecond).Succeeded, Is.True);
            var first = production.GetMachineInspection(id);
            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.Value.Outputs.Single(quantity => quantity.ResourceId == output).Quantity, Is.EqualTo(1));
            Assert.That(production.AdvanceSimulation(5 * TimeSpan.TicksPerSecond).Succeeded, Is.True);
            var blocked = production.GetMachineInspection(id);
            Assert.That(blocked.Value.Outputs.Single(quantity => quantity.ResourceId == output).Quantity, Is.EqualTo(1), "Full output blocks a second cycle.");
            Assert.That(blocked.Value.Blockers, Is.Not.Empty);
            Assert.That(production.RemoveResource(id, BufferRole.Output, output, 1).Succeeded, Is.True);
            Assert.That(production.AdvanceSimulation(5 * TimeSpan.TicksPerSecond).Succeeded, Is.True);
            var second = production.GetMachineInspection(id);
            Assert.That(second.Value.Outputs.Single(quantity => quantity.ResourceId == output).Quantity, Is.EqualTo(1));

            var saved = bootstrap.PersistenceCommands.SaveNow();
            Assert.That(saved.Succeeded, Is.True, saved.Message);
            Assert.That(store.Active, Is.Not.Null);
            Assert.That(production.RemoveResource(id, BufferRole.Output, output, 1).Succeeded, Is.True);
            var loaded = bootstrap.PersistenceCommands.LoadDevelopmentCheckpoint();
            Assert.That(loaded.Succeeded, Is.True, loaded.Message);
            Assert.That(production.GetMachineInspection(id).Value.Outputs.Single(quantity => quantity.ResourceId == output).Quantity, Is.EqualTo(1));
#endif
        }

        [UnityTest]
        public IEnumerator RejectedDocumentsShowCodedErrorBeforeCheckpointAccess()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The Editor byte-source seam is unavailable in a player.");
            yield break;
#else
            var valid = AssetDatabase.LoadAssetAtPath<TextAsset>(ContentPath).bytes;
            var json = Encoding.UTF8.GetString(valid);
            var cases = new[]
            {
                new FailureCase("null override", () => null, "content.empty"),
                new FailureCase("throwing override", () => throw new IOException("C:\\private\\secret\\checkpoint.json"), "content.unavailable"),
                new FailureCase("empty", () => Array.Empty<byte>(), "content.empty"),
                new FailureCase("BOM", () => new byte[] { 0xEF, 0xBB, 0xBF }.Concat(valid).ToArray(), "content.encoding"),
                new FailureCase("truncated", () => Encoding.UTF8.GetBytes(json.Substring(0, json.Length / 2)), "json.malformed"),
                new FailureCase("oversized", () => new byte[GameplayContentLimits.MaximumByteCount + 1], "content.too-large"),
                new FailureCase("unknown member", () => Encoding.UTF8.GetBytes(json.Replace("\"contentSchemaVersion\": 1,", "\"contentSchemaVersion\": 1, \"unknown\": true,")), "member.unknown"),
                new FailureCase("unresolved reference", () => Encoding.UTF8.GetBytes(json.Replace("\"resourceId\": \"dev.input\"", "\"resourceId\": \"missing.input\"")), "ref.resource.unknown"),
                new FailureCase("unsupported version", () => Encoding.UTF8.GetBytes(json.Replace("\"contentSchemaVersion\": 1", "\"contentSchemaVersion\": 2")), "content.version.unsupported"),
                new FailureCase("unresolved presentation", () => Encoding.UTF8.GetBytes(json.Replace("F001-MACHINE-D001-NORMAL", "MISSING-SPRITE")), "presentation.key.unresolved"),
            };

            foreach (var failure in cases)
            {
                var before = CaptureCheckpointFiles();
                var storeCalls = 0;
                compositionOverride = CoreGameFoundationBootstrap.OverrideCompositionDependenciesForTests(
                    catalog => { storeCalls++; return new IsolatedCheckpointStore(); },
                    () => DateTimeOffset.UtcNow,
                    () => 0d,
                    failure.Bytes);
                var logger = Debug.unityLogger;
                var originalHandler = logger.logHandler;
                logger.logHandler = new QuietLogHandler();
                try
                {
                    yield return LoadAuthoredScene();
                }
                finally
                {
                    logger.logHandler = originalHandler;
                }
                var bootstrap = FindBootstrap();
                AssertRejected(bootstrap, failure.Code, failure.Name);
                if (failure.Name == "unresolved presentation")
                {
                    Assert.That(bootstrap.InitializationError, Does.Contain("'dev.machine.basic'").And.Contain("'MISSING-SPRITE'"),
                        "FR-015: the diagnostic names the entry and the unresolved key.");
                }
                if (failure.Name == "throwing override")
                {
                    Assert.That(bootstrap.InitializationError, Does.Contain("(IOException)").And.Not.Contain("secret"),
                        "The exception type is reported without its path-bearing message.");
                }
                if (failure.Name == "empty")
                {
                    bootstrap.Hud.ApplyScreenLayout(360f, 780f, new Rect(0f, 40f, 360f, 700f));
                    Assert.That(bootstrap.Hud.Layout, Is.EqualTo(FactoryHudLayout.CompactPortrait));
                    AssertRejected(bootstrap, failure.Code, "compact portrait safe area");
                }
                Assert.That(storeCalls, Is.Zero, failure.Name);
                AssertCheckpointFilesUnchanged(before);
                var unload = SceneManager.UnloadSceneAsync(loadedScene);
                while (unload != null && !unload.isDone) yield return null;
                loadedScene = default;
                compositionOverride.Dispose();
                compositionOverride = null;
            }
#endif
        }

        [UnityTest]
        public IEnumerator UnexpectedBootstrapExceptionReportsTypeNameOnly()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The Editor composition seam is unavailable in a player.");
            yield break;
#else
            compositionOverride = CoreGameFoundationBootstrap.OverrideCompositionDependenciesForTests(
                _ => throw new IOException("C:\\private\\secret\\checkpoint.json"),
                () => DateTimeOffset.UtcNow,
                () => 0d);
            var logger = Debug.unityLogger;
            var originalHandler = logger.logHandler;
            logger.logHandler = new QuietLogHandler();
            try
            {
                yield return LoadAuthoredScene();
            }
            finally
            {
                logger.logHandler = originalHandler;
            }
            var bootstrap = FindBootstrap();
            Assert.That(bootstrap.IsInitialized, Is.False);
            Assert.That(bootstrap.InitializationError, Does.Contain("IOException").And.Not.Contain("secret"));
            Assert.That(bootstrap.Definitions, Is.Null);
#endif
        }

        [UnityTest]
        public IEnumerator NullSerializedAssetOnAuthoredCompositionUsesContentEmpty()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The serialized authored-scene composition test runs in the Editor.");
            yield break;
#else
            var storeCalls = 0;
            compositionOverride = CoreGameFoundationBootstrap.OverrideCompositionDependenciesForTests(
                _ => { storeCalls++; return new IsolatedCheckpointStore(); },
                () => DateTimeOffset.UtcNow,
                () => 0d);
            yield return LoadAuthoredScene();
            var authored = FindBootstrap();
            Assert.That(authored.IsInitialized, Is.True, authored.InitializationError);
            var callsFromAuthoredScene = storeCalls;

            // Clone the authored composition while inactive so its serialized references remain valid,
            // then remove only the packaged TextAsset before Awake runs on the clone.
            authored.gameObject.SetActive(false);
            var clone = Object.Instantiate(authored.gameObject);
            clones.Add(clone);
            var bootstrap = clone.GetComponent<CoreGameFoundationBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            var content = serialized.FindProperty("gameplayContent");
            Assert.That(content, Is.Not.Null);
            content.objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var before = CaptureCheckpointFiles();
            var logger = Debug.unityLogger;
            var originalHandler = logger.logHandler;
            logger.logHandler = new QuietLogHandler();
            try
            {
                clone.SetActive(true);
            }
            finally
            {
                logger.logHandler = originalHandler;
            }
            AssertRejected(bootstrap, "content.empty", "null serialized TextAsset");
            Assert.That(storeCalls, Is.EqualTo(callsFromAuthoredScene), "Rejected clone must not construct a store.");
            AssertCheckpointFilesUnchanged(before);
#endif
        }

#if UNITY_EDITOR
        private IEnumerator LoadAuthoredScene()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!SceneManager.GetSceneByPath(ScenePath).isLoaded) yield return null;
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            yield return null;
        }

        private CoreGameFoundationBootstrap FindBootstrap() => loadedScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<CoreGameFoundationBootstrap>(true)).Single();

        private static void AssertRejected(CoreGameFoundationBootstrap bootstrap, string code, string name)
        {
            Assert.That(bootstrap.IsInitialized, Is.False, name);
            Assert.That(bootstrap.InitializationError, Does.StartWith(code + ":"), name);
            Assert.That(bootstrap.Session, Is.Null, name);
            Assert.That(bootstrap.Definitions, Is.Null, name);
            Assert.That(bootstrap.Hud.IsReady, Is.True, name);
            Assert.That(bootstrap.Hud.State, Is.EqualTo(FactoryHudState.Error), name);
            Assert.That(bootstrap.Hud.Root.ClassListContains("factory-hud--error"), Is.True, name);
            Assert.That(bootstrap.Hud.Root.Q<Label>("status").text, Does.StartWith(code + ":"), name);
            Assert.That(bootstrap.Hud.BuildButton.enabledSelf, Is.False, name);
            Assert.That(bootstrap.Hud.ConfirmButton.enabledSelf, Is.False, name);
            Assert.That(bootstrap.InitializationError, Does.Not.Contain("C:\\private"), name);
        }

        private Dictionary<string, byte[]> CaptureCheckpointFiles() => Directory.GetFiles(checkpointRoot)
            .ToDictionary(Path.GetFileName, File.ReadAllBytes, StringComparer.Ordinal);

        private void AssertCheckpointFilesUnchanged(Dictionary<string, byte[]> before)
        {
            var after = CaptureCheckpointFiles();
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            foreach (var pair in before)
            {
                Assert.That(after[pair.Key], Is.EqualTo(pair.Value), pair.Key);
            }
        }

        private sealed class FailureCase
        {
            public FailureCase(string name, Func<byte[]> bytes, string code)
            {
                Name = name;
                Bytes = bytes;
                Code = code;
            }
            public string Name { get; }
            public Func<byte[]> Bytes { get; }
            public string Code { get; }
        }

        private sealed class QuietLogHandler : ILogHandler
        {
            public void LogFormat(LogType logType, Object context, string format, params object[] args) { }
            public void LogException(Exception exception, Object context) { }
        }
#endif
    }
}
