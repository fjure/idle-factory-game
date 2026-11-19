using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>The milestones the world acknowledges (FR-035, research R14).</summary>
    public enum MilestoneKind
    {
        /// <summary>The factory level rose; it may open locations (the slot unlock).</summary>
        FactoryLevelUp = 0,

        /// <summary>A prestige completed: a new run began.</summary>
        PrestigeCompleted = 1,
    }

    /// <summary>
    /// One milestone to acknowledge (data-model.md <c>PendingMilestone</c>). It carries what the banner and the world need and nothing that is a reward:
    /// FR-035 forbids invented rewards, so no amount lives here.
    /// </summary>
    public readonly struct PendingMilestone : IEquatable<PendingMilestone>
    {
        public PendingMilestone(MilestoneKind kind, int level, int firstNewLocation, int newlyUnlockedCount, bool stageRaised, int stage, bool arisenOffline)
        {
            Kind = kind;
            Level = level;
            FirstNewLocation = firstNewLocation;
            NewlyUnlockedCount = newlyUnlockedCount;
            StageRaised = stageRaised;
            Stage = stage;
            ArisenOffline = arisenOffline;
        }

        public MilestoneKind Kind { get; }

        /// <summary>The factory level reached (the new level after a level-up, the level the new run starts at after a prestige).</summary>
        public int Level { get; }

        /// <summary>The first location that opened, or -1 when none did.</summary>
        public int FirstNewLocation { get; }

        /// <summary>How many locations opened (they are the consecutive ids from <see cref="FirstNewLocation"/>).</summary>
        public int NewlyUnlockedCount { get; }

        /// <summary>The level-up moved the factory into a later development stage (the deck extends).</summary>
        public bool StageRaised { get; }

        /// <summary>The development stage after the milestone.</summary>
        public int Stage { get; }

        /// <summary>The milestone arose during an offline return: it is listed in the offline summary only and never raises a banner (R14).</summary>
        public bool ArisenOffline { get; }

        public bool Equals(PendingMilestone other) =>
            Kind == other.Kind && Level == other.Level && FirstNewLocation == other.FirstNewLocation && NewlyUnlockedCount == other.NewlyUnlockedCount &&
            StageRaised == other.StageRaised && Stage == other.Stage && ArisenOffline == other.ArisenOffline;

        public override bool Equals(object obj) => obj is PendingMilestone other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 31 + Level) * 31 + NewlyUnlockedCount;

        public override string ToString() => Kind + " level " + Level + ", +" + NewlyUnlockedCount + " locations" + (StageRaised ? ", stage raised" : string.Empty) + (ArisenOffline ? " (offline)" : string.Empty);
    }

    /// <summary>
    /// Detects the milestones by diffing the run's factory state between two observations (T540, FR-035, R14): a factory-level increase within the same run
    /// (with the newly opened locations) and a prestige completion (a run-id change together with a higher prestige count). The first observation, and any
    /// observation that cannot be compared, only sets the baseline and raises nothing, so a start, a restore or a recovery never shows a banner.
    /// <para>
    /// The watcher reads values, never a session: it is plain C# and Edit Mode tests drive it directly. <see cref="Observe(int,int,int,Guid,long)"/> allocates
    /// nothing in the steady state; only an actual milestone enqueues an entry. Milestones that arose offline come from <see cref="ReconcileOffline"/> and are
    /// returned to the caller (the offline summary), never queued for the banner. The prestige count only classifies a run-id change as a prestige: it is
    /// <b>not</b> an input to the development stage (<see cref="DevelopmentStage"/>).
    /// </para>
    /// </summary>
    public sealed class MilestoneWatcher
    {
        /// <summary>The queue holds at most this many milestones; the oldest is dropped, so a burst never grows it.</summary>
        public const int MaxQueued = 4;

        private readonly List<PendingMilestone> queue = new List<PendingMilestone>(MaxQueued + 1);
        private bool hasBaseline;
        private Guid runId;
        private int level;
        private int unlocked;
        private long prestigeCount;

        /// <summary>The number of milestones waiting for the banner.</summary>
        public int PendingCount => queue.Count;

        /// <summary>The total number of milestones detected so far (live and offline), for tests.</summary>
        public int DetectedTotal { get; private set; }

        /// <summary>The prestige count of the baseline (negative when unknown or before the first observation).</summary>
        public long BaselinePrestigeCount => hasBaseline ? prestigeCount : -1;

        /// <summary>
        /// Forgets the baseline and the queue (a re-baseline): the next observation only sets the baseline. A load, a restore and a cold start call it, so a state that
        /// merely arrived with a higher factory level, or another run, is never taken for a milestone.
        /// </summary>
        public void Reset()
        {
            hasBaseline = false;
            queue.Clear();
        }

        /// <summary>Observes a factory snapshot of the run <paramref name="currentRunId"/>; <paramref name="currentPrestigeCount"/> is the run's prestige count (a negative value means unknown).</summary>
        public void Observe(RunFactorySnapshot snapshot, Guid currentRunId, long currentPrestigeCount = -1, int baseUnlocked = 0)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Observe(snapshot.FactoryLevel, snapshot.SlotCount, snapshot.Slots.Count, currentRunId, currentPrestigeCount, baseUnlocked);
        }

        /// <summary>Observes the raw values: the factory level, the unlocked locations, the locations at the maximum level, the run id and the prestige count.</summary>
        public void Observe(int factoryLevel, int unlockedLocations, int totalLocations, Guid currentRunId, long currentPrestigeCount = -1, int baseUnlocked = 0)
        {
            if (!Detect(factoryLevel, unlockedLocations, totalLocations, currentRunId, currentPrestigeCount, baseUnlocked, false, out var milestone))
            {
                return;
            }

            queue.Add(milestone);
            DetectedTotal++;
            if (queue.Count > MaxQueued)
            {
                queue.RemoveAt(0);
            }
        }

        /// <summary>Takes the oldest waiting milestone, or false when none waits. Allocation-free.</summary>
        public bool TryDequeue(out PendingMilestone milestone)
        {
            if (queue.Count == 0)
            {
                milestone = default;
                return false;
            }

            milestone = queue[0];
            queue.RemoveAt(0);
            return true;
        }

        /// <summary>
        /// The state changed because the player was away (an offline return): the milestone it caused, flagged <see cref="PendingMilestone.ArisenOffline"/>, is returned
        /// to the caller for the offline summary and is never queued for the banner. The baseline moves to the returned state either way.
        /// </summary>
        public IReadOnlyList<PendingMilestone> ReconcileOffline(int factoryLevel, int unlockedLocations, int totalLocations, Guid currentRunId, long currentPrestigeCount = -1, int baseUnlocked = 0)
        {
            if (!Detect(factoryLevel, unlockedLocations, totalLocations, currentRunId, currentPrestigeCount, baseUnlocked, true, out var milestone))
            {
                return Array.Empty<PendingMilestone>();
            }

            DetectedTotal++;
            return new[] { milestone };
        }

        /// <summary>The snapshot form of <see cref="ReconcileOffline(int,int,int,Guid,long)"/>.</summary>
        public IReadOnlyList<PendingMilestone> ReconcileOffline(RunFactorySnapshot snapshot, Guid currentRunId, long currentPrestigeCount = -1, int baseUnlocked = 0)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return ReconcileOffline(snapshot.FactoryLevel, snapshot.SlotCount, snapshot.Slots.Count, currentRunId, currentPrestigeCount, baseUnlocked);
        }

        private bool Detect(int factoryLevel, int unlockedLocations, int totalLocations, Guid currentRunId, long currentPrestigeCount, int baseUnlocked, bool offline, out PendingMilestone milestone)
        {
            milestone = default;
            var previousLevel = level;
            var previousUnlocked = unlocked;
            var previousRun = runId;
            var previousPrestige = prestigeCount;
            var hadBaseline = hasBaseline;
            hasBaseline = true;
            runId = currentRunId;
            level = factoryLevel;
            unlocked = unlockedLocations;
            prestigeCount = currentPrestigeCount;
            if (!hadBaseline)
            {
                return false;
            }

            var stage = DevelopmentStage.StageOf(unlockedLocations, totalLocations, baseUnlocked);
            if (currentRunId != previousRun)
            {
                // A run-id change is a prestige only when the prestige count says so (a restore or a recovery that swaps the run keeps it). An unknown count (a
                // negative value on either side) cannot tell, so the run-id change alone counts.
                var prestiged = currentPrestigeCount < 0 || previousPrestige < 0 || currentPrestigeCount > previousPrestige;
                if (!prestiged)
                {
                    return false;
                }

                milestone = new PendingMilestone(MilestoneKind.PrestigeCompleted, factoryLevel, -1, 0, false, stage, offline);
                return true;
            }

            if (factoryLevel <= previousLevel)
            {
                return false;
            }

            var opened = Math.Max(0, unlockedLocations - previousUnlocked);
            var raised = stage > DevelopmentStage.StageOf(previousUnlocked, totalLocations, baseUnlocked);
            milestone = new PendingMilestone(MilestoneKind.FactoryLevelUp, factoryLevel, opened > 0 ? previousUnlocked : -1, opened, raised, stage, offline);
            return true;
        }
    }
}
