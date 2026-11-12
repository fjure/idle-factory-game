using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T429: the state-cue set (data-model "State-cue set"). Every machine state that <see cref="SlotGridModel.BadgeFor(bool, MachineBlocker, MachinePhase, out string, out string)"/>
    /// resolves maps to exactly one distinct cue row (variant b: pixel badges, running intentionally empty), the slot conditions live in
    /// separate tables, the geometry is re-derived for D = 24 from the Art Bible, and the set holds no precedence rule of its own.
    /// </summary>
    public sealed class StateCueSetTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void DestroyCreatedAssets()
        {
            foreach (var asset in created)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            created.Clear();
        }

        private Sprite Make(string name, int width, int height, Vector2 pivotPixels, float pixelsPerUnit)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(pivotPixels.x / width, pivotPixels.y / height), pixelsPerUnit);
            sprite.name = name;
            created.Add(texture);
            created.Add(sprite);
            return sprite;
        }

        private Sprite CanvasLayer(string name) => Make(name, 80, 64, new Vector2(40, 15), 24f);

        // ---------------------------------------------------------------------------------------------------
        // Machine states: one row per BadgeFor result, no second precedence rule
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void EveryBadgeForResultMapsToExactlyOneRowOfTheDefaultSet()
        {
            var set = StateCueSet.Default;
            var seen = new HashSet<MachineVisualState>();
            foreach (var active in new[] { true, false })
            {
                foreach (var blocker in Enum.GetValues(typeof(MachineBlocker)).Cast<MachineBlocker>())
                {
                    foreach (var phase in Enum.GetValues(typeof(MachinePhase)).Cast<MachinePhase>())
                    {
                        var key = SlotGridModel.BadgeFor(active, blocker, phase, out var text, out var word);
                        var state = SlotGridModel.StateFor(active, blocker, phase);
                        var context = $"active {active}, {blocker}, {phase}";
                        Assert.That(set.TryGetRowForBadgeKey(key, out var row), Is.True, "no row for " + key + " at " + context);
                        Assert.That(row.State, Is.EqualTo(state), "the row is the one StateFor resolves at " + context);
                        Assert.That(row.BadgeKey, Is.EqualTo(key), context);
                        Assert.That(SlotGridModel.BadgeKeyOf(state), Is.EqualTo(key), context);
                        Assert.That(text, Is.Not.Empty);
                        Assert.That(word, Is.Not.Empty);
                        seen.Add(state);
                    }
                }
            }

            Assert.That(seen.Count, Is.EqualTo(6), "all six machine states are reachable through BadgeFor");
        }

        [Test]
        public void ThePrecedenceTableIsTheArtBibleOrderAndTheSetCarriesNoSecondRule()
        {
            // Art Bible 6.2: inactive, missing input, storage full, waiting, paused, running.
            Assert.That(SlotGridModel.StateFor(false, MachineBlocker.MissingInput, MachinePhase.Paused), Is.EqualTo(MachineVisualState.Inactive));
            Assert.That(SlotGridModel.StateFor(true, MachineBlocker.Inactive, MachinePhase.Running), Is.EqualTo(MachineVisualState.Inactive));
            Assert.That(SlotGridModel.StateFor(true, MachineBlocker.MissingInput, MachinePhase.Waiting), Is.EqualTo(MachineVisualState.MissingInput));
            Assert.That(SlotGridModel.StateFor(true, MachineBlocker.StorageFull, MachinePhase.Waiting), Is.EqualTo(MachineVisualState.StorageFull));
            Assert.That(SlotGridModel.StateFor(true, MachineBlocker.None, MachinePhase.Waiting), Is.EqualTo(MachineVisualState.Waiting));
            Assert.That(SlotGridModel.StateFor(true, MachineBlocker.None, MachinePhase.Paused), Is.EqualTo(MachineVisualState.Paused));
            Assert.That(SlotGridModel.StateFor(true, MachineBlocker.None, MachinePhase.Running), Is.EqualTo(MachineVisualState.Running));

            // The set only maps; no public member of it reads a blocker or a phase.
            var bindings = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var member in typeof(StateCueSet).GetMethods(bindings))
            {
                foreach (var parameter in member.GetParameters())
                {
                    Assert.That(parameter.ParameterType, Is.Not.EqualTo(typeof(MachineBlocker)), member.Name);
                    Assert.That(parameter.ParameterType, Is.Not.EqualTo(typeof(MachinePhase)), member.Name);
                }
            }
        }

        [Test]
        public void EveryNonRunningStateHasADistinctCueShapeKeyAndRunningIsIntentionallyEmpty()
        {
            var rows = StateCueSet.Default.Rows;
            Assert.That(rows.Count, Is.EqualTo(Enum.GetValues(typeof(MachineVisualState)).Length));
            Assert.That(rows.Select(row => row.State).Distinct().Count(), Is.EqualTo(rows.Count), "one row per state");
            Assert.That(rows.Select(row => row.BadgeKey).Distinct().Count(), Is.EqualTo(rows.Count), "one badge key per state");

            var running = StateCueSet.Default.RowFor(MachineVisualState.Running);
            Assert.That(running.BadgeKey, Is.EqualTo("F003-BADGE-RUNNING"));
            Assert.That(running.IntentionallyEmpty, Is.True, "owner decision R8: the running badge resolves to the intentionallyEmpty entry");
            Assert.That(running.CueIconKey, Is.Null, "no cue sprite for a running machine");
            Assert.That(running.Animated, Is.True);

            var cueKeys = new HashSet<string>();
            foreach (var row in rows.Where(row => row.State != MachineVisualState.Running))
            {
                Assert.That(row.IntentionallyEmpty, Is.False, row.State.ToString());
                Assert.That(row.CueIconKey, Is.Not.Null.And.Not.Empty, row.State + " draws a cue");
                Assert.That(row.Animated, Is.False, row.State + " is a still state (only running animates)");
                Assert.That(cueKeys.Add(row.CueIconKey), Is.True, "the cue shape key of " + row.State + " is shared with another state");
                Assert.That(row.LabelPolicy.HasLabel, Is.False, "no world text on a machine badge; the machine panel names the state");
            }

            Assert.That(rows.Count(row => row.Animated), Is.EqualTo(1), "only running animates");
        }

        [Test]
        public void TheSetRejectsAMissingOrADuplicateStateRow()
        {
            var rows = StateCueSet.Default.Rows.ToList();
            Assert.Throws<ArgumentException>(() => new StateCueSet(rows.Take(5)), "a missing state");
            Assert.Throws<ArgumentException>(() => new StateCueSet(rows.Concat(new[] { rows[2] })), "a duplicate state");
            Assert.Throws<ArgumentNullException>(() => new StateCueSet(null));
            Assert.That(new StateCueSet(rows.AsEnumerable().Reverse()).RowFor(MachineVisualState.Waiting).BadgeKey, Is.EqualTo("F003-BADGE-WAITING"), "row order is irrelevant");
        }

        [Test]
        public void RowsAreReadOnlyDuplicateBadgeKeysAreRejectedAndRowForGuardsTheEnumRange()
        {
            var set = StateCueSet.Default;
            Assert.That(set.Rows is StateCueRow[], Is.False, "the backing array is not exposed");
            Assert.That(((System.Collections.Generic.ICollection<StateCueRow>)set.Rows).IsReadOnly, Is.True);
            Assert.That(ReferenceEquals(set.Rows, set.Rows), Is.True, "created once in the constructor");

            var rows = set.Rows.ToList();
            var clash = new StateCueRow(rows[1].State, rows[2].BadgeKey, rows[1].CueIconKey, false, false, LabelPolicy.Never);
            var duplicate = new[] { rows[0], clash, rows[2], rows[3], rows[4], rows[5] };
            Assert.Throws<ArgumentException>(() => new StateCueSet(duplicate), "two states sharing one badge key");

            Assert.Throws<ArgumentOutOfRangeException>(() => set.RowFor((MachineVisualState)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => set.RowFor((MachineVisualState)(-1)));
            Assert.That(set.RowFor(MachineVisualState.Inactive).State, Is.EqualTo(MachineVisualState.Inactive));
        }

        [Test]
        public void TheBodyIsUnchangedForEveryStateUnderVariantB()
        {
            // Variant b: no body-variant key exists on a row, so the body entry is the same for every state (frame 0 when still).
            Assert.That(typeof(StateCueRow).GetProperties().Select(property => property.Name), Does.Not.Contain("BodyVariantKey"));
            foreach (var row in StateCueSet.Default.Rows)
            {
                Assert.That(row.Animated, Is.EqualTo(row.State == MachineVisualState.Running));
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Slot conditions (separate tables) and the lock label policy
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void SlotConditionsHaveTheirOwnTablesAndMatchTheSlotModelKeys()
        {
            Assert.That(StateCueSet.PlateKeyFor(SlotCondition.Empty, 0), Is.EqualTo(SlotGridModel.SlotEmptyKey));
            Assert.That(StateCueSet.PlateKeyFor(SlotCondition.Occupied, 0), Is.EqualTo(SlotGridModel.SlotOccupiedKey));
            Assert.That(StateCueSet.PlateKeyFor(SlotCondition.Locked, 3), Is.EqualTo("F003-SLOT-LOCKED-L3"));
            Assert.That(StateCueSet.PlateKeyFor(SlotCondition.Locked, 1), Is.EqualTo("F003-SLOT-LOCKED-L2"), "the level clamps like SlotGridModel.Describe");
            Assert.That(StateCueSet.PlateKeyFor(SlotCondition.Locked, 9), Is.EqualTo("F003-SLOT-LOCKED-L5"));
            Assert.That(StateCueSet.SelectionFrameKey, Is.EqualTo("F003-SLOT-SELECTED-FRAME"));
            Assert.That(StateCueSet.MarkerKeys, Is.EqualTo(new[] { "F003-MARKER-BOOSTED", "F003-MARKER-OVERDRIVE" }));

            for (var level = 2; level <= 5; level++)
            {
                Assert.That(StateCueSet.LabelKeyFor(SlotCondition.Locked, level), Is.EqualTo("F004-LABEL-LOCK-L" + level));
            }

            Assert.That(StateCueSet.LabelKeyFor(SlotCondition.Empty, 0), Is.Null);
            Assert.That(StateCueSet.LabelKeyFor(SlotCondition.Occupied, 0), Is.Null);
        }

        [Test]
        public void TheLockDigitLabelShowsFromACapHeightOfTwelvePointsAtEveryScreenClassNMin()
        {
            var policy = StateCueSet.LabelPolicyFor(SlotCondition.Locked);
            Assert.That(policy.CapHeightArtPixels, Is.EqualTo(15), "3 x 5 font at scale 3 (owner decision R4)");
            Assert.That(StateCueSet.LabelPolicyFor(SlotCondition.Empty).HasLabel, Is.False);
            Assert.That(StateCueSet.LabelPolicyFor(SlotCondition.Occupied).HasLabel, Is.False);

            // Art Bible 8.4: cap height at n_min per class (22.46, 22.26, 17.10, 16.07, 15.62, 14.97 pt) is above the 12 pt threshold.
            foreach (var capPoints in new[] { 22.46f, 22.26f, 17.10f, 16.07f, 15.62f, 14.97f })
            {
                var pointsPerCell = capPoints * StateCueGeometry.ArtPixelsPerCell / 15f;
                Assert.That(policy.CapHeightPoints(pointsPerCell, StateCueGeometry.ArtPixelsPerCell), Is.EqualTo(capPoints).Within(0.001f));
                Assert.That(policy.IsShown(pointsPerCell, StateCueGeometry.ArtPixelsPerCell), Is.True, capPoints + " pt");
            }

            // Below 12 pt of cap height (19.2 pt per cell) the label is hidden; the slot panel and notice still name the level.
            Assert.That(policy.IsShown(19.3f, StateCueGeometry.ArtPixelsPerCell), Is.True);
            Assert.That(policy.IsShown(19.1f, StateCueGeometry.ArtPixelsPerCell), Is.False);
            Assert.That(LabelPolicy.Never.IsShown(1000f, StateCueGeometry.ArtPixelsPerCell), Is.False);
        }

        [Test]
        public void TheAccentKeyFollowsTheMachineBodyKey()
        {
            Assert.That(StateCueSet.AccentKeyFor("F003-MACHINE-MINER"), Is.EqualTo("F004-ACCENT-MINER"));
            Assert.That(StateCueSet.AccentKeyFor("F003-MACHINE-ASSEMBLER"), Is.EqualTo("F004-ACCENT-ASSEMBLER"));
            Assert.That(StateCueSet.AccentKeyFor(null), Is.Null);
            Assert.That(StateCueSet.AccentKeyFor("F003-MACHINE-"), Is.Null);
            Assert.That(StateCueSet.AccentKeyFor("F003-SLOT-EMPTY"), Is.Null);
        }

        // ---------------------------------------------------------------------------------------------------
        // Geometry re-derived for D = 24 (Art Bible 3.2)
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheOverlayAnchorsAreTheArtBibleBoxesAtD24()
        {
            const float Px = 1f / 24f;
            Assert.That(StateCueGeometry.ArtPixelsPerCell, Is.EqualTo(new RunZoomSettings().artPixelsPerCell), "D comes from the one zoom setting");
            Assert.That(StateCueGeometry.CanvasPivot.x, Is.EqualTo(0.5f));
            Assert.That(StateCueGeometry.CanvasPivot.y, Is.EqualTo(0.234375f));
            Assert.That(StateCueGeometry.CanvasWorldSize.x, Is.EqualTo(80f * Px).Within(1e-5f));
            Assert.That(StateCueGeometry.CanvasWorldSize.y, Is.EqualTo(64f * Px).Within(1e-5f));

            // Badge: bottom-centre (59, 33) = +19 px right, +16 px up = (0.7917, 0.6667) world units from the footprint pivot.
            Assert.That(StateCueGeometry.BadgeAnchorOffset.x, Is.EqualTo(0.7917f).Within(1e-4f));
            Assert.That(StateCueGeometry.BadgeAnchorOffset.y, Is.EqualTo(0.6667f).Within(1e-4f));
            Assert.That(StateCueGeometry.BadgeSizeWorld, Is.EqualTo(23f * Px).Within(1e-5f));

            // T524 re-baseline (OD-12, K4): the anchor and the 23 px box are unchanged (old 23 x 23 box = new box); the extent is new and
            // adds the 4 px pointer tail below the anchor: old badge extent 23 x 23 px, new 23 x 27 px, bottom edge 16 px -> 12 px above the pivot.
            Assert.That(StateCueGeometry.BadgeTailPixels, Is.EqualTo(4));
            Assert.That(StateCueGeometry.BadgeTailBaseWidthPixels, Is.EqualTo(7));
            var extent = StateCueGeometry.BadgeExtentBox();
            Assert.That(extent.width, Is.EqualTo(23f * Px).Within(1e-5f), "the tail adds no width");
            Assert.That(extent.height, Is.EqualTo(27f * Px).Within(1e-5f), "box 23 px plus tail 4 px");
            Assert.That(extent.yMin, Is.EqualTo(12f * Px).Within(1e-5f), "the tail tip is 12 px above the pivot");
            Assert.That(extent.yMax, Is.EqualTo(StateCueGeometry.BadgeBox().yMax).Within(1e-5f), "the top edge does not move");
            Assert.That(extent.center.x, Is.EqualTo(StateCueGeometry.BadgeAnchorOffset.x).Within(1e-5f), "the tail is centred on the anchor column");

            // Markers: M1 box x 24..34 and M2 box x 36..46, rows 16..26 (11 x 11); centres (29, 21) and (41, 21) from the top-left.
            Assert.That(StateCueGeometry.Marker1Offset.x, Is.EqualTo(-11f * Px).Within(1e-5f));
            Assert.That(StateCueGeometry.Marker2Offset.x, Is.EqualTo(1f * Px).Within(1e-5f));
            Assert.That(StateCueGeometry.Marker1Offset.y, Is.EqualTo(28f * Px).Within(1e-5f));
            Assert.That(StateCueGeometry.Marker2Offset.y, Is.EqualTo(28f * Px).Within(1e-5f));
            Assert.That(StateCueGeometry.MarkerSizeWorld, Is.EqualTo(11f * Px).Within(1e-5f));

            // T456: the footprint pivot is per slot (SlotGridLayout.PivotOf): centred on the 70 px plate, 15 art px above the plate bottom.
            Assert.That(SlotGridLayout.PivotOf(0, 3).x, Is.EqualTo(35f * Px).Within(1e-5f));
            Assert.That(SlotGridLayout.PivotOf(11, 3).y, Is.EqualTo(15f * Px).Within(1e-5f));
        }

        [Test]
        public void TheBadgeAndBothMarkerBoxesNeverOverlapAndStayInTheOverlayBand()
        {
            var badge = StateCueGeometry.BadgeBox();
            var first = StateCueGeometry.MarkerBox(0);
            var second = StateCueGeometry.MarkerBox(1);
            Assert.That(first.Overlaps(second), Is.False, "M1 and M2");
            Assert.That(second.Overlaps(badge), Is.False, "M2 and the badge");
            Assert.That(first.Overlaps(badge), Is.False, "M1 and the badge");

            // K4: the tail extent keeps every guarantee of the bare box (T517: 0 px over markers, plate, hit shapes and the row band).
            var extent = StateCueGeometry.BadgeExtentBox();
            Assert.That(first.Overlaps(extent), Is.False, "M1 and the badge tail extent");
            Assert.That(second.Overlaps(extent), Is.False, "M2 and the badge tail extent");
            Assert.That(extent.yMin, Is.GreaterThan(0f), "the tail ends above the footprint pivot");

            // One art pixel apart at least: the boxes share no pixel and no edge.
            Assert.That(second.xMin - first.xMax, Is.GreaterThanOrEqualTo(1f / 24f - 1e-5f));
            Assert.That(badge.xMin - second.xMax, Is.GreaterThanOrEqualTo(1f / 24f - 1e-5f));

            // The badge reaches 24 art px (1.0 cell) above the footprint top (15 px above the pivot): Art Bible 3.4.
            Assert.That(badge.yMax - 15f / 24f, Is.EqualTo(1f).Within(1e-4f));
            // The marker rows end 2 rows above... the plate apex (row 29 of the canvas): the marker bottom is at least 2 px above it.
            var apexAbovePivot = (49f - 29f) / 24f;
            Assert.That(first.yMin - apexAbovePivot, Is.GreaterThanOrEqualTo(2f / 24f - 1e-4f));
            Assert.That(second.yMin - apexAbovePivot, Is.GreaterThanOrEqualTo(2f / 24f - 1e-4f));
        }

        [Test]
        public void OnlyAFullMachineCanvasCountsAsACanvasLayer()
        {
            Assert.That(StateCueGeometry.IsCanvasLayer(CanvasLayer("canvas")), Is.True);
            Assert.That(StateCueGeometry.IsCanvasLayer(Make("tight-badge", 72, 32, new Vector2(36, 16), 64f)), Is.False, "the F003 placeholder badge");
            Assert.That(StateCueGeometry.IsCanvasLayer(Make("plate", 128, 128, Vector2.zero, 64f)), Is.False, "the F003 placeholder plate");
            Assert.That(StateCueGeometry.IsCanvasLayer(null), Is.False);
        }

        // ---------------------------------------------------------------------------------------------------
        // Carry-forward from the #541 Architect review (catalog)
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheStaticF004RowsReachACatalogThatIsConfiguredWithoutRequirements()
        {
            // F4: the scene path calls Validate() on a serialized catalog and never passes requirement rows, so the F004 rows are static.
            var accent = Enumerable.Range(0, 6).Select(i => CanvasLayer("accent-" + i)).ToList();
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(new[]
            {
                new PresentationSpriteEntry("F004-ACCENT-MINER", accent, 8f),
                new PresentationSpriteEntry("F004-LABEL-LOCK-L3", new[] { CanvasLayer("lock-l3") }, null),
            });
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.TryGetEntry("F004-ACCENT-MINER", out var entry), Is.True);
            Assert.That(entry.FrameCount, Is.EqualTo(6));

            var wrongSize = new PresentationSpriteCatalog();
            wrongSize.Configure(new[] { new PresentationSpriteEntry("F004-ACCENT-PRESS", Make("tight", 72, 32, new Vector2(36, 16), 64f)) });
            Assert.That(wrongSize.IsValid, Is.False, "a registered F004 key validates the 80 x 64 canvas and the (40, 15) pivot");

            var wrongPivot = new PresentationSpriteCatalog();
            wrongPivot.Configure(new[] { new PresentationSpriteEntry("F004-LABEL-LOCK-L2", Make("pivot", 80, 64, new Vector2(0, 0), 24f)) });
            Assert.That(wrongPivot.IsValid, Is.False);

            var absent = new PresentationSpriteCatalog();
            absent.Configure(new PresentationSpriteEntry[0]);
            Assert.That(absent.IsValid, Is.True, "the rows are optional until the F004 art is integrated");
        }

        [Test]
        public void TheFrameRangeMessageNamesTheAllowedMinimumAndMaximum()
        {
            var catalog = new PresentationSpriteCatalog();
            var two = new[] { CanvasLayer("l-a"), CanvasLayer("l-b") };
            catalog.Configure(new[] { new PresentationSpriteEntry("F004-LABEL-LOCK-L4", two, null) });
            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.Contain("2 frame(s)").And.Contain("allowed 1 to 1"));

            var seventeen = Enumerable.Range(0, 17).Select(i => CanvasLayer("a-" + i)).ToArray();
            var animated = new PresentationSpriteCatalog();
            animated.Configure(new[] { new PresentationSpriteEntry("F004-ACCENT-SMELTER", seventeen, 8f) });
            Assert.That(animated.Error, Does.Contain("allowed 1 to 16"));
        }

        // ---------------------------------------------------------------------------------------------------
        // The shared-clock formula used by the view seam
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheFrameFormulaIsFloorOfTimeTimesRateModuloTheFrameCount()
        {
            Assert.That(SlotGridView.FrameAt(0, 8f, 6), Is.EqualTo(0));
            Assert.That(SlotGridView.FrameAt(124, 8f, 6), Is.EqualTo(0));
            Assert.That(SlotGridView.FrameAt(125, 8f, 6), Is.EqualTo(1));
            Assert.That(SlotGridView.FrameAt(749, 8f, 6), Is.EqualTo(5));
            Assert.That(SlotGridView.FrameAt(750, 8f, 6), Is.EqualTo(0), "wraps after six frames");
            Assert.That(SlotGridView.FrameAt(1_000_000_000_000L, 8f, 6), Is.InRange(0, 5), "no precision loss over a long session");
            Assert.That(SlotGridView.FrameAt(500, 8f, 1), Is.EqualTo(0), "a still entry");
            Assert.That(SlotGridView.FrameAt(500, 0f, 6), Is.EqualTo(0), "no rate");
            Assert.That(SlotGridView.FrameAt(-5, 8f, 6), Is.EqualTo(0));
        }
    }
}
