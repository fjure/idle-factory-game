using System;
using IdleFactory.Run.Application;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// Player-readable copy of the stable rejection codes of contracts/run-application.md. A rejected command shows this
    /// text next to the control that was used and changes no state. The shell owns the shared wording so every panel
    /// (T113 to T118) says the same thing for the same code.
    /// </summary>
    public static class RunRejectionCopy
    {
        public static string For(string reasonCode)
        {
            switch (reasonCode)
            {
                case RunReasons.SlotOccupied: return "That slot already has a machine.";
                case RunReasons.SlotUnavailable: return "That slot is not unlocked yet.";
                case RunReasons.MachineLocked: return "That machine is not unlocked yet.";
                case RunReasons.CashInsufficient: return "Not enough Cash.";
                case RunReasons.LevelMax: return "Already at the maximum level.";
                case RunReasons.ConfirmationRequired: return "Please confirm this action first.";
                case RunReasons.PriorityNotPermutation: return "The priority order changed. The list was refreshed.";
                case RunReasons.ResourceLocked: return "That resource is not unlocked yet.";
                case RunReasons.ContractActiveExists: return "Finish or abandon the active contract first.";
                case RunReasons.ContractNoneActive: return "There is no active contract.";
                case RunReasons.ContractExpired: return "That contract has expired.";
                case RunReasons.StockInsufficient: return "Not enough stock.";
                case RunReasons.ItemNone: return "You have no Item of that kind.";
                case RunReasons.ItemTargetInvalid: return "That Item cannot target this machine.";
                case RunReasons.EffectConflict: return "A matching effect is already running.";
                case RunReasons.ResearchPrerequisite: return "Research its prerequisites first.";
                case RunReasons.RpInsufficient: return "Not enough Research points.";
                case RunReasons.OverdriveCoolingDown: return "Overdrive is cooling down.";
                case RunReasons.OverdriveActive: return "Overdrive is already running.";
                case RunReasons.IpInsufficient: return "Not enough Industry points.";
                case RunReasons.PrestigeBelowThreshold: return "The factory is not valuable enough to sell yet.";
                case RunReasons.TransitionPending: return "A Factory Sale is in progress.";
                case RunReasons.ValueLimit: return "That number is too large to handle.";
                case RunReasons.LifecycleBlocked: return "The factory is not available right now.";
                case RunReasons.CommandInvalid: return "That could not be done. Nothing was changed.";
                default: return "That action is not available.";
            }
        }

        /// <summary>The "Locked" reason of the Overdrive control.</summary>
        public static string OverdriveLocked(OverdriveStatus status)
        {
            if (status == null || status.Unlock == null)
            {
                return "Overdrive is locked.";
            }

            switch (status.Unlock.Kind)
            {
                case "factoryLevelAtLeast": return "Overdrive unlocks at factory level " + status.Unlock.Level + ".";
                case "researchPurchased": return "Overdrive unlocks with a Research.";
                case "resourceProduced": return "Overdrive unlocks once a resource is produced.";
                default: return "Overdrive is locked.";
            }
        }

        /// <summary>A countdown as m:ss (hours are folded into minutes).</summary>
        public static string Countdown(long milliseconds)
        {
            var totalSeconds = Math.Max(0L, (milliseconds + 999L) / 1000L);
            return (totalSeconds / 60L).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                (totalSeconds % 60L).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
