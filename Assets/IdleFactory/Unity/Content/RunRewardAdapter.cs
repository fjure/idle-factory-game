using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Transitional reward adapter (plan step 4b, analysis finding I1; removed by T320). Schema v3 carries a Cash ratio
    /// (<c>rewardRatioPpm</c>) and a catalog-level Item ladder instead of the v2 per-offer Cash curve and Item rewards, but the
    /// F003 delivery path (<c>RunCommands.DeliverContract</c>, <c>DueItems</c>, the contract snapshots) still reads
    /// <see cref="ContractRewardDef"/> lists. This adapter synthesizes those lists from the v3 fields so that path keeps
    /// working until T320 replaces the reward formula:
    /// <list type="bullet">
    /// <item><description><b>Cash</b>: a 1,024-entry table, <c>ceil(quantity[i] x basePrice x rewardRatioPpm / 10^6)</c>, in checked 64-bit math
    /// (<c>MulDivCeil</c> of the exact market value <c>quantity x basePrice</c>). An overflow is a content error (the validator reports
    /// <c>contract.reward.ratio</c> before the builder runs).</description></item>
    /// <item><description><b>RP</b>: the offer's <c>rpReward</c> curve, unchanged.</description></item>
    /// <item><description><b>Items</b>: each ladder entry <c>(index, itemId)</c> becomes a completion index of an Item reward on one
    /// standing delivery offer. The F003 path counts completions per offer, so the run-wide ladder index is applied to the owning
    /// offer's own completion count. The owner of an Item is chosen by its tier: standing delivery offers ordered by the price of
    /// their resource (then by ID) are matched to tiers 1, 2, 3 in turn, and a tier beyond the last offer goes to the last offer.
    /// With the shipped revision-2 content this reproduces the F003 grants exactly (Tier I from the timed Bar offer, Tier II from the
    /// Plate offer, Tier III from the Gear offer). <c>perRunCap</c> is not applied (the F003 path has no run-wide cap).</description></item>
    /// <item><description><b>Legacy Kind</b>: <c>timedBar</c> for a timed offer; for an untimed offer <c>plate</c> for the cheapest resource
    /// among the untimed offers and <c>gear</c> for every other resource. The Kind only labels the offer in the F003 views.</description></item>
    /// </list>
    /// </summary>
    internal static class RunRewardAdapter
    {
        internal const int TableEntries = 1024;

        /// <summary><c>ceil(quantity[i] x basePrice x ratioPpm / 10^6)</c> for every entry, or false when a step leaves the exact range.</summary>
        internal static bool TryCashTable(IReadOnlyList<long> quantity, long basePrice, long ratioPpm, out long[] table)
        {
            table = null;
            if (quantity == null || quantity.Count != TableEntries || basePrice < 0 || basePrice > ExactBound.Value ||
                ratioPpm < 0 || ratioPpm > ExactBound.Value)
            {
                return false;
            }

            var result = new long[TableEntries];
            for (var i = 0; i < TableEntries; i++)
            {
                long market;
                if (!CheckedMath.TryMultiply(quantity[i], basePrice, out market) ||
                    !MulDiv.MulDivCeil(market, ratioPpm, Curve.Ppm, out result[i]) ||
                    !ExactBound.Contains(result[i]))
                {
                    return false;
                }
            }

            table = result;
            return true;
        }

        /// <summary>The legacy <c>ContractOfferDef.Kind</c> of every contract of <paramref name="doc"/>, in document order.</summary>
        internal static string[] DeriveKinds(RunContentDocumentDto doc)
        {
            var kinds = new string[doc.Contracts.Count];
            var prices = ResourcePrices(doc);
            var untimedResources = doc.Contracts
                .Where(c => c != null && !c.DurationMs.HasValue && c.ResourceId != null)
                .Select(c => c.ResourceId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => PriceOf(prices, id))
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToList();
            for (var i = 0; i < kinds.Length; i++)
            {
                var contract = doc.Contracts[i];
                if (contract == null)
                {
                    continue;
                }

                kinds[i] = contract.DurationMs.HasValue
                    ? "timedBar"
                    : untimedResources.IndexOf(contract.ResourceId) <= 0 ? "plate" : "gear";
            }

            return kinds;
        }

        /// <summary>The Item reward definitions of every contract of <paramref name="doc"/> (see the class remarks), in document order.</summary>
        internal static List<ContractRewardDef>[] ItemRewards(RunContentDocumentDto doc)
        {
            var result = new List<ContractRewardDef>[doc.Contracts.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = new List<ContractRewardDef>();
            }

            var ladder = doc.ContractItemLadder;
            if (ladder == null || ladder.Entries == null || ladder.Entries.Count == 0)
            {
                return result;
            }

            var prices = ResourcePrices(doc);
            var owners = Enumerable.Range(0, doc.Contracts.Count)
                .Where(i => doc.Contracts[i] != null && doc.Contracts[i].Objective == "deliver" && doc.Contracts[i].Class == "standing")
                .OrderBy(i => PriceOf(prices, doc.Contracts[i].ResourceId))
                .ThenBy(i => doc.Contracts[i].Id, StringComparer.Ordinal)
                .ToList();
            if (owners.Count == 0)
            {
                return result;
            }

            var tiers = doc.Items.ToDictionary(item => item.Id, item => item.Tier, StringComparer.Ordinal);
            var grouped = new SortedDictionary<string, SortedSet<int>>(StringComparer.Ordinal);
            foreach (var entry in ladder.Entries)
            {
                var tier = tiers[entry.ItemId];
                var owner = owners[Math.Min(Math.Max(tier, 1) - 1, owners.Count - 1)];
                var key = owner.ToString("D5", System.Globalization.CultureInfo.InvariantCulture) + "|" + entry.ItemId;
                if (!grouped.TryGetValue(key, out var indices))
                {
                    grouped[key] = indices = new SortedSet<int>();
                }

                indices.Add(checked((int)entry.Index));
            }

            foreach (var pair in grouped)
            {
                var separator = pair.Key.IndexOf('|');
                var owner = int.Parse(pair.Key.Substring(0, separator), System.Globalization.CultureInfo.InvariantCulture);
                result[owner].Add(new ContractRewardDef("item", null, pair.Key.Substring(separator + 1), pair.Value));
            }

            return result;
        }

        private static Dictionary<string, long> ResourcePrices(RunContentDocumentDto doc)
        {
            var prices = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var resource in doc.Resources)
            {
                prices[resource.Id] = resource.BasePrice;
            }

            return prices;
        }

        private static long PriceOf(Dictionary<string, long> prices, string resourceId)
        {
            return resourceId != null && prices.TryGetValue(resourceId, out var price) ? price : long.MaxValue;
        }
    }
}
