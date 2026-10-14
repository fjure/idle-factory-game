using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IdleFactory.Run.Content;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// A complete <see cref="RunCatalog"/>, or the diagnostics of a rejected version-3 document. A partial catalog is
    /// never exposed.
    /// </summary>
    public sealed class RunCatalogLoadResult
    {
        private static readonly IReadOnlyList<ContentDiagnostic> NoDiagnostics =
            new ReadOnlyCollection<ContentDiagnostic>(new List<ContentDiagnostic>());

        private RunCatalogLoadResult(RunCatalog catalog, IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            Catalog = catalog;
            Diagnostics = diagnostics;
        }

        public bool IsSuccess => Catalog != null;

        public RunCatalog Catalog { get; }

        public IReadOnlyList<ContentDiagnostic> Diagnostics { get; }

        public static RunCatalogLoadResult Loaded(RunCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            return new RunCatalogLoadResult(catalog, NoDiagnostics);
        }

        public static RunCatalogLoadResult Rejected(IEnumerable<ContentDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            var copy = diagnostics.ToList();
            if (copy.Count == 0 || copy.Any(d => d == null))
            {
                throw new ArgumentException("A rejected result needs at least one non-null diagnostic.", nameof(diagnostics));
            }

            return new RunCatalogLoadResult(null, new ReadOnlyCollection<ContentDiagnostic>(copy));
        }
    }

    /// <summary>
    /// Builds the immutable <see cref="RunCatalog"/> from a version-3 DTO graph (specs/005-economy-contracts/contracts/
    /// gameplay-content-v3.md). Since T320 the contract rewards come from <c>rewardRatioPpm</c> through <c>ContractRewards</c> (the transitional reward adapter is gone). The builder runs <see cref="RunContentValidator"/> first and constructs the catalog only
    /// when it reports zero violations (all-or-nothing). The validator mirrors every constructor rule of the Core content
    /// model, so a constructor that still throws is an internal inconsistency: it is reported as the static code
    /// <c>content.internal</c> and never echoes the exception message, a path or document text (SEC-5). The catalog
    /// holds no DTO reference: every value is copied into Core definitions.
    /// </summary>
    public static class RunCatalogBuilder
    {
        public const string InternalErrorCode = "content.internal";

        public static RunCatalogLoadResult Build(RunContentDocumentDto document)
        {
            return Build(document, null);
        }

        public static RunCatalogLoadResult Build(RunContentDocumentDto document, IPresentationKeyResolver presentationKeys)
        {
            return Build(document, presentationKeys, Map);
        }

        /// <summary>Test-only seam: replaces the DTO-to-Core mapping so the <c>content.internal</c> path can be reached. Internal (T141); the EditMode test assembly sees it through InternalsVisibleTo.</summary>
        internal static RunCatalogLoadResult Build(
            RunContentDocumentDto document,
            IPresentationKeyResolver presentationKeys,
            Func<RunContentDocumentDto, RunCatalog> catalogFactory)
        {
            var violations = RunContentValidator.Validate(document, presentationKeys);
            if (violations.Count > 0)
            {
                return RunCatalogLoadResult.Rejected(violations);
            }

            try
            {
                return RunCatalogLoadResult.Loaded(catalogFactory(document));
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                // Deliberately drops the exception: its message could carry document values.
                return RunCatalogLoadResult.Rejected(new[]
                {
                    new ContentDiagnostic(
                        InternalErrorCode,
                        string.Empty,
                        null,
                        null,
                        null,
                        "The validated content document could not be built into a catalog."),
                });
            }
        }

        private static RunCatalog Map(RunContentDocumentDto doc)
        {
            var policy = doc.OfflinePolicies.First(p => p.Revision == 1);
            return new RunCatalog(
                doc.ContentRevision,
                doc.Resources.Select(r => new ResourceDef(r.Id, r.Label, r.PresentationKey, r.BasePrice, Unlock(r.Unlock))),
                doc.Recipes.Select(r => new MachineRecipe(r.Id, r.Inputs.Select(Amount), r.Outputs.Select(Amount), r.DurationTicks)),
                doc.Machines.Select(m => new MachineDef(
                    m.Id,
                    m.Label,
                    m.PresentationKey,
                    m.RecipeId,
                    m.MaxLevel,
                    ToCurve(m.Cost),
                    m.SpeedGrowthPpm,
                    m.Milestones.Select(x => new SpeedMilestone(x.Level, x.SpeedPpm)),
                    Unlock(m.Unlock),
                    new BuildPriceDef(m.BuildPrice.Base, m.BuildPrice.OwnedGrowthPpm, m.BuildPrice.PrestigeGrowthPpm, m.BuildPrice.Cap))),
                doc.StorageLevels.Select(s => new StorageLevelDef(s.Level, s.Capacity, s.Cost)),
                doc.FactoryLevels.Select(f => new FactoryLevelDef(f.Level, checked((int)f.Slots), f.Cost)),
                Contracts(doc),
                doc.Items.Select(Item),
                doc.Research.Select(Research),
                doc.IndustryUpgrades.Select(u => new IndustryUpgradeDef(
                    u.Id,
                    u.Label,
                    u.MaxLevel,
                    ToCurve(u.IpCost),
                    Effect(u.Effect),
                    u.DecayPpm,
                    u.PracticalCeilingLevel,
                    u.MinGainPpm)),
                new OverdriveDef(doc.Overdrive.DurationMs, doc.Overdrive.CooldownMs, doc.Overdrive.SpeedPpm, Unlock(doc.Overdrive.Unlock)),
                new PrestigeDef(
                    // The legacy threshold is T(0), the threshold at prestige index 0 (T312 follow-up).
                    doc.Prestige.Threshold.Base,
                    doc.Prestige.IpCurve.Kind,
                    doc.Prestige.IpCurve.Coefficient,
                    doc.Prestige.IpCurve.Divisor,
                    new ThresholdDef(doc.Prestige.Threshold.Base, doc.Prestige.Threshold.GrowthPpm, doc.Prestige.Threshold.MaxIndex)),
                new RunStartDef(
                    doc.RunStart.StartingCash,
                    doc.RunStart.InitialUnlocks,
                    doc.RunStart.RemovalRefundPpm,
                    doc.RunStart.MinCycleTicks,
                    doc.RunStart.MaxCombinedSpeedPpm,
                    doc.RunStart.MaxCombinedOutputFactor,
                    doc.RunStart.MaxRewardRatioPpm,
                    doc.RunStart.SellSteps,
                    doc.RunStart.SellAll,
                    doc.RunStart.LiveGapThresholdMs,
                    ManualMining(doc.RunStart.ManualMining),
                    doc.RunStart.MinRunDurationMs,
                    doc.RunStart.UptimeMaxPpm),
                new RunOfflinePolicy(policy.Revision, policy.EfficiencyNumerator, policy.EfficiencyDenominator, policy.MaxCreditedTicks),
                // Ascending by (index, Item ID), so the catalog is identical for any document order of the entries (T314 follow-up, data-model.md).
                new ContractItemLadder(
                    doc.ContractItemLadder.Entries
                        .OrderBy(e => e.Index)
                        .ThenBy(e => e.ItemId, StringComparer.Ordinal)
                        .Select(e => new ContractItemLadderEntry(e.Index, e.ItemId)),
                    doc.ContractItemLadder.PerRunCap),
                doc.Milestones.Select(m => new MilestoneDef(m.FractionPpm)),
                doc.ChainDominanceMarginPpm);
        }

        private static ManualMiningDef ManualMining(RunManualMiningDto dto)
        {
            return new ManualMiningDef(
                dto.ResourceId,
                dto.BaseYieldMilli,
                dto.MaxActivationsPerSecond,
                dto.StartWindowMs,
                dto.ReferenceMinerLevel,
                dto.MaxShareOfMinerPpm,
                dto.MiningAdvantageMaxPpm);
        }

        private static Curve ToCurve(RunCurveDto dto)
        {
            return new Curve(dto.Base, dto.GrowthPpm, dto.Rounding);
        }

        private static UnlockCondition Unlock(RunUnlockDto dto)
        {
            return new UnlockCondition(dto.Kind, dto.Id, dto.Level ?? 0);
        }

        private static ResourceAmount Amount(RunAmountDto dto)
        {
            return new ResourceAmount(dto.ResourceId, dto.Amount);
        }

        // Since T320 an offer carries only its RP curve as a reward definition: the Cash of a completion comes from RewardRatioPpm at delivery
        // (ContractRewards) and the Items from the catalog ladder, so nothing is synthesized per offer any more.
        private static List<ContractOfferDef> Contracts(RunContentDocumentDto doc)
        {
            var kinds = LabelKinds(doc);
            var offers = new List<ContractOfferDef>();
            for (var i = 0; i < doc.Contracts.Count; i++)
            {
                var dto = doc.Contracts[i];
                offers.Add(new ContractOfferDef(
                    dto.Id,
                    kinds[i],
                    dto.ResourceId,
                    ToCurve(dto.Quantity),
                    dto.DurationMs,
                    new[] { new ContractRewardDef("rp", ToCurve(dto.RpReward)) },
                    Unlock(dto.Unlock),
                    dto.Objective,
                    dto.Class,
                    dto.RewardRatioPpm,
                    dto.CooldownMs,
                    dto.Bonus == null ? null : new BonusDef(Unlock(dto.Bonus.Trigger), dto.Bonus.WindowMs)));
            }

            return offers;
        }

        /// <summary>
        /// The legacy <c>ContractOfferDef.Kind</c> of every contract of <paramref name="doc"/>, in document order: <c>timedBar</c> for a timed
        /// offer; for an untimed offer <c>plate</c> for the cheapest resource among the untimed offers and <c>gear</c> for every other resource. The
        /// Kind only labels an offer in the F003 views (the Contracts panel title); it takes no part in a reward, a rule or a save (the
        /// vocabulary leaves with the Contracts UI task). Only the label is still derived here, not any gameplay.
        /// </summary>
        private static string[] LabelKinds(RunContentDocumentDto doc)
        {
            var prices = doc.Resources.ToDictionary(r => r.Id, r => r.BasePrice, StringComparer.Ordinal);
            long PriceOf(string id) => id != null && prices.TryGetValue(id, out var price) ? price : long.MaxValue;
            var untimedResources = doc.Contracts
                .Where(c => c != null && !c.DurationMs.HasValue && c.ResourceId != null)
                .Select(c => c.ResourceId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(PriceOf)
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToList();
            var kinds = new string[doc.Contracts.Count];
            for (var i = 0; i < kinds.Length; i++)
            {
                var contract = doc.Contracts[i];
                kinds[i] = contract.DurationMs.HasValue
                    ? "timedBar"
                    : untimedResources.IndexOf(contract.ResourceId) <= 0 ? "plate" : "gear";
            }

            return kinds;
        }

        // An Item's own target (factory or machineType) selects the machines it affects, so its effect carries the
        // widest effect target the Core model accepts for the kind: allMachines for speed, machineType for output.
        private static ItemDef Item(RunItemDto dto)
        {
            var effect = dto.Effect.Kind == "machineOutput"
                ? new EffectDef("machineOutput", "machineType", null, dto.Effect.Value)
                : new EffectDef(dto.Effect.Kind, "allMachines", null, dto.Effect.Value);
            return new ItemDef(
                dto.Id,
                dto.Tier,
                dto.Label,
                dto.PresentationKey,
                dto.DurationMs,
                dto.Target.Kind,
                dto.Target.AllowedMachineIds ?? new List<string>(),
                effect);
        }

        private static ResearchDef Research(RunResearchDto dto)
        {
            return new ResearchDef(
                dto.Id,
                dto.Label,
                dto.MaxLevel,
                ToCurve(dto.RpCost),
                dto.Prerequisites.Select(p => new ResearchPrerequisite(p.Id, p.Level)),
                Effect(dto.Effect));
        }

        private static EffectDef Effect(RunEffectDto dto)
        {
            return new EffectDef(dto.Kind, dto.Target?.Kind ?? "none", dto.Target?.Id, dto.Value);
        }
    }
}
