using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Application
{
    /// <summary>Result of <see cref="RunCommands.PurchaseResearch"/>.</summary>
    public readonly struct ResearchPurchaseResult
    {
        public string ResearchId { get; }
        public int NewLevel { get; }
        public long RpPaid { get; }
        public bool HasNext { get; }

        /// <summary>The RP cost of the next level; 0 at the maximum level.</summary>
        public long NextCost { get; }

        public ResearchPurchaseResult(string researchId, int newLevel, long rpPaid, bool hasNext, long nextCost)
        {
            ResearchId = researchId;
            NewLevel = newLevel;
            RpPaid = rpPaid;
            HasNext = hasNext;
            NextCost = nextCost;
        }
    }

    /// <summary>Result of <see cref="RunCommands.PurchaseIndustry"/>.</summary>
    public readonly struct IndustryPurchaseResult
    {
        public string UpgradeId { get; }
        public int NewLevel { get; }
        public long IpPaid { get; }
        public bool HasNext { get; }

        /// <summary>The IP cost of the next level; 0 at the maximum level.</summary>
        public long NextCost { get; }

        /// <summary>True for <c>startingCash</c> and <c>startingStorageLevel</c>, which apply at the next run start (R12).</summary>
        public bool AppliesNextRun { get; }

        public IndustryPurchaseResult(string upgradeId, int newLevel, long ipPaid, bool hasNext, long nextCost, bool appliesNextRun)
        {
            UpgradeId = upgradeId;
            NewLevel = newLevel;
            IpPaid = ipPaid;
            HasNext = hasNext;
            NextCost = nextCost;
            AppliesNextRun = appliesNextRun;
        }
    }

    /// <summary>
    /// The Research and Industry purchases of User Story 3. A Research purchase always ends in
    /// <see cref="RunSession.EndCommand{T}"/> (T140: the unlock pass evaluates <c>researchPurchased</c> at the
    /// command settle). Unknown IDs have no contract code and are <c>command.invalid</c> (the T137 convention).
    /// </summary>
    public sealed partial class RunCommands
    {
        /// <summary>
        /// Buys the next level of a Research. Rejected, in this order, with command.invalid (unknown),
        /// research.prerequisite (a prerequisite below its required level), level.max, rp.insufficient or value.limit.
        /// Spends the RP of the next level and raises the level in one checked step
        /// (<see cref="RunState.TryPurchaseResearch"/>); the effect applies at once (the settle rescales progress).
        /// </summary>
        public RunCommandResult<ResearchPurchaseResult> PurchaseResearch(string researchId)
        {
            if (_session.IsLifecycleBlocked) return Reject<ResearchPurchaseResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            if (researchId == null || !_catalog.Research.TryGetValue(researchId, out ResearchDef def))
                return Reject<ResearchPurchaseResult>(RunReasons.CommandInvalid);
            for (int i = 0; i < def.Prerequisites.Count; i++)
                if (state.ResearchLevel(def.Prerequisites[i].ResearchId) < def.Prerequisites[i].Level)
                    return Reject<ResearchPurchaseResult>(RunReasons.ResearchPrerequisite);
            int level = (int)state.ResearchLevel(researchId);
            if (level >= def.MaxLevel) return Reject<ResearchPurchaseResult>(RunReasons.LevelMax);
            long cost = def.RpCostTable[level];
            if (state.Rp < cost) return Reject<ResearchPurchaseResult>(RunReasons.RpInsufficient);

            // Trial (undone before the real transaction): the modifiers after the purchase must be computable.
            RunStateResult trial = state.TryPurchaseResearch(researchId, cost);
            if (trial != RunStateResult.Ok) return Reject<ResearchPurchaseResult>(RunReasons.From(trial));
            bool fits = ChangeFits(null);
            state.TrySetResearchLevel(researchId, level);
            state.TryAddRp(cost);
            if (!fits) return Reject<ResearchPurchaseResult>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult bought = state.TryPurchaseResearch(researchId, cost);
            if (bought != RunStateResult.Ok) return Reject<ResearchPurchaseResult>(RunReasons.From(bought));
            bool hasNext = level + 1 < def.MaxLevel;
            return _session.EndCommand(new ResearchPurchaseResult(researchId, level + 1, cost, hasNext, hasNext ? def.RpCostTable[level + 1] : 0), true);
        }

        /// <summary>
        /// Buys the next level of an Industry upgrade with IP, permanently. Rejected with command.invalid (unknown
        /// upgrade, or no permanent state), level.max, ip.insufficient or value.limit. The run-scoped effects
        /// (<c>machineSpeed</c>, <c>upgradeCost</c>, <c>salePrice</c>) apply now; <c>startingCash</c> and
        /// <c>startingStorageLevel</c> only change the permanent level, which the next run start reads (R12).
        /// </summary>
        public RunCommandResult<IndustryPurchaseResult> PurchaseIndustry(string upgradeId)
        {
            if (_session.IsLifecycleBlocked) return Reject<IndustryPurchaseResult>(RunReasons.LifecycleBlocked);
            PermanentState permanent = Permanent;
            if (permanent == null || upgradeId == null || !_catalog.IndustryUpgrades.TryGetValue(upgradeId, out IndustryUpgradeDef def))
                return Reject<IndustryPurchaseResult>(RunReasons.CommandInvalid);
            int level = (int)permanent.IndustryLevel(upgradeId);
            if (level >= def.MaxLevel) return Reject<IndustryPurchaseResult>(RunReasons.LevelMax);
            long cost = def.IpCostTable[level];
            if (permanent.Ip < cost) return Reject<IndustryPurchaseResult>(RunReasons.IpInsufficient);

            RunStateResult trial = permanent.TryPurchaseIndustry(upgradeId, cost);
            if (trial != RunStateResult.Ok) return Reject<IndustryPurchaseResult>(RunReasons.From(trial));
            bool fits = ChangeFits(null);
            permanent.TrySetIndustryLevel(upgradeId, level);
            permanent.TryAddIp(cost);
            if (!fits) return Reject<IndustryPurchaseResult>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult bought = permanent.TryPurchaseIndustry(upgradeId, cost);
            if (bought != RunStateResult.Ok) return Reject<IndustryPurchaseResult>(RunReasons.From(bought));
            bool hasNext = level + 1 < def.MaxLevel;
            return _session.EndCommand(new IndustryPurchaseResult(upgradeId, level + 1, cost, hasNext, hasNext ? def.IpCostTable[level + 1] : 0,
                IsNextRunEffect(def.Effect)), true);
        }

        internal static bool IsNextRunEffect(EffectDef effect) => effect.Kind == "startingCash" || effect.Kind == "startingStorageLevel";
    }
}
