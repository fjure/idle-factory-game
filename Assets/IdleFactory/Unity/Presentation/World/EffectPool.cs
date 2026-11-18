using System;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>The effects of the F006 FX family the world pool can play (research R12). The first one outranks every other one.</summary>
    public enum EffectKind
    {
        /// <summary>A machine finished a cycle (FR-029). It outranks the decorative effects.</summary>
        CompletionPop = 0,

        /// <summary>A successful machine upgrade (FR-034). Decorative.</summary>
        UpgradeGlint = 1,

        /// <summary>The Spark of the Smelter and the Press, also the response to a switch-on. Decorative.</summary>
        Spark = 2,

        /// <summary>The Smelter's steam. Decorative.</summary>
        Steam = 3,

        /// <summary>The dust of a new build. Decorative.</summary>
        BuildDust = 4,
    }

    /// <summary>What <see cref="EffectPool.TrySpawn"/> did with a request.</summary>
    public enum EffectSpawnResult
    {
        /// <summary>The effect plays.</summary>
        Spawned = 0,

        /// <summary>Reduced motion is on: no effect ever spawns (FR-034, R12).</summary>
        Reduced = 1,

        /// <summary>The same kind spawned at this location less than <see cref="EffectPool.CoalesceMilliseconds"/> ago.</summary>
        Coalesced = 2,

        /// <summary>Every active renderer plays an effect that is not outranked by this one.</summary>
        NoCapacity = 3,

        /// <summary>The kind has no art (a smaller development catalog), so nothing can be drawn.</summary>
        Unavailable = 4,

        /// <summary>The location is not one of the pool's locations.</summary>
        InvalidLocation = 5,
    }

    /// <summary>The state of one pool renderer.</summary>
    public enum EffectSlotState
    {
        Free = 0,
        Active = 1,

        /// <summary>A cancelled effect plays its remaining frames, fading, instead of popping out (R12).</summary>
        Draining = 2,
    }

    /// <summary>
    /// The world effect pool (T529, F006 research R12, contracts/world-presentation.md section 4): a plain C# scheduler for the 8 renderers of
    /// <see cref="EffectPoolView"/>. It owns no sprite and no GameObject, so Edit Mode proves its rules without a scene.
    /// <list type="bullet">
    /// <item><description><b>8 renderers = 3 active + 5 draining tails.</b> At most <see cref="ActiveLimit"/> effects play at once. A cancelled effect moves to a draining tail (its remaining frames, fading) and keeps its renderer until they are played, so the cap of 3 can churn without a pop-out and without allocating.</description></item>
    /// <item><description><b>A CompletionPop outranks the decorative effects.</b> With 3 active, a pop cancels the oldest decorative one; a decorative effect never cancels anything, and a pop is dropped only when all 3 active effects are pops (a burst the world cannot show anyway).</description></item>
    /// <item><description><b>Coalescing.</b> The same kind at the same location is one effect per <see cref="CoalesceMilliseconds"/>. The timestamps are sized from the layout's location count (<see cref="EnsureLocations"/>), never a fixed 12.</description></item>
    /// <item><description><b>Reduced motion</b> clears everything and spawns nothing. Offline deltas and baseline resets reach the pool as a delta of 0 and spawn nothing (the model reports them so, see <see cref="ConsumeCompletions"/>).</description></item>
    /// <item><description><b>Pace.</b> An effect latches the loop rate of its location when it spawns (the model's rate, already capped per machine family) and keeps it to its last frame, so a pace change shows from the next frame 0, never in the middle of a loop.</description></item>
    /// </list>
    /// The steady state allocates nothing: the slots and the timestamp table are fixed arrays, and only a larger layout grows the table.
    /// </summary>
    public sealed class EffectPool : IMachineEffects
    {
        /// <summary>The renderers of the pool: 3 active plus 5 for draining tails.</summary>
        public const int RendererCount = 8;

        public const int ActiveLimit = 3;
        public const int DrainingLimit = RendererCount - ActiveLimit;

        /// <summary>The coalescing window per location and kind.</summary>
        public const long CoalesceMilliseconds = 250;

        public const int KindCount = 5;

        private const long NoStamp = long.MinValue / 2;

        private struct Slot
        {
            public EffectSlotState State;
            public EffectKind Kind;
            public int Location;
            public int Fps;
            public int FrameCount;
            public long StartMs;
            public long DrainStartMs;
            public int DrainFromFrame;
        }

        private readonly Slot[] slots = new Slot[RendererCount];
        private readonly int[] kindFrames = new int[KindCount];
        private readonly int[] kindFps = new int[KindCount];
        private long[] lastSpawn = Array.Empty<long>();
        private int[] locationFps = Array.Empty<int>();
        private int locationCount;
        private long now;
        private bool reduced;
        private long consumedGeneration = -1;

        /// <summary>The number of locations the timestamp table is sized for.</summary>
        public int LocationCount => locationCount;

        /// <summary>True while reduced motion is on: every effect was cleared and none spawns.</summary>
        public bool ReducedMotion => reduced;

        /// <summary>The presentation time of the last <see cref="Advance"/>, in milliseconds.</summary>
        public long Milliseconds => now;

        /// <summary>The number of effects spawned so far (for tests and the profile).</summary>
        public long SpawnedTotal { get; private set; }

        /// <summary>The number of requests dropped by the 250 ms window.</summary>
        public long CoalescedTotal { get; private set; }

        /// <summary>The number of requests dropped because no renderer could be given.</summary>
        public long DroppedTotal { get; private set; }

        /// <summary>The number of active effects (at most <see cref="ActiveLimit"/>).</summary>
        public int ActiveCount => CountOf(EffectSlotState.Active);

        /// <summary>The number of draining tails (at most <see cref="DrainingLimit"/>).</summary>
        public int DrainingCount => CountOf(EffectSlotState.Draining);

        public EffectSlotState StateOf(int slot) => slots[slot].State;

        public EffectKind KindOf(int slot) => slots[slot].Kind;

        public int LocationOf(int slot) => slots[slot].Location;

        /// <summary>The loop rate the effect of a slot latched at its spawn.</summary>
        public int FpsOf(int slot) => slots[slot].Fps;

        /// <summary>The frame an occupied slot shows at the last <see cref="Advance"/>; -1 for a free slot.</summary>
        public int FrameOf(int slot)
        {
            ref var s = ref slots[slot];
            if (s.State == EffectSlotState.Free)
            {
                return -1;
            }

            return s.State == EffectSlotState.Active
                ? FrameAfter(s.StartMs, s.Fps)
                : s.DrainFromFrame + FrameAfter(s.DrainStartMs, s.Fps);
        }

        /// <summary>The opacity of a slot: 1 while active, fading from 1 towards 0 over the frames a draining tail still has to play.</summary>
        public float AlphaOf(int slot)
        {
            ref var s = ref slots[slot];
            if (s.State != EffectSlotState.Draining)
            {
                return s.State == EffectSlotState.Active ? 1f : 0f;
            }

            var remaining = s.FrameCount - s.DrainFromFrame;
            if (remaining <= 0)
            {
                return 0f;
            }

            var played = FrameOf(slot) - s.DrainFromFrame;
            var alpha = 1f - (float)played / remaining;
            return alpha < 0f ? 0f : alpha;
        }

        /// <summary>The priority of a kind: the pop is 1, every decorative effect 0.</summary>
        public static int PriorityOf(EffectKind kind) => kind == EffectKind.CompletionPop ? 1 : 0;

        /// <summary>The catalog key of the first frame family of a kind (the F006 FX atlas).</summary>
        public static string SpriteKeyOf(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.CompletionPop: return "F006-FX-COMPLETION-POP";
                case EffectKind.UpgradeGlint: return "F006-FX-UPGRADE-GLINT";
                case EffectKind.Spark: return "F006-FX-SPARK";
                case EffectKind.Steam: return "F006-FX-STEAM";
                default: return "F006-FX-BUILD-DUST";
            }
        }

        /// <summary>The effect a machine response plays: the glint of an upgrade, the dust of a build (T539), the spark of a switch-on.</summary>
        public static EffectKind KindOf(MachineResponseKind response)
        {
            switch (response)
            {
                case MachineResponseKind.UpgradeGlint: return EffectKind.UpgradeGlint;
                case MachineResponseKind.BuildDust: return EffectKind.BuildDust;
                default: return EffectKind.Spark;
            }
        }

        /// <summary>Declares the art of a kind: its frame count and its default rate. A kind with no frames cannot spawn.</summary>
        public void SetKindArt(EffectKind kind, int frameCount, int framesPerSecond)
        {
            kindFrames[(int)kind] = frameCount < 0 ? 0 : frameCount;
            kindFps[(int)kind] = framesPerSecond > 0 ? framesPerSecond : MachinePresentationModel.NormalFps;
        }

        /// <summary>Sizes the timestamp and rate tables for a layout. Only a larger location count allocates; it never shrinks.</summary>
        public void EnsureLocations(int count)
        {
            if (count < 0)
            {
                count = 0;
            }

            if (count > locationCount)
            {
                var stamps = new long[KindCount * count];
                for (var i = 0; i < stamps.Length; i++)
                {
                    stamps[i] = NoStamp;
                }

                // Keep the stamps of the old locations: the table is laid out kind by kind, so each row is copied to its new offset.
                for (var kind = 0; kind < KindCount; kind++)
                {
                    for (var location = 0; location < locationCount; location++)
                    {
                        stamps[kind * count + location] = lastSpawn[kind * locationCount + location];
                    }
                }

                lastSpawn = stamps;
                Array.Resize(ref locationFps, count);
                locationCount = count;
            }
        }

        /// <summary>Sets the loop rate the next effect at a location latches (the machine's activity rate after its family cap); 0 uses the kind's rate.</summary>
        public void SetLocationFps(int location, int framesPerSecond)
        {
            if (location >= 0 && location < locationCount)
            {
                locationFps[location] = framesPerSecond;
            }
        }

        /// <summary>Sets reduced motion. Turning it on clears every effect at once (the world shows no moving pixel, FR-015).</summary>
        public void SetReducedMotion(bool value)
        {
            reduced = value;
            if (value)
            {
                Clear();
            }
        }

        /// <summary>Drops every effect, active or draining.</summary>
        public void Clear()
        {
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = default;
            }
        }

        /// <summary>Forgets the coalescing timestamps and the consumed model generation (a session replace).</summary>
        public void Reset()
        {
            for (var i = 0; i < lastSpawn.Length; i++)
            {
                lastSpawn[i] = NoStamp;
            }

            consumedGeneration = -1;
            Clear();
        }

        /// <summary>Advances presentation time and frees the effects that played their last frame. Allocation-free.</summary>
        public void Advance(long presentationMilliseconds)
        {
            if (presentationMilliseconds > now)
            {
                now = presentationMilliseconds;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                ref var s = ref slots[i];
                if (s.State != EffectSlotState.Free && FrameOf(i) >= s.FrameCount)
                {
                    s = default;
                }
            }
        }

        /// <summary>The <see cref="IMachineEffects"/> seam of the card: the glint of an upgrade, the spark of a switch-on.</summary>
        public void Spawn(MachineResponseKind kind, int location) => TrySpawn(KindOf(kind), location);

        /// <summary>
        /// Spawns an effect at a location at the pool's current time, or says why it did not. <paramref name="framesPerSecond"/> 0 takes the latest
        /// rate set for the location with <see cref="SetLocationFps"/>, otherwise the kind's own rate.
        /// </summary>
        public EffectSpawnResult TrySpawn(EffectKind kind, int location, int framesPerSecond = 0)
        {
            if (location < 0 || location >= locationCount)
            {
                return EffectSpawnResult.InvalidLocation;
            }

            if (reduced)
            {
                return EffectSpawnResult.Reduced;
            }

            var frames = kindFrames[(int)kind];
            if (frames <= 0)
            {
                return EffectSpawnResult.Unavailable;
            }

            var stampIndex = (int)kind * locationCount + location;
            if (now - lastSpawn[stampIndex] < CoalesceMilliseconds)
            {
                CoalescedTotal++;
                return EffectSpawnResult.Coalesced;
            }

            var slot = AcquireSlot(kind);
            if (slot < 0)
            {
                DroppedTotal++;
                return EffectSpawnResult.NoCapacity;
            }

            var fps = framesPerSecond > 0 ? framesPerSecond : locationFps[location] > 0 ? locationFps[location] : kindFps[(int)kind];
            slots[slot] = new Slot
            {
                State = EffectSlotState.Active,
                Kind = kind,
                Location = location,
                Fps = fps,
                FrameCount = frames,
                StartMs = now,
            };
            lastSpawn[stampIndex] = now;
            SpawnedTotal++;
            return EffectSpawnResult.Spawned;
        }

        /// <summary>
        /// Spawns one CompletionPop at every location whose <see cref="MachinePresentation.CompletionsDelta"/> is above 0 and sets the loop rate of
        /// every occupied location. A buffer is consumed once: the same <see cref="MachinePresentationModel.Generation"/> is ignored, so a caller that
        /// reads the model in every frame spawns an effect once (P2-F10). The deltas of a baseline reset, an offline return and a replaced session are
        /// 0, so they spawn nothing; under reduced motion nothing spawns either. Returns the number of pops spawned.
        /// </summary>
        public int ConsumeCompletions(MachinePresentationModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model.Generation == consumedGeneration)
            {
                return 0;
            }

            consumedGeneration = model.Generation;
            EnsureLocations(model.LocationCount);
            var spawned = 0;
            for (var i = 0; i < model.LocationCount; i++)
            {
                var presentation = model[i];
                if (!presentation.IsOccupied)
                {
                    continue;
                }

                SetLocationFps(i, presentation.ActivityFps);
                if (presentation.CompletionsDelta > 0 && TrySpawn(EffectKind.CompletionPop, i) == EffectSpawnResult.Spawned)
                {
                    spawned++;
                }
            }

            return spawned;
        }

        // ------------------------------------------------------------------------------------------------------------------------------

        private int FrameAfter(long startMs, int fps)
        {
            var elapsed = now - startMs;
            return elapsed <= 0 ? 0 : (int)(elapsed * fps / 1000L);
        }

        private int CountOf(EffectSlotState state)
        {
            var count = 0;
            for (var i = 0; i < slots.Length; i++)
            {
                count += slots[i].State == state ? 1 : 0;
            }

            return count;
        }

        // A renderer for a new effect, or -1. With 3 active, only a pop gets one: it cancels the oldest decorative effect into a draining tail.
        private int AcquireSlot(EffectKind kind)
        {
            if (CountOf(EffectSlotState.Active) >= ActiveLimit)
            {
                if (PriorityOf(kind) == 0)
                {
                    return -1;
                }

                var victim = OldestActive(decorativeOnly: true);
                if (victim < 0)
                {
                    return -1;
                }

                Cancel(victim);
            }

            var free = FreeSlot();
            if (free >= 0)
            {
                return free;
            }

            // 3 active plus 5 draining tails fill the 8 renderers only when a tail is kept: the oldest tail gives way to the new effect.
            var oldestTail = OldestDraining();
            if (oldestTail >= 0)
            {
                slots[oldestTail] = default;
            }

            return oldestTail;
        }

        private void Cancel(int slot)
        {
            ref var s = ref slots[slot];
            var frame = FrameOf(slot);
            if (frame >= s.FrameCount - 1)
            {
                // Nothing left to drain.
                s = default;
                return;
            }

            if (CountOf(EffectSlotState.Draining) >= DrainingLimit)
            {
                var oldestTail = OldestDraining();
                if (oldestTail >= 0)
                {
                    slots[oldestTail] = default;
                }
            }

            s.State = EffectSlotState.Draining;
            s.DrainFromFrame = frame;
            s.DrainStartMs = now;
        }

        private int FreeSlot()
        {
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].State == EffectSlotState.Free)
                {
                    return i;
                }
            }

            return -1;
        }

        private int OldestActive(bool decorativeOnly)
        {
            var best = -1;
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].State != EffectSlotState.Active || (decorativeOnly && PriorityOf(slots[i].Kind) != 0))
                {
                    continue;
                }

                if (best < 0 || slots[i].StartMs < slots[best].StartMs)
                {
                    best = i;
                }
            }

            return best;
        }

        private int OldestDraining()
        {
            var best = -1;
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].State != EffectSlotState.Draining)
                {
                    continue;
                }

                if (best < 0 || slots[i].DrainStartMs < slots[best].DrainStartMs)
                {
                    best = i;
                }
            }

            return best;
        }
    }
}
