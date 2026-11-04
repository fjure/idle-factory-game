using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>One supported screen class in panel points (1 px per point) with its safe-area insets (UX-1, UX-3).</summary>
    public sealed class ShellScreen
    {
        public ShellScreen(string name, int width, int height, int left, int top, int right, int bottom, FactoryHudLayout expected)
        {
            Name = name;
            Width = width;
            Height = height;
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
            Expected = expected;
        }

        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public int Left { get; }
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }
        public FactoryHudLayout Expected { get; }
        public bool IsPhone => !FactoryHudDocument.HasSidePanel(Expected);

        /// <summary>The Screen.safeArea convention: bottom-left origin, in pixels.</summary>
        public Rect SafeAreaPx => new Rect(Left, Bottom, Width - Left - Right, Height - Top - Bottom);

        public override string ToString() => Name;
    }

    /// <summary>The screen classes of contracts/ui-surfaces.md (Global): 360x640, 390x844 and 768x1024 with their rotations, 1280x720 and 1920x1080.</summary>
    public static class ShellScreens
    {
        public static IEnumerable<ShellScreen> All()
        {
            yield return new ShellScreen("phone-portrait 360x640", 360, 640, 0, 24, 0, 16, FactoryHudLayout.CompactPortrait);
            yield return new ShellScreen("phone-landscape 640x360", 640, 360, 44, 0, 44, 16, FactoryHudLayout.CompactLandscape);
            yield return new ShellScreen("phone-portrait 390x844", 390, 844, 0, 47, 0, 34, FactoryHudLayout.CompactPortrait);
            yield return new ShellScreen("phone-landscape 844x390", 844, 390, 47, 0, 47, 21, FactoryHudLayout.CompactLandscape);
            yield return new ShellScreen("tablet-portrait 768x1024", 768, 1024, 0, 24, 0, 20, FactoryHudLayout.TabletPortrait);
            yield return new ShellScreen("tablet-landscape 1024x768", 1024, 768, 0, 24, 0, 20, FactoryHudLayout.TabletLandscape);
            yield return new ShellScreen("desktop 1280x720", 1280, 720, 0, 0, 0, 0, FactoryHudLayout.Desktop);
            yield return new ShellScreen("desktop 1920x1080", 1920, 1080, 0, 0, 0, 0, FactoryHudLayout.Desktop);
        }

        public static IEnumerable<ShellScreen> Phones() => All().Where(screen => screen.IsPhone);

        public static IEnumerable<ShellScreen> SidePanels() => All().Where(screen => !screen.IsPhone);

        /// <summary>The same device turned the other way, with the insets rotated with it.</summary>
        public static ShellScreen Rotated(ShellScreen screen)
        {
            var landscape = screen.Width > screen.Height;
            var width = screen.Height;
            var height = screen.Width;
            var expected = FactoryHudDocument.Classify(width, height);
            return landscape
                ? new ShellScreen(screen.Name + " rotated", width, height, 0, 24, 0, 16, expected)
                : new ShellScreen(screen.Name + " rotated", width, height, 44, 0, 44, 16, expected);
        }
    }

    /// <summary>A panel a test mounts into the shell: a button list and a long text, as the worst case of a real panel.</summary>
    public sealed class TestShellPanel : IShellPanel
    {
        private readonly List<Button> buttons = new List<Button>();

        public TestShellPanel(string id, string title, int buttonCount = 12, bool longText = true, bool shortLabels = false)
        {
            Id = id;
            Title = title;
            var root = new VisualElement { name = "test-panel-" + id };
            if (longText)
            {
                var text = new Label(
                    "Worst-case panel content. " + string.Concat(Enumerable.Repeat("A long line of panel text that wraps over several lines. ", 8)))
                {
                    name = "test-panel-text",
                };
                text.AddToClassList("run-panel__empty");
                root.Add(text);
            }

            for (var i = 0; i < buttonCount; i++)
            {
                var button = RunShellDocument.CreateButton("test-panel-button-" + i, shortLabels ? "Action " + (i + 1) : "Panel action number " + (i + 1) + " with a long label that may wrap");
                buttons.Add(button);
                root.Add(button);
            }

            Root = root;
        }

        public string Id { get; }
        public string Title { get; }
        public VisualElement Root { get; }
        public IReadOnlyList<Button> Buttons => buttons;
        public int ShownCount { get; private set; }
        public int HiddenCount { get; private set; }
        public int RefreshCount { get; private set; }
        public int LastSlot { get; private set; } = -2;

        public void OnShown(ShellPanelContext context)
        {
            ShownCount++;
            LastSlot = context.SlotIndex;
        }

        public void Refresh() => RefreshCount++;

        public void OnHidden() => HiddenCount++;
    }

    /// <summary>Builds the run shell into an off-screen panel at a declared screen class, as the layout tests of T053 do for the HUD.</summary>
    public sealed class ShellRig : IDisposable
    {
        private readonly List<Object> owned = new List<Object>();

        private ShellRig(ShellScreen screen)
        {
            Screen = screen;
        }

        public ShellScreen Screen { get; private set; }

        /// <summary>The text-scale step the rig applied (100 to 200).</summary>
        public int TextScalePercent { get; private set; } = 100;

        /// <summary>The device profile the rig applied (the shell default: touch, unknown DPI).</summary>
        public ShellDeviceProfile Profile { get; private set; } = ShellDeviceProfile.Default;

        public RunShellDocument Shell { get; private set; }
        public UIDocument Document { get; private set; }
        public RenderTexture Texture { get; private set; }
        public VisualElement Root => Shell.Root;

        public static ShellRig Create(ShellScreen screen) => Create(screen, null, 100);

        /// <summary>
        /// Builds the shell at a screen class with a text-scale step and an optional device profile (T191). The rig turns the
        /// shell's own device probing off, so the profile and text scale are exactly the ones the test names. Panel points stay
        /// 1:1 with pixels (no panel-scale change), so a profile with a known DPI belongs in <see cref="CreateAuthored"/>.
        /// </summary>
        public static ShellRig Create(ShellScreen screen, ShellDeviceProfile? profile, int textScalePercent)
        {
            var rig = new ShellRig(screen);
            rig.Texture = new RenderTexture(screen.Width, screen.Height, 24);
            rig.Texture.Create();
            rig.owned.Add(rig.Texture);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.targetTexture = rig.Texture;
            rig.owned.Add(settings);

            var host = new GameObject("run-shell-rig");
            rig.owned.Add(host);
            rig.Document = host.AddComponent<UIDocument>();
            rig.Document.panelSettings = settings;
            rig.Shell = host.AddComponent<RunShellDocument>();
            rig.Shell.AutoProbeDevice = false;
#if UNITY_EDITOR
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/IdleFactory/Unity/UI/FactoryRun.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/IdleFactory/Unity/UI/FactoryRun.uss");
            Assert.That(tree, Is.Not.Null);
            Assert.That(styles, Is.Not.Null);
            Assert.That(rig.Shell.Configure(rig.Document, tree, styles, LoadIcon), Is.True, rig.Shell.Error);
#endif
            rig.ApplyProfileAndScale(profile, textScalePercent);
            return rig;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Builds the shell into an off-screen panel of a device's pixel size with a copy of the authored
        /// <c>CoreGameFoundationPanelSettings</c> (scale with screen size), so the panel scale the device profile decides is
        /// the one the panel really uses. The test screen carries the device's pixel size and the layout class the panel-point
        /// size is expected to give.
        /// </summary>
        public static ShellRig CreateAuthored(ShellScreen screen, ShellDeviceProfile profile, int textScalePercent = 100)
        {
            var rig = new ShellRig(screen);
            rig.Texture = new RenderTexture(screen.Width, screen.Height, 24);
            rig.Texture.Create();
            rig.owned.Add(rig.Texture);

            var authored = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/IdleFactory/Unity/UI/CoreGameFoundationPanelSettings.asset");
            Assert.That(authored, Is.Not.Null);
            var settings = Object.Instantiate(authored);
            settings.targetTexture = rig.Texture;
            rig.owned.Add(settings);

            var host = new GameObject("run-shell-rig");
            rig.owned.Add(host);
            rig.Document = host.AddComponent<UIDocument>();
            rig.Document.panelSettings = settings;
            rig.Shell = host.AddComponent<RunShellDocument>();
            rig.Shell.AutoProbeDevice = false;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/IdleFactory/Unity/UI/FactoryRun.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/IdleFactory/Unity/UI/FactoryRun.uss");
            Assert.That(rig.Shell.Configure(rig.Document, tree, styles, LoadIcon), Is.True, rig.Shell.Error);
            rig.ApplyProfileAndScale(profile, textScalePercent);
            return rig;
        }
#endif

        /// <summary>Records a text-scale step applied to the shell directly, so the layout assertions know the expected font sizes.</summary>
        public void SetTextScalePercent(int percent) => TextScalePercent = percent;

        private void ApplyProfileAndScale(ShellDeviceProfile? profile, int textScalePercent)
        {
            Profile = profile ?? ShellDeviceProfile.Default;
            Shell.ApplyDeviceProfile(Profile);
            TextScalePercent = textScalePercent;
            Shell.ApplyTextScale(textScalePercent / 100f);
        }

        /// <summary>The shell's panel scale in force: the one it raised, or the panel's own.</summary>
        public float PixelsPerPoint => Root.panel.scaledPixelsPerPoint;

#if UNITY_EDITOR
        private static Sprite LoadIcon(string key) =>
            UiIconSprites.Load(key);
#endif

        /// <summary>Lays the shell out for a screen class exactly as the bootstrap does and lets UI Toolkit resolve it.</summary>
        public IEnumerator Layout(ShellScreen screen = null)
        {
            if (screen != null)
            {
                Screen = screen;
            }

            yield return null;
            Shell.ApplyScreenLayout(Screen.Width, Screen.Height, Screen.SafeAreaPx);
            yield return Settle();
        }

        public static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            yield return null;
        }

        /// <summary>The safe area in panel-point coordinates with a top-left origin, as <c>worldBound</c> reports.</summary>
        public Rect SafeRect()
        {
            var panelBounds = Root.panel.visualTree.worldBound;
            return Rect.MinMaxRect(
                panelBounds.xMin + Screen.Left,
                panelBounds.yMin + Screen.Top,
                panelBounds.xMax - Screen.Right,
                panelBounds.yMax - Screen.Bottom);
        }

        public void Dispose()
        {
            foreach (var item in owned)
            {
                if (item is RenderTexture texture)
                {
                    RenderTexture.active = null;
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

    /// <summary>A live Feature 003 run session over the real Iron content, for read-model tests without the scene.</summary>
    public static class RunShellFixtures
    {
#if UNITY_EDITOR
        private static IdleFactory.Run.Content.RunCatalog shippedCatalog;

        /// <summary>The shipped Iron catalog, so panel tests derive prices and rewards instead of hard-coding one content revision.</summary>
        public static IdleFactory.Run.Content.RunCatalog ShippedCatalog
        {
            get
            {
                if (shippedCatalog == null)
                {
                    var loaded = GameplayContentDispatcher.Load(RunSceneRun.IronBytes());
                    Assert.That(loaded.IsSuccess, Is.True);
                    shippedCatalog = loaded.RunCatalog;
                }

                return shippedCatalog;
            }
        }

        /// <summary>Cash for the first level of a machine type (its build cost).</summary>
        public static long BuildCost(string machineId) => ShippedCatalog.Machines[machineId].CostTable[0];

        public static long MinerCost => BuildCost("machine.miner");

        /// <summary>The build price of the next copy of a type when <paramref name="owned"/> copies stand, before discounts, at prestige count 0 (the content curve, revision 3 grows it per owned copy).</summary>
        public static long NextBuildCost(string machineId, long owned)
        {
            Assert.That(IdleFactory.Run.Application.BuildPricing.TryCurve(ShippedCatalog.Machines[machineId].BuildPrice, owned, 0, out var price), Is.True);
            return price;
        }

        /// <summary>Cash price of factory level <paramref name="level"/> (the upgrade that reaches it).</summary>
        public static long FactoryLevelCost(int level) => ShippedCatalog.FactoryLevels[level - 1].Cost;

        /// <summary>Highest level of a machine type.</summary>
        public static int MachineMaxLevel(string machineId) => ShippedCatalog.Machines[machineId].MaxLevel;

        /// <summary>Cycle time in milliseconds of a machine type at level 1 (the duration of its recipe).</summary>
        public static long CycleMs(string machineId)
        {
            var machine = ShippedCatalog.Machines[machineId];
            return ShippedCatalog.Recipes[machine.RecipeId].DurationTicks / RunSession.TicksPerMs;
        }

        /// <summary>Cash refunded when a machine that cost <paramref name="invested"/> is removed.</summary>
        public static long RemovalRefund(long invested) => invested * ShippedCatalog.RunStart.RemovalRefundPpm / 1_000_000L;

        /// <summary>The Cash a test starts with so that it can build and upgrade a Miner a few times without being the subject of the test.</summary>
        public static long ComfortableCash => MinerCost * 8;

        /// <summary>Length of an Overdrive boost in milliseconds.</summary>
        public static long OverdriveDurationMs => ShippedCatalog.Overdrive.DurationMs;

        /// <summary>Cooldown after an Overdrive boost in milliseconds.</summary>
        public static long OverdriveCooldownMs => ShippedCatalog.Overdrive.CooldownMs;

        /// <summary>Factory level at which Overdrive unlocks.</summary>
        public static int OverdriveUnlockLevel => ShippedCatalog.Overdrive.Unlock.Level;

        /// <summary>Capacity of storage level <paramref name="level"/> (1-based).</summary>
        public static long StorageCapacity(int level) => ShippedCatalog.StorageLevels[level - 1].Capacity;

        /// <summary>Cash price of the upgrade that reaches storage level <paramref name="level"/>.</summary>
        public static long StorageLevelCost(int level) => ShippedCatalog.StorageLevels[level - 1].Cost;

        /// <summary>Number of storage levels of the shipped content (the maximum level).</summary>
        public static int StorageLevelCount => ShippedCatalog.StorageLevels.Count;

        /// <summary>Slot count of factory level <paramref name="level"/> (1-based).</summary>
        public static long FactorySlots(int level) => ShippedCatalog.FactoryLevels[level - 1].SlotCount;

        /// <summary>Number of factory levels of the shipped content (the maximum level).</summary>
        public static int FactoryLevelCount => ShippedCatalog.FactoryLevels.Count;

        /// <summary>Units a first completion of an offer asks for.</summary>
        public static long OfferQuantity(string offerId) => ShippedCatalog.ContractOffers[offerId].QuantityTable[0];

        /// <summary>RP of the first completion of an offer.</summary>
        public static long OfferRp(string offerId) => ShippedCatalog.ContractOffers[offerId].Rewards.First(reward => reward.Kind == "rp").ValueTable[0];

        /// <summary>Time limit of a timed offer in milliseconds.</summary>
        public static long OfferDurationMs(string offerId) => ShippedCatalog.ContractOffers[offerId].DurationMs.Value;

        /// <summary>Cooldown of an offer in milliseconds.</summary>
        public static long OfferCooldownMs(string offerId) => ShippedCatalog.ContractOffers[offerId].CooldownMs;

        /// <summary>Labels of the Items the ladder grants on the completion that follows <paramref name="runCompletions"/> completions of the run.</summary>
        public static string[] DueItemLabels(long runCompletions) => ShippedCatalog.ContractItemLadder.Entries
            .Where(entry => entry.Index == runCompletions).Select(entry => ShippedCatalog.Items[entry.ItemId].Label).ToArray();

        /// <summary>A duration as the panels write a countdown: minutes, a colon and two-digit seconds (rounded up as the panel does).</summary>
        public static string Clock(long milliseconds)
        {
            var seconds = (milliseconds + 999) / 1000;
            return (seconds / 60) + ":" + (seconds % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Cash of the first completion of an offer.</summary>
        public static long ContractCash(string offerId)
        {
            var offer = ShippedCatalog.ContractOffers[offerId];
            Assert.That(ContractRewards.TryCash(offer.QuantityTable[0], ShippedCatalog.Resources[offer.ResourceId].BasePrice, 1_000_000, offer.RewardRatioPpm, out long cash), Is.True);
            return cash;
        }

        /// <summary>The same Cash as the panel writes it (thousands separators).</summary>
        public static string ContractCashText(string offerId) => ContractCash(offerId).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        public static RunSession NewSession(out RunCommands commands, int factoryLevel = 1, long cash = 25, bool unlockEverything = false, byte[] content = null)
        {
            var loaded = GameplayContentDispatcher.Load(content ?? RunSceneRun.IronBytes());
            Assert.That(loaded.IsSuccess, Is.True);
            var catalog = loaded.RunCatalog;
            var permanent = new PermanentState(catalog);
            var start = NextRunStart.Compute(catalog, permanent);
            IEnumerable<string> unlocks = catalog.RunStart.InitialUnlocks;
            if (unlockEverything)
            {
                unlocks = catalog.RunStart.InitialUnlocks.Concat(catalog.Machines.Keys).Concat(catalog.Resources.Keys)
                    .Concat(new[] { RunState.OverdriveUnlockId }).Distinct().ToArray();
            }

            var startMs = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var state = new RunState(catalog, Guid.NewGuid(), 1, null, cash, 0, start.StorageLevel, factoryLevel, unlocks);
            Assert.That(new RunSimulator(catalog).Advance(state, permanent, 0, startMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            var session = new RunSession(catalog, state, permanent, startMs);
            commands = new RunCommands(session);
            return session;
        }

        /// <summary>The Overdrive control status in each stage, taken from a real session (the status has no public constructor).</summary>
        public static OverdriveStatus OverdriveStatusIn(OverdriveStage stage)
        {
            var session = NewSession(out var commands, factoryLevel: OverdriveUnlockLevel, cash: 1_000, unlockEverything: stage != OverdriveStage.Locked);
            if (stage == OverdriveStage.Active || stage == OverdriveStage.CoolingDown)
            {
                Assert.That(commands.ActivateOverdrive().IsSuccess, Is.True);
            }

            if (stage == OverdriveStage.CoolingDown)
            {
                Assert.That(session.Advance((OverdriveDurationMs + 1_000L) * RunSession.TicksPerMs).IsSuccess, Is.True);
            }

            var status = RunSnapshots.Overdrive(session);
            Assert.That(status.State, Is.EqualTo(stage));
            return status;
        }
#endif

        public static string PresentationKeyOf(RunSession session, string machineId) => session.Catalog.Machines[machineId].PresentationKey;
    }
}
