using System.Collections.Generic;

namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// Sorted (ordinal) id-to-count list. Iteration order is the ordinal key order, never hash order (GD-6).
    /// </summary>
    internal sealed class OrdinalCounts
    {
        private readonly List<string> _keys = new List<string>();
        private readonly List<long> _values = new List<long>();

        public int Count => _keys.Count;
        public string KeyAt(int index) => _keys[index];
        public long ValueAt(int index) => _values[index];

        public int IndexOf(string key)
        {
            int lo = 0, hi = _keys.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                int c = string.CompareOrdinal(_keys[mid], key);
                if (c == 0) return mid;
                if (c < 0) lo = mid + 1; else hi = mid - 1;
            }
            return ~lo;
        }

        public long Get(string key)
        {
            int i = IndexOf(key);
            return i >= 0 ? _values[i] : 0;
        }

        /// <summary>Sets a value; a zero value removes the key when <paramref name="removeZero"/> is set.</summary>
        public void Set(string key, long value, bool removeZero)
        {
            int i = IndexOf(key);
            if (i >= 0)
            {
                if (removeZero && value == 0) { _keys.RemoveAt(i); _values.RemoveAt(i); }
                else _values[i] = value;
                return;
            }
            if (removeZero && value == 0) return;
            i = ~i;
            _keys.Insert(i, key);
            _values.Insert(i, value);
        }

        public IdCount[] ToArray()
        {
            var result = new IdCount[_keys.Count];
            for (int i = 0; i < result.Length; i++) result[i] = new IdCount(_keys[i], _values[i]);
            return result;
        }
    }
}
