using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Domain
{
    /// <summary>What one completion of the active contract grants, computed by <see cref="ContractRewards.CheckGrant"/>.</summary>
    internal readonly struct ContractRewardPlan
    {
        /// <summary>The Cash reward, <c>MulDivFloor(SalePricing value of the required units, rewardRatioPpm, 10^6)</c>.</summary>
        public long Cash { get; }

        /// <summary>The RP reward at the offer's own completion count (the offer's <c>rpReward</c> curve).</summary>
        public long Rp { get; }

        /// <summary>The pre-increment run-wide completion index; the Items due are the ladder entries at this index.</summary>
        public long RunIndex { get; }

        public ContractRewardPlan(long cash, long rp, long runIndex)
        {
            Cash = cash;
            Rp = rp;
            RunIndex = runIndex;
        }
    }

    /// <summary>
    /// T320 (F5-R04 GD-9, FR-007 to FR-009, ECO-1, ECO-2, ECO-11): the one contract reward. A completion grants
    /// <list type="bullet">
    /// <item><description><b>Cash</b> <c>MulDivFloor(SalePricing.TryValue(required, basePrice, salePricePpm), rewardRatioPpm, 10^6)</c>: one
    /// sale-basis floor and then one ratio floor, the same expression for manual and automatic delivery and for production objectives.
    /// <c>salePricePpm</c> is the sale-price modifier read at the completion instant (FR-009), not at acceptance; <c>required</c> is fixed at
    /// acceptance. The sale basis is <see cref="SalePricing"/>, which is not duplicated here.</description></item>
    /// <item><description><b>RP</b>: the offer's <c>rp</c> reward curve at the offer's own completion count (unchanged since F003).</description></item>
    /// <item><description><b>Items</b>: the ladder entries (<see cref="ContractItemLadder"/>) whose <c>Index</c> equals the pre-increment
    /// run-wide completion index (<see cref="ContractBoard.RunCompletionTotal"/>, every offer and every completion path counted), up to
    /// <see cref="ContractItemLadder.PerRunCap"/>. The cap counts ladder entries in (index, Item ID) order: an entry is granted when
    /// fewer than <c>PerRunCap</c> entries precede it, which is exactly the number of Items granted earlier in the run because every index is
    /// passed once. No grant counter is stored, so nothing is persisted for the cap and a loaded run grants the same Items. Indices
    /// from 1024 on never grant (the ladder holds indices 0 to 1,023). Items due at one index are returned in ordinal Item-ID order.</description></item>
    /// </list>
    ///
    /// State ownership: none. Pure and stateless; <see cref="CheckGrant"/> reads and changes nothing, <see cref="Grant"/> writes Cash, RP and
    /// the permanent Item stacks through the owning objects after a successful check and cannot fail then. Determinism: integer-only, checked,
    /// bounded by 10^18; no allocation except the arrays of <see cref="DueItems"/> (command and snapshot paths, not the simulator).
    /// </summary>
    internal static class ContractRewards
    {
        /// <summary>Ladder indices and per-offer reward tables are bounded by 1,024 entries.</summary>
        internal const int IndexLimit = 1024;

        /// <summary>
        /// The Cash of a completion. False, with <paramref name="cash"/> 0, when an operand leaves 0 to 10^18, the sale basis
        /// <c>required x basePrice</c> does, or the floored reward does. Nothing is clamped.
        /// </summary>
        internal static bool TryCash(long requiredUnits, long basePrice, long salePricePpm, long rewardRatioPpm, out long cash)
        {
            cash = 0;
            return SalePricing.TryValue(requiredUnits, basePrice, salePricePpm, out long basis) &&
                   MulDiv.MulDivFloor(basis, rewardRatioPpm, SalePricing.Ppm, out cash);
        }

        /// <summary>The RP of a completion: the sum of the offer's <c>rp</c> rewards at <paramref name="completionCount"/>. False on overflow or an index outside the table.</summary>
        internal static bool TryRp(ContractOfferDef offer, long completionCount, out long rp)
        {
            rp = 0;
            if (completionCount < 0 || completionCount >= IndexLimit) return false;
            for (int i = 0; i < offer.Rewards.Count; i++)
            {
                ContractRewardDef reward = offer.Rewards[i];
                if (reward.Kind == "rp" && !CheckedMath.TryAdd(rp, reward.ValueTable[(int)completionCount], out rp)) return false;
            }
            return true;
        }

        /// <summary>The number of Items the ladder grants at <paramref name="runIndex"/>, after the per-run cap.</summary>
        internal static int DueItemCount(ContractItemLadder ladder, long runIndex)
        {
            if (ladder == null || runIndex < 0 || runIndex >= IndexLimit) return 0;
            int due = 0;
            for (int i = 0; i < ladder.Entries.Count; i++)
                if (ladder.Entries[i].Index == runIndex && IsWithinCap(ladder, ladder.Entries[i])) due++;
            return due;
        }

        /// <summary>
        /// The Item ID of the <paramref name="ordinal"/>-th Item due at <paramref name="runIndex"/> in ordinal Item-ID order
        /// (0 to <see cref="DueItemCount"/> - 1), or null.
        /// </summary>
        internal static string DueItemId(ContractItemLadder ladder, long runIndex, int ordinal)
        {
            if (ladder == null || runIndex < 0 || runIndex >= IndexLimit || ordinal < 0) return null;
            for (int i = 0; i < ladder.Entries.Count; i++)
            {
                ContractItemLadderEntry entry = ladder.Entries[i];
                if (entry.Index != runIndex || !IsWithinCap(ladder, entry)) continue;
                int smaller = 0;
                for (int j = 0; j < ladder.Entries.Count; j++)
                {
                    ContractItemLadderEntry other = ladder.Entries[j];
                    if (other.Index == runIndex && IsWithinCap(ladder, other) && string.CompareOrdinal(other.ItemId, entry.ItemId) < 0) smaller++;
                }
                if (smaller == ordinal) return entry.ItemId;
            }
            return null;
        }

        /// <summary>The Items due at <paramref name="runIndex"/>, one of each, in ordinal Item-ID order; empty when none is due.</summary>
        internal static IdCount[] DueItems(ContractItemLadder ladder, long runIndex)
        {
            var due = new IdCount[DueItemCount(ladder, runIndex)];
            for (int n = 0; n < due.Length; n++) due[n] = new IdCount(DueItemId(ladder, runIndex, n), 1);
            return due;
        }

        /// <summary>True when fewer than <c>PerRunCap</c> ladder entries precede <paramref name="entry"/> in (index, Item ID) order.</summary>
        private static bool IsWithinCap(ContractItemLadder ladder, ContractItemLadderEntry entry)
        {
            long before = 0;
            for (int i = 0; i < ladder.Entries.Count; i++)
            {
                ContractItemLadderEntry other = ladder.Entries[i];
                if (other.Index < entry.Index ||
                    (other.Index == entry.Index && string.CompareOrdinal(other.ItemId, entry.ItemId) < 0))
                {
                    if (++before >= ladder.PerRunCap) return false;
                }
            }
            return before < ladder.PerRunCap;
        }

        /// <summary>
        /// Validates the completion of the active contract of <paramref name="state"/> without changing anything: the Cash and RP rewards
        /// (value.limit when one passes 10^18 or would push Cash or RP past it), and the Items due at the current run-wide index (the Item
        /// stacks must take them; <see cref="RunStateResult.InvalidArgument"/> when one is due and <paramref name="permanent"/> is null).
        /// <paramref name="salePricePpm"/> is the sale-price modifier of the offer's resource at this instant.
        /// </summary>
        internal static RunStateResult CheckGrant(RunState state, PermanentState permanent, long salePricePpm, out ContractRewardPlan plan)
        {
            plan = default(ContractRewardPlan);
            if (state == null) return RunStateResult.InvalidArgument;
            return TryPlan(state, permanent, salePricePpm, state.Cash, state.Rp, out plan);
        }

        /// <summary>
        /// <see cref="CheckGrant"/> against explicit Cash and RP balances instead of the ones of <paramref name="state"/>: the one reward plan of the
        /// active contract, shared by the commands (the balances of the state) and the simulator (T326, which holds scratch balances and calls it once
        /// per advance with 0 and 0 to validate the reward itself and the Item stacks, then adds the plan to its own balances per attempt).
        /// The reward arithmetic, the checks and their order are those of <see cref="CheckGrant"/>.
        /// </summary>
        internal static RunStateResult TryPlan(RunState state, PermanentState permanent, long salePricePpm, long cashNow, long rpNow,
            out ContractRewardPlan plan)
        {
            plan = default(ContractRewardPlan);
            if (state == null) return RunStateResult.InvalidArgument;
            ContractBoard board = state.Contracts;
            ActiveContract active = board.Active;
            if (active == null) return RunStateResult.ContractNoneActive;
            RunCatalog catalog = state.Catalog;
            if (!catalog.ContractOffers.TryGetValue(active.OfferId, out ContractOfferDef offer) ||
                !catalog.Resources.TryGetValue(offer.ResourceId, out ResourceDef resource))
                return RunStateResult.InvalidArgument;
            if (!TryCash(active.RequiredUnits, resource.BasePrice, salePricePpm, offer.RewardRatioPpm, out long cash) ||
                !TryRp(offer, board.CompletionCount(active.OfferId), out long rp))
                return RunStateResult.ValueLimit;
            if (!CheckedMath.TryAdd(cashNow, cash, out _) || !CheckedMath.TryAdd(rpNow, rp, out _)) return RunStateResult.ValueLimit;

            long runIndex = board.RunCompletionTotal;
            int due = DueItemCount(catalog.ContractItemLadder, runIndex);
            for (int n = 0; n < due; n++)
            {
                if (permanent == null) return RunStateResult.InvalidArgument;
                RunStateResult item = permanent.CheckAddItems(DueItemId(catalog.ContractItemLadder, runIndex, n), 1);
                if (item != RunStateResult.Ok) return item;
            }
            plan = new ContractRewardPlan(cash, rp, runIndex);
            return RunStateResult.Ok;
        }

        /// <summary>
        /// Applies a plan returned by <see cref="CheckGrant"/>: Cash, RP and the due Items. Call it only with a plan from a successful
        /// check on the unchanged state; it then cannot fail. Run it before the completion is counted, so the plan's run index stays the
        /// pre-increment one.
        /// </summary>
        internal static RunStateResult Grant(RunState state, PermanentState permanent, in ContractRewardPlan plan)
        {
            if (state == null) return RunStateResult.InvalidArgument;
            RunStateResult result = state.TryAddCash(plan.Cash);
            if (result != RunStateResult.Ok) return result;
            result = state.TryAddRp(plan.Rp);
            if (result != RunStateResult.Ok) return result;
            ContractItemLadder ladder = state.Catalog.ContractItemLadder;
            int due = DueItemCount(ladder, plan.RunIndex);
            for (int n = 0; n < due; n++)
            {
                if (permanent == null) return RunStateResult.InvalidArgument;
                result = permanent.TryAddItems(DueItemId(ladder, plan.RunIndex, n), 1);
                if (result != RunStateResult.Ok) return result;
            }
            return RunStateResult.Ok;
        }
    }
}
