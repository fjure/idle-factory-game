using System;
using System.Collections;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T337 (FR-032, F5-R10): the live-gap wiring of <see cref="FactoryRuntimeHost"/> and <see cref="RunLifecycleAdapter"/> over the real
    /// <see cref="RunPersistence"/> and the real file store. The host reads UTC explicitly, a wall delta strictly above the shared threshold is a
    /// departure at the previous frame and a standard return (published as a return so the summary opens), and the time scale applies only afterwards.
    /// The decision itself and the persistence overload are in the Edit Mode <c>LiveGapTests</c>.
    /// </summary>
    public sealed class LiveGapHostTests
    {
        private const string MinerId = "machine.miner";
        private const long Second = TimeSpan.TicksPerSecond;

        private string root;
        private RunRig rig;
        private GameObject hostObject;
        private FactoryRuntimeHost host;
        private RunLifecycleAdapter adapter;
        private double activeSeconds;
        private long threshold;
        private GameObject extraObject;
        private RecordingSink sink;

        /// <summary>Records the Clock and OfflineReturn records of the session (T364); every other record is ignored.</summary>
        private sealed class RecordingSink : IRunEventSink
        {
            public readonly System.Collections.Generic.List<RunClockEvent> Clocks = new System.Collections.Generic.List<RunClockEvent>();
            public readonly System.Collections.Generic.List<RunOfflineReturnEvent> Returns = new System.Collections.Generic.List<RunOfflineReturnEvent>();
            public void OnHeader(in RunHeaderEvent e) { }
            public void OnSample(in RunSampleEvent e) { }
            public void OnMilestone(in RunMilestoneEvent e) { }
            public void OnSale(in RunSaleEvent e) { }
            public void OnMining(in RunMiningEvent e) { }
            public void OnContract(in RunContractEvent e) { }
            public void OnPrestige(in RunPrestigeEvent e) { }
            public void OnOfflineReturn(in RunOfflineReturnEvent e) => Returns.Add(e);
            public void OnBottleneck(in RunBottleneckEvent e) { }
            public void OnPurchase(in RunPurchaseEvent e) { }
            public void OnClock(in RunClockEvent e) => Clocks.Add(e);
            public void OnLifecycle(in RunLifecycleEvent e) { }
        }

        private sealed class FakeTimeScaleSource : ITimeScaleSource
        {
            public double Scale { get; set; } = 1d;
            public int ReportCount { get; private set; }

            public void ReportEffectiveMultiplier(double effectiveMultiplier) => ReportCount++;
        }

        private sealed class FlakyClock : IUtcClock
        {
            private readonly ManualUtcClock inner;

            public FlakyClock(ManualUtcClock inner) => this.inner = inner;

            public bool Fail { get; set; }

            public DateTimeOffset GetUtcNow()
            {
                if (Fail) throw new InvalidOperationException("Injected clock failure.");
                return inner.GetUtcNow();
            }
        }

        [UnitySetUp]
        public IEnumerator CreateRun()
        {
            FactoryRuntimeHost.DefaultTimeScaleSource = null;
            root = RunPlayerFixtures.NewRoot();
            sink = new RecordingSink();
            rig = RunRig.Create(root, RunPlayerFixtures.ScaledCatalog(), RunPlayerFixtures.StartUtcMs, sink);
            rig.ColdStartOk(null);
            var built = rig.Commands.Build(0, MinerId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);

            // Auto-sell keeps the store from filling, so the Miner works (and the host advances) however long the offline credit is.
            Assert.That(rig.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);

            activeSeconds = 100d;
            hostObject = new GameObject("Live Gap Host Test");
            host = hostObject.AddComponent<FactoryRuntimeHost>();
            adapter = hostObject.AddComponent<RunLifecycleAdapter>();
            host.ConfigureRun(rig.Session, () => activeSeconds);
            adapter.Configure(rig.Persistence, host.PrepareForLifecycleDeparture);
            threshold = rig.Persistence.LiveGapThresholdMs;
            host.ConfigureLiveGap(rig.Clock, threshold, adapter.ReconcileLiveGap);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Destroy()
        {
            FactoryRuntimeHost.DefaultTimeScaleSource = null;
            if (extraObject != null) UnityEngine.Object.Destroy(extraObject);
            if (hostObject != null) UnityEngine.Object.Destroy(hostObject);
            yield return null;
            extraObject = null;
            hostObject = null;
            host = null;
            adapter = null;
            RunPlayerFixtures.DeleteRoot(root);
        }

        /// <summary>One synthetic frame: the wall clock and the active-time source move by their own deltas, then the host runs its frame step.</summary>
        private void Frame(long utcDeltaMs, double activeDeltaSeconds)
        {
            activeSeconds += activeDeltaSeconds;
            rig.Clock.UnixMs += utcDeltaMs;
            host.FlushActiveInterval();
        }

        private long Ticks => rig.Session.ActiveTicksSinceAnchor;

        [Test]
        public void TheThresholdIsStrictAtTheHost_BelowAndEqualRunLive_OneMillisecondAboveIsAnOfflineReturn()
        {
            Frame(1_000, 1d);
            var ticksBefore = Ticks;
            Frame(threshold - 1, 0.125d);
            Frame(threshold, 0.125d);
            Assert.That(host.LiveGapsDetected, Is.EqualTo(0), "-1 and = are simulated live");
            Assert.That(Ticks - ticksBefore, Is.EqualTo(Second / 4), "both frames advanced by their active delta");
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.None));

            var previousFrame = rig.Clock.UnixMs;
            Frame(threshold + 1, 0.125d);

            Assert.That(host.LiveGapsDetected, Is.EqualTo(1));
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return), "published as a return, so the offline summary opens");
            Assert.That(adapter.LastResult.IsSuccess, Is.True, adapter.LastResult.ReasonCode);
            var summary = adapter.LastResult.Value.OfflineSummary;
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.DepartureUtcMs, Is.EqualTo(previousFrame), "a departure at the previous frame");
            Assert.That(summary.MeasuredMs, Is.EqualTo(threshold + 1));
            Assert.That(rig.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
        }

        [Test]
        public void TheGapFrameIsNotSimulatedLive_AndNothingIsReplayedAfterwards()
        {
            Frame(1_000, 1d);
            Frame(threshold + 1, 5d);
            Assert.That(host.LiveGapsDetected, Is.EqualTo(1));
            var afterGap = Ticks;
            Assert.That(afterGap, Is.LessThan(5 * Second), "the 5 s of the gap frame were not advanced at 1/1");

            Frame(1_000, 1d);
            Assert.That(host.LiveGapsDetected, Is.EqualTo(1), "no second gap");
            Assert.That(Ticks - afterGap, Is.EqualTo(Second), "the next frame advances by its own delta only");
        }

        [Test]
        public void ANegativeClockCreditsNothing_TheRunCarriesOn_AndLaterDeltasRunFromTheNewClock()
        {
            Frame(1_000, 1d);
            var before = Ticks;
            Frame(-3_600_000, 1d);
            Assert.That(host.LiveGapsDetected, Is.EqualTo(0), "a backwards clock is not a gap");
            Assert.That(Ticks - before, Is.EqualTo(Second), "the frame was simulated live");
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.None), "no offline credit");

            Frame(threshold, 1d);
            Assert.That(host.LiveGapsDetected, Is.EqualTo(0), "measured from the new clock");
            Frame(threshold + 1, 1d);
            Assert.That(host.LiveGapsDetected, Is.EqualTo(1));
        }

        // ---- T364: Clock records ------------------------------------------------------------------------------------------------

        [Test]
        public void ALiveGapIsRecordedAsAClockRecordWithTheWallDeltaAndTheReturnTookIt()
        {
            Frame(1_000, 1d);
            Assert.That(sink.Clocks, Is.Empty, "a live frame records nothing");

            Frame(threshold + 1, 0.125d);

            Assert.That(sink.Clocks, Has.Count.EqualTo(1));
            Assert.That(sink.Clocks[0].Kind, Is.EqualTo(RunClockKind.LiveGap));
            Assert.That(sink.Clocks[0].DeltaMs, Is.EqualTo(threshold + 1), "the wall delta, not the active one");
            Assert.That(sink.Clocks[0].Reclassified, Is.True, "the standard offline return took the gap");
        }

        [Test]
        public void AGapThatCouldNotStartIsRecordedAsNotReclassified()
        {
            host.ConfigureLiveGap(rig.Clock, threshold,
                previousFrame => RunCommandResult.Reject<RunPersistenceOutcome>(RunPersistenceReasons.LifecycleNoOp));
            Frame(1_000, 1d);
            Frame(threshold + 1, 1d);

            Assert.That(sink.Clocks, Has.Count.EqualTo(1));
            Assert.That(sink.Clocks[0].Kind, Is.EqualTo(RunClockKind.LiveGap));
            Assert.That(sink.Clocks[0].Reclassified, Is.False);
        }

        [Test]
        public void AForwardJumpInsideTheThresholdIsRecordedAndSimulatedLikeAnyLiveFrame()
        {
            Frame(1_000, 1d);
            long before = Ticks;

            Frame(20_000, 0.125d); // the wall clock moved 20 s while the app's own time moved 125 ms
            Assert.That(host.LiveGapsDetected, Is.EqualTo(0), "below the threshold: not a gap");
            Assert.That(Ticks - before, Is.EqualTo(Second / 8), "advanced by the active delta only");
            Assert.That(sink.Clocks, Has.Count.EqualTo(1));
            Assert.That(sink.Clocks[0].Kind, Is.EqualTo(RunClockKind.ForwardJump));
            Assert.That(sink.Clocks[0].DeltaMs, Is.InRange(19_800L, 19_900L), "the surplus over the real delta");
            Assert.That(sink.Clocks[0].Reclassified, Is.False);

            Frame(1_000, 1d);
            Frame(4_000, 0.125d); // 3.9 s of surplus is below FactoryRuntimeHost.ForwardJumpMinMs
            Assert.That(sink.Clocks, Has.Count.EqualTo(1), "an ordinary frame or a small surplus records nothing");
        }

        [Test]
        public void TheBootstrapWiringFlagsOnlyALiveGapReturnAsReclassified()
        {
            // The composition root raises the OfflineReturn record from the adapter's ResultChanged with IsLiveGapReturn (CoreGameFoundationBootstrap.LastOfflineSummary).
            adapter.ResultChanged += (result, kind) =>
            {
                if (result.IsSuccess && result.Value.OfflineSummary != null)
                    IdleFactory.Unity.Persistence.SessionLogRaiser.RaiseOfflineReturn(rig.Session, result.Value.OfflineSummary, adapter.IsLiveGapReturn);
            };

            Frame(1_000, 1d);
            Frame(threshold + 1, 0.125d);
            Assert.That(sink.Returns, Has.Count.EqualTo(1));
            Assert.That(sink.Returns[0].ReclassifiedLiveGap, Is.True, "a live-gap return");
            Assert.That(adapter.IsLiveGapReturn, Is.False, "the flag is only set while the result is published");

            hostObject.SendMessage("OnApplicationPause", true);
            rig.Clock.UnixMs += 120_000;
            hostObject.SendMessage("OnApplicationPause", false);
            Assert.That(sink.Returns, Has.Count.EqualTo(2), "the pause return is an offline return too");
            Assert.That(sink.Returns[1].ReclassifiedLiveGap, Is.False, "but not a live gap");
        }

        [Test]
        public void AcceleratedFramesDoNotCount_TheGapReadsTheWallDeltaNeverTheScaledOne()
        {
            // A 1 s threshold makes the point sharp: a x600 frame simulates 10 s (S_max), ten times the threshold, in a frame whose wall delta is
            // exactly the threshold.
            host.ConfigureLiveGap(rig.Clock, 1_000, adapter.ReconcileLiveGap);
            var fake = new FakeTimeScaleSource { Scale = 600d };
            host.TimeScaleSource = fake;
            var before = Ticks;

            Frame(1_000, 1d);
            Frame(1_000, 1d);

            Assert.That(host.LiveGapsDetected, Is.EqualTo(0));
            Assert.That(fake.ReportCount, Is.EqualTo(2), "the time scale was applied to every live frame");
            Assert.That(Ticks - before, Is.EqualTo(20 * Second), "20 simulated seconds in two frames, none of them a gap");
        }

        [Test]
        public void TheTimeScaleIsAppliedAfterTheGapCheck_AGapFrameNeverReachesIt()
        {
            var fake = new FakeTimeScaleSource { Scale = 10d };
            host.TimeScaleSource = fake;
            Frame(1_000, 1d);
            Assert.That(fake.ReportCount, Is.EqualTo(1));

            Frame(threshold + 1, 1d);
            Assert.That(host.LiveGapsDetected, Is.EqualTo(1), "the wall delta decides, even with a scale source present");
            Assert.That(fake.ReportCount, Is.EqualTo(1), "the gap frame was not scaled or advanced");

            Frame(1_000, 1d);
            Assert.That(fake.ReportCount, Is.EqualTo(2));
        }

        [Test]
        public void AGapThatCannotStart_IsReportedAndItsIntervalIsDroppedNotSimulated()
        {
            string reported = null;
            host.LiveGapRejected += code => reported = code;
            host.ConfigureLiveGap(rig.Clock, threshold,
                previousFrame => RunCommandResult.Reject<RunPersistenceOutcome>(RunPersistenceReasons.LifecycleNoOp));
            Frame(1_000, 1d);
            var before = Ticks;

            Frame(threshold + 1, 5d);

            Assert.That(host.LiveGapsDetected, Is.EqualTo(1));
            Assert.That(reported, Is.EqualTo(RunPersistenceReasons.LifecycleNoOp));
            Assert.That(Ticks, Is.EqualTo(before), "the gap's interval was neither simulated nor credited");
            Frame(1_000, 1d);
            Assert.That(Ticks - before, Is.EqualTo(Second), "the host carries on with the next frame");
        }

        [Test]
        public void AnActiveSegmentRestartIsNotAGap_TheReferenceIsTakenWhenTheSegmentStarts()
        {
            Frame(1_000, 1d);

            // A lifecycle departure stops the host; the absence is the lifecycle adapter's business, not the live-gap check's.
            host.PrepareForLifecycleDeparture();
            rig.Clock.UnixMs += 2 * 3_600_000L;
            activeSeconds += 5d;
            host.SynchronizeAfterSuccessfulLifecycleReturn();
            host.enabled = true;

            Frame(1_000, 1d);
            Assert.That(host.LiveGapsDetected, Is.EqualTo(0));
        }

        [Test]
        public void AnUnreadableClockIsNotAGap_AndTheNextReadableFrameStartsANewReference()
        {
            var flaky = new FlakyClock(rig.Clock);
            host.ConfigureLiveGap(flaky, threshold, adapter.ReconcileLiveGap);
            Frame(1_000, 1d);

            flaky.Fail = true;
            Frame(2 * 3_600_000L, 1d);
            Assert.That(host.LiveGapsDetected, Is.EqualTo(0), "an unreadable clock never reports a gap");

            flaky.Fail = false;
            Frame(1_000, 1d);
            Assert.That(host.LiveGapsDetected, Is.EqualTo(0), "the first readable frame only sets the reference");
            Frame(threshold + 1, 1d);
            Assert.That(host.LiveGapsDetected, Is.EqualTo(1));
        }

        [Test]
        public void TheFirstReadableFrameAfterAnUnreadableClockRecordsNoForwardJump()
        {
            var flaky = new FlakyClock(rig.Clock);
            host.ConfigureLiveGap(flaky, threshold, adapter.ReconcileLiveGap);
            Frame(1_000, 1d);

            flaky.Fail = true;
            Frame(30_000, 0.125d); // unreadable: the reference is forgotten
            flaky.Fail = false;
            Frame(30_000, 0.125d); // readable again: only a new reference, however far the clock went meanwhile

            Assert.That(sink.Clocks, Is.Empty, "no jump of the outage length or of the epoch value");

            Frame(20_000, 0.125d); // with a reference again, a real jump is recorded
            Assert.That(sink.Clocks, Has.Count.EqualTo(1));
            Assert.That(sink.Clocks[0].Kind, Is.EqualTo(RunClockKind.ForwardJump));
        }

        [Test]
        public void TheGapPathIsRefusedAfterALifecycleDeparture_ItNeverStacksOnAPendingReturn()
        {
            adapter.NotifyPauseChanged(true);
            Assert.That(rig.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
            var returnsBefore = adapter.LastReturnKind;

            var refused = adapter.ReconcileLiveGap(rig.Clock.UnixMs);

            Assert.That(refused.IsSuccess, Is.False);
            Assert.That(refused.ReasonCode, Is.EqualTo(RunPersistenceReasons.LifecycleNoOp));
            Assert.That(rig.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended), "the pending return is untouched");
            Assert.That(adapter.LastReturnKind, Is.EqualTo(returnsBefore));

            adapter.NotifyPauseChanged(false);
            Assert.That(rig.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running), "the ordinary return still reconciles");
        }

        [Test]
        public void ADepartureThatThrewBeforeTheSuspensionStarted_IsRetriedByALaterFocusLoss()
        {
            // T306 review finding 2: the departure flag was set before RecordSuspension ran, so a failed departure was never retried.
            extraObject = new GameObject("Live Gap Retry Adapter");
            var retry = extraObject.AddComponent<RunLifecycleAdapter>();
            var prepared = 0;
            retry.Configure(rig.Persistence, () =>
            {
                prepared++;
                if (prepared == 1) throw new InvalidOperationException("Injected host preparation failure.");
            });

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Run lifecycle callback failed and was contained"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("InvalidOperationException"));
            retry.NotifyPauseChanged(true);
            Assert.That(rig.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running), "the first departure never reached the persistence");

            retry.NotifyFocusChanged(false);
            Assert.That(prepared, Is.EqualTo(2), "the focus loss retried the departure");
            Assert.That(rig.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));

            retry.NotifyFocusChanged(true);
            retry.NotifyPauseChanged(false);
            Assert.That(rig.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
        }
    }
}
