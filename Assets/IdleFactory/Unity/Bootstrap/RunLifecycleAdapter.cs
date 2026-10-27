using System;
using IdleFactory.Core.Application;
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
    /// <para>
    /// The platform class (<see cref="LifecyclePlatform"/>) decides what losing focus means (T250). On a phone or tablet focus
    /// loss and pause are a departure as above. On a desktop player and in the editor's Play Mode they are not: pause is ignored,
    /// focus loss neither records a departure nor stops the simulation, and the return reconciles nothing and opens no
    /// summary. Focus loss then only writes a plain safety checkpoint (<see cref="WriteSafetyCheckpoint"/>). A regular
    /// shutdown records the departure on both classes.
    /// </para>
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
        private Action prepareForSafetyCheckpoint;
        private Func<LifecycleScreenConfiguration> readScreenConfiguration = () => LifecycleScreenConfiguration.Current;
        private Func<double> readRealtimeSeconds = () => Time.realtimeSinceStartupAsDouble;
        private bool configured;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private bool applicationQuitting;
        private bool departureRecorded;
        private bool departureIncludedPause;
        private bool departureFollowedConfigurationChange;
        private bool desktopFocused = true;
        private LifecycleScreenConfiguration departureConfiguration;
        private LifecycleScreenConfiguration observedConfiguration;
        private double lastConfigurationChangeSeconds = double.NegativeInfinity;

        public RunCommandResult<RunPersistenceOutcome> LastResult { get; private set; }

        /// <summary>
        /// The result of the latest desktop focus-loss safety checkpoint (T250), or null when none was attempted. It is never
        /// published through <see cref="ResultChanged"/>: it is not a departure, so it carries no return kind and no summary.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> LastSafetyCheckpoint { get; private set; }

        /// <summary>How many desktop focus-loss safety checkpoints were attempted since start-up (T250); a diagnostic for tests.</summary>
        public int SafetyCheckpointAttempts { get; private set; }

        /// <summary>Classifies <see cref="LastResult"/>; presentation withholds the offline summary of a configuration-only return.</summary>
        public LifecycleReturnKind LastReturnKind { get; private set; }

        public event Action<RunCommandResult<RunPersistenceOutcome>, LifecycleReturnKind> ResultChanged;

        /// <summary>
        /// Binds the lifecycle commands. <paramref name="prepareForDeparture"/> runs synchronously before the suspension is
        /// recorded (the host flushes its last active interval); <paramref name="measureResume"/> wraps every return so the
        /// host can log its elapsed time. <paramref name="prepareForSafetyCheckpoint"/> runs before a desktop safety checkpoint
        /// (the host brings the run up to date without stopping, T250).
        /// </summary>
        public void Configure(
            RunPersistence runPersistence,
            Action prepareForDeparture,
            Func<Func<RunCommandResult<RunPersistenceOutcome>>, RunCommandResult<RunPersistenceOutcome>> measureResume = null,
            Action prepareForSafetyCheckpoint = null)
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
            this.prepareForSafetyCheckpoint = prepareForSafetyCheckpoint;
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

        private void OnApplicationPause(bool paused) => Guard(() => ApplyPauseChanged(paused));

        private void ApplyPauseChanged(bool paused)
        {
            if (LifecyclePlatform.IsDesktop)
            {
                // Unity raises pause on a desktop player (and in editor Play Mode) only while Run In Background is off, which the
                // project turns on. A pause that still arrives is not the player leaving, so it is not a departure (T250).
                return;
            }

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
        public void NotifyFocusChanged(bool hasFocus) => Guard(() => ApplyFocusChanged(hasFocus));

        /// <summary>
        /// T306 (SEC-9): a lifecycle callback must never let an exception escape into the engine's callback dispatch. Every
        /// entry point runs through this guard; a failure leaves the run live and the next callback or save starts clean.
        /// </summary>
        private static void Guard(Action callback)
        {
            try
            {
                callback();
            }
            catch (Exception exception)
            {
                LogContained("Run lifecycle callback failed and was contained", exception);
            }
        }

        /// <summary>
        /// Logs a contained failure: the exception type name everywhere, and the full exception with its stack in the editor and
        /// development builds only (T337; a release build never logs a stack or a path). Shared by every lifecycle entry point that
        /// swallows an exception (SEC-9).
        /// </summary>
        internal static void LogContained(string what, Exception exception)
        {
            Debug.LogWarning(what + ": " + exception.GetType().Name);
            if (Debug.isDebugBuild)
            {
                Debug.LogWarning(exception.ToString());
            }
        }

        private void ApplyFocusChanged(bool hasFocus)
        {
            if (LifecyclePlatform.IsDesktop)
            {
                NotifyDesktopFocusChanged(hasFocus);
                return;
            }

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

        private void NotifyDesktopFocusChanged(bool hasFocus)
        {
            if (desktopFocused == hasFocus)
            {
                return;
            }

            desktopFocused = hasFocus;
            if (!hasFocus)
            {
                WriteSafetyCheckpoint();
            }
        }

        /// <summary>
        /// T250: the plain checkpoint a desktop player writes when it loses window focus. It is the periodic-autosave command of
        /// <see cref="RunPersistence"/>, not a suspension: the lifecycle stays <see cref="LifecycleState.Running"/>, the host keeps
        /// simulating, nothing reconciles on regaining focus, and no summary opens. It exists because the run has no timer-driven
        /// autosave (Feature 001 A-005), so without it an unclean exit would roll the factory back to the last departure or start.
        /// The write skips silently while a prestige is pending or the lifecycle is not running, and a failed write changes
        /// nothing but the persistence status. The write is throttled by the minimum interval of
        /// <see cref="RunPersistence.Autosave"/> (T306, FR-033: the outcome then reports
        /// <see cref="RunPersistenceOutcome.AutosaveThrottled"/>); a thrown exception is contained and recorded as a failed
        /// <see cref="LastSafetyCheckpoint"/> (SEC-9). Once the application is quitting, a late focus-lost writes nothing.
        /// </summary>
        public void WriteSafetyCheckpoint()
        {
            if (!configured || applicationQuitting || departureRecorded)
            {
                return;
            }

            SafetyCheckpointAttempts++;
            try
            {
                prepareForSafetyCheckpoint?.Invoke();
                LastSafetyCheckpoint = persistence.Autosave();
            }
            catch (Exception exception)
            {
                LastSafetyCheckpoint = RunCommandResult.Reject<RunPersistenceOutcome>(RunPersistenceReasons.PersistenceFailed);
                LogContained("Run safety checkpoint failed and was contained", exception);
            }
        }

        /// <summary>Applies one normalized pause transition; exposed for deterministic lifecycle integration tests.</summary>
        public void NotifyPauseChanged(bool paused) => OnApplicationPause(paused);

        private void OnApplicationQuit() => Guard(() =>
        {
            if (applicationQuitting)
            {
                return;
            }

            applicationQuitting = true;
            RecordDeparture();
        });

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
            try
            {
                prepareForDeparture?.Invoke();
                Publish(persistence.RecordSuspension(), LifecycleReturnKind.None);
            }
            catch
            {
                // T337 (T306 review finding 2): a departure that threw before the persistence left Running never happened, so the flag is
                // cleared and a later pause or focus loss records it again. Once the lifecycle has left Running (a suspension that
                // did start) the flag stays set, so the matching return still reconciles. The exception goes on to the guard.
                if (persistence.Lifecycle == LifecycleState.Running)
                {
                    departureRecorded = false;
                }

                throw;
            }
        }

        /// <summary>True only while the result of a live-gap reconciliation is being published (T338: the session log flags it).</summary>
        public bool IsLiveGapReturn { get; private set; }

        /// <summary>
        /// T337 (FR-032, F5-R10): the host's live-gap path. A wall delta above the threshold between two live frames is a departure at
        /// the previous frame, <paramref name="previousFrameUtcMs"/>, followed at once by the standard offline return, so the sleep is
        /// credited like a quit and restart (equivalence case (p)). The gap is not a lifecycle departure the player made: no host
        /// preparation runs (the host is the caller and has simulated everything up to the previous frame), the departure flags are
        /// untouched and the return is published as a <see cref="LifecycleReturnKind.Return"/>, so the offline summary opens.
        /// <para>
        /// The resume follows whenever the suspension entered the suspended state, even if its checkpoint could not be written or a
        /// pending prestige was not durable (the return then uses the FR-037 fallback, exactly as after a failed departure). If the
        /// suspension was refused before it left Running (the run is not live, or the application is quitting), nothing happened and
        /// the refusal is returned without publishing. An exception is contained (SEC-9); if it escaped after the suspension began,
        /// the departure flag is set, so the next pause or focus return reconciles instead of leaving the run suspended.
        /// </para>
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> ReconcileLiveGap(long previousFrameUtcMs)
        {
            if (!configured || applicationQuitting || departureRecorded)
            {
                return RunCommandResult.Reject<RunPersistenceOutcome>(RunPersistenceReasons.LifecycleNoOp);
            }

            try
            {
                var suspension = persistence.RecordSuspension(previousFrameUtcMs);
                if (persistence.Lifecycle != LifecycleState.Suspended)
                {
                    return suspension;
                }

                var result = measureResume != null
                    ? measureResume(() => persistence.ResumeAndReconcile())
                    : persistence.ResumeAndReconcile();
                IsLiveGapReturn = true; // T338: lets the session log tell this return from a pause or focus return while subscribers run
                try { Publish(result, LifecycleReturnKind.Return); }
                finally { IsLiveGapReturn = false; }
                return result;
            }
            catch (Exception exception)
            {
                if (persistence.Lifecycle != LifecycleState.Running)
                {
                    departureRecorded = true;
                    departureIncludedPause = false;
                }

                LogContained("Run live-gap reconciliation failed and was contained", exception);
                return RunCommandResult.Reject<RunPersistenceOutcome>(RunPersistenceReasons.PersistenceFailed);
            }
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
