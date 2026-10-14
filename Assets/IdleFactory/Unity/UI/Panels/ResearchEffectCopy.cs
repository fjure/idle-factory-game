using System;
using System.Globalization;
using IdleFactory.Run.Content;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// Player-readable copy of a Research or Industry effect (T116, FR-016, FR-023). Pure presentation: the effect kind, target and
    /// value come from the read models and the labels from the catalog; no economy rule lives here. Parts-per-million values show as a
    /// factor floored to two decimals ("x1.05"), so a shown factor never overstates the exact one.
    /// </summary>
    public static class ResearchEffectCopy
    {
        /// <summary>"Speed x1.05 on Miner per level", "Output x2 on Miner", "+500 starting Cash per level".</summary>
        public static string Describe(RunCatalog catalog, string effectKind, string targetKind, string targetId, long value, bool perLevel = true)
        {
            var target = TargetText(catalog, targetKind, targetId);
            var level = perLevel ? " per level" : string.Empty;
            switch (effectKind)
            {
                case "machineSpeed": return "Speed x" + Factor(value) + target + level;
                case "machineOutput": return "Output x" + value.ToString(CultureInfo.InvariantCulture) + target;
                case "salePrice": return "Sale price x" + Factor(value) + target + level;
                case "upgradeCost": return "Upgrade cost x" + Factor(value) + target + level;
                case "manualYield": return "Manual mining yield x" + Factor(value) + target + level;
                case "startingCash": return "+" + QuantityFormatter.Format(value) + " starting Cash" + level;
                case "startingStorageLevel": return "+" + QuantityFormatter.Format(value) + " starting storage level" + level;
                default: return effectKind;
            }
        }

        /// <summary>The exact effect text for the detail line, with the unrounded factor.</summary>
        public static string DescribeExact(RunCatalog catalog, string effectKind, string targetKind, string targetId, long value)
        {
            switch (effectKind)
            {
                case "machineSpeed":
                case "salePrice":
                case "upgradeCost":
                case "manualYield":
                    return Describe(catalog, effectKind, targetKind, targetId, value) + " (" + value.ToString("N0", CultureInfo.InvariantCulture) + " ppm)";
                case "startingCash":
                case "startingStorageLevel":
                    return Describe(catalog, effectKind, targetKind, targetId, value) + " (" + value.ToString("N0", CultureInfo.InvariantCulture) + ")";
                default:
                    return Describe(catalog, effectKind, targetKind, targetId, value);
            }
        }

        private static string TargetText(RunCatalog catalog, string targetKind, string targetId)
        {
            switch (targetKind)
            {
                case "allMachines": return " on all machines";
                case "allResources": return " on all resources";
                case "expansion": return " on expansion";
                case "machine": return " on " + (catalog != null && catalog.Machines.TryGetValue(targetId, out var machine) ? machine.Label : targetId);
                case "machineType": return " on " + (catalog != null && catalog.Machines.TryGetValue(targetId, out var type) ? type.Label : targetId);
                case "resource": return " on " + (catalog != null && catalog.Resources.TryGetValue(targetId, out var resource) ? resource.Label : targetId);
                default: return string.Empty;
            }
        }

        /// <summary>A parts-per-million factor floored to two decimals with trailing zeros dropped: 1,050,000 is "1.05", 980,000 is "0.98".</summary>
        public static string Factor(long ppm)
        {
            if (ppm < 0)
            {
                return "0";
            }

            var whole = ppm / 1_000_000L;
            var hundredths = (ppm % 1_000_000L) / 10_000L;
            var text = whole.ToString(CultureInfo.InvariantCulture);
            if (hundredths == 0)
            {
                return text;
            }

            text += "." + hundredths.ToString("00", CultureInfo.InvariantCulture);
            return text.EndsWith("0", StringComparison.Ordinal) ? text.Substring(0, text.Length - 1) : text;
        }
    }
}
