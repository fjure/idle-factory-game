using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>Which world element a tap resolved to.</summary>
    public enum WorldTapSource
    {
        /// <summary>Empty world: nothing was hit (and the vein, the depot, the links and the deck are never hit targets).</summary>
        None = 0,

        /// <summary>A machine's tap shape (the diamond clipped to its lattice cell).</summary>
        Machine = 1,

        /// <summary>The visible icon of an attention marker.</summary>
        MarkerIcon = 2,

        /// <summary>The padded touch circle of an attention marker (at least 48 points across), outside its icon.</summary>
        MarkerHalo = 3,
    }

    /// <summary>
    /// The tap priority of the world (P2-F13, T528). Two kinds of element can be hit: a machine's tap shape and an attention marker, whose touch circle is at
    /// least 48 points across (UI/UX C4) and so can reach into a machine's shape on a small zoom. The rule, in order:
    /// <list type="number">
    /// <item><description>the <b>visible icon</b> of a marker wins over everything under it: a tap the player aimed at a drawn marker never selects the machine behind it;</description></item>
    /// <item><description>otherwise a <b>machine shape</b> wins: the invisible padding of a marker never steals a tap from a visible machine;</description></item>
    /// <item><description>otherwise the <b>marker's touch circle</b> catches the tap (the padding exists for exactly this: a finger beside a small icon);</description></item>
    /// <item><description>otherwise the world is empty. The deposit, the depot, the links and the deck are scenery: they are never hit targets.</description></item>
    /// </list>
    /// Either hit target resolves to a location (a marker selects its first affected location), so the caller opens that location. Pure; allocation-free.
    /// </summary>
    public static class WorldTapResolver
    {
        public static WorldTapSource Resolve(FactoryWorldLayout layout, AttentionMarkerView markers, Vector2 world, out BuildingLocationId target)
        {
            if (markers != null && markers.TryHitIcon(world, out target))
            {
                return WorldTapSource.MarkerIcon;
            }

            if (layout != null && layout.TryHit(world, out target))
            {
                return WorldTapSource.Machine;
            }

            if (markers != null && markers.TryHit(world, out target))
            {
                return WorldTapSource.MarkerHalo;
            }

            target = default;
            return WorldTapSource.None;
        }
    }
}
