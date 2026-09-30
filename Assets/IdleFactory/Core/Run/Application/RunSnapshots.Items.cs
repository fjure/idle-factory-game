using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Application
{
    // -------------------------------------------------------------------------------------------------------
    // Items, Research, Industry and Overdrive read models (FR-016 - FR-019, ui-surfaces.md)
    // -------------------------------------------------------------------------------------------------------

    /// <summary>One choice of the Item activation flow: a target and whether activating on it is possible now.</summary>
    public readonly struct ItemTargetChoice
    {
        /// <summary>The machine type ID to pass to ActivateItem, or null for a <c>factory</c> Item.</summary>
        public string Target { get; }

        /// <summary>The target key of the effect the choice would create (the conflict key part).</summary>
        public string TargetKey { get; }

        /// <summary>False when an effect with the same conflict key is running.</summary>
        public bool Available { get; }

        /// <summary><c>effect.conflict</c> when unavailable, otherwise null.</summary>
        public string Reason { get; }

        public ItemTargetChoice(string target, string targetKey, bool available, string reason)
        {
            Target = target;
            TargetKey = targetKey;
            Available = available;
            Reason = reason;
        }
    }

    /// <summary>An Item definition with the owned count and its activation choices.</summary>
    public sealed class ItemStackSnapshot
    {
        public string ItemId { get; }
        public int Tier { get; }
        public string Label { get; }
        public string PresentationKey { get; }
        public long DurationMs { get; }

        /// <summary><c>factory</c> or <c>machineType</c>.</summary>
        public string TargetKind { get; }
        public string EffectKind { get; }
        public long EffectValue { get; }

        /// <summary>Owned Items of this kind (0 for a definition the player has none of).</summary>
        public long Count { get; }

        /// <summary>One choice for a <c>factory</c> Item, otherwise one per allowed machine type in content order.</summary>
        public IReadOnlyList<ItemTargetChoice> Targets { get; }

        /// <summary>True when at least one owned Item can be activated on at least one target.</summary>
        public bool CanActivate { get; }

        /// <summary><c>item.none</c> with no Item, <c>effect.conflict</c> when every target conflicts, otherwise null.</summary>
        public string ActivateReason { get; }

        internal ItemStackSnapshot(ItemDef def, long count, ItemTargetChoice[] targets, bool canActivate, string reason)
        {
            ItemId = def.Id;
            Tier = def.Tier;
            Label = def.Label;
            PresentationKey = def.PresentationKey;
            DurationMs = def.DurationMs;
            TargetKind = def.TargetKind;
            EffectKind = def.Effect.Kind;
            EffectValue = def.Effect.Value;
            Count = count;
            Targets = new ReadOnlyCollection<ItemTargetChoice>(targets);
            CanActivate = canActivate;
            ActivateReason = reason;
        }
    }

    /// <summary>A running Item effect with its countdown.</summary>
    public sealed class ActiveEffectSnapshot
    {
        public string ItemId { get; }
        public string Label { get; }
        public string EffectKind { get; }
        public string TargetKey { get; }
        public long StartUtcMs { get; }
        public long EndUtcMs { get; }

        /// <summary>Milliseconds to the end at the timeline (0 once passed).</summary>
        public long RemainingMs { get; }

        internal ActiveEffectSnapshot(ActiveEffect effect, string label, long remainingMs)
        {
            ItemId = effect.SourceId;
            Label = label;
            EffectKind = effect.ConflictKey.EffectKind;
            TargetKey = effect.ConflictKey.TargetKey;
            StartUtcMs = effect.StartUtcMs;
            EndUtcMs = effect.EndUtcMs;
            RemainingMs = remainingMs;
        }
    }

    /// <summary>Item stacks by tier and the running effects.</summary>
    public sealed class ItemsSnapshot
    {
        public long StructureRevision { get; }
        public long QuantityRevision { get; }
        public long TimelineUtcMs { get; }

        /// <summary>Every Item definition, ordered by tier and then ordinal ID.</summary>
        public IReadOnlyList<ItemStackSnapshot> Stacks { get; }

        /// <summary>The running effects in the effect set's order (source ID, effect kind, target key).</summary>
        public IReadOnlyList<ActiveEffectSnapshot> Active { get; }

        /// <summary>The total number of owned Items; 0 shows the "No Items yet" empty state.</summary>
        public long TotalCount { get; }

        internal ItemsSnapshot(long structureRevision, long quantityRevision, long timelineUtcMs, ItemStackSnapshot[] stacks,
            ActiveEffectSnapshot[] active, long totalCount)
        {
            StructureRevision = structureRevision;
            QuantityRevision = quantityRevision;
            TimelineUtcMs = timelineUtcMs;
            Stacks = new ReadOnlyCollection<ItemStackSnapshot>(stacks);
            Active = new ReadOnlyCollection<ActiveEffectSnapshot>(active);
            TotalCount = totalCount;
        }
    }

    /// <summary>A prerequisite of a Research node and whether it is met.</summary>
    public readonly struct ResearchPrerequisiteView
    {
        public string ResearchId { get; }
        public int Level { get; }
        public bool Met { get; }

        public ResearchPrerequisiteView(string researchId, int level, bool met)
        {
            ResearchId = researchId;
            Level = level;
            Met = met;
        }
    }

    /// <summary>A Research node: level, cost, effect, prerequisites and whether the next level can be bought.</summary>
    public sealed class ResearchNodeSnapshot
    {
        public string ResearchId { get; }
        public string Label { get; }
        public int Level { get; }
        public int MaxLevel { get; }

        /// <summary>The RP cost of the next level; 0 at the maximum level.</summary>
        public long NextCost { get; }
        public string EffectKind { get; }
        public string TargetKind { get; }
        public string TargetId { get; }

        /// <summary>The effect value applied once per purchased level.</summary>
        public long EffectValue { get; }
        public IReadOnlyList<ResearchPrerequisiteView> Prerequisites { get; }

        /// <summary>Prerequisite depth, 0 without prerequisites: the left-to-right order of the graph (OD-8).</summary>
        public int Depth { get; }
        public bool CanPurchase { get; }

        /// <summary><c>research.prerequisite</c>, <c>level.max</c> or <c>rp.insufficient</c> (in the command's order) when disabled, otherwise null.</summary>
        public string PurchaseReason { get; }

        internal ResearchNodeSnapshot(ResearchDef def, int level, long nextCost, ResearchPrerequisiteView[] prerequisites, int depth,
            bool canPurchase, string reason)
        {
            ResearchId = def.Id;
            Label = def.Label;
            Level = level;
            MaxLevel = def.MaxLevel;
            NextCost = nextCost;
            EffectKind = def.Effect.Kind;
            TargetKind = def.Effect.TargetKind;
            TargetId = def.Effect.TargetId;
            EffectValue = def.Effect.Value;
            Prerequisites = new ReadOnlyCollection<ResearchPrerequisiteView>(prerequisites);
            Depth = depth;
            CanPurchase = canPurchase;
            PurchaseReason = reason;
        }
    }

    /// <summary>The Research graph and the RP balance.</summary>
    public sealed class ResearchSnapshot
    {
        public long StructureRevision { get; }
        public long QuantityRevision { get; }
        public long TimelineUtcMs { get; }
        public long Rp { get; }

        /// <summary>Every Research in keyboard order: prerequisite depth, then ordinal ID.</summary>
        public IReadOnlyList<ResearchNodeSnapshot> Nodes { get; }

        internal ResearchSnapshot(long structureRevision, long quantityRevision, long timelineUtcMs, long rp, ResearchNodeSnapshot[] nodes)
        {
            StructureRevision = structureRevision;
            QuantityRevision = quantityRevision;
            TimelineUtcMs = timelineUtcMs;
            Rp = rp;
            Nodes = new ReadOnlyCollection<ResearchNodeSnapshot>(nodes);
        }
    }

    /// <summary>An Industry upgrade: level, cost, effect and whether it applies now or at the next run.</summary>
    public sealed class IndustryUpgradeSnapshot
    {
        public string UpgradeId { get; }
        public string Label { get; }
        public int Level { get; }
        public int MaxLevel { get; }

        /// <summary>The IP cost of the next level; 0 at the maximum level.</summary>
        public long NextCost { get; }
        public string EffectKind { get; }
        public string TargetKind { get; }
        public string TargetId { get; }
        public long EffectValue { get; }

        /// <summary>True for <c>startingCash</c> and <c>startingStorageLevel</c>: the "applies next run" tag.</summary>
        public bool AppliesNextRun { get; }
        public bool CanPurchase { get; }

        /// <summary><c>level.max</c> or <c>ip.insufficient</c> when disabled, otherwise null.</summary>
        public string PurchaseReason { get; }

        internal IndustryUpgradeSnapshot(IndustryUpgradeDef def, int level, long nextCost, bool canPurchase, string reason)
        {
            UpgradeId = def.Id;
            Label = def.Label;
            Level = level;
            MaxLevel = def.MaxLevel;
            NextCost = nextCost;
            EffectKind = def.Effect.Kind;
            TargetKind = def.Effect.TargetKind;
            TargetId = def.Effect.TargetId;
            EffectValue = def.Effect.Value;
            AppliesNextRun = RunCommands.IsNextRunEffect(def.Effect);
            CanPurchase = canPurchase;
            PurchaseReason = reason;
        }
    }

    /// <summary>
    /// The IP balance, the upgrades, and what the next run start needs from them (the prestige task applies
    /// them): <see cref="NextRunStartingCash"/> and <see cref="NextRunStorageLevel"/>.
    /// </summary>
    public sealed class IndustrySnapshot
    {
        public long StructureRevision { get; }
        public long QuantityRevision { get; }
        public long TimelineUtcMs { get; }
        public long Ip { get; }

        /// <summary>Every upgrade in ordinal ID order.</summary>
        public IReadOnlyList<IndustryUpgradeSnapshot> Upgrades { get; }

        /// <summary>
        /// <c>RunStart.StartingCash</c> plus <c>valuePerLevel x level</c> of every <c>startingCash</c> upgrade,
        /// saturating at the exact bound.
        /// </summary>
        public long NextRunStartingCash { get; }

        /// <summary>
        /// <c>1 + valuePerLevel x level</c> of every <c>startingStorageLevel</c> upgrade, clamped to the number of
        /// storage levels of the catalog.
        /// </summary>
        public int NextRunStorageLevel { get; }

        internal IndustrySnapshot(long structureRevision, long quantityRevision, long timelineUtcMs, long ip,
            IndustryUpgradeSnapshot[] upgrades, long nextRunStartingCash, int nextRunStorageLevel)
        {
            StructureRevision = structureRevision;
            QuantityRevision = quantityRevision;
            TimelineUtcMs = timelineUtcMs;
            Ip = ip;
            Upgrades = new ReadOnlyCollection<IndustryUpgradeSnapshot>(upgrades);
            NextRunStartingCash = nextRunStartingCash;
            NextRunStorageLevel = nextRunStorageLevel;
        }
    }

    /// <summary>The Overdrive control state (HUD).</summary>
    public enum OverdriveStage
    {
        /// <summary>Not unlocked yet.</summary>
        Locked = 0,
        Ready = 1,
        Active = 2,
        CoolingDown = 3,
    }

    /// <summary>Overdrive: ready, active with remaining time, or cooling down with remaining time (plus locked until its unlock).</summary>
    public sealed class OverdriveStatus
    {
        public long StructureRevision { get; }
        public long QuantityRevision { get; }
        public long TimelineUtcMs { get; }
        public OverdriveStage State { get; }

        /// <summary>Milliseconds until the boost ends (Active) or until Overdrive is ready again (CoolingDown); 0 otherwise.</summary>
        public long RemainingMs { get; }
        public long DurationMs { get; }
        public long CooldownMs { get; }
        public long SpeedPpm { get; }

        /// <summary>The condition that unlocks Overdrive, for the "Locked until" copy.</summary>
        public UnlockCondition Unlock { get; }
        public long? ActiveUntilUtcMs { get; }
        public long? CooldownUntilUtcMs { get; }
        public bool CanActivate { get; }

        /// <summary><c>command.invalid</c> (locked), <c>overdrive.active</c> or <c>overdrive.cooling-down</c> when disabled, otherwise null.</summary>
        public string ActivateReason { get; }

        internal OverdriveStatus(long structureRevision, long quantityRevision, long timelineUtcMs, OverdriveStage state, long remainingMs,
            OverdriveDef def, long? activeUntil, long? cooldownUntil, bool canActivate, string reason)
        {
            StructureRevision = structureRevision;
            QuantityRevision = quantityRevision;
            TimelineUtcMs = timelineUtcMs;
            State = state;
            RemainingMs = remainingMs;
            DurationMs = def.DurationMs;
            CooldownMs = def.CooldownMs;
            SpeedPpm = def.SpeedPpm;
            Unlock = def.Unlock;
            ActiveUntilUtcMs = activeUntil;
            CooldownUntilUtcMs = cooldownUntil;
            CanActivate = canActivate;
            ActivateReason = reason;
        }
    }

    public static partial class RunSnapshots
    {
        public static ItemsSnapshot Items(RunSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RunCatalog catalog = session.Catalog;
            RunState state = session.State;
            PermanentState permanent = session.Permanent;
            long now = session.TimelineUtcMs;

            var defs = new List<ItemDef>(catalog.Items.Values);
            defs.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.CompareOrdinal(a.Id, b.Id));
            var stacks = new ItemStackSnapshot[defs.Count];
            long total = 0;
            for (int i = 0; i < stacks.Length; i++)
            {
                ItemDef def = defs[i];
                long count = permanent == null ? 0 : permanent.ItemCount(def.Id);
                total += count;
                bool factory = def.TargetKind == "factory";
                var targets = new ItemTargetChoice[factory ? 1 : def.AllowedMachineIds.Count];
                bool anyAvailable = false;
                for (int t = 0; t < targets.Length; t++)
                {
                    string target = factory ? null : def.AllowedMachineIds[t];
                    string key = factory ? RunCommands.FactoryTargetKey : target;
                    bool conflict = RunningEffect(state, new ConflictKey(def.Effect.Kind, key), now);
                    targets[t] = new ItemTargetChoice(target, key, !conflict, conflict ? RunReasons.EffectConflict : null);
                    anyAvailable |= !conflict;
                }
                bool can = count >= 1 && anyAvailable;
                string reason = count < 1 ? RunReasons.ItemNone : anyAvailable ? null : RunReasons.EffectConflict;
                stacks[i] = new ItemStackSnapshot(def, count, targets, can, reason);
            }

            IReadOnlyList<ActiveEffect> effects = state.Effects.Effects;
            var active = new ActiveEffectSnapshot[effects.Count];
            for (int i = 0; i < active.Length; i++)
            {
                catalog.Items.TryGetValue(effects[i].SourceId, out ItemDef def);
                active[i] = new ActiveEffectSnapshot(effects[i], def?.Label ?? effects[i].SourceId,
                    effects[i].EndUtcMs > now ? effects[i].EndUtcMs - now : 0);
            }
            return new ItemsSnapshot(session.StructureRevision, session.QuantityRevision, now, stacks, active, total);
        }

        private static bool RunningEffect(RunState state, ConflictKey key, long now)
        {
            IReadOnlyList<ActiveEffect> effects = state.Effects.Effects;
            for (int i = 0; i < effects.Count; i++)
                if (effects[i].ConflictKey.Equals(key) && effects[i].EndUtcMs > now) return true;
            return false;
        }

        public static ResearchSnapshot Research(RunSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RunCatalog catalog = session.Catalog;
            RunState state = session.State;
            var ids = new List<string>(catalog.Research.Keys);
            ids.Sort(StringComparer.Ordinal);
            var nodes = new List<ResearchNodeSnapshot>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                ResearchDef def = catalog.Research[ids[i]];
                int level = (int)state.ResearchLevel(def.Id);
                var prerequisites = new ResearchPrerequisiteView[def.Prerequisites.Count];
                bool met = true;
                for (int p = 0; p < prerequisites.Length; p++)
                {
                    ResearchPrerequisite pre = def.Prerequisites[p];
                    bool ok = state.ResearchLevel(pre.ResearchId) >= pre.Level;
                    prerequisites[p] = new ResearchPrerequisiteView(pre.ResearchId, pre.Level, ok);
                    met &= ok;
                }
                bool atMax = level >= def.MaxLevel;
                long cost = atMax ? 0 : def.RpCostTable[level];
                string reason = !met ? RunReasons.ResearchPrerequisite : atMax ? RunReasons.LevelMax : state.Rp < cost ? RunReasons.RpInsufficient : null;
                nodes.Add(new ResearchNodeSnapshot(def, level, cost, prerequisites, Depth(catalog, def, 0), reason == null, reason));
            }
            nodes.Sort((a, b) => a.Depth != b.Depth ? a.Depth.CompareTo(b.Depth) : string.CompareOrdinal(a.ResearchId, b.ResearchId));
            return new ResearchSnapshot(session.StructureRevision, session.QuantityRevision, session.TimelineUtcMs, state.Rp, nodes.ToArray());
        }

        /// <summary>The longest prerequisite chain above a Research; a cycle (which content validation rejects) stops at 1024.</summary>
        private static int Depth(RunCatalog catalog, ResearchDef def, int guard)
        {
            if (guard > 1024) return 0;
            int depth = 0;
            for (int i = 0; i < def.Prerequisites.Count; i++)
            {
                if (!catalog.Research.TryGetValue(def.Prerequisites[i].ResearchId, out ResearchDef pre)) continue;
                int d = Depth(catalog, pre, guard + 1) + 1;
                if (d > depth) depth = d;
            }
            return depth;
        }

        public static IndustrySnapshot Industry(RunSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RunCatalog catalog = session.Catalog;
            PermanentState permanent = session.Permanent;
            var ids = new List<string>(catalog.IndustryUpgrades.Keys);
            ids.Sort(StringComparer.Ordinal);
            long ip = permanent == null ? 0 : permanent.Ip;
            var upgrades = new IndustryUpgradeSnapshot[ids.Count];
            long cash = catalog.RunStart.StartingCash;
            long storage = 1;
            for (int i = 0; i < upgrades.Length; i++)
            {
                IndustryUpgradeDef def = catalog.IndustryUpgrades[ids[i]];
                int level = permanent == null ? 0 : (int)permanent.IndustryLevel(def.Id);
                bool atMax = level >= def.MaxLevel;
                long cost = atMax ? 0 : def.IpCostTable[level];
                string reason = permanent == null ? RunReasons.CommandInvalid : atMax ? RunReasons.LevelMax : ip < cost ? RunReasons.IpInsufficient : null;
                upgrades[i] = new IndustryUpgradeSnapshot(def, level, cost, reason == null, reason);
                if (def.Effect.Kind == "startingCash") cash = SaturatingAdd(cash, SaturatingMultiply(def.Effect.Value, level));
                else if (def.Effect.Kind == "startingStorageLevel") storage = SaturatingAdd(storage, SaturatingMultiply(def.Effect.Value, level));
            }
            int storageLevel = (int)Math.Min(storage, catalog.StorageLevels.Count);
            return new IndustrySnapshot(session.StructureRevision, session.QuantityRevision, session.TimelineUtcMs, ip, upgrades, cash, storageLevel);
        }

        private static long SaturatingAdd(long a, long b) => CheckedMath.TryAdd(a, b, out long sum) ? sum : ExactBound.Value;

        private static long SaturatingMultiply(long a, long b) => CheckedMath.TryMultiply(a, b, out long product) ? product : ExactBound.Value;

        public static OverdriveStatus Overdrive(RunSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RunCatalog catalog = session.Catalog;
            RunState state = session.State;
            long now = session.TimelineUtcMs;
            OverdriveState od = state.Overdrive;
            OverdriveStage status;
            long remaining = 0;
            string reason = null;
            if (!state.IsUnlocked(RunState.OverdriveUnlockId))
            {
                status = OverdriveStage.Locked;
                reason = RunReasons.CommandInvalid;
            }
            else if (od.IsActive(now))
            {
                status = OverdriveStage.Active;
                remaining = od.ActiveUntilUtcMs.Value - now;
                reason = RunReasons.OverdriveActive;
            }
            else if (od.IsCoolingDown(now))
            {
                status = OverdriveStage.CoolingDown;
                remaining = od.CooldownUntilUtcMs.Value - now;
                reason = RunReasons.OverdriveCoolingDown;
            }
            else
            {
                status = OverdriveStage.Ready;
            }
            return new OverdriveStatus(session.StructureRevision, session.QuantityRevision, now, status, remaining, catalog.Overdrive,
                od.ActiveUntilUtcMs, od.CooldownUntilUtcMs, reason == null, reason);
        }
    }
}
