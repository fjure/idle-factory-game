using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T066: captures and asserts factory readability at the supported minimum zoom (640x360, orthographic size
    /// 12, the <see cref="FactoryCameraController"/> maximum, 15 px/cell). Per
    /// specs/001-core-game-foundation/reviews/f001-placeholder-final-review.md ("Zoom readability"), the factory
    /// boundary, both footprint silhouettes (D001 2x2 and the alternate 3x1/1x3 footprint) and a retained status
    /// badge symbol must survive Unity's point-sampled render without relying on color. Frames and an evidence
    /// README documenting the method are written to specs/001-core-game-foundation/reviews/t066-evidence/.
    /// </summary>
    public sealed class ZoomReadabilityTests
    {
        private const int ViewportWidth = 640;
        private const int ViewportHeight = 360;
        private const float MaximumOrthographicSize = 12f; // FactoryCameraController maximum (ART-F1, T041-F1).
        private const float PxPerWorldUnit = ViewportHeight / (2f * MaximumOrthographicSize); // 15 px/cell.
        private const float FootprintLuminanceDelta = 0.03f;
        private const float BoundaryLuminanceDelta = 0.03f;
        private const float BadgeVarianceMinimum = 0.0008f;
        private static readonly Vector2 SubpixelOffset = new Vector2(0.37f, 0.21f);
        private static readonly Rect FactoryWorldBounds = new Rect(0f, 0f, 16f, 16f);

        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string EvidenceDirectory = "specs/001-core-game-foundation/reviews/t066-evidence";
        private const string SpriteRoot = "Assets/IdleFactory/Art/Development/F001/Imported/";

        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Object> transientObjects = new List<Object>();
        private Scene loadedScene;
        private SceneCompositionTestScope compositionScope;

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in objects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            foreach (var item in transientObjects)
            {
                if (item is RenderTexture renderTexture)
                {
                    RenderTexture.active = null;
                    renderTexture.Release();
                }

                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            objects.Clear();
            transientObjects.Clear();
        }

        [UnityTearDown]
        public IEnumerator UnloadDevelopmentScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(loadedScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            loadedScene = default;
            compositionScope?.Dispose();
            compositionScope = null;
        }

        /// <summary>
        /// Loads the authored scene, places one selected D001 machine (real dev catalog, real D001 sprite), and
        /// asserts the factory boundary, the D001 2x2 footprint silhouette and its retained status badge symbol
        /// are all present in the 640x360/size-12 render, both at the authored camera position and at a
        /// sub-cell-offset position (point-sampled sprites can lose detail at particular subpixel alignments).
        /// </summary>
        [UnityTest]
        public IEnumerator AuthoredSceneD001FootprintBoundaryAndBadgeReadableAtMaximumZoomOut()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development scene is loaded directly by the Editor test harness.");
            yield break;
#else
            var anchor = new GridPosition(2, 2);
            compositionScope = SceneCompositionTestScope.WithSeededCheckpoint(catalog => BuildSeededD001State(catalog, anchor));

            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                yield return null;
            }
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            yield return null;

            var bootstrap = loadedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameFoundationBootstrap>(true))
                .SingleOrDefault();
            Assert.That(bootstrap, Is.Not.Null, "The authored scene must expose exactly one CoreGameFoundationBootstrap.");
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(1),
                "The isolated checkpoint must seed exactly the D001 machine under test.");
            var instanceId = bootstrap.Snapshot.Buildings.Single().InstanceId;
            bootstrap.FactoryView.Select(instanceId);
            yield return null;

            var texture = new RenderTexture(ViewportWidth, ViewportHeight, 16);
            texture.Create();
            transientObjects.Add(texture);
            var camera = bootstrap.FactoryCamera;
            camera.targetTexture = texture;
            camera.orthographicSize = MaximumOrthographicSize;
            camera.transform.position = new Vector3(FactoryWorldBounds.center.x, FactoryWorldBounds.center.y, -10f);

            var footprintWorld = new Rect(anchor.X, anchor.Y, 2f, 2f);
            var badgeWorld = BadgeWorldRect(anchor, 2f, 2f);

            CaptureAndAssert(
                camera,
                Vector3.zero,
                "authored-d001-default.png",
                new[] { footprintWorld },
                new[] { badgeWorld },
                "D001 (authored scene, default camera position)");
            CaptureAndAssert(
                camera,
                new Vector3(SubpixelOffset.x, SubpixelOffset.y, 0f),
                "authored-d001-offset.png",
                new[] { footprintWorld },
                new[] { badgeWorld },
                "D001 (authored scene, sub-cell offset camera position)");

            // The RenderTexture is released in TearDown, before the scene (and this camera) unloads; detach it
            // from the authored scene's own camera first so the release does not warn about a live target.
            camera.targetTexture = null;
#endif
        }

        /// <summary>
        /// Builds a standalone FactoryView with the same production sprites, grid mapper and camera configuration
        /// as the authored scene (the packaged development-catalog.json registers only D001, so the alternate 3x1
        /// footprint cannot be placed through it), places the alternate building in both allowed footprint
        /// orientations (3x1 and its quarter-turn 1x3), and asserts the boundary, both footprint silhouettes and
        /// each selected instance's retained status badge symbol at the 640x360/size-12 view, at the authored
        /// camera position and at a sub-cell-offset position.
        /// </summary>
        [UnityTest]
        public IEnumerator StandaloneAlternateFootprintBothOrientationsReadableAtMaximumZoomOut()
        {
            var catalog = CreateAlternateCatalog();
            var horizontalAnchor = new GridPosition(2, 6);
            var verticalAnchor = new GridPosition(10, 2);
            var horizontalId = new BuildingInstanceId(new Guid(1, 0, 0, new byte[8]));
            var verticalId = new BuildingInstanceId(new Guid(2, 0, 0, new byte[8]));
            var state = new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 16, 16),
                catalog,
                new[]
                {
                    CreateAlternateInstance(catalog, horizontalId, horizontalAnchor, 0),
                    CreateAlternateInstance(catalog, verticalId, verticalAnchor, 1),
                });
            var session = CreateRunningSession(state);
            var commands = new PlacementCommands(session, new NeverCalledIdSource());
            var mapper = new GridWorldMapper(1f, Vector2.zero);
            var sprites = CreateProductionSpriteCatalog();

            var viewObject = NewObject("standalone-factory-view");
            var view = viewObject.AddComponent<FactoryView>();
            view.Configure(commands, catalog, sprites, mapper);
            Assert.That(view.Error, Is.Empty, view.Error);

            var camera = CreateCaptureCamera();

            var horizontalFootprint = new Rect(horizontalAnchor.X, horizontalAnchor.Y, 3f, 1f);
            var verticalFootprint = new Rect(verticalAnchor.X, verticalAnchor.Y, 1f, 3f);
            var bothFootprints = new[] { horizontalFootprint, verticalFootprint };

            view.Select(horizontalId);
            yield return null;
            CaptureAndAssert(
                camera,
                Vector3.zero,
                "alternate-horizontal-default.png",
                bothFootprints,
                new[] { BadgeWorldRect(horizontalAnchor, 3f, 1f) },
                "alternate 3x1 selected, both footprint orientations present (default camera position)");
            CaptureAndAssert(
                camera,
                new Vector3(SubpixelOffset.x, SubpixelOffset.y, 0f),
                "alternate-horizontal-offset.png",
                bothFootprints,
                new[] { BadgeWorldRect(horizontalAnchor, 3f, 1f) },
                "alternate 3x1 selected, both footprint orientations present (sub-cell offset camera position)");

            view.Select(verticalId);
            yield return null;
            CaptureAndAssert(
                camera,
                Vector3.zero,
                "alternate-vertical-default.png",
                bothFootprints,
                new[] { BadgeWorldRect(verticalAnchor, 1f, 3f) },
                "alternate 1x3 selected, both footprint orientations present (default camera position)");
            CaptureAndAssert(
                camera,
                new Vector3(SubpixelOffset.x, SubpixelOffset.y, 0f),
                "alternate-vertical-offset.png",
                bothFootprints,
                new[] { BadgeWorldRect(verticalAnchor, 1f, 3f) },
                "alternate 1x3 selected, both footprint orientations present (sub-cell offset camera position)");
        }

        // ---- capture and assertion helpers ----------------------------------------------------------------

        private void CaptureAndAssert(
            Camera camera,
            Vector3 offsetFromCenter,
            string fileName,
            IReadOnlyList<Rect> footprintsWorld,
            IReadOnlyList<Rect> badgesWorld,
            string context)
        {
            camera.transform.position = new Vector3(
                FactoryWorldBounds.center.x + offsetFromCenter.x,
                FactoryWorldBounds.center.y + offsetFromCenter.y,
                -10f);

            var texture = ReadFrame(camera);
            SaveEvidencePng(texture, fileName);

            var backgroundLuminance = SampleBackgroundLuminance(texture, camera);
            AssertBoundaryVisible(texture, camera, backgroundLuminance, context);
            foreach (var footprintWorld in footprintsWorld)
            {
                AssertRegionDistinctFromBackground(texture, camera, footprintWorld, backgroundLuminance, context + " footprint silhouette");
            }

            foreach (var badgeWorld in badgesWorld)
            {
                AssertBadgeSymbolRetained(texture, camera, badgeWorld, context);
            }
        }

        private Texture2D ReadFrame(Camera camera)
        {
            camera.Render();
            var previousActive = RenderTexture.active;
            RenderTexture.active = camera.targetTexture;
            var texture = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, camera.targetTexture.width, camera.targetTexture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previousActive;
            transientObjects.Add(texture);
            return texture;
        }

        private void SaveEvidencePng(Texture2D texture, string fileName)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", EvidenceDirectory));
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, fileName), texture.EncodeToPNG());
        }

        /// <summary>
        /// Rec. 709 relative luminance from RGB alone (ignores hue and saturation), so every assertion below is a
        /// grayscale/luminance comparison and never depends on color to detect shape or presence.
        /// </summary>
        private static float Luminance(Color32 pixel) =>
            (0.2126f * pixel.r + 0.7152f * pixel.g + 0.0722f * pixel.b) / 255f;

        private static Rect WorldToScreenRect(Camera camera, Rect worldRect)
        {
            var min = camera.WorldToScreenPoint(new Vector3(worldRect.xMin, worldRect.yMin, 0f));
            var max = camera.WorldToScreenPoint(new Vector3(worldRect.xMax, worldRect.yMax, 0f));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static (float mean, float variance) RegionLuminanceStats(Texture2D texture, Rect screenRect)
        {
            var xMin = Mathf.Clamp(Mathf.FloorToInt(screenRect.xMin), 0, texture.width - 1);
            var xMax = Mathf.Clamp(Mathf.CeilToInt(screenRect.xMax), xMin + 1, texture.width);
            var yMin = Mathf.Clamp(Mathf.FloorToInt(screenRect.yMin), 0, texture.height - 1);
            var yMax = Mathf.Clamp(Mathf.CeilToInt(screenRect.yMax), yMin + 1, texture.height);

            var sum = 0f;
            var count = 0;
            for (var y = yMin; y < yMax; y++)
            {
                for (var x = xMin; x < xMax; x++)
                {
                    sum += Luminance(texture.GetPixel(x, y));
                    count++;
                }
            }

            var mean = count == 0 ? 0f : sum / count;
            var varianceSum = 0f;
            for (var y = yMin; y < yMax; y++)
            {
                for (var x = xMin; x < xMax; x++)
                {
                    var delta = Luminance(texture.GetPixel(x, y)) - mean;
                    varianceSum += delta * delta;
                }
            }

            var variance = count == 0 ? 0f : varianceSum / count;
            return (mean, variance);
        }

        private float SampleBackgroundLuminance(Texture2D texture, Camera camera)
        {
            // A corner well outside the padded factory bounds; at size 12 the 640x360 view spans far more
            // world width than the 16x16 factory, so the corners are always clear-color background.
            var screenRect = new Rect(4, 4, 12, 12);
            return RegionLuminanceStats(texture, screenRect).mean;
        }

        private void AssertBoundaryVisible(Texture2D texture, Camera camera, float backgroundLuminance, string context)
        {
            // Sample along the bottom world edge (y = 0) across the visible factory width; boundary modules
            // render there at BoundarySortingOrder above the background.
            var boundaryWorld = new Rect(4f, -0.3f, 8f, 0.6f);
            var screenRect = WorldToScreenRect(camera, boundaryWorld);
            var (mean, _) = RegionLuminanceStats(texture, screenRect);
            Assert.That(Mathf.Abs(mean - backgroundLuminance), Is.GreaterThan(BoundaryLuminanceDelta),
                $"{context}: the factory boundary must remain visually distinct from the clear-color background " +
                $"(boundary luminance={mean:F4}, background luminance={backgroundLuminance:F4}).");
        }

        private void AssertRegionDistinctFromBackground(
            Texture2D texture, Camera camera, Rect footprintWorld, float backgroundLuminance, string context)
        {
            var screenRect = WorldToScreenRect(camera, footprintWorld);
            var (mean, variance) = RegionLuminanceStats(texture, screenRect);
            Assert.That(Mathf.Abs(mean - backgroundLuminance), Is.GreaterThan(FootprintLuminanceDelta),
                $"{context}: the footprint silhouette must remain visually distinct from the background " +
                $"(footprint luminance={mean:F4}, background luminance={backgroundLuminance:F4}).");
            Assert.That(variance, Is.GreaterThan(0f),
                $"{context}: the footprint region must retain internal detail, not render as a single flat value.");
        }

        private void AssertBadgeSymbolRetained(Texture2D texture, Camera camera, Rect badgeWorld, string context)
        {
            var screenRect = WorldToScreenRect(camera, badgeWorld);
            var (_, variance) = RegionLuminanceStats(texture, screenRect);
            Assert.That(variance, Is.GreaterThan(BadgeVarianceMinimum),
                $"{context}: the status badge must retain a recognizable symbol shape (luminance variance " +
                $"{variance:F6} at or below the flat-swatch floor {BadgeVarianceMinimum:F6} would mean the badge " +
                "rendered as an undifferentiated patch, i.e. the symbol was lost).");
        }

        /// <summary>Matches <see cref="FactoryView"/>'s AddBadge placement: centered above the footprint.</summary>
        private static Rect BadgeWorldRect(GridPosition anchor, float footprintWidth, float footprintHeight)
        {
            var badgeWidth = 1f; // 64px source / 64 PPU.
            var badgeHeight = 0.5f; // 32px source / 64 PPU.
            var centerX = anchor.X + footprintWidth * 0.5f;
            var centerY = anchor.Y + footprintHeight - 0.25f;
            return new Rect(centerX - badgeWidth * 0.5f, centerY - badgeHeight * 0.5f, badgeWidth, badgeHeight);
        }

        // ---- scene/state construction helpers --------------------------------------------------------------

        private static FactoryState BuildSeededD001State(DefinitionCatalog catalog, GridPosition anchor)
        {
            var definitionId = new BuildingDefinitionId("dev.machine.basic");
            var definition = catalog.Resolve(definitionId);
            var recipe = definition.RecipeId.HasValue ? catalog.Resolve(definition.RecipeId.Value) : null;
            var instance = new BuildingInstanceState(
                new BuildingInstanceId(new Guid(9, 0, 0, new byte[8])),
                definition,
                anchor,
                0,
                true,
                definition.InputBuffer != null ? new ResourceBufferState(definition.InputBuffer, EmptyQuantities(definition.InputBuffer)) : null,
                definition.OutputBuffer != null ? new ResourceBufferState(definition.OutputBuffer, EmptyQuantities(definition.OutputBuffer)) : null,
                recipe != null ? new ProductionState(recipe, 0) : null);
            return new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 16, 16),
                catalog,
                new[] { instance });
        }

        /// <summary>Every buffer must declare each of its capacity resources exactly once (zero quantity here).</summary>
        private static IEnumerable<KeyValuePair<ResourceId, long>> EmptyQuantities(BufferDefinition buffer) =>
            buffer.Capacities.Keys.Select(resourceId => new KeyValuePair<ResourceId, long>(resourceId, 0L));

        private static DefinitionCatalog CreateAlternateCatalog()
        {
            var alternate = new BuildingDefinition(
                new BuildingDefinitionId("test.machine.alternate-footprint"),
                new Footprint(3, 1),
                new[] { 0, 1 },
                null,
                null,
                null,
                PresentationSpriteCatalog.AlternateNormal,
                true);
            return new DefinitionCatalog(
                Array.Empty<ResourceDefinition>(),
                Array.Empty<RecipeDefinition>(),
                new[] { alternate },
                new[]
                {
                    new OfflinePolicyDefinition(
                        new OfflinePolicyRevision(1),
                        DefinitionCatalog.SupportedEfficiencyNumerator,
                        DefinitionCatalog.SupportedEfficiencyDenominator,
                        DefinitionCatalog.SupportedMaxCreditedTicks),
                });
        }

        private static BuildingInstanceState CreateAlternateInstance(
            DefinitionCatalog catalog, BuildingInstanceId id, GridPosition anchor, int orientation)
        {
            var definition = catalog.Resolve(new BuildingDefinitionId("test.machine.alternate-footprint"));
            return new BuildingInstanceState(id, definition, anchor, orientation, true, null, null, null);
        }

        private static FactorySession CreateRunningSession(FactoryState state)
        {
            var session = new FactorySession(state, state.Catalog, new FixedUtcClock(), new RejectingCheckpointStore());
            Transition(session, LifecycleState.Loading);
            Transition(session, LifecycleState.Running);
            return session;
        }

        private static void Transition(FactorySession session, LifecycleState state)
        {
            var method = typeof(FactorySession).GetMethod("TransitionTo", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = (CommandResult)method.Invoke(session, new object[] { state });
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private Camera CreateCaptureCamera()
        {
            var texture = new RenderTexture(ViewportWidth, ViewportHeight, 16);
            texture.Create();
            transientObjects.Add(texture);

            var gameObject = NewObject("zoom-readability-camera");
            var camera = gameObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = MaximumOrthographicSize;
            camera.targetTexture = texture;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Matches Assets/IdleFactory/Scenes/CoreGameFoundation.unity's authored Camera background exactly.
            camera.backgroundColor = new Color(0.035f, 0.047f, 0.055f, 1f);
            camera.transform.position = new Vector3(FactoryWorldBounds.center.x, FactoryWorldBounds.center.y, -10f);
            return camera;
        }

#if UNITY_EDITOR
        private static PresentationSpriteCatalog CreateProductionSpriteCatalog()
        {
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(new[]
            {
                Entry(PresentationSpriteCatalog.GridTile, "f001-grid-tile-1x1-v01.png"),
                Entry(PresentationSpriteCatalog.BoundaryEdge, "f001-grid-boundary-edge-v01.png"),
                Entry(PresentationSpriteCatalog.BoundaryCorner, "f001-grid-boundary-corner-v01.png"),
                Entry(PresentationSpriteCatalog.D001Normal, "f001-machine-d001-normal-v01.png"),
                Entry(PresentationSpriteCatalog.D001Selected, "f001-machine-d001-selected-v01.png"),
                Entry(PresentationSpriteCatalog.AlternateNormal, "f001-machine-alt-3x1-normal-v01.png"),
                Entry(PresentationSpriteCatalog.AlternateSelected, "f001-machine-alt-3x1-selected-v01.png"),
                Entry(PresentationSpriteCatalog.D001GhostValid, "f001-ghost-d001-valid-v01.png"),
                Entry(PresentationSpriteCatalog.D001GhostInvalid, "f001-ghost-d001-invalid-v01.png"),
                Entry(PresentationSpriteCatalog.AlternateGhostValid, "f001-ghost-alt-3x1-valid-v01.png"),
                Entry(PresentationSpriteCatalog.AlternateGhostInvalid, "f001-ghost-alt-3x1-invalid-v01.png"),
                Entry(PresentationSpriteCatalog.ResourceInput, "f001-resource-dev-input-v01.png"),
                Entry(PresentationSpriteCatalog.ResourceOutput, "f001-resource-dev-output-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeNormal, "f001-badge-normal-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeSelected, "f001-badge-selected-v01.png"),
                Entry(PresentationSpriteCatalog.BadgePlacementValid, "f001-badge-placement-valid-v01.png"),
                Entry(PresentationSpriteCatalog.BadgePlacementInvalid, "f001-badge-placement-invalid-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeProducing, "f001-badge-producing-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeMissingInput, "f001-badge-missing-input-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeFullOutput, "f001-badge-full-output-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeInactive, "f001-badge-inactive-v01.png"),
            });
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            return catalog;
        }

        private static PresentationSpriteEntry Entry(string key, string fileName)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + fileName);
            Assert.That(sprite, Is.Not.Null, SpriteRoot + fileName);
            return new PresentationSpriteEntry(key, sprite);
        }
#else
        private static PresentationSpriteCatalog CreateProductionSpriteCatalog()
        {
            throw new PlatformNotSupportedException("Sprite loading by asset path is available only in the Editor.");
        }
#endif

        private GameObject NewObject(string name)
        {
            var gameObject = new GameObject(name);
            objects.Add(gameObject);
            return gameObject;
        }

        private sealed class NeverCalledIdSource : IBuildingInstanceIdSource
        {
            public CommandResult<BuildingInstanceId> Next() =>
                throw new InvalidOperationException("This fixture seeds instances directly; placement is not exercised.");
        }

        private sealed class FixedUtcClock : IUtcClock
        {
            public DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        }

        private sealed class RejectingCheckpointStore : ICheckpointStore
        {
            public CheckpointReadResult ReadActive() => CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "none");
            public CheckpointReadResult ReadPrevious() => CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "none");
            public CheckpointReadResult Read(CheckpointId checkpointId) => CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "none");
            public CommandResult<CheckpointId> Promote(Checkpoint checkpoint) => CommandResult<CheckpointId>.Success(checkpoint.Id);
            public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate) => CommandResult<CheckpointId>.Success(recoveryCandidate.Id);
        }
    }
}
