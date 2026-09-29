namespace IdleFactory.Run.Content
{
    public sealed class StorageLevelDef
    {
        public int Level { get; }
        public long Capacity { get; }
        public long Cost { get; }
        public StorageLevelDef(int level, long capacity, long cost)
        {
            if (level < 1) throw new System.ArgumentOutOfRangeException(nameof(level));
            Level = level;
            Capacity = ContentGuard.PositiveQuantity(capacity, nameof(capacity));
            Cost = ContentGuard.Quantity(cost, nameof(cost));
            if (level == 1 && cost != 0) throw new System.ArgumentException("level.first-cost.non-zero", nameof(cost));
        }
    }
}
