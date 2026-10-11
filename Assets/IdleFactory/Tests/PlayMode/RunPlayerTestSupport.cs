using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// Shared harness of the Feature 003 StandalonePlayer tests (T121). Everything here compiles and runs in a built
    /// player: it needs no <c>UNITY_EDITOR</c> API, no scene and no composition seam, and it never touches
    /// <c>Application.persistentDataPath</c>. Every run uses an isolated temporary root that the test deletes.
    ///
    /// The fixtures a player needs are Play Mode <see cref="TextAsset"/> copies under
    /// <c>Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV1/Resources/RunPlayerFixtures/</c> (a <c>Resources</c> folder is the
    /// only way to package a stand-alone asset in a player without a scene). <c>SaveV1PlayerFixtureParityTests</c> (Edit
    /// Mode) proves that each copy is byte-identical to its editor-only original (QA-2).
    /// </summary>
    internal static class RunPlayerFixtures
    {
        public const string ResourceDirectory = "RunPlayerFixtures";

        /// <summary>The Save V1 fixture <c>02-idle-placed-d001</c>: one idle development machine with four Ore in its input buffer.</summary>
        public const string V1FixtureName = "02-idle-placed-d001";

        /// <summary>The checkpoint ID the V1 fixture carries (recorded as the source ID of the import).</summary>
        public const string V1FixtureCheckpointId = "11111111-1111-1111-1111-111111110002";

        /// <summary>The V1 fixture's departure instant (2026-01-01T00:00:00Z).</summary>
        public const long V1FixtureDepartureUtcMs = 1767225600000L;

        public const string IronContentName = "iron-factory-content";
        public const string DevelopmentCatalogName = "development-catalog";

        /// <summary>The Factory Value threshold of the scaled journey (the balance trace's scaled scenario, ECO-3).</summary>
        public const long ScaledThresholdFv = 100_000;

        public const long StartUtcMs = 1_700_000_000_000L;

        public static byte[] Bytes(string name)
        {
            var asset = Resources.Load<TextAsset>(ResourceDirectory + "/" + name);
            Assert.That(asset, Is.Not.Null, "The packaged player fixture '" + name + "' is missing from the build.");
            return asset.bytes;
        }

        /// <summary>The packaged Iron document, built through the production dispatcher.</summary>
        public static RunCatalog IronCatalog()
        {
            var loaded = GameplayContentDispatcher.Load(Bytes(IronContentName));
            Assert.That(loaded.RunCatalog, Is.Not.Null, string.Join("; ", loaded.Diagnostics.Select(d => d.Code + " " + d.Path)));
            return loaded.RunCatalog;
        }

        /// <summary>
        /// The packaged Iron catalog with the prestige threshold (and the IP curve divisor) divided by 100: the same game and
        /// the same commands with a shorter first run. The real threshold of content revision 1 is not reached within six
        /// simulated hours by the reference policy (balance-trace-r1.md), so the full SC-001 chain is exercised on the scaled
        /// scenario, as the balance trace does.
        /// </summary>
        public static RunCatalog ScaledCatalog()
        {
            var c = IronCatalog();
            var threshold = new ThresholdDef(ScaledThresholdFv, c.Prestige.Threshold.GrowthPpm, c.Prestige.Threshold.MaxIndex);
            var prestige = new PrestigeDef(ScaledThresholdFv, c.Prestige.CurveKind, c.Prestige.Coefficient, c.Prestige.Divisor / 100, threshold);
            return new RunCatalog(c.ContentRevision, c.Resources.Values, c.Recipes.Values, c.Machines.Values, c.StorageLevels, c.FactoryLevels,
                c.ContractOffers.Values, c.Items.Values, c.ResearchInContentOrder, c.IndustryUpgrades.Values, c.Overdrive, prestige,
                c.RunStart, c.OfflinePolicy, c.ContractItemLadder, c.Milestones, c.ChainDominanceMarginPpm);
        }

        /// <summary>The version 1 development catalog that decodes Save V1 files.</summary>
        public static DefinitionCatalog DevelopmentCatalog()
        {
            var result = GameplayContentLoader.Load(Bytes(DevelopmentCatalogName));
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Code + " " + d.Path)));
            return result.Catalog;
        }

        public static string NewRoot()
        {
            var root = Path.Combine(Application.temporaryCachePath, "t121-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        public static void DeleteRoot(string root)
        {
            try
            {
                if (root != null && Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>Every file under <paramref name="directory"/> with its bytes, keyed by relative path (empty when it does not exist).</summary>
        public static Dictionary<string, byte[]> CaptureFiles(string directory)
        {
            if (!Directory.Exists(directory)) return new Dictionary<string, byte[]>(StringComparer.Ordinal);
            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(
                file => file.Substring(directory.Length).TrimStart('\\', '/').Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);
        }

        public static void AssertSameFiles(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual, string what)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), what + ": file set");
            foreach (var pair in expected) Assert.That(actual[pair.Key], Is.EqualTo(pair.Value), what + ": " + pair.Key);
        }

        /// <summary>Exact comparable text of the live run: identity, Cash, RP, levels, machines, stock and the permanent state.</summary>
        public static string Fingerprint(RunSession session)
        {
            var state = session.State;
            var b = new StringBuilder();
            b.Append(state.RunId).Append('|').Append(state.RunNumber).Append("|prev=").Append(state.PreviousRunId)
                .Append("|cash=").Append(state.Cash).Append("|rp=").Append(state.Rp).Append("|storage=").Append(state.StorageLevel)
                .Append("|factory=").Append(state.FactoryLevel).Append("|ip=").Append(session.Permanent.Ip)
                .Append("|prestiges=").Append(session.Permanent.PrestigeCount);
            foreach (var m in state.Machines)
                b.Append("|m=").Append(m.MachineId).Append('@').Append(m.SlotIndex).Append('L').Append(m.Level).Append('p').Append(m.ProgressTicks);
            for (var i = 0; i < state.Inventory.Count; i++)
                b.Append("|s=").Append(state.Inventory.IdAt(i)).Append(':').Append(state.Inventory.UnitsAt(i));
            foreach (var level in session.Permanent.IndustryLevels()) b.Append("|ind=").Append(level.Id).Append(':').Append(level.Count);
            foreach (var stack in session.Permanent.ItemStacks()) b.Append("|item=").Append(stack.Id).Append(':').Append(stack.Count);
            return b.ToString();
        }
    }

    /// <summary>A clock the test sets by hand; the run authority never reads the device clock in these tests.</summary>
    internal sealed class ManualUtcClock : IUtcClock
    {
        public long UnixMs { get; set; }

        public DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(UnixMs);
    }

    /// <summary>
    /// A store that delegates every call to the real <see cref="JsonRunCheckpointStore"/> and, when armed, reports a failure
    /// for the next <c>Promote</c> AFTER the real promotion became durable: the interruption between the durable write and
    /// the publication of a prestige (R09).
    /// </summary>
    internal sealed class FailAfterPromoteStore : IRunCheckpointStore
    {
        private readonly IRunCheckpointStore inner;

        public FailAfterPromoteStore(IRunCheckpointStore inner) => this.inner = inner;

        /// <summary>When true, the next <c>Promote</c> writes for real and then reports <c>PersistenceFailed</c>; the flag clears itself.</summary>
        public bool FailNextPromoteAfterWriting { get; set; }

        public RunCheckpointReadResult ReadActive() => inner.ReadActive();

        public RunCheckpointReadResult ReadPrevious() => inner.ReadPrevious();

        public RunCheckpointReadResult Read(CheckpointId checkpointId) => inner.Read(checkpointId);

        public CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint)
        {
            var result = inner.Promote(checkpoint);
            if (!FailNextPromoteAfterWriting) return result;
            FailNextPromoteAfterWriting = false;
            return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "injected interruption after the durable write");
        }

        public CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint recoveryCandidate) => inner.RecoverPrevious(recoveryCandidate);

        public CommandResult<long> NextSupersedeGeneration() => inner.NextSupersedeGeneration();

        public CommandResult<CheckpointId> SupersedeDamaged(RunCheckpoint checkpoint) => inner.SupersedeDamaged(checkpoint);

        public CommandResult<RunCheckpointPresence> ProbeFiles() => inner.ProbeFiles();
    }

    /// <summary>
    /// The production composition of a run, assembled as <c>CoreGameFoundationBootstrap.InitializeRunRuntime</c> does, but
    /// over an isolated root and a manual clock: <see cref="RunSession"/>, <see cref="RunCommands"/>,
    /// <see cref="RunPersistence"/> and the real <see cref="JsonRunCheckpointStore"/> file protocol.
    /// </summary>
    internal sealed class RunRig
    {
        private RunRig()
        {
        }

        public RunCatalog Catalog { get; private set; }
        public RunSession Session { get; private set; }
        public RunCommands Commands { get; private set; }
        public RunPersistence Persistence { get; private set; }
        public FailAfterPromoteStore Store { get; private set; }
        public ManualUtcClock Clock { get; private set; }
        public string Root { get; private set; }

        public static RunRig Create(string root, RunCatalog catalog, long clockUtcMs)
        {
            var clock = new ManualUtcClock { UnixMs = clockUtcMs };
            var store = new FailAfterPromoteStore(new JsonRunCheckpointStore(root, catalog));
            var permanent = new PermanentState(catalog);
            var start = NextRunStart.Compute(catalog, permanent);
            var state = new RunState(catalog, Guid.NewGuid(), 1, null, start.StartingCash, 0, start.StorageLevel, 1, catalog.RunStart.InitialUnlocks);
            var settled = new RunSimulator(catalog).Advance(state, permanent, 0, clockUtcMs, null);
            Assert.That(settled, Is.EqualTo(RunAdvanceResult.Ok), "The fresh run did not settle.");
            var session = new RunSession(catalog, state, permanent, clockUtcMs);
            var commands = new RunCommands(session);
            var persistence = new RunPersistence(session, store, clock);
            return new RunRig
            {
                Catalog = catalog,
                Session = session,
                Commands = commands,
                Persistence = persistence,
                Store = store,
                Clock = clock,
                Root = root,
            };
        }

        /// <summary>Moves the manual clock to the run timeline (the run only advances by <see cref="RunSession.Advance"/>).</summary>
        public void SyncClock() => Clock.UnixMs = Session.TimelineUtcMs;

        public RunPersistenceOutcome ColdStartOk(ILegacyCheckpointSource legacy)
        {
            var result = Persistence.ColdStart(legacy);
            Assert.That(result.IsSuccess, Is.True, "cold start: " + result.ReasonCode + " " + Persistence.BlockedImportCode);
            Assert.That(Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            return result.Value;
        }
    }

    /// <summary>One W003 variant as a packaged Save V2 fixture (T104 originals, T122 Play Mode copies).</summary>
    public sealed class W003PlayerFixture
    {
        public W003PlayerFixture(string name, string scenario, string sha256, bool reference)
        {
            Name = name;
            Scenario = scenario;
            Sha256 = sha256;
            Reference = reference;
        }

        public string Name { get; }
        public string Scenario { get; }

        /// <summary>The SHA-256 the fixture README lists (QA-7); the Edit Mode parity test ties it to the original.</summary>
        public string Sha256 { get; }

        public bool Reference { get; }

        public override string ToString() => Scenario;
    }

    /// <summary>
    /// The W003 fixtures and the catalog they load against. The catalog is the test-local one of <c>W003Workload</c> (Edit
    /// Mode, T098), which a player assembly cannot reference; the construction below is the same, and the tests prove it is
    /// equivalent through the fixture round trip and the W003-B event bound of 12 x 230,400 completions.
    /// </summary>
    internal static class W003PlayerFixtures
    {
        public const string ResourceDirectory = "W003PlayerFixtures";
        public const long TenHoursMs = 10L * 3600 * 1000;
        public const long EightHoursMs = 8L * 3600 * 1000;

        public static readonly W003PlayerFixture Bound = new W003PlayerFixture("05-w003-b-bound", "W003-B",
            "7e278091c3b6fd3a9047bf90555fc2749663e406ac97218a26f611a35e6e8be0", false);

        public static readonly W003PlayerFixture Stress = new W003PlayerFixture("06-w003-s-stress", "W003-S",
            "0467d293d69ed358ae1ec74d5b94192aaaf5f983f75cfad23bca6dd27a95ab73", false);

        public static readonly W003PlayerFixture Reference = new W003PlayerFixture("07-w003-r-reference", "W003-R",
            "7cb3d7a7461ac31a8d8273fff19ad4d30e9547414a7332542c9345dd02e9d22d", true);

        public static byte[] Bytes(W003PlayerFixture fixture)
        {
            var asset = Resources.Load<TextAsset>(ResourceDirectory + "/" + fixture.Name);
            Assert.That(asset, Is.Not.Null, "The packaged player fixture '" + fixture.Name + "' is missing from the build.");
            return asset.bytes;
        }

        public static string Sha256Of(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static RunCatalog Catalog(W003PlayerFixture fixture)
        {
            return fixture.Reference
                ? BuildCatalog(null, 25, 1_080_000, new[] { new SpeedMilestone(10, 2_000_000), new SpeedMilestone(25, 2_000_000) },
                    1_200_000, 100_000_000L, 4_000)
                : BuildCatalog(new long[] { 40_000_000, 30_000_000, 40_000_000, 37_500_000 }, 2, 1_080_000, null,
                    2_000_000, 1_000_000_000L, 16_000);
        }

        /// <summary>Loads the packaged bytes, checks the recorded hash and returns the document built against <paramref name="catalog"/>.</summary>
        public static SaveV2Document Load(W003PlayerFixture fixture, RunCatalog catalog)
        {
            byte[] bytes = Bytes(fixture);
            Assert.That(Sha256Of(bytes), Is.EqualTo(fixture.Sha256), fixture.Scenario + " packaged bytes");
            var read = SaveV2Mapper.Deserialize(bytes, catalog);
            Assert.That(read.IsSuccess, Is.True, fixture.Scenario + " " + read.Code);
            return read.Document;
        }

        private static RunCatalog BuildCatalog(long[] level2SpeedPpm, int maxLevel, long growthPpm, IReadOnlyList<SpeedMilestone> milestones,
            long itemFactorPpm, long maxCombinedSpeedPpm, long storageCapacity)
        {
            var always = new UnlockCondition("always");
            var resources = new[]
            {
                new ResourceDef("iron.ore", "Iron Ore", "F003-RES-IRON-ORE", 1, always),
                new ResourceDef("iron.bar", "Iron Bar", "F003-RES-IRON-BAR", 3, always),
                new ResourceDef("iron.plate", "Iron Plate", "F003-RES-IRON-PLATE", 8, always),
                new ResourceDef("iron.gear", "Gear", "F003-RES-IRON-GEAR", 24, always),
            };
            var recipes = new[]
            {
                new MachineRecipe("recipe.mine-iron-ore", new ResourceAmount[0], new[] { new ResourceAmount("iron.ore", 2) }, 20_000_000),
                new MachineRecipe("recipe.smelt-iron-bar", new[] { new ResourceAmount("iron.ore", 2) }, new[] { new ResourceAmount("iron.bar", 1) }, 20_000_000),
                new MachineRecipe("recipe.press-iron-plate", new[] { new ResourceAmount("iron.bar", 2) }, new[] { new ResourceAmount("iron.plate", 1) }, 40_000_000),
                new MachineRecipe("recipe.assemble-iron-gear", new[] { new ResourceAmount("iron.plate", 2) }, new[] { new ResourceAmount("iron.gear", 1) }, 50_000_000),
            };
            var cost = new Curve(10, 1_150_000, "ceil");
            string[] ids = { "machine.miner", "machine.smelter", "machine.press", "machine.assembler" };
            string[] labels = { "Miner", "Smelter", "Press", "Assembler" };
            var machines = new MachineDef[4];
            for (int i = 0; i < 4; i++)
            {
                long growth = level2SpeedPpm != null ? level2SpeedPpm[i] : growthPpm;
                machines[i] = new MachineDef(ids[i], labels[i], "F003-MACHINE-" + labels[i].ToUpperInvariant(), recipes[i].Id,
                    maxLevel, cost, growth, milestones ?? new SpeedMilestone[0], always);
            }

            var items = new[]
            {
                new ItemDef("item.boost", 1, "Boost", "F003-ITEM-BOOST", TenHoursMs, "factory", new string[0],
                    new EffectDef("machineSpeed", "allMachines", null, itemFactorPpm)),
            };
            return new RunCatalog(1, resources, recipes, machines,
                new[] { new StorageLevelDef(1, 1000, 0), new StorageLevelDef(2, storageCapacity, 2500) },
                new[] { new FactoryLevelDef(1, 12, 0) },
                new ContractOfferDef[0], items, new ResearchDef[0], new IndustryUpgradeDef[0],
                new OverdriveDef(TenHoursMs, 300_000, 2_000_000, always),
                new PrestigeDef(10_000_000, "sqrt", 10, 10_000_000),
                new RunStartDef(25, new[] { "iron.ore", "machine.miner" }, 500_000, 1_000_000,
                    maxCombinedSpeedPpm, 4, 2_000_000),
                new RunOfflinePolicy(1, 4, 5, 288_000_000_000L));
        }
    }

    /// <summary>
    /// The per-frame work of <c>RunShellPresenter.Refresh</c> without the UI Toolkit element writes (a player test has no scene,
    /// and the shell document loads its UXML through the asset database, which a player cannot): the same hot reads, the same
    /// <c>StructureRevision</c> gate with its snapshot rebuilds, the machine-state change detection, the once-per-second
    /// Overdrive status and the slot model. The label and element writes are not included; the emulator present-interval gate
    /// (T124) covers the real HUD.
    /// </summary>
    internal sealed class HudRefreshProbe
    {
        private readonly RunSession session;
        private MachineStateView[] views = new MachineStateView[16];
        private MachineStateView[] previousViews = new MachineStateView[16];
        private int previousViewCount = -1;
        private long structureRevision = -1;
        private long lastCash = long.MinValue;
        private long lastRp = long.MinValue;
        private long lastUsed = long.MinValue;
        private long lastCapacity = long.MinValue;
        private long overdriveSecond = long.MinValue;
        private bool factoryDirty = true;
        private bool overdriveActive;
        private RunFactorySnapshot snapshot;
        private readonly HashSet<string> boosted = new HashSet<string>(StringComparer.Ordinal);

        public HudRefreshProbe(RunSession session) => this.session = session;

        /// <summary>Refreshes caused by a <see cref="RunSession.StructureRevision"/> change (snapshot rebuild of the structure dependents).</summary>
        public int RebuildsByStructureRevision { get; private set; }

        /// <summary>Factory snapshot rebuilds in total (a structure change or a machine state change).</summary>
        public int FactoryRebuilds { get; private set; }

        public int OverdriveStatusReads { get; private set; }

        /// <summary>Keeps the reads observable so nothing is optimized away.</summary>
        public long Sink { get; private set; }

        public void Refresh()
        {
            long cash = session.ReadCash();
            long rp = session.ReadRp();
            var inventory = session.State.Inventory;
            long used = inventory.Total;
            long capacity = inventory.Capacity;
            bool structureChanged = session.StructureRevision != structureRevision;
            if (structureChanged)
            {
                structureRevision = session.StructureRevision;
                factoryDirty = true;
                RebuildsByStructureRevision++;
                var effects = RunSnapshots.Items(session).Active;
                boosted.Clear();
                for (int i = 0; i < effects.Count; i++)
                {
                    if (!string.IsNullOrEmpty(effects[i].TargetKey)) boosted.Add(effects[i].TargetKey);
                }

                Sink += RunSnapshots.Research(session).Nodes.Count;
            }

            if (cash != lastCash) { lastCash = cash; Sink += cash; }
            if (rp != lastRp || structureChanged) { lastRp = rp; Sink += rp; }
            if (used != lastUsed || capacity != lastCapacity) { lastUsed = used; lastCapacity = capacity; Sink += used; }

            if (MachineStatesChanged()) factoryDirty = true;

            long now = session.TimelineUtcMs;
            long second = now / 1000L;
            var overdrive = session.State.Overdrive;
            if (structureChanged || (second != overdriveSecond && (overdriveActive || overdrive.IsActive(now) || overdrive.IsCoolingDown(now))))
            {
                overdriveSecond = second;
                var status = RunSnapshots.Overdrive(session);
                overdriveActive = status.State == OverdriveStage.Active;
                OverdriveStatusReads++;
            }

            if (factoryDirty)
            {
                factoryDirty = false;
                FactoryRebuilds++;
                snapshot = RunSnapshots.Factory(session);
                var visuals = SlotGridModel.DescribeAll(snapshot, key => key, overdriveActive, boosted, -1);
                Sink += visuals.Length;
            }
        }

        private bool MachineStatesChanged()
        {
            int count = session.CopyMachineStates(views);
            if (count > views.Length)
            {
                views = new MachineStateView[count];
                previousViews = new MachineStateView[count];
                previousViewCount = -1;
                session.CopyMachineStates(views);
            }

            bool changed = count != previousViewCount;
            for (int i = 0; !changed && i < count; i++)
            {
                var a = views[i];
                var b = previousViews[i];
                changed = a.SlotIndex != b.SlotIndex || a.Level != b.Level || a.IsActive != b.IsActive ||
                    a.Phase != b.Phase || a.Blocker != b.Blocker || a.MachineTypeIndex != b.MachineTypeIndex;
            }

            if (changed)
            {
                Array.Copy(views, previousViews, Math.Min(count, views.Length));
                previousViewCount = count;
            }

            return changed;
        }
    }
}
