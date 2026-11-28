using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>The side of the lattice a margin cell sits on.</summary>
    public enum MarginSide
    {
        Right = 0,
        Left = 1,
        Bottom = 2,
        Top = 3,
    }

    /// <summary>
    /// Picks one cell of the margin band: the <see cref="side"/> it lies on and its position <see cref="along"/> that side, counted from the
    /// bottom (front) row or the left column; a negative value counts from the far end (-1 is the last row or column). The value is clamped.
    /// </summary>
    [Serializable]
    public struct MarginCellRule
    {
        public MarginSide side;
        public int along;

        public MarginCellRule(MarginSide side, int along)
        {
            this.side = side;
            this.along = along;
        }
    }

    /// <summary>The ground detail the hatch tile of a locked location uses (R06).</summary>
    public enum HatchRule
    {
        HatchWithPadlock = 0,
        PlainHatch = 1,
    }

    /// <summary>The deck-edge dressing of one development stage: railing, then wall, then gantry (R06, R15).</summary>
    public enum EdgeRule
    {
        Railing = 0,
        Wall = 1,
        Gantry = 2,
    }

    /// <summary>The selection weight of one prop kind (R22).</summary>
    [Serializable]
    public struct PropWeight
    {
        public string kind;
        public float weight;
    }

    /// <summary>
    /// The highest activity-loop rate of one machine family (F006 R12, P2-F14): the measured flash rate of a family decides it, not the pace. The
    /// Assembler already flashes 3 times per second, so it never runs above 8 fps. Authored here, in presentation tuning, never in gameplay content.
    /// </summary>
    [Serializable]
    public struct FamilyFpsCap
    {
        public string machineId;
        public int maxFps;

        public FamilyFpsCap(string machineId, int maxFps)
        {
            this.machineId = machineId;
            this.maxFps = maxFps;
        }
    }

    /// <summary>The prop-kind weights of one development stage.</summary>
    [Serializable]
    public sealed class StagePropWeights
    {
        public PropWeight[] weights = Array.Empty<PropWeight>();
    }

    /// <summary>
    /// The lattice pitch of a layout as immutable instance data (F007 T610, research R09 amendment, ARCH-4): the cell size in <b>world units</b>, an optional
    /// arrangement key and the hit-size floor. It replaces the compile-time constants <c>SlotGridLayout.PitchX/PitchY</c> as the source of every position
    /// a <see cref="FactoryWorldLayout"/> computes; those constants remain as the F006 values (<see cref="F006"/>). Part of the
    /// <see cref="LayoutComposition"/> equality and identity, so a pitch change refits the camera. The location ids, count and unlock order never depend on it.
    /// </summary>
    public readonly struct LayoutPitch : IEquatable<LayoutPitch>
    {
        /// <summary>The one arrangement key this build knows: locations paired side by side share a slab (a "bay"). Grouping is by site zone (the lattice cell), never by production chain (WD-4).</summary>
        public const string BaysRule = "bays";

        /// <summary>The hit floor in points every pitch has to keep at the minimum zoom (F006 FR-005).</summary>
        public const float HitPointsFloor = 44f;

        /// <summary>The F006 lattice: 70 x 54 art px at 24 px per unit (2.9167 x 2.25 world units), no grouping, the 44 pt floor. Reproduces today's geometry bit for bit.</summary>
        public static readonly LayoutPitch F006 = new LayoutPitch(new Vector2(SlotGridLayout.PitchX, SlotGridLayout.PitchY), string.Empty, HitPointsFloor);

        private readonly string groupRule;

        public LayoutPitch(Vector2 pitchWorld, string groupRule, float minHitPoints)
        {
            PitchWorld = pitchWorld;
            this.groupRule = groupRule ?? string.Empty;
            MinHitPoints = minHitPoints;
        }

        /// <summary>The lattice cell size in world units (cells of one unit; 24 F006 art pixels).</summary>
        public Vector2 PitchWorld { get; }

        /// <summary>The arrangement key: empty (none) or <see cref="BaysRule"/>.</summary>
        public string GroupRule => groupRule ?? string.Empty;

        /// <summary>The least height of a hit area in points at the minimum zoom; at least <see cref="HitPointsFloor"/>.</summary>
        public float MinHitPoints { get; }

        /// <summary>False for <c>default</c>: a pitch that was never set. A layout then uses <see cref="F006"/>.</summary>
        public bool IsSet => PitchWorld.x > 0f && PitchWorld.y > 0f && MinHitPoints > 0f;

        /// <summary>This pitch, or <see cref="F006"/> when none was set.</summary>
        public LayoutPitch OrDefault => IsSet ? this : F006;

        /// <summary>True when the cell size is exactly the F006 one (the layout then reproduces the F006 integer art-pixel arithmetic).</summary>
        public bool HasF006Cell => PitchWorld.x == SlotGridLayout.PitchX && PitchWorld.y == SlotGridLayout.PitchY;

        /// <summary>The height of the tap shape in world units: the 45 art px hit diamond, clipped to the cell height.</summary>
        public float HitHeightWorld => Mathf.Min(SlotGridLayout.HitHeightPixels / (float)SlotGridLayout.Density, PitchWorld.y);

        /// <summary>
        /// The violations of the pitch rules; empty when the pitch is valid. The hit area has to reach <see cref="MinHitPoints"/> at the minimum zoom, where
        /// one world unit spans at least <paramref name="readableCellPoints"/> points less the 1 % tolerance of the minimum-scale rule (the readable
        /// floor, 24 points per cell in the shipped zoom settings).
        /// </summary>
        public IReadOnlyList<string> Problems(float readableCellPoints = 24f)
        {
            var problems = new List<string>();
            if (float.IsNaN(PitchWorld.x) || float.IsNaN(PitchWorld.y) || float.IsInfinity(PitchWorld.x) || float.IsInfinity(PitchWorld.y) || PitchWorld.x <= 0f || PitchWorld.y <= 0f)
            {
                problems.Add("pitchWorld must be finite and greater than 0.");
                return problems;
            }

            if (float.IsNaN(MinHitPoints) || MinHitPoints < HitPointsFloor)
            {
                problems.Add("minHitPoints must be at least " + HitPointsFloor + ".");
            }
            else if (HitHeightWorld * readableCellPoints * (1f - CameraScaleModel.MinScaleTolerance) < MinHitPoints)
            {
                problems.Add("the pitch is too tight: its hit area is under " + MinHitPoints + " points at the minimum zoom.");
            }

            if (GroupRule.Length > 0 && GroupRule != BaysRule)
            {
                problems.Add("groupRule must be empty or '" + BaysRule + "'.");
            }

            return problems;
        }

        public bool Equals(LayoutPitch other) =>
            PitchWorld.x.Equals(other.PitchWorld.x) && PitchWorld.y.Equals(other.PitchWorld.y) &&
            string.Equals(GroupRule, other.GroupRule, StringComparison.Ordinal) && MinHitPoints.Equals(other.MinHitPoints);

        public override bool Equals(object obj) => obj is LayoutPitch other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = PitchWorld.x.GetHashCode();
                hash = hash * 31 + PitchWorld.y.GetHashCode();
                hash = hash * 31 + MinHitPoints.GetHashCode();
                for (var i = 0; i < GroupRule.Length; i++)
                {
                    hash = hash * 31 + GroupRule[i];
                }

                return hash;
            }
        }
    }

    /// <summary>
    /// The authored lattice pitch (data-model LayoutPitchSettings): a <see cref="WorldCompositionSettings"/> field and a field of every look profile. The
    /// defaults are the F006 lattice, so an asset serialized before this field existed reads as F006. Converted to the immutable <see cref="LayoutPitch"/>
    /// before a layout reads it.
    /// </summary>
    [Serializable]
    public sealed class LayoutPitchSettings
    {
        [Tooltip("Lattice pitch in world units (one unit is 24 F006 art pixels). F006: 70 x 54 art px = 2.9167 x 2.25.")]
        public Vector2 pitchWorld = new Vector2(SlotGridLayout.PitchX, SlotGridLayout.PitchY);

        [Tooltip("Optional arrangement key: empty, or 'bays' (pairs of side-by-side locations share a slab). By site zone, never by production chain.")]
        public string groupRule = string.Empty;

        [Tooltip("The least height of a hit area in points at the minimum zoom. At least 44.")]
        public float minHitPoints = LayoutPitch.HitPointsFloor;

        /// <summary>The F006 values.</summary>
        public static LayoutPitchSettings F006() => new LayoutPitchSettings();

        /// <summary>The immutable value: a pitch that was never set (zero values) reads as F006.</summary>
        public LayoutPitch ToPitch() => new LayoutPitch(pitchWorld, groupRule, minHitPoints).OrDefault;
    }

    /// <summary>
    /// The layout values a <see cref="FactoryWorldLayout"/> reads, copied out of <see cref="WorldCompositionSettings"/> so that the layout
    /// is immutable and never holds the asset.
    /// </summary>
    public readonly struct LayoutComposition
    {
        public LayoutComposition(float marginWorldUnits, int deckBandCells, MarginCellRule depotRule, MarginCellRule depositRule, LayoutPitch pitch = default)
        {
            MarginWorldUnits = marginWorldUnits;
            DeckBandCells = deckBandCells;
            DepotRule = depotRule;
            DepositRule = depositRule;
            Pitch = pitch.OrDefault;
        }

        /// <summary>The lattice pitch (F007 T610, ARCH-4): part of the equality and of the identity, so a pitch change refits the camera.</summary>
        public LayoutPitch Pitch { get; }

        /// <summary>This composition with another lattice pitch.</summary>
        public LayoutComposition WithPitch(LayoutPitch pitch) => new LayoutComposition(MarginWorldUnits, DeckBandCells, DepotRule, DepositRule, pitch);

        /// <summary>The fit margin the camera model adds around <see cref="FactoryWorldLayout.Bounds"/>, in world units (cells of 24 art px).</summary>
        public float MarginWorldUnits { get; }

        /// <summary>The width of the deck margin band around the lattice, in lattice cells (70 x 54 art px).</summary>
        public int DeckBandCells { get; }

        public MarginCellRule DepotRule { get; }
        public MarginCellRule DepositRule { get; }

        /// <summary>
        /// A short stable identity of the composition values that decide the geometry (P1-F1): empty for the F004-reproducing defaults, so the
        /// F004 layout id is unchanged, and <c>+c</c> plus eight hex digits for any other composition. The camera model keys its refit on the
        /// layout id, so an edited composition refits instead of keeping a stale framing.
        /// </summary>
        public string IdentitySuffix
        {
            get
            {
                if (Equals(WorldCompositionSettings.DefaultComposition(WorldOrientation.Portrait)))
                {
                    return string.Empty;
                }

                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + MarginWorldUnits.GetHashCode();
                    hash = hash * 31 + DeckBandCells;
                    hash = hash * 31 + (int)DepotRule.side;
                    hash = hash * 31 + DepotRule.along;
                    hash = hash * 31 + (int)DepositRule.side;
                    hash = hash * 31 + DepositRule.along;
                    if (!Pitch.Equals(LayoutPitch.F006))
                    {
                        // Only a non-F006 pitch enters the hash, so the identity of every earlier composition is unchanged (ARCH-4).
                        hash = hash * 31 + Pitch.GetHashCode();
                    }

                    return "+c" + ((uint)hash).ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        public bool Equals(LayoutComposition other) =>
            MarginWorldUnits.Equals(other.MarginWorldUnits) && DeckBandCells == other.DeckBandCells &&
            DepotRule.side == other.DepotRule.side && DepotRule.along == other.DepotRule.along &&
            DepositRule.side == other.DepositRule.side && DepositRule.along == other.DepositRule.along && Pitch.Equals(other.Pitch);

        public override bool Equals(object obj) => obj is LayoutComposition other && Equals(other);

        public override int GetHashCode() => MarginWorldUnits.GetHashCode() ^ (DeckBandCells << 8) ^ ((int)DepotRule.side << 10) ^ ((int)DepositRule.side << 14) ^ Pitch.GetHashCode();
    }

    /// <summary>
    /// The authored world composition values (Feature 006, R22, data-model.md): margin, depot and deposit cell rules, prop weights and
    /// density per development stage, the prop height limit, and the hatch and edge rules. It is presentation tuning, not gameplay content
    /// (FR-001). The World Designer owns the values and the Architect owns the shape. The asset is created and edited only through Unity
    /// tooling (Inspector, MCP or a batch editor script), never as hand-edited YAML. The defaults reproduce F004: the half-cell fit margin
    /// and no props.
    /// </summary>
    public sealed class WorldCompositionSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/IdleFactory/Art/F006/WorldCompositionSettings.asset";

        public const int StageCount = 3;
        public const float DefaultMarginWorldUnits = SlotGridLayout.HalfCellMargin;
        public const int DefaultDeckBandCells = 1;
        public const int MaxPropHeightLimitArtPx = 24;
        public const float MaxMarginWorldUnits = 4f;
        public const int MaxDeckBandCells = 4;

        /// <summary>The fastest activity loop (10 fps, a cycle under one second); a family cap is at most this.</summary>
        public const int MaxFamilyFps = 10;

        [Tooltip("Fit margin in world units (cells of 24 art px, not lattice cells) that the camera model adds around the layout bounds. 0.5 reproduces F004.")]
        [UnityEngine.Serialization.FormerlySerializedAs("marginCells")]
        public float marginWorldUnits = DefaultMarginWorldUnits;

        [Tooltip("Width of the deck margin band around the lattice in lattice cells (70 x 54 art px). The depot and the deposit sit in this band.")]
        public int deckBandCells = DefaultDeckBandCells;

        [Tooltip("The depot cell at the end of the row trunks. Left or Right only: trunks run in the row gaps toward the depot side.")]
        public MarginCellRule depotRulePortrait = new MarginCellRule(MarginSide.Right, 0);

        public MarginCellRule depotRuleLandscape = new MarginCellRule(MarginSide.Right, 0);

        [Tooltip("The ore-vein deposit cell. Never Top: the badge band of the back row reaches into the top margin cell.")]
        public MarginCellRule depositRule = new MarginCellRule(MarginSide.Left, -1);

        [Tooltip("The lattice pitch in world units, an optional arrangement key and the hit floor (F007 T610). The defaults are the F006 lattice; an asset serialized before this field existed reads as F006.")]
        public LayoutPitchSettings layoutPitch = LayoutPitchSettings.F006();

        public StagePropWeights[] propWeightsPerStage = NewStageWeights();
        public float[] propDensityPerStage = new float[StageCount];

        [Tooltip("Height limit for props in art pixels.")]
        public int maxPropHeightArtPx = MaxPropHeightLimitArtPx;

        [Tooltip("Per machine family: the highest activity-loop rate in frames per second (6, 8 or 10), from the family's measured flash rate (R12). A family not listed runs at its pace rate.")]
        public FamilyFpsCap[] familyFpsCaps = NewDefaultFamilyFpsCaps();

        public HatchRule hatchRule = HatchRule.HatchWithPadlock;
        public EdgeRule[] edgeRulePerStage = { EdgeRule.Railing, EdgeRule.Wall, EdgeRule.Gantry };

        private static WorldCompositionSettings defaults;

        /// <summary>A shared, hidden instance with the F004-reproducing defaults, for callers that have no authored asset (tests, tools).</summary>
        public static WorldCompositionSettings Defaults
        {
            get
            {
                if (defaults == null)
                {
                    defaults = CreateInstance<WorldCompositionSettings>();
                    defaults.hideFlags = HideFlags.HideAndDontSave;
                }

                return defaults;
            }
        }

        /// <summary>The loop-rate caps the art pass T522 measured: the Assembler stays at 8 fps (3 flashes per second at its measured rate, Art Bible 6.4).</summary>
        public static FamilyFpsCap[] NewDefaultFamilyFpsCaps() => new[] { new FamilyFpsCap("machine.assembler", 8) };

        /// <summary>The cap table as a lookup (machine id to the highest fps). A missing, empty or invalid row is skipped.</summary>
        public static IReadOnlyDictionary<string, int> BuildFamilyFpsCaps(IReadOnlyList<FamilyFpsCap> caps)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            if (caps == null)
            {
                return map;
            }

            for (var i = 0; i < caps.Count; i++)
            {
                if (!string.IsNullOrEmpty(caps[i].machineId) && caps[i].maxFps > 0)
                {
                    map[caps[i].machineId] = caps[i].maxFps;
                }
            }

            return map;
        }

        /// <summary>The family loop-rate caps of these settings, for <see cref="MachinePresentationModel"/> (built once per call, not per frame).</summary>
        public IReadOnlyDictionary<string, int> FamilyFpsCapMap() => BuildFamilyFpsCaps(familyFpsCaps);

        private static StagePropWeights[] NewStageWeights()
        {
            var result = new StagePropWeights[StageCount];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = new StagePropWeights();
            }

            return result;
        }

        /// <summary>The F004-reproducing default values, without creating an asset (safe in field initializers and constructors).</summary>
        public static LayoutComposition DefaultComposition(WorldOrientation orientation) => new LayoutComposition(
            DefaultMarginWorldUnits, DefaultDeckBandCells, new MarginCellRule(MarginSide.Right, 0), new MarginCellRule(MarginSide.Left, -1), LayoutPitch.F006);

        /// <summary>The layout values for an orientation.</summary>
        public LayoutComposition ComposeFor(WorldOrientation orientation) => new LayoutComposition(
            marginWorldUnits,
            deckBandCells,
            orientation == WorldOrientation.Landscape ? depotRuleLandscape : depotRulePortrait,
            depositRule,
            layoutPitch != null ? layoutPitch.ToPitch() : LayoutPitch.F006);

        /// <summary>The deck-edge dressing of a stage (0 Open deck, 1 Walled yard, 2 Gantry line); the stage is clamped.</summary>
        public EdgeRule EdgeRuleOf(int stage) =>
            edgeRulePerStage != null && edgeRulePerStage.Length == StageCount ? edgeRulePerStage[ClampStage(stage)] : (EdgeRule)ClampStage(stage);

        /// <summary>The share of eligible prop cells that carry a prop at a stage, in [0, 1]; 0 when the settings carry no value.</summary>
        public float PropDensityOf(int stage) =>
            propDensityPerStage != null && propDensityPerStage.Length == StageCount ? Mathf.Clamp01(propDensityPerStage[ClampStage(stage)]) : 0f;

        /// <summary>The prop-kind weights of a stage; empty when the settings carry none.</summary>
        public IReadOnlyList<PropWeight> PropWeightsOf(int stage) =>
            propWeightsPerStage != null && propWeightsPerStage.Length == StageCount && propWeightsPerStage[ClampStage(stage)]?.weights != null
                ? propWeightsPerStage[ClampStage(stage)].weights
                : Array.Empty<PropWeight>();

        private static int ClampStage(int stage) => Mathf.Clamp(stage, 0, StageCount - 1);

        /// <summary>The violations of the value rules; empty when the settings are valid.</summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            if (float.IsNaN(marginWorldUnits) || marginWorldUnits < 0f || marginWorldUnits > MaxMarginWorldUnits)
            {
                problems.Add("marginWorldUnits must be within [0, " + MaxMarginWorldUnits + "].");
            }

            if (deckBandCells < 1 || deckBandCells > MaxDeckBandCells)
            {
                problems.Add("deckBandCells must be within [1, " + MaxDeckBandCells + "].");
            }

            if (layoutPitch != null)
            {
                foreach (var problem in layoutPitch.ToPitch().Problems())
                {
                    problems.Add("layoutPitch: " + problem);
                }
            }

            CheckDepot(depotRulePortrait, "depotRulePortrait", problems);
            CheckDepot(depotRuleLandscape, "depotRuleLandscape", problems);
            if (!Enum.IsDefined(typeof(MarginSide), depositRule.side) || depositRule.side == MarginSide.Top)
            {
                problems.Add("depositRule.side must be Right, Left or Bottom.");
            }

            if (propWeightsPerStage == null || propWeightsPerStage.Length != StageCount)
            {
                problems.Add("propWeightsPerStage needs exactly " + StageCount + " entries.");
            }
            else
            {
                foreach (var stage in propWeightsPerStage)
                {
                    if (stage?.weights == null)
                    {
                        problems.Add("propWeightsPerStage entries need a weights list.");
                        continue;
                    }

                    foreach (var weight in stage.weights)
                    {
                        if (string.IsNullOrEmpty(weight.kind) || float.IsNaN(weight.weight) || weight.weight < 0f)
                        {
                            problems.Add("a prop weight needs a kind and a weight of at least 0.");
                        }
                    }
                }
            }

            if (propDensityPerStage == null || propDensityPerStage.Length != StageCount)
            {
                problems.Add("propDensityPerStage needs exactly " + StageCount + " entries.");
            }
            else
            {
                foreach (var density in propDensityPerStage)
                {
                    if (float.IsNaN(density) || density < 0f || density > 1f)
                    {
                        problems.Add("propDensityPerStage values must be within [0, 1].");
                    }
                }
            }

            if (maxPropHeightArtPx < 1 || maxPropHeightArtPx > MaxPropHeightLimitArtPx)
            {
                problems.Add("maxPropHeightArtPx must be within [1, " + MaxPropHeightLimitArtPx + "].");
            }

            if (familyFpsCaps != null)
            {
                foreach (var cap in familyFpsCaps)
                {
                    if (string.IsNullOrEmpty(cap.machineId) || cap.maxFps < 1 || cap.maxFps > MaxFamilyFps)
                    {
                        problems.Add("a familyFpsCaps row needs a machine id and a rate within [1, " + MaxFamilyFps + "].");
                    }
                }
            }

            if (!Enum.IsDefined(typeof(HatchRule), hatchRule))
            {
                problems.Add("hatchRule is not a defined value.");
            }

            if (edgeRulePerStage == null || edgeRulePerStage.Length != StageCount)
            {
                problems.Add("edgeRulePerStage needs exactly " + StageCount + " entries.");
            }
            else
            {
                foreach (var edge in edgeRulePerStage)
                {
                    if (!Enum.IsDefined(typeof(EdgeRule), edge))
                    {
                        problems.Add("edgeRulePerStage holds an undefined value.");
                    }
                }
            }

            return problems;
        }

        private static void CheckDepot(MarginCellRule rule, string name, List<string> problems)
        {
            if (rule.side != MarginSide.Left && rule.side != MarginSide.Right)
            {
                problems.Add(name + ".side must be Left or Right: trunks run in the row gaps toward the depot side.");
            }
        }
    }
}
