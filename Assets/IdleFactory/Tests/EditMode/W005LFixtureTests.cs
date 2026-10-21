using System;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.SaveV3;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T336 (issue #401, QA-2, PF-11, PF-16): the committed <c>W005-L</c> Save V3 fixture. It is regenerated in memory from
    /// <see cref="W005LWorkload"/> (the real run commands against the shipped catalog and the real <see cref="SaveV2Mapper"/>) and compared byte for
    /// byte with the committed original, the StandalonePlayer <c>Resources</c> copy and the README. A difference fails the test and is never skipped;
    /// set <c>IDLEFACTORY_REGENERATE_SAVEV3_FIXTURES=1</c> to rewrite the original, the copy and the README (the comparison still runs afterwards,
    /// against the files just written). The committed V3 bytes also round-trip through the reader (the #400 follow-up): the QA-11 fixtures of T335
    /// are built in memory, this one is a file.
    /// </summary>
    public sealed class W005LFixtureTests
    {
        private const string RegenerateEnvironmentVariable = "IDLEFACTORY_REGENERATE_SAVEV3_FIXTURES";
        private const string OriginalDirectory = "IdleFactory/Tests/EditMode/Fixtures/SaveV3";
        private const string PlayerDirectory = "IdleFactory/Tests/PlayMode/Fixtures/SaveV3/Resources/W005PlayerFixtures";

        private static bool Regenerate => Environment.GetEnvironmentVariable(RegenerateEnvironmentVariable) == "1";
        private static string OriginalPath => Path.Combine(Application.dataPath, OriginalDirectory, W005LWorkload.FileName);
        private static string PlayerPath => Path.Combine(Application.dataPath, PlayerDirectory, W005LWorkload.FileName);
        private static string ReadmePath => Path.Combine(Application.dataPath, OriginalDirectory, "README.md");

        private bool _logToUnity;

        [SetUp]
        public void SetUp()
        {
            _logToUnity = CheckpointDiagnostics.LogToUnity;
            CheckpointDiagnostics.LogToUnity = false;
        }

        [TearDown]
        public void TearDown()
        {
            CheckpointDiagnostics.LogToUnity = _logToUnity;
        }

        [Test]
        public void CommittedFixture_IsByteIdenticalToItsInMemoryRegeneration()
        {
            W005LScenario scenario = W005LWorkload.Build(W005LWorkload.LoadShippedCatalog());
            string readme = W005LWorkload.Readme(scenario);
            if (Regenerate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OriginalPath));
                Directory.CreateDirectory(Path.GetDirectoryName(PlayerPath));
                File.WriteAllBytes(OriginalPath, scenario.Bytes);
                File.WriteAllBytes(PlayerPath, scenario.Bytes);
                File.WriteAllText(ReadmePath, readme, new UTF8Encoding(false));
                AssetDatabase.Refresh();
            }

            string hint = " Set " + RegenerateEnvironmentVariable + "=1 and rerun to update the committed fixture (the player copy and the README follow).";
            Assert.That(File.Exists(OriginalPath), Is.True, "The committed W005-L fixture is missing." + hint);
            Assert.That(File.ReadAllBytes(OriginalPath), Is.EqualTo(scenario.Bytes), "The W005-L fixture drifted." + hint);
            Assert.That(File.Exists(PlayerPath), Is.True, "The W005-L player copy is missing." + hint);
            Assert.That(File.ReadAllBytes(PlayerPath), Is.EqualTo(scenario.Bytes), "The W005-L player copy drifted." + hint);
            Assert.That(File.Exists(ReadmePath), Is.True, "The Save V3 fixture README is missing." + hint);
            Assert.That(File.ReadAllText(ReadmePath, new UTF8Encoding(false)).Replace("\r\n", "\n"), Is.EqualTo(readme),
                "The Save V3 fixture README is stale." + hint);
        }

        [Test]
        public void Regeneration_IsDeterministic()
        {
            var catalog = W005LWorkload.LoadShippedCatalog();
            Assert.That(W005LWorkload.Build(catalog).Bytes, Is.EqualTo(W005LWorkload.Build(catalog).Bytes));
        }

        [Test]
        public void Readme_ListsTheHashOfTheCommittedFixture()
        {
            string readme = File.ReadAllText(ReadmePath, new UTF8Encoding(false));
            string row = readme.Split('\n').SingleOrDefault(line => line.StartsWith("| `" + W005LWorkload.FileName + "`", StringComparison.Ordinal));
            Assert.That(row, Is.Not.Null);
            Assert.That(row, Does.Contain(W005LWorkload.Sha256(File.ReadAllBytes(OriginalPath))));
        }

        [Test]
        public void CommittedBytes_RoundTripThroughTheReader()
        {
            byte[] disk = File.ReadAllBytes(OriginalPath);
            var catalog = W005LWorkload.LoadShippedCatalog();
            SaveV2ReadResult read = SaveV2Mapper.Deserialize(disk, catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            Assert.That(read.Document.SchemaVersion, Is.EqualTo(SaveV2Mapper.SchemaVersion), "a Save V3 file");
            Assert.That(read.Document.Run.Validate(), Is.EqualTo(RunStateResult.Ok));

            // The loaded document writes the same bytes again: the file is canonical, and nothing the reader drops (no content drift here) or adds.
            SaveV2WriteResult again = SaveV2Mapper.Serialize(read.Document.Meta, read.Document.Run, read.Document.Permanent);
            Assert.That(again.IsSuccess, Is.True, again.Code);
            Assert.That(again.Bytes, Is.EqualTo(disk), "read then write is a fixed point");

            // The same bytes through the checkpoint codec of the real store (the path a cold start takes).
            CheckpointDecodeResult<RunCheckpoint> decoded = new SaveV2CheckpointCodec(catalog).Deserialize(disk);
            Assert.That(decoded.Succeeded, Is.True, decoded.Message);
        }

        [Test]
        public void CommittedBytes_LoadWithTheW005LShape()
        {
            var catalog = W005LWorkload.LoadShippedCatalog();
            SaveV2ReadResult read = SaveV2Mapper.Deserialize(File.ReadAllBytes(OriginalPath), catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            RunState run = read.Document.Run;
            Assert.That(run.RunNumber, Is.EqualTo(W005LWorkload.RunNumber));
            Assert.That(read.Document.Permanent.PrestigeCount, Is.EqualTo(W005LWorkload.PrestigeCount));
            Assert.That(run.FactoryLevel, Is.EqualTo(5));
            Assert.That(run.SlotCount, Is.EqualTo(12), "twelve slots");
            Assert.That(run.Machines.Count, Is.EqualTo(12), "all slots used");
            Assert.That(run.Machines.GroupBy(m => m.MachineId).Select(g => g.Count()).OrderBy(c => c), Is.EqualTo(new[] { 2, 3, 3, 4 }), "the chain mix");
            Assert.That(run.AutoSell, Is.EqualTo(new[] { "iron.gear" }), "auto-sell on the end product");
            Assert.That(run.Contracts.Active, Is.Not.Null);
            Assert.That(run.Contracts.Active.OfferId, Is.EqualTo("contract.gear"));
            Assert.That(run.Contracts.Active.AutoDeliver, Is.True, "auto-deliver is active");
            Assert.That(run.Contracts.RunCompletionTotal, Is.EqualTo(140));
            Assert.That(read.Document.Meta.DepartureUtcMs, Is.EqualTo(W005LWorkload.StartUtcMs + W005LWorkload.SettleSeconds * 1000 + 2_100), "departure");
        }

        [Test]
        public void Reconcile_EightHours_AppliesWithinThePassBound_AndDeliversTheContract()
        {
            var catalog = W005LWorkload.LoadShippedCatalog();
            SaveV2Document document = SaveV2Mapper.Deserialize(File.ReadAllBytes(OriginalPath), catalog).Document;
            long returnUtcMs = document.Meta.DepartureUtcMs + 9L * 3_600_000L;
            var segmenter = new OfflineSegmenter(catalog);
            RunOfflineSummary summary = segmenter.Reconcile(document.Run, document.Permanent, document.Meta.DepartureUtcMs, returnUtcMs);

            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(summary.EffectiveTicks, Is.EqualTo(230_400_000_000L), "the eight hour credited cap");
            Assert.That(document.Run.Validate(), Is.EqualTo(RunStateResult.Ok));
            long completions = segmenter.Simulator.LastCompletions;
            Assert.That(completions, Is.GreaterThan(0));
            Assert.That(completions, Is.LessThanOrEqualTo(12L * 230_400L), "never above the event bound");
            Assert.That(completions, Is.EqualTo(summary.Completions));
            Assert.That(segmenter.Simulator.LastMaxPassesPerTimestamp, Is.LessThanOrEqualTo(document.Run.Machines.Count + 2), "N + 2 passes per timestamp (PF-1)");
            Assert.That(document.Run.Contracts.CompletionCount("contract.gear"), Is.GreaterThan(51), "auto-deliver completed at least one Gear contract");
            TestContext.Out.WriteLine("W005-L-EDITMODE completions=" + completions + " timestamps=" + segmenter.Simulator.LastTimestamps + " passes=" +
                                      segmenter.Simulator.LastPasses + " maxPassesPerTimestamp=" + segmenter.Simulator.LastMaxPassesPerTimestamp);
        }
    }
}
