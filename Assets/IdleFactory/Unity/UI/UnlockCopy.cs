using System;
using System.Collections.Generic;
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

        /// <summary>
        /// One clause for several conditions (R-10): conditions of one kind are grouped and their labels joined ("you produce Iron Plate, Gear or
        /// Iron Bar"), groups of different kinds are joined with "or". Duplicates are dropped; the order is the order of first appearance.
        /// </summary>
        public static string AfterAny(RunCatalog catalog, IEnumerable<UnlockCondition> conditions)
        {
            var groups = new List<string>();
            var labels = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var condition in conditions)
            {
                var kind = condition?.Kind ?? string.Empty;
                var grouped = kind == "resourceProduced" || kind == "researchPurchased";
                var key = grouped ? kind : After(catalog, condition);
                var label = grouped ? Label(catalog, condition) : null;
                if (!labels.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    labels.Add(key, list);
                    groups.Add(key);
                }

                if (label != null && !list.Contains(label))
                {
                    list.Add(label);
                }
            }

            var clauses = new List<string>();
            foreach (var key in groups)
            {
                var list = labels[key];
                if (list.Count == 0)
                {
                    clauses.Add(key);
                    continue;
                }

                clauses.Add((key == "resourceProduced" ? "you produce " : "you research ") + JoinLabels(list));
            }

            return JoinLabels(clauses, true);
        }

        private static string Label(RunCatalog catalog, UnlockCondition condition)
        {
            if (condition.Kind == "resourceProduced")
            {
                return ResourceLabel(catalog, condition.Id);
            }

            return catalog != null && catalog.Research.TryGetValue(condition.Id ?? string.Empty, out var research) ? research.Label : condition.Id;
        }

        /// <summary>"A", "A or B", "A, B or C" (clauses of different kinds are joined with "or" only).</summary>
        private static string JoinLabels(List<string> items, bool clauses = false)
        {
            if (items.Count == 1)
            {
                return items[0];
            }

            var head = string.Join(clauses ? " or " : ", ", items.GetRange(0, items.Count - 1));
            if (clauses && items.Count > 2)
            {
                head = string.Join(" or ", items.GetRange(0, items.Count - 1));
            }

            return head + " or " + items[items.Count - 1];
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
