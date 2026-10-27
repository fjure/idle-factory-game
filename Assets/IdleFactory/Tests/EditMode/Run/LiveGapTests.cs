using System;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Unity.Bootstrap;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T337 (FR-032, FR-033, F5-R10, SEC-10): the large live gap. A wall delta strictly above <c>runStart.liveGapThresholdMs</c> between two live
    /// frames is a departure at the previous frame, <c>RecordSuspension(atUtcMs)</c>, followed by the standard <c>ResumeAndReconcile</c>.
    /// <list type="bullet">
    /// <item><see cref="LiveGapMonitor"/> is the decision, on plain milliseconds: threshold -1, = and +1, a backwards clock, no reference yet.</item>
    /// <item>The persistence overload is exercised on the real <see cref="RunPersistence"/> over the scripted store (<see cref="DurabilityRig"/>):
    /// the departure instant and its SEC-10 bounds, the equivalence with a quit and restart, a checkpoint inside a gap, and a pending prestige.</item>
    /// </list>
    /// The host and lifecycle-adapter wiring (accelerated frames not counting, the gap before the time scale) is in the Play Mode
    /// <c>LiveGapHostTests</c>. Case (p) of the equivalence suite is in <see cref="AutoDeliverEquivalenceTests"/>.
    /// </summary>
    public sealed class LiveGapTests
    {
        private const long Threshold = 60_000;
        private const long TwoHours = 2 * 3_600_000L;

        // ---- the decision: LiveGapMonitor ------------------------------------------------------------------------------------------

        [TestCase(-1L, LiveGapVerdict.Live)]
        [TestCase(0L, LiveGapVerdict.Live)]
        [TestCase(1L, LiveGapVerdict.Gap)]
        public void TheThresholdIsStrict_BelowAndEqualAreLive_OneMillisecondAboveIsAGap(long offsetFromThreshold, LiveGapVerdict expected)
        {
            var monitor = new LiveGapMonitor(Threshold);
            monitor.Reset(1_000);

            LiveGapVerdict verdict = monitor.Observe(1_000 + Threshold + offsetFromThreshold, out long previous);

            Assert.That(verdict, Is.EqualTo(expected));
            Assert.That(previous, Is.EqualTo(1_000), "the departure instant is the previous frame");
            Assert.That(monitor.PreviousUtcMs, Is.EqualTo(1_000 + Threshold + offsetFromThreshold), "the observed frame is the new reference");
        }

        [Test]
        public void ANegativeClockIsNeitherAGapNorACredit_AndLaterDeltasRunFromTheNewClock()
        {
            var monitor = new LiveGapMonitor(Threshold);
            monitor.Reset(10_000_000);

            Assert.That(monitor.Observe(10_000_000 - 1, out _), Is.EqualTo(LiveGapVerdict.BackwardClock));
            Assert.That(monitor.Observe(10_000_000 - 3_600_000, out long previous), Is.EqualTo(LiveGapVerdict.BackwardClock));
            Assert.That(previous, Is.EqualTo(10_000_000 - 1));

            long newClock = 10_000_000 - 3_600_000;
            Assert.That(monitor.Observe(newClock + Threshold, out _), Is.EqualTo(LiveGapVerdict.Live), "measured from the new clock, not the old one");
            Assert.That(monitor.Observe(newClock + 2 * Threshold + 1, out _), Is.EqualTo(LiveGapVerdict.Gap));
        }

        [Test]
        public void WithoutAReferenceTheFirstObservationIsLive_AndClearForgetsIt()
        {
            var monitor = new LiveGapMonitor(Threshold);
            Assert.That(monitor.HasPrevious, Is.False);
            Assert.That(monitor.Observe(5_000_000, out _), Is.EqualTo(LiveGapVerdict.Live), "an absence before the first frame is not this check's");
            Assert.That(monitor.HasPrevious, Is.True);

            monitor.Clear();
            Assert.That(monitor.HasPrevious, Is.False);
            Assert.That(monitor.Observe(5_000_000 + 10 * Threshold, out _), Is.EqualTo(LiveGapVerdict.Live));
        }

        [Test]
        public void ADistanceThatOverflowsALongIsAGap_AndTheThresholdMustBePositive()
        {
            var monitor = new LiveGapMonitor(Threshold);
            monitor.Reset(long.MinValue + 1);
            Assert.That(monitor.Observe(long.MaxValue, out _), Is.EqualTo(LiveGapVerdict.Gap));

            Assert.Throws<ArgumentOutOfRangeException>(() => new LiveGapMonitor(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LiveGapMonitor(-5));
        }

        [Test]
        public void ResetStartsANewReferenceWithoutAVerdict()
        {
            var monitor = new LiveGapMonitor(Threshold);
            monitor.Reset(0);
            monitor.Reset(10 * Threshold);
            Assert.That(monitor.Observe(10 * Threshold + Threshold, out long previous), Is.EqualTo(LiveGapVerdict.Live));
            Assert.That(previous, Is.EqualTo(10 * Threshold));
        }

        // ---- one shared threshold ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheAutosaveIntervalAndTheGapThresholdAreOneAccessor()
        {
            DurabilityRig h = Played();
            long catalogValue = h.Rig.Catalog.RunStart.LiveGapThresholdMs ?? 60_000;

            Assert.That(h.Persistence.LiveGapThresholdMs, Is.EqualTo(catalogValue));
            Assert.That(h.Persistence.MinimumAutosaveIntervalMs, Is.EqualTo(h.Persistence.LiveGapThresholdMs));
        }

        // ---- the departure of RecordSuspension(atUtcMs) ----------------------------------------------------------------------------

        [Test]
        public void TheGapDepartureIsThePreviousFrame_AndTheReturnCreditsFromIt()
        {
            DurabilityRig h = Played();
            long previousFrame = h.Session.TimelineUtcMs;
            h.Clock.UnixMs = previousFrame + TwoHours;

            DurabilityRig.Ok(h.Persistence.RecordSuspension(previousFrame));

            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(previousFrame), "not the clock read of the wake-up");
            RunPersistenceOutcome returned = DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(returned.OfflineSummary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(returned.OfflineSummary.DepartureUtcMs, Is.EqualTo(previousFrame));
            Assert.That(returned.OfflineSummary.ReturnUtcMs, Is.EqualTo(previousFrame + TwoHours));
            Assert.That(returned.OfflineSummary.MeasuredMs, Is.EqualTo(TwoHours));
        }

        [Test]
        public void AGapEqualsAQuitAndRestart_SameStateSameSummary()
        {
            DurabilityRig gap = Played();
            DurabilityRig quit = Played();
            long previousFrame = gap.Session.TimelineUtcMs;
            Assert.That(quit.Session.TimelineUtcMs, Is.EqualTo(previousFrame), "the two runs are identical up to the departure");
            Assert.That(gap.Dump(), Is.EqualTo(quit.Dump()));

            gap.Clock.UnixMs = previousFrame + TwoHours;
            DurabilityRig.Ok(gap.Persistence.RecordSuspension(previousFrame));
            RunPersistenceOutcome gapReturn = DurabilityRig.Ok(gap.Persistence.ResumeAndReconcile());

            quit.Clock.UnixMs = previousFrame;
            DurabilityRig.Ok(quit.Persistence.RecordSuspension());
            quit.Clock.UnixMs = previousFrame + TwoHours;
            RunPersistenceOutcome quitReturn = DurabilityRig.Ok(quit.Persistence.ResumeAndReconcile());

            Assert.That(gap.Dump(), Is.EqualTo(quit.Dump()), "the reclassified gap is the quit-and-restart state");
            RunOfflineSummary g = gapReturn.OfflineSummary;
            RunOfflineSummary q = quitReturn.OfflineSummary;
            Assert.That(g.Outcome, Is.EqualTo(q.Outcome));
            Assert.That(g.DepartureUtcMs, Is.EqualTo(q.DepartureUtcMs));
            Assert.That(g.ReturnUtcMs, Is.EqualTo(q.ReturnUtcMs));
            Assert.That(g.MeasuredMs, Is.EqualTo(q.MeasuredMs));
            Assert.That(g.EffectiveTicks, Is.EqualTo(q.EffectiveTicks));
            Assert.That(g.CashAfter, Is.EqualTo(q.CashAfter));
            Assert.That(g.Completions, Is.EqualTo(q.Completions));
            Assert.That(g.SalesIncome, Is.EqualTo(q.SalesIncome));
        }

        [Test]
        public void ThePlainOverloadIsUnchanged_ItDepartsAtTheClockRead()
        {
            DurabilityRig h = Played();
            long timeline = h.Session.TimelineUtcMs;
            h.Clock.UnixMs = timeline + 5_000;

            DurabilityRig.Ok(h.Persistence.RecordSuspension());

            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(timeline + 5_000), "max(clock, timeline), as before T337");
        }

        // SEC-10: the departure invariant.

        [Test]
        public void AnAtInTheFuture_IsClampedToTheClockRead_NeverLater()
        {
            DurabilityRig h = Played();
            long timeline = h.Session.TimelineUtcMs;
            h.Clock.UnixMs = timeline + 5_000;

            DurabilityRig.Ok(h.Persistence.RecordSuspension(timeline + 10_000_000_000L));
            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(timeline + 5_000));
            Assert.That(h.Active().DepartureUtcMs, Is.LessThanOrEqualTo(h.Clock.UnixMs));
        }

        [Test]
        public void AnAtBeforeTheTimeline_IsRaisedToTheTimelineEnd_NeverBeforeTheSimulatedTime()
        {
            DurabilityRig h = Played();
            long timeline = h.Session.TimelineUtcMs;
            h.Clock.UnixMs = timeline + 5_000;

            DurabilityRig.Ok(h.Persistence.RecordSuspension(timeline - 100_000));
            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(timeline), "load rule 4: no timer is after the departure");
            Assert.That(h.Active().DepartureUtcMs, Is.LessThanOrEqualTo(h.Clock.UnixMs));
        }

        [Test]
        public void TheDepartureNeverMovesBackBeforeTheActiveGeneration()
        {
            DurabilityRig h = Played();
            long timeline = h.Session.TimelineUtcMs;
            h.Clock.UnixMs = timeline + 50_000;
            DurabilityRig.Ok(h.Persistence.SaveNow());
            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(timeline + 50_000));

            // The wall clock was set back by 30 s; the gap's previous frame is behind the active generation's departure.
            h.Clock.UnixMs = timeline + 20_000;
            DurabilityRig.Ok(h.Persistence.RecordSuspension(timeline + 20_000));

            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(timeline + 50_000));
        }

        [Test]
        public void AnAcceleratedTimelineAheadOfTheClock_DepartsAtTheTimeline_AndTheReturnCreditsNothing()
        {
            // Development time acceleration (D-I): the simulated timeline ran ahead of the wall clock. Load rule 4 keeps the departure at the
            // timeline, so the departure is later than the clock read and the credit of the next return is 0.
            DurabilityRig h = Played();
            long timeline = h.Session.TimelineUtcMs;
            h.Clock.UnixMs = timeline - 5_000;

            DurabilityRig.Ok(h.Persistence.RecordSuspension(timeline - 6_000));
            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(timeline));

            h.Clock.UnixMs = timeline - 4_000;
            RunPersistenceOutcome returned = DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(returned.OfflineSummary, Is.Not.Null);
            Assert.That(returned.OfflineSummary.Outcome, Is.EqualTo(RunOfflineOutcome.BackwardClock));
            Assert.That(returned.OfflineSummary.EffectiveTicks, Is.EqualTo(0L));
        }

        // ---- a checkpoint inside a gap ---------------------------------------------------------------------------------------------

        [Test]
        public void ACheckpointInsideAGap_IsStampedFromTheRealClock_SoTheNextAutosaveIsThrottled()
        {
            DurabilityRig h = Played();
            long previousFrame = h.Session.TimelineUtcMs;
            long interval = h.Persistence.MinimumAutosaveIntervalMs;
            h.Clock.UnixMs = previousFrame + TwoHours;

            DurabilityRig.Ok(h.Persistence.RecordSuspension(previousFrame));
            DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());

            // The departure instant is two hours old; the checkpoint itself was written now. Stamping it from atUtcMs would let this write.
            Assert.That(DurabilityRig.Ok(h.Persistence.Autosave()).AutosaveThrottled, Is.True, "inside the interval of the gap's own checkpoint");
            h.Clock.UnixMs += interval - 1;
            Assert.That(DurabilityRig.Ok(h.Persistence.Autosave()).AutosaveThrottled, Is.True);
            h.Clock.UnixMs += 1;
            RunPersistenceOutcome written = DurabilityRig.Ok(h.Persistence.Autosave());
            Assert.That(written.AutosaveThrottled, Is.False, "the interval has elapsed since the real clock read");
            Assert.That(written.WasNoOp, Is.False);
        }

        [Test]
        public void ARepeatedGapSuspensionWhileSuspendedIsTheNoOp_AndOutsideRunningItIsRefused()
        {
            DurabilityRig h = Played();
            long previousFrame = h.Session.TimelineUtcMs;
            h.Clock.UnixMs = previousFrame + TwoHours;
            DurabilityRig.Ok(h.Persistence.RecordSuspension(previousFrame));
            CheckpointId departure = h.Active().Id;

            RunPersistenceOutcome again = DurabilityRig.Ok(h.Persistence.RecordSuspension(previousFrame + 1_000));
            Assert.That(again.WasNoOp, Is.True);
            Assert.That(h.Active().Id, Is.EqualTo(departure), "nothing was rewritten");

            DurabilityRig notRunning = DurabilityRig.Create(resume: false);
            DurabilityRig.Rejected(notRunning.Persistence.RecordSuspension(0), RunPersistenceReasons.LifecycleNoOp);
            Assert.That(notRunning.Persistence.Lifecycle, Is.Not.EqualTo(LifecycleState.Suspended));
        }

        // ---- a gap during a pending prestige ----------------------------------------------------------------------------------------

        [Test]
        public void AGapWhileAPrestigeIsPending_AndNotDurable_SuspendsButCreditsNothingAndAwardsNothing()
        {
            DurabilityRig h = ReadyToPrestige();
            Guid oldRun = h.Session.State.RunId;
            long ip = h.Session.Permanent.Ip;
            long award = Confirm(h).IpAwarded;
            CheckpointId before = h.Active().Id;
            long previousFrame = h.Session.TimelineUtcMs;
            h.Clock.UnixMs = previousFrame + TwoHours;

            DurabilityRig.Rejected(h.Persistence.RecordSuspension(previousFrame), RunReasons.TransitionPending);
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended), "the departure suspends even though nothing was written");
            Assert.That(h.Active().Id, Is.EqualTo(before), "no generation was written");

            RunPersistenceOutcome returned = DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(returned.TransitionPending, Is.True);
            Assert.That(returned.OfflineSummary, Is.Null, "the old run is not credited while its successor is pending");
            Assert.That(h.Session.State.RunId, Is.EqualTo(oldRun));
            Assert.That(h.Session.State.RunNumber, Is.EqualTo(1));
            Assert.That(h.Session.Permanent.Ip, Is.EqualTo(ip), "no award");
            Assert.That(h.Session.PendingTransition, Is.Not.Null, "the transition stays retryable");

            DurabilityRig.Ok(h.Persistence.RetryPendingTransition());
            Assert.That(h.Session.State.RunNumber, Is.EqualTo(2));
            Assert.That(h.Session.Permanent.Ip, Is.EqualTo(ip + award), "exactly one award");
        }

        [Test]
        public void AGapWhileAPrestigeIsPending_AndDurable_PublishesTheNewRunAndCreditsItOnceFromItsOwnDeparture()
        {
            DurabilityRig h = ReadyToPrestige();
            Guid oldRun = h.Session.State.RunId;
            long ip = h.Session.Permanent.Ip;
            long award = Confirm(h).IpAwarded;
            RunCheckpoint promotion = PromoteBehindTheSessionsBack(h, 3);
            long previousFrame = h.Session.TimelineUtcMs;
            h.Clock.UnixMs = previousFrame + TwoHours;

            DurabilityRig.Ok(h.Persistence.RecordSuspension(previousFrame));

            Assert.That(h.Session.State.RunNumber, Is.EqualTo(2), "the verified promotion was published");
            long departure = h.Active().DepartureUtcMs;
            Assert.That(departure, Is.GreaterThanOrEqualTo(promotion.DepartureUtcMs), "never before the new run's own departure");
            Assert.That(departure, Is.LessThanOrEqualTo(h.Clock.UnixMs), "SEC-10");

            RunPersistenceOutcome returned = DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(returned.OfflineSummary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(returned.OfflineSummary.DepartureUtcMs, Is.EqualTo(departure), "the departure of the new run, never the old run's");
            Assert.That(h.Session.State.PreviousRunId, Is.EqualTo(oldRun));
            Assert.That(h.Session.Permanent.Ip, Is.EqualTo(ip + award), "exactly one award");
            Assert.That(h.Session.Permanent.PrestigeCount, Is.EqualTo(1));
            Assert.That(h.Session.PendingTransition, Is.Null);
        }

        // ---- helpers ----------------------------------------------------------------------------------------------------------------

        /// <summary>A played run (two machines, auto-sell, Overdrive, 1.3 s of production) whose clock is synchronized to the timeline.</summary>
        private static DurabilityRig Played()
        {
            DurabilityRig h = DurabilityRig.Create();
            h.Play();
            h.Sync();
            return h;
        }

        private static DurabilityRig ReadyToPrestige()
        {
            DurabilityRig h = Played();
            DurabilityRig.Ok(h.Persistence.SaveNow());
            return h;
        }

        private static PrestigeConfirmation Confirm(DurabilityRig h)
        {
            RunCommandResult<PrestigeConfirmation> confirmed = h.Commands.ConfirmPrestige();
            Assert.That(confirmed.IsSuccess, Is.True, confirmed.ReasonCode);
            return confirmed.Value;
        }

        /// <summary>The pending candidate becomes durable without the session learning of it (a crash between the two steps).</summary>
        private static RunCheckpoint PromoteBehindTheSessionsBack(DurabilityRig h, int checkpointSuffix)
        {
            PrestigeCandidate candidate = h.Session.PendingTransition;
            RunCheckpoint active = h.Active();
            var checkpoint = new RunCheckpoint(new CheckpointId(new Guid("55555555-5555-5555-5555-" + checkpointSuffix.ToString("D12"))),
                active.Generation + 1, Math.Max(candidate.ConfirmedUtcMs, active.DepartureUtcMs), h.Rig.Catalog.OfflinePolicy.Revision, null,
                h.Rig.Catalog.ContentRevision, null, candidate.Run, candidate.Permanent);
            Assert.That(h.Store.Promote(checkpoint).Succeeded, Is.True);
            return checkpoint;
        }
    }
}
