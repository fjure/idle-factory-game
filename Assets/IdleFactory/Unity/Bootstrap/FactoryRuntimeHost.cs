using System;
using System.Diagnostics;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>
    /// Advances the run session from one cumulative active-time source (since T119 the Feature 003 run is the only session
    /// it advances).
    /// </summary>
    public sealed class FactoryRuntimeHost : MonoBehaviour
    {
        private const double CoreTicksPerSecond = TimeSpan.TicksPerSecond;
        private const double LongExclusiveUpperBound = 9223372036854775808d;

        private RunSession runSession;
        private MachineStateView[] machineViews = new MachineStateView[16];
        private Func<double> readCumulativeActiveSeconds;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private bool applicationQuitting;
        private bool trackingActiveSegment;
        private double completedActiveSeconds;
        private double activeSegmentStartSeconds;
        private long accountedCumulativeTicks;
        private ITimeScaleSource timeScaleSource;
        private IUtcClock liveGapClock;
        private LiveGapMonitor liveGapMonitor;
        private Func<long, RunCommandResult<RunPersistenceOutcome>> reconcileLiveGap;

        /// <summary>
        /// S_max (F5-R11): the most simulated time one frame may advance once a time-scale source is present, in seconds.
        /// </summary>
        public const double MaxSimulatedSecondsPerFrame = 10d;

        private static ITimeScaleSource defaultTimeScaleSource;

        /// <summary>
        /// The development time-scale source used by hosts without their own. Null in every production build; only
        /// <c>IdleFactory.DevTools</c> sets it (define <c>IDLEFACTORY_DEV</c>).
        /// </summary>
        public static ITimeScaleSource DefaultTimeScaleSource
        {
            get => defaultTimeScaleSource;
            set => defaultTimeScaleSource = value;
        }

        /// <summary>
        /// The time-scale source of this host, falling back to <see cref="DefaultTimeScaleSource"/>. With no source the
        /// advance is the real delta, unchanged and unclamped.
        /// </summary>
        public ITimeScaleSource TimeScaleSource
        {
            get => timeScaleSource ?? defaultTimeScaleSource;
            set => timeScaleSource = value;
        }

        /// <summary>Raised when the run session rejects an advance, with its stable reason code (for example <c>value.limit</c>).</summary>
        public event Action<string> RunAdvancementRejected;

        /// <summary>
        /// Raised when a live gap was detected but the departure could not start (T337): the run was not live or the application was
        /// quitting. It carries the stable reason code; the interval of the gap is dropped, never simulated live.
        /// </summary>
        public event Action<string> LiveGapRejected;

        /// <summary>How many live gaps (a wall delta strictly above the threshold) this host has detected; a diagnostic for tests.</summary>
        public int LiveGapsDetected { get; private set; }

        /// <summary>The run session this host advances, or null before <see cref="ConfigureRun"/>.</summary>
        public RunSession Run => runSession;

        /// <summary>Whether Unity lifecycle state currently permits active-time advancement.</summary>
        public bool IsApplicationLifecycleAvailable => applicationFocused && !applicationPaused && !applicationQuitting;

        /// <summary>
        /// Configures the host to advance a <see cref="RunSession"/> from one cumulative active-time source. The host
        /// advances only while some machine can produce or some timer (an effect, the Overdrive window or cooldown, a
        /// contract deadline) is pending, so an idle factory costs no simulation. The interval of an idle frame is
        /// accounted and dropped, never replayed later.
        /// </summary>
        public void ConfigureRun(RunSession session, Func<double> cumulativeActiveTimeSource)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (cumulativeActiveTimeSource == null)
            {
                throw new ArgumentNullException(nameof(cumulativeActiveTimeSource));
            }

            if (runSession != null)
            {
                throw new InvalidOperationException("The factory runtime host has already been configured.");
            }

            runSession = session;
            readCumulativeActiveSeconds = cumulativeActiveTimeSource;
            completedActiveSeconds = 0d;
            accountedCumulativeTicks = 0L;
            StartActiveSegmentIfAvailable();
        }

        /// <summary>
        /// Enables the live-gap check (T337, FR-032, F5-R10): <paramref name="utcClock"/> is the explicit UTC read (the existing clock
        /// port, not the active-time delegate), <paramref name="thresholdMs"/> the shared threshold
        /// (<see cref="RunPersistence.LiveGapThresholdMs"/>), and <paramref name="reconcileGap"/> the gap path (the lifecycle adapter's
        /// <see cref="RunLifecycleAdapter.ReconcileLiveGap"/>: a departure at the previous frame, then the standard offline return).
        /// Without this call the host never checks for a gap. It may be called again to replace the wiring.
        /// </summary>
        public void ConfigureLiveGap(IUtcClock utcClock, long thresholdMs, Func<long, RunCommandResult<RunPersistenceOutcome>> reconcileGap)
        {
            liveGapClock = utcClock ?? throw new ArgumentNullException(nameof(utcClock));
            reconcileLiveGap = reconcileGap ?? throw new ArgumentNullException(nameof(reconcileGap));
            liveGapMonitor = new LiveGapMonitor(thresholdMs);
            if (trackingActiveSegment)
            {
                ResetLiveGapReference();
            }
        }

        /// <summary>
        /// Runs a resume or cold-start reconciliation and, in Development builds, logs one static line with its elapsed
        /// milliseconds (no path or file text), which the emulator journey parses (PE-2). The result is returned unchanged.
        /// </summary>
        public T MeasureResumeAndReconcile<T>(Func<T> reconcile)
        {
            if (reconcile == null)
            {
                throw new ArgumentNullException(nameof(reconcile));
            }

            var watch = Stopwatch.StartNew();
            try
            {
                return reconcile();
            }
            finally
            {
                watch.Stop();
                if (Debug.isDebugBuild)
                {
                    Debug.Log("ResumeAndReconcile ms=" + watch.ElapsedMilliseconds);
                }
            }
        }

        /// <summary>True when an advance can change the run: a machine can produce, or a timer is pending.</summary>
        public bool RunHasWork() => runSession != null && RunNeedsAdvance(runSession, ref machineViews);

        /// <summary>The gate of <see cref="RunHasWork"/>, reusable by presentation and tests. Allocation-free once the buffer fits.</summary>
        public static bool RunNeedsAdvance(RunSession session, ref MachineStateView[] buffer)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var state = session.State;
            if (state.Effects.Count > 0 || state.Overdrive.ActiveUntilUtcMs.HasValue ||
                state.Overdrive.CooldownUntilUtcMs.HasValue)
            {
                return true;
            }

            var contract = state.Contracts.Active;
            if (contract != null && contract.DeadlineUtcMs.HasValue)
            {
                return true;
            }

            var count = session.CopyMachineStates(buffer);
            if (count > buffer.Length)
            {
                buffer = new MachineStateView[count];
                count = session.CopyMachineStates(buffer);
            }

            for (var i = 0; i < count; i++)
            {
                var phase = buffer[i].Phase;
                if (phase == MachinePhase.Running)
                {
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            AdvanceThroughCurrentActiveTime();
        }

        private void OnApplicationPause(bool paused)
        {
            if (LifecyclePlatform.IsDesktop)
            {
                // A desktop player or the editor's Play Mode keeps simulating when it is paused or loses focus (T250).
                return;
            }

            if (applicationPaused == paused)
            {
                return;
            }

            if (paused)
            {
                StopActiveSegment();
                applicationPaused = true;
                return;
            }

            applicationPaused = false;
            StartActiveSegmentIfAvailable();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (LifecyclePlatform.IsDesktop)
            {
                // Losing window focus is not a departure on a desktop player: the active segment keeps running (T250).
                return;
            }

            if (applicationFocused == hasFocus)
            {
                return;
            }

            if (!hasFocus)
            {
                StopActiveSegment();
                applicationFocused = false;
                return;
            }

            applicationFocused = true;
            StartActiveSegmentIfAvailable();
        }

        private void OnApplicationQuit()
        {
            // T337 (T306 QA observation 1, SEC-9): a quit callback never lets an exception reach the engine.
            try
            {
                StopActiveSegment();
            }
            catch (Exception exception)
            {
                RunLifecycleAdapter.LogContained("Run host quit handling failed and was contained", exception);
            }
            finally
            {
                applicationQuitting = true;
            }
        }

        private void OnDisable()
        {
            StopActiveSegment();
        }

        private void OnEnable()
        {
            StartActiveSegmentIfAvailable();
        }

        private void OnDestroy()
        {
            StopActiveSegment();
        }

        /// <summary>
        /// Brings the run up to the current active time without stopping the segment, so a checkpoint written right now (the
        /// desktop focus-loss safety checkpoint, T250) holds everything simulated so far.
        /// </summary>
        public void FlushActiveInterval()
        {
            AdvanceThroughCurrentActiveTime();
        }

        /// <summary>
        /// Stops and disables this host before the lifecycle adapter records a suspension checkpoint.
        /// This explicit handoff also covers Unity callback orderings where a disabled behaviour misses
        /// the matching focus or pause return callback.
        /// </summary>
        public void PrepareForLifecycleDeparture()
        {
            StopActiveSegment();
            applicationFocused = false;
            applicationPaused = true;
            if (enabled)
            {
                enabled = false;
            }
        }

        /// <summary>
        /// Synchronizes host availability after the adapter has successfully reconciled a return. The composition root enables
        /// the host again; the host itself advances only while the run has work.
        /// </summary>
        public void SynchronizeAfterSuccessfulLifecycleReturn()
        {
            if (applicationQuitting)
            {
                return;
            }

            applicationFocused = true;
            applicationPaused = false;
        }

        private void StartActiveSegmentIfAvailable()
        {
            if (runSession == null || trackingActiveSegment || applicationPaused || !applicationFocused ||
                applicationQuitting || !isActiveAndEnabled)
            {
                return;
            }

            double nowSeconds;
            if (!TryReadValidClock(out nowSeconds))
            {
                return;
            }

            activeSegmentStartSeconds = nowSeconds;
            trackingActiveSegment = true;
            ResetLiveGapReference();
        }

        private void StopActiveSegment()
        {
            if (!trackingActiveSegment)
            {
                return;
            }

            double currentCumulativeActiveSeconds;
            long currentCumulativeTicks;
            if (TryGetCurrentCumulativeTime(out currentCumulativeActiveSeconds, out currentCumulativeTicks))
            {
                // The closing interval of a segment is never a live gap: a departure is already being recorded around it.
                AdvanceToCumulativeTicks(currentCumulativeTicks, false);
                completedActiveSeconds = currentCumulativeActiveSeconds;
            }

            trackingActiveSegment = false;
        }

        private void AdvanceThroughCurrentActiveTime()
        {
            if (!trackingActiveSegment)
            {
                return;
            }

            double currentCumulativeActiveSeconds;
            long currentCumulativeTicks;
            if (!TryGetCurrentCumulativeTime(out currentCumulativeActiveSeconds, out currentCumulativeTicks))
            {
                trackingActiveSegment = false;
                return;
            }

            AdvanceToCumulativeTicks(currentCumulativeTicks, true);
        }

        private void AdvanceToCumulativeTicks(long cumulativeTicks, bool detectLiveGap)
        {
            if (cumulativeTicks <= accountedCumulativeTicks)
            {
                return;
            }

            var elapsedTicks = cumulativeTicks - accountedCumulativeTicks;
            // Account each observed interval once, including a rejected attempt. A blocked lifecycle interval
            // must not be replayed later as a production catch-up.
            accountedCumulativeTicks = cumulativeTicks;

            // T337 (F5-R10): the live-gap check sits after the real delta is accounted and before the time scale is applied. It reads
            // the UTC delta, never the scaled one, so an accelerated frame is not a gap. A gap is reclassified as an offline return and
            // its frame is not simulated live (the delta stays accounted, so it is never replayed).
            if (detectLiveGap && TryHandleLiveGap(elapsedTicks))
            {
                return;
            }

            if (runSession == null || !RunNeedsAdvance(runSession, ref machineViews))
            {
                return;
            }

            // Order (plan, data flow): the real delta is accounted above, then scaled and clamped to S_max, then advanced.
            var source = TimeScaleSource;
            if (source != null)
            {
                elapsedTicks = ScaleAndClamp(elapsedTicks, source);
                if (elapsedTicks <= 0L)
                {
                    return;
                }
            }

            var advanced = runSession.Advance(elapsedTicks);
            if (!advanced.IsSuccess)
            {
                RunAdvancementRejected?.Invoke(advanced.ReasonCode);
            }
        }

        /// <summary>
        /// A live frame whose UTC delta exceeds the real (monotonic) delta by more than this is recorded as a Clock <c>forwardJump</c> (T364): the wall
        /// clock moved ahead of the app's own time without crossing the live-gap threshold. Log only; the simulation is not changed.
        /// </summary>
        public const long ForwardJumpMinMs = 5_000L;

        private bool TryHandleLiveGap(long realElapsedTicks)
        {
            if (liveGapMonitor == null || reconcileLiveGap == null)
            {
                return false;
            }

            if (!TryReadUtcMs(out var utcMs))
            {
                // An unreadable clock is not a gap, and the interval it hides cannot be measured: the next readable frame starts a new reference.
                liveGapMonitor.Clear();
                return false;
            }

            LiveGapVerdict verdict = liveGapMonitor.Observe(utcMs, out var previousFrameUtcMs);
            if (verdict != LiveGapVerdict.Gap)
            {
                // Live, or a backwards clock, which credits nothing and does not interrupt the simulation.
                if (verdict == LiveGapVerdict.Live && runSession != null)
                {
                    long ahead = (utcMs - previousFrameUtcMs) - realElapsedTicks / RunSession.TicksPerMs;
                    if (ahead > ForwardJumpMinMs) runSession.Events.OnClock(new RunClockEvent(RunClockKind.ForwardJump, ahead, false));
                }

                return false;
            }

            LiveGapsDetected++;
            var result = reconcileLiveGap(previousFrameUtcMs);
            // T364: the gap reaches the session log as a Clock record after its offline return; reclassified says the return took it.
            runSession?.Events.OnClock(new RunClockEvent(RunClockKind.LiveGap, utcMs - previousFrameUtcMs, result.IsSuccess));
            if (!result.IsSuccess && !string.IsNullOrEmpty(result.ReasonCode))
            {
                LiveGapRejected?.Invoke(result.ReasonCode);
            }

            return true;
        }

        private void ResetLiveGapReference()
        {
            if (liveGapMonitor == null)
            {
                return;
            }

            if (TryReadUtcMs(out var utcMs))
            {
                liveGapMonitor.Reset(utcMs);
            }
            else
            {
                liveGapMonitor.Clear();
            }
        }

        private bool TryReadUtcMs(out long utcMs)
        {
            utcMs = 0L;
            if (liveGapClock == null)
            {
                return false;
            }

            try
            {
                utcMs = liveGapClock.GetUtcNow().ToUniversalTime().ToUnixTimeMilliseconds();
                return true;
            }
            catch (Exception)
            {
                // An unreadable clock is not a gap; the next readable frame starts a new reference.
                return false;
            }
        }

        private static long ScaleAndClamp(long realTicks, ITimeScaleSource source)
        {
            var scale = source.Scale;
            if (double.IsNaN(scale) || scale <= 0d)
            {
                scale = 1d;
            }

            if (scale == 1d)
            {
                // An effective scale of exactly 1 is the real delta, unclamped, like the null source.
                source.ReportEffectiveMultiplier(1d);
                return realTicks;
            }

            const double maxTicks = MaxSimulatedSecondsPerFrame * CoreTicksPerSecond;
            var scaled = realTicks * scale;
            var simulated = scaled >= maxTicks ? (long)maxTicks : (long)scaled;
            source.ReportEffectiveMultiplier((double)simulated / realTicks);
            return simulated;
        }

        private bool TryGetCurrentCumulativeTime(out double cumulativeActiveSeconds, out long cumulativeTicks)
        {
            cumulativeActiveSeconds = 0d;
            cumulativeTicks = 0L;

            double nowSeconds;
            if (!TryReadValidClock(out nowSeconds) || nowSeconds < activeSegmentStartSeconds)
            {
                return false;
            }

            cumulativeActiveSeconds = completedActiveSeconds + (nowSeconds - activeSegmentStartSeconds);
            if (double.IsNaN(cumulativeActiveSeconds) || double.IsInfinity(cumulativeActiveSeconds) ||
                cumulativeActiveSeconds < 0d)
            {
                return false;
            }

            var cumulativeTicksAsDouble = Math.Floor(cumulativeActiveSeconds * CoreTicksPerSecond);
            if (double.IsNaN(cumulativeTicksAsDouble) || double.IsInfinity(cumulativeTicksAsDouble) ||
                cumulativeTicksAsDouble < 0d || cumulativeTicksAsDouble >= LongExclusiveUpperBound)
            {
                return false;
            }

            cumulativeTicks = (long)cumulativeTicksAsDouble;
            return true;
        }

        private bool TryReadValidClock(out double nowSeconds)
        {
            nowSeconds = 0d;
            if (readCumulativeActiveSeconds == null)
            {
                return false;
            }

            nowSeconds = readCumulativeActiveSeconds();
            return !double.IsNaN(nowSeconds) && !double.IsInfinity(nowSeconds) && nowSeconds >= 0d;
        }
    }
}
