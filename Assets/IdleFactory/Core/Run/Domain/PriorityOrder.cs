using System;
using System.Collections.Generic;

namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// Ordered list that is a permutation of the current machine instance IDs. A new machine is appended, a
    /// removed one is deleted, and reordering is a permutation command (all-or-nothing). Append and remove are
    /// internal so that only <see cref="RunState"/> can change membership and keep it equal to the machine set.
    /// </summary>
    public sealed class PriorityOrder
    {
        private readonly List<Guid> _ids = new List<Guid>();

        public int Count => _ids.Count;
        public Guid this[int index] => _ids[index];
        public IReadOnlyList<Guid> Ids => _ids;
        public bool Contains(Guid id) => _ids.Contains(id);
        public int IndexOf(Guid id) => _ids.IndexOf(id);
        public Guid[] ToArray() => _ids.ToArray();

        internal RunStateResult TryAppend(Guid id)
        {
            if (id == Guid.Empty) return RunStateResult.InvalidArgument;
            if (_ids.Contains(id)) return RunStateResult.DuplicateInstance;
            _ids.Add(id);
            return RunStateResult.Ok;
        }

        internal RunStateResult TryRemove(Guid id)
        {
            int i = _ids.IndexOf(id);
            if (i < 0) return RunStateResult.UnknownInstance;
            _ids.RemoveAt(i);
            return RunStateResult.Ok;
        }

        /// <summary>Replaces the order with <paramref name="permutation"/>, which must contain exactly the current IDs.</summary>
        public RunStateResult TryReorder(IReadOnlyList<Guid> permutation)
        {
            if (permutation == null || permutation.Count != _ids.Count) return RunStateResult.NotPermutation;
            var seen = new bool[_ids.Count];
            for (int i = 0; i < permutation.Count; i++)
            {
                int at = _ids.IndexOf(permutation[i]);
                if (at < 0 || seen[at]) return RunStateResult.NotPermutation;
                seen[at] = true;
            }
            for (int i = 0; i < permutation.Count; i++) _ids[i] = permutation[i];
            return RunStateResult.Ok;
        }
    }
}
