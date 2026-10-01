using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IdleFactory.Run.Content;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// A complete <see cref="RunCatalog"/>, or the diagnostics of a rejected version-2 document. A partial catalog is
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
    /// Builds the immutable <see cref="RunCatalog"/> from a version-2 DTO graph (specs/003-first-factory-prestige/contracts/
    /// gameplay-content-v2.md). The builder runs <see cref="RunContentValidator"/> first and constructs the catalog only
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

        /// <summary>Seam for tests: replaces the DTO-to-Core mapping so the <c>content.internal</c> path can be reached.</summary>
        public static RunCatalogLoadResult Build(
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
            catch (Exception)
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
                    Unlock(m.Unlock))),
                doc.StorageLevels.Select(s => new StorageLevelDef(s.Level, s.Capacity, s.Cost)),
                doc.FactoryLevels.Select(f => new FactoryLevelDef(f.Level, checked((int)f.Slots), f.Cost)),
                doc.Contracts.Select(Contract),
                doc.Items.Select(Item),
                doc.Research.Select(Research),
                doc.IndustryUpgrades.Select(u => new IndustryUpgradeDef(u.Id, u.Label, u.MaxLevel, ToCurve(u.IpCost), Effect(u.Effect))),
                new OverdriveDef(doc.Overdrive.DurationMs, doc.Overdrive.CooldownMs, doc.Overdrive.SpeedPpm, Unlock(doc.Overdrive.Unlock)),
                new PrestigeDef(
                    doc.Prestige.ThresholdFactoryValue,
                    doc.Prestige.IpCurve.Kind,
                    doc.Prestige.IpCurve.Coefficient,
                    doc.Prestige.IpCurve.Divisor),
                new RunStartDef(
                    doc.RunStart.StartingCash,
                    doc.RunStart.InitialUnlocks,
                    doc.RunStart.RemovalRefundPpm,
                    doc.RunStart.MinCycleTicks,
                    doc.RunStart.MaxCombinedSpeedPpm,
                    doc.RunStart.MaxCombinedOutputFactor,
                    doc.RunStart.MaxRewardRatioPpm),
                new RunOfflinePolicy(policy.Revision, policy.EfficiencyNumerator, policy.EfficiencyDenominator, policy.MaxCreditedTicks));
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

        private static ContractOfferDef Contract(RunContractDto dto)
        {
            return new ContractOfferDef(
                dto.Id,
                dto.Kind,
                dto.ResourceId,
                ToCurve(dto.Quantity),
                dto.DurationMs,
                dto.Rewards.Select(r => new ContractRewardDef(
                    r.Kind,
                    r.Curve == null ? null : ToCurve(r.Curve),
                    r.ItemId,
                    r.OnCompletionIndices)),
                Unlock(dto.Unlock));
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
