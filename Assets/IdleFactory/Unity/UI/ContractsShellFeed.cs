using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using UnityEngine;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// The shell-level part of the contracts surface (T330, contracts/ui-surfaces-f005.md <c>tab-contracts-badge</c> and the auto-delivered
    /// notice). The Contracts panel only redraws while it is visible, but the badge on the tab and the notice that a contract ended by
    /// itself must show on every tab, so the presenter drives this feed every frame. It reads <see cref="RunSnapshots.Contracts"/> only when the
    /// structure, the quantities or the timeline second changed, and it holds no rule: the number of deliverable contracts and the outcome come
    /// from the snapshot, and the shell only draws them.
    /// </summary>
    public sealed class ContractsShellFeed
    {
        private readonly RunSession session;
        private readonly RunShellDocument shell;
        private long keyStructure = -1;
        private long keyQuantity = -1;
        private long keySecond = -1;
        private bool seeded;
        private long lastOutcomeSerial;
        private float lastReadTime = float.NegativeInfinity;
        private readonly string[] offerIds;

        /// <summary>Quantity-only changes (every production cycle) re-read the snapshot at most this often, in real seconds.</summary>
        public const float QuantityReadSeconds = 0.2f;

        public ContractsShellFeed(RunSession session, RunShellDocument shell)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            offerIds = new string[session.Catalog.ContractOffers.Count];
            var next = 0;
            foreach (var id in session.Catalog.ContractOffers.Keys)
            {
                offerIds[next++] = id;
            }
        }

        /// <summary>The number of deliverable contracts the badge was last given.</summary>
        public int Deliverable { get; private set; }

        /// <summary>Forces the next <see cref="Update"/> to read the snapshot again.</summary>
        public void Invalidate()
        {
            keyStructure = -1;
            lastReadTime = float.NegativeInfinity;
        }

        /// <summary>
        /// Refreshes the badge and announces a contract the simulator ended since the last update (auto-delivery or production completion). The
        /// first update only records the outcome that already exists, so a load or an offline return does not announce an old ending.
        /// </summary>
        public void Update()
        {
            if (!shell.IsReady)
            {
                return;
            }

            var second = session.TimelineUtcMs / 1000L;
            var sameStructure = keyStructure == session.StructureRevision && keySecond == second;
            if (sameStructure && (keyQuantity == session.QuantityRevision || Time.realtimeSinceStartup - lastReadTime < QuantityReadSeconds))
            {
                return;
            }

            lastReadTime = Time.realtimeSinceStartup;
            if (!InPlay())
            {
                // No offer is unlocked and none is active: nothing can be deliverable or end, and no snapshot is built.
                keyStructure = session.StructureRevision;
                keyQuantity = session.QuantityRevision;
                keySecond = second;
                seeded = true;
                if (Deliverable != 0)
                {
                    Deliverable = 0;
                    shell.SetContractsBadge(0);
                }

                return;
            }

            keyStructure = session.StructureRevision;
            keyQuantity = session.QuantityRevision;
            keySecond = second;
            var snapshot = RunSnapshots.Contracts(session);
            Deliverable = CountDeliverable(snapshot);
            shell.SetContractsBadge(Deliverable);

            var outcome = snapshot.LastOutcome;
            if (!seeded)
            {
                seeded = true;
            }
            else if (outcome.Serial != lastOutcomeSerial)
            {
                var text = ContractOutcomeCopy.Notice(session.Catalog, outcome);
                if (text != null)
                {
                    shell.ShowNotice(text, ShellNoticeKind.Info);
                }
            }

            lastOutcomeSerial = outcome.Serial;
        }

        private bool InPlay()
        {
            if (session.State.Contracts.Active != null || lastOutcomeSerial != 0)
            {
                return true;
            }

            for (var i = 0; i < offerIds.Length; i++)
            {
                if (session.State.IsUnlocked(offerIds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The contracts the player can hand in right now: the active delivery contract when its stock covers the quantity, otherwise (nothing
        /// active) the unlocked, acceptable delivery offers whose stock already covers the next quantity. Zero clears the badge.
        /// </summary>
        public static int CountDeliverable(ContractsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return 0;
            }

            if (snapshot.Active != null)
            {
                return snapshot.Active.CanDeliver ? 1 : 0;
            }

            var count = 0;
            for (var i = 0; i < snapshot.Offers.Count; i++)
            {
                var offer = snapshot.Offers[i];
                if (offer.Unlocked && offer.CanAccept && offer.Deliverable)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Player-readable copy of contracts that ended without a command (auto-delivery and production completion).</summary>
    public static class ContractOutcomeCopy
    {
        /// <summary>
        /// "Auto-delivered 100 Iron Plate: +$900, +4 RP, 1 Item." or "Produced 20 Iron Plate: contract complete. +$50, +2 RP." Null for an
        /// ending that has no notice (an expiry shows the outcome card, no ending at all shows nothing).
        /// </summary>
        public static string Notice(RunCatalog catalog, ContractOutcome outcome)
        {
            if (outcome.Kind != ContractOutcomeKind.AutoDelivered && outcome.Kind != ContractOutcomeKind.ProduceCompleted)
            {
                return null;
            }

            var resource = ResourceLabelOfOffer(catalog, outcome.OfferId);
            var rewards = Rewards(outcome.CashReward, outcome.RpReward, outcome.ItemCount);
            var text = new StringBuilder();
            if (outcome.Kind == ContractOutcomeKind.AutoDelivered)
            {
                text.Append("Auto-delivered ").Append(QuantityFormatter.Format(outcome.RequiredUnits)).Append(' ').Append(resource);
                if (rewards.Length > 0)
                {
                    text.Append(": ").Append(rewards);
                }
            }
            else
            {
                text.Append("Produced ").Append(QuantityFormatter.Format(outcome.RequiredUnits)).Append(' ').Append(resource).Append(": contract complete");
                if (rewards.Length > 0)
                {
                    text.Append(". ").Append(rewards);
                }
            }

            return text.Append('.').ToString();
        }

        /// <summary>"+$900, +4 RP, 1 Item" (parts that are zero are left out); empty when nothing was granted.</summary>
        public static string Rewards(long cash, long rp, int itemCount)
        {
            var parts = new List<string>();
            if (cash > 0)
            {
                parts.Add("+$" + QuantityFormatter.Format(cash));
            }

            if (rp > 0)
            {
                parts.Add("+" + QuantityFormatter.Format(rp) + " RP");
            }

            if (itemCount > 0)
            {
                parts.Add(itemCount.ToString(CultureInfo.InvariantCulture) + (itemCount == 1 ? " Item" : " Items"));
            }

            return string.Join(", ", parts);
        }

        public static string ResourceLabelOfOffer(RunCatalog catalog, string offerId)
        {
            if (catalog != null && offerId != null && catalog.ContractOffers.TryGetValue(offerId, out var offer))
            {
                return catalog.Resources.TryGetValue(offer.ResourceId, out var resource) ? resource.Label : offer.ResourceId;
            }

            return offerId ?? string.Empty;
        }
    }
}
