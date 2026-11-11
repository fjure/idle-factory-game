#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// The fixed run of the F006 baseline: the shipped Iron content at the highest factory level with eight machines and four empty
    /// slots, advanced 45 s so that part of the chain starves. Every number the baseline reports derives from this one run.
    /// </summary>
    public static class BaselineFixture
    {
        public const long Cash = 50_000_000L;
        public const long AdvanceMs = 45_000L;

        public static readonly (int Slot, string MachineId)[] Machines =
        {
            (0, "machine.miner"), (1, "machine.miner"), (2, "machine.miner"),
            (3, "machine.smelter"), (4, "machine.smelter"), (5, "machine.smelter"),
            (6, "machine.press"), (7, "machine.assembler"),
        };

        /// <summary>The first empty, unlocked slot (the build sheet opens here).</summary>
        public const int EmptySlot = 8;

        public static string Description =>
            "Iron content, factory level " + RunShellFixtures.FactoryLevelCount + " (" + RunShellFixtures.FactorySlots(RunShellFixtures.FactoryLevelCount) + " slots), Cash " + Cash.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ", 3 miners (slots 1-3), 3 smelters (4-6), 1 press (7), 1 assembler (8), slots 9-12 empty, all contract offers unlocked, advanced "
            + (AdvanceMs / 1000L).ToString(System.Globalization.CultureInfo.InvariantCulture) + " s";

        public static RunSession Create(out RunCommands commands)
        {
            var session = RunShellFixtures.NewSession(out commands, RunShellFixtures.FactoryLevelCount, Cash, true);
            ContractsPanelTestSupport.UnlockContracts(session);
            foreach (var (slot, machineId) in Machines)
            {
                var built = commands.Build(slot, machineId);
                Assert.That(built.IsSuccess, Is.True, "build " + machineId + " in slot " + slot + ": " + built.ReasonCode);
            }

            Assert.That(session.Advance(AdvanceMs * RunSession.TicksPerMs).IsSuccess, Is.True);
            return session;
        }

        /// <summary>The slots whose machine stands still (the world draws a stopped badge on them).</summary>
        public static int[] StoppedSlots(RunSession session) =>
            RunSnapshots.Factory(session).Machines.Where(machine => machine.IsActive && machine.Blocker != MachineBlocker.None).Select(machine => machine.SlotIndex).OrderBy(slot => slot).ToArray();
    }

    /// <summary>The sprite catalog and camera look of the authored scene, taken once (the RenderCaptureHarness approach).</summary>
    internal static class BaselineArt
    {
        private static PresentationSpriteCatalog sprites;
        private static Color background;
        private static CameraClearFlags flags;
        private static float cameraZ;
        private static float near;
        private static float far;

        public static PresentationSpriteCatalog Sprites => sprites;
        public static Color Background => background;
        public static CameraClearFlags Flags => flags;
        public static float CameraZ => cameraZ;
        public static float Near => near;
        public static float Far => far;

        public static IEnumerator Ensure()
        {
            if (sprites != null && sprites.Validate())
            {
                yield break;
            }

            var root = RunSceneRun.NewRoot();
            var run = new RunSceneRun(root, new RunSceneClock());
            yield return run.Start(RunSceneRun.IronBytes, true);
            var bootstrap = run.Bootstrap;
            var catalog = bootstrap.SpriteCatalog;
            var valid = catalog.IsValid;
            var camera = bootstrap.FactoryCamera;
            var color = camera.backgroundColor;
            color.a = 1f;
            var clear = camera.clearFlags;
            var z = camera.transform.position.z;
            var nearClip = camera.nearClipPlane;
            var farClip = camera.farClipPlane;
            yield return run.Stop();
            RunSceneRun.DeleteRoot(root);
            Assert.That(valid, Is.True, "the scene's sprite catalog is valid");
            sprites = catalog;
            background = color;
            flags = clear;
            cameraZ = z;
            near = nearClip;
            far = farClip;
        }
    }

    /// <summary>
    /// The current game composed off-screen at a screen class: the real shell with the real panels, the slot grid, the run presenter and
    /// the production camera framing, driven by the fixed baseline run. Nothing here changes production behaviour; it only mounts the
    /// same classes the bootstrap mounts, at a screen size the batch editor cannot give the scene.
    /// </summary>
    internal sealed class BaselineRig : IDisposable
    {
        private const string PanelRoot = "Assets/IdleFactory/Unity/UI/Panels/";
        private readonly List<Object> owned = new List<Object>();
        private readonly List<IDisposable> registrations = new List<IDisposable>();
        private Rect closedRect;
        private bool framed;

        private BaselineRig()
        {
        }

        public ShellRig Shell { get; private set; }
        public RunSession Session { get; private set; }
        public RunCommands Commands { get; private set; }
        public SlotGridView Grid { get; private set; }
        public RunShellPresenter Presenter { get; private set; }
        public FactoryCameraController Controller { get; private set; }
        public Camera Camera { get; private set; }
        public RenderTexture WorldTexture { get; private set; }
        public RunCameraFraming Framing { get; private set; }
        public MachinePanelPresenter Machine { get; private set; }
        public BuildSheetPresenter Build { get; private set; }
        public OfflineSummaryPanel Offline { get; private set; }
        public int Columns { get; private set; }

        public RunShellDocument ShellDocument => Shell.Shell;
        public ShellScreen Screen => Shell.Screen;

        private static VisualTreeAsset Tree(string name) => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + name + ".uxml");

        private static StyleSheet Styles(string name) => AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + name + ".uss");

        /// <summary>
        /// Builds the rig. <paramref name="profile"/> null gives the plain panel (1 point per pixel, the way the layout tests run); a device
        /// profile builds the panel with the authored settings, so the panel scale the device would use is the one that is measured.
        /// </summary>
        public static IEnumerator Create(ShellScreen screen, int percent, ShellDeviceProfile? profile, Action<BaselineRig> done)
        {
            yield return BaselineArt.Ensure();
            var rig = new BaselineRig();
            rig.Shell = profile.HasValue ? ShellRig.CreateAuthored(screen, profile.Value, percent) : ShellRig.Create(screen, null, percent);

            // The shell column draws nothing, so the panel clears its target before every frame (workstation pitfall: an uncleared
            // target keeps earlier frames and doubles the strip and the tab bar).
            var settings = rig.Shell.Document.panelSettings;
            settings.clearColor = true;
            settings.colorClearValue = Color.clear;

            rig.Session = BaselineFixture.Create(out var commands);
            rig.Commands = commands;
            var catalog = rig.Session.Catalog;

            var gridObject = new GameObject("f006-baseline-grid");
            rig.owned.Add(gridObject);
            rig.Grid = gridObject.AddComponent<SlotGridView>();
            rig.Grid.Configure(BaselineArt.Sprites);
            rig.Columns = SlotGridLayout.ColumnsFor(screen.Width, screen.Height);
            rig.Grid.SetColumns(rig.Columns);

            var shell = rig.Shell.Shell;
            Func<string, Sprite> icons = ContractsPanelTestSupport.Icon;
            rig.Build = new BuildSheetPresenter(rig.Session, commands, Tree("BuildSheetPanel"), Styles("BuildSheetPanel"), icons);
            rig.Machine = new MachinePanelPresenter(rig.Session, commands, Tree("MachinePanel"), Styles("MachinePanel"), icons);
            rig.registrations.Add(shell.Panels.Register(rig.Build));
            rig.registrations.Add(shell.Panels.Register(rig.Machine));
            rig.registrations.Add(shell.Panels.Register(new MarketPanelHost(rig.Session, commands, shell, Tree("InventoryRatesPanel"), Styles("InventoryRatesPanel"),
                Tree("PrioritiesPanel"), Styles("PrioritiesPanel"), Tree("ExpansionPanel"), Styles("ExpansionPanel"), icons)));
            rig.registrations.Add(shell.Panels.Register(new ContractsPanel(rig.Session, commands, Styles("ContractsPanel"), icons)));
            rig.registrations.Add(shell.Panels.Register(new ItemsPanel(rig.Session, commands, Styles("ItemsPanel"), icons)));
            rig.registrations.Add(shell.Panels.Register(new ResearchPanel(rig.Session, commands, Styles("ResearchPanel"), icons,
                new IndustryPanel(rig.Session, commands, Styles("IndustryPanel"), icons))));
            rig.registrations.Add(shell.Panels.Register(new PrestigePresenter(rig.Session, commands, Tree("PrestigePanel"), Styles("PrestigePanel"), icons)));
            rig.Offline = new OfflineSummaryPanel(catalog, () => throw new InvalidOperationException("the baseline never retries a return"), Styles("OfflineSummaryPanel"), icons);

            rig.Presenter = new RunShellPresenter(rig.Session, commands, shell, rig.Grid,
                id => catalog.Machines.TryGetValue(id, out var machine) ? machine.PresentationKey : string.Empty);

            rig.WorldTexture = new RenderTexture(screen.Width, screen.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                antiAliasing = 1,
                useMipMap = false,
                filterMode = FilterMode.Point,
                name = "f006-baseline-world",
            };
            rig.WorldTexture.Create();
            rig.owned.Add(rig.WorldTexture);

            var cameraObject = new GameObject("f006-baseline-camera");
            rig.owned.Add(cameraObject);
            cameraObject.transform.position = new Vector3(0f, 0f, BaselineArt.CameraZ);
            rig.Camera = cameraObject.AddComponent<Camera>();
            rig.Camera.orthographic = true;
            rig.Camera.clearFlags = BaselineArt.Flags;
            rig.Camera.backgroundColor = BaselineArt.Background;
            rig.Camera.nearClipPlane = BaselineArt.Near;
            rig.Camera.farClipPlane = BaselineArt.Far;
            rig.Camera.allowMSAA = false;
            rig.Camera.allowHDR = false;
            rig.Camera.targetTexture = rig.WorldTexture;
            rig.Camera.enabled = false;
            rig.Controller = cameraObject.AddComponent<FactoryCameraController>();
            rig.Framing = new RunCameraFraming(new RunZoomSettings());
            rig.Controller.Configure(rig.Camera, rig.Framing.ScaleSettings);

            yield return rig.Shell.Layout();
            rig.Presenter.Invalidate();
            rig.Presenter.Refresh();
            shell.SetGameplayAvailable(true);
            yield return ShellRig.Settle();
            rig.ApplyViewport();
            done(rig);
        }

        /// <summary>Hands the camera the shell's current world rect the way the bootstrap does: fit once, then follow the sheet and the selection.</summary>
        public void ApplyViewport()
        {
            var shell = ShellDocument;
            if (!shell.TryGetUnobstructedScreenRect(Screen.Height, out var area))
            {
                return;
            }

            var ppp = Shell.PixelsPerPoint;
            var layout = RunCameraFraming.Layout(Camera, Grid.Columns, ppp, area);
            var selected = shell.SelectedSlot;
            if (!framed)
            {
                Controller.Fit(layout);
                framed = true;
                closedRect = area;
            }
            else if (selected < 0 && Controller.SheetOpen)
            {
                Controller.CloseSheet(closedRect);
            }
            else if (selected < 0)
            {
                Controller.UpdateLayout(layout);
                if (!shell.IsSheetOpen)
                {
                    closedRect = area;
                }
            }

            if (selected >= 0)
            {
                Controller.OpenSheet(area, Framing.FrameSlot(selected, Grid.Columns));
            }

            Grid.SetProjectedPixelsPerCell(RunCameraFraming.PixelsPerCell(Camera, ppp));
        }

        public IEnumerator Settle()
        {
            yield return ShellRig.Settle();
            Presenter.Refresh();
            ApplyViewport();
            yield return ShellRig.Settle();
        }

        // ------------------------------------------------------------------------------------------------------
        // States
        // ------------------------------------------------------------------------------------------------------

        public IEnumerator ShowNoPanel()
        {
            DismissOffline();
            ShellDocument.CloseSheet();
            yield return Settle();
        }

        public IEnumerator ShowMachineSheet(int slot)
        {
            DismissOffline();
            Assert.That(ShellDocument.OpenSlot(slot, SlotCondition.Occupied), Is.True);
            yield return Settle();
        }

        public IEnumerator ShowBuildSheet(int slot)
        {
            DismissOffline();
            Assert.That(ShellDocument.OpenSlot(slot, SlotCondition.Empty), Is.True);
            yield return Settle();
        }

        public IEnumerator ShowTab(ShellTab tab)
        {
            DismissOffline();
            ShellDocument.SelectTab(tab);
            yield return Settle();
        }

        public IEnumerator ShowOfflineSummary()
        {
            ShellDocument.CloseSheet();
            Offline.Mount(ShellDocument.Root, ShellDocument);
            Offline.ShowSummary(OfflineSummaryPanelTestSupport.NormalReturn());
            yield return Settle();
            Assert.That(Offline.IsOpen, Is.True, "the offline summary opened");
        }

        public void DismissOffline()
        {
            if (Offline != null && Offline.IsOpen)
            {
                Offline.Dismiss();
            }

            if (Offline != null && Offline.Root.parent != null)
            {
                Offline.Unmount();
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Measurement and capture
        // ------------------------------------------------------------------------------------------------------

        /// <summary>The selected slot's screen rect in pixels (bottom-left origin), from the camera.</summary>
        public Rect SlotScreenRect(int slot)
        {
            var world = SlotGridLayout.SlotRect(slot, Columns);
            var low = Camera.WorldToScreenPoint(new Vector3(world.xMin, world.yMin, 0f));
            var high = Camera.WorldToScreenPoint(new Vector3(world.xMax, world.yMax, 0f));
            return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
        }

        public ShellGeometryResult Measure(string state)
        {
            var overlay = Offline != null && Offline.IsOpen ? Offline.Card : null;
            var result = ShellGeometryProbe.Measure(Shell, state, overlay);
            var selected = ShellDocument.SelectedSlot;
            if (selected >= 0 && ShellDocument.TryGetUnobstructedScreenRect(Screen.Height, out var area))
            {
                var slot = SlotScreenRect(selected);
                // Both rects are in screen pixels with a bottom-left origin here, so the containment needs no conversion.
                result.Notes["selectedSlotVisibleShare"] = ShellGeometryProbe.ContainedShare(slot, area);
                result.Notes["selectedSlot"] = selected + 1;
            }

            // The lattice is the building area while no ground art exists: the bounding box of the 12 slots on screen, clipped to the world rect.
            if (ShellDocument.TryGetUnobstructedScreenRect(Screen.Height, out var worldPixels))
            {
                var lattice = SlotScreenRect(0);
                for (var slot = 1; slot < SlotGridLayout.SlotCount; slot++)
                {
                    var next = SlotScreenRect(slot);
                    lattice = Rect.MinMaxRect(Mathf.Min(lattice.xMin, next.xMin), Mathf.Min(lattice.yMin, next.yMin), Mathf.Max(lattice.xMax, next.xMax), Mathf.Max(lattice.yMax, next.yMax));
                }

                var ppp = Shell.PixelsPerPoint;
                var visiblePixels = ShellGeometryProbe.IntersectionArea(lattice, worldPixels);
                result.Notes["latticeShare"] = ShellGeometryProbe.Share(visiblePixels / ((double)ppp * ppp), result.SafeArea);
                result.Notes["latticeInsideWorldShare"] = ShellGeometryProbe.ContainedShare(lattice, worldPixels);
            }

            result.Notes["stoppedMachines"] = BaselineFixture.StoppedSlots(Session).Length;
            result.Notes["columns"] = Columns;
            return result;
        }

        /// <summary>One composite frame (world under shell) as a PNG; the capture is evidence only.</summary>
        public byte[] CapturePng()
        {
            Camera.Render();
            var world = Read(WorldTexture);
            var shell = Read(Shell.Texture);
            var texture = new Texture2D(Screen.Width, Screen.Height, TextureFormat.RGBA32, false, false);
            try
            {
                var pixels = new Color32[world.Length];
                for (var i = 0; i < pixels.Length; i++)
                {
                    var top = shell[i];
                    var under = world[i];
                    var alpha = top.a / 255f;
                    pixels[i] = new Color32(
                        (byte)Mathf.RoundToInt(top.r * alpha + under.r * (1f - alpha)),
                        (byte)Mathf.RoundToInt(top.g * alpha + under.g * (1f - alpha)),
                        (byte)Mathf.RoundToInt(top.b * alpha + under.b * (1f - alpha)), 255);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static Color32[] Read(RenderTexture target)
        {
            var previous = RenderTexture.active;
            var readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, false);
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                readback.Apply(false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
            }
        }

        public void Dispose()
        {
            DismissOffline();
            Presenter?.Dispose();
            foreach (var registration in registrations)
            {
                registration.Dispose();
            }

            registrations.Clear();
            Shell?.Dispose();
            foreach (var item in owned)
            {
                if (item is RenderTexture texture)
                {
                    texture.Release();
                }

                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            owned.Clear();
        }
    }
}
#endif
