using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>How fast a machine's activity loop plays (F006 R08, R12). It is derived from the cycle time and never saved.</summary>
    public enum ActivityPace
    {
        /// <summary>A cycle of 4 s or longer: 6 fps, 800 ms per lane cell.</summary>
        Slow = 0,

        /// <summary>A cycle of 1 s up to (not including) 4 s: 8 fps, 600 ms per lane cell.</summary>
        Normal = 1,

        /// <summary>A cycle under 1 s: 10 fps, 400 ms per lane cell.</summary>
        Fast = 2,
    }

    /// <summary>
    /// What the world draws for one building location (F006 data-model "MachinePresentation", contracts/presentation-model.md section 2).
    /// A plain value: the model overwrites its buffer in place on a refresh, so copy a value to keep it.
    /// </summary>
    public readonly struct MachinePresentation
    {
        public MachinePresentation(
            BuildingLocationId location, Guid instanceId, string machineId, int machineTypeIndex, int level, MachineVisualState visualState,
            bool upgradeAvailable, ActivityPace pace, int activityFps, int completionsDelta, string limitingResourceId)
        {
            Location = location;
            IsOccupied = true;
            InstanceId = instanceId;
            MachineId = machineId;
            MachineTypeIndex = machineTypeIndex;
            Level = level;
            VisualState = visualState;
            UpgradeAvailable = upgradeAvailable;
            Pace = pace;
            ActivityFps = activityFps;
            CompletionsDelta = completionsDelta;
            LimitingResourceId = limitingResourceId;
        }

        public MachinePresentation(BuildingLocationId location)
        {
            Location = location;
            IsOccupied = false;
            InstanceId = default;
            MachineId = null;
            MachineTypeIndex = -1;
            Level = 0;
            VisualState = MachineVisualState.Inactive;
            UpgradeAvailable = false;
            Pace = ActivityPace.Normal;
            ActivityFps = 0;
            CompletionsDelta = 0;
            LimitingResourceId = null;
        }

        public BuildingLocationId Location { get; }

        /// <summary>False for an empty, locked or unknown location: every other value is then the default and nothing is drawn.</summary>
        public bool IsOccupied { get; }

        public Guid InstanceId { get; }
        public string MachineId { get; }
        public int MachineTypeIndex { get; }
        public int Level { get; }

        /// <summary>From <see cref="SlotGridModel.StateFor"/>, the single precedence rule (Art Bible 6.2). There is no second rule here.</summary>
        public MachineVisualState VisualState { get; }

        /// <summary><c>NextUpgrade.Available &amp;&amp; NextUpgrade.Affordable</c> (FR-027).</summary>
        public bool UpgradeAvailable { get; }

        /// <summary>
        /// True when the upgrade marker is drawn: an upgrade is available and the machine shows no problem badge (missing input, storage full or
        /// waiting, Art Bible 6.2). A paused or inactive machine shows a neutral badge, which does not hide the marker.
        /// </summary>
        public bool UpgradeMarkerVisible => UpgradeAvailable && !MachinePresentationModel.IsProblemState(VisualState);

        public ActivityPace Pace { get; }

        /// <summary>The loop rate of <see cref="Pace"/> (6, 8 or 10 fps) after the per-family flash cap. It applies from the next frame 0 (R12).</summary>
        public int ActivityFps { get; }

        /// <summary>
        /// The completed cycles since the previous refresh, capped for display at <see cref="MachinePresentationModel.MaxDisplayedCompletionsDelta"/>.
        /// 0 on a baseline reset (first refresh, restore, offline return, a decrease, a replaced machine).
        /// </summary>
        public int CompletionsDelta { get; }

        /// <summary>The resource a missing-input machine is short of (its first short input), otherwise null.</summary>
        public string LimitingResourceId { get; }
    }

    /// <summary>
    /// Derives the per-location machine presentation from the run read models (F006 T532, R08). A plain C# model: no MonoBehaviour, no scene,
    /// deterministic, and allocation-free on a refresh once its buffers are sized (they are sized from the layout's location count).
    /// <para>
    /// State ownership: the model owns only its output buffer and a per-location completed-cycles baseline. It reads the session's snapshots and
    /// <see cref="MachineStateView"/> rows and never writes to them. Nothing here is saved, and no economy, offline or prestige code reads it, so
    /// the save format is unchanged and an observer changes no result (the baseline is derived data).
    /// </para>
    /// <para>
    /// Caller contract: refresh only when <see cref="NeedsRefresh"/> says so (the snapshots are built on demand and allocate; the model's own
    /// <see cref="Refresh"/> does not). Call <see cref="ResetBaseline"/> when the session is replaced (restore, prestige, new run), because the
    /// revisions of a new session may repeat old values. Pass <c>suppressDeltas</c> on the refresh that follows an offline return.
    /// </para>
    /// </summary>
    public sealed class MachinePresentationModel
    {
        /// <summary>Ticks of 100 ns per second, as in <see cref="RunSession"/>.</summary>
        public const long TicksPerSecond = RunSession.TicksPerMs * 1000L;

        /// <summary>A cycle at or above this many ticks (4 s) is <see cref="ActivityPace.Slow"/>.</summary>
        public const long SlowCycleTicks = 4 * TicksPerSecond;

        /// <summary>A cycle below this many ticks (1 s) is <see cref="ActivityPace.Fast"/>.</summary>
        public const long FastCycleTicks = 1 * TicksPerSecond;

        public const int SlowFps = 6;
        public const int NormalFps = 8;
        public const int FastFps = 10;

        /// <summary>
        /// The most completions one refresh reports for a location. A larger counter difference is a burst the world cannot show anyway
        /// (the effect pool has 3 active renderers), so the display value is capped; the baseline still advances by the full difference.
        /// </summary>
        public const int MaxDisplayedCompletionsDelta = 3;

        private static readonly IReadOnlyDictionary<string, int> NoCaps = new Dictionary<string, int>(0);

        private static readonly IReadOnlyDictionary<string, int> ShippedCaps = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            // The Assembler loop already flashes 3 times per second at its measured rate (Art Bible 6.4), so it never runs above 8 fps (R12).
            { "machine.assembler", NormalFps },
        };

        private readonly IReadOnlyDictionary<string, int> familyFpsCaps;
        private MachinePresentation[] buffer = Array.Empty<MachinePresentation>();
        private long[] baseline = Array.Empty<long>();
        private Guid[] baselineInstance = Array.Empty<Guid>();
        private bool[] hasBaseline = Array.Empty<bool>();
        private int locationCount;
        private bool hasRevision;
        private long structureRevision;
        private long quantityRevision;
        private int lastLocationCount = -1;

        /// <param name="familyFpsCaps">Machine id to the highest loop rate of that family; null uses <see cref="ShippedFamilyFpsCaps"/>.</param>
        public MachinePresentationModel(IReadOnlyDictionary<string, int> familyFpsCaps = null)
        {
            this.familyFpsCaps = familyFpsCaps ?? ShippedCaps;
        }

        /// <summary>The per-family loop-rate caps that ship today (the Assembler at 8 fps). The art pass T522 may extend the table.</summary>
        public static IReadOnlyDictionary<string, int> ShippedFamilyFpsCaps => ShippedCaps;

        /// <summary>The model without any family cap, for tests.</summary>
        public static IReadOnlyDictionary<string, int> NoFamilyFpsCaps => NoCaps;

        /// <summary>The number of locations of the last refresh; the buffer holds one <see cref="MachinePresentation"/> per location id.</summary>
        public int LocationCount => locationCount;

        /// <summary>The presentation of location <paramref name="index"/> (the live buffer: the next refresh overwrites it in place).</summary>
        public MachinePresentation this[int index] => index >= 0 && index < locationCount ? buffer[index] : new MachinePresentation(new BuildingLocationId(index));

        /// <summary>The presentation of one location; an unknown id returns an unoccupied value.</summary>
        public MachinePresentation Get(BuildingLocationId location) =>
            location.Value >= 0 && location.Value < locationCount ? buffer[location.Value] : new MachinePresentation(location);

        /// <summary>The cycle time bucket (R08): 4 s or longer is Slow, 1 s up to 4 s Normal, under 1 s Fast. A missing cycle time is Normal.</summary>
        public static ActivityPace PaceOf(long cycleTicks)
        {
            if (cycleTicks <= 0)
            {
                return ActivityPace.Normal;
            }

            if (cycleTicks >= SlowCycleTicks)
            {
                return ActivityPace.Slow;
            }

            return cycleTicks >= FastCycleTicks ? ActivityPace.Normal : ActivityPace.Fast;
        }

        /// <summary>The loop rate of a pace before any family cap.</summary>
        public static int FpsOf(ActivityPace pace)
        {
            switch (pace)
            {
                case ActivityPace.Slow: return SlowFps;
                case ActivityPace.Fast: return FastFps;
                default: return NormalFps;
            }
        }

        /// <summary>The cell travel time of a lane pip by pace (contracts/world-presentation.md): 800, 600 or 400 ms.</summary>
        public static int PipMillisecondsPerCell(ActivityPace pace)
        {
            switch (pace)
            {
                case ActivityPace.Slow: return 800;
                case ActivityPace.Fast: return 400;
                default: return 600;
            }
        }

        /// <summary>A problem state draws a saturated badge (missing input, storage full, waiting); paused and inactive are benign (Art Bible 6.2).</summary>
        public static bool IsProblemState(MachineVisualState state) =>
            state == MachineVisualState.MissingInput || state == MachineVisualState.StorageFull || state == MachineVisualState.Waiting;

        /// <summary>True when a refresh with these revisions would change anything; false means the snapshots need not even be built.</summary>
        public bool NeedsRefresh(long structureRevision, long quantityRevision, int layoutLocationCount) =>
            !hasRevision || structureRevision != this.structureRevision || quantityRevision != this.quantityRevision || layoutLocationCount != lastLocationCount;

        /// <summary>Forgets the baselines and the cached revisions: the next refresh reports no delta. Call it when the session is replaced.</summary>
        public void ResetBaseline()
        {
            hasRevision = false;
            for (var i = 0; i < hasBaseline.Length; i++)
            {
                hasBaseline[i] = false;
            }
        }

        /// <summary>
        /// Refreshes the buffer from the read models. <paramref name="views"/> is <see cref="RunSession.CopyMachineStates(MachineStateView[])"/> in slot
        /// order (it carries the completed-cycles counter) and <paramref name="factory"/> the snapshot of the same revisions (next upgrade, cycle time).
        /// <paramref name="rates"/> is optional and only supplies <see cref="MachinePresentation.LimitingResourceId"/>.
        /// </summary>
        /// <returns>
        /// False when nothing changed (the revisions and the location count are as before): the buffer then reports no delta. A skipped refresh never
        /// repeats a delta, so a view that reads the buffer every frame spawns an effect once.
        /// </returns>
        public bool Refresh(
            FactoryWorldLayout layout, RunFactorySnapshot factory, MachineStateView[] views, int viewCount, RatesSnapshot rates = null, bool suppressDeltas = false)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (views == null) throw new ArgumentNullException(nameof(views));
            var count = layout.LocationCount;
            if (!suppressDeltas && !NeedsRefresh(factory.StructureRevision, factory.QuantityRevision, count))
            {
                ClearDeltas();
                return false;
            }

            EnsureCapacity(count);
            locationCount = count;
            lastLocationCount = count;
            hasRevision = true;
            structureRevision = factory.StructureRevision;
            quantityRevision = factory.QuantityRevision;

            for (var i = 0; i < count; i++)
            {
                buffer[i] = new MachinePresentation(new BuildingLocationId(i));
            }

            var machines = factory.Machines;
            var rateMachines = rates != null ? rates.Machines : null;
            for (var m = 0; m < machines.Count; m++)
            {
                var machine = machines[m];
                if (machine.SlotIndex < 0 || machine.SlotIndex >= count)
                {
                    continue;
                }

                var view = m < viewCount && m < views.Length ? views[m] : default;
                var hasView = m < viewCount && m < views.Length && view.SlotIndex == machine.SlotIndex;
                var location = machine.SlotIndex;
                var state = SlotGridModel.StateFor(machine.IsActive, machine.Blocker, machine.Phase);
                var cycle = machine.Rates != null ? machine.Rates.CycleTicks : 0L;
                var pace = PaceOf(cycle);
                var fps = FpsOf(pace);
                if (familyFpsCaps.TryGetValue(machine.MachineId, out var cap) && cap > 0 && cap < fps)
                {
                    fps = cap;
                }

                var upgrade = machine.NextUpgrade != null && machine.NextUpgrade.Available && machine.NextUpgrade.Affordable;
                var delta = 0;
                if (hasView)
                {
                    delta = Advance(location, machine.InstanceId, view.CompletedCycles, suppressDeltas);
                }
                else
                {
                    hasBaseline[location] = false;
                }

                string limiting = null;
                if (state == MachineVisualState.MissingInput && rateMachines != null && m < rateMachines.Count && rateMachines[m].InstanceId == machine.InstanceId)
                {
                    limiting = FirstMissingResource(rateMachines[m]);
                }

                buffer[location] = new MachinePresentation(
                    new BuildingLocationId(location), machine.InstanceId, machine.MachineId, machine.TypeIndex, machine.Level, state, upgrade, pace, fps,
                    delta, limiting);
            }

            // A location that lost its machine forgets its baseline: a machine built there later starts a new baseline.
            for (var i = 0; i < count; i++)
            {
                if (!buffer[i].IsOccupied)
                {
                    hasBaseline[i] = false;
                }
            }

            return true;
        }

        private int Advance(int location, Guid instance, long counter, bool suppress)
        {
            var delta = 0;
            if (!suppress && hasBaseline[location] && baselineInstance[location] == instance && counter >= baseline[location])
            {
                var difference = counter - baseline[location];
                delta = difference > MaxDisplayedCompletionsDelta ? MaxDisplayedCompletionsDelta : (int)difference;
            }

            // A first sight, another machine, a decrease or a suppressed refresh is a baseline reset: delta 0, baseline taken.
            baseline[location] = counter;
            baselineInstance[location] = instance;
            hasBaseline[location] = true;
            return delta;
        }

        private static string FirstMissingResource(MachineDiagnosis diagnosis)
        {
            var blockers = diagnosis.Blockers;
            for (var i = 0; i < blockers.Count; i++)
            {
                if (blockers[i].Kind == MachineBlocker.MissingInput)
                {
                    return blockers[i].ResourceId;
                }
            }

            return null;
        }

        private void ClearDeltas()
        {
            for (var i = 0; i < locationCount; i++)
            {
                var value = buffer[i];
                if (value.IsOccupied && value.CompletionsDelta != 0)
                {
                    buffer[i] = new MachinePresentation(
                        value.Location, value.InstanceId, value.MachineId, value.MachineTypeIndex, value.Level, value.VisualState, value.UpgradeAvailable,
                        value.Pace, value.ActivityFps, 0, value.LimitingResourceId);
                }
            }
        }

        private void EnsureCapacity(int count)
        {
            if (buffer.Length >= count)
            {
                return;
            }

            // Grows only when the layout grows (a new location count); the steady state never allocates.
            Array.Resize(ref buffer, count);
            Array.Resize(ref baseline, count);
            Array.Resize(ref baselineInstance, count);
            Array.Resize(ref hasBaseline, count);
        }
    }
}
