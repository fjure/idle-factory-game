using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// T360 (F5-R02, Option 1): a recipe resolved against one <see cref="GlobalInventory"/> shape. It holds the
    /// inventory position and amount of every input and output in plain arrays, the checked input and output sums
    /// for the current output factor, and which output shares its resource with an input. The run simulator's
    /// completion pass uses it for the unit-fit computation (<see cref="TryFit"/>) and the exchange
    /// (<see cref="GlobalInventory.TryExchangeIndexed"/>) without interface enumeration, string lookups or
    /// per-call recomputation. The arithmetic is exactly that of <c>SpeedComposer.TryCompletedUnits</c> and
    /// <c>GlobalInventory.TryExchange</c>, which stay the command path; it is checked integer arithmetic only.
    ///
    /// State ownership: one instance per staged machine of a <c>RunSimulator</c>, owned and rebuilt by it whenever
    /// the staged inventory shape changes (<see cref="Rebuild"/>) or the machine's derived factors change
    /// (<see cref="SetFactor"/>). The arrays are reused when the recipe side lengths are unchanged, so a rebuild
    /// after the buffers have grown allocates nothing. Not thread-safe.
    /// </summary>
    internal sealed class RecipeIndex
    {
        internal int[] InIndex = Array.Empty<int>();
        internal long[] InAmount = Array.Empty<long>();
        internal int[] OutIndex = Array.Empty<int>();
        internal long[] OutAmount = Array.Empty<long>();

        /// <summary>For each output, the position in the inputs of the same resource, or -1.</summary>
        internal int[] OutInput = Array.Empty<int>();

        // Scratch of the exchange: the validated amounts, reused by the commit pass.
        internal long[] Take = Array.Empty<long>();
        internal long[] Give = Array.Empty<long>();

        // The inventory this index was resolved against and its resource count at that time. Positions shift when a resource is allowed, so a
        // consumer checks both (IsResolvedAgainst) and fails closed instead of reading a stale position (#429 follow-up 4b).
        private GlobalInventory _resolvedAgainst;
        private int _resolvedCount = -1;

        private long _inputSum;
        private bool _inputSumOverflow;
        private long _outputSum;
        private bool _fault = true;

        /// <summary>
        /// True when <paramref name="stock"/> is the inventory this index was last rebuilt against and its resource count has not changed since
        /// (an unlock inserts a resource and shifts every later position). <see cref="TryFit"/> and
        /// <see cref="GlobalInventory.TryExchangeIndexed"/> require it, so a stale index fails closed instead of touching the wrong resource.
        /// </summary>
        internal bool IsResolvedAgainst(GlobalInventory stock) => ReferenceEquals(stock, _resolvedAgainst) && stock.Count == _resolvedCount;

        /// <summary>Sum of the input amounts per recipe unit (valid when the sum did not overflow).</summary>
        internal long InputSum => _inputSum;

        /// <summary>Sum of the output amounts times the output factor of <see cref="SetFactor"/>.</summary>
        internal long OutputSum => _outputSum;

        /// <summary>
        /// Resolves <paramref name="recipe"/> against <paramref name="stock"/>. A resource the stock does not
        /// allow gets position -1; the owner never completes such a machine (its exchange would be ResourceLocked).
        /// Recipe sides hold no duplicate resource (<c>MachineRecipe</c> rejects them).
        /// </summary>
        internal void Rebuild(MachineRecipe recipe, GlobalInventory stock)
        {
            int inputs = recipe.Inputs.Count;
            int outputs = recipe.Outputs.Count;
            if (InIndex.Length != inputs)
            {
                InIndex = new int[inputs];
                InAmount = new long[inputs];
                Take = new long[inputs];
            }
            if (OutIndex.Length != outputs)
            {
                OutIndex = new int[outputs];
                OutAmount = new long[outputs];
                OutInput = new int[outputs];
                Give = new long[outputs];
            }

            long sum = 0;
            bool overflow = false;
            for (int r = 0; r < inputs; r++)
            {
                ResourceAmount input = recipe.Inputs[r];
                InIndex[r] = stock.PositionOf(input.ResourceId);
                InAmount[r] = input.Amount;
                if (!overflow && !CheckedMath.TryAdd(sum, input.Amount, out sum)) overflow = true;
            }
            for (int a = 0; a < outputs; a++)
            {
                ResourceAmount output = recipe.Outputs[a];
                OutIndex[a] = stock.PositionOf(output.ResourceId);
                OutAmount[a] = output.Amount;
                int shared = -1;
                for (int r = 0; r < inputs; r++)
                    if (string.Equals(recipe.Inputs[r].ResourceId, output.ResourceId, StringComparison.Ordinal)) { shared = r; break; }
                OutInput[a] = shared;
            }
            _inputSum = sum;
            _inputSumOverflow = overflow;
            _resolvedAgainst = stock;
            _resolvedCount = stock.Count;

            // Rebuild keeps the factor-dependent state (_outputSum, _fault) on purpose: it depends on the recipe amounts
            // and the owner's factors, never on inventory positions, so a mid-advance rebuild after an unlock (the
            // positions shift, the recipe does not) must not invalidate it. A first use needs SetFactor: _fault starts
            // true, and the simulator calls SetFactor through RefreshDerived (it sets _configDirty at staging) before
            // any completion pass.
        }

        /// <summary>
        /// Derives the output sum for the machine's current batch and output factor. Any condition for which
        /// <c>SpeedComposer.TryCompletedUnits</c> returns false (batch or factor below 1, an overflow of the input
        /// sum, an output product or the output sum) makes <see cref="TryFit"/> fail.
        /// </summary>
        internal void SetFactor(long batchFactor, long outputFactor)
        {
            _fault = batchFactor < 1 || outputFactor < 1 || _inputSumOverflow;
            long sum = 0;
            for (int a = 0; !_fault && a < OutAmount.Length; a++)
            {
                if (!CheckedMath.TryMultiply(OutAmount[a], outputFactor, out long produced) ||
                    !CheckedMath.TryAdd(sum, produced, out sum))
                    _fault = true;
            }
            _outputSum = sum;
        }

        /// <summary>
        /// OD-9 <c>k' = min(k, k_in, k_cap)</c> of a due cycle, as <c>SpeedComposer.TryCompletedUnits</c>. False
        /// on a checked overflow (or an invalid factor), and for a stock this index was not resolved against
        /// (<see cref="IsResolvedAgainst"/>).
        /// </summary>
        internal bool TryFit(long batchFactor, GlobalInventory stock, out long units)
        {
            units = 0;
            if (_fault || !IsResolvedAgainst(stock)) return false;
            long kIn = long.MaxValue;
            for (int r = 0; r < InIndex.Length; r++)
            {
                // A resource the inventory does not allow (position -1) holds 0 units, as in Units(id) of the command path.
                int at = InIndex[r];
                long fit = at >= 0 ? stock.UnitsAt(at) / InAmount[r] : 0;
                if (fit < kIn) kIn = fit;
            }
            long kCap = long.MaxValue;
            if (_outputSum > _inputSum) kCap = stock.Free / (_outputSum - _inputSum);
            long k = batchFactor;
            if (kIn < k) k = kIn;
            if (kCap < k) k = kCap;
            units = k;
            return true;
        }
    }
}
