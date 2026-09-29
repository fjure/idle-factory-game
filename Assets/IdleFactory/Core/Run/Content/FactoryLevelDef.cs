namespace IdleFactory.Run.Content
{
    public sealed class FactoryLevelDef
    {
        public int Level { get; }
        public int SlotCount { get; }
        public long Cost { get; }
        public FactoryLevelDef(int level, int slotCount, long cost)
        {
            if (level < 1) throw new System.ArgumentOutOfRangeException(nameof(level));
            if (slotCount < 1 || slotCount > 12) throw new System.ArgumentOutOfRangeException(nameof(slotCount), "factory.slots.too-many");
            Level = level; SlotCount = slotCount;
            Cost = ContentGuard.Quantity(cost, nameof(cost));
            if (level == 1 && cost != 0) throw new System.ArgumentException("level.first-cost.non-zero", nameof(cost));
        }
    }
}
