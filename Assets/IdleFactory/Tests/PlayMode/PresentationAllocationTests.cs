using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T431 (FR-026): once warm, the presentation hot paths allocate nothing: the shared clock tick (accent renderers included), the
    /// state-cue refresh, the camera snap during pan and zoom, and a reduced-motion change in either direction. Measured with the
    /// <c>GC.Alloc</c> recorder (<see cref="GcAllocationCounter"/>). The class also runs tagged for the Windows test player.
    /// </summary>
    [Category("StandalonePlayer")]
    public sealed class PresentationAllocationTests
    {
        private const int D = 24;
        private const int W = 80;
        private const int H = 64;

        private static readonly string[] MachineKeys =
            { "F003-MACHINE-MINER", "F003-MACHINE-SMELTER", "F003-MACHINE-PRESS", "F003-MACHINE-ASSEMBLER" };

        private readonly List<Object> owned = new List<Object>();

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
        }

        private sealed class FixedSignal : IReducedMotionSignal
        {
            public bool Reduce;

            public bool ShouldReduceMotion() => Reduce;
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
            foreach (var key in MachineKeys)
            {
                Add(key, 6, 8f, 16);
                Add(StateCueSet.AccentKeyFor(key), 6, 8f, 16);
            }

            foreach (MachineVisualState state in System.Enum.GetValues(typeof(MachineVisualState)))
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

        private static SlotVisual Occupied(int index, MachineVisualState state, bool selected)
        {
            return new SlotVisual(index, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, MachineKeys[index % MachineKeys.Length],
                SlotGridModel.BadgeKeyOf(state), state.ToString(), true, true, selected, 0, "Slot " + (index + 1));
        }

        private static SlotVisual[] Frame(int variant)
        {
            var states = (MachineVisualState[])System.Enum.GetValues(typeof(MachineVisualState));
            return Enumerable.Range(0, SlotGridLayout.SlotCount)
                .Select(i => i >= 10
                    ? new SlotVisual(i, SlotCondition.Locked, SlotGridModel.LockedKey(2 + (i + variant) % 4), null, null, null, false, false, false,
                        2 + (i + variant) % 4, "Slot " + (i + 1) + ", locked")
                    : Occupied(i, states[(i + variant) % states.Length], i == variant % 10))
                .ToArray();
        }

        private SlotGridView NewView()
        {
            var host = new GameObject("allocation-grid");
            owned.Add(host);
            var view = host.AddComponent<SlotGridView>();
            view.Configure(Catalog());
            view.SetColumns(3);
            view.SetProjectedPixelsPerCell(48f);
            return view;
        }

        private static ReducedMotionPreference PreferenceOf(int i) =>
            i % 3 == 0 ? ReducedMotionPreference.On : (i % 3 == 1 ? ReducedMotionPreference.Off : ReducedMotionPreference.FollowSystem);

        [Test]
        public void TheClockTickWithAccentRenderersAllocatesNothingOnceWarm()
        {
            var view = NewView();
            view.Render(Frame(0));
            var clock = new PresentationAnimationClock();
            for (var i = 0; i < 40; i++)
            {
                clock.Drive(i * 17L, view);
            }

            Assert.That(Enumerable.Range(0, 12).Count(i => view.RendererOf(i, SlotGridView.SlotRendererRole.Accent).enabled), Is.GreaterThan(0),
                "the warm-up must include running machines with an accent renderer");
            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var i = 0; i < 200; i++)
                {
                    clock.Drive(1000 + i * 17L, view);
                }
            });
            Assert.That(allocations, Is.EqualTo(0));
        }

        [Test]
        public void TheCueRefreshAllocatesNothingOnceWarm()
        {
            var view = NewView();
            var frames = new[] { Frame(0), Frame(1), Frame(2), Frame(3) };
            foreach (var frame in frames)
            {
                view.Render(frame);
            }

            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var i = 0; i < 100; i++)
                {
                    view.Render(frames[i % frames.Length]);
                }
            });
            Assert.That(allocations, Is.EqualTo(0));
        }

        [Test]
        public void TheCueKeysAreCachedStrings()
        {
            var accent = StateCueSet.AccentKeyFor("F003-MACHINE-MINER");
            Assert.That(StateCueSet.AccentKeyFor("F003-MACHINE-MINER"), Is.SameAs(accent));
            Assert.That(accent, Is.EqualTo("F004-ACCENT-MINER"));
            Assert.That(StateCueSet.AccentKeyFor("F003-SLOT-EMPTY"), Is.Null);
            Assert.That(StateCueSet.AccentKeyFor(null), Is.Null);
            Assert.That(StateCueSet.LabelKeyFor(SlotCondition.Locked, 3), Is.SameAs(StateCueSet.LabelKeyFor(SlotCondition.Locked, 3)));
            Assert.That(StateCueSet.LabelKeyFor(SlotCondition.Locked, 3), Is.EqualTo(PresentationSpriteCatalog.LockLabelKeyPrefix + "3"));
            Assert.That(StateCueSet.LabelKeyFor(SlotCondition.Locked, 99), Is.EqualTo(PresentationSpriteCatalog.LockLabelKeyPrefix + "5"));
            Assert.That(StateCueSet.LabelKeyFor(SlotCondition.Occupied, 3), Is.Null);
        }

        [Test]
        public void ReducedMotionOnAndOffAllocateNothingOnceWarm()
        {
            var view = NewView();
            view.Render(Frame(0));
            var clock = new PresentationAnimationClock();
            var signal = new FixedSignal();

            for (var i = 0; i < 40; i++)
            {
                clock.Refresh(PreferenceOf(i), signal);
                clock.Drive(i * 130L, view);
            }

            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var i = 0; i < 150; i++)
                {
                    signal.Reduce = i % 2 == 0;
                    clock.Refresh(PreferenceOf(i), signal);
                    clock.Drive(10_000 + i * 130L, view);
                }
            });
            Assert.That(allocations, Is.EqualTo(0));
        }

        [Test]
        public void ReducedMotionFreezesTheLoopsFromTheNextUpdateAndOffReleasesThem()
        {
            var view = NewView();
            view.Render(Frame(0));
            var clock = new PresentationAnimationClock();
            var signal = new FixedSignal();
            var running = Enumerable.Range(0, 12).First(i => view.RendererOf(i, SlotGridView.SlotRendererRole.Accent).enabled);
            var machine = view.RendererOf(running, SlotGridView.SlotRendererRole.Machine);
            var accent = view.RendererOf(running, SlotGridView.SlotRendererRole.Accent);

            clock.Refresh(ReducedMotionPreference.Off, signal);
            clock.Drive(0, view);
            clock.Drive(1000, view);
            Assert.That(machine.sprite.name, Does.EndWith("-2"), "floor(1.0 s * 8) mod 6");
            Assert.That(accent.sprite.name, Does.EndWith("-2"));

            clock.Refresh(ReducedMotionPreference.On, signal);
            clock.Drive(1125, view);
            Assert.That(machine.sprite.name, Does.EndWith("-0"), "reduced motion forces frame 0 from the next update");
            Assert.That(accent.sprite.name, Does.EndWith("-0"));

            clock.Refresh(ReducedMotionPreference.Off, signal);
            clock.Drive(1250, view);
            Assert.That(machine.sprite.name, Does.EndWith("-4"), "floor(1.25 s * 8) mod 6");
            Assert.That(accent.sprite.name, Does.EndWith("-4"));
        }

        [Test]
        public void OnlyRunningMachinesAnimateThroughTheClockAndSuspendKeepsTheFrames()
        {
            var view = NewView();
            view.Render(Frame(0));
            var clock = new PresentationAnimationClock();
            clock.Drive(0, view);
            clock.Drive(1000, view);
            var states = (MachineVisualState[])System.Enum.GetValues(typeof(MachineVisualState));
            var sawRunning = false;
            for (var i = 0; i < 10; i++)
            {
                var machine = view.RendererOf(i, SlotGridView.SlotRendererRole.Machine);
                var running = states[i % states.Length] == MachineVisualState.Running;
                sawRunning |= running;
                Assert.That(machine.sprite.name, running ? Does.EndWith("-2") : Does.EndWith("-0"), "slot " + i + " " + states[i % states.Length]);
            }

            Assert.That(sawRunning, Is.True);
            clock.SetSuspended(true);
            clock.Drive(6000, view);
            Assert.That(view.RendererOf(0, SlotGridView.SlotRendererRole.Machine).sprite.name, Does.EndWith("-2"), "suspended: frames stay where they are");
        }

        [Test]
        public void TheCameraSnapDuringPanAndZoomAllocatesNothingOnceWarm()
        {
            var cameraObject = new GameObject("allocation-camera");
            owned.Add(cameraObject);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.enabled = false;
            var controller = cameraObject.AddComponent<FactoryCameraController>();
            controller.Configure(camera, new RunZoomSettings().ToScaleSettings());
            controller.Fit(new ScaleLayout(1080, 1920, 3f, new Rect(0f, 0f, 1080f, 1700f), 3));
            var view = NewView();
            view.Render(Frame(0));

            void Gesture(int i)
            {
                controller.PanByScreenDelta(new Vector2(7.3f - (i % 5) * 3f, 4.1f - (i % 3) * 3f));
                controller.ZoomAtScreenPosition(i % 4 == 0 ? 1f : (i % 4 == 1 ? -1f : (i % 2 == 0 ? 0.12f : -0.12f)), new Vector2(540f, 900f));
                view.SetProjectedPixelsPerCell(controller.Scale * 8f);
            }

            for (var i = 0; i < 60; i++)
            {
                Gesture(i);
            }

            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var i = 0; i < 120; i++)
                {
                    Gesture(i);
                }
            });
            Assert.That(allocations, Is.EqualTo(0));
        }
    }
}
