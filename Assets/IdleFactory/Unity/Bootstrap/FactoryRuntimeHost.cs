using System;
using IdleFactory.Core.Application;
using UnityEngine;

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

        private void StartActiveSegmentIfAvailable()
        {
            if (commands == null || trackingActiveSegment || applicationPaused || !applicationFocused ||
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

            var result = commands.AdvanceSimulation(elapsedTicks);
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
