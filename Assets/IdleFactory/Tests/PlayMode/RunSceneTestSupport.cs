using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>A clock a Play Mode test moves by hand: wall-clock instant and cumulative active seconds.</summary>
    internal sealed class RunSceneClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        public double CumulativeActiveSeconds { get; set; }
        public long UnixMs => UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Loads the authored scene against an isolated persistence root and deterministic clocks (the Feature 003 run
    /// composition seam), and offers the file and fixture helpers of the T110 Play Mode suites.
    /// </summary>
    internal sealed class RunSceneRun
    {
        public const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        public const string IronContentPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";
        public const string DevelopmentCatalogPath = "Assets/IdleFactory/Content/Development/development-catalog.json";
        public const string V1FixtureDirectory = "Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1";

        private IDisposable compositionOverride;
        private Scene scene;

        public RunSceneRun(string root, RunSceneClock clock)
        {
            Root = root;
            Clock = clock;
        }

        public string Root { get; }
        public RunSceneClock Clock { get; }
        public CoreGameFoundationBootstrap Bootstrap { get; private set; }

        public static string NewRoot()
        {
            var root = Path.Combine(Application.temporaryCachePath, "run-scene-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        public static void DeleteRoot(string root)
        {
            try
            {
                if (root != null && Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

#if UNITY_EDITOR
        public static byte[] IronBytes() => AssetDatabase.LoadAssetAtPath<TextAsset>(IronContentPath).bytes;

        public static byte[] DevelopmentCatalogBytes() => AssetDatabase.LoadAssetAtPath<TextAsset>(DevelopmentCatalogPath).bytes;

        /// <summary>Starts the scene. <paramref name="quiet"/> swallows the error log of a rejected start.</summary>
        public IEnumerator Start(Func<byte[]> contentBytes = null, bool quiet = false, Func<byte[]> legacyBytes = null,
            Func<RunCatalog, IRunCheckpointStore> createRunStore = null)
        {
            compositionOverride = CoreGameFoundationBootstrap.OverrideRunCompositionForTests(
                Root,
                () => Clock.UtcNow,
                () => Clock.CumulativeActiveSeconds,
                contentBytes,
                createRunStore,
                legacyBytes);
            var logger = Debug.unityLogger;
            var original = logger.logHandler;
            if (quiet)
            {
                logger.logHandler = new QuietLogHandler();
            }

            try
            {
                EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
                while (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
                {
                    yield return null;
                }

                scene = SceneManager.GetSceneByPath(ScenePath);
                yield return null;
            }
            finally
            {
                logger.logHandler = original;
            }

            Bootstrap = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameFoundationBootstrap>(true))
                .Single();
        }

#endif

        public IEnumerator Stop()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            scene = default;
            Bootstrap = null;
            compositionOverride?.Dispose();
            compositionOverride = null;
        }

        /// <summary>Every file below <paramref name="directory"/>, keyed by its relative path.</summary>
        public static Dictionary<string, byte[]> CaptureFiles(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return new Dictionary<string, byte[]>(StringComparer.Ordinal);
            }

            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .ToDictionary(
                    file => file.Substring(directory.Length).TrimStart('\\', '/').Replace('\\', '/'),
                    File.ReadAllBytes,
                    StringComparer.Ordinal);
        }

        public static string V1Directory(string root) => SaveV1CheckpointFiles.Layout.DirectoryFor(root);

        public static string V2Directory(string root) => SaveV2CheckpointFiles.Layout.DirectoryFor(root);

#if UNITY_EDITOR
        /// <summary>A Save V1 checkpoint of <paramref name="machines"/> development machines, published through the Feature 001 protocol.</summary>
        public static void WriteV1Checkpoint(string root, int machines, DateTimeOffset departureUtc)
        {
            var catalog = GameplayContentLoader.Load(DevelopmentCatalogBytes()).Catalog;
            var definition = catalog.Resolve(new BuildingDefinitionId("dev.machine.basic"));
            var recipe = catalog.Resolve(new RecipeId("dev.recipe.basic"));
            var input = new ResourceId("dev.input");
            var output = new ResourceId("dev.output");
            var buildings = new List<BuildingInstanceState>();
            for (var i = 0; i < machines; i++)
            {
                buildings.Add(new BuildingInstanceState(
                    new BuildingInstanceId(new Guid("22222222-2222-2222-2222-" + (i + 1).ToString("D12"))),
                    definition,
                    new GridPosition(i * 2, 0),
                    0,
                    true,
                    new ResourceBufferState(definition.InputBuffer, new[] { new KeyValuePair<ResourceId, long>(input, 4L) }),
                    new ResourceBufferState(definition.OutputBuffer, new[] { new KeyValuePair<ResourceId, long>(output, 0L) }),
                    new ProductionState(recipe, 0)));
            }

            var factory = new FactoryState(new FactoryBounds(new GridPosition(0, 0), 16, 16), catalog, buildings);
            var checkpoint = new Checkpoint(
                new CheckpointId(new Guid("11111111-1111-1111-1111-111111110001")),
                1L,
                departureUtc,
                new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                null,
                factory);
            var written = new JsonCheckpointStore(root, catalog).Promote(checkpoint);
            if (!written.Succeeded)
            {
                throw new InvalidOperationException("The V1 fixture could not be written: " + written.Outcome);
            }
        }
#endif

        /// <summary>Exact comparable text of the live run: Cash, RP, machines, stock and the run identity.</summary>
        public static string Fingerprint(RunSession session)
        {
            var state = session.State;
            var builder = new StringBuilder();
            builder.Append(state.RunId).Append('|').Append(state.RunNumber).Append("|cash=").Append(state.Cash)
                .Append("|rp=").Append(state.Rp).Append("|storage=").Append(state.StorageLevel)
                .Append("|factory=").Append(state.FactoryLevel);
            foreach (var machine in state.Machines)
            {
                builder.Append("|m=").Append(machine.MachineId).Append('@').Append(machine.SlotIndex).Append('L')
                    .Append(machine.Level).Append('p').Append(machine.ProgressTicks);
            }

            for (var i = 0; i < state.Inventory.Count; i++)
            {
                builder.Append("|s=").Append(state.Inventory.IdAt(i)).Append(':').Append(state.Inventory.UnitsAt(i));
            }

            return builder.ToString();
        }

        private sealed class QuietLogHandler : ILogHandler
        {
            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
            }

            public void LogException(Exception exception, Object context)
            {
            }
        }
    }
}
