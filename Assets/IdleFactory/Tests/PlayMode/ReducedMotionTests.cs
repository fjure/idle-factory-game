using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T433 (FR-020, FR-020a, FR-026): the Settings sheet's choice reaches the animation clock, the loops freeze from the next frame and
    /// are released again, reduced motion touches only the looping art animation, the OS signal is read at a change and not per frame,
    /// and the save directories stay byte-identical across preference changes. Also runs tagged for the Windows test player.
    /// </summary>
    [Category("StandalonePlayer")]
    public sealed class ReducedMotionTests
    {
        private const int D = 24;
        private const int W = 80;
        private const int H = 64;
        private const string MinerKey = "F003-MACHINE-MINER";

        private readonly List<Object> owned = new List<Object>();
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = SettingsTestHarness.NewRoot();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            owned.Clear();
            SettingsTestHarness.DeleteRoot(root);
        }

        private Sprite Block(string name, int shade)
        {
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, name = name };
            var pixels = new Color32[W * H];
            for (var y = 20; y < 40; y++)
            {
                for (var x = 20 + shade; x < 40 + shade; x++)
                {
                    pixels[y * W + x] = new Color32(200, 200, 200, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, W, H), new Vector2(40f / W, 15f / H), D, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            owned.Add(texture);
            owned.Add(sprite);
            return sprite;
        }

        private PresentationSpriteCatalog Catalog()
        {
            var entries = new List<PresentationSpriteEntry>();
            var requirements = new List<PresentationSpriteRequirement>();

            void Add(string key, int frames, float? fps, int maxFrames)
            {
                var sprites = Enumerable.Range(0, frames).Select(f => Block(key + "-" + f, f)).ToArray();
                entries.Add(new PresentationSpriteEntry(key, sprites, fps));
                requirements.Add(new PresentationSpriteRequirement(key, W / (float)D, H / (float)D, 0.5f, 15f / H, 1, maxFrames, false));
            }

            Add(SlotGridModel.SlotEmptyKey, 1, null, 1);
            Add(SlotGridModel.SlotOccupiedKey, 1, null, 1);
            for (var level = 2; level <= 5; level++)
            {
                Add(SlotGridModel.LockedKey(level), 1, null, 1);
                Add(PresentationSpriteCatalog.LockLabelKeyPrefix + level, 1, null, 1);
            }

            Add(SlotGridModel.SlotSelectedFrameKey, 1, null, 1);
            Add(MinerKey, 6, 8f, 16);
            Add(StateCueSet.AccentKeyFor(MinerKey), 6, 8f, 16);
            foreach (MachineVisualState state in Enum.GetValues(typeof(MachineVisualState)))
            {
                Add(SlotGridModel.BadgeKeyOf(state), 1, null, 1);
            }

            Add(SlotGridModel.MarkerBoostedKey, 1, null, 1);
            Add(SlotGridModel.MarkerOverdriveKey, 1, null, 1);
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(entries, requirements);
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            return catalog;
        }

        private SlotGridView NewViewWithARunningMiner()
        {
            var host = new GameObject("reduced-motion-grid");
            owned.Add(host);
            var view = host.AddComponent<SlotGridView>();
            view.Configure(Catalog());
            view.SetLayout(LatticeLayoutBuilder.ShippedForColumns(3));
            view.SetProjectedPixelsPerCell(48f);
            var frame = Enumerable.Range(0, SlotGridLayout.SlotCount)
                .Select(i => new SlotVisual(i, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, MinerKey,
                    SlotGridModel.BadgeKeyOf(MachineVisualState.Running), MachineVisualState.Running.ToString(), true, true, false, 0, "Slot " + (i + 1)))
                .ToArray();
            view.Render(frame);
            return view;
        }

        [Test]
        public void ChoosingOnInTheSheetFreezesTheLoopsFromTheNextFrameAndOffReleasesThem()
        {
            var harness = new SettingsTestHarness(root);
            var presenter = harness.NewPresenter();
            var view = NewViewWithARunningMiner();
            var machine = view.RendererOf(0, SlotGridView.SlotRendererRole.Machine);
            var accent = view.RendererOf(0, SlotGridView.SlotRendererRole.Accent);

            harness.Clock.Drive(0, view);
            harness.Clock.Drive(1000, view);
            Assert.That(machine.sprite.name, Does.EndWith("-2"), "floor(1.0 s * 8) mod 6: the loop runs");

            presenter.Choose(ReducedMotionPreference.On);
            harness.Clock.Drive(1125, view);
            Assert.That(machine.sprite.name, Does.EndWith("-0"), "frozen from the next frame");
            Assert.That(accent.sprite.name, Does.EndWith("-0"));
            harness.Clock.Drive(1250, view);
            Assert.That(machine.sprite.name, Does.EndWith("-0"));
            Assert.That(harness.Clock.Milliseconds, Is.EqualTo(1250), "the clock itself keeps time: only the art loop is held");

            presenter.Choose(ReducedMotionPreference.Off);
            harness.Clock.Drive(1375, view);
            Assert.That(machine.sprite.name, Does.EndWith("-5"), "floor(1.375 s * 8) mod 6: released");
        }

        [Test]
        public void FollowSystemReadsTheSignalWhenTheChoiceChangesNotPerFrame()
        {
            var harness = new SettingsTestHarness(root, systemReduces: true);
            var presenter = harness.NewPresenter();
            var view = NewViewWithARunningMiner();
            Assert.That(harness.Clock.ReducedMotion, Is.True, "no file: Follow system, and the system reduces");

            var before = harness.Refreshes;
            for (var i = 0; i < 300; i++)
            {
                harness.Clock.Drive(i * 17L, view);
            }

            Assert.That(harness.Refreshes, Is.EqualTo(before), "the effective state is cached while frames run");

            harness.Signal.Reduce = false;
            Assert.That(harness.Clock.ReducedMotion, Is.True, "a changed OS setting is picked up at the next refresh, not mid-frame");
            presenter.OnShown(null);
            Assert.That(harness.Clock.ReducedMotion, Is.False, "opening the sheet re-resolves");
            Assert.That(presenter.EffectiveLine.text, Is.EqualTo("Following system: reduced motion off"));

            presenter.Choose(ReducedMotionPreference.On);
            presenter.Choose(ReducedMotionPreference.FollowSystem);
            Assert.That(harness.Refreshes, Is.EqualTo(before + 3));
        }

        [Test]
        public void ThePreferenceFileIsTheOnlyFileAChangeWritesAndSaveDirectoriesStayByteIdentical()
        {
            var checkpoints = Path.Combine(root, "IdleFactory", "Checkpoints", "v2");
            var sessionLog = Path.Combine(root, "IdleFactory", "SessionLog");
            Directory.CreateDirectory(checkpoints);
            Directory.CreateDirectory(sessionLog);
            File.WriteAllBytes(Path.Combine(checkpoints, "checkpoint-a.json"), new byte[] { 1, 2, 3, 4, 5 });
            File.WriteAllBytes(Path.Combine(sessionLog, "session.log"), new byte[] { 9, 8, 7 });
            var beforeCheckpoints = RunSceneRun.CaptureFiles(Path.Combine(root, "IdleFactory", "Checkpoints"));
            var beforeLog = RunSceneRun.CaptureFiles(sessionLog);

            var harness = new SettingsTestHarness(root);
            var presenter = harness.NewPresenter();
            presenter.OnShown(null);
            presenter.Choose(ReducedMotionPreference.On);
            presenter.Choose(ReducedMotionPreference.Off);
            presenter.Choose(ReducedMotionPreference.FollowSystem);
            harness.Store.Reload();

            AssertSame(beforeCheckpoints, RunSceneRun.CaptureFiles(Path.Combine(root, "IdleFactory", "Checkpoints")), "checkpoints");
            AssertSame(beforeLog, RunSceneRun.CaptureFiles(sessionLog), "session log");
            var written = Directory.GetFiles(Path.Combine(root, "IdleFactory"), "*", SearchOption.AllDirectories)
                .Select(file => file.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/'))
                .Where(file => file.Contains("/Preferences/"))
                .ToList();
            Assert.That(written, Is.EqualTo(new[] { "IdleFactory/Preferences/display-v1.json" }), "one preference file and no leftover temp file");
        }

        [Test]
        public void ReducedMotionIsAPresentationChoiceOnly()
        {
            var harness = new SettingsTestHarness(root);
            var presenter = harness.NewPresenter();
            var view = NewViewWithARunningMiner();
            harness.Clock.Drive(0, view);
            var occupied = view.RendererOf(3, SlotGridView.SlotRendererRole.Machine).sprite;
            presenter.Choose(ReducedMotionPreference.On);
            harness.Clock.Drive(500, view);
            harness.Clock.Drive(2000, view);
            Assert.That(harness.Clock.Milliseconds, Is.EqualTo(2000), "presentation time keeps running");
            Assert.That(harness.Clock.Suspended, Is.False);
            Assert.That(view.RendererOf(3, SlotGridView.SlotRendererRole.Machine).sprite.name, Does.EndWith("-0"));
            Assert.That(occupied.name, Does.EndWith("-0"));
        }

        private static void AssertSame(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after, string name)
        {
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys), name);
            foreach (var pair in before)
            {
                Assert.That(after[pair.Key], Is.EqualTo(pair.Value), name + ": " + pair.Key);
            }
        }
    }
}
