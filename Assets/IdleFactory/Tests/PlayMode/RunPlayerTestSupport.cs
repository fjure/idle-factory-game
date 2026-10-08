using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
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
            var prestige = new PrestigeDef(ScaledThresholdFv, c.Prestige.CurveKind, c.Prestige.Coefficient, c.Prestige.Divisor / 100);
            return new RunCatalog(c.ContentRevision, c.Resources.Values, c.Recipes.Values, c.Machines.Values, c.StorageLevels, c.FactoryLevels,
                c.ContractOffers.Values, c.Items.Values, c.ResearchInContentOrder, c.IndustryUpgrades.Values, c.Overdrive, prestige,
                c.RunStart, c.OfflinePolicy);
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
}
