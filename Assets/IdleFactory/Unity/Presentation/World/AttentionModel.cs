using System;
using IdleFactory.Run.Application;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// The system conditions that drive only the hub dot (F006 R09, U-4): Contracts, Research, Prestige and Overdrive. The Market has no flag
    /// by design, so there is no member for it. The flags are supplied by the shell from conditions that already exist elsewhere.
    /// </summary>
    [Flags]
    public enum SystemAttentionFlags
    {
        None = 0,
        Contracts = 1,
        Research = 2,
        Prestige = 4,
        Overdrive = 8,
    }

    /// <summary>What a production attention marker says.</summary>
    public enum AttentionKind
    {
        /// <summary>The factory is short of a resource: the marker shows that resource's icon at the trunk junction of the first affected row.</summary>
        Shortage = 0,

        /// <summary>At least one machine cannot store its output: the marker sits at the depot.</summary>
        StoreFull = 1,
    }

    /// <summary>
    /// One world marker of production attention. It carries no number: the world shows the resource icon only (FR-030); the affected count lives in the
    /// strip indicator and the card.
    /// </summary>
    public readonly struct AttentionMarker
    {
        public AttentionMarker(
            AttentionKind kind, string resourceId, int rank, int affectedCount, long coveragePpm, bool isSharedCause, BuildingLocationId firstAffected,
            Vector2 position)
        {
            Kind = kind;
            ResourceId = resourceId;
            Rank = rank;
            AffectedCount = affectedCount;
            CoveragePpm = coveragePpm;
            IsSharedCause = isSharedCause;
            FirstAffected = firstAffected;
            Position = position;
        }

        public AttentionKind Kind { get; }

        /// <summary>The short resource of a <see cref="AttentionKind.Shortage"/> marker (drives the icon), otherwise null.</summary>
        public string ResourceId { get; }

        /// <summary>0 for the top shortage resource; a store-full marker has rank 0 of its own kind.</summary>
        public int Rank { get; }

        /// <summary>The machines affected: those blocked by the resource, or the machines that cannot store their output.</summary>
        public int AffectedCount { get; }

        /// <summary>The supply coverage the shortage ranking used (<see cref="ResourceRates.SupplyCoveragePpm"/>); 0 for store full.</summary>
        public long CoveragePpm { get; }

        /// <summary>Two or more machines are blocked by the same resource (R09 "shared cause").</summary>
        public bool IsSharedCause { get; }

        /// <summary>The lowest location id among the affected machines. A tap on the marker selects it.</summary>
        public BuildingLocationId FirstAffected { get; }

        /// <summary>The marker centre in world units, computed from <see cref="FactoryWorldLayout"/> (never from the depot view).</summary>
        public Vector2 Position { get; }
    }

    /// <summary>The single strip indicator: the top cause, its affected count, and "+N" for further shortage resources.</summary>
    public readonly struct AttentionStrip
    {
        public AttentionStrip(AttentionKind kind, string resourceId, int affectedCount, int moreCount, BuildingLocationId firstAffected)
        {
            IsVisible = true;
            Kind = kind;
            ResourceId = resourceId;
            AffectedCount = affectedCount;
            MoreCount = moreCount;
            FirstAffected = firstAffected;
        }

        public bool IsVisible { get; }
        public AttentionKind Kind { get; }

        /// <summary>The top shortage resource (icon), or null for store full.</summary>
        public string ResourceId { get; }

        public int AffectedCount { get; }

        /// <summary>The number of further shortage resources: the strip shows "+MoreCount" when it is above 0. Store full is not a resource and is not counted.</summary>
        public int MoreCount { get; }

        /// <summary>A tap on the strip selects this location.</summary>
        public BuildingLocationId FirstAffected { get; }
    }

    /// <summary>
    /// Derives production attention and system attention from the run read model (F006 T532, R09, FR-026, FR-028, FR-030). A plain C# model:
    /// deterministic, no scene, and allocation-free on a refresh once its buffers are sized.
    /// <para>
    /// Shortage (the single production-level signal, copy "Short on &lt;resource&gt;"): the resources that block at least one MissingInput machine and
    /// have demand (<see cref="ResourceRates.HasDemand"/>), ordered by supply coverage ascending, then affected count descending, then content
    /// (inventory) order. No MissingInput machine means no signal, whatever the coverage; a blocked machine counts even when the coverage is 100 % or
    /// more (an auto-sold input that never reaches the stock). Coverage is a steady-state ratio and never decides alone (G-2, G-3).
    /// </para>
    /// <para>
    /// World markers: one per shortage resource, at most <see cref="MaxShortageMarkers"/> visible, at the trunk junction of the row of the first affected
    /// location (the point where the row's trunk turns into the depot collector), plus one at the depot for store full. Positions come from the
    /// layout. The strip indicator shows the top cause and "+N".
    /// </para>
    /// <para>
    /// State ownership: the model owns its buffers only and reads immutable snapshots. Nothing is saved; no economy rule reads it. The machine state
    /// is <see cref="SlotGridModel.StateFor"/>, the single precedence. Refresh only when <see cref="NeedsRefresh"/> says so.
    /// </para>
    /// </summary>
    public sealed class AttentionModel
    {
        /// <summary>The most shortage markers shown at once (R09 and analysis D8).</summary>
        public const int MaxShortageMarkers = 2;

        /// <summary>The most markers of all kinds: the shortage markers plus the depot marker.</summary>
        public const int MaxMarkers = MaxShortageMarkers + 1;

        /// <summary>The marker icon box in art pixels. It sits in the 70 x 54 depot-column cell, clear of every keep-out box of the lattice.</summary>
        public const int MarkerSizePixels = 22;

        /// <summary>
        /// When two shortage markers would share one junction (both first affected locations are in the same row), the second moves this many art pixels
        /// along the trunk toward the lattice. The cell is 70 px wide, so the moved marker (centre at 22, half size 11) stays inside it.
        /// </summary>
        public const int SecondMarkerShiftPixels = 22;

        private const float Density = SlotGridLayout.Density;

        private readonly AttentionMarker[] markers = new AttentionMarker[MaxMarkers];
        private int markerCount;
        private AttentionStrip strip;
        private SystemAttentionFlags system;

        // Per-resource counters, in inventory (content) order, and a flat location matrix; both grow once and are then reused.
        private int[] affectedCount = Array.Empty<int>();
        private int[] firstAffected = Array.Empty<int>();
        private byte[] affectedMatrix = Array.Empty<byte>();
        private int[] shortageOrder = Array.Empty<int>();
        private int[] sharedOrder = Array.Empty<int>();
        private int resourceCount;
        private int locationCount;
        private int shortageCount;
        private int sharedCount;
        private int storeFullCount;
        private int storeFullFirst = -1;
        private RatesSnapshot lastRates;

        private bool hasRevision;
        private long structureRevision;
        private long quantityRevision;
        private FactoryWorldLayout lastLayout;
        private SystemAttentionFlags lastSystem;

        /// <summary>The world markers in display order: the shortage markers by rank, then the depot marker. Length <see cref="MarkerCount"/>.</summary>
        public int MarkerCount => markerCount;

        public AttentionMarker GetMarker(int index)
        {
            if (index < 0 || index >= markerCount) throw new ArgumentOutOfRangeException(nameof(index));
            return markers[index];
        }

        /// <summary>The strip indicator; <see cref="AttentionStrip.IsVisible"/> is false when there is no production attention.</summary>
        public AttentionStrip Strip => strip;

        /// <summary>The number of shortage resources (all of them, also those beyond <see cref="MaxShortageMarkers"/>).</summary>
        public int ShortageCount => shortageCount;

        /// <summary>The number of machines that cannot store their output; any above 0 shows the depot full state.</summary>
        public int StoreFullCount => storeFullCount;

        public bool StoreFull => storeFullCount > 0;

        /// <summary>The system flags of the last refresh.</summary>
        public SystemAttentionFlags SystemFlags => system;

        /// <summary>The hub dot: true when any system flag is set. Production attention never drives it.</summary>
        public bool HubDot => system != SystemAttentionFlags.None;

        /// <summary>True when any production attention (a shortage or store full) exists. System attention is not part of it.</summary>
        public bool HasProductionAttention => shortageCount > 0 || storeFullCount > 0;

        /// <summary>The number of shared causes: resources that block two or more MissingInput machines (R09).</summary>
        public int SharedCauseCount => sharedCount;

        /// <summary>The resource id of shared cause <paramref name="index"/> (content order).</summary>
        public string SharedCauseResourceId(int index) => lastRates.Resources[sharedOrder[Checked(index, sharedCount)]].ResourceId;

        /// <summary>The machines blocked by shared cause <paramref name="index"/>.</summary>
        public int SharedCauseAffectedCount(int index) => affectedCount[sharedOrder[Checked(index, sharedCount)]];

        /// <summary>The resource id of shortage <paramref name="rank"/> (0 is the top).</summary>
        public string ShortageResourceId(int rank) => lastRates.Resources[shortageOrder[Checked(rank, shortageCount)]].ResourceId;

        public int ShortageAffectedCount(int rank) => affectedCount[shortageOrder[Checked(rank, shortageCount)]];

        public long ShortageCoveragePpm(int rank) => lastRates.Resources[shortageOrder[Checked(rank, shortageCount)]].SupplyCoveragePpm;

        /// <summary>True when the machine at <paramref name="location"/> is blocked by shortage <paramref name="rank"/>.</summary>
        public bool IsAffectedByShortage(int rank, BuildingLocationId location) =>
            location.Value >= 0 && location.Value < locationCount && affectedMatrix[shortageOrder[Checked(rank, shortageCount)] * locationCount + location.Value] != 0;

        /// <summary>The copy of the shortage signal: "Short on &lt;resource&gt;". The caller supplies the resource's player-facing label.</summary>
        public static string ShortOnCopy(string resourceLabel) => "Short on " + resourceLabel;

        /// <summary>
        /// The trunk junction of a location's row: the point where the row's trunk turns into the depot collector, in the margin band beside the
        /// lattice. It is read from <see cref="FactoryWorldLayout"/>.
        /// </summary>
        public static Vector2 JunctionOf(FactoryWorldLayout layout, BuildingLocationId location)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var trunk = layout.TrunkOf(layout.RowOf(location));
            return trunk.IsEmpty ? layout.DepotAnchor : trunk.Points[1];
        }

        /// <summary>The marker box (<see cref="MarkerSizePixels"/> square) around a marker centre, in world units.</summary>
        public static Rect MarkerBoxAt(Vector2 centre)
        {
            var size = MarkerSizePixels / Density;
            return new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size);
        }

        /// <summary>True when a refresh with these inputs would change anything; false means the snapshot need not even be built.</summary>
        public bool NeedsRefresh(long structureRevision, long quantityRevision, FactoryWorldLayout layout, SystemAttentionFlags system) =>
            !hasRevision || structureRevision != this.structureRevision || quantityRevision != this.quantityRevision || !ReferenceEquals(layout, lastLayout) ||
            system != lastSystem;

        /// <summary>Forgets the cached revisions so that the next refresh recomputes. Call it when the session is replaced.</summary>
        public void Invalidate() => hasRevision = false;

        /// <summary>Recomputes production and system attention. Returns false (and changes nothing) when the revisions, layout and flags are as before.</summary>
        public bool Refresh(FactoryWorldLayout layout, RatesSnapshot rates, SystemAttentionFlags systemFlags = SystemAttentionFlags.None)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (rates == null) throw new ArgumentNullException(nameof(rates));
            if (!NeedsRefresh(rates.StructureRevision, rates.QuantityRevision, layout, systemFlags))
            {
                return false;
            }

            hasRevision = true;
            structureRevision = rates.StructureRevision;
            quantityRevision = rates.QuantityRevision;
            lastLayout = layout;
            lastSystem = systemFlags;
            lastRates = rates;
            system = systemFlags;

            var resources = rates.Resources;
            resourceCount = resources.Count;
            locationCount = layout.LocationCount;
            EnsureCapacity(resourceCount, locationCount);
            for (var r = 0; r < resourceCount; r++)
            {
                affectedCount[r] = 0;
                firstAffected[r] = -1;
            }

            Array.Clear(affectedMatrix, 0, resourceCount * locationCount);
            storeFullCount = 0;
            storeFullFirst = -1;

            var machines = rates.Machines;
            for (var m = 0; m < machines.Count; m++)
            {
                var diagnosis = machines[m];
                var location = diagnosis.SlotIndex;
                if (location < 0 || location >= locationCount)
                {
                    continue;
                }

                var state = SlotGridModel.StateFor(diagnosis.IsActive, diagnosis.PrimaryBlocker, diagnosis.Phase);
                if (state == MachineVisualState.StorageFull)
                {
                    storeFullCount++;
                    storeFullFirst = storeFullFirst < 0 || location < storeFullFirst ? location : storeFullFirst;
                }
                else if (state == MachineVisualState.MissingInput)
                {
                    var blockers = diagnosis.Blockers;
                    for (var b = 0; b < blockers.Count; b++)
                    {
                        var blocker = blockers[b];
                        if (blocker.Kind != MachineBlocker.MissingInput || blocker.ResourceId == null)
                        {
                            continue;
                        }

                        var r = IndexOf(resources, blocker.ResourceId);
                        if (r < 0)
                        {
                            continue; // a resource the run has not unlocked is not a shortage the player can act on
                        }

                        var cell = r * locationCount + location;
                        if (affectedMatrix[cell] != 0)
                        {
                            continue;
                        }

                        affectedMatrix[cell] = 1;
                        affectedCount[r]++;
                        firstAffected[r] = firstAffected[r] < 0 || location < firstAffected[r] ? location : firstAffected[r];
                    }
                }
            }

            BuildShortage(resources);
            BuildMarkers(layout, resources);
            return true;
        }

        private void BuildShortage(System.Collections.Generic.IReadOnlyList<ResourceRates> resources)
        {
            shortageCount = 0;
            sharedCount = 0;
            for (var r = 0; r < resourceCount; r++)
            {
                if (affectedCount[r] >= 2)
                {
                    sharedOrder[sharedCount++] = r; // content order
                }

                if (affectedCount[r] == 0 || !resources[r].HasDemand)
                {
                    continue;
                }

                // Insertion sort by coverage ascending, affected count descending, content order (the index) ascending.
                var at = shortageCount++;
                while (at > 0 && Before(r, shortageOrder[at - 1], resources))
                {
                    shortageOrder[at] = shortageOrder[at - 1];
                    at--;
                }

                shortageOrder[at] = r;
            }
        }

        private bool Before(int candidate, int other, System.Collections.Generic.IReadOnlyList<ResourceRates> resources)
        {
            var a = resources[candidate].SupplyCoveragePpm;
            var b = resources[other].SupplyCoveragePpm;
            if (a != b)
            {
                return a < b;
            }

            if (affectedCount[candidate] != affectedCount[other])
            {
                return affectedCount[candidate] > affectedCount[other];
            }

            return candidate < other;
        }

        private void BuildMarkers(FactoryWorldLayout layout, System.Collections.Generic.IReadOnlyList<ResourceRates> resources)
        {
            markerCount = 0;
            var shown = Math.Min(shortageCount, MaxShortageMarkers);
            var shift = (layout.DepotOnRight ? -1f : 1f) * SecondMarkerShiftPixels / Density;
            for (var rank = 0; rank < shown; rank++)
            {
                var r = shortageOrder[rank];
                var first = new BuildingLocationId(firstAffected[r]);
                var position = JunctionOf(layout, first);
                if (rank == 1 && layout.RowOf(first) == layout.RowOf(markers[0].FirstAffected))
                {
                    position = new Vector2(position.x + shift, position.y);
                }

                markers[markerCount++] = new AttentionMarker(
                    AttentionKind.Shortage, resources[r].ResourceId, rank, affectedCount[r], resources[r].SupplyCoveragePpm, affectedCount[r] >= 2, first, position);
            }

            if (storeFullCount > 0)
            {
                markers[markerCount++] = new AttentionMarker(
                    AttentionKind.StoreFull, null, 0, storeFullCount, 0L, storeFullCount >= 2, new BuildingLocationId(storeFullFirst), layout.DepotAnchor);
            }

            if (shortageCount > 0)
            {
                var top = markers[0];
                strip = new AttentionStrip(AttentionKind.Shortage, top.ResourceId, top.AffectedCount, shortageCount - 1, top.FirstAffected);
            }
            else if (storeFullCount > 0)
            {
                strip = new AttentionStrip(AttentionKind.StoreFull, null, storeFullCount, 0, new BuildingLocationId(storeFullFirst));
            }
            else
            {
                strip = default;
            }
        }

        private static int IndexOf(System.Collections.Generic.IReadOnlyList<ResourceRates> resources, string id)
        {
            for (var i = 0; i < resources.Count; i++)
            {
                if (string.Equals(resources[i].ResourceId, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int Checked(int index, int count)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            return index;
        }

        private void EnsureCapacity(int resources, int locations)
        {
            if (affectedCount.Length < resources)
            {
                affectedCount = new int[resources];
                firstAffected = new int[resources];
                shortageOrder = new int[resources];
                sharedOrder = new int[resources];
            }

            if (affectedMatrix.Length < resources * locations)
            {
                affectedMatrix = new byte[resources * locations];
            }
        }
    }
}
