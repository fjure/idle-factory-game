using System;

namespace IdleFactory.Run.Content
{
    public sealed class UnlockCondition
    {
        public string Kind { get; }
        public string Id { get; }
        public int Level { get; }

        public UnlockCondition(string kind, string id = null, int level = 0)
        {
            if (kind != "always" && kind != "researchPurchased" && kind != "factoryLevelAtLeast" && kind != "resourceProduced")
                throw new ArgumentException("unlock.kind.unknown", nameof(kind));
            if ((kind == "researchPurchased" || kind == "resourceProduced") != (id != null))
                throw new ArgumentException("Unlock ID is required exactly for ID-based conditions.", nameof(id));
            if (id != null) ContentGuard.Id(id, nameof(id));
            if ((kind == "factoryLevelAtLeast") != (level > 0))
                throw new ArgumentOutOfRangeException(nameof(level));
            Kind = kind; Id = id; Level = level;
        }
    }
}
