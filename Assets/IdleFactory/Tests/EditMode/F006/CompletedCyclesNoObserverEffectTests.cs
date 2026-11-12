using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T514 (Feature 006, research R08, FR-029, SC-007): reading <c>CompletedCycles</c> has no observer effect. The save bytes and the trace of a
    /// scripted run hash identically with and without a reader of <see cref="RunSession.CompletedCyclesOf"/> and
    /// <see cref="RunSession.CopyMachineStates(MachineStateView[])"/>; the save document carries no completion count; and the machine read
    /// model and its refresh allocate nothing on a warmed session.
    /// </summary>
    public sealed class CompletedCyclesNoObserverEffectTests
    {
        private const long Second = 10_000_000;

        private static string Sha256(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var sb = new StringBuilder();
                foreach (byte b in sha.ComputeHash(new UTF8Encoding(false).GetBytes(text))) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static void Read(RunSession session, MachineStateView[] buffer, ref long sink)
        {
            int n = session.CopyMachineStates(buffer);
            for (int i = 0; i < n; i++) sink += buffer[i].CompletedCycles;
            IReadOnlyList<MachineInstance> machines = session.State.Machines;
            for (int i = 0; i < machines.Count; i++) sink += session.CompletedCyclesOf(machines[i].InstanceId); // no foreach: it would box an enumerator
        }

        /// <summary>One scripted run over the W003 workload: the trace is the state dump after every advance, the save is the written Save V3 document.</summary>
        private static void RunW003(bool reader, out string traceSha, out byte[] save)
        {
            W003Scenario sc = W003Workload.Build(W003Variant.Stress);
            var session = new RunSession(sc.Catalog, sc.State, sc.Permanent, sc.StartUtcMs);
            var buffer = new MachineStateView[64];
            long sink = 0;
            var trace = new StringBuilder();
            foreach (long step in new long[] { 10_000_000, 33_000_000, 1_000_000, 57_000_000, 120_000_000, 300_000_000 })
            {
                Assert.That(session.Advance(step).IsSuccess, Is.True);
                if (reader) Read(session, buffer, ref sink);
                trace.Append(SaveV2Scenarios.Dump(session.State, session.Permanent)).Append('\n');
            }
            traceSha = Sha256(trace.ToString());
            SaveV2WriteResult written = SaveV2Mapper.Serialize(SaveV2Scenarios.Meta(sc.Catalog, 1, 0, session.TimelineUtcMs), session.State, session.Permanent);
            Assert.That(written.IsSuccess, Is.True, written.Code);
            save = written.Bytes;
            if (reader) Assert.That(sink, Is.GreaterThan(0), "the reader really read a count");
        }

        /// <summary>The same with commands (build, upgrade, remove, prestige-free) on a rig, so settles at command instants are covered too.</summary>
        private static void RunCommands(bool reader, out string traceSha, out byte[] save)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(SaveV2Scenarios.Options(), 50_000_000);
            var buffer = new MachineStateView[64];
            long sink = 0;
            var trace = new StringBuilder();
            void Step(string what)
            {
                if (reader) Read(rig.Session, buffer, ref sink);
                trace.Append(what).Append('|').Append(SaveV2Scenarios.Dump(rig.State, rig.Session.Permanent)).Append('\n');
            }

            Guid a = rig.Commands.Build(0, RunCommandFixtures.Miner).Value.InstanceId; Step("build");
            Guid b = rig.Commands.Build(1, RunCommandFixtures.Smelter).Value.InstanceId; Step("build");
            rig.Advance(7 * Second); Step("advance");
            Assert.That(rig.Commands.Upgrade(a).IsSuccess, Is.True); Step("upgrade");
            rig.Advance(13 * Second); Step("advance");
            Assert.That(rig.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True); Step("autosell");
            rig.Advance(21 * Second); Step("advance");
            Assert.That(rig.Commands.Remove(b, true).IsSuccess, Is.True); Step("remove");
            rig.Advance(9 * Second); Step("advance");
            traceSha = Sha256(trace.ToString());
            SaveV2WriteResult written = SaveV2Mapper.Serialize(SaveV2Scenarios.Meta(rig.Catalog, 1, 0, rig.Session.TimelineUtcMs), rig.State, rig.Session.Permanent);
            Assert.That(written.IsSuccess, Is.True, written.Code);
            save = written.Bytes;
            if (reader) Assert.That(sink, Is.GreaterThan(0));
        }

        [Test]
        public void SaveAndTraceHashesAreIdenticalWithAndWithoutAReaderOnW003()
        {
            RunW003(false, out string traceWithout, out byte[] saveWithout);
            RunW003(true, out string traceWith, out byte[] saveWith);
            Assert.That(traceWith, Is.EqualTo(traceWithout), "trace SHA-256");
            Assert.That(SaveV2Scenarios.Sha256(saveWith), Is.EqualTo(SaveV2Scenarios.Sha256(saveWithout)), "save SHA-256");
            Assert.That(saveWith, Is.EqualTo(saveWithout), "save bytes");
        }

        [Test]
        public void SaveAndTraceHashesAreIdenticalWithAndWithoutAReaderAcrossCommands()
        {
            RunCommands(false, out string traceWithout, out byte[] saveWithout);
            RunCommands(true, out string traceWith, out byte[] saveWith);
            Assert.That(traceWith, Is.EqualTo(traceWithout), "trace SHA-256");
            Assert.That(SaveV2Scenarios.Sha256(saveWith), Is.EqualTo(SaveV2Scenarios.Sha256(saveWithout)), "save SHA-256");
        }

        [Test]
        public void TheSaveDocumentCarriesNoCompletionCount()
        {
            RunCommands(true, out _, out byte[] save);
            string text = new UTF8Encoding(false, true).GetString(save);
            Assert.That(text.IndexOf("completedcycles", StringComparison.OrdinalIgnoreCase), Is.LessThan(0));
            Assert.That(text.IndexOf("completedCount", StringComparison.OrdinalIgnoreCase), Is.LessThan(0));
        }

        [Test]
        public void TheMachineReadModelAndItsRefreshAllocateNothingOnAWarmedSession()
        {
            W003Scenario sc = W003Workload.Build(W003Variant.Reference);
            var session = new RunSession(sc.Catalog, sc.State, sc.Permanent, sc.StartUtcMs);
            var buffer = new MachineStateView[64];
            long sink = 0;
            for (int warm = 0; warm < 50; warm++)
            {
                Assert.That(session.Advance(Second).IsSuccess, Is.True);
                Read(session, buffer, ref sink);
            }

            for (int sample = 0; sample < 100; sample++)
            {
                Assert.That(session.Advance(Second).IsSuccess, Is.True);
                int copied = 0;
                int allocations = GcAllocationCounter.Count(() => copied = session.CopyMachineStates(buffer));
                Assert.That(copied, Is.EqualTo(session.State.Machines.Count));
                Assert.That(allocations, Is.Zero, "CopyMachineStates (with RefreshViews) allocated in sample " + sample);
            }

            long cycles = 0;
            int allocationsOfAdvanceAndRead = GcAllocationCounter.Count(() =>
            {
                session.Advance(Second);
                Read(session, buffer, ref cycles);
            });
            Assert.That(allocationsOfAdvanceAndRead, Is.Zero, "a warmed Advance, the refresh and the count reads allocate nothing");
            Assert.That(cycles, Is.GreaterThan(0));
        }
    }
}
