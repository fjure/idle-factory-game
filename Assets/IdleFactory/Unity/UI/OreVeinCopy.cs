using System.Globalization;
using IdleFactory.Run.Application;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// Player-readable copy of the ore vein (T359, F5-R20 VEIN-6, VEIN-7, VEIN-8). Pure presentation: the yield comes from the
    /// source, the reasons are the stable codes of <c>Mine</c>, and no rule lives here.
    /// </summary>
    public static class OreVeinCopy
    {
        /// <summary>The short visible label of the vein. The accessible name carries the resource and the yield.</summary>
        public const string Caption = "Mine";

        /// <summary>The cue shown in the pulse while activations are ignored at the rate cap.</summary>
        public const string MaxRate = "Max rate";

        /// <summary>The short reason of a full store, shown inside the vein.</summary>
        public const string StoreFull = "Store full";

        /// <summary>The reason shown while a dialog is open.</summary>
        public const string DialogOpen = "Close the dialog first.";

        /// <summary>A milli-Ore yield as the player reads it: 100 is "0.1", 1,500 is "1.5", 2,000 is "2".</summary>
        public static string Yield(long milli)
        {
            if (milli < 0)
            {
                milli = 0;
            }

            if (milli >= 1_000_000L)
            {
                return QuantityFormatter.Format(milli / 1000L);
            }

            var whole = milli / 1000L;
            var fraction = milli % 1000L;
            var text = whole.ToString(CultureInfo.InvariantCulture);
            if (fraction == 0)
            {
                return text;
            }

            return text + "." + fraction.ToString("000", CultureInfo.InvariantCulture).TrimEnd('0');
        }

        /// <summary>"+0.1" for one activation, "+0.1 x3" (with the multiplication sign) while a hold coalesces three into one pulse.</summary>
        public static string Pulse(long milli, long count) =>
            count <= 1 ? "+" + Yield(milli) : "+" + Yield(milli) + " ×" + count.ToString(CultureInfo.InvariantCulture);

        /// <summary>The player-readable reason shown inside the vein for a rejection code.</summary>
        public static string Reason(string reasonCode)
        {
            switch (reasonCode)
            {
                case RunReasons.StorageFull: return StoreFull;
                default: return RunRejectionCopy.For(reasonCode);
            }
        }

        /// <summary>
        /// The accessible name: "Mine Iron Ore, +0.1 per click", plus the state that blocks mining. It does not change per click,
        /// so a screen reader hears nothing per activation, only a change of state or of the yield (VEIN-8).
        /// </summary>
        public static string AccessibleName(string resourceLabel, long yieldMilli, string blockingReasonCode)
        {
            var name = "Mine " + resourceLabel + ", +" + Yield(yieldMilli) + " per click";
            switch (blockingReasonCode)
            {
                case RunReasons.StorageFull: return name + ", store full";
                case RunReasons.ValueLimit: return name + ", limit reached";
                case RunReasons.LifecycleBlocked: return name + ", unavailable";
                default: return name;
            }
        }
    }
}
