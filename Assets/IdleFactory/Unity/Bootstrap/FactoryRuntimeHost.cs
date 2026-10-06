using System;
using System.Diagnostics;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>
    /// Advances the shared factory session from one cumulative active-time source.
    /// </summary>
    public sealed class FactoryRuntimeHost : MonoBehaviour
    {
        private const double CoreTicksPerSecond = TimeSpan.TicksPerSecond;
        private const double LongExclusiveUpperBound = 9223372036854775808d;

        private ProductionCommands commands;
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

        /// <summary>
        /// Raised when the application rejects active-time advancement, so the composition root can surface
        /// lifecycle or simulation failures without placing domain decisions in this Unity adapter.
        /// </summary>
        public event Action<CommandOutcome, string> AdvancementRejected;

        /// <summary>Raised when the run session rejects an advance, with its stable reason code (for example <c>value.limit</c>).</summary>
        public event Action<string> RunAdvancementRejected;

        /// <summary>The run session this host advances, or null while the legacy production path is configured.</summary>
        public RunSession Run => runSession;

        /// <summary>Whether Unity lifecycle state currently permits active-time advancement.</summary>
        public bool IsApplicationLifecycleAvailable => applicationFocused && !applicationPaused && !applicationQuitting;

        /// <summary>Configures this host to sample Unity's monotonic real-time clock.</summary>
        public void Configure(ProductionCommands productionCommands)
        {
            Configure(productionCommands, ReadUnityRealtimeSeconds);
        }

        /// <summary>
        /// Configures the host with a monotonic time source measured in seconds. Only intervals while this
        /// host is active are accumulated, and the overload lets runtime tests supply a deterministic clock.
        /// </summary>
        public void Configure(
            ProductionCommands productionCommands,
            Func<double> cumulativeActiveTimeSource)
        {
            if (productionCommands == null)
            {
                throw new ArgumentNullException(nameof(productionCommands));
            }

            if (cumulativeActiveTimeSource == null)
            {
                throw new ArgumentNullException(nameof(cumulativeActiveTimeSource));
            }

            if (commands != null)
            {
                throw new InvalidOperationException("The factory runtime host has already been configured.");
            }

            commands = productionCommands;
            readCumulativeActiveSeconds = cumulativeActiveTimeSource;
            completedActiveSeconds = 0d;
            accountedCumulativeTicks = 0L;
            StartActiveSegmentIfAvailable();
        }

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

            if (commands != null || runSession != null)
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
            StopActiveSegment();
            applicationQuitting = true;
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
        /// Synchronizes host availability after the adapter has successfully reconciled a return.
        /// The inspector remains responsible for enabling the host only when a machine can advance.
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
            if ((commands == null && runSession == null) || trackingActiveSegment || applicationPaused || !applicationFocused ||
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
                AdvanceToCumulativeTicks(currentCumulativeTicks);
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

            AdvanceToCumulativeTicks(currentCumulativeTicks);
        }

        private void AdvanceToCumulativeTicks(long cumulativeTicks)
        {
            if (cumulativeTicks <= accountedCumulativeTicks)
            {
                return;
            }

            var elapsedTicks = cumulativeTicks - accountedCumulativeTicks;
            // Account each observed interval once, including a rejected attempt. A blocked lifecycle interval
            // must not be replayed later as a production catch-up.
            accountedCumulativeTicks = cumulativeTicks;

            if (runSession != null)
            {
                if (!RunNeedsAdvance(runSession, ref machineViews))
                {
                    return;
                }

                var advanced = runSession.Advance(elapsedTicks);
                if (!advanced.IsSuccess)
                {
                    RunAdvancementRejected?.Invoke(advanced.ReasonCode);
                }

                return;
            }

            var result = commands.AdvanceActiveTime(elapsedTicks);
            if (!result.Succeeded)
            {
                AdvancementRejected?.Invoke(result.Outcome, result.Message);
            }
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

        private static double ReadUnityRealtimeSeconds() => Time.realtimeSinceStartupAsDouble;
    }
}
