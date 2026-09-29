using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// State that survives prestige: IP, Industry levels, Item stacks and the prestige counter. Levels and stacks
    /// are ordinal-ID-ordered lists (GD-6); every mutator is all-or-nothing.
    /// </summary>
    public sealed class PermanentState
    {
        private readonly RunCatalog _catalog;
        private readonly OrdinalCounts _industryLevels = new OrdinalCounts();
        private readonly OrdinalCounts _itemStacks = new OrdinalCounts();

        public long Ip { get; private set; }
        public long PrestigeCount { get; private set; }

        public PermanentState(RunCatalog catalog, long ip = 0, long prestigeCount = 0)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (!ExactBound.Contains(ip)) throw new ArgumentOutOfRangeException(nameof(ip));
            if (!ExactBound.Contains(prestigeCount)) throw new ArgumentOutOfRangeException(nameof(prestigeCount));
            Ip = ip;
            PrestigeCount = prestigeCount;
        }

        public long IndustryLevel(string upgradeId) => _industryLevels.Get(upgradeId);
        public long ItemCount(string itemId) => _itemStacks.Get(itemId);
        public IdCount[] IndustryLevels() => _industryLevels.ToArray();
        public IdCount[] ItemStacks() => _itemStacks.ToArray();

        public RunStateResult TryAddIp(long amount)
        {
            if (amount < 0) return RunStateResult.InvalidArgument;
            if (!CheckedMath.TryAdd(Ip, amount, out long next)) return RunStateResult.ValueLimit;
            Ip = next;
            return RunStateResult.Ok;
        }

        public RunStateResult TryIncrementPrestige()
        {
            if (!CheckedMath.TryAdd(PrestigeCount, 1, out long next)) return RunStateResult.ValueLimit;
            PrestigeCount = next;
            return RunStateResult.Ok;
        }

        /// <summary>Spends <paramref name="ipCost"/> and raises the Industry level by one, as one step.</summary>
        public RunStateResult TryPurchaseIndustry(string upgradeId, long ipCost)
        {
            if (upgradeId == null || ipCost < 0 || !_catalog.IndustryUpgrades.TryGetValue(upgradeId, out IndustryUpgradeDef def))
                return RunStateResult.InvalidArgument;
            long level = _industryLevels.Get(upgradeId);
            if (level >= def.MaxLevel) return RunStateResult.LevelMax;
            if (Ip < ipCost) return RunStateResult.IpInsufficient;
            Ip -= ipCost;
            _industryLevels.Set(upgradeId, level + 1, false);
            return RunStateResult.Ok;
        }

        public RunStateResult TryAddItems(string itemId, long count)
        {
            if (itemId == null || count < 1 || !_catalog.Items.ContainsKey(itemId)) return RunStateResult.InvalidArgument;
            if (!CheckedMath.TryAdd(_itemStacks.Get(itemId), count, out long next)) return RunStateResult.ValueLimit;
            _itemStacks.Set(itemId, next, false);
            return RunStateResult.Ok;
        }

        public RunStateResult TryConsumeItem(string itemId)
        {
            if (itemId == null) return RunStateResult.InvalidArgument;
            long count = _itemStacks.Get(itemId);
            if (count < 1) return RunStateResult.ItemNone;
            _itemStacks.Set(itemId, count - 1, true);
            return RunStateResult.Ok;
        }
    }
}
