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
    /// gameplay-content-v3.md). Until T320 the contract rewards are synthesized by <see cref="RunRewardAdapter"/>. The builder runs <see cref="RunContentValidator"/> first and constructs the catalog only
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
                new ContractItemLadder(
                    doc.ContractItemLadder.Entries.Select(e => new ContractItemLadderEntry(e.Index, e.ItemId)),
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

        // Transitional (T313, removed by T320): the legacy reward lists come from RunRewardAdapter. The validator has already
        // proven that every Cash table fits and that the vocabularies are known, so a failure here is content.internal.
        private static List<ContractOfferDef> Contracts(RunContentDocumentDto doc)
        {
            var kinds = RunRewardAdapter.DeriveKinds(doc);
            var itemRewards = RunRewardAdapter.ItemRewards(doc);
            var prices = doc.Resources.ToDictionary(r => r.Id, r => r.BasePrice, StringComparer.Ordinal);
            var offers = new List<ContractOfferDef>();
            for (var i = 0; i < doc.Contracts.Count; i++)
            {
                var dto = doc.Contracts[i];
                var quantity = ToCurve(dto.Quantity);
                if (!RunRewardAdapter.TryCashTable(quantity.Precompute(RunRewardAdapter.TableEntries), prices[dto.ResourceId], dto.RewardRatioPpm, out var cash))
                {
                    throw new InvalidOperationException("The Cash reward table does not fit the exact range.");
                }

                var rewards = new List<ContractRewardDef> { ContractRewardDef.FromTable("cash", cash) };
                rewards.AddRange(itemRewards[i]);
                rewards.Add(new ContractRewardDef("rp", ToCurve(dto.RpReward)));
                offers.Add(new ContractOfferDef(
                    dto.Id,
                    kinds[i],
                    dto.ResourceId,
                    quantity,
                    dto.DurationMs,
                    rewards,
                    Unlock(dto.Unlock),
                    dto.Objective,
                    dto.Class,
                    dto.RewardRatioPpm,
                    dto.CooldownMs,
                    dto.Bonus == null ? null : new BonusDef(Unlock(dto.Bonus.Trigger), dto.Bonus.WindowMs)));
            }

            return offers;
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
