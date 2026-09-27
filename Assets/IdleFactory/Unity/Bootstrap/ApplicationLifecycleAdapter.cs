using System;
using IdleFactory.Core.Application;
using UnityEngine;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>How the lifecycle result in <see cref="ApplicationLifecycleAdapter.LastResult"/> was reached.</summary>
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

    /// <summary>Normalizes Unity lifecycle callbacks into durable suspension and return commands.</summary>
    public sealed class ApplicationLifecycleAdapter : MonoBehaviour
    {
        /// <summary>
        /// A screen configuration change observed this close before a focus-only departure belongs to it. On the
        /// Android player the new surface size can be observed one frame before the rotation removes focus (T060).
        /// </summary>
        public const double ConfigurationChangeWindowSeconds = 1.0;

        private PersistenceCommands persistenceCommands;
        private Action prepareForDeparture;
        private Func<LifecycleScreenConfiguration> readScreenConfiguration = () => LifecycleScreenConfiguration.Current;
        private Func<double> readRealtimeSeconds = () => Time.realtimeSinceStartupAsDouble;
        private bool configured;
        private bool startupReconciled;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private bool applicationQuitting;
        private bool departureRecorded;
        private bool departureIncludedPause;
        private bool departureFollowedConfigurationChange;
        private LifecycleScreenConfiguration departureConfiguration;
        private LifecycleScreenConfiguration observedConfiguration;
        private double lastConfigurationChangeSeconds = double.NegativeInfinity;

        public CommandResult<PersistenceOperationResult> LastResult { get; private set; }

        /// <summary>
        /// Classifies <see cref="LastResult"/>. Presentation uses it to withhold Durable offline return feedback for a
        /// return the player never left (FR-036); lifecycle persistence treats every return identically (FR-037).
        /// </summary>
        public LifecycleReturnKind LastReturnKind { get; private set; }

        public event Action<CommandResult<PersistenceOperationResult>> ResultChanged;

        /// <summary>
        /// Binds the application-owned lifecycle commands and resolves the cold-start checkpoint before gameplay.
        /// A blocked result leaves lifecycle admission closed in the underlying session.
        /// </summary>
        public void Configure(PersistenceCommands commands, Action prepareForDeparture = null)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            if (configured)
            {
                throw new InvalidOperationException("The application lifecycle adapter has already been configured.");
            }

            persistenceCommands = commands;
            this.prepareForDeparture = prepareForDeparture;
            observedConfiguration = readScreenConfiguration();
            configured = true;
            TryResume();
        }

        /// <summary>
        /// Replaces the screen and realtime readers; exposed for deterministic lifecycle integration tests.
        /// Call before <see cref="Configure"/>.
        /// </summary>
        public void UseScreenConfigurationSource(Func<LifecycleScreenConfiguration> screenConfiguration, Func<double> realtimeSeconds)
        {
            if (configured)
            {
                throw new InvalidOperationException("The screen configuration source must be set before configuration.");
            }

            readScreenConfiguration = screenConfiguration ?? throw new ArgumentNullException(nameof(screenConfiguration));
            readRealtimeSeconds = realtimeSeconds ?? throw new ArgumentNullException(nameof(realtimeSeconds));
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
                // A pause means the player left the visible game (suspend, lock, background). It also covers a
                // pause that arrives after a focus-only departure has already been recorded.
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
            if (!configured || !startupReconciled || departureRecorded)
            {
                return;
            }

            // The command retains the exact checkpoint ID only when the write succeeds. If it fails,
            // its next matching return applies the contract's explicit FR-037 fallback.
            departureRecorded = true;
            departureIncludedPause = applicationPaused;
            departureConfiguration = readScreenConfiguration();
            departureFollowedConfigurationChange = !departureConfiguration.Equals(observedConfiguration) ||
                readRealtimeSeconds() - lastConfigurationChangeSeconds <= ConfigurationChangeWindowSeconds;
            prepareForDeparture?.Invoke();
            Publish(persistenceCommands.RecordSuspension(), LifecycleReturnKind.None);
        }

        private void TryResume()
        {
            if (!configured || applicationQuitting || applicationPaused || !applicationFocused)
            {
                return;
            }

            if (!startupReconciled)
            {
                startupReconciled = true;
                Publish(persistenceCommands.ResumeAndReconcile(), LifecycleReturnKind.ColdStart);
                return;
            }

            if (!departureRecorded)
            {
                return;
            }

            // Mark the normalized return before calling into application commands so duplicate callbacks
            // cannot reconcile the same in-process return twice, even when reconciliation blocks.
            departureRecorded = false;
            var returnKind = ClassifyReturn();
            Publish(persistenceCommands.ResumeAndReconcile(), returnKind);
        }

        private LifecycleReturnKind ClassifyReturn()
        {
            // T060: on Android a runtime rotation removes window focus without pausing the player, and the surface
            // size changes around that focus loss. A return is configuration-only when no pause was reported and
            // the screen configuration changed just before, during or at the end of the departure.
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

        private void Publish(CommandResult<PersistenceOperationResult> result, LifecycleReturnKind returnKind)
        {
            LastResult = result;
            LastReturnKind = returnKind;
            ResultChanged?.Invoke(result);
        }
    }
}
