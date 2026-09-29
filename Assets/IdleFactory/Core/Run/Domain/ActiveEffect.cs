using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;

namespace IdleFactory.Run.Domain
{
    /// <summary>Conflict identity of an effect: <c>(effectKind, targetKey)</c>, compared ordinally.</summary>
    public readonly struct ConflictKey : IEquatable<ConflictKey>
    {
        public string EffectKind { get; }
        public string TargetKey { get; }
        public ConflictKey(string effectKind, string targetKey)
        {
            if (string.IsNullOrEmpty(effectKind)) throw new ArgumentException("Effect kind is required.", nameof(effectKind));
            if (string.IsNullOrEmpty(targetKey)) throw new ArgumentException("Target key is required.", nameof(targetKey));
            EffectKind = effectKind;
            TargetKey = targetKey;
        }

        public bool Equals(ConflictKey other) =>
            string.Equals(EffectKind, other.EffectKind, StringComparison.Ordinal) &&
            string.Equals(TargetKey, other.TargetKey, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ConflictKey other && Equals(other);
        public override int GetHashCode() => (EffectKind ?? string.Empty).GetHashCode() * 31 + (TargetKey ?? string.Empty).GetHashCode();

        public static int Compare(ConflictKey a, ConflictKey b)
        {
            int c = string.CompareOrdinal(a.EffectKind, b.EffectKind);
            return c != 0 ? c : string.CompareOrdinal(a.TargetKey, b.TargetKey);
        }
    }

    /// <summary>A running Item effect: <c>[StartUtcMs, EndUtcMs)</c> with <c>EndUtcMs = StartUtcMs + Item.DurationMs</c>.</summary>
    public sealed class ActiveEffect
    {
        public string SourceId { get; }
        public ConflictKey ConflictKey { get; }
        public long StartUtcMs { get; }
        public long EndUtcMs { get; }

        public ActiveEffect(string sourceId, ConflictKey conflictKey, long startUtcMs, long endUtcMs)
        {
            if (string.IsNullOrEmpty(sourceId)) throw new ArgumentException("Source ID is required.", nameof(sourceId));
            if (string.IsNullOrEmpty(conflictKey.EffectKind)) throw new ArgumentException("Conflict key is required.", nameof(conflictKey));
            if (startUtcMs < 0 || endUtcMs <= startUtcMs) throw new ArgumentOutOfRangeException(nameof(endUtcMs));
            SourceId = sourceId;
            ConflictKey = conflictKey;
            StartUtcMs = startUtcMs;
            EndUtcMs = endUtcMs;
        }
    }

    /// <summary>
    /// Active effects with at most one effect per <see cref="ConflictKey"/>, kept in a fixed order: source ID,
    /// then effect kind, then target key (ordinal). Speed composition applies items in this source-ID order.
    /// </summary>
    public sealed class ActiveEffectSet
    {
        private readonly List<ActiveEffect> _effects = new List<ActiveEffect>();

        public IReadOnlyList<ActiveEffect> Effects => _effects;
        public int Count => _effects.Count;

        public bool ContainsKey(ConflictKey key)
        {
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i].ConflictKey.Equals(key)) return true;
            return false;
        }

        public RunStateResult TryAdd(ActiveEffect effect)
        {
            if (effect == null) return RunStateResult.InvalidArgument;
            if (ContainsKey(effect.ConflictKey)) return RunStateResult.EffectConflict;
            int at = _effects.Count;
            for (int i = 0; i < _effects.Count; i++)
            {
                if (Compare(effect, _effects[i]) < 0) { at = i; break; }
            }
            _effects.Insert(at, effect);
            return RunStateResult.Ok;
        }

        /// <summary>Removes every effect whose <c>EndUtcMs &lt;= nowUtcMs</c>; returns how many were removed.</summary>
        public int RemoveExpired(long nowUtcMs)
        {
            return _effects.RemoveAll(e => e.EndUtcMs <= nowUtcMs);
        }

        private static int Compare(ActiveEffect a, ActiveEffect b)
        {
            int c = string.CompareOrdinal(a.SourceId, b.SourceId);
            return c != 0 ? c : ConflictKey.Compare(a.ConflictKey, b.ConflictKey);
        }
    }
}
