using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdleFactory.Run.Application
{
    // Session-log records (data-model "Session log (not game state; Unity adapter)"). Each is a readonly struct that
    // carries only the allowlisted fields of its table row: numbers (long/int), bool, closed enums, content IDs
    // (string members named "...Id", always content IDs, never player text) and immutable lists of those. No text,
    // paths, exceptions or save bytes. The allowlist is pinned by RunEventSinkTests.EveryEventTypeIsAReadonlyStructOfAllowlistedMembersOnly.

    public enum RunBuildKind { Release, Development, Editor }

    public enum RunPlatformClass { Desktop, Mobile }

    public enum RunMilestoneKind { FirstGear, FirstContract, FirstItem, FirstPrestige, Run2Eligible }

    public enum RunContractEventKind { Accept, Deliver, AutoDeliver, ProduceComplete, Expire, Abandon }

    public enum RunPurchaseKind { Build, Upgrade, Storage, Factory, Research, Industry }

    public enum RunClockKind { Backward, ForwardJump, LiveGap }

    public enum RunLifecycleKind { Start, Quit, FocusLossCheckpoint, LogToggled, Cleared }

    /// <summary>A machine type at a level and how many of it exist (Sample: machines by type and level).</summary>
    public readonly struct RunMachineCount
    {
        public RunMachineCount(string machineId, int level, int count)
        {
            MachineId = machineId;
            Level = level;
            Count = count;
        }

        public string MachineId { get; }
        public int Level { get; }
        public int Count { get; }
    }

    /// <summary>A purchased industry (permanent upgrade) and its level (Prestige: industryPurchases).</summary>
    public readonly struct RunIndustryLevel
    {
        public RunIndustryLevel(string industryId, int level)
        {
            IndustryId = industryId;
            Level = level;
        }

        public string IndustryId { get; }
        public int Level { get; }
    }

    /// <summary>A 128-bit random per-app-start session identifier (contracts/session-log.md). Numbers only; this type does not generate it.</summary>
    public readonly struct RunSessionId
    {
        public RunSessionId(ulong high, ulong low)
        {
            High = high;
            Low = low;
        }

        public ulong High { get; }
        public ulong Low { get; }
    }

    internal static class RunEventLists
    {
        private static readonly IReadOnlyList<RunMachineCount> NoMachines = new ReadOnlyCollection<RunMachineCount>(new RunMachineCount[0]);
        private static readonly IReadOnlyList<RunIndustryLevel> NoIndustries = new ReadOnlyCollection<RunIndustryLevel>(new RunIndustryLevel[0]);

        /// <summary>A defensive copy behind a read-only wrapper, so neither the caller's array nor a cast can change the record.</summary>
        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, IReadOnlyList<T> empty)
        {
            if (source == null || source.Count == 0) return empty;
            var copy = new T[source.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i];
            return new ReadOnlyCollection<T>(copy);
        }

        public static IReadOnlyList<RunMachineCount> Machines(IReadOnlyList<RunMachineCount> source) => Copy(source, NoMachines);

        public static IReadOnlyList<RunIndustryLevel> Industries(IReadOnlyList<RunIndustryLevel> source) => Copy(source, NoIndustries);
    }

    public readonly struct RunHeaderEvent
    {
        public RunHeaderEvent(int schema, int contentRevision, RunBuildKind buildKind, RunPlatformClass platformClass, int timeScale, RunSessionId sessionId)
        {
            Schema = schema;
            ContentRevision = contentRevision;
            BuildKind = buildKind;
            PlatformClass = platformClass;
            TimeScale = timeScale;
            SessionId = sessionId;
        }

        public int Schema { get; }
        public int ContentRevision { get; }
        public RunBuildKind BuildKind { get; }
        public RunPlatformClass PlatformClass { get; }
        /// <summary>The adapter's time-scale multiplier as a whole number (1 = real time).</summary>
        public int TimeScale { get; }
        /// <summary>128 random bits drawn once per app start (T338 generates them); never text.</summary>
        public RunSessionId SessionId { get; }
    }

    public readonly struct RunSampleEvent
    {
        public RunSampleEvent(long playSeconds, long factoryValue, long cash, IReadOnlyList<RunMachineCount> machines, int storageLevel, int factoryLevel)
        {
            PlaySeconds = playSeconds;
            FactoryValue = factoryValue;
            Cash = cash;
            Machines = RunEventLists.Machines(machines);
            StorageLevel = storageLevel;
            FactoryLevel = factoryLevel;
        }

        public long PlaySeconds { get; }
        public long FactoryValue { get; }
        public long Cash { get; }
        public IReadOnlyList<RunMachineCount> Machines { get; }
        public int StorageLevel { get; }
        public int FactoryLevel { get; }
    }

    public readonly struct RunMilestoneEvent
    {
        public RunMilestoneEvent(RunMilestoneKind kind, long playSeconds)
        {
            Kind = kind;
            PlaySeconds = playSeconds;
        }

        public RunMilestoneKind Kind { get; }
        public long PlaySeconds { get; }
    }

    public readonly struct RunSaleEvent
    {
        public RunSaleEvent(string resourceId, long units, long cash, bool manual)
        {
            ResourceId = resourceId;
            Units = units;
            Cash = cash;
            Manual = manual;
        }

        public string ResourceId { get; }
        public long Units { get; }
        public long Cash { get; }
        public bool Manual { get; }
    }

    /// <summary>
    /// Manual mining (T358, D-O): the numbers of one <c>Mine</c> call, not one record per activation. <see cref="Counted"/>
    /// activations were credited, <see cref="Ignored"/> were over the per-second rate cap, <see cref="Blocked"/> were stopped
    /// by a full store or the numeric bound, and <see cref="Ore"/> whole Ore moved into stock. The Unity adapter rolls these up
    /// per minute for the session log.
    /// </summary>
    public readonly struct RunMiningEvent
    {
        public RunMiningEvent(string resourceId, long counted, long ignored, long blocked, long ore)
        {
            ResourceId = resourceId;
            Counted = counted;
            Ignored = ignored;
            Blocked = blocked;
            Ore = ore;
        }

        public string ResourceId { get; }
        public long Counted { get; }
        public long Ignored { get; }
        public long Blocked { get; }
        public long Ore { get; }
    }

    public readonly struct RunContractEvent
    {
        public RunContractEvent(RunContractEventKind kind, string offerId, int index, long required, long reward, long ratioPpm)
        {
            Kind = kind;
            OfferId = offerId;
            Index = index;
            Required = required;
            Reward = reward;
            RatioPpm = ratioPpm;
        }

        /// <summary>The table's <c>event</c> field.</summary>
        public RunContractEventKind Kind { get; }
        public string OfferId { get; }
        public int Index { get; }
        public long Required { get; }
        public long Reward { get; }
        public long RatioPpm { get; }
    }

    public readonly struct RunPrestigeEvent
    {
        public RunPrestigeEvent(long factoryValue, long threshold, long ip, long secondsSinceEligible, IReadOnlyList<RunIndustryLevel> industryPurchases)
        {
            FactoryValue = factoryValue;
            Threshold = threshold;
            Ip = ip;
            SecondsSinceEligible = secondsSinceEligible;
            IndustryPurchases = RunEventLists.Industries(industryPurchases);
        }

        public long FactoryValue { get; }
        public long Threshold { get; }
        public long Ip { get; }
        public long SecondsSinceEligible { get; }
        public IReadOnlyList<RunIndustryLevel> IndustryPurchases { get; }
    }

    public readonly struct RunOfflineReturnEvent
    {
        public RunOfflineReturnEvent(long absenceMs, long creditedMs, bool capHit, long completions, long storeFullStallSeconds,
            long soldUnits, long cashDelta, bool reclassifiedLiveGap)
        {
            AbsenceMs = absenceMs;
            CreditedMs = creditedMs;
            CapHit = capHit;
            Completions = completions;
            StoreFullStallSeconds = storeFullStallSeconds;
            SoldUnits = soldUnits;
            CashDelta = cashDelta;
            ReclassifiedLiveGap = reclassifiedLiveGap;
        }

        public long AbsenceMs { get; }
        public long CreditedMs { get; }
        public bool CapHit { get; }
        public long Completions { get; }
        public long StoreFullStallSeconds { get; }
        public long SoldUnits { get; }
        public long CashDelta { get; }
        public bool ReclassifiedLiveGap { get; }
    }

    public readonly struct RunBottleneckEvent
    {
        public RunBottleneckEvent(string resourceId, long missingInputSeconds, long storeFullSeconds)
        {
            ResourceId = resourceId;
            MissingInputSeconds = missingInputSeconds;
            StoreFullSeconds = storeFullSeconds;
        }

        public string ResourceId { get; }
        public long MissingInputSeconds { get; }
        public long StoreFullSeconds { get; }
    }

    public readonly struct RunPurchaseEvent
    {
        public RunPurchaseEvent(RunPurchaseKind kind, string targetId, int level, long cost, long secondsSinceAffordable)
        {
            Kind = kind;
            TargetId = targetId;
            Level = level;
            Cost = cost;
            SecondsSinceAffordable = secondsSinceAffordable;
        }

        public RunPurchaseKind Kind { get; }
        public string TargetId { get; }
        public int Level { get; }
        public long Cost { get; }
        public long SecondsSinceAffordable { get; }
    }

    public readonly struct RunClockEvent
    {
        public RunClockEvent(RunClockKind kind, long deltaMs, bool reclassified)
        {
            Kind = kind;
            DeltaMs = deltaMs;
            Reclassified = reclassified;
        }

        public RunClockKind Kind { get; }
        public long DeltaMs { get; }
        public bool Reclassified { get; }
    }

    public readonly struct RunLifecycleEvent
    {
        public RunLifecycleEvent(RunLifecycleKind kind, int droppedEvents)
        {
            Kind = kind;
            DroppedEvents = droppedEvents;
        }

        /// <summary>The table's <c>event</c> field.</summary>
        public RunLifecycleKind Kind { get; }
        public int DroppedEvents { get; }
    }
}
