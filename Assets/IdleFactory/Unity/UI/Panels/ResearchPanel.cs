using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The Research tab (T116, FR-016, FR-032, US3 scenario 3, OD-8; contracts/ui-surfaces.md, Research). It draws the Research
    /// <b>graph</b>: one column per prerequisite depth from left to right, the nodes of a depth in content order, a node of at least 44 pt
    /// with its level, next cost and effect, and a line from each prerequisite to the node that needs it (a solid line when the
    /// prerequisite is met, a dashed one when it is not, so the state never rides on color alone). Tab visits the nodes in that order
    /// (depth, then content order); an arrow key moves to the nearest node in its direction. At phone width the graph scrolls sideways
    /// inside the sheet and the focused node is kept in view horizontally and vertically.
    /// Pressing a node buys its next level through <see cref="RunCommands.PurchaseResearch"/>; a node that cannot be bought shows its
    /// reason before the tap (a missing prerequisite, or the RP still missing), and the command is still re-validated on the tap. The
    /// panel reads <see cref="RunSnapshots.Research"/> and holds no economy rule.
    /// An optional <see cref="IndustryPanel"/> is hosted as a section under the graph (the Industry surface has no tab of its own).
    /// </summary>
    public sealed class ResearchPanel : IShellPanel
    {
        public const string PanelTitle = "Research";
        public const string EmptyText = "Earn RP from contracts.";
        public const string PanHintText = "Scroll sideways to see more Research.";

        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly Func<string, Sprite> icons;
        private readonly IndustryPanel industry;
        private readonly Label rpLabel;
        private readonly Label emptyLabel;
        private readonly Label panHint;
        private readonly ScrollView graphScroll;
        private readonly VisualElement canvas;
        private readonly VisualElement edgeLayer;
        private readonly VisualElement detail;
        private readonly Label detailTitle;
        private readonly Label detailLevel;
        private readonly Label detailEffect;
        private readonly Label detailCost;
        private readonly Label detailNeeds;
        private readonly Label detailReason;
        private readonly Label feedback;
        private readonly List<NodeView> nodes = new List<NodeView>();
        private readonly Dictionary<string, NodeView> byId = new Dictionary<string, NodeView>(StringComparer.Ordinal);
        private readonly List<Edge> edges = new List<Edge>();
        private RunShellDocument shell;
        private ResearchSnapshot snapshot;
        private long keyStructure = -1;
        private long keyQuantity = -1;
        private bool forceRefresh = true;
        private string selectedId;
        private int lastMoveFrame = -1;
        private NavigationMoveEvent.Direction lastMoveDirection;

        /// <summary>Builds the panel. <paramref name="styles"/> is <c>ResearchPanel.uss</c>; <paramref name="icons"/> resolves a presentation key to a sprite (optional).</summary>
        public ResearchPanel(RunSession session, RunCommands commands, StyleSheet styles = null, Func<string, Sprite> icons = null,
            IndustryPanel industry = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.icons = icons;
            this.industry = industry;
            var root = new VisualElement { name = "research-panel" };
            root.AddToClassList("research-panel");
            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            Root = root;

            var header = new VisualElement { name = "research-header" };
            header.AddToClassList("research-card");
            rpLabel = MakeText("research-rp", "research-value");
            emptyLabel = MakeText("research-empty", "research-line");
            emptyLabel.text = EmptyText;
            header.Add(rpLabel);
            header.Add(emptyLabel);
            root.Add(header);

            var initial = RunSnapshots.Research(session);

            graphScroll = new ScrollView(ScrollViewMode.Horizontal) { name = "research-graph" };
            graphScroll.AddToClassList("research-graph");
            graphScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            graphScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            canvas = new VisualElement { name = "research-canvas" };
            canvas.AddToClassList("research-canvas");
            edgeLayer = new VisualElement { name = "research-edges", pickingMode = PickingMode.Ignore };
            edgeLayer.AddToClassList("research-edges");
            edgeLayer.generateVisualContent += DrawEdges;
            canvas.Add(edgeLayer);

            VisualElement column = null;
            var columnDepth = -1;
            foreach (var node in initial.Nodes)
            {
                if (node.Depth != columnDepth)
                {
                    columnDepth = node.Depth;
                    column = new VisualElement { name = "research-column-" + columnDepth };
                    column.AddToClassList("research-column");
                    canvas.Add(column);
                }

                var view = new NodeView(node, this);
                nodes.Add(view);
                byId.Add(node.ResearchId, view);
                column.Add(view.Button);
            }

            foreach (var node in initial.Nodes)
            {
                foreach (var prerequisite in node.Prerequisites)
                {
                    edges.Add(new Edge(byId[prerequisite.ResearchId], byId[node.ResearchId], prerequisite.Level));
                }
            }

            graphScroll.Add(canvas);
            root.Add(graphScroll);
            panHint = MakeText("research-pan-hint", "research-hint");
            panHint.text = PanHintText;
            root.Add(panHint);

            canvas.RegisterCallback<FocusInEvent>(OnNodeFocusIn);
            // Trickle down: the default focus switch of a navigation event runs at the target, before a bubbling handler could stop it.
            canvas.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            canvas.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            canvas.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            graphScroll.RegisterCallback<GeometryChangedEvent>(OnGeometry);

            detail = new VisualElement { name = "research-detail" };
            detail.AddToClassList("research-card");
            detailTitle = MakeText("research-detail-title", "research-title");
            detailLevel = MakeText("research-detail-level", "research-line");
            detailEffect = MakeText("research-detail-effect", "research-line");
            detailCost = MakeText("research-detail-cost", "research-line");
            detailNeeds = MakeText("research-detail-needs", "research-hint");
            detailReason = MakeText("research-detail-reason", "research-hint");
            feedback = MakeText("research-feedback", "research-feedback");
            foreach (var label in new[] { detailTitle, detailLevel, detailEffect, detailCost, detailNeeds, detailReason, feedback })
            {
                detail.Add(label);
            }

            root.Add(detail);
            if (industry != null)
            {
                root.Add(industry.Root);
            }

            selectedId = nodes.Count > 0 ? nodes[0].Id : null;
        }

        public string Id => ShellPanelIds.Research;
        public string Title => PanelTitle;
        public VisualElement Root { get; }

        /// <summary>The latest snapshot the panel drew from, or null before the first refresh.</summary>
        public ResearchSnapshot Snapshot => snapshot;

        public Label RpLabel => rpLabel;
        public Label EmptyLabel => emptyLabel;
        public Label PanHint => panHint;
        public ScrollView GraphScroll => graphScroll;
        public VisualElement Canvas => canvas;
        public VisualElement Detail => detail;
        public Label DetailTitle => detailTitle;
        public Label DetailLevel => detailLevel;
        public Label DetailEffect => detailEffect;
        public Label DetailCost => detailCost;
        public Label DetailNeeds => detailNeeds;
        public Label DetailReason => detailReason;
        public Label Feedback => feedback;

        /// <summary>The hosted Industry section, or null.</summary>
        public IndustryPanel Industry => industry;

        /// <summary>The node buttons in keyboard order: prerequisite depth from left to right, then content order (OD-8).</summary>
        public IReadOnlyList<Button> NodeButtons
        {
            get
            {
                var list = new List<Button>(nodes.Count);
                foreach (var node in nodes)
                {
                    list.Add(node.Button);
                }

                return list;
            }
        }

        /// <summary>The Research IDs in keyboard order.</summary>
        public IReadOnlyList<string> NodeOrder
        {
            get
            {
                var list = new List<string>(nodes.Count);
                foreach (var node in nodes)
                {
                    list.Add(node.Id);
                }

                return list;
            }
        }

        /// <summary>The prerequisite lines the graph draws, as (prerequisite ID, dependent ID, met).</summary>
        public IReadOnlyList<(string From, string To, bool Met)> EdgeList
        {
            get
            {
                var list = new List<(string, string, bool)>(edges.Count);
                foreach (var edge in edges)
                {
                    list.Add((edge.From.Id, edge.To.Id, edge.Met));
                }

                return list;
            }
        }

        public Button NodeButton(string researchId) => byId[researchId].Button;

        /// <summary>The ID of the node the detail card describes: the focused or last pressed node.</summary>
        public string SelectedId => selectedId;

        public void OnShown(ShellPanelContext context)
        {
            shell = context?.Shell;
            industry?.OnShown(context);
            Invalidate();
            Refresh();
        }

        public void OnHidden() => industry?.OnHidden();

        /// <summary>Forces the next <see cref="Refresh"/> to redraw.</summary>
        public void Invalidate()
        {
            forceRefresh = true;
            industry?.Invalidate();
        }

        /// <summary>Redraws when the structure or the quantities changed; the shell calls it every frame while visible.</summary>
        public void Refresh()
        {
            industry?.Refresh();
            if (!forceRefresh && session.StructureRevision == keyStructure && session.QuantityRevision == keyQuantity)
            {
                return;
            }

            forceRefresh = false;
            keyStructure = session.StructureRevision;
            keyQuantity = session.QuantityRevision;
            Render(RunSnapshots.Research(session));
        }

        /// <summary>
        /// The panel has no pending selection and no confirmation (a Research purchase needs none), so a rotation has nothing to cancel;
        /// the method exists so the bootstrap can register every panel's rotation hook the same way. Returns false.
        /// </summary>
        public bool CancelForRotation() => industry != null && industry.CancelForRotation();

        // ---------------------------------------------------------------------------------------------------
        // Rendering
        // ---------------------------------------------------------------------------------------------------

        private void Render(ResearchSnapshot data)
        {
            snapshot = data;
            rpLabel.text = "Research points: " + QuantityFormatter.Format(data.Rp) + " RP";
            var anyLevel = false;
            foreach (var node in data.Nodes)
            {
                anyLevel |= node.Level > 0;
            }

            SetDisplayed(emptyLabel, data.Rp == 0 && !anyLevel);

            var lookup = new Dictionary<string, ResearchNodeSnapshot>(StringComparer.Ordinal);
            foreach (var node in data.Nodes)
            {
                lookup[node.ResearchId] = node;
            }

            foreach (var node in data.Nodes)
            {
                byId[node.ResearchId].Render(node, data, lookup, this);
            }

            foreach (var edge in edges)
            {
                edge.Met = lookup[edge.To.Id].Prerequisites.Count > 0 && IsMet(lookup[edge.To.Id], edge.From.Id);
            }

            edgeLayer.MarkDirtyRepaint();
            RenderDetail(lookup);
        }

        private static bool IsMet(ResearchNodeSnapshot node, string prerequisiteId)
        {
            foreach (var prerequisite in node.Prerequisites)
            {
                if (prerequisite.ResearchId == prerequisiteId)
                {
                    return prerequisite.Met;
                }
            }

            return false;
        }

        private void RenderDetail(Dictionary<string, ResearchNodeSnapshot> lookup)
        {
            if (selectedId == null || !lookup.TryGetValue(selectedId, out var node))
            {
                SetDisplayed(detail, false);
                return;
            }

            SetDisplayed(detail, true);
            var catalog = session.Catalog;
            detailTitle.text = node.Label;
            detailLevel.text = "Level " + node.Level.ToString(CultureInfo.InvariantCulture) + " of " + node.MaxLevel.ToString(CultureInfo.InvariantCulture);
            detailEffect.text = ResearchEffectCopy.DescribeExact(catalog, node.EffectKind, node.TargetKind, node.TargetId, node.EffectValue);
            detailCost.text = node.Level >= node.MaxLevel
                ? "Fully researched."
                : "Next level costs " + node.NextCost.ToString("N0", CultureInfo.InvariantCulture) + " RP. You have " +
                    snapshot.Rp.ToString("N0", CultureInfo.InvariantCulture) + " RP.";

            var needs = new StringBuilder();
            foreach (var prerequisite in node.Prerequisites)
            {
                if (needs.Length > 0)
                {
                    needs.Append('\n');
                }

                var current = lookup.TryGetValue(prerequisite.ResearchId, out var source) ? source : null;
                needs.Append("Needs ").Append(current?.Label ?? prerequisite.ResearchId).Append(" level ")
                    .Append(prerequisite.Level.ToString(CultureInfo.InvariantCulture))
                    .Append(prerequisite.Met ? ": done" : ": not yet (now level " + (current?.Level ?? 0).ToString(CultureInfo.InvariantCulture) + ")");
            }

            detailNeeds.text = needs.ToString();
            SetDisplayed(detailNeeds, needs.Length > 0);
            var reason = node.CanPurchase ? string.Empty : RunRejectionCopy.For(node.PurchaseReason);
            detailReason.text = reason;
            SetDisplayed(detailReason, reason.Length > 0);
        }

        private void Select(string researchId)
        {
            if (selectedId == researchId)
            {
                return;
            }

            selectedId = researchId;
            ClearFeedback();
            if (snapshot != null)
            {
                var lookup = new Dictionary<string, ResearchNodeSnapshot>(StringComparer.Ordinal);
                foreach (var node in snapshot.Nodes)
                {
                    lookup[node.ResearchId] = node;
                }

                RenderDetail(lookup);
            }

            foreach (var node in nodes)
            {
                node.Button.EnableInClassList("research-node--selected", node.Id == researchId);
            }
        }

        private void ClearFeedback()
        {
            feedback.text = string.Empty;
            SetDisplayed(feedback, false);
        }

        private void ShowFeedback(string text)
        {
            feedback.text = text;
            SetDisplayed(feedback, text.Length > 0);
        }

        // ---------------------------------------------------------------------------------------------------
        // Purchase
        // ---------------------------------------------------------------------------------------------------

        private void OnNodePressed(NodeView view)
        {
            Select(view.Id);
            ClearFeedback();
            var result = commands.PurchaseResearch(view.Id);
            if (!result.IsSuccess)
            {
                // The panel showed the reason before the tap; the command re-validated it and changed nothing.
                ShowFeedback(RunRejectionCopy.For(result.ReasonCode));
                Invalidate();
                Refresh();
                return;
            }

            var bought = result.Value;
            Invalidate();
            Refresh();
            shell?.ShowNotice(view.Label + " is now level " + bought.NewLevel.ToString(CultureInfo.InvariantCulture) + ".", ShellNoticeKind.Info);
        }

        // ---------------------------------------------------------------------------------------------------
        // Focus, keyboard and scrolling
        // ---------------------------------------------------------------------------------------------------

        private void OnNodeFocusIn(FocusInEvent evt)
        {
            if (!(evt.target is Button button) || !byId.TryGetValue(NodeIdOf(button), out var view))
            {
                return;
            }

            Select(view.Id);
            KeepInView(view.Button);
        }

        /// <summary>Scrolls the graph sideways and the sheet vertically so the node is in view.</summary>
        private void KeepInView(VisualElement node)
        {
            Reveal(node);

            // The shell's own focus handler runs after this one and scrolls the nearest scroll view (the graph) to the node's far edge, and a
            // world bound read right after a scroll can be stale, so the reveal is repeated once the event is done.
            revealNode = node;
            node.schedule.Execute(() => RevealIfCurrent(node)).ExecuteLater(0);
            node.schedule.Execute(() => RevealIfCurrent(node)).ExecuteLater(40);
        }

        private VisualElement revealNode;

        /// <summary>A repeated reveal only acts for the node that is still the latest focused one, so a late one never scrolls back.</summary>
        private void RevealIfCurrent(VisualElement node)
        {
            if (revealNode == node)
            {
                Reveal(node);
            }
        }

        private void Reveal(VisualElement node)
        {
            graphScroll.ScrollTo(node);
            AlignOversized(graphScroll, node);
            var sheet = shell?.PanelScroll;
            if (sheet != null && sheet.contentContainer.Contains(node))
            {
                sheet.ScrollTo(node);
                AlignOversized(sheet, node);
            }
        }

        /// <summary>
        /// A node larger than its viewport (a short sheet at 200 % text) cannot be shown whole: its top-left corner is aligned with the
        /// viewport so the node starts in view, instead of the end the default scroll shows.
        /// </summary>
        private static void AlignOversized(ScrollView scroll, VisualElement node)
        {
            var viewport = scroll.contentViewport.worldBound;
            var bound = node.worldBound;
            var offset = scroll.scrollOffset;
            if (bound.height > viewport.height + 0.5f)
            {
                offset.y += bound.yMin - viewport.yMin;
            }

            if (bound.width > viewport.width + 0.5f)
            {
                offset.x += bound.xMin - viewport.xMin;
            }

            scroll.scrollOffset = offset;
        }

        private static string NodeIdOf(Button button) =>
            button.name.StartsWith("research-node-", StringComparison.Ordinal) ? button.name.Substring("research-node-".Length) : string.Empty;

        private void OnKeyDown(KeyDownEvent evt)
        {
            NavigationMoveEvent.Direction? direction = null;
            switch (evt.keyCode)
            {
                case KeyCode.LeftArrow: direction = NavigationMoveEvent.Direction.Left; break;
                case KeyCode.RightArrow: direction = NavigationMoveEvent.Direction.Right; break;
                case KeyCode.UpArrow: direction = NavigationMoveEvent.Direction.Up; break;
                case KeyCode.DownArrow: direction = NavigationMoveEvent.Direction.Down; break;
            }

            if (direction.HasValue && MoveFocus(direction.Value, evt.target as Button))
            {
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }

        private void OnNavigationMove(NavigationMoveEvent evt)
        {
            var direction = evt.direction;
            if ((direction == NavigationMoveEvent.Direction.Left || direction == NavigationMoveEvent.Direction.Right ||
                 direction == NavigationMoveEvent.Direction.Up || direction == NavigationMoveEvent.Direction.Down) && MoveFocus(direction, evt.target as Button))
            {
                // Without PreventDefault the panel's own spatial navigation would move the focus a second time.
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }

        /// <summary>Moves focus to the nearest node in a direction; returns false when the focus is not on a node (the event is left alone).</summary>
        private bool MoveFocus(NavigationMoveEvent.Direction direction, Button origin)
        {
            var focused = origin ?? canvas.focusController?.focusedElement as Button;
            if (focused == null || !byId.ContainsKey(NodeIdOf(focused)))
            {
                return false;
            }

            // The input system raises a key event and a navigation event for one arrow press: act on the first only.
            if (lastMoveFrame == Time.frameCount && lastMoveDirection == direction && Application.isPlaying)
            {
                return true;
            }

            lastMoveFrame = Time.frameCount;
            lastMoveDirection = direction;
            var centers = new List<Vector2>(nodes.Count);
            var from = -1;
            for (var i = 0; i < nodes.Count; i++)
            {
                centers.Add(nodes[i].CenterInCanvas(canvas));
                if (nodes[i].Button == focused)
                {
                    from = i;
                }
            }

            var next = NearestInDirection(centers, from, direction);
            if (next >= 0)
            {
                nodes[next].Button.Focus();
            }
            else if (from >= 0)
            {
                // Nothing in that direction: the focus stays, even if a default handler moved it meanwhile.
                var stay = nodes[from].Button;
                stay.Focus();
                stay.schedule.Execute(() =>
                {
                    if (stay.focusController?.focusedElement != stay)
                    {
                        stay.Focus();
                    }
                }).ExecuteLater(0);
            }

            return true;
        }

        /// <summary>
        /// The index of the nearest point from <paramref name="from"/> in a direction (Left, Right, Up or Down; y grows downward), or -1.
        /// A point inside the 45 degree cone of the direction always beats one outside it; within the cone or outside it, the nearest wins and
        /// the lower index breaks a tie (so the result follows the keyboard order).
        /// </summary>
        public static int NearestInDirection(IReadOnlyList<Vector2> centers, int from, NavigationMoveEvent.Direction direction)
        {
            if (centers == null || from < 0 || from >= centers.Count)
            {
                return -1;
            }

            var best = -1;
            var bestScore = float.MaxValue;
            var origin = centers[from];
            for (var i = 0; i < centers.Count; i++)
            {
                if (i == from)
                {
                    continue;
                }

                var delta = centers[i] - origin;
                float primary;
                float secondary;
                switch (direction)
                {
                    case NavigationMoveEvent.Direction.Right: primary = delta.x; secondary = Mathf.Abs(delta.y); break;
                    case NavigationMoveEvent.Direction.Left: primary = -delta.x; secondary = Mathf.Abs(delta.y); break;
                    case NavigationMoveEvent.Direction.Down: primary = delta.y; secondary = Mathf.Abs(delta.x); break;
                    case NavigationMoveEvent.Direction.Up: primary = -delta.y; secondary = Mathf.Abs(delta.x); break;
                    default: return -1;
                }

                if (primary <= 0.5f)
                {
                    continue;
                }

                // A point far off the axis is not "in that direction" at all: it is never a fallback.
                if (secondary > primary * 4f)
                {
                    continue;
                }

                var score = (primary * primary) + (secondary * secondary) + (secondary > primary ? 1e12f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        private void OnGeometry(GeometryChangedEvent evt)
        {
            edgeLayer.MarkDirtyRepaint();
            var viewport = graphScroll.contentViewport;
            var overflows = viewport != null && canvas.layout.width > viewport.layout.width + 1f;
            SetDisplayed(panHint, overflows);
        }

        // ---------------------------------------------------------------------------------------------------
        // Edges
        // ---------------------------------------------------------------------------------------------------

        private static readonly Color MetColor = new Color(0.22f, 0.74f, 0.97f, 1f);
        private static readonly Color OpenColor = new Color(0.58f, 0.64f, 0.72f, 1f);

        private void DrawEdges(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            foreach (var edge in edges)
            {
                var a = edge.From.RightMiddle(canvas);
                var b = edge.To.LeftMiddle(canvas);
                var middle = (a.x + b.x) * 0.5f;
                painter.strokeColor = edge.Met ? MetColor : OpenColor;
                painter.lineWidth = edge.Met ? 3f : 2f;
                var points = new[] { a, new Vector2(middle, a.y), new Vector2(middle, b.y), b };
                for (var i = 0; i + 1 < points.Length; i++)
                {
                    if (edge.Met)
                    {
                        painter.BeginPath();
                        painter.MoveTo(points[i]);
                        painter.LineTo(points[i + 1]);
                        painter.Stroke();
                    }
                    else
                    {
                        Dashed(painter, points[i], points[i + 1]);
                    }
                }
            }
        }

        private static void Dashed(Painter2D painter, Vector2 from, Vector2 to)
        {
            const float dash = 6f;
            const float gap = 5f;
            var length = Vector2.Distance(from, to);
            if (length < 0.01f)
            {
                return;
            }

            var direction = (to - from) / length;
            for (var at = 0f; at < length; at += dash + gap)
            {
                painter.BeginPath();
                painter.MoveTo(from + (direction * at));
                painter.LineTo(from + (direction * Mathf.Min(at + dash, length)));
                painter.Stroke();
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------------

        private static Label MakeText(string name, string className)
        {
            var label = new Label { name = name };
            label.AddToClassList(className);
            return label;
        }

        private static void SetDisplayed(VisualElement element, bool displayed) =>
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;

        private sealed class Edge
        {
            public Edge(NodeView from, NodeView to, int level)
            {
                From = from;
                To = to;
                Level = level;
            }

            public NodeView From { get; }
            public NodeView To { get; }
            public int Level { get; }
            public bool Met { get; set; }
        }

        private sealed class NodeView
        {
            private readonly ResearchPanel owner;

            public NodeView(ResearchNodeSnapshot node, ResearchPanel owner)
            {
                this.owner = owner;
                Id = node.ResearchId;
                Label = node.Label;
                Button = RunShellDocument.CreateButton("research-node-" + node.ResearchId, node.Label);
                Button.AddToClassList("research-node");
                Button.clicked += () => owner.OnNodePressed(this);
            }

            public string Id { get; }
            public string Label { get; }
            public Button Button { get; }

            public Vector2 CenterInCanvas(VisualElement canvas) => OffsetInCanvas(canvas) + (Button.layout.size * 0.5f);

            public Vector2 LeftMiddle(VisualElement canvas) => OffsetInCanvas(canvas) + new Vector2(0f, Button.layout.height * 0.5f);

            public Vector2 RightMiddle(VisualElement canvas) => OffsetInCanvas(canvas) + new Vector2(Button.layout.width, Button.layout.height * 0.5f);

            private Vector2 OffsetInCanvas(VisualElement canvas)
            {
                var offset = Vector2.zero;
                for (VisualElement current = Button; current != null && current != canvas; current = current.parent)
                {
                    offset += current.layout.position;
                }

                return offset;
            }

            public void Render(ResearchNodeSnapshot node, ResearchSnapshot data, Dictionary<string, ResearchNodeSnapshot> lookup, ResearchPanel panel)
            {
                var atMax = node.Level >= node.MaxLevel;
                var cost = atMax
                    ? "Max"
                    : QuantityFormatter.Format(node.NextCost) + " RP";
                var effect = ResearchEffectCopy.Describe(panel.session.Catalog, node.EffectKind, node.TargetKind, node.TargetId, node.EffectValue, false);
                string status;
                string state;
                string iconKey;
                switch (node.PurchaseReason)
                {
                    case null:
                        status = "Tap to research";
                        state = node.Level > 0 ? "owned" : "available";
                        iconKey = node.Level > 0 ? "F003-UI-ICON-RESEARCH-NODE-OWNED" : "F003-UI-ICON-RESEARCH-NODE-AVAILABLE";
                        break;
                    case RunReasons.ResearchPrerequisite:
                        status = "Locked: " + MissingPrerequisites(node, lookup);
                        state = "locked";
                        iconKey = "F003-UI-ICON-RESEARCH-NODE-LOCKED";
                        break;
                    case RunReasons.LevelMax:
                        status = "Fully researched";
                        state = "maxed";
                        iconKey = "F003-UI-ICON-RESEARCH-NODE-MAXED";
                        break;
                    default:
                        status = "Need " + QuantityFormatter.Format(Math.Max(0L, node.NextCost - data.Rp)) + " more RP";
                        state = "short";
                        iconKey = node.Level > 0 ? "F003-UI-ICON-RESEARCH-NODE-OWNED" : "F003-UI-ICON-RESEARCH-NODE-AVAILABLE";
                        break;
                }

                Button.text = node.Label + "\nLv " + node.Level.ToString(CultureInfo.InvariantCulture) + "/" +
                    node.MaxLevel.ToString(CultureInfo.InvariantCulture) + " · " + cost + "\n" + effect + "\n" + status;
                Button.tooltip = Button.text;
                foreach (var name in new[] { "available", "owned", "locked", "maxed", "short" })
                {
                    Button.EnableInClassList("research-node--" + name, name == state);
                }

                Button.EnableInClassList("research-node--selected", Id == panel.selectedId);
                RunShellDocument.SetSoftDisabled(Button, !node.CanPurchase);
                var sprite = panel.icons?.Invoke(iconKey);
                Button.style.backgroundImage = sprite == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(sprite);
            }

            private static string MissingPrerequisites(ResearchNodeSnapshot node, Dictionary<string, ResearchNodeSnapshot> lookup)
            {
                var text = new StringBuilder();
                foreach (var prerequisite in node.Prerequisites)
                {
                    if (prerequisite.Met)
                    {
                        continue;
                    }

                    if (text.Length > 0)
                    {
                        text.Append(", ");
                    }

                    text.Append(lookup.TryGetValue(prerequisite.ResearchId, out var source) ? source.Label : prerequisite.ResearchId)
                        .Append(" Lv ").Append(prerequisite.Level.ToString(CultureInfo.InvariantCulture));
                }

                return text.Length == 0 ? "prerequisite" : "needs " + text;
            }
        }
    }
}
