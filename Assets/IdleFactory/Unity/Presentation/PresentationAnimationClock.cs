using System;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// The one shared presentation clock of Feature 004 (contracts/presentation-and-state-cues.md "Animation", research R05, FR-019,
    /// FR-020, FR-026). It keeps presentation time in integer milliseconds of unscaled time, paused while the application is suspended,
    /// and hands that time plus the effective reduced-motion state to <see cref="SlotGridView.AdvanceFrames"/> once per update. The view
    /// owns the per-slot step (<c>frame = floor(t * fps) mod frameCount</c>, only running machines animate, the <c>F004-ACCENT-*</c>
    /// layer follows its body), so there is no per-machine update, no <c>Animator</c> and no allocation in <see cref="Tick"/>.
    /// The clock is pure C#: the caller supplies the unscaled time, which keeps it testable below the engine boundary.
    /// </summary>
    public sealed class PresentationAnimationClock
    {
        private long presentationMilliseconds;
        private long lastUnscaledMilliseconds;
        private bool hasReference;
        private bool suspended;

        /// <summary>Presentation time in integer milliseconds: unscaled time that elapsed while the application was not suspended.</summary>
        public long Milliseconds => presentationMilliseconds;

        /// <summary>True while the application is suspended; presentation time does not advance and the frames stay where they are.</summary>
        public bool Suspended => suspended;

        /// <summary>The effective reduced-motion state: when true every animated entry shows frame 0 from the next update (FR-020).</summary>
        public bool ReducedMotion { get; private set; }

        /// <summary>
        /// Advances presentation time to <paramref name="unscaledMilliseconds"/> (integer milliseconds of unscaled time from any fixed
        /// origin, for example <c>Time.unscaledTimeAsDouble * 1000</c>). The first call, and the first call after a resume, only takes a
        /// new reference, so the time spent suspended never counts. A reading that goes backwards is ignored.
        /// </summary>
        public long Tick(long unscaledMilliseconds)
        {
            if (!hasReference)
            {
                hasReference = true;
                lastUnscaledMilliseconds = unscaledMilliseconds;
                return presentationMilliseconds;
            }

            var delta = unscaledMilliseconds - lastUnscaledMilliseconds;
            lastUnscaledMilliseconds = unscaledMilliseconds;
            if (!suspended && delta > 0)
            {
                presentationMilliseconds += delta;
            }

            return presentationMilliseconds;
        }

        /// <summary>Pauses or resumes presentation time. Resuming drops the old reference so the suspended interval is not added.</summary>
        public void SetSuspended(bool value)
        {
            if (suspended == value)
            {
                return;
            }

            suspended = value;
            if (!value)
            {
                hasReference = false;
            }
        }

        /// <summary>Sets the effective reduced-motion state directly (see <see cref="Refresh"/> for the resolver path).</summary>
        public void SetReducedMotion(bool reduced) => ReducedMotion = reduced;

        /// <summary>
        /// Resolves the effective reduced-motion state with <see cref="ReducedMotionResolver"/> (preference, else the OS signal) and keeps
        /// it. The OS signal can cost a native call, so callers refresh on start-up, on focus gain and after a preference change, not per
        /// frame.
        /// </summary>
        public EffectiveReducedMotion Refresh(ReducedMotionPreference preference, IReducedMotionSignal signal)
        {
            var effective = ReducedMotionResolver.Resolve(preference, signal);
            ReducedMotion = effective.Reduced;
            return effective;
        }

        /// <summary>One update: ticks the clock and advances every animated entry of the view in a single call. Allocation-free.</summary>
        public void Drive(long unscaledMilliseconds, SlotGridView view)
        {
            var now = Tick(unscaledMilliseconds);
            if (view != null)
            {
                view.AdvanceFrames(now, ReducedMotion);
            }
        }

        /// <summary><c>floor(t * fps) mod frameCount</c> for integer milliseconds; 0 for a still entry or a non-positive rate.</summary>
        public static int FrameAt(long milliseconds, float framesPerSecond, int frameCount) =>
            SlotGridView.FrameAt(milliseconds, framesPerSecond, frameCount);
    }
}
