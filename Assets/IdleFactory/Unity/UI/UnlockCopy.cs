using System.Globalization;
using IdleFactory.Run.Content;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// Player-readable text of an unlock condition, read from the catalog (never hard-coded per resource). One wording for every
    /// surface that explains a lock: "Locked: unlocks after you produce Iron Plate." (T362, contracts/ui-surfaces-f005.md, Sell row).
    /// </summary>
    public static class UnlockCopy
    {
        /// <summary>The clause after "unlocks after": "you produce Iron Plate", "the factory reaches level 3" or "you research Miner Tuning".</summary>
        public static string After(RunCatalog catalog, UnlockCondition condition)
        {
            if (condition == null)
            {
                return "it unlocks";
            }

            switch (condition.Kind)
            {
                case "resourceProduced": return "you produce " + ResourceLabel(catalog, condition.Id);
                case "factoryLevelAtLeast": return "the factory reaches level " + condition.Level.ToString(CultureInfo.InvariantCulture);
                case "researchPurchased":
                    return "you research " + (catalog != null && catalog.Research.TryGetValue(condition.Id ?? string.Empty, out var research) ? research.Label : condition.Id);
                default: return "its requirement is met";
            }
        }

        /// <summary>The full lock line of a resource: "Locked: unlocks after you produce Iron Ore."</summary>
        public static string LockedLine(RunCatalog catalog, string resourceId)
        {
            UnlockCondition condition = null;
            if (catalog != null && resourceId != null && catalog.Resources.TryGetValue(resourceId, out var def))
            {
                condition = def.Unlock;
            }

            return "Locked: unlocks after " + After(catalog, condition) + ".";
        }

        private static string ResourceLabel(RunCatalog catalog, string resourceId) =>
            resourceId != null && catalog != null && catalog.Resources.TryGetValue(resourceId, out var def) ? def.Label : resourceId ?? string.Empty;
    }
}
