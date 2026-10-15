using System;
using System.Globalization;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The goal line at the top of Market (<c>goals-next</c>, T333, FR-018, F5-R07; contracts/ui-surfaces-f005.md). It shows the next
    /// Factory Value goal ("Next goal $X: $Y of $X (Z %)"), "Factory Sale available" at the threshold and "All goals reached" once the
    /// content milestones and the threshold are all passed. Content without milestones shows the threshold as the only goal. A reached
    /// goal raises one notice through the shell (presentation state keyed by run and milestone index, seeded without notifying when the
    /// panel is shown, a run changes or a save loads, so passed goals never re-notify). Milestones grant nothing.
    /// The line is Market-only (a known limitation until F004 places it on the Factory view): goals crossed while Market is not shown
    /// are seeded silently and raise no notice.
    /// The panel reads the run only through <see cref="GoalsTracker"/> (the cached snapshot, rebuilt on a goal crossing) and holds no
    /// rule. The percentage is always <see cref="GoalsSnapshot.PercentAt"/> of the live Factory Value, never the cached
    /// <see cref="GoalsSnapshot.PercentFloored"/>. "All goals reached" combines <see cref="GoalsSnapshot.AllReached"/> with
    /// <see cref="GoalsSnapshot.FactorySaleAvailable"/> because <c>AllReached</c> is true while the threshold is still the next goal.
    /// </summary>
    public sealed class GoalsLinePanel
    {
        public const string NextName = "goals-next";
        public const string ExactName = "goals-exact";
        public const string AllReachedName = "goals-all-reached";
        public const string ReasonName = "goals-reason";
        public const string SaleText = "Factory Sale available";
        public const string AllReachedText = "All goals reached";
        public const string UnavailableText = "The goal line is not available for this run.";

        private readonly RunSession session;
        private readonly RunShellDocument shell;
        private readonly Func<long> playSeconds;
        private readonly GoalsTracker tracker = new GoalsTracker();
        private readonly VisualElement root;
        private readonly Label next;
        private readonly Button exactButton;
        private readonly Label allReached;
        private readonly Label reason;
        private string exact = string.Empty;
        private long structureRevision = -1;
        private long quantityRevision = -1;
        private float lastRender = float.NegativeInfinity;
        private Guid noticeRun;
        private bool noticeSeeded;
        private int noticedMilestones;
        private bool noticedSale;

        /// <param name="playSeconds">The run's play time in whole seconds, for the <see cref="RunMilestoneKind.Run2Eligible"/> event. Null reads the active time since the session anchor.</param>
        public GoalsLinePanel(RunSession session, RunShellDocument shell, StyleSheet styles = null, Func<string, Sprite> icons = null, Func<long> playSeconds = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            this.playSeconds = playSeconds ?? DefaultPlaySeconds;
            root = new VisualElement { name = "goals-panel" };
            root.AddToClassList("goals-panel");
            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            // The goal sentence is a wrapping Label: a long sentence in a 44 pt Button is taller than a short landscape side panel at 200 %
            // text and cannot be scrolled fully into view. The focusable control is the compact exact-value button below it.
            next = MarketPanelParts.NewLabel(NextName, "goals-next");
            WrappedLabelFit.Attach(next);
            root.Add(next);
            exactButton = MarketPanelParts.NewValueButton(ExactName, "goals-exact");
            exactButton.text = "Exact value";
            root.Add(exactButton);
            allReached = MarketPanelParts.NewLabel(AllReachedName, "goals-caption", AllReachedText);
            WrappedLabelFit.Attach(allReached);
            root.Add(allReached);
            reason = MarketPanelParts.NewLabel(ReasonName, "goals-caption", UnavailableText);
            WrappedLabelFit.Attach(reason);
            PanelIcons.Mark(reason, icons, PanelIcons.Warning);
            root.Add(reason);
            shell.AttachExactValue(exactButton, () => exact); // the shell owns and disposes the binding
            MarketPanelParts.SetVisible(allReached, false);
            MarketPanelParts.SetVisible(reason, false);
        }

        public VisualElement Root => root;
        public Label NextLabel => next;
        public Button ExactButton => exactButton;
        public Label AllReachedLabel => allReached;
        public Label ReasonLabel => reason;

        /// <summary>The exact-value detail text of the line (the value the shell shows when <see cref="ExactButton"/> is focused, hovered or tapped).</summary>
        public string ExactText => exact;

        /// <summary>The cached tracker, for tests (rebuild count).</summary>
        public GoalsTracker Tracker => tracker;

        /// <summary>The minimum time between two renders caused by a Factory Value change alone (default 0.25 s; tests set 0).</summary>
        public float RefreshIntervalSeconds { get; set; } = 0.25f;

        public bool IsNextShown => next.style.display != DisplayStyle.None;
        public bool IsAllReachedShown => allReached.style.display != DisplayStyle.None;
        public bool IsReasonShown => reason.style.display != DisplayStyle.None;

        /// <summary>Renders at once and seeds the one-time notice state from the current run, so goals passed while Market was hidden never notify.</summary>
        public void OnShown(ShellPanelContext context) => Render(true);

        public void Refresh()
        {
            if (session.StructureRevision != structureRevision ||
                (session.QuantityRevision != quantityRevision && Time.realtimeSinceStartup - lastRender >= RefreshIntervalSeconds))
            {
                Render(false);
            }
        }

        public void OnHidden()
        {
        }

        /// <summary>Forces the next <see cref="Refresh"/> to redraw.</summary>
        public void Invalidate()
        {
            structureRevision = -1;
        }

        private long DefaultPlaySeconds() => session.ActiveTicksSinceAnchor / (RunSession.TicksPerMs * 1000L);

        /// <summary>The live Factory Value; an overflowing value counts as the exact bound, like the tracker's own read.</summary>
        private long LiveFactoryValue() =>
            FactoryValuation.TryCompute(session.Catalog, session.State, session.Permanent, out var breakdown) ? breakdown.Total : ExactBound.Value;

        private void Render(bool seed)
        {
            structureRevision = session.StructureRevision;
            quantityRevision = session.QuantityRevision;
            lastRender = Time.realtimeSinceStartup;
            var goals = tracker.Current(session, playSeconds());
            var factoryValue = LiveFactoryValue();
            Apply(goals, factoryValue);
            Notify(goals, seed);
        }

        private void Apply(GoalsSnapshot goals, long factoryValue)
        {
            if (!goals.Available)
            {
                MarketPanelParts.SetVisible(next, false);
                MarketPanelParts.SetVisible(exactButton, false);
                MarketPanelParts.SetVisible(allReached, false);
                MarketPanelParts.SetVisible(reason, true);
                exact = UnavailableText;
                exactButton.tooltip = UnavailableText.TrimEnd('.') + ", exact value";
                return;
            }

            MarketPanelParts.SetVisible(reason, false);
            MarketPanelParts.SetVisible(next, true);
            MarketPanelParts.SetVisible(exactButton, true);

            // The Sale line wins over the percentage; "All goals reached" shows beside it only when content defined milestones.
            if (goals.FactorySaleAvailable)
            {
                next.text = SaleText;
                next.tooltip = SaleText;
                exactButton.tooltip = SaleText + ", exact value";
                exact = SaleText + ". Factory Value " + QuantityFormatter.FormatExact(factoryValue) + " meets the threshold of " +
                    QuantityFormatter.FormatExact(goals.Threshold) + ".";
                MarketPanelParts.SetVisible(allReached, goals.AllReached && goals.MilestoneCount > 0);
                return;
            }

            MarketPanelParts.SetVisible(allReached, false);
            var percent = goals.PercentAt(factoryValue).ToString(CultureInfo.InvariantCulture);
            var target = MarketPanelParts.Money(goals.Target);
            next.text = "Next goal " + target + ": " + MarketPanelParts.Money(factoryValue) + " of " + target + " (" + percent + " %)";
            next.tooltip = "Next goal " + target + ", " + percent + " percent";
            exactButton.tooltip = next.tooltip + ", exact value";
            exact = "Factory Value " + QuantityFormatter.FormatExact(factoryValue) + " of " + MarketPanelParts.MoneyExact(goals.Target) +
                " for the next goal (" + percent + " percent)" + (goals.NextIsThreshold ? ", the Factory Sale threshold." : ".");
        }

        /// <summary>
        /// One notice per goal, keyed (run, milestone index) and never repeated (F5-R07): the high-water mark only rises, so a Factory
        /// Value that dips below a passed goal and rises again stays quiet. A seed, a first read and a new run notify nothing.
        /// </summary>
        private void Notify(GoalsSnapshot goals, bool seed)
        {
            if (!goals.Available)
            {
                return;
            }

            var run = session.State.RunId;
            var newRun = !noticeSeeded || run != noticeRun;
            if (newRun)
            {
                noticedMilestones = 0;
                noticedSale = false;
            }

            if (newRun || seed)
            {
                noticeRun = run;
                noticeSeeded = true;
                noticedMilestones = Math.Max(noticedMilestones, goals.ReachedMilestones);
                noticedSale |= goals.FactorySaleAvailable;
                return;
            }

            string text = null;
            if (goals.FactorySaleAvailable && !noticedSale)
            {
                noticedSale = true;
                text = SaleText;
            }

            if (goals.ReachedMilestones > noticedMilestones)
            {
                noticedMilestones = goals.ReachedMilestones;
                text = text ?? "Goal reached: " + goals.ReachedMilestones.ToString(CultureInfo.InvariantCulture) + " of " +
                    goals.MilestoneCount.ToString(CultureInfo.InvariantCulture);
            }

            if (text != null)
            {
                shell.ShowNotice(text);
            }
        }
    }
}
