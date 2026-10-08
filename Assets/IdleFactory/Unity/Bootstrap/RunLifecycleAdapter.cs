using System;
using IdleFactory.Run.Application;
using UnityEngine;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>How the lifecycle result in <see cref="RunLifecycleAdapter.LastResult"/> was reached.</summary>
    public enum LifecycleReturnKind
    {
        /// <summary>No lifecycle return has been published yet, or the latest result was a departure.</summary>
        None,

        /// <summary>The cold-start reconciliation of the active checkpoint.</summary>
        ColdStart,

        /// <summary>An in-process return after the player left, for example suspend, lock or background.</summary>
        Return,

        /// <summary>
        /// An in-process return caused only by a screen configuration change the player did not leave, such as a
        /// runtime rotation (FR-036). The checkpoint and offline accounting are unchanged; only feedback differs.
        /// </summary>
        ConfigurationChange,
    }

    /// <summary>The screen properties that a runtime rotation or other display configuration change alters.</summary>
    public readonly struct LifecycleScreenConfiguration : IEquatable<LifecycleScreenConfiguration>
    {
        public LifecycleScreenConfiguration(int width, int height, ScreenOrientation orientation)
        {
            Width = width;
            Height = height;
            Orientation = orientation;
        }

        public int Width { get; }
        public int Height { get; }
        public ScreenOrientation Orientation { get; }

        public static LifecycleScreenConfiguration Current =>
            new LifecycleScreenConfiguration(Screen.width, Screen.height, Screen.orientation);

        public bool Equals(LifecycleScreenConfiguration other) =>
            Width == other.Width && Height == other.Height && Orientation == other.Orientation;

        public override bool Equals(object obj) => obj is LifecycleScreenConfiguration other && Equals(other);

        public override int GetHashCode() => (Width * 397) ^ (Height * 31) ^ (int)Orientation;
    }

    /// <summary>
    /// Normalizes Unity focus, pause and quit callbacks into the Save V2 suspension and return commands of
    /// <see cref="RunPersistence"/>. It classifies a return like the retired Feature 001 lifecycle adapter did (a focus-only
    /// departure that coincides with a screen configuration change is a <see cref="LifecycleReturnKind.ConfigurationChange"/>,
    /// FR-036): the checkpoint and the exactly-once offline accounting are unchanged, only the feedback differs. The cold start is not performed here; the bootstrap runs
    /// <see cref="RunPersistence.ColdStart"/> before it configures this adapter.
    /// </summary>
    public sealed class RunLifecycleAdapter : MonoBehaviour
    {
        /// <summary>
        /// A screen configuration change observed this close before a focus-only departure belongs to it. On the
        /// Android player the new surface size can be observed one frame before the rotation removes focus (T060).
        /// </summary>
        public const double ConfigurationChangeWindowSeconds = 1.0;

        private RunPersistence persistence;
        private Action prepareForDeparture;
        private Func<Func<RunCommandResult<RunPersistenceOutcome>>, RunCommandResult<RunPersistenceOutcome>> measureResume;
        private Func<LifecycleScreenConfiguration> readScreenConfiguration = () => LifecycleScreenConfiguration.Current;
        private Func<double> readRealtimeSeconds = () => Time.realtimeSinceStartupAsDouble;
        private bool configured;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private bool applicationQuitting;
        private bool departureRecorded;
        private bool departureIncludedPause;
        private bool departureFollowedConfigurationChange;
        private LifecycleScreenConfiguration departureConfiguration;
        private LifecycleScreenConfiguration observedConfiguration;
        private double lastConfigurationChangeSeconds = double.NegativeInfinity;

        public RunCommandResult<RunPersistenceOutcome> LastResult { get; private set; }

        /// <summary>Classifies <see cref="LastResult"/>; presentation withholds the offline summary of a configuration-only return.</summary>
        public LifecycleReturnKind LastReturnKind { get; private set; }

        public event Action<RunCommandResult<RunPersistenceOutcome>, LifecycleReturnKind> ResultChanged;

        /// <summary>
        /// Binds the lifecycle commands. <paramref name="prepareForDeparture"/> runs synchronously before the suspension is
        /// recorded (the host flushes its last active interval); <paramref name="measureResume"/> wraps every return so the
        /// host can log its elapsed time.
        /// </summary>
        public void Configure(
            RunPersistence runPersistence,
            Action prepareForDeparture,
            Func<Func<RunCommandResult<RunPersistenceOutcome>>, RunCommandResult<RunPersistenceOutcome>> measureResume = null)
        {
            if (runPersistence == null)
            {
                throw new ArgumentNullException(nameof(runPersistence));
            }

            if (configured)
            {
                throw new InvalidOperationException("The run lifecycle adapter has already been configured.");
            }

            persistence = runPersistence;
            this.prepareForDeparture = prepareForDeparture;
            this.measureResume = measureResume;
            observedConfiguration = readScreenConfiguration();
            configured = true;
        }

        /// <summary>
        /// Replaces the screen and realtime readers for deterministic tests. The bootstrap configures this adapter during
        /// start-up, so the readers may be replaced afterwards; the observed configuration
        /// is then re-read from the new source.
        /// </summary>
        public void UseScreenConfigurationSource(Func<LifecycleScreenConfiguration> screenConfiguration, Func<double> realtimeSeconds)
        {
            readScreenConfiguration = screenConfiguration ?? throw new ArgumentNullException(nameof(screenConfiguration));
            readRealtimeSeconds = realtimeSeconds ?? throw new ArgumentNullException(nameof(realtimeSeconds));
            observedConfiguration = readScreenConfiguration();
            lastConfigurationChangeSeconds = double.NegativeInfinity;
        }

        private void Update()
        {
            ObserveScreenConfiguration();
        }

        /// <summary>Records the screen configuration the running frame used; exposed for lifecycle integration tests.</summary>
        public void ObserveScreenConfiguration()
        {
            if (!configured)
            {
                return;
            }

            var current = readScreenConfiguration();
            if (current.Equals(observedConfiguration))
            {
                return;
            }

            observedConfiguration = current;
            lastConfigurationChangeSeconds = readRealtimeSeconds();
        }

        private void OnApplicationPause(bool paused)
        {
            if (applicationPaused == paused)
            {
                if (!paused)
                {
                    TryResume();
                }

                return;
            }

            applicationPaused = paused;
            if (paused)
            {
                departureIncludedPause = true;
                RecordDeparture();
                return;
            }

            TryResume();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            NotifyFocusChanged(hasFocus);
        }

        /// <summary>Applies one normalized focus transition; exposed for deterministic lifecycle integration tests.</summary>
        public void NotifyFocusChanged(bool hasFocus)
        {
            if (applicationFocused == hasFocus)
            {
                if (hasFocus)
                {
                    TryResume();
                }

                return;
            }

            applicationFocused = hasFocus;
            if (!hasFocus)
            {
                RecordDeparture();
                return;
            }

            TryResume();
        }

        /// <summary>Applies one normalized pause transition; exposed for deterministic lifecycle integration tests.</summary>
        public void NotifyPauseChanged(bool paused) => OnApplicationPause(paused);

        private void OnApplicationQuit()
        {
            if (applicationQuitting)
            {
                return;
            }

            applicationQuitting = true;
            RecordDeparture();
        }

        private void RecordDeparture()
        {
            if (!configured || departureRecorded)
            {
                return;
            }

            // The command retains the exact checkpoint ID only when the write succeeds; if it fails, the next
            // matching return applies the contract's explicit fallback.
            departureRecorded = true;
            departureIncludedPause = applicationPaused;
            departureConfiguration = readScreenConfiguration();
            departureFollowedConfigurationChange = !departureConfiguration.Equals(observedConfiguration) ||
                readRealtimeSeconds() - lastConfigurationChangeSeconds <= ConfigurationChangeWindowSeconds;
            prepareForDeparture?.Invoke();
            Publish(persistence.RecordSuspension(), LifecycleReturnKind.None);
        }

        private void TryResume()
        {
            if (!configured || applicationQuitting || applicationPaused || !applicationFocused || !departureRecorded)
            {
                return;
            }

            // Mark the normalized return before calling into the application so duplicate callbacks cannot
            // reconcile the same in-process return twice, even when the reconciliation blocks.
            departureRecorded = false;
            var returnKind = ClassifyReturn();
            var result = measureResume != null
                ? measureResume(() => persistence.ResumeAndReconcile())
                : persistence.ResumeAndReconcile();
            Publish(result, returnKind);
        }

        private LifecycleReturnKind ClassifyReturn()
        {
            if (departureIncludedPause)
            {
                return LifecycleReturnKind.Return;
            }

            var returnConfiguration = readScreenConfiguration();
            var configurationChanged = departureFollowedConfigurationChange ||
                !returnConfiguration.Equals(departureConfiguration) ||
                !returnConfiguration.Equals(observedConfiguration);
            return configurationChanged ? LifecycleReturnKind.ConfigurationChange : LifecycleReturnKind.Return;
        }

        private void Publish(RunCommandResult<RunPersistenceOutcome> result, LifecycleReturnKind returnKind)
        {
            LastResult = result;
            LastReturnKind = returnKind;
            ResultChanged?.Invoke(result, returnKind);
        }
    }
}
