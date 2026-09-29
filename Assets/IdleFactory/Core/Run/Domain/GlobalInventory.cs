using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// <c>resourceId -> units</c>. Invariants: every entry is at least 0, the sum of all units never exceeds
    /// <see cref="Capacity"/>, and only allowed (unlocked or mapped) resource IDs appear. The only mutations of
    /// the units are the atomic exchange, a sale, a delivery and an import; each is checked and rejected without
    /// any partial change. Entries are kept in ordinal ID order (GD-6).
    /// </summary>
    public sealed class GlobalInventory
    {
        private string[] _ids;
        private long[] _units;
        private long _total;

        public GlobalInventory(IEnumerable<string> allowedResourceIds, long capacity)
        {
            if (allowedResourceIds == null) throw new ArgumentNullException(nameof(allowedResourceIds));
            if (capacity < 1 || capacity > ExactBound.Value) throw new ArgumentOutOfRangeException(nameof(capacity));
            var ids = new List<string>();
            foreach (string id in allowedResourceIds)
            {
                if (string.IsNullOrEmpty(id)) throw new ArgumentException("Invalid resource ID.", nameof(allowedResourceIds));
                ids.Add(id);
            }
            ids.Sort(StringComparer.Ordinal);
            for (int i = 1; i < ids.Count; i++)
                if (string.Equals(ids[i - 1], ids[i], StringComparison.Ordinal))
                    throw new ArgumentException("Duplicate resource ID.", nameof(allowedResourceIds));
            _ids = ids.ToArray();
            _units = new long[_ids.Length];
            Capacity = capacity;
        }

        public long Capacity { get; private set; }
        public long Total => _total;
        public long Free => Capacity - _total;
        public int Count => _ids.Length;
        public string IdAt(int index) => _ids[index];
        public long UnitsAt(int index) => _units[index];

        public bool IsAllowed(string resourceId) => IndexOf(resourceId) >= 0;

        /// <summary>Units in stock; 0 for an unknown resource.</summary>
        public long Units(string resourceId)
        {
            int i = IndexOf(resourceId);
            return i >= 0 ? _units[i] : 0;
        }

        /// <summary>Adds an allowed resource ID (an unlock). Returns false when it was already present.</summary>
        internal bool Allow(string resourceId)
        {
            int i = IndexOf(resourceId);
            if (i >= 0) return false;
            i = ~i;
            var ids = new string[_ids.Length + 1];
            var units = new long[_units.Length + 1];
            Array.Copy(_ids, 0, ids, 0, i);
            Array.Copy(_units, 0, units, 0, i);
            ids[i] = resourceId;
            Array.Copy(_ids, i, ids, i + 1, _ids.Length - i);
            Array.Copy(_units, i, units, i + 1, _units.Length - i);
            _ids = ids;
            _units = units;
            return true;
        }

        /// <summary>Changes the capacity (storage upgrade). Rejected when it would drop below the stock.</summary>
        internal RunStateResult TrySetCapacity(long capacity)
        {
            if (capacity < 1 || capacity > ExactBound.Value) return RunStateResult.ValueLimit;
            if (capacity < _total) return RunStateResult.CapacityExceeded;
            Capacity = capacity;
            return RunStateResult.Ok;
        }

        /// <summary>
        /// Atomic exchange of <paramref name="units"/> recipe units: removes <c>units x inputs</c> and adds
        /// <c>units x outputs x outputFactor</c> in one step. Rejected as a whole if any input is missing, the
        /// result would exceed the capacity, or any product overflows.
        /// </summary>
        public RunStateResult TryExchange(IReadOnlyList<ResourceAmount> inputs, IReadOnlyList<ResourceAmount> outputs,
            long units, long outputFactor)
        {
            if (inputs == null || outputs == null || units < 1 || outputFactor < 1) return RunStateResult.InvalidArgument;
            return Apply(inputs, units, 1, outputs, units, outputFactor);
        }

        /// <summary>Removes stock for a sale. The Cash credit is the caller's part of the same transaction.</summary>
        public RunStateResult TrySell(string resourceId, long units) => Remove(resourceId, units);

        /// <summary>Removes stock for a contract delivery.</summary>
        public RunStateResult TryDeliver(string resourceId, long units) => Remove(resourceId, units);

        /// <summary>Adds stock from a Save V1 import (atomic for the whole list).</summary>
        public RunStateResult TryImport(IReadOnlyList<ResourceAmount> amounts)
        {
            if (amounts == null) return RunStateResult.InvalidArgument;
            return Apply(Array.Empty<ResourceAmount>(), 1, 1, amounts, 1, 1);
        }

        private RunStateResult Remove(string resourceId, long units)
        {
            if (resourceId == null || units < 1) return RunStateResult.InvalidArgument;
            int i = IndexOf(resourceId);
            if (i < 0) return RunStateResult.ResourceLocked;
            if (units > ExactBound.Value) return RunStateResult.ValueLimit;
            if (_units[i] < units) return RunStateResult.StockInsufficient;
            _units[i] -= units;
            _total -= units;
            return RunStateResult.Ok;
        }

        private RunStateResult Apply(IReadOnlyList<ResourceAmount> remove, long removeUnits, long removeFactor,
            IReadOnlyList<ResourceAmount> add, long addUnits, long addFactor)
        {
            var next = (long[])_units.Clone();
            for (int r = 0; r < remove.Count; r++)
            {
                int i = IndexOf(remove[r].ResourceId);
                if (i < 0) return RunStateResult.ResourceLocked;
                if (!TryAmount(remove[r].Amount, removeUnits, removeFactor, out long amount)) return RunStateResult.ValueLimit;
                if (next[i] < amount) return RunStateResult.StockInsufficient;
                next[i] -= amount;
            }

            for (int a = 0; a < add.Count; a++)
            {
                int i = IndexOf(add[a].ResourceId);
                if (i < 0) return RunStateResult.ResourceLocked;
                if (!TryAmount(add[a].Amount, addUnits, addFactor, out long amount)) return RunStateResult.ValueLimit;
                if (!CheckedMath.TryAdd(next[i], amount, out long sum)) return RunStateResult.ValueLimit;
                next[i] = sum;
            }

            if (!CheckedMath.TrySum(next, out long total)) return RunStateResult.ValueLimit;
            if (total > Capacity) return RunStateResult.CapacityExceeded;
            _units = next;
            _total = total;
            return RunStateResult.Ok;
        }

        private static bool TryAmount(long amount, long a, long b, out long result)
        {
            return CheckedMath.TryMultiply(amount, a, out result) && CheckedMath.TryMultiply(result, b, out result);
        }

        private int IndexOf(string resourceId)
        {
            if (resourceId == null) return -1;
            int lo = 0, hi = _ids.Length - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                int c = string.CompareOrdinal(_ids[mid], resourceId);
                if (c == 0) return mid;
                if (c < 0) lo = mid + 1; else hi = mid - 1;
            }
            return ~lo;
        }
    }
}
