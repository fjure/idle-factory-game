using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Simulation
{
    /// <summary>
    /// The one value that maps simulated ticks (100 ns) to real UTC milliseconds (F5-R05, AR-1, GD-1, AR2-1). It is shared
    /// by <c>RunSession.TimelineUtcMs</c>, <see cref="OfflineSegmenter"/> and the mapping overload of
    /// <see cref="RunSimulator.Advance"/>, so every stamp of the run is produced by the same arithmetic.
    ///
    /// Fields: <see cref="OriginUtcMs"/> is the real instant of simulated offset 0; <see cref="OriginTickPhase"/>
    /// (<c>0 &lt;= phase &lt; 10 000</c>) is the sub-millisecond remainder of that instant in ticks; the ratio
    /// <see cref="RealPerSimNumerator"/> / <see cref="RealPerSimDenominator"/> is the real time that one simulated tick
    /// takes: <c>1/1</c> live and the inverse of the offline efficiency for an offline segment (<c>5/4</c> at 80 %).
    ///
    /// The only rounding rule, a single floor over the exact rational:
    /// <c>RealUtc(t) = OriginUtcMs + floor((OriginTickPhase + t x num / den) / 10 000)</c>. The inner quotient is floored
    /// first, which gives the same result as one floor over the whole expression because the outer divisor is an
    /// integer. All arithmetic is checked (quantities up to 10^18, 128-bit intermediate, no floating point); an
    /// unrepresentable stamp is reported as false by <see cref="TryRealUtcMs"/>.
    ///
    /// Live frames carry the sub-millisecond phase forward (<see cref="TryLiveAt"/>), so a live advance and any partition
    /// of it give identical stamps. The mapping is a value type: it is built per advance and evaluated per stamp, never
    /// per completion (PF-6); the live case (<c>num == den</c>) costs one comparison and two checked additions.
    /// A <c>default</c> value is invalid (zero ratio) and rejected by <see cref="RunSimulator"/>.
    /// </summary>
    public readonly struct TimelineMapping : IEquatable<TimelineMapping>
    {
        /// <summary>Simulation ticks (100 ns) per real millisecond.</summary>
        public const long TicksPerMs = 10_000;

        /// <summary>The real instant (Unix ms) of simulated offset 0.</summary>
        public long OriginUtcMs { get; }

        /// <summary>The sub-millisecond remainder of the origin in ticks, <c>0 ... 9 999</c>.</summary>
        public long OriginTickPhase { get; }

        /// <summary>Numerator of the real time per simulated tick.</summary>
        public long RealPerSimNumerator { get; }

        /// <summary>Denominator of the real time per simulated tick.</summary>
        public long RealPerSimDenominator { get; }

        /// <summary>
        /// Creates a mapping. The ratio must be positive and the phase inside one millisecond; the origin is not range
        /// checked here (a stamp that leaves the exact bound is reported by <see cref="TryRealUtcMs"/>).
        /// </summary>
        public TimelineMapping(long originUtcMs, long originTickPhase, long realPerSimNumerator, long realPerSimDenominator)
        {
            if (originTickPhase < 0 || originTickPhase >= TicksPerMs) throw new ArgumentOutOfRangeException(nameof(originTickPhase));
            if (realPerSimNumerator <= 0) throw new ArgumentOutOfRangeException(nameof(realPerSimNumerator));
            if (realPerSimDenominator <= 0) throw new ArgumentOutOfRangeException(nameof(realPerSimDenominator));
            OriginUtcMs = originUtcMs;
            OriginTickPhase = originTickPhase;
            RealPerSimNumerator = realPerSimNumerator;
            RealPerSimDenominator = realPerSimDenominator;
        }

        /// <summary>True for a constructed mapping; false for <c>default</c>.</summary>
        public bool IsValid => RealPerSimNumerator > 0 && RealPerSimDenominator > 0;

        /// <summary>The live mapping, <c>1/1</c>, with its origin at a whole real millisecond.</summary>
        public static TimelineMapping Live(long originUtcMs) => new TimelineMapping(originUtcMs, 0, 1, 1);

        /// <summary>The live mapping with its origin at a whole millisecond plus a sub-millisecond phase in ticks.</summary>
        public static TimelineMapping Live(long originUtcMs, long originTickPhase) => new TimelineMapping(originUtcMs, originTickPhase, 1, 1);

        /// <summary>
        /// The live mapping at the instant that lies <paramref name="activeTicks"/> after the whole-millisecond anchor:
        /// the origin is <c>anchor + activeTicks / 10 000</c> and the phase <c>activeTicks % 10 000</c>, so the phase of the
        /// run timeline is carried forward. False when the origin leaves the exact bound.
        /// </summary>
        public static bool TryLiveAt(long anchorUtcMs, long activeTicks, out TimelineMapping mapping)
        {
            mapping = default;
            if (activeTicks < 0 || !CheckedMath.TryAdd(anchorUtcMs, activeTicks / TicksPerMs, out long origin)) return false;
            mapping = new TimelineMapping(origin, activeTicks % TicksPerMs, 1, 1);
            return true;
        }

        /// <summary>
        /// The mapping of an offline segment that starts at the departure: the origin is the departure, the phase 0 and
        /// the ratio the inverse of the policy's efficiency (<c>5/4</c> for 4/5). Effective ticks <c>E(b)</c> of a real
        /// boundary <c>b</c> inside the cap map back to exactly <c>b</c>.
        /// </summary>
        public static TimelineMapping Offline(RunOfflinePolicy policy, long departureUtcMs)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            return new TimelineMapping(departureUtcMs, 0, policy.EfficiencyDenominator, policy.EfficiencyNumerator);
        }

        /// <summary>
        /// <c>RealUtc(t)</c> in Unix ms for <paramref name="simTicks"/> simulated ticks after the origin (the single floor
        /// rule, see the type remarks). False, with the output 0, for an invalid mapping, negative ticks or a stamp
        /// that does not fit the exact bound.
        /// </summary>
        public bool TryRealUtcMs(long simTicks, out long utcMs)
        {
            utcMs = 0;
            if (simTicks < 0 || !IsValid) return false;
            long scaled = simTicks;
            if (RealPerSimNumerator != RealPerSimDenominator &&
                !MulDiv.MulDivFloor(simTicks, RealPerSimNumerator, RealPerSimDenominator, out scaled))
                return false;
            if (!CheckedMath.TryAdd(OriginTickPhase, scaled, out long ticks)) return false;
            return CheckedMath.TryAdd(OriginUtcMs, ticks / TicksPerMs, out utcMs);
        }

        /// <summary><see cref="TryRealUtcMs"/> for a caller that has already bounded its inputs; throws when the stamp is not representable.</summary>
        public long RealUtcMs(long simTicks)
        {
            if (!TryRealUtcMs(simTicks, out long utcMs)) throw new OverflowException("The timeline stamp is not representable.");
            return utcMs;
        }

        public bool Equals(TimelineMapping other) =>
            OriginUtcMs == other.OriginUtcMs && OriginTickPhase == other.OriginTickPhase &&
            RealPerSimNumerator == other.RealPerSimNumerator && RealPerSimDenominator == other.RealPerSimDenominator;

        public override bool Equals(object obj) => obj is TimelineMapping other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                long h = OriginUtcMs;
                h = h * 31 + OriginTickPhase;
                h = h * 31 + RealPerSimNumerator;
                h = h * 31 + RealPerSimDenominator;
                return (int)(h ^ (h >> 32));
            }
        }
    }
}
