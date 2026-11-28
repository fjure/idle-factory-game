using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.Presentation.Look;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.DevTools
{
    /// <summary>
    /// T610 (ARCH-4, look-profile rule 5): a look-profile switch to a profile with another lattice pitch, driven through the real
    /// <see cref="CoreGameFoundationBootstrap"/> of the authored scene. The profile source is honoured only in a development build, so this case lives in the DevTools
    /// assembly (<c>scripts/test-unity.ps1 -DevTools</c>, <c>IDLEFACTORY_DEV</c>). The bootstrap's switch handler must rebuild the world layout from the profile's pitch
    /// (a new layout identity and new geometry), and the camera must refit to it through its single writer; a switch back to F006 restores the F006 layout bit for bit.
    /// </summary>
    public sealed class LookProfilePitchSwitchTests
    {
        private sealed class PitchSource : ILookProfileSource
        {
            private readonly Dictionary<string, LookProfile> held = new Dictionary<string, LookProfile>(StringComparer.Ordinal);

            public bool IsReady { get; private set; }

            public string SelectedProfileId { get; private set; }

            public event Action Changed;

            public void Hold(LookProfile profile) => held[profile.Id] = profile;

            public bool TryGetProfile(string id, out LookProfile profile) => held.TryGetValue(id ?? string.Empty, out profile);

            public void MarkReady()
            {
                IsReady = true;
                Changed?.Invoke();
            }

            public void Select(string id)
            {
                SelectedProfileId = id;
                Changed?.Invoke();
            }
        }

        private readonly List<Object> owned = new List<Object>();
        private IDisposable compositionOverride;
        private ILookProfileSource previousSource;
        private Scene scene;
        private string root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousSource = CoreGameFoundationBootstrap.DefaultLookProfileSource;
            LatticeLayoutBuilder.UsePitch(null);
            yield break;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
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
            compositionOverride?.Dispose();
            compositionOverride = null;
            CoreGameFoundationBootstrap.DefaultLookProfileSource = previousSource;
            LatticeLayoutBuilder.UsePitch(null);
            foreach (var item in owned)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            owned.Clear();
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
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";

        private IEnumerator WaitForCameraOn(FactoryCameraController camera, string layoutId)
        {
            for (var frame = 0; frame < 120; frame++)
            {
                if (camera.HasLayout && camera.Model.Layout.LayoutId == layoutId)
                {
                    yield break;
                }

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ASwitchToAProfileWithAnotherPitchRebuildsTheLayoutAndRefitsTheCameraAndSwitchingBackRestoresF006()
        {
            var source = new PitchSource();
            CoreGameFoundationBootstrap.DefaultLookProfileSource = source;
            root = Path.Combine(Application.temporaryCachePath, "t610-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var utcNow = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
            compositionOverride = CoreGameFoundationBootstrap.OverrideRunCompositionForTests(root, () => utcNow, () => 0d);

            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                yield return null;
            }

            scene = SceneManager.GetSceneByPath(ScenePath);
            yield return null;
            var bootstrap = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<CoreGameFoundationBootstrap>(true)).Single();
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            var camera = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<FactoryCameraController>(true)).First();
            var resolver = bootstrap.LookProfiles;
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.F006Id), "the shipped default is F006");

            // F006 before the switch: the shipped layout and a camera fitted to it.
            var f006 = bootstrap.SlotGrid.Layout;
            Assert.That(f006.Pitch, Is.EqualTo(LayoutPitch.F006));
            Assert.That(f006.LayoutId, Does.Not.Contain("+c"));
            yield return WaitForCameraOn(camera, f006.LayoutId);
            Assert.That(camera.Model.Layout.LayoutId, Is.EqualTo(f006.LayoutId), "the camera is fitted to the F006 layout");
            Assert.That(camera.Model.Layout.Bounds, Is.EqualTo(f006.Bounds));

            // A prototype with the dense pitch, over the scene's own catalog and the F006 requirement table so that it validates like the shipped profile.
            var dense = LookProfile.Create(LookProfile.PrototypeAId, true, bootstrap.ActiveSpriteCatalog, LookProfileRequirementTable.F006());
            owned.Add(dense);
            dense.ConfigureLayoutPitch(new LayoutPitchSettings { pitchWorld = new Vector2(70f / SlotGridLayout.Density, 46f / SlotGridLayout.Density), groupRule = string.Empty, minHitPoints = LayoutPitch.HitPointsFloor });
            source.Hold(dense);
            source.MarkReady();
            source.Select(LookProfile.PrototypeAId);

            // The bootstrap's switch handler has run synchronously: the active profile, the builder pitch and the world layout follow.
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.PrototypeAId), resolver.Error);
            var switched = bootstrap.SlotGrid.Layout;
            Assert.That(LatticeLayoutBuilder.ActivePitch, Is.EqualTo((LayoutPitch?)dense.LayoutPitch));
            Assert.That(switched, Is.Not.SameAs(f006));
            Assert.That(switched.Pitch, Is.EqualTo(dense.LayoutPitch));
            Assert.That(switched.LayoutId, Is.Not.EqualTo(f006.LayoutId).And.Contains("+c"), "the pitch is part of the layout identity");
            Assert.That(switched.LocationCount, Is.EqualTo(f006.LocationCount), "ids and count do not change");
            Assert.That(switched.Bounds.height, Is.LessThan(f006.Bounds.height), "the rows sit closer");
            Assert.That(switched.Validate(), Is.Empty);
            Assert.That(LatticeLayoutBuilder.Shipped(switched.Orientation), Is.SameAs(switched), "every caller of the builder reads the same layout");

            // The camera refits to the new layout (one writer: the controller).
            yield return WaitForCameraOn(camera, switched.LayoutId);
            Assert.That(camera.Model.Layout.LayoutId, Is.EqualTo(switched.LayoutId), "the camera refit on the new layout identity");
            Assert.That(camera.Model.Layout.Bounds, Is.EqualTo(switched.Bounds));

            // Back to F006: the layout is the F006 one again, bit for bit, and the camera follows.
            source.Select(LookProfile.F006Id);
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.F006Id));
            var back = bootstrap.SlotGrid.Layout;
            Assert.That(back.LayoutId, Is.EqualTo(f006.LayoutId));
            Assert.That(back.Bounds, Is.EqualTo(f006.Bounds));
            for (var i = 0; i < back.LocationCount; i++)
            {
                Assert.That(back.AnchorOf(new BuildingLocationId(i)), Is.EqualTo(f006.AnchorOf(new BuildingLocationId(i))), "anchor " + i);
            }

            yield return WaitForCameraOn(camera, f006.LayoutId);
            Assert.That(camera.Model.Layout.LayoutId, Is.EqualTo(f006.LayoutId), "the camera refit back to the F006 layout");
        }
#else
        [Test]
        public void TheBootstrapSwitchNeedsTheEditorSceneLoader()
        {
            Assert.Ignore("The scene loads through the editor in Play Mode.");
        }
#endif
    }
}
