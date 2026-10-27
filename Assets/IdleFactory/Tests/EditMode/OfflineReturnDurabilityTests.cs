using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IdleFactory.Core.Application;
using IdleFactory.Core.Numerics;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The durable offline return (T105, contracts/run-application.md, FR-025, FR-024, R09, QA-5): the return credits the
    /// offline segmenter's result exactly once, every protocol step failing individually is followed by a retry and by a
    /// restart that end in the same state, the credit is clamped to the persisted departure, a value.limit return forfeits
    /// its credit and advances the departure, and loaded timers that are already past are expired by the return and
    /// never dropped at parse (load rule 3).
    /// </summary>
    public sealed class OfflineReturnDurabilityTests
    {
        private bool logToUnity;

        [SetUp]
        public void SetUp()
        {
            logToUnity = CheckpointDiagnostics.LogToUnity;
            CheckpointDiagnostics.LogToUnity = false;
        }

        [TearDown]
        public void TearDown()
        {
            CheckpointDiagnostics.LogToUnity = logToUnity;
        }

        private static RunPersistenceOutcome Ok(RunCommandResult<RunPersistenceOutcome> result, string context = "") =>
            DurabilityRig.Ok(result, context);

        /// <summary>Everything an offline credit would change: Cash, RP, every stock unit and the permanent balances.</summary>
        private static string Holdings(RunState run, PermanentState permanent)
        {
            var b = new StringBuilder();
            b.Append("cash=").Append(run.Cash).Append(" rp=").Append(run.Rp).Append(" ip=").Append(permanent.Ip);
            for (int i = 0; i < run.Inventory.Count; i++) b.Append(' ').Append(run.Inventory.IdAt(i)).Append('=').Append(run.Inventory.UnitsAt(i));
            b.Append(" items=").Append(string.Join(",", permanent.ItemStacks().Select(x => x.Id + ":" + x.Count)));
            return b.ToString();
        }

        // ------------------------------------------------------------------------------------------------------
        // The credit, exactly once
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void Return_CreditsTheSegmenterResultOnce_AdvancesTheDeparture_AndLinksTheSource()
        {
            DurabilityRig h = DurabilityRig.Suspended();
            RunCheckpoint departure = h.Active();
            long r = h.Clock.UnixMs;
            long ip = h.Session.Permanent.Ip;
            long items = h.Session.Permanent.ItemStacks().Sum(x => x.Count);

            RunPersistenceOutcome returned = Ok(h.Persistence.ResumeAndReconcile());

            string reference = DurabilityRig.ReferenceDump(h, departure.Id, r, out RunOfflineSummary expected);
            RunOfflineSummary summary = returned.OfflineSummary;
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(summary.DepartureUtcMs, Is.EqualTo(departure.DepartureUtcMs), "d is the persisted departure");
            Assert.That(summary.ReturnUtcMs, Is.EqualTo(r));
            Assert.That(summary.MeasuredMs, Is.EqualTo(2 * 3_600_000L));
            Assert.That(summary.Completions, Is.GreaterThan(0));
            Assert.That(summary.Completions, Is.EqualTo(expected.Completions));
            Assert.That(h.Dump(), Is.EqualTo(reference), "the live run is exactly the segmenter result");

            RunCheckpoint active = h.Active();
            Assert.That(active.ReconciledFromCheckpointId, Is.EqualTo(departure.Id), "the exactly-once key");
            Assert.That(active.DepartureUtcMs, Is.EqualTo(r), "the departure advances to the return instant");
            Assert.That(active.Generation, Is.EqualTo(departure.Generation + 1));
            Assert.That(SaveV2Scenarios.Dump(active.Run, active.Permanent), Is.EqualTo(reference));
            Assert.That(h.Session.TimelineUtcMs, Is.EqualTo(r));
            Assert.That(h.Session.Permanent.Ip, Is.EqualTo(ip), "a return awards no IP");
            Assert.That(h.Session.Permanent.ItemStacks().Sum(x => x.Count), Is.EqualTo(items), "a return grants no Item");

            // The rates read model sees the completions of the return (T103 handoff 5).
            Guid miner = h.Session.State.Machines[0].InstanceId;
            Assert.That(h.Session.TryGetLastCompletion(miner, out long units, out _, out _), Is.True);
            Assert.That(units, Is.GreaterThan(0));

            DurabilityRig.Rejected(h.Persistence.ResumeAndReconcile(), RunPersistenceReasons.LifecycleNoOp);
        }

        [Test]
        public void Restart_AfterAReturn_CreditsOnlyTheNewAbsence_NeverTheOldOneAgain()
        {
            DurabilityRig h = DurabilityRig.Suspended();
            long r = h.Clock.UnixMs;
            Ok(h.Persistence.ResumeAndReconcile());
            string afterReturn = h.Dump();
            RunCheckpoint active = h.Active();

            // A process restart at the same instant: the active generation is the return candidate, the absence is zero.
            DurabilityRig same = DurabilityRig.Restart(h, r, 500);
            RunPersistenceOutcome zero = Ok(same.Persistence.ResumeAndReconcile());
            Assert.That(zero.OfflineSummary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(zero.OfflineSummary.CreditedTicks, Is.EqualTo(0));
            Assert.That(zero.OfflineSummary.Completions, Is.EqualTo(0));
            Assert.That(same.Dump(), Is.EqualTo(afterReturn), "nothing is credited twice");

            // One hour later the credit is one hour from r, not 3 hours from the original departure.
            string reference = DurabilityRig.ReferenceDump(h, active.Id, r + 3_600_000L, out _);
            DurabilityRig later = DurabilityRig.Restart(h, r + 3_600_000L, 600);
            RunPersistenceOutcome hour = Ok(later.Persistence.ResumeAndReconcile());
            Assert.That(hour.OfflineSummary.DepartureUtcMs, Is.EqualTo(r));
            Assert.That(hour.OfflineSummary.MeasuredMs, Is.EqualTo(3_600_000L));
            Assert.That(later.Dump(), Is.EqualTo(reference));
            Assert.That(later.Dump(), Is.Not.EqualTo(afterReturn));
        }

        [Test]
        public void ReturnFailingAtEveryBackendCall_ThenRetry_CreditsExactlyOnce()
        {
            // A clean run gives the expected state and the number of backend calls of the return.
            DurabilityRig clean = DurabilityRig.Suspended();
            clean.Backend.ResetCounters();
            Ok(clean.Persistence.ResumeAndReconcile());
            int calls = clean.Backend.Calls;
            string expected = clean.Dump();
            long ip = clean.Session.Permanent.Ip;
            Assert.That(calls, Is.GreaterThan(10));
            var failures = new List<string>();

            for (int i = 0; i < calls; i++)
            {
                DurabilityRig h = DurabilityRig.Suspended();
                RunCheckpoint departure = h.Active();
                h.Backend.ResetCounters();
                h.Backend.FailAtCall(i);
                var first = h.Persistence.ResumeAndReconcile();
                h.Backend.ClearFault();
                if (!first.IsSuccess)
                {
                    if (first.ReasonCode != RunPersistenceReasons.ReturnBlocked) failures.Add(i + ": " + first.ReasonCode);
                    if (h.Persistence.Lifecycle != LifecycleState.ReturnBlocked) failures.Add(i + ": not blocked");
                    var retried = h.Persistence.RetryReturn();
                    if (!retried.IsSuccess) failures.Add(i + ": retry " + retried.ReasonCode);
                }

                if (h.Persistence.Lifecycle != LifecycleState.Running) failures.Add(i + ": not running");
                if (h.Dump() != expected) failures.Add(i + ": state differs (credit duplicated or lost)");
                RunCheckpoint active = h.Active();
                if (active.ReconciledFromCheckpointId != departure.Id) failures.Add(i + ": not linked to the departure");
                if (active.Generation != departure.Generation + 1) failures.Add(i + ": generation " + active.Generation);
                if (h.Previous().Id != departure.Id) failures.Add(i + ": previous is not the departure");
                if (h.Session.Permanent.Ip != ip) failures.Add(i + ": IP changed");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void ReturnFailingAtEveryBackendCall_ThenRestart_CreditsExactlyOnce()
        {
            DurabilityRig clean = DurabilityRig.Suspended();
            clean.Backend.ResetCounters();
            Ok(clean.Persistence.ResumeAndReconcile());
            int calls = clean.Backend.Calls;
            long r = clean.Clock.UnixMs;
            string expected = clean.Dump();
            var failures = new List<string>();

            for (int i = 0; i < calls; i++)
            {
                DurabilityRig h = DurabilityRig.Suspended();
                h.Backend.ResetCounters();
                h.Backend.FailAtCall(i);
                h.Persistence.ResumeAndReconcile();
                h.Backend.ClearFault();

                // The process dies here. Whether the return candidate became durable or not, the restart at the same
                // instant lands in the same state: the old departure is credited once, or the candidate is loaded and
                // credits nothing more.
                DurabilityRig restarted = DurabilityRig.Restart(h, r, 900 + i);
                var resumed = restarted.Persistence.ResumeAndReconcile();
                if (!resumed.IsSuccess) failures.Add(i + ": restart " + resumed.ReasonCode);
                else if (restarted.Dump() != expected) failures.Add(i + ": state differs after the restart");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void ReturnRetry_NeverReconcilesTheSameSourceObjectsTwice()
        {
            // A failed promotion leaves the source checkpoint in memory; the retry must start from a private fresh read.
            DurabilityRig h = DurabilityRig.Suspended();
            RunCheckpoint departure = h.Active();
            long r = h.Clock.UnixMs;
            h.Backend.FailOnFirst("Create", CheckpointStorageFile.Pending);
            var first = h.Persistence.ResumeAndReconcile();
            Assert.That(first.IsSuccess, Is.False);
            Assert.That(h.Backend.FaultTriggered, Is.True);
            Assert.That(h.Active().Id, Is.EqualTo(departure.Id), "nothing was promoted");

            Ok(h.Persistence.RetryReturn());
            string reference = DurabilityRig.ReferenceDump(h, departure.Id, r, out _);
            Assert.That(h.Dump(), Is.EqualTo(reference), "one absence is credited once, although the reconcile ran twice");
        }

        // ------------------------------------------------------------------------------------------------------
        // Departure clamps
        // ------------------------------------------------------------------------------------------------------

        [TestCase(10L * 24 * 3_600_000L)]
        [TestCase(ExactBound.Value)]
        public void ADepartureInTheFuture_CreditsNothing_AndIsNeverRewrittenEarlier(long aheadOrAbsolute)
        {
            DurabilityRig seed = DurabilityRig.Create();
            seed.Play();
            seed.Sync();
            long now = seed.Clock.UnixMs;
            long future = aheadOrAbsolute == ExactBound.Value ? ExactBound.Value : now + aheadOrAbsolute;
            string before = Holdings(seed.Session.State, seed.Session.Permanent);

            // A forged or stale checkpoint whose departure lies far in the future.
            var forged = new RunCheckpoint(new CheckpointId(new Guid("44444444-4444-4444-4444-000000000001")), 1, future,
                seed.Rig.Catalog.OfflinePolicy.Revision, null, seed.Rig.Catalog.ContentRevision, null, seed.Session.State, seed.Session.Permanent);
            Assert.That(seed.Store.Promote(forged).Succeeded, Is.True);

            DurabilityRig restarted = DurabilityRig.Restart(seed, now + 3_600_000L, 100);
            RunPersistenceOutcome returned = Ok(restarted.Persistence.ResumeAndReconcile());

            Assert.That(returned.OfflineSummary.Outcome, Is.EqualTo(RunOfflineOutcome.BackwardClock), "max(0, now - departure) is zero");
            Assert.That(returned.OfflineSummary.CreditedTicks, Is.EqualTo(0));
            Assert.That(Holdings(restarted.Session.State, restarted.Session.Permanent), Is.EqualTo(before), "nothing is credited");
            RunCheckpoint active = restarted.Active();
            Assert.That(active.DepartureUtcMs, Is.EqualTo(future), "the departure is never rewritten earlier");
            Assert.That(active.ReconciledFromCheckpointId, Is.EqualTo(forged.Id));
            Assert.That(restarted.Session.AnchorUtcMs, Is.EqualTo(future));

            // Later writes keep the future departure too.
            restarted.Clock.UnixMs = now + 5_000;
            Ok(restarted.Persistence.SaveNow());
            Assert.That(restarted.Active().DepartureUtcMs, Is.GreaterThanOrEqualTo(future));
        }

        [Test]
        public void AForgedOneTickCycle_YieldsNoMoreOutputThanAnHonestSave()
        {
            // The mapper restores cycleDurationTicks exactly; the return's simulation and the settle of the install rescale
            // every machine to its effective cycle first (load rule 2, T104 handoff 1).
            List<SaveV2Fixture> game = SaveV2Scenarios.PlayedGame();
            SaveV2Fixture mid = game[0];
            string text = Encoding.UTF8.GetString(mid.Bytes);
            Match miner = Regex.Match(text, "\"progressTicks\":(\\d+),\"cycleDurationTicks\":(\\d+)");
            Assert.That(miner.Success, Is.True);
            long cycle = long.Parse(miner.Groups[2].Value);

            long Output(string progressAndCycle, out long completions)
            {
                string mutated = text.Substring(0, miner.Index) + progressAndCycle + text.Substring(miner.Index + miner.Length);
                var decoded = new SaveV2CheckpointCodec(mid.Catalog).Deserialize(Encoding.UTF8.GetBytes(mutated));
                Assert.That(decoded.Succeeded, Is.True, decoded.Message);
                RunCheckpoint source = decoded.Checkpoint;
                long r = source.DepartureUtcMs + 8 * 3_600_000L;
                var seg = new OfflineSegmenter(mid.Catalog);
                RunOfflineSummary summary = seg.Reconcile(source.Run, source.Permanent, source.DepartureUtcMs, r);
                Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
                completions = summary.Completions;
                return summary.ProducedUnits.Sum(x => x.Units);
            }

            long honest = Output("\"progressTicks\":" + miner.Groups[1].Value + ",\"cycleDurationTicks\":" + cycle, out long honestCompletions);
            long honestFull = Output("\"progressTicks\":" + cycle + ",\"cycleDurationTicks\":" + cycle, out long fullCompletions);
            long forgedZero = Output("\"progressTicks\":0,\"cycleDurationTicks\":1", out long zeroCompletions);
            long forgedFull = Output("\"progressTicks\":1,\"cycleDurationTicks\":1", out long oneCompletions);

            Assert.That(forgedZero, Is.LessThanOrEqualTo(honest));
            Assert.That(zeroCompletions, Is.LessThanOrEqualTo(honestCompletions));
            Assert.That(forgedFull, Is.LessThanOrEqualTo(honestFull), "a finished cycle is the most an honest save can hold");
            Assert.That(oneCompletions, Is.LessThanOrEqualTo(fullCompletions));
            Assert.That(honestCompletions, Is.GreaterThan(0));
        }

        [Test]
        public void AForgedOneTickCycle_LoadedThroughTheReturn_IsRescaledBeforeItProduces()
        {
            // The same forgery through the real return path and install.
            List<SaveV2Fixture> game = SaveV2Scenarios.PlayedGame();
            SaveV2Fixture mid = game[0];
            string text = Encoding.UTF8.GetString(mid.Bytes);
            Match miner = Regex.Match(text, "\"progressTicks\":(\\d+),\"cycleDurationTicks\":(\\d+)");
            string forgedText = text.Substring(0, miner.Index) + "\"progressTicks\":1,\"cycleDurationTicks\":1" + text.Substring(miner.Index + miner.Length);
            var decoded = new SaveV2CheckpointCodec(mid.Catalog).Deserialize(Encoding.UTF8.GetBytes(forgedText));
            Assert.That(decoded.Succeeded, Is.True, decoded.Message);

            var backend = new ScriptedBackend();
            var store = new JsonRunCheckpointStore("memory-root", mid.Catalog, backend);
            Assert.That(store.Promote(decoded.Checkpoint).Succeeded, Is.True);
            var seed = new RunSession(mid.Catalog, RunCheckpointTestSupport.CheckpointOf(mid).Run, RunCheckpointTestSupport.CheckpointOf(mid).Permanent,
                mid.Meta.DepartureUtcMs);
            long departure = decoded.Checkpoint.DepartureUtcMs;
            var clock = new FixedUtcClock(departure + 8 * 3_600_000L);
            var persistence = new RunPersistence(seed, store, clock, new SequenceCheckpointIds(40), new OfflineRunReturnReconciler(mid.Catalog));
            RunPersistenceOutcome returned = Ok(persistence.ResumeAndReconcile());

            // 8 h at 80 percent of a cycle of a few seconds is a few thousand completions, never 10^11 of a one-tick cycle.
            Assert.That(returned.OfflineSummary.Completions, Is.LessThan(1_000_000));
            MachineInstance live = seed.State.Machines[0];
            Assert.That(live.CycleDurationTicks, Is.GreaterThan(1_000_000), "the live machine runs on its effective cycle");
        }

        // ------------------------------------------------------------------------------------------------------
        // value.limit: forfeit, advance the departure, never retry the credit
        // ------------------------------------------------------------------------------------------------------

        private static DurabilityRig NearTheBound()
        {
            DurabilityRig h = DurabilityRig.Create();
            h.Play();
            Assert.That(h.Rig.State.TryAddCash(ExactBound.Value - h.Rig.State.Cash - 1), Is.EqualTo(RunStateResult.Ok));
            h.Sync();
            Ok(h.Persistence.RecordSuspension());
            h.Clock.UnixMs = h.Active().DepartureUtcMs + 3_600_000L;
            return h;
        }

        [Test]
        public void ValueLimit_ForfeitsTheCredit_KeepsTheState_AdvancesTheDeparture_AndReportsTheStaticCode()
        {
            DurabilityRig h = NearTheBound();
            RunCheckpoint departure = h.Active();
            long r = h.Clock.UnixMs;
            string before = Holdings(departure.Run, departure.Permanent);

            RunPersistenceOutcome returned = Ok(h.Persistence.ResumeAndReconcile());
            string kept = h.Dump();

            RunOfflineSummary summary = returned.OfflineSummary;
            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.SimulationRejected));
            Assert.That(summary.Code, Is.EqualTo("offline.value-limit"));
            Assert.That(RunOfflineSummary.ValueLimitCode, Is.EqualTo("offline.value-limit"));
            Assert.That(summary.CreditForfeited, Is.True);
            Assert.That(summary.IsRetryable, Is.False);
            Assert.That(summary.CreditedTicks, Is.EqualTo(0));
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running), "the session runs");
            Assert.That(Holdings(h.Session.State, h.Session.Permanent), Is.EqualTo(before), "the state is as before the absence: nothing produced, sold or spent");
            RunCheckpoint active = h.Active();
            Assert.That(active.DepartureUtcMs, Is.EqualTo(r), "the departure advances to the return instant");
            Assert.That(active.ReconciledFromCheckpointId, Is.EqualTo(departure.Id));

            // A restart at the same instant, and a retry, never attempt the credit again.
            DurabilityRig restarted = DurabilityRig.Restart(h, r, 300);
            RunPersistenceOutcome again = Ok(restarted.Persistence.ResumeAndReconcile());
            Assert.That(again.OfflineSummary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(again.OfflineSummary.CreditedTicks, Is.EqualTo(0));
            Assert.That(restarted.Dump(), Is.EqualTo(kept), "no attempt to credit again");
        }

        [Test]
        public void ValueLimit_FailingAtEveryBackendCall_ThenRetryOrRestart_ForfeitsExactlyOnce()
        {
            DurabilityRig clean = NearTheBound();
            long r = clean.Clock.UnixMs;
            clean.Backend.ResetCounters();
            Ok(clean.Persistence.ResumeAndReconcile());
            int calls = clean.Backend.Calls;
            string expected = clean.Dump();
            var failures = new List<string>();

            for (int i = 0; i < calls; i++)
            {
                DurabilityRig h = NearTheBound();
                RunCheckpoint departure = h.Active();
                h.Backend.ResetCounters();
                h.Backend.FailAtCall(i);
                var first = h.Persistence.ResumeAndReconcile();
                h.Backend.ClearFault();
                if (!first.IsSuccess && !h.Persistence.RetryReturn().IsSuccess) failures.Add(i + ": retry failed");
                if (h.Dump() != expected) failures.Add(i + ": state differs");
                RunCheckpoint active = h.Active();
                if (active.DepartureUtcMs != r) failures.Add(i + ": departure " + active.DepartureUtcMs);
                if (active.ReconciledFromCheckpointId != departure.Id) failures.Add(i + ": link");

                DurabilityRig fresh = NearTheBound();
                fresh.Backend.ResetCounters();
                fresh.Backend.FailAtCall(i);
                fresh.Persistence.ResumeAndReconcile();
                fresh.Backend.ClearFault();
                DurabilityRig restarted = DurabilityRig.Restart(fresh, r, 800 + i);
                if (!restarted.Persistence.ResumeAndReconcile().IsSuccess) failures.Add(i + ": restart failed");
                else if (restarted.Dump() != expected) failures.Add(i + ": state differs after the restart");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        // ------------------------------------------------------------------------------------------------------
        // Load rule 3: timers that are already past are expired by the return, never dropped at parse
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void AMidRunWhoseEffectOverdriveAndContractArePastAtLoad_HasThemExpiredByTheReturn_NotDroppedAtParse()
        {
            List<SaveV2Fixture> game = SaveV2Scenarios.PlayedGame();
            SaveV2Fixture mid = game[0];
            RunCheckpoint source = RunCheckpointTestSupport.CheckpointOf(mid);
            long departure = source.DepartureUtcMs;
            ActiveEffect effect = source.Run.Effects.Effects[0];
            ActiveContract contract = source.Run.Contracts.Active;

            // Parsing keeps every timer, whatever the clock says.
            Assert.That(source.Run.Effects.Count, Is.EqualTo(1));
            Assert.That(source.Run.Overdrive.ActiveUntilUtcMs.HasValue, Is.True);
            Assert.That(source.Run.Contracts.Active, Is.Not.Null);

            var backend = new ScriptedBackend();
            var store = new JsonRunCheckpointStore("memory-root", mid.Catalog, backend);
            Assert.That(store.Promote(source).Succeeded, Is.True);
            RunCheckpoint read = store.ReadActive().Checkpoint;
            Assert.That(read.Run.Effects.Count, Is.EqualTo(1), "still there after a store round trip");

            long r = Math.Max(Math.Max(effect.EndUtcMs, source.Run.Overdrive.CooldownUntilUtcMs.Value), contract.DeadlineUtcMs.Value) + 60_000;
            Assert.That(r, Is.GreaterThan(departure));
            var seed = new RunSession(mid.Catalog, RunCheckpointTestSupport.CheckpointOf(mid).Run, RunCheckpointTestSupport.CheckpointOf(mid).Permanent, departure);
            var persistence = new RunPersistence(seed, store, new FixedUtcClock(r), new SequenceCheckpointIds(60), new OfflineRunReturnReconciler(mid.Catalog));
            RunPersistenceOutcome returned = Ok(persistence.ResumeAndReconcile());

            RunOfflineSummary summary = returned.OfflineSummary;
            Assert.That(summary.ExpiredEffects.Select(x => x.SourceId), Is.EqualTo(new[] { effect.SourceId }), "the effect ended during the absence");
            Assert.That(summary.OverdriveActiveEnded, Is.True);
            Assert.That(summary.OverdriveCooldownEnded, Is.True);
            Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.Expired));
            Assert.That(seed.State.Effects.Count, Is.EqualTo(0));
            Assert.That(seed.State.Overdrive.ActiveUntilUtcMs.HasValue, Is.False);
            Assert.That(seed.State.Contracts.Active, Is.Null);
            Assert.That(store.ReadActive().Checkpoint.Run.Effects.Count, Is.EqualTo(0), "the durable generation carries the expiry");
        }
    }
}
