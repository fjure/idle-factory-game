#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.ResearchPanelTestSupport;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T116 (FR-016, FR-032, US3 scenario 3, OD-8, UX-3): the Research graph. Its keyboard order (prerequisite depth, then content order),
    /// the arrow keys to the nearest node, 44 pt nodes with level, cost and effect, the prerequisite edges, a missing prerequisite or RP
    /// shown as the reason before the tap, the purchase through the application, the focused node kept in view at phone width, and the
    /// worst-case layout at every screen class (rotations included) at 100 and 200 percent text scale.
    /// </summary>
    public sealed class ResearchPanelTests
    {
        private static readonly string[] ExpectedOrder =
        {
            Bargaining, MinerTuning,
            DeepVeins, MarketAnalysis, SmelterTuning, ToolDiscount,
            PressTuning, WarehousePlanning,
            AssemblerTuning,
        };

        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private ResearchPanel panel;
        private IndustryPanel industry;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        public static IEnumerable<ShellScreen> Representative()
        {
            var all = ShellScreens.All().ToList();
            yield return all.First(screen => screen.Name.StartsWith("phone-portrait 360", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("phone-landscape 640", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("tablet-portrait", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("desktop 1280", StringComparison.Ordinal));
        }

        private static ShellScreen Phone360() =>
            ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 360", StringComparison.Ordinal));

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
            panel = null;
            industry = null;
        }

        private IEnumerator Open(ShellScreen screen, int percent = 100, bool withIndustry = false)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            session = NewSession(out commands);
            industry = withIndustry ? new IndustryPanel(session, commands, Styles("IndustryPanel.uss"), Icon) : null;
            panel = new ResearchPanel(session, commands, Styles("ResearchPanel.uss"), Icon, industry);
            rig.Shell.Panels.Register(panel);
            rig.Shell.SelectTab(ShellTab.Research);
            yield return ShellRig.Settle();
        }

        private IEnumerator Sync()
        {
            panel.Invalidate();
            panel.Refresh();
            yield return ShellRig.Settle();
        }

        private static bool Shown(VisualElement element) => IsDisplayed(element);

        private static Vector2 Center(VisualElement element) => element.worldBound.center;

        // ---------------------------------------------------------------------------------------------------
        // Mount, empty state, content
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheShellMountsThePanelUnderTheResearchTab([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Research));
            Assert.That(panel.Id, Is.EqualTo(ShellPanelIds.Research));
            Assert.That(panel.Root.parent, Is.SameAs(rig.Shell.PanelContent));
        }

        [UnityTest]
        public IEnumerator WithoutRpTheEmptyStateSendsThePlayerToContractsAndDisappearsOnceRpIsEarned()
        {
            yield return Open(Phone360());
            Assert.That(Shown(panel.EmptyLabel), Is.True);
            Assert.That(panel.EmptyLabel.text, Is.EqualTo("Earn RP from contracts."));
            Assert.That(panel.RpLabel.text, Is.EqualTo("Research points: 0 RP"));

            GrantRp(session, 12);
            yield return Sync();
            Assert.That(Shown(panel.EmptyLabel), Is.False);
            Assert.That(panel.RpLabel.text, Is.EqualTo("Research points: 12 RP"));
        }

        [UnityTest]
        public IEnumerator EveryNodeShowsItsLevelCostAndEffect()
        {
            yield return Open(Phone360());
            var miner = panel.NodeButton(MinerTuning).text;
            var cost = session.Catalog.Research[MinerTuning].RpCostTable[0];
            Assert.That(miner, Does.Contain("Miner Tuning"));
            Assert.That(miner, Does.Contain("Lv 0/5"));
            Assert.That(miner, Does.Contain(QuantityFormatter.Format(cost) + " RP"));
            Assert.That(miner, Does.Contain("Speed x1.05 on Miner"));

            Assert.That(panel.NodeButton(DeepVeins).text, Does.Contain("Output x2 on Miner"));
            Assert.That(panel.NodeButton(Bargaining).text, Does.Contain("Sale price x1.05 on all resources"));
            Assert.That(panel.NodeButton(ToolDiscount).text, Does.Contain("Upgrade cost x0."));
            Assert.That(panel.NodeButton(MarketAnalysis).text, Does.Contain("Sale price x1.1 on "));
        }

        // ---------------------------------------------------------------------------------------------------
        // The graph: keyboard order, columns and edges (OD-8)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheKeyboardOrderIsPrerequisiteDepthThenContentOrder()
        {
            yield return Open(Phone360());
            Assert.That(panel.NodeOrder, Is.EqualTo(ExpectedOrder));

            // Independently of the panel: depth is the longest prerequisite chain, ties keep the catalog's content order.
            var catalog = session.Catalog;
            int Depth(string id) => catalog.Research[id].Prerequisites.Count == 0
                ? 0
                : 1 + catalog.Research[id].Prerequisites.Max(prerequisite => Depth(prerequisite.ResearchId));
            var content = catalog.ResearchInContentOrder.Select(def => def.Id).ToList();
            var expected = content.OrderBy(Depth).ThenBy(id => content.IndexOf(id)).ToList();
            Assert.That(panel.NodeOrder, Is.EqualTo(expected));

            // Tab (the shell's focus order) visits the nodes in exactly that order.
            var focusOrder = rig.Shell.GetFocusOrder().OfType<Button>().Where(button => button.name.StartsWith("research-node-", StringComparison.Ordinal))
                .Select(button => button.name.Substring("research-node-".Length)).ToList();
            Assert.That(focusOrder, Is.EqualTo(ExpectedOrder));
        }

        [UnityTest]
        public IEnumerator DepthRunsFromLeftToRightAndTheNodesOfADepthKeepContentOrderTopToBottom([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            var depthOf = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var node in panel.Snapshot.Nodes)
            {
                depthOf[node.ResearchId] = node.Depth;
            }

            for (var i = 1; i < ExpectedOrder.Length; i++)
            {
                var previous = panel.NodeButton(ExpectedOrder[i - 1]).layout;
                var current = panel.NodeButton(ExpectedOrder[i]).layout;
                var previousColumn = panel.NodeButton(ExpectedOrder[i - 1]).parent.layout;
                var currentColumn = panel.NodeButton(ExpectedOrder[i]).parent.layout;
                if (depthOf[ExpectedOrder[i]] == depthOf[ExpectedOrder[i - 1]])
                {
                    Assert.That(currentColumn.x, Is.EqualTo(previousColumn.x).Within(0.5f), "same depth, same column");
                    Assert.That(current.y, Is.GreaterThan(previous.y), ExpectedOrder[i] + " sits below " + ExpectedOrder[i - 1]);
                }
                else
                {
                    Assert.That(currentColumn.x, Is.GreaterThan(previousColumn.x), ExpectedOrder[i] + " sits right of " + ExpectedOrder[i - 1]);
                }
            }

            foreach (var node in panel.Snapshot.Nodes)
            {
                foreach (var prerequisite in node.Prerequisites)
                {
                    Assert.That(panel.NodeButton(prerequisite.ResearchId).parent.layout.x, Is.LessThan(panel.NodeButton(node.ResearchId).parent.layout.x),
                        prerequisite.ResearchId + " is left of " + node.ResearchId);
                }
            }
        }

        [UnityTest]
        public IEnumerator EveryPrerequisiteIsAnEdgeAndAMetOneIsToldFromAnOpenOne()
        {
            yield return Open(Phone360());
            var edges = panel.EdgeList;
            Assert.That(edges.Count, Is.EqualTo(7));
            Assert.That(edges, Does.Contain((MinerTuning, SmelterTuning, false)));
            Assert.That(edges, Does.Contain((SmelterTuning, PressTuning, false)));
            Assert.That(edges, Does.Contain((PressTuning, AssemblerTuning, false)));
            Assert.That(edges, Does.Contain((Bargaining, MarketAnalysis, false)));
            Assert.That(edges.All(edge => !edge.Met), Is.True, "nothing is researched yet");

            SetResearchLevel(session, MinerTuning, 2);
            yield return Sync();
            Assert.That(panel.EdgeList, Does.Contain((MinerTuning, SmelterTuning, true)));
            Assert.That(panel.EdgeList, Does.Contain((MinerTuning, ToolDiscount, true)));
            Assert.That(panel.EdgeList, Does.Contain((MinerTuning, DeepVeins, false)), "Deep Veins needs level 3");
        }

        // ---------------------------------------------------------------------------------------------------
        // 44 pt nodes
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryNodeIsAtLeast44PointsAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            foreach (var button in panel.NodeButtons)
            {
                Assert.That(button.layout.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{screen} @{percent}%: {button.name} width");
                Assert.That(button.layout.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{screen} @{percent}%: {button.name} height");
                Assert.That(button.focusable, Is.True);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Disabled with the reason, and the purchase
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AMissingPrerequisiteOrMissingRpIsShownAsTheReasonBeforeTheTap()
        {
            yield return Open(Phone360());
            var miner = panel.NodeButton(MinerTuning);
            var smelter = panel.NodeButton(SmelterTuning);
            var cost = session.Catalog.Research[MinerTuning].RpCostTable[0];
            Assert.That(IsSoftDisabled(miner), Is.True);
            Assert.That(miner.text, Does.Contain("Need " + QuantityFormatter.Format(cost) + " more RP"));
            Assert.That(IsSoftDisabled(smelter), Is.True);
            Assert.That(smelter.text, Does.Contain("Locked: needs Miner Tuning Lv 2"));

            smelter.Focus();
            yield return ShellRig.Settle();
            Assert.That(panel.SelectedId, Is.EqualTo(SmelterTuning));
            Assert.That(panel.DetailReason.text, Is.EqualTo("Research its prerequisites first."));
            Assert.That(panel.DetailNeeds.text, Does.Contain("Needs Miner Tuning level 2: not yet (now level 0)"));

            miner.Focus();
            yield return ShellRig.Settle();
            Assert.That(panel.DetailReason.text, Is.EqualTo("Not enough Research points."));
        }

        private static bool IsSoftDisabled(VisualElement element) => RunShellDocument.IsSoftDisabled(element);

        [UnityTest]
        public IEnumerator LockedNodesShowTheLockAndRejectionsTheWarningBesideTheirTextAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, withIndustry: true);
            GrantRp(session, 1);
            yield return Sync();
            var where = $"{screen} @{percent}%";

            var smelter = panel.NodeButton(SmelterTuning);
            smelter.Focus();
            yield return ShellRig.Settle();
            Assert.That(panel.SelectedId, Is.EqualTo(SmelterTuning));
            ContractsPanelTestSupport.AssertIconBesideText(panel.DetailReason, PanelIcons.Lock, where + " locked node");

            var miner = panel.NodeButton(MinerTuning);
            miner.Focus();
            yield return ShellRig.Settle();
            ContractsPanelTestSupport.AssertIconBesideText(panel.DetailReason, PanelIcons.Warning, where + " unaffordable node");
            Submit(miner);
            yield return ShellRig.Settle();
            ContractsPanelTestSupport.AssertIconBesideText(panel.Feedback, PanelIcons.Warning, where + " rejection");
            ContractsPanelTestSupport.AssertIconBesideText(industry.ReasonLabel(Calibration), PanelIcons.Warning, where + " industry reason");
            ContractsPanelTestSupport.AssertTextButtonsHaveNoChildren(panel.Root, where);
            AssertShellLayout(rig, "research with lock and warning icons");
        }

        [UnityTest]
        public IEnumerator APressOnALockedOrUnaffordableNodeShowsTheRejectionAndChangesNothing()
        {
            yield return Open(Phone360());
            GrantRp(session, 1);
            yield return Sync();

            var smelter = panel.NodeButton(SmelterTuning);
            smelter.Focus();
            Submit(smelter);
            yield return ShellRig.Settle();
            Assert.That(Shown(panel.Feedback), Is.True);
            Assert.That(panel.Feedback.text, Is.EqualTo("Research its prerequisites first."));
            Assert.That(session.State.ResearchLevel(SmelterTuning), Is.Zero);
            Assert.That(session.State.Rp, Is.EqualTo(1));

            var miner = panel.NodeButton(MinerTuning);
            miner.Focus();
            Submit(miner);
            yield return ShellRig.Settle();
            Assert.That(panel.Feedback.text, Is.EqualTo("Not enough Research points."));
            Assert.That(session.State.ResearchLevel(MinerTuning), Is.Zero);
            Assert.That(session.State.Rp, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator APressOnAnAffordableNodeBuysOneLevelThroughTheApplication([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            GrantRp(session, 100);
            yield return Sync();
            var cost = session.Catalog.Research[MinerTuning].RpCostTable[0];
            var miner = panel.NodeButton(MinerTuning);
            Assert.That(IsSoftDisabled(miner), Is.False);
            Assert.That(miner.text, Does.Contain("Tap to research"));

            miner.Focus();
            Submit(miner);
            yield return ShellRig.Settle();
            Assert.That(session.State.ResearchLevel(MinerTuning), Is.EqualTo(1));
            Assert.That(session.State.Rp, Is.EqualTo(100 - cost));
            Assert.That(miner.text, Does.Contain("Lv 1/5"));
            Assert.That(panel.RpLabel.text, Is.EqualTo("Research points: " + QuantityFormatter.Format(100 - cost) + " RP"));
            Assert.That(rig.Shell.NoticeText, Is.EqualTo("Miner Tuning is now level 1."));
            Assert.That(Shown(panel.Feedback), Is.False);
            Assert.That(Focused(rig.Root), Is.SameAs(miner), "focus stays on the node");
        }

        [UnityTest]
        public IEnumerator ResearchingAPrerequisiteOpensTheNodesThatNeedIt()
        {
            yield return Open(Phone360());
            GrantRp(session, 1_000);
            yield return Sync();
            var smelter = panel.NodeButton(SmelterTuning);
            Assert.That(IsSoftDisabled(smelter), Is.True);
            for (var i = 0; i < 2; i++)
            {
                var miner = panel.NodeButton(MinerTuning);
                miner.Focus();
                Submit(miner);
                yield return ShellRig.Settle();
            }

            Assert.That(session.State.ResearchLevel(MinerTuning), Is.EqualTo(2));
            Assert.That(IsSoftDisabled(smelter), Is.False);
            Assert.That(smelter.text, Does.Contain("Tap to research"));
            Assert.That(panel.EdgeList, Does.Contain((MinerTuning, SmelterTuning, true)));
        }

        [UnityTest]
        public IEnumerator AMaxedNodeSaysSoAndKeepsItsReason()
        {
            yield return Open(Phone360());
            SetResearchLevel(session, MinerTuning, 3);
            SetResearchLevel(session, DeepVeins, 1);
            GrantRp(session, 1_000);
            yield return Sync();
            var node = panel.NodeButton(DeepVeins);
            Assert.That(node.text, Does.Contain("Lv 1/1 · Max"));
            Assert.That(node.text, Does.Contain("Fully researched"));
            Assert.That(IsSoftDisabled(node), Is.True);
            node.Focus();
            Submit(node);
            yield return ShellRig.Settle();
            Assert.That(panel.Feedback.text, Is.EqualTo("Already at the maximum level."));
            Assert.That(session.State.ResearchLevel(DeepVeins), Is.EqualTo(1));
        }

        // ---------------------------------------------------------------------------------------------------
        // Arrow keys
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheNearestPointInADirectionWinsAndAPointInsideTheConeBeatsOneOutsideIt()
        {
            var points = new List<Vector2>
            {
                new Vector2(0f, 0f),     // 0: origin
                new Vector2(100f, 10f),  // 1: right, close
                new Vector2(100f, 60f),  // 2: right, lower
                new Vector2(220f, 0f),   // 3: right, far
                new Vector2(10f, 90f),   // 4: below
                new Vector2(-100f, 0f),  // 5: left
                new Vector2(5f, -80f),   // 6: above
            };
            Assert.That(ResearchPanel.NearestInDirection(points, 0, NavigationMoveEvent.Direction.Right), Is.EqualTo(1));
            Assert.That(ResearchPanel.NearestInDirection(points, 0, NavigationMoveEvent.Direction.Left), Is.EqualTo(5));
            Assert.That(ResearchPanel.NearestInDirection(points, 0, NavigationMoveEvent.Direction.Down), Is.EqualTo(4));
            Assert.That(ResearchPanel.NearestInDirection(points, 0, NavigationMoveEvent.Direction.Up), Is.EqualTo(6));
            Assert.That(ResearchPanel.NearestInDirection(points, 3, NavigationMoveEvent.Direction.Right), Is.EqualTo(-1), "nothing further right");
            Assert.That(ResearchPanel.NearestInDirection(points, 5, NavigationMoveEvent.Direction.Left), Is.EqualTo(-1), "nothing further left");

            // A point at a steep angle is outside the cone: the farther point on the axis wins over the near one off it.
            var steep = new List<Vector2> { new Vector2(0f, 0f), new Vector2(20f, 200f), new Vector2(180f, 0f) };
            Assert.That(ResearchPanel.NearestInDirection(steep, 0, NavigationMoveEvent.Direction.Right), Is.EqualTo(2));
            Assert.That(ResearchPanel.NearestInDirection(steep, 0, NavigationMoveEvent.Direction.Down), Is.EqualTo(1));

            // A tie keeps the lower index (the keyboard order).
            var tie = new List<Vector2> { new Vector2(0f, 0f), new Vector2(50f, -20f), new Vector2(50f, 20f) };
            Assert.That(ResearchPanel.NearestInDirection(tie, 0, NavigationMoveEvent.Direction.Right), Is.EqualTo(1));
            Assert.That(ResearchPanel.NearestInDirection(points, -1, NavigationMoveEvent.Direction.Right), Is.EqualTo(-1));
            Assert.That(ResearchPanel.NearestInDirection(null, 0, NavigationMoveEvent.Direction.Right), Is.EqualTo(-1));
        }

        private IEnumerator Step(Button from, NavigationMoveEvent.Direction direction, bool viaKey)
        {
            from.Focus();
            yield return null;
            if (viaKey)
            {
                Arrow(from, direction == NavigationMoveEvent.Direction.Left ? KeyCode.LeftArrow
                    : direction == NavigationMoveEvent.Direction.Right ? KeyCode.RightArrow
                    : direction == NavigationMoveEvent.Direction.Up ? KeyCode.UpArrow : KeyCode.DownArrow);
            }
            else
            {
                Move(from, direction);
            }

            yield return ShellRig.Settle();
        }

        [UnityTest]
        public IEnumerator ArrowKeysMoveFocusToTheNearestNodeInThatDirection([Values(false, true)] bool viaKey, [ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            var all = panel.NodeButtons.ToList();
            string Name(VisualElement element) => element == null ? "null" : element.name;

            // Down and up stay in the column, in content order.
            yield return Step(panel.NodeButton(Bargaining), NavigationMoveEvent.Direction.Down, viaKey);
            Assert.That(Focused(rig.Root), Is.SameAs(panel.NodeButton(MinerTuning)), "down from bargaining, got " + Name(Focused(rig.Root)));
            yield return Step(panel.NodeButton(MinerTuning), NavigationMoveEvent.Direction.Up, viaKey);
            Assert.That(Focused(rig.Root), Is.SameAs(panel.NodeButton(Bargaining)), "up from miner, got " + Name(Focused(rig.Root)));

            // The first node has nothing above it and nothing to the left: focus stays.
            yield return Step(panel.NodeButton(Bargaining), NavigationMoveEvent.Direction.Up, viaKey);
            Assert.That(Focused(rig.Root), Is.SameAs(panel.NodeButton(Bargaining)), "up from bargaining, got " + Name(Focused(rig.Root)));
            yield return Step(panel.NodeButton(Bargaining), NavigationMoveEvent.Direction.Left, viaKey);
            Assert.That(Focused(rig.Root), Is.SameAs(panel.NodeButton(Bargaining)), "left from bargaining, got " + Name(Focused(rig.Root)));

            // Right goes to the nearest node of a deeper column; left comes back to a shallower one.
            foreach (var fromId in new[] { Bargaining, MinerTuning, SmelterTuning })
            {
                var from = panel.NodeButton(fromId);
                from.Focus();
                yield return ShellRig.Settle();
                var expected = all.Where(node => Center(node).x > Center(from).x + 0.5f)
                    .OrderBy(node => (Center(node) - Center(from)).sqrMagnitude + (Mathf.Abs(Center(node).y - Center(from).y) > Center(node).x - Center(from).x ? 1e12f : 0f))
                    .First();
                yield return Step(from, NavigationMoveEvent.Direction.Right, viaKey);
                Assert.That(Focused(rig.Root), Is.SameAs(expected), "right of " + fromId + ", got " + Name(Focused(rig.Root)));
                var back = Focused(rig.Root) as Button;
                Assert.That(Center(back).x, Is.GreaterThan(Center(from).x));
                yield return Step(back, NavigationMoveEvent.Direction.Left, viaKey);
                Assert.That(Center((Button)Focused(rig.Root)).x, Is.LessThan(Center(back).x), "left goes back to a shallower column");
            }

            // The last depth has nothing to its right.
            yield return Step(panel.NodeButton(AssemblerTuning), NavigationMoveEvent.Direction.Right, viaKey);
            Assert.That(Focused(rig.Root), Is.SameAs(panel.NodeButton(AssemblerTuning)), "right of assembler, got " + Name(Focused(rig.Root)));
        }

        [UnityTest]
        public IEnumerator AKeyEventAndANavigationEventOfOnePressMoveFocusOnlyOnce()
        {
            yield return Open(Phone360());
            var first = panel.NodeButton(Bargaining);
            first.Focus();
            yield return null;
            Arrow(first, KeyCode.DownArrow);
            Move(first, NavigationMoveEvent.Direction.Down);
            yield return ShellRig.Settle();
            Assert.That(Focused(rig.Root), Is.SameAs(panel.NodeButton(MinerTuning)), "the input system raises both events for one press");
        }

        // ---------------------------------------------------------------------------------------------------
        // Scrolling: the focused node stays in view at phone width
        // ---------------------------------------------------------------------------------------------------

        private void AssertNodeInView(Button node, string context)
        {
            var graphViewport = panel.GraphScroll.contentViewport.worldBound;
            var sheetViewport = rig.Shell.PanelScroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport").worldBound;
            var bound = node.worldBound;
            AssertInView(new Rect(graphViewport.xMin, 0f, graphViewport.width, 1f), new Rect(bound.xMin, 0f, bound.width, 1f), $"{context}: {node.name} horizontally in the graph viewport {graphViewport}, node {bound}");
            AssertInView(new Rect(0f, sheetViewport.yMin, 1f, sheetViewport.height), new Rect(0f, bound.yMin, 1f, bound.height), $"{context}: {node.name} vertically in the sheet {sheetViewport}, node {bound}");
            Assert.That(node.ClassListContains("run-control--focused"), Is.True, $"{context}: {node.name} shows its focus ring");
        }

        [UnityTest]
        public IEnumerator TheGraphScrollsSidewaysAtPhoneWidthAndTheFocusedNodeIsKeptInView([Values(100, 200)] int percent)
        {
            yield return Open(Phone360(), percent);
            var viewport = panel.GraphScroll.contentViewport;
            Assert.That(panel.Canvas.layout.width, Is.GreaterThan(viewport.layout.width + 1f), "four columns do not fit 360 pt: the graph scrolls");
            Assert.That(Shown(panel.PanHint), Is.True, "the player is told the graph scrolls");
            Assert.That(panel.PanHint.text, Is.EqualTo("Scroll sideways to see more Research."));

            foreach (var id in ExpectedOrder.Concat(ExpectedOrder.Reverse()))
            {
                var node = panel.NodeButton(id);
                node.Focus();
                yield return ShellRig.Settle();
                Assert.That(Focused(rig.Root), Is.SameAs(node));
                AssertNodeInView(node, $"360x640 @{percent}%");
            }
        }

        [UnityTest]
        public IEnumerator ArrowingAcrossTheGraphKeepsTheFocusedNodeInView()
        {
            yield return Open(Phone360());
            var current = panel.NodeButton(MinerTuning);
            current.Focus();
            yield return ShellRig.Settle();
            foreach (var direction in new[] { NavigationMoveEvent.Direction.Right, NavigationMoveEvent.Direction.Right, NavigationMoveEvent.Direction.Right,
                         NavigationMoveEvent.Direction.Down, NavigationMoveEvent.Direction.Left, NavigationMoveEvent.Direction.Left, NavigationMoveEvent.Direction.Left })
            {
                yield return null;
                Move(current, direction);
                yield return ShellRig.Settle();
                current = (Button)Focused(rig.Root);
                Assert.That(current.name, Does.StartWith("research-node-"));
                AssertNodeInView(current, "arrowing " + direction);
            }
        }

        [UnityTest]
        public IEnumerator EveryNodeScrollsIntoViewAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            foreach (var id in new[] { ExpectedOrder.First(), ExpectedOrder[ExpectedOrder.Length / 2], ExpectedOrder.Last() })
            {
                rig.Shell.PanelScroll.scrollOffset = Vector2.zero;
                panel.GraphScroll.scrollOffset = Vector2.zero;
                yield return ShellRig.Settle();
                var node = panel.NodeButton(id);
                node.Focus();
                yield return ShellRig.Settle();
                AssertNodeInView(node, $"{screen} @{percent}%");
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout (UX-3, F-4): worst-case content at every screen class, rotations included, at 100 and 200 percent
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// The worst case of the panel: a huge RP balance, nodes in every state (maxed, owned, locked, short of RP), the longest node text,
        /// a rejection shown, the Industry section hosted under the graph, and the last node selected.
        /// </summary>
        private IEnumerator BuildWorstCase()
        {
            GrantRp(session, Huge / 1000L);
            GrantIp(session, Huge / 1000L);
            SetResearchLevel(session, MinerTuning, 5);
            SetResearchLevel(session, DeepVeins, 1);
            SetResearchLevel(session, SmelterTuning, 3);
            SetResearchLevel(session, Bargaining, 1);
            SetIndustryLevel(session, SeedCapital, 4);
            yield return Sync();
            var locked = panel.NodeButton(AssemblerTuning);
            locked.Focus();
            Submit(locked);
            yield return ShellRig.Settle();
            Assert.That(Shown(panel.Feedback), Is.True);
        }

        [UnityTest]
        public IEnumerator TheWorstCasePanelStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, withIndustry: true);
            yield return BuildWorstCase();
            yield return AssertPanelLayout(rig, panel.Root, "worst-case research", percent);
        }

        [UnityTest]
        public IEnumerator TheEmptyStateStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            yield return ShellRig.Settle();
            AssertShellLayout(rig, "empty research");
        }

        [UnityTest]
        public IEnumerator PanelTextFollowsTheScaleVariablesAndNeverFallsBelowTheMinimum([ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(Phone360(), percent, withIndustry: true);
            yield return BuildWorstCase();
            var expected = percent / 100f;
            foreach (var label in panel.Root.Query<TextElement>().ToList().Where(item => IsDisplayed(item) && !string.IsNullOrEmpty(item.text)))
            {
                Assert.That(label.resolvedStyle.fontSize, Is.GreaterThanOrEqualTo(12f * expected - 0.51f), label.name);
                Assert.That(label.resolvedStyle.whiteSpace, Is.Not.EqualTo(WhiteSpace.NoWrap), label.name + " wraps");
            }
        }

        [UnityTest]
        public IEnumerator TheIndustrySectionIsHostedUnderTheGraphAndFollowsTheShowHideCalls()
        {
            yield return Open(Phone360(), withIndustry: true);
            Assert.That(panel.Industry, Is.SameAs(industry));
            Assert.That(industry.Root.parent, Is.SameAs(panel.Root));
            Assert.That(panel.Root.IndexOf(industry.Root), Is.GreaterThan(panel.Root.IndexOf(panel.Detail)));
            Assert.That(industry.Snapshot, Is.Not.Null, "the host shows the section with the panel");
            GrantIp(session, 7);
            panel.Invalidate();
            panel.Refresh();
            Assert.That(industry.IpLabel.text, Is.EqualTo("Industry points: 7 IP"));
        }

        [UnityTest]
        public IEnumerator ARotationHasNothingToCancel()
        {
            yield return Open(Phone360());
            Assert.That(panel.CancelForRotation(), Is.False);
            yield return rig.Layout(ShellScreens.Rotated(rig.Screen));
            Assert.That(panel.NodeOrder, Is.EqualTo(ExpectedOrder));
        }
    }
}
#endif
