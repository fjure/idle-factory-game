using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The Contracts tab (T115, FR-014, FR-015, FR-032, US2 scenarios 2 and 3; contracts/ui-surfaces.md, Contracts). It shows the
    /// offers (kind, required units, rewards, duration), the active contract with its progress against stock, a deadline countdown
    /// that ticks once per second and Deliver (disabled with the missing units), Accept (disabled while a contract is active),
    /// Abandon for an untimed contract behind the shell's confirmation ("no reward, no penalty", cancelled by a rotation, UX-5),
    /// the expiry outcome card and the empty state. The panel reads <see cref="RunSnapshots.Contracts"/> and mutates only through
    /// <see cref="RunCommands"/>; it holds no economy rule. Every control stays focusable when it is disabled and its reason is shown
    /// as text next to it; the command re-validates every tap and a rejection shows its copy next to the control that was used.
    /// </summary>
    public sealed class ContractsPanel : IShellPanel
    {
        public const string PanelTitle = "Contracts";
        private const string ProduceObjective = "produce";
        private const string BonusClass = "bonus";
        public const string AbandonMessage = "You get no reward and no penalty. The contract can be accepted again and your stock stays as it is.";
        public const string ExpiredNoPenalty = "No reward, no penalty.";
        public const string AutoDeliverHint = "Applies to this contract only. Its stock is kept out of auto-sell.";
        public const string ProductionHint = "Completes automatically.";
        public const string AutoDeliverShortStock = "Auto-deliver was on; stock was short.";
        public const string CompletionBlockedText = "Waiting to complete: that number is too large to handle. It tries again automatically.";

        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly Func<string, Sprite> icons;
        private readonly VisualElement emptyBox;
        private readonly Label emptyLabel;
        private readonly VisualElement expiredCard;
        private readonly Label expiredTitle;
        private readonly Label expiredLine;
        private readonly Label expiredAutoNote;
        private readonly VisualElement activeCard;
        private readonly Label activeTitle;
        private readonly Label activeRequirement;
        private readonly VisualElement progressTrack;
        private readonly VisualElement progressFill;
        private readonly Label activeProgress;
        private readonly Label activeMissing;
        private readonly Label activeDeadline;
        private readonly Label activeRewards;
        private readonly Label activeHint;
        private readonly Label activeFeedback;
        private readonly Label activeAutoHint;
        private readonly Label activeBlocked;
        private readonly Label produceText;
        private readonly VisualElement produceTrack;
        private readonly VisualElement produceFill;
        private IDisposable exactProduce;
        private string seenAutoOffer;
        private long? seenAutoDeadline;
        private bool seenAutoOn;
        private readonly VisualElement offersSection;
        private readonly Dictionary<string, OfferRow> offerRows = new Dictionary<string, OfferRow>(StringComparer.Ordinal);
        private readonly List<OfferRow> offerOrder = new List<OfferRow>();
        private IDisposable exactValue;
        private RunShellDocument attachedShell;
        private RunShellDocument shell;
        private long keyStructure = -1;
        private long keyQuantity = -1;
        private long keySecond = -1;
        private bool keyExpired;
        private bool forceRefresh = true;
        private bool confirmOpen;
        private long? deadlineMs;
        private ContractsSnapshot snapshot;

        /// <summary>Builds the panel. <paramref name="styles"/> is <c>ContractsPanel.uss</c>; <paramref name="icons"/> resolves a presentation key to a sprite (optional).</summary>
        public ContractsPanel(RunSession session, RunCommands commands, StyleSheet styles = null, Func<string, Sprite> icons = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.icons = icons;
            var root = new VisualElement { name = "contracts-panel" };
            root.AddToClassList("contracts-panel");
            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            Root = root;

            // A wrapped label is wrapped in a container (wiki: UI Toolkit HUD layout pitfalls), or a bounded scroll view clamps its height.
            emptyBox = MakeCard("contracts-empty-box", "contracts-card--plain");
            emptyLabel = MakeText("contracts-empty", "contracts-line");
            WrappedLabelFit.Attach(emptyLabel);
            PanelIcons.Mark(emptyLabel, icons, PanelIcons.Lock);
            emptyBox.Add(emptyLabel);
            root.Add(emptyBox);

            expiredCard = MakeCard("contracts-expired", "contracts-card--warning");
            expiredTitle = MakeText("contracts-expired-title", "contracts-title");
            expiredLine = MakeText("contracts-expired-line", "contracts-line");
            expiredAutoNote = MakeText("contracts-expired-autodeliver", "contracts-hint");
            expiredCard.Add(expiredTitle);
            expiredCard.Add(expiredLine);
            expiredCard.Add(expiredAutoNote);
            root.Add(expiredCard);

            activeCard = MakeCard("contracts-active", "contracts-card--active");
            var activeHeader = MakeText("contracts-active-header", "contracts-section-title");
            activeHeader.text = "Active contract";
            activeTitle = MakeText("contracts-active-title", "contracts-title");
            activeRequirement = MakeText("contracts-active-requirement", "contracts-line");
            progressTrack = new VisualElement { name = "contracts-progress" };
            progressTrack.AddToClassList("contracts-progress");
            progressFill = new VisualElement { name = "contracts-progress-fill", pickingMode = PickingMode.Ignore };
            progressFill.AddToClassList("contracts-progress__fill");
            progressTrack.Add(progressFill);
            activeProgress = MakeText("contracts-active-progress", "contracts-line");
            activeMissing = MakeText("contracts-active-missing", "contracts-hint");
            activeDeadline = MakeText("contracts-active-deadline", "contracts-line");
            activeRewards = MakeText("contracts-active-rewards", "contracts-line");
            activeHint = MakeText("contracts-active-hint", "contracts-hint");
            activeFeedback = MakeText("contracts-active-feedback", "contracts-feedback");
            PanelIcons.Mark(activeFeedback, icons, PanelIcons.Warning);
            activeAutoHint = MakeText("contracts-autodeliver-hint", "contracts-hint");
            activeBlocked = MakeText("contracts-completion-blocked", "contracts-feedback");
            PanelIcons.Mark(activeBlocked, icons, PanelIcons.Warning);
            DeliverButton = RunShellDocument.CreateButton("contracts-deliver", "Deliver", true);
            AutoDeliverButton = RunShellDocument.CreateButton("contracts-autodeliver", "Auto-deliver: Off");
            AbandonButton = RunShellDocument.CreateButton("contracts-abandon", "Abandon");
            DeliverButton.clicked += OnDeliver;
            AutoDeliverButton.clicked += OnAutoDeliver;
            AbandonButton.clicked += OnAbandon;

            // The production card shows "Produced N of M" with a bounded bar instead of the stock line. It is a focusable control so its
            // exact value can be inspected like every compact number (the text sits in children, so the button itself has no text).
            ProduceProgress = new Button { name = "contracts-produce-progress", focusable = true };
            ProduceProgress.AddToClassList("run-control");
            ProduceProgress.AddToClassList("contracts-produce");
            produceText = MakeText("contracts-produce-progress-text", "contracts-line");
            produceText.pickingMode = PickingMode.Ignore;
            produceTrack = new VisualElement { name = "contracts-produce-progress-bar", pickingMode = PickingMode.Ignore };
            produceTrack.AddToClassList("contracts-progress");
            produceFill = new VisualElement { name = "contracts-produce-progress-fill", pickingMode = PickingMode.Ignore };
            produceFill.AddToClassList("contracts-progress__fill");
            produceTrack.Add(produceFill);
            ProduceProgress.Add(produceText);
            ProduceProgress.Add(produceTrack);

            var actions = new VisualElement { name = "contracts-active-actions" };
            actions.AddToClassList("contracts-actions");
            actions.Add(DeliverButton);
            actions.Add(AutoDeliverButton);
            actions.Add(AbandonButton);
            foreach (var element in new VisualElement[]
            {
                activeHeader, activeTitle, activeRequirement, progressTrack, ProduceProgress, activeProgress, activeMissing, activeDeadline,
                activeRewards, actions, activeAutoHint, activeBlocked, activeHint, activeFeedback,
            })
            {
                activeCard.Add(element);
            }

            root.Add(activeCard);

            offersSection = new VisualElement { name = "contracts-offers" };
            offersSection.AddToClassList("contracts-offers");
            var offersHeader = MakeText("contracts-offers-header", "contracts-section-title");
            offersHeader.text = "Offers";
            offersSection.Add(offersHeader);
            root.Add(offersSection);

            foreach (var offer in RunSnapshots.Contracts(session).Offers)
            {
                var row = new OfferRow(offer.OfferId, this);
                offerRows.Add(offer.OfferId, row);
                offerOrder.Add(row);
                offersSection.Add(row.Card);
            }
        }

        public string Id => ShellPanelIds.Contracts;
        public string Title => PanelTitle;
        public VisualElement Root { get; }

        public Button DeliverButton { get; }
        public Button AbandonButton { get; }

        /// <summary><c>contracts-autodeliver</c>: the Off/On toggle of a delivery objective (hidden for a production objective and an expired card).</summary>
        public Button AutoDeliverButton { get; }

        /// <summary><c>contracts-produce-progress</c>: "Produced N of M" with a bounded bar on a production objective.</summary>
        public Button ProduceProgress { get; }

        /// <summary>The latest snapshot the panel drew from, or null before the first refresh.</summary>
        public ContractsSnapshot Snapshot => snapshot;

        public Label EmptyLabel => emptyLabel;
        public VisualElement ExpiredCard => expiredCard;
        public Label ExpiredLine => expiredLine;
        public VisualElement ActiveCard => activeCard;
        public Label ActiveRequirement => activeRequirement;
        public Label ActiveProgress => activeProgress;
        public Label ActiveMissing => activeMissing;
        public Label ActiveDeadline => activeDeadline;
        public Label ActiveRewards => activeRewards;
        public Label ActiveHint => activeHint;
        public Label ActiveFeedback => activeFeedback;
        public Label ActiveAutoHint => activeAutoHint;
        public Label ActiveBlocked => activeBlocked;
        public Label ProduceProgressText => produceText;
        public VisualElement ProduceProgressFill => produceFill;
        public Label ExpiredAutoNote => expiredAutoNote;
        public VisualElement OffersSection => offersSection;

        /// <summary>True while the Abandon confirmation is open.</summary>
        public bool IsAbandonConfirmationOpen => confirmOpen;

        public Button AcceptButton(string offerId) => offerRows[offerId].Accept;
        public VisualElement OfferCard(string offerId) => offerRows[offerId].Card;
        public Label OfferReason(string offerId) => offerRows[offerId].Reason;
        public Label OfferFeedback(string offerId) => offerRows[offerId].Feedback;
        public Label OfferRewards(string offerId) => offerRows[offerId].Rewards;
        public Label OfferRequirement(string offerId) => offerRows[offerId].Requirement;

        /// <summary><c>contracts-offer-cooldown-&lt;offerId&gt;</c>: "Available in m:ss", "Bonus, ends in m:ss" or "Bonus ended".</summary>
        public Label OfferCooldown(string offerId) => offerRows[offerId].Cooldown;
        public Label OfferDuration(string offerId) => offerRows[offerId].Duration;
        public IReadOnlyList<string> OfferIds => new List<string>(offerRows.Keys);

        public void OnShown(ShellPanelContext context)
        {
            shell = context?.Shell;
            if (shell != null && !ReferenceEquals(attachedShell, shell))
            {
                exactValue?.Dispose();
                attachedShell = shell;
                exactValue = shell.AttachExactValue(DeliverButton, ExactStockText);
                exactProduce?.Dispose();
                exactProduce = shell.AttachExactValue(ProduceProgress, ExactProducedText);
            }

            for (var i = 0; i < offerOrder.Count; i++)
            {
                offerOrder[i].ResetReservedHeight();
            }

            Invalidate();
            Refresh();
        }

        public void OnHidden()
        {
            // An open confirmation belongs to this panel; the shell keeps it open until it is answered, so only the stale flag is reset here.
            ClearFeedback();
        }

        /// <summary>Forces the next <see cref="Refresh"/> to redraw (for a state change that bumped no revision).</summary>
        public void Invalidate() => forceRefresh = true;

        /// <summary>
        /// Redraws when the structure, the quantities or the timeline second changed, or at the deadline instant. The shell calls it
        /// every frame while the panel is visible, so it does nothing in the common case (and never rebuilds focused controls).
        /// </summary>
        public void Refresh()
        {
            var second = session.TimelineUtcMs / 1000L;
            var expired = deadlineMs.HasValue && session.TimelineUtcMs > deadlineMs.Value;
            if (!forceRefresh && session.StructureRevision == keyStructure && session.QuantityRevision == keyQuantity &&
                second == keySecond && expired == keyExpired)
            {
                return;
            }

            forceRefresh = false;
            keyStructure = session.StructureRevision;
            keyQuantity = session.QuantityRevision;
            keySecond = second;
            keyExpired = expired;
            Render(RunSnapshots.Contracts(session));
        }

        /// <summary>
        /// Cancels an open Abandon confirmation because of a rotation, without abandoning (UX-5, UX-9). Register it with the
        /// bootstrap's transient actions. Returns whether something was open.
        /// </summary>
        public bool CancelForRotation()
        {
            if (!confirmOpen || shell == null)
            {
                return false;
            }

            return shell.CancelConfirmationForRotation();
        }

        // ---------------------------------------------------------------------------------------------------
        // Rendering
        // ---------------------------------------------------------------------------------------------------

        private void Render(ContractsSnapshot data)
        {
            snapshot = data;
            var active = data.Active;
            deadlineMs = active?.DeadlineUtcMs;
            keyExpired = deadlineMs.HasValue && data.TimelineUtcMs > deadlineMs.Value;

            var anyUnlocked = false;
            for (var i = 0; i < data.Offers.Count; i++)
            {
                anyUnlocked |= data.Offers[i].Unlocked;
            }

            var showEmpty = !anyUnlocked && active == null;
            SetDisplayed(emptyBox, showEmpty);
            if (showEmpty)
            {
                emptyLabel.text = UnlockSummary(data);
            }

            RenderExpired(data.LastExpired);
            RenderActive(active);
            SetDisplayed(offersSection, !showEmpty);
            for (var i = 0; i < data.Offers.Count; i++)
            {
                offerRows[data.Offers[i].OfferId].Render(data.Offers[i], active);
            }
        }

        private void RenderExpired(ExpiredContractSnapshot expired)
        {
            SetDisplayed(expiredCard, expired != null);
            if (expired == null)
            {
                SetDisplayed(expiredAutoNote, false);
                return;
            }

            // The setting is cleared by the expiry, so the panel remembers the last one it drew for this very contract (offer and deadline).
            var wasAuto = seenAutoOn && string.Equals(seenAutoOffer, expired.OfferId, StringComparison.Ordinal) && seenAutoDeadline == expired.DeadlineUtcMs;
            expiredAutoNote.text = wasAuto ? AutoDeliverShortStock : string.Empty;
            SetDisplayed(expiredAutoNote, wasAuto);

            expiredTitle.text = KindLabel(expired.Kind) + " expired";
            var resource = ResourceLabelOfOffer(expired.OfferId);
            expiredLine.text = "The deadline passed before " + QuantityFormatter.Format(expired.RequiredUnits) + " " + resource +
                " were delivered. " + ExpiredNoPenalty + " You can accept an offer again.";
        }

        private void RenderActive(ActiveContractSnapshot active)
        {
            SetDisplayed(activeCard, active != null);
            if (active == null)
            {
                return;
            }

            seenAutoOffer = active.OfferId;
            seenAutoDeadline = active.DeadlineUtcMs;
            seenAutoOn = active.AutoDeliver;

            var resource = ResourceLabel(active.ResourceId);
            var production = active.Objective == ProduceObjective;
            activeTitle.text = KindLabel(active.Kind);
            activeRewards.text = RewardText(active.Rewards);

            if (active.RemainingMs.HasValue)
            {
                activeDeadline.text = active.DeliverReason == RunReasons.ContractExpired
                    ? "Time is up."
                    : "Time left: " + RunRejectionCopy.Countdown(active.RemainingMs.Value);
                SetDisplayed(activeDeadline, true);
            }
            else
            {
                activeDeadline.text = "No time limit.";
                SetDisplayed(activeDeadline, true);
            }

            SetDisplayed(progressTrack, !production);
            SetDisplayed(activeProgress, !production);
            SetDisplayed(activeMissing, !production);
            SetDisplayed(ProduceProgress, production);
            SetDisplayed(DeliverButton, !production);
            SetDisplayed(AutoDeliverButton, active.CanAutoDeliver && !production);
            SetDisplayed(AbandonButton, active.CanAbandon);

            if (production)
            {
                RenderProduction(active, resource);
            }
            else
            {
                RenderDelivery(active, resource);
            }

            // One-line hint under the controls: what the toggle does, or that a production objective needs no control.
            string hint;
            if (production)
            {
                hint = ProductionHint;
            }
            else if (active.CanAutoDeliver)
            {
                hint = AutoDeliverHint;
            }
            else
            {
                hint = string.Empty;
            }

            activeAutoHint.text = hint;
            SetDisplayed(activeAutoHint, hint.Length > 0);
            activeBlocked.text = active.CompletionBlocked ? CompletionBlockedText : string.Empty;
            SetDisplayed(activeBlocked, active.CompletionBlocked);

            var autoSell = !production && session.State.IsAutoSell(active.ResourceId);
            activeHint.text = autoSell
                ? "Auto-sell is on for " + resource + ", but it keeps the stock this contract needs."
                : string.Empty;
            SetDisplayed(activeHint, autoSell);
        }

        private void RenderDelivery(ActiveContractSnapshot active, string resource)
        {
            activeRequirement.text = "Deliver " + QuantityFormatter.Format(active.RequiredUnits) + " " + resource;
            activeProgress.text = "In stock: " + QuantityFormatter.Format(active.StockUnits) + " of " + QuantityFormatter.Format(active.RequiredUnits);
            var ratio = active.RequiredUnits <= 0 ? 1f : Mathf.Clamp01((float)((double)active.StockUnits / active.RequiredUnits));
            progressFill.style.width = Length.Percent(ratio * 100f);
            progressTrack.EnableInClassList("contracts-progress--complete", active.MissingUnits == 0);

            if (active.MissingUnits > 0)
            {
                activeMissing.text = "Missing " + QuantityFormatter.Format(active.MissingUnits) + " " + resource + ".";
            }
            else
            {
                activeMissing.text = active.CanDeliver ? "Ready to deliver." : RunRejectionCopy.For(active.DeliverReason);
            }

            PanelIcons.Mark(activeMissing, icons, active.MissingUnits == 0 && !active.CanDeliver ? PanelIcons.Warning : null);
            RunShellDocument.SetSoftDisabled(DeliverButton, !active.CanDeliver);
            DeliverButton.text = active.CanDeliver ? "Deliver" : "Deliver (" + DeliverDisabledShort(active) + ")";

            // The setting is shown as text and in the accessible name, never by colour alone.
            AutoDeliverButton.text = "Auto-deliver: " + (active.AutoDeliver ? "On" : "Off");
            AutoDeliverButton.tooltip = "Auto-deliver, " + (active.AutoDeliver ? "on" : "off");
            AutoDeliverButton.EnableInClassList("contracts-autodeliver--on", active.AutoDeliver);
        }

        /// <summary>A production objective reads Produced / RequiredUnits; the stock fields are delivery-only and are never shown here.</summary>
        private void RenderProduction(ActiveContractSnapshot active, string resource)
        {
            activeRequirement.text = "Produce " + QuantityFormatter.Format(active.RequiredUnits) + " " + resource;
            var text = "Produced " + QuantityFormatter.Format(active.Produced) + " of " + QuantityFormatter.Format(active.RequiredUnits);
            produceText.text = text;
            ProduceProgress.tooltip = text;
            var ratio = active.RequiredUnits <= 0 ? 1f : Mathf.Clamp01((float)((double)active.Produced / active.RequiredUnits));
            produceFill.style.width = Length.Percent(ratio * 100f);
            produceTrack.EnableInClassList("contracts-progress--complete", active.Produced >= active.RequiredUnits);
        }

        private static string DeliverDisabledShort(ActiveContractSnapshot active) =>
            active.DeliverReason == RunReasons.ContractExpired ? "expired" : "missing " + QuantityFormatter.Format(active.MissingUnits);

        private string ExactProducedText()
        {
            var active = snapshot?.Active;
            return active == null
                ? string.Empty
                : "Produced " + QuantityFormatter.FormatExact(active.Produced) + " of " + QuantityFormatter.FormatExact(active.RequiredUnits) + " " + ResourceLabel(active.ResourceId);
        }

        private string ExactStockText()
        {
            var active = snapshot?.Active;
            return active == null
                ? string.Empty
                : "In stock " + QuantityFormatter.FormatExact(active.StockUnits) + " of " + QuantityFormatter.FormatExact(active.RequiredUnits) + " " + ResourceLabel(active.ResourceId);
        }

        // ---------------------------------------------------------------------------------------------------
        // Commands
        // ---------------------------------------------------------------------------------------------------

        private void OnAccept(string offerId, Button invoker)
        {
            ClearFeedback();
            var result = commands.AcceptContract(offerId);
            if (!result.IsSuccess)
            {
                ShowFeedback(offerRows[offerId].Feedback, RunRejectionCopy.For(result.ReasonCode), result.ReasonCode);
                Invalidate();
                Refresh();
                return;
            }

            shell?.ClearNotice();
            Invalidate();
            Refresh();
        }

        private void OnDeliver()
        {
            ClearFeedback();
            var result = commands.DeliverContract();
            if (!result.IsSuccess)
            {
                ShowFeedback(activeFeedback, RunRejectionCopy.For(result.ReasonCode), result.ReasonCode);
                Invalidate();
                Refresh();
                return;
            }

            var delivered = result.Value;
            shell?.ShowNotice(DeliveredText(delivered), ShellNoticeKind.Info);
            Invalidate();
            Refresh();
            RestoreFocus(DeliverButton);
        }

        private void OnAutoDeliver()
        {
            ClearFeedback();
            var active = RunSnapshots.Contracts(session).Active;
            var result = commands.SetAutoDeliver(active == null || !active.AutoDeliver);
            if (!result.IsSuccess)
            {
                ShowFeedback(activeFeedback, RunRejectionCopy.For(result.ReasonCode), result.ReasonCode);
            }

            // Turning it on can deliver at once (the stock is already there): the contract card then disappears and focus moves on.
            Invalidate();
            Refresh();
            RestoreFocus(AutoDeliverButton);
        }

        private void OnAbandon()
        {
            ClearFeedback();
            if (shell == null)
            {
                return;
            }

            confirmOpen = true;
            var opened = shell.RequestConfirmation(new ConfirmationRequest
            {
                Title = "Abandon this contract?",
                Message = AbandonMessage,
                ConfirmLabel = "Abandon",
                CancelLabel = "Keep contract",
                Destructive = true,
                Invoker = AbandonButton,
                OnConfirm = ConfirmAbandon,
                OnCancel = reason => confirmOpen = false,
            });
            if (!opened)
            {
                confirmOpen = false;
            }
        }

        private void ConfirmAbandon()
        {
            confirmOpen = false;
            var result = commands.AbandonContract();
            if (!result.IsSuccess)
            {
                ShowFeedback(activeFeedback, RunRejectionCopy.For(result.ReasonCode), result.ReasonCode);
            }
            else
            {
                shell?.ShowNotice("Contract abandoned. No reward, no penalty.", ShellNoticeKind.Info);
            }

            Invalidate();
            Refresh();
            RestoreFocus(AbandonButton);
        }

        private string DeliveredText(ContractDeliveryResult delivered)
        {
            var text = new StringBuilder("Delivered ");
            text.Append(QuantityFormatter.Format(delivered.UnitsConsumed));
            var items = delivered.ItemsGranted;
            var rewards = new List<string>();
            if (delivered.CashReward > 0)
            {
                rewards.Add("+" + QuantityFormatter.Format(delivered.CashReward) + " Cash");
            }

            if (delivered.RpReward > 0)
            {
                rewards.Add("+" + QuantityFormatter.Format(delivered.RpReward) + " RP");
            }

            for (var i = 0; items != null && i < items.Count; i++)
            {
                rewards.Add(ItemLabel(items[i].Id) + " x" + items[i].Count.ToString(CultureInfo.InvariantCulture));
            }

            if (rewards.Count > 0)
            {
                text.Append(": ").Append(string.Join(", ", rewards));
            }

            return text.Append('.').ToString();
        }

        private void ShowFeedback(Label target, string text, string reasonCode)
        {
            target.text = text;
            SetDisplayed(target, true);
        }

        private void ClearFeedback()
        {
            ResetFeedback(activeFeedback);
            for (var i = 0; i < offerOrder.Count; i++)
            {
                ResetFeedback(offerOrder[i].Feedback);
            }
        }

        private static void ResetFeedback(Label label)
        {
            label.text = string.Empty;
            SetDisplayed(label, false);
        }

        /// <summary>Gives focus to the first visible control when the one that was used is gone.</summary>
        private void RestoreFocus(VisualElement used)
        {
            if (shell == null || IsVisible(used))
            {
                return;
            }

            for (var i = 0; i < offerOrder.Count; i++)
            {
                if (IsVisible(offerOrder[i].Accept))
                {
                    shell.FocusWhenReady(offerOrder[i].Accept);
                    return;
                }
            }

            shell.FocusWhenReady(shell.TabButton(ShellTab.Contracts));
        }

        private bool IsVisible(VisualElement element)
        {
            for (var current = element; current != null && current != Root.parent; current = current.parent)
            {
                if (current.style.display == DisplayStyle.None)
                {
                    return false;
                }
            }

            return true;
        }

        // ---------------------------------------------------------------------------------------------------
        // Copy
        // ---------------------------------------------------------------------------------------------------

        private string ResourceLabel(string resourceId) =>
            resourceId != null && session.Catalog.Resources.TryGetValue(resourceId, out var def) ? def.Label : resourceId ?? string.Empty;

        private string ResourceLabelOfOffer(string offerId) =>
            offerId != null && session.Catalog.ContractOffers.TryGetValue(offerId, out var offer) ? ResourceLabel(offer.ResourceId) : string.Empty;

        private string ItemLabel(string itemId) =>
            itemId != null && session.Catalog.Items.TryGetValue(itemId, out var item) ? item.Label : itemId ?? string.Empty;

        public static string KindLabel(string kind)
        {
            switch (kind)
            {
                case "plate": return "Plate contract";
                case "gear": return "Gear contract";
                case "timedBar": return "Timed bar contract";
                default: return string.IsNullOrEmpty(kind) ? "Contract" : kind;
            }
        }

        private string RewardText(IReadOnlyList<ContractRewardView> rewards)
        {
            if (rewards == null || rewards.Count == 0)
            {
                return "No reward left.";
            }

            var parts = new List<string>();
            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                switch (reward.Kind)
                {
                    case "cash": parts.Add(QuantityFormatter.Format(reward.Amount) + " Cash"); break;
                    case "rp": parts.Add(QuantityFormatter.Format(reward.Amount) + " RP"); break;
                    default: parts.Add(ItemLabel(reward.ItemId) + " x" + reward.Amount.ToString(CultureInfo.InvariantCulture)); break;
                }
            }

            return "Reward: " + string.Join(", ", parts);
        }

        /// <summary>"Unlocks after you produce Iron Plate" for one condition.</summary>
        private string UnlockText(UnlockCondition condition) => UnlockCopy.After(session.Catalog, condition);

        private string UnlockSummary(ContractsSnapshot data)
        {
            var parts = new List<string>();
            for (var i = 0; i < data.Offers.Count; i++)
            {
                var text = UnlockText(data.Offers[i].Unlock);
                if (!parts.Contains(text))
                {
                    parts.Add(text);
                }
            }

            return parts.Count == 0 ? "Contracts unlock after you build more of the factory." : "Contracts unlock after " + string.Join(" or ", parts) + ".";
        }

        // ---------------------------------------------------------------------------------------------------
        // Element helpers
        // ---------------------------------------------------------------------------------------------------

        private static VisualElement MakeCard(string name, string variant)
        {
            var card = new VisualElement { name = name };
            card.AddToClassList("contracts-card");
            card.AddToClassList(variant);
            return card;
        }

        private static Label MakeText(string name, string className)
        {
            var label = new Label { name = name };
            label.AddToClassList(className);
            return label;
        }

        private static void SetDisplayed(VisualElement element, bool displayed) =>
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;

        private sealed class OfferRow
        {
            private readonly string offerId;
            private readonly ContractsPanel owner;

            public OfferRow(string offerId, ContractsPanel owner)
            {
                this.offerId = offerId;
                this.owner = owner;
                Card = MakeCard("contracts-offer-" + offerId, "contracts-offer");
                Title = MakeText("contracts-offer-title-" + offerId, "contracts-title");
                Requirement = MakeText("contracts-offer-requirement-" + offerId, "contracts-line");
                Duration = MakeText("contracts-offer-duration-" + offerId, "contracts-line");
                Rewards = MakeText("contracts-offer-rewards-" + offerId, "contracts-line");
                Cooldown = MakeText("contracts-offer-cooldown-" + offerId, "contracts-line");
                Cooldown.AddToClassList("contracts-cooldown");
                Cooldown.RegisterCallback<GeometryChangedEvent>(OnCooldownGeometry);
                Reason = MakeText("contracts-offer-reason-" + offerId, "contracts-hint");
                Feedback = MakeText("contracts-offer-feedback-" + offerId, "contracts-feedback");
                Accept = RunShellDocument.CreateButton("contracts-accept-" + offerId, "Accept", true);
                Accept.clicked += () => owner.OnAccept(offerId, Accept);
                foreach (var element in new VisualElement[] { Title, Requirement, Duration, Rewards, Cooldown, Reason, Accept, Feedback })
                {
                    Card.Add(element);
                }

                SetDisplayed(Feedback, false);
                SetDisplayed(Cooldown, false);
                PanelIcons.Mark(Feedback, owner.icons, PanelIcons.Warning);
                PanelIcons.Mark(Reason, owner.icons, PanelIcons.Warning);
            }

            public VisualElement Card { get; }
            public Label Title { get; }
            public Label Requirement { get; }
            public Label Duration { get; }
            public Label Rewards { get; }
            public Label Cooldown { get; }
            public Label Reason { get; }
            public Label Feedback { get; }
            public Button Accept { get; }

            private bool reserved;

            /// <summary>Forgets the reserved line height (a text-scale change can make it too tall).</summary>
            public void ResetReservedHeight() => Cooldown.style.minHeight = StyleKeyword.Null;

            // The countdown line changes text every second and at the bonus end; its height only grows, so the row never shrinks and jumps.
            private void OnCooldownGeometry(GeometryChangedEvent evt)
            {
                var height = Cooldown.layout.height;
                if (!float.IsNaN(height) && height > 0f && Cooldown.resolvedStyle.display != DisplayStyle.None)
                {
                    var current = Cooldown.style.minHeight.keyword == StyleKeyword.Undefined ? Cooldown.style.minHeight.value.value : 0f;
                    if (height > current + 0.5f)
                    {
                        Cooldown.style.minHeight = height;
                    }
                }
            }

            private string CooldownText(ContractOfferSnapshot offer, ActiveContractSnapshot active)
            {
                if (offer.Class == BonusClass)
                {
                    reserved = true;
                    if (active != null && active.OfferId == offerId)
                    {
                        return "Bonus accepted.";
                    }

                    if (!offer.BonusWindowRemainingMs.HasValue)
                    {
                        return "Bonus not active yet.";
                    }

                    if (offer.BonusWindowRemainingMs.Value <= 0)
                    {
                        return "Bonus ended";
                    }

                    if (offer.CooldownRemainingMs > 0 || offer.AcceptReason == RunReasons.ContractBonusUnavailable)
                    {
                        return "Bonus used";
                    }

                    return "Bonus, ends in " + RunRejectionCopy.Countdown(offer.BonusWindowRemainingMs.Value);
                }

                if (offer.CooldownRemainingMs > 0)
                {
                    reserved = true;
                    return "Available in " + RunRejectionCopy.Countdown(offer.CooldownRemainingMs);
                }

                // Once an offer has cooled down the line stays with its last word, so the card does not shrink when the countdown ends.
                return reserved ? "Available now" : string.Empty;
            }

            public void Render(ContractOfferSnapshot offer, ActiveContractSnapshot active)
            {
                Title.text = KindLabel(offer.Kind);
                Card.EnableInClassList("contracts-card--locked", !offer.Unlocked);
                var resource = owner.ResourceLabel(offer.ResourceId);
                var exhausted = offer.Unlocked && offer.RequiredUnits <= 0;
                var verb = offer.Objective == ProduceObjective ? "Produce " : "Deliver ";
                Requirement.text = offer.Unlocked && !exhausted
                    ? verb + QuantityFormatter.Format(offer.RequiredUnits) + " " + resource
                    : verb + resource;
                SetDisplayed(Requirement, true);
                Duration.text = offer.DurationMs.HasValue
                    ? "Time limit: " + RunRejectionCopy.Countdown(offer.DurationMs.Value)
                    : "No time limit.";
                Rewards.text = offer.Unlocked && !exhausted ? owner.RewardText(offer.Rewards) : string.Empty;
                SetDisplayed(Rewards, offer.Unlocked && !exhausted);

                string reason;
                if (!offer.Unlocked)
                {
                    reason = "Locked: unlocks after " + owner.UnlockText(offer.Unlock) + ".";
                }
                else if (exhausted)
                {
                    reason = "There are no more of these contracts in this run.";
                }
                else if (offer.CanAccept)
                {
                    reason = string.Empty;
                }
                else
                {
                    reason = RunRejectionCopy.For(offer.AcceptReason);
                }

                var cooldown = offer.Unlocked && !exhausted ? CooldownText(offer, active) : string.Empty;
                Cooldown.text = cooldown;
                SetDisplayed(Cooldown, cooldown.Length > 0);

                Reason.text = reason;
                SetDisplayed(Reason, reason.Length > 0);
                PanelIcons.Mark(Reason, owner.icons, offer.Unlocked ? PanelIcons.Warning : PanelIcons.Lock);
                RunShellDocument.SetSoftDisabled(Accept, !offer.CanAccept);
            }
        }
    }

    /// <summary>
    /// A wrapped label that is the only text of a scroll view's content is measured once with the viewport as its height limit and is not
    /// measured again, so its tail can be clipped in a short sheet (the shell's dialog message has the same problem, T112). The label's
    /// minimum height is set to its real wrapped height, which makes the scroll extent right; the change re-runs this handler until stable.
    /// </summary>
    internal static class WrappedLabelFit
    {
        public static void Attach(Label label) => label.RegisterCallback<GeometryChangedEvent>(OnGeometry);

        private static void OnGeometry(GeometryChangedEvent evt)
        {
            var label = (Label)evt.target;
            var width = label.contentRect.width;
            if (width <= 0f || string.IsNullOrEmpty(label.text))
            {
                return;
            }

            var needed = label.MeasureTextSize(label.text, width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined).y;
            if (PanelIcons.HasIcon(label))
            {
                // The beside-text icon is 24 pt tall (PanelIcons); a one-line text must not shrink the label below it.
                needed = Mathf.Max(needed, PanelIcons.IconSize);
            }

            var current = label.style.minHeight.keyword == StyleKeyword.Undefined ? label.style.minHeight.value.value : 0f;
            if (Mathf.Abs(needed - current) > 0.5f)
            {
                label.style.minHeight = needed;
            }
        }
    }
}
