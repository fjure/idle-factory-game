using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Injectable presentation-key resolution hook. The validator never loads sprites: the composition root supplies an
    /// implementation backed by the sprite catalog, and an unresolved key on a resource, machine or Item is reported as
    /// <c>presentation.key.unresolved</c>. Passing no resolver skips the check (like v1, a key is not resolved by the reader).
    /// </summary>
    public interface IPresentationKeyResolver
    {
        bool IsResolved(string presentationKey);
    }

    /// <summary>
    /// Applies the version-3 semantic rules (specs/003-first-factory-prestige/contracts/gameplay-content-v2.md, "Semantic rule
    /// codes", as evolved by specs/005-economy-contracts/contracts/gameplay-content-v3.md) to a fresh <see cref="RunContentReader"/> DTO graph. It reports every violation, in
    /// document order (families in canonical order, entries in list order, then rule order), up to
    /// <see cref="GameplayContentLimits.MaximumReportedDiagnostics"/>; the last reported diagnostic then carries a
    /// truncation note. No domain object is built here (RunCatalog construction is a later work item). Diagnostics carry
    /// JSON Pointers, never file-system paths, and echoed IDs are truncated to 128 UTF-16 code units.
    /// </summary>
    public static class RunContentValidator
    {
        private const long Ppm = 1_000_000L;
        private const long MillionPpm = Ppm;
        private const int MaxEchoedLength = 128;
        private const int MaxDefinitionLevel = 1024;
        private const int ContractTableEntries = 1024;
        private const int MaxFactorySlots = 12;
        private const long MaxOutputFactorBound = 1_000_000L;
        private const long UpperTimeBound = 1_000_000_000_000L;

        // Structural content rules (T314). The "content constants" have no catalog member; they are named here and listed in the guide's rule table.
        private const int MaxSellSteps = 8;
        private const long LiveGapMinMs = 1_000L;
        private const long LiveGapMaxMs = 28_800_000L;
        private const long BuildBound = 1_000_000_000_000L;
        private const int RatioRulesRevision = 3;
        private const long StandingRatioMaxPpm = 1_500_000L;
        private const long BonusRatioMaxPpm = 2_000_000L;
        private const long ProduceMinQuantity = 10L;
        private const long CostFloorPpm = 500_000L;
        private const long CostFloorScale = 1_000_000_000_000_000_000L;

        /// <summary>
        /// Every rule code this validator implements: the v2 contract table minus the codes v3 retires (<c>contract.kind.*</c>,
        /// <c>contract.reward.empty</c>, <c>contract.reward.kind.unknown</c>, <c>contract.kinds.incomplete</c>, <c>contract.duration.required</c> and
        /// <c>contract.duration.forbidden</c>), plus <c>contract.objective.unknown</c> and <c>contract.class.unknown</c> (T313, before a
        /// <c>ContractOfferDef</c> is constructed), including the identity, structure and unlock codes and <c>content.version.unsupported</c>
        /// from the reader, plus the structural rules of T314 (<c>sell.steps</c>, <c>live-gap.range</c>, <c>contract.bonus.fields</c>,
        /// <c>contract.ratio.standing</c>, <c>contract.ratio.bonus</c>, <c>contract.quantity.cap</c>, <c>contract.produce.min</c>,
        /// <c>milestone.order</c>, <c>build.base.bound</c> and <c>cost.floor</c>), plus <c>prestige.threshold.bound</c> and
        /// <c>industry.ceiling.level</c> (T331). The semantic rules of the v3 contract table that the
        /// story tasks own are not implemented here.
        /// </summary>
        public static readonly IReadOnlyList<string> RuleCodes = new[]
        {
            "content.version.unsupported",
            "id.empty", "id.too-long", "id.not-canonical", "id.duplicate", "id.reserved", "value.null", "value.out-of-range",
            "unlock.kind.unknown", "unlock.level.invalid", "unlock.unreachable", "ref.research.unknown",
            "runstart.unlock.unknown", "runstart.refund.out-of-range", "runstart.min-cycle.invalid",
            "runstart.speed-bound.invalid", "runstart.output-bound.invalid", "runstart.reward-ratio.invalid",
            "resource.price.negative", "recipe.outputs.empty", "recipe.amount.non-positive", "recipe.duration.non-positive",
            "recipe.duration.out-of-range", "recipe.resource.duplicate", "ref.resource.unknown",
            "ref.recipe.unknown", "machine.max-level.invalid", "machine.milestone.level-invalid",
            "machine.milestone.duplicate", "curve.growth.invalid", "curve.overflow", "curve.stalled", "speed.level.dead",
            "batch.overflow",
            "level.sequence.invalid", "level.first-cost.non-zero", "storage.capacity.non-increasing",
            "factory.slots.non-increasing", "factory.slots.too-many",
            "contract.objective.unknown", "contract.class.unknown", "contract.reward.item.unknown",
            "contract.reward.ratio", "contract.rp.growth", "contract.duration.out-of-range",
            "sell.steps", "live-gap.range", "contract.bonus.fields", "contract.ratio.standing", "contract.ratio.bonus",
            "contract.quantity.cap", "contract.produce.min", "milestone.order", "build.base.bound", "cost.floor",
            "prestige.threshold.bound", "industry.ceiling.level",
            "item.tier.invalid", "item.tiers.incomplete", "item.target.invalid", "item.duration.non-positive", "item.duration.out-of-range",
            "research.prerequisite.unknown", "research.prerequisite.cycle", "research.prerequisite.level-invalid",
            "upgrade.max-level.invalid", "industry.first-affordable.missing",
            "effect.kind.unknown", "effect.target.invalid", "effect.value.out-of-range",
            "overdrive.value.invalid", "overdrive.duration.out-of-range", "overdrive.cooldown.out-of-range", "prestige.threshold.invalid", "prestige.curve.unsupported",
            "policy.revision-1.required",
            "presentation.key.unresolved",
        };

        public static IReadOnlyList<ContentDiagnostic> Validate(RunContentDocumentDto document)
        {
            return Validate(document, null);
        }

        public static IReadOnlyList<ContentDiagnostic> Validate(RunContentDocumentDto document, IPresentationKeyResolver presentationKeys)
        {
            var validation = new Validation(presentationKeys);
            if (document == null)
            {
                validation.Add("value.null", string.Empty, null, "Content document is missing.");
                return validation.Diagnostics;
            }

            validation.Run(document);
            return validation.Diagnostics;
        }

        private enum EffectSource
        {
            Item,
            Research,
            Industry,
        }

        private sealed class SpeedSource
        {
            public string TargetKind;
            public string TargetId;
            public long Ppm;
            public int Levels;

            /// <summary>Per-level factors of a decaying Industry source (T331), or null when every level folds <see cref="Ppm"/>.</summary>
            public long[] Table;
        }

        private sealed class Validation
        {
            private readonly IPresentationKeyResolver _keys;
            private readonly List<ContentDiagnostic> _diagnostics = new List<ContentDiagnostic>();
            private bool _truncated;

            private readonly HashSet<string> _resourceIds = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, RunResourceDto> _resources = new Dictionary<string, RunResourceDto>(StringComparer.Ordinal);
            private readonly Dictionary<string, RunRecipeDto> _recipes = new Dictionary<string, RunRecipeDto>(StringComparer.Ordinal);
            private readonly HashSet<string> _machineIds = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _contractIds = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _itemIds = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, RunResearchDto> _research = new Dictionary<string, RunResearchDto>(StringComparer.Ordinal);
            private readonly HashSet<string> _obtainableItems = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _cyclicResearch = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _unlockable = new HashSet<string>(StringComparer.Ordinal);

            private readonly List<SpeedSource> _researchSpeed = new List<SpeedSource>();
            private readonly List<SpeedSource> _industrySpeed = new List<SpeedSource>();

            // The final sets of the unlock fixpoint, kept for the bonus-trigger reachability check.
            private HashSet<string> _producedFinal = new HashSet<string>(StringComparer.Ordinal);
            private HashSet<string> _purchasableFinal = new HashSet<string>(StringComparer.Ordinal);

            private RunContentDocumentDto _doc;
            private int _factoryLevelCount;

            // Run-start bounds, valid only when the matching flag is set.
            private bool _minCycleOk;
            private bool _speedBoundOk;
            private bool _outputBoundOk;
            private bool _ratioBoundOk;
            private long _minCycle;
            private long _speedBound;
            private long _outputBound;
            private long _ratioBound;
            private bool _overdriveSpeedOk;
            private long _overdriveSpeed;
            private long _totalAdded;

            public Validation(IPresentationKeyResolver keys)
            {
                _keys = keys;
            }

            public IReadOnlyList<ContentDiagnostic> Diagnostics => _diagnostics.AsReadOnly();

            public void Add(string code, string path, string entryId, string message)
            {
                _totalAdded++;
                if (_diagnostics.Count < GameplayContentLimits.MaximumReportedDiagnostics)
                {
                    _diagnostics.Add(new ContentDiagnostic(code, path, Clip(entryId), null, null, OneLine(message)));
                    return;
                }

                if (!_truncated)
                {
                    _truncated = true;
                    var last = _diagnostics[_diagnostics.Count - 1];
                    _diagnostics[_diagnostics.Count - 1] = new ContentDiagnostic(
                        last.Code,
                        last.Path,
                        last.EntryId,
                        last.Line,
                        last.Column,
                        last.Message + $" Additional semantic diagnostics were omitted after {GameplayContentLimits.MaximumReportedDiagnostics} violations.");
                }
            }

            public void Run(RunContentDocumentDto doc)
            {
                _doc = doc;
                _factoryLevelCount = doc.FactoryLevels == null ? 0 : doc.FactoryLevels.Count;

                if (doc.ContentSchemaVersion != RunContentReader.SupportedContentSchemaVersion)
                {
                    Add("content.version.unsupported", "/contentSchemaVersion", null, "contentSchemaVersion must be " + RunContentReader.SupportedContentSchemaVersion.ToString(CultureInfo.InvariantCulture) + ".");
                }

                if (doc.ContentRevision < 1)
                {
                    Add("value.out-of-range", "/contentRevision", null, "contentRevision must be at least 1.");
                }

                GatherIds();
                GatherObtainableItems();
                GatherSpeedSources();
                FindResearchCycles();
                ComputeUnlockReachability();

                ValidateRunStart();
                ValidateResources();
                ValidateRecipes();
                ValidateMachines();
                ValidateStorageLevels();
                ValidateFactoryLevels();
                ValidateContracts();
                ValidateContractItemLadder();
                ValidateItems();
                ValidateResearch();
                ValidateOverdrive();
                ValidatePrestige();
                ValidateMilestones();
                ValidateIndustryUpgrades();
                ValidateCostFloor();
                CheckRange(_doc.ChainDominanceMarginPpm, 1, ExactBound.Value, "/chainDominanceMarginPpm", null, "chainDominanceMarginPpm must be 1 to 10^18 (greater than 0).");
                ValidateOfflinePolicies();
            }

            // ---- pre-passes (no diagnostics) ---------------------------------------------------------

            private void GatherIds()
            {
                if (_doc.Resources != null)
                {
                    foreach (var resource in _doc.Resources)
                    {
                        if (resource != null && IsCanonicalId(resource.Id))
                        {
                            _resourceIds.Add(resource.Id);
                            _resources[resource.Id] = resource;
                        }
                    }
                }

                if (_doc.Recipes != null)
                {
                    foreach (var recipe in _doc.Recipes)
                    {
                        if (recipe != null && IsCanonicalId(recipe.Id))
                        {
                            _recipes[recipe.Id] = recipe;
                        }
                    }
                }

                AddCanonicalIds(_doc.Machines, m => m.Id, _machineIds);
                AddCanonicalIds(_doc.Contracts, c => c.Id, _contractIds);
                AddCanonicalIds(_doc.Items, i => i.Id, _itemIds);
                if (_doc.Research != null)
                {
                    foreach (var research in _doc.Research)
                    {
                        if (research != null && IsCanonicalId(research.Id))
                        {
                            _research[research.Id] = research;
                        }
                    }
                }
            }

            private static void AddCanonicalIds<T>(List<T> list, Func<T, string> id, HashSet<string> target) where T : class
            {
                if (list == null)
                {
                    return;
                }

                foreach (var entry in list)
                {
                    if (entry != null && IsCanonicalId(id(entry)))
                    {
                        target.Add(id(entry));
                    }
                }
            }

            private void GatherObtainableItems()
            {
                var ladder = _doc.ContractItemLadder;
                if (ladder == null || ladder.Entries == null)
                {
                    return;
                }

                foreach (var entry in ladder.Entries)
                {
                    if (entry != null && entry.ItemId != null && entry.Index >= 0 && entry.Index < ContractTableEntries)
                    {
                        _obtainableItems.Add(entry.ItemId);
                    }
                }
            }

            private void GatherSpeedSources()
            {
                if (_doc.Overdrive != null && _doc.Overdrive.SpeedPpm > Ppm && _doc.Overdrive.SpeedPpm <= ExactBound.Value)
                {
                    _overdriveSpeedOk = true;
                    _overdriveSpeed = _doc.Overdrive.SpeedPpm;
                }

                if (_doc.Research != null)
                {
                    foreach (var research in _doc.Research)
                    {
                        AddSpeedSource(_researchSpeed, research == null ? null : research.Effect, research == null ? 0 : research.MaxLevel);
                    }
                }

                if (_doc.IndustryUpgrades != null)
                {
                    foreach (var upgrade in _doc.IndustryUpgrades)
                    {
                        AddSpeedSource(_industrySpeed, upgrade == null ? null : upgrade.Effect, upgrade == null ? 0 : upgrade.MaxLevel, upgrade == null ? 0 : upgrade.DecayPpm);
                    }
                }
            }

            /// <summary>The per-level factors of a decaying Industry factor effect (T331), or null when the source does not decay or the table is not valid.</summary>
            private static long[] DecayTable(RunEffectDto effect, int maxLevel, long decayPpm)
            {
                if (!IndustryValueTable.IsDecaying(decayPpm) ||
                    !IndustryValueTable.TryBuild(effect.Kind, effect.Value, decayPpm, maxLevel, out var table))
                {
                    return null;
                }

                return table;
            }

            private static void AddSpeedSource(List<SpeedSource> sources, RunEffectDto effect, int maxLevel, long decayPpm = 0)
            {
                if (effect == null || effect.Kind != "machineSpeed" || effect.Target == null ||
                    effect.Value <= Ppm || effect.Value > ExactBound.Value || maxLevel < 1 || maxLevel > MaxDefinitionLevel)
                {
                    return;
                }

                if (effect.Target.Kind == "allMachines" || (effect.Target.Kind == "machine" && effect.Target.Id != null))
                {
                    sources.Add(new SpeedSource
                    {
                        TargetKind = effect.Target.Kind,
                        TargetId = effect.Target.Id,
                        Ppm = effect.Value,
                        Levels = maxLevel,
                        Table = DecayTable(effect, maxLevel, decayPpm),
                    });
                }
            }

            /// <summary>
            /// Kahn elimination over the valid prerequisite edges. Whatever remains is in a cycle or depends on one;
            /// a member is reported only when it can reach itself.
            /// </summary>
            private void FindResearchCycles()
            {
                if (_doc.Research == null)
                {
                    return;
                }

                var prerequisites = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
                foreach (var research in _doc.Research)
                {
                    if (research == null || !IsCanonicalId(research.Id))
                    {
                        continue;
                    }

                    if (!prerequisites.TryGetValue(research.Id, out var set))
                    {
                        set = new HashSet<string>(StringComparer.Ordinal);
                        prerequisites.Add(research.Id, set);
                    }

                    if (research.Prerequisites == null)
                    {
                        continue;
                    }

                    foreach (var prerequisite in research.Prerequisites)
                    {
                        if (prerequisite != null && IsCanonicalId(prerequisite.Id) && _research.ContainsKey(prerequisite.Id))
                        {
                            set.Add(prerequisite.Id);
                        }
                    }
                }

                var remaining = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
                foreach (var pair in prerequisites)
                {
                    remaining.Add(pair.Key, new HashSet<string>(pair.Value, StringComparer.Ordinal));
                }

                var changed = true;
                while (changed)
                {
                    changed = false;
                    var free = new List<string>();
                    foreach (var pair in remaining)
                    {
                        if (pair.Value.Count == 0)
                        {
                            free.Add(pair.Key);
                        }
                    }

                    foreach (var id in free)
                    {
                        remaining.Remove(id);
                        changed = true;
                        foreach (var pair in remaining)
                        {
                            pair.Value.Remove(id);
                        }
                    }
                }

                foreach (var start in remaining.Keys)
                {
                    var visited = new HashSet<string>(StringComparer.Ordinal);
                    var stack = new Stack<string>();
                    foreach (var next in remaining[start])
                    {
                        stack.Push(next);
                    }

                    while (stack.Count > 0)
                    {
                        var current = stack.Pop();
                        if (current == start)
                        {
                            _cyclicResearch.Add(start);
                            break;
                        }

                        if (!visited.Add(current) || !remaining.TryGetValue(current, out var following))
                        {
                            continue;
                        }

                        foreach (var next in following)
                        {
                            stack.Push(next);
                        }
                    }
                }
            }

            /// <summary>
            /// Static fixpoint over the unlock graph (no diagnostics here; <see cref="ValidateUnlock"/> reports). Start set:
            /// <c>runStart.initialUnlocks</c>. A resource, machine, contract offer or Overdrive becomes unlockable when its
            /// condition can hold given what is already reachable: <c>always</c> and <c>factoryLevelAtLeast</c> always can
            /// (factory levels are bought, and an out-of-range level has its own code); <c>researchPurchased</c> needs a
            /// purchasable Research (every prerequisite purchasable and some unlockable contract offer whose resource can be
            /// produced pays RP); <c>resourceProduced</c> needs a producer, that is an unlockable machine whose recipe
            /// resources are all unlockable and whose inputs can themselves be produced (stock starts empty). References to
            /// unknown IDs and unknown unlock kinds are treated as satisfiable, because they have their own diagnostics.
            /// </summary>
            private void ComputeUnlockReachability()
            {
                var produced = new HashSet<string>(StringComparer.Ordinal);
                var purchasable = new HashSet<string>(StringComparer.Ordinal);
                _producedFinal = produced;
                _purchasableFinal = purchasable;
                if (_doc.RunStart != null && _doc.RunStart.InitialUnlocks != null)
                {
                    foreach (var id in _doc.RunStart.InitialUnlocks)
                    {
                        if (IsCanonicalId(id))
                        {
                            _unlockable.Add(id);
                        }
                    }
                }

                var changed = true;
                while (changed)
                {
                    changed = false;
                    changed |= UnlockCandidates(_doc.Resources, r => r.Id, r => r.Unlock, produced, purchasable);
                    changed |= UnlockCandidates(_doc.Machines, m => m.Id, m => m.Unlock, produced, purchasable);
                    changed |= UnlockCandidates(_doc.Contracts, c => c.Id, c => c.Unlock, produced, purchasable);
                    if (_doc.Overdrive != null && !_unlockable.Contains(RunState.OverdriveUnlockId) &&
                        UnlockConditionCanHold(_doc.Overdrive.Unlock, produced, purchasable))
                    {
                        _unlockable.Add(RunState.OverdriveUnlockId);
                        changed = true;
                    }

                    changed |= ProduceResources(produced);
                    changed |= PurchaseResearch(produced, purchasable);
                }
            }

            private bool UnlockCandidates<T>(
                List<T> list,
                Func<T, string> id,
                Func<T, RunUnlockDto> unlock,
                HashSet<string> produced,
                HashSet<string> purchasable) where T : class
            {
                var added = false;
                if (list == null)
                {
                    return false;
                }

                foreach (var entry in list)
                {
                    if (entry == null || !IsCanonicalId(id(entry)) || _unlockable.Contains(id(entry)))
                    {
                        continue;
                    }

                    if (UnlockConditionCanHold(unlock(entry), produced, purchasable))
                    {
                        _unlockable.Add(id(entry));
                        added = true;
                    }
                }

                return added;
            }

            private bool UnlockConditionCanHold(RunUnlockDto unlock, HashSet<string> produced, HashSet<string> purchasable)
            {
                if (unlock == null)
                {
                    return true;
                }

                switch (unlock.Kind)
                {
                    case "researchPurchased":
                        return !IsCanonicalId(unlock.Id) || !_research.ContainsKey(unlock.Id) || purchasable.Contains(unlock.Id);
                    case "resourceProduced":
                        return !IsCanonicalId(unlock.Id) || !_resourceIds.Contains(unlock.Id) || produced.Contains(unlock.Id);
                    default:
                        return true;
                }
            }

            private bool ProduceResources(HashSet<string> produced)
            {
                var added = false;
                if (_doc.Machines == null)
                {
                    return false;
                }

                foreach (var machine in _doc.Machines)
                {
                    if (machine == null || !IsCanonicalId(machine.Id) || !_unlockable.Contains(machine.Id) ||
                        !IsCanonicalId(machine.RecipeId) || !_recipes.TryGetValue(machine.RecipeId, out var recipe))
                    {
                        continue;
                    }

                    if (!RecipeCanRun(recipe, produced))
                    {
                        continue;
                    }

                    if (recipe.Outputs != null)
                    {
                        foreach (var output in recipe.Outputs)
                        {
                            if (output != null && IsCanonicalId(output.ResourceId) && produced.Add(output.ResourceId))
                            {
                                added = true;
                            }
                        }
                    }
                }

                return added;
            }

            private bool RecipeCanRun(RunRecipeDto recipe, HashSet<string> produced)
            {
                if (recipe.Inputs != null)
                {
                    foreach (var input in recipe.Inputs)
                    {
                        if (input == null || !IsCanonicalId(input.ResourceId) || !_unlockable.Contains(input.ResourceId) ||
                            !produced.Contains(input.ResourceId))
                        {
                            return false;
                        }
                    }
                }

                if (recipe.Outputs != null)
                {
                    foreach (var output in recipe.Outputs)
                    {
                        if (output == null || !IsCanonicalId(output.ResourceId) || !_unlockable.Contains(output.ResourceId))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            private bool PurchaseResearch(HashSet<string> produced, HashSet<string> purchasable)
            {
                if (_doc.Research == null || !RpIsObtainable(produced))
                {
                    return false;
                }

                var added = false;
                foreach (var research in _doc.Research)
                {
                    if (research == null || !IsCanonicalId(research.Id) || purchasable.Contains(research.Id))
                    {
                        continue;
                    }

                    var open = true;
                    if (research.Prerequisites != null)
                    {
                        foreach (var prerequisite in research.Prerequisites)
                        {
                            if (prerequisite != null && IsCanonicalId(prerequisite.Id) && _research.ContainsKey(prerequisite.Id) &&
                                !purchasable.Contains(prerequisite.Id))
                            {
                                open = false;
                                break;
                            }
                        }
                    }

                    if (open)
                    {
                        purchasable.Add(research.Id);
                        added = true;
                    }
                }

                return added;
            }

            /// <summary>RP only comes from a contract's <c>rpReward</c>, so some unlockable offer for a producible resource must pay it (a curve base above 0).</summary>
            private bool RpIsObtainable(HashSet<string> produced)
            {
                if (_doc.Contracts == null)
                {
                    return false;
                }

                foreach (var contract in _doc.Contracts)
                {
                    if (contract == null || contract.RpReward == null || contract.RpReward.Base <= 0 || !IsCanonicalId(contract.Id) ||
                        !_unlockable.Contains(contract.Id) || !IsCanonicalId(contract.ResourceId) || !produced.Contains(contract.ResourceId))
                    {
                        continue;
                    }

                    return true;
                }

                return false;
            }

            private void CheckReservedId(string id, string path, string entryId)
            {
                if (string.Equals(id, RunState.OverdriveUnlockId, StringComparison.Ordinal))
                {
                    Add(
                        "id.reserved",
                        path,
                        entryId,
                        $"Identifier '{RunState.OverdriveUnlockId}' is reserved for the Overdrive unlock.");
                }
            }

            // ---- run start ------------------------------------------------------------------------------

            private void ValidateRunStart()
            {
                var runStart = _doc.RunStart;
                if (runStart == null)
                {
                    Add("value.null", "/runStart", null, "runStart is missing.");
                    return;
                }

                const string path = "/runStart";
                if (!ExactBound.Contains(runStart.StartingCash))
                {
                    Add("value.out-of-range", path + "/startingCash", null, "startingCash must be 0 to 10^18.");
                }

                if (runStart.InitialUnlocks != null)
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    for (var index = 0; index < runStart.InitialUnlocks.Count; index++)
                    {
                        var id = runStart.InitialUnlocks[index];
                        var idPath = Pointer(path + "/initialUnlocks", index);
                        CheckIdentity(id, idPath, null, seen);
                        if (IsCanonicalId(id) && !_resourceIds.Contains(id) && !_machineIds.Contains(id) &&
                            !_contractIds.Contains(id) && !_research.ContainsKey(id) && !_itemIds.Contains(id))
                        {
                            Add("runstart.unlock.unknown", idPath, null, $"Unknown initial unlock '{Clip(id)}'.");
                        }
                    }
                }

                if (runStart.RemovalRefundPpm < 0 || runStart.RemovalRefundPpm > 999_999)
                {
                    Add("runstart.refund.out-of-range", path + "/removalRefundPpm", null, "removalRefundPpm must be 0 to 999,999.");
                }

                _minCycleOk = runStart.MinCycleTicks >= 1_000_000 && runStart.MinCycleTicks <= UpperTimeBound;
                _minCycle = runStart.MinCycleTicks;
                if (!_minCycleOk)
                {
                    Add("runstart.min-cycle.invalid", path + "/minCycleTicks", null, "minCycleTicks must be 1,000,000 to 10^12 ticks.");
                }

                _speedBoundOk = runStart.MaxCombinedSpeedPpm >= Ppm && runStart.MaxCombinedSpeedPpm <= UpperTimeBound;
                _speedBound = runStart.MaxCombinedSpeedPpm;
                if (!_speedBoundOk)
                {
                    Add("runstart.speed-bound.invalid", path + "/maxCombinedSpeedPpm", null, "maxCombinedSpeedPpm must be 10^6 to 10^12.");
                }

                _outputBoundOk = runStart.MaxCombinedOutputFactor >= 2 && runStart.MaxCombinedOutputFactor <= MaxOutputFactorBound;
                _outputBound = runStart.MaxCombinedOutputFactor;
                if (!_outputBoundOk)
                {
                    Add("runstart.output-bound.invalid", path + "/maxCombinedOutputFactor", null, "maxCombinedOutputFactor must be 2 to 1,000,000.");
                }

                _ratioBoundOk = runStart.MaxRewardRatioPpm >= MillionPpm && runStart.MaxRewardRatioPpm <= ExactBound.Value;
                _ratioBound = runStart.MaxRewardRatioPpm;
                if (!_ratioBoundOk)
                {
                    Add("runstart.reward-ratio.invalid", path + "/maxRewardRatioPpm", null, "maxRewardRatioPpm must be at least 10^6.");
                }

                ValidateRunStartV3(runStart, path);
            }

            // Schema v3 run-start members: sell.steps and live-gap.range (T314), the exact range of the remaining numbers, the contract's plain
            // member ranges and the manual-mining resource reference. start.reachable and manual.yield.bound belong to the manual-mining task.
            private void ValidateRunStartV3(RunStartDto runStart, string path)
            {
                ValidateSellSteps(runStart.SellSteps, path + "/sellSteps");
                CheckRange(
                    runStart.LiveGapThresholdMs,
                    LiveGapMinMs,
                    LiveGapMaxMs,
                    path + "/liveGapThresholdMs",
                    null,
                    "liveGapThresholdMs must be 1,000 to 28,800,000 (8 hours).",
                    "live-gap.range");
                CheckRange(runStart.MinRunDurationMs, 0, ExactBound.Value, path + "/minRunDurationMs", null, "minRunDurationMs must be 0 to 10^18.");
                CheckRange(runStart.UptimeMaxPpm, 0, ExactBound.Value, path + "/uptimeMaxPpm", null, "uptimeMaxPpm must be 0 to 10^18.");

                var mining = runStart.ManualMining;
                if (mining == null)
                {
                    Add("value.null", path + "/manualMining", null, "manualMining is missing.");
                    return;
                }

                var miningPath = path + "/manualMining";
                CheckIdentity(mining.ResourceId, miningPath + "/resourceId", null, null);
                if (IsCanonicalId(mining.ResourceId) && !_resourceIds.Contains(mining.ResourceId))
                {
                    Add("ref.resource.unknown", miningPath + "/resourceId", null, $"Unknown resource '{Clip(mining.ResourceId)}'.");
                }

                CheckRange(mining.BaseYieldMilli, 1, ExactBound.Value, miningPath + "/baseYieldMilli", null, "baseYieldMilli must be 1 to 10^18.");
                CheckRange(mining.MaxActivationsPerSecond, 1, 20, miningPath + "/maxActivationsPerSecond", null, "maxActivationsPerSecond must be 1 to 20.");
                CheckRange(mining.StartWindowMs, 0, ExactBound.Value, miningPath + "/startWindowMs", null, "startWindowMs must be 0 to 10^18.");
                CheckRange(mining.ReferenceMinerLevel, 1, ExactBound.Value, miningPath + "/referenceMinerLevel", null, "referenceMinerLevel must be 1 to 10^18.");
                CheckRange(mining.MaxShareOfMinerPpm, 0, 3_000_000, miningPath + "/maxShareOfMinerPpm", null, "maxShareOfMinerPpm must be 0 to 3,000,000.");
                CheckRange(mining.MiningAdvantageMaxPpm, 0, ExactBound.Value, miningPath + "/miningAdvantageMaxPpm", null, "miningAdvantageMaxPpm must be 0 to 10^18.");
            }

            // sell.steps: 1 to 8 distinct, ascending, positive integers (the optional trailing "all" is carried by SellAll). The reader's cap of 64
            // integers stays; a list over 8 is this rule, never value.out-of-range.
            private void ValidateSellSteps(List<long> steps, string path)
            {
                if (steps == null)
                {
                    Add("sell.steps", path, null, "sellSteps must list 1 to 8 integers.");
                    return;
                }

                if (steps.Count < 1 || steps.Count > MaxSellSteps)
                {
                    Add("sell.steps", path, null, "sellSteps must list 1 to 8 integers.");
                }

                var previous = 0L;
                for (var index = 0; index < steps.Count; index++)
                {
                    var step = steps[index];
                    var stepPath = Pointer(path, index);
                    if (step < 1 || step > ExactBound.Value)
                    {
                        Add("sell.steps", stepPath, null, "A sell step must be 1 to 10^18.");
                        continue;
                    }

                    if (step <= previous)
                    {
                        Add("sell.steps", stepPath, null, "Sell steps must be distinct and strictly ascending.");
                    }

                    previous = step;
                }
            }

            // ---- resources and recipes ------------------------------------------------------------------

            private void ValidateResources()
            {
                if (_doc.Resources == null)
                {
                    return;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < _doc.Resources.Count; index++)
                {
                    var resource = _doc.Resources[index];
                    var path = Pointer("/resources", index);
                    if (resource == null)
                    {
                        Add("value.null", path, null, "Resource entry is missing.");
                        continue;
                    }

                    var entryId = KnownEntryId(resource.Id);
                    CheckIdentity(resource.Id, path + "/id", null, seen);
                    CheckReservedId(resource.Id, path + "/id", entryId);
                    CheckPresentationKey(resource.PresentationKey, path + "/presentationKey", entryId);

                    if (resource.BasePrice < 0)
                    {
                        Add("resource.price.negative", path + "/basePrice", entryId, "basePrice must not be negative.");
                    }
                    else if (resource.BasePrice > ExactBound.Value)
                    {
                        Add("value.out-of-range", path + "/basePrice", entryId, "basePrice must not exceed 10^18.");
                    }

                    ValidateUnlock(resource.Unlock, path + "/unlock", entryId, resource.Id);
                }
            }

            private void ValidateRecipes()
            {
                if (_doc.Recipes == null)
                {
                    return;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < _doc.Recipes.Count; index++)
                {
                    var recipe = _doc.Recipes[index];
                    var path = Pointer("/recipes", index);
                    if (recipe == null)
                    {
                        Add("value.null", path, null, "Recipe entry is missing.");
                        continue;
                    }

                    var entryId = KnownEntryId(recipe.Id);
                    CheckIdentity(recipe.Id, path + "/id", null, seen);
                    ValidateAmounts(recipe.Inputs, path + "/inputs", entryId, false);
                    ValidateAmounts(recipe.Outputs, path + "/outputs", entryId, true);

                    if (recipe.DurationTicks <= 0)
                    {
                        Add("recipe.duration.non-positive", path + "/durationTicks", entryId, "durationTicks must be positive.");
                    }
                    else if (recipe.DurationTicks > UpperTimeBound)
                    {
                        Add("recipe.duration.out-of-range", path + "/durationTicks", entryId, "durationTicks must not exceed 10^12 ticks.");
                    }
                }
            }

            private void ValidateAmounts(List<RunAmountDto> amounts, string path, string entryId, bool requireNonEmpty)
            {
                if (amounts == null || amounts.Count == 0)
                {
                    if (requireNonEmpty)
                    {
                        Add("recipe.outputs.empty", path, entryId, "A recipe needs at least one output.");
                    }

                    return;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < amounts.Count; index++)
                {
                    var amount = amounts[index];
                    var amountPath = Pointer(path, index);
                    if (amount == null)
                    {
                        Add("value.null", amountPath, entryId, "Resource amount entry is missing.");
                        continue;
                    }

                    CheckIdentity(amount.ResourceId, amountPath + "/resourceId", entryId, null);
                    var key = NormalizedId(amount.ResourceId);
                    if (key != null && !seen.Add(key))
                    {
                        Add("recipe.resource.duplicate", amountPath + "/resourceId", entryId, $"Recipe side repeats resource '{Clip(amount.ResourceId)}'.");
                    }

                    if (IsCanonicalId(amount.ResourceId) && !_resourceIds.Contains(amount.ResourceId))
                    {
                        Add("ref.resource.unknown", amountPath + "/resourceId", entryId, $"Unknown resource '{Clip(amount.ResourceId)}'.");
                    }

                    if (amount.Amount <= 0)
                    {
                        Add("recipe.amount.non-positive", amountPath + "/amount", entryId, "Recipe amounts must be positive.");
                    }
                    else if (amount.Amount > ExactBound.Value)
                    {
                        Add("value.out-of-range", amountPath + "/amount", entryId, "Recipe amounts must not exceed 10^18.");
                    }
                }
            }

            // ---- machines -------------------------------------------------------------------------------

            private void ValidateMachines()
            {
                if (_doc.Machines == null)
                {
                    return;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < _doc.Machines.Count; index++)
                {
                    var machine = _doc.Machines[index];
                    var path = Pointer("/machines", index);
                    if (machine == null)
                    {
                        Add("value.null", path, null, "Machine entry is missing.");
                        continue;
                    }

                    var entryId = KnownEntryId(machine.Id);
                    CheckIdentity(machine.Id, path + "/id", null, seen);
                    CheckReservedId(machine.Id, path + "/id", entryId);
                    CheckPresentationKey(machine.PresentationKey, path + "/presentationKey", entryId);

                    RunRecipeDto recipe = null;
                    CheckIdentity(machine.RecipeId, path + "/recipeId", entryId, null);
                    if (IsCanonicalId(machine.RecipeId) && !_recipes.TryGetValue(machine.RecipeId, out recipe))
                    {
                        Add("ref.recipe.unknown", path + "/recipeId", entryId, $"Unknown recipe '{Clip(machine.RecipeId)}'.");
                    }

                    var maxLevelOk = machine.MaxLevel >= 1 && machine.MaxLevel <= MaxDefinitionLevel;
                    if (!maxLevelOk)
                    {
                        Add("machine.max-level.invalid", path + "/maxLevel", entryId, "maxLevel must be 1 to 1,024.");
                    }

                    EvaluateCurve(machine.Cost, path + "/cost", entryId, maxLevelOk ? machine.MaxLevel : 1);
                    ValidateBuildPrice(machine.BuildPrice, path + "/buildPrice", entryId);
                    ValidateSpeed(machine, path, entryId, maxLevelOk, recipe);
                    ValidateUnlock(machine.Unlock, path + "/unlock", entryId, machine.Id);
                }
            }

            // Schema v3 build price: build.base.bound (base and cap at most 10^12, T314), the growth floors and the Core-safety ranges. build.price.cap
            // (cap >= base) belongs to the build-pricing task.
            private void ValidateBuildPrice(RunBuildPriceDto price, string path, string entryId)
            {
                if (price == null)
                {
                    Add("value.null", path, entryId, "buildPrice is missing.");
                    return;
                }

                CheckBuildBound(price.Base, path + "/base", entryId, "base");
                CheckRange(price.OwnedGrowthPpm, Ppm, ExactBound.Value, path + "/ownedGrowthPpm", entryId, "ownedGrowthPpm must be 10^6 to 10^18.");
                CheckRange(price.PrestigeGrowthPpm, Ppm, ExactBound.Value, path + "/prestigeGrowthPpm", entryId, "prestigeGrowthPpm must be 10^6 to 10^18.");
                CheckBuildBound(price.Cap, path + "/cap", entryId, "cap");
            }

            // A negative value is a plain range error; anything above 10^12 (including values above the exact range) is build.base.bound.
            private void CheckBuildBound(long value, string path, string entryId, string member)
            {
                if (value < 0)
                {
                    Add("value.out-of-range", path, entryId, member + " must not be negative.");
                }
                else if (value > BuildBound)
                {
                    Add("build.base.bound", path, entryId, member + " must not exceed 10^12.");
                }
            }

            private void ValidateSpeed(RunMachineDto machine, string path, string entryId, bool maxLevelOk, RunRecipeDto recipe)
            {
                var growthOk = true;
                if (machine.SpeedGrowthPpm > ExactBound.Value)
                {
                    growthOk = false;
                    Add("value.out-of-range", path + "/speedGrowthPpm", entryId, "speedGrowthPpm must not exceed 10^18.");
                }
                else if (machine.SpeedGrowthPpm <= Ppm)
                {
                    growthOk = false;
                    Add("speed.level.dead", path + "/speedGrowthPpm", entryId, "speedGrowthPpm must exceed 10^6 so that SpeedTable strictly increases.");
                }

                var milestonesOk = true;
                var valid = new List<RunMilestoneDto>();
                if (machine.Milestones != null)
                {
                    var levels = new HashSet<int>();
                    for (var index = 0; index < machine.Milestones.Count; index++)
                    {
                        var milestone = machine.Milestones[index];
                        var milestonePath = Pointer(path + "/milestones", index);
                        if (milestone == null)
                        {
                            milestonesOk = false;
                            Add("value.null", milestonePath, entryId, "Milestone entry is missing.");
                            continue;
                        }

                        var entryOk = true;
                        if (milestone.Level < 2 || (maxLevelOk && milestone.Level > machine.MaxLevel))
                        {
                            entryOk = false;
                            Add("machine.milestone.level-invalid", milestonePath + "/level", entryId, "A milestone level must be 2 to maxLevel.");
                        }
                        else if (!levels.Add(milestone.Level))
                        {
                            entryOk = false;
                            Add("machine.milestone.duplicate", milestonePath + "/level", entryId, "Two milestones share a level.");
                        }

                        if (milestone.SpeedPpm > ExactBound.Value)
                        {
                            entryOk = false;
                            Add("value.out-of-range", milestonePath + "/speedPpm", entryId, "A milestone speedPpm must not exceed 10^18.");
                        }
                        else if (milestone.SpeedPpm <= Ppm)
                        {
                            entryOk = false;
                            Add("speed.level.dead", milestonePath + "/speedPpm", entryId, "A milestone speedPpm must exceed 10^6, otherwise the milestone is dead.");
                        }

                        if (entryOk)
                        {
                            valid.Add(milestone);
                        }
                        else
                        {
                            milestonesOk = false;
                        }
                    }
                }

                if (!maxLevelOk || !growthOk || !milestonesOk)
                {
                    return;
                }

                valid.Sort((a, b) => a.Level.CompareTo(b.Level));
                var speeds = new long[machine.MaxLevel + 1];
                speeds[1] = Ppm;
                var baseSpeed = Ppm;
                for (var level = 2; level <= machine.MaxLevel; level++)
                {
                    if (!MulDiv.MulDivFloor(baseSpeed, machine.SpeedGrowthPpm, Ppm, out baseSpeed))
                    {
                        Add("curve.overflow", path + "/speedGrowthPpm", entryId, $"SpeedTable exceeds 10^18 at level {level}.");
                        return;
                    }

                    var speed = baseSpeed;
                    for (var i = 0; i < valid.Count && valid[i].Level <= level; i++)
                    {
                        if (!MulDiv.MulDivFloor(speed, valid[i].SpeedPpm, Ppm, out speed))
                        {
                            Add("curve.overflow", path + "/milestones", entryId, $"SpeedTable exceeds 10^18 at level {level}.");
                            return;
                        }
                    }

                    speeds[level] = speed;
                    if (speeds[level] <= speeds[level - 1])
                    {
                        Add("speed.level.dead", path, entryId, $"SpeedTable does not strictly increase at level {level}.");
                        return;
                    }
                }

                if (!_speedBoundOk)
                {
                    return;
                }

                var worstSpeed = WorstCaseSpeed(machine.Id, speeds[machine.MaxLevel]);
                if (worstSpeed < 0 || worstSpeed > _speedBound)
                {
                    Add(
                        "speed.level.dead",
                        path,
                        entryId,
                        "The worst-case composed speed with every speed source at its maximum exceeds maxCombinedSpeedPpm.");
                    return;
                }

                if (recipe != null)
                {
                    CheckBatch(recipe, worstSpeed, path, entryId);
                }
            }

            /// <summary>Worst-case composed speed of a machine at its maximum level, or -1 when the composition overflows.</summary>
            private long WorstCaseSpeed(string machineId, long maxLevelSpeed)
            {
                var research = new List<SpeedFactor>();
                foreach (var source in _researchSpeed)
                {
                    if (source.TargetKind == "allMachines" || source.TargetId == machineId)
                    {
                        research.Add(new SpeedFactor(source.Ppm, source.Levels));
                    }
                }

                var industry = new List<SpeedFactor>();
                foreach (var source in _industrySpeed)
                {
                    if (source.TargetKind == "allMachines" || source.TargetId == machineId)
                    {
                        if (source.Table == null)
                        {
                            industry.Add(new SpeedFactor(source.Ppm, source.Levels));
                        }
                        else
                        {
                            // A decaying Industry source folds one factor per level, in level order (the same floor per level as the runtime).
                            foreach (var factor in source.Table)
                            {
                                industry.Add(new SpeedFactor(factor, 1));
                            }
                        }
                    }
                }

                // Items stack across distinct conflict keys (effect kind, target); the best Item wins per key.
                var perKey = new SortedDictionary<string, long>(StringComparer.Ordinal);
                if (_doc.Items != null)
                {
                    foreach (var item in _doc.Items)
                    {
                        if (item == null || item.Effect == null || item.Target == null || item.Effect.Kind != "machineSpeed" ||
                            item.Effect.Value <= Ppm || item.Effect.Value > ExactBound.Value)
                        {
                            continue;
                        }

                        string key = null;
                        if (item.Target.Kind == "factory")
                        {
                            key = "factory";
                        }
                        else if (item.Target.Kind == "machineType" && item.Target.AllowedMachineIds != null &&
                                 item.Target.AllowedMachineIds.Contains(machineId))
                        {
                            var sorted = new List<string>(item.Target.AllowedMachineIds);
                            sorted.Sort(StringComparer.Ordinal);
                            key = "type:" + string.Join("\n", sorted);
                        }

                        if (key != null && (!perKey.TryGetValue(key, out var best) || item.Effect.Value > best))
                        {
                            perKey[key] = item.Effect.Value;
                        }
                    }
                }

                var itemFactors = new List<long>(perKey.Values);
                if (!SpeedComposer.TryCompose(
                        maxLevelSpeed,
                        research,
                        industry,
                        itemFactors,
                        _overdriveSpeedOk ? _overdriveSpeed : Ppm,
                        ExactBound.Value,
                        out var composed))
                {
                    return -1;
                }

                return composed;
            }

            private void CheckBatch(RunRecipeDto recipe, long worstSpeed, string path, string entryId)
            {
                if (!_minCycleOk || !_outputBoundOk || recipe.DurationTicks < 1 || recipe.DurationTicks > UpperTimeBound || worstSpeed < 1)
                {
                    return;
                }

                if (!SpeedComposer.TryBatch(recipe.DurationTicks, worstSpeed, _minCycle, out var batch, out _))
                {
                    Add("batch.overflow", path, entryId, "The worst-case batch factor exceeds 10^18.");
                    return;
                }

                if (!BatchFits(recipe.Inputs, batch) || !BatchFits(recipe.Outputs, batch))
                {
                    Add("batch.overflow", path, entryId, "The worst-case batch amount (batch x amount x maxCombinedOutputFactor) exceeds 10^18.");
                }
            }

            private bool BatchFits(List<RunAmountDto> amounts, long batch)
            {
                if (amounts == null)
                {
                    return true;
                }

                foreach (var amount in amounts)
                {
                    if (amount == null || amount.Amount < 1 || amount.Amount > ExactBound.Value)
                    {
                        continue;
                    }

                    if (!MulDiv.MulDivFloor(batch, amount.Amount, 1, out var units) ||
                        !MulDiv.MulDivFloor(units, _outputBound, 1, out _))
                    {
                        return false;
                    }
                }

                return true;
            }

            // ---- expansion tables ------------------------------------------------------------------------

            private void ValidateStorageLevels()
            {
                if (_doc.StorageLevels == null)
                {
                    return;
                }

                var sequenceReported = false;
                long previous = 0;
                var hasPrevious = false;
                for (var index = 0; index < _doc.StorageLevels.Count; index++)
                {
                    var level = _doc.StorageLevels[index];
                    var path = Pointer("/storageLevels", index);
                    if (level == null)
                    {
                        Add("value.null", path, null, "Storage level entry is missing.");
                        hasPrevious = false;
                        continue;
                    }

                    if (!sequenceReported && level.Level != index + 1)
                    {
                        sequenceReported = true;
                        Add("level.sequence.invalid", path + "/level", null, "Levels must start at 1 and be contiguous.");
                    }

                    if (index == 0 && level.Cost != 0)
                    {
                        Add("level.first-cost.non-zero", path + "/cost", null, "Level 1 must cost 0.");
                    }
                    else if (level.Cost < 0 || level.Cost > ExactBound.Value)
                    {
                        Add("value.out-of-range", path + "/cost", null, "cost must be 0 to 10^18.");
                    }

                    if (level.Capacity < 1 || level.Capacity > ExactBound.Value)
                    {
                        Add("value.out-of-range", path + "/capacity", null, "capacity must be 1 to 10^18.");
                        hasPrevious = false;
                        continue;
                    }

                    if (hasPrevious && level.Capacity <= previous)
                    {
                        Add("storage.capacity.non-increasing", path + "/capacity", null, "Storage capacity must strictly increase.");
                    }

                    previous = level.Capacity;
                    hasPrevious = true;
                }
            }

            private void ValidateFactoryLevels()
            {
                if (_doc.FactoryLevels == null)
                {
                    return;
                }

                var sequenceReported = false;
                long previous = 0;
                var hasPrevious = false;
                for (var index = 0; index < _doc.FactoryLevels.Count; index++)
                {
                    var level = _doc.FactoryLevels[index];
                    var path = Pointer("/factoryLevels", index);
                    if (level == null)
                    {
                        Add("value.null", path, null, "Factory level entry is missing.");
                        hasPrevious = false;
                        continue;
                    }

                    if (!sequenceReported && level.Level != index + 1)
                    {
                        sequenceReported = true;
                        Add("level.sequence.invalid", path + "/level", null, "Levels must start at 1 and be contiguous.");
                    }

                    if (index == 0 && level.Cost != 0)
                    {
                        Add("level.first-cost.non-zero", path + "/cost", null, "Level 1 must cost 0.");
                    }
                    else if (level.Cost < 0 || level.Cost > ExactBound.Value)
                    {
                        Add("value.out-of-range", path + "/cost", null, "cost must be 0 to 10^18.");
                    }

                    if (level.Slots > MaxFactorySlots)
                    {
                        Add("factory.slots.too-many", path + "/slots", null, "A factory has at most 12 slots.");
                    }
                    else if (level.Slots < 1)
                    {
                        Add("value.out-of-range", path + "/slots", null, "slots must be at least 1.");
                        hasPrevious = false;
                        continue;
                    }

                    if (hasPrevious && level.Slots <= previous)
                    {
                        Add("factory.slots.non-increasing", path + "/slots", null, "Factory slots must strictly increase.");
                    }

                    previous = level.Slots;
                    hasPrevious = true;
                }
            }

            // ---- contracts --------------------------------------------------------------------------------

            private void ValidateContracts()
            {
                if (_doc.Contracts == null)
                {
                    return;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < _doc.Contracts.Count; index++)
                {
                    var contract = _doc.Contracts[index];
                    var path = Pointer("/contracts", index);
                    if (contract == null)
                    {
                        Add("value.null", path, null, "Contract entry is missing.");
                        continue;
                    }

                    var entryId = KnownEntryId(contract.Id);
                    CheckIdentity(contract.Id, path + "/id", null, seen);
                    CheckReservedId(contract.Id, path + "/id", entryId);

                    // The vocabularies are checked here, before RunCatalogBuilder constructs a ContractOfferDef, whose constructor only guards them.
                    if (Array.IndexOf(RunContentReader.ContractObjectives, contract.Objective) < 0)
                    {
                        Add("contract.objective.unknown", path + "/objective", entryId, $"Unknown contract objective '{Clip(contract.Objective)}'.");
                    }

                    if (Array.IndexOf(RunContentReader.ContractClasses, contract.Class) < 0)
                    {
                        Add("contract.class.unknown", path + "/class", entryId, $"Unknown contract class '{Clip(contract.Class)}'.");
                    }

                    var classKnown = Array.IndexOf(RunContentReader.ContractClasses, contract.Class) >= 0;
                    if (classKnown && (contract.Bonus != null) != (contract.Class == "bonus"))
                    {
                        Add(
                            "contract.bonus.fields",
                            path + "/bonus",
                            entryId,
                            contract.Class == "bonus" ? "A bonus offer needs a bonus member." : "Only a bonus offer may carry a bonus member.");
                    }

                    RunResourceDto resource = null;
                    CheckIdentity(contract.ResourceId, path + "/resourceId", entryId, null);
                    if (IsCanonicalId(contract.ResourceId) && !_resources.TryGetValue(contract.ResourceId, out resource))
                    {
                        Add("ref.resource.unknown", path + "/resourceId", entryId, $"Unknown resource '{Clip(contract.ResourceId)}'.");
                    }

                    if (contract.DurationMs.HasValue && (contract.DurationMs.Value < 1 || contract.DurationMs.Value > RunLimits.MaxTimedDurationMs))
                    {
                        Add("contract.duration.out-of-range", path + "/durationMs", entryId, "durationMs must be 1 to seven days (604800000).");
                    }

                    var quantity = EvaluateCurve(contract.Quantity, path + "/quantity", entryId, ContractTableEntries);
                    var ratioReported = ValidateRewardRatio(contract, path, entryId, resource, quantity);
                    if (!ratioReported && _doc.ContentRevision >= RatioRulesRevision)
                    {
                        ValidateRatioRanges(contract, path, entryId);
                    }

                    ValidateQuantityRules(contract, path, entryId, quantity);
                    CheckRange(contract.CooldownMs, 0, ExactBound.Value, path + "/cooldownMs", entryId, "cooldownMs must be 0 to 10^18.");
                    EvaluateCurve(contract.RpReward, path + "/rpReward", entryId, ContractTableEntries);
                    if (contract.RpReward != null && contract.Quantity != null && contract.RpReward.GrowthPpm > contract.Quantity.GrowthPpm &&
                        contract.RpReward.GrowthPpm <= ExactBound.Value)
                    {
                        Add(
                            "contract.rp.growth",
                            path + "/rpReward/growthPpm",
                            entryId,
                            "An RP reward curve may not grow faster than the contract quantity curve.");
                    }

                    ValidateBonus(contract.Bonus, path + "/bonus", entryId);
                    ValidateUnlock(contract.Unlock, path + "/unlock", entryId, contract.Id);
                }
            }

            // Bonus trigger and window. The trigger takes the full unlock validation: its shape, then reachability (contract.bonus.fields, whether the
            // member is present at all, is checked with the offer).
            private void ValidateBonus(RunBonusDto bonus, string path, string entryId)
            {
                if (bonus == null)
                {
                    return;
                }

                var before = _totalAdded;
                ValidateUnlockShape(bonus.Trigger, path + "/trigger", entryId);
                if (before == _totalAdded && !UnlockConditionCanHold(bonus.Trigger, _producedFinal, _purchasableFinal))
                {
                    Add(
                        "unlock.unreachable",
                        path + "/trigger",
                        entryId,
                        "This trigger can never be satisfied: following the unlock conditions from runStart.initialUnlocks reaches a dead end or a cycle.");
                }

                CheckRange(bonus.WindowMs, 1, ExactBound.Value, path + "/windowMs", entryId, "windowMs must be 1 to 10^18.");
            }

            /// <summary>
            /// The transitional Cash reward (<see cref="RunRewardAdapter"/>) must fit the exact range at every completion index, and a delivery
            /// offer pays between the market value of the required units and <c>maxRewardRatioPpm</c> times it (the v2 rule
            /// <c>contract.reward.ratio</c>, expressed on the ratio). The v3 ratio rules <c>contract.ratio.*</c> are T314.
            /// </summary>
            private bool ValidateRewardRatio(RunContractDto contract, string path, string entryId, RunResourceDto resource, long[] quantity)
            {
                var ratioPath = path + "/rewardRatioPpm";
                if (contract.RewardRatioPpm < 0 || contract.RewardRatioPpm > ExactBound.Value)
                {
                    Add("value.out-of-range", ratioPath, entryId, "rewardRatioPpm must be 0 to 10^18.");
                    return true;
                }

                if (contract.Objective == "deliver")
                {
                    if (contract.RewardRatioPpm < MillionPpm)
                    {
                        Add("contract.reward.ratio", ratioPath, entryId, "The Cash reward is below the market value of the required units.");
                        return true;
                    }

                    if (_ratioBoundOk && contract.RewardRatioPpm > _ratioBound)
                    {
                        Add("contract.reward.ratio", ratioPath, entryId, "The Cash reward exceeds maxRewardRatioPpm times the market value.");
                        return true;
                    }
                }

                // A price outside 0 to 10^18 is reported by resource.price.negative / value.out-of-range, not twice.
                if (quantity != null && resource != null && resource.BasePrice >= 0 && resource.BasePrice <= ExactBound.Value &&
                    !RunRewardAdapter.TryCashTable(quantity, resource.BasePrice, contract.RewardRatioPpm, out _))
                {
                    Add("contract.reward.ratio", ratioPath, entryId, "The Cash reward exceeds 10^18 at some completion index.");
                    return true;
                }

                return false;
            }

            /// <summary>
            /// The v3 ratio ranges (<c>contentRevision</c> 3 and later), layered on top of <see cref="ValidateRewardRatio"/>, which stays ungated
            /// as the floor, ceiling and overflow safety layer (revision 2 content keeps its ratios of 1.67 to 1.75). They run only for an offer the
            /// v2 check did not already report, so one ratio is never reported twice: <c>contract.ratio.standing</c> is the 1.5 ceiling of a
            /// standing delivery offer (its floor of 1.0 is the v2 floor) and <c>contract.ratio.bonus</c> the 2.0 ceiling of a bonus offer.
            /// <c>contract.ratio.produce</c> belongs to T320.
            /// </summary>
            private void ValidateRatioRanges(RunContractDto contract, string path, string entryId)
            {
                var ratioPath = path + "/rewardRatioPpm";
                if (contract.Class == "standing" && contract.Objective == "deliver" && contract.RewardRatioPpm > StandingRatioMaxPpm)
                {
                    Add("contract.ratio.standing", ratioPath, entryId, "A standing delivery offer pays at most 1.5 times the market value.");
                }
                else if (contract.Class == "bonus" && contract.RewardRatioPpm > BonusRatioMaxPpm)
                {
                    Add("contract.ratio.bonus", ratioPath, entryId, "A bonus offer pays at most 2.0 times the market value.");
                }
            }

            /// <summary>
            /// <c>contract.quantity.cap</c>: the catalog has no cap member, so the check is on the quantity curve: the smallest requirement (index 0)
            /// of a delivery offer may not exceed the top storage capacity, or no storage level could ever hold it. (The requirement at every
            /// reachable index is <c>contract.quantity.capacity</c>, T320.) <c>contract.produce.min</c>: a production offer asks for at least
            /// <see cref="ProduceMinQuantity"/> units at index 0, so that trivial offers cannot farm early Items (content constant).
            /// </summary>
            private void ValidateQuantityRules(RunContractDto contract, string path, string entryId, long[] quantity)
            {
                if (quantity == null || quantity.Length == 0)
                {
                    return;
                }

                if (contract.Objective == "deliver")
                {
                    var top = TopStorageCapacity();
                    if (top > 0 && quantity[0] > top)
                    {
                        Add("contract.quantity.cap", path + "/quantity/base", entryId, "A delivery offer may not require more than the top storage capacity.");
                    }
                }
                else if (contract.Objective == "produce" && quantity[0] < ProduceMinQuantity)
                {
                    Add("contract.produce.min", path + "/quantity/base", entryId, "A production offer must require at least 10 units.");
                }
            }

            private long TopStorageCapacity()
            {
                long top = 0;
                if (_doc.StorageLevels != null)
                {
                    foreach (var level in _doc.StorageLevels)
                    {
                        if (level != null && level.Capacity > top && level.Capacity <= ExactBound.Value)
                        {
                            top = level.Capacity;
                        }
                    }
                }

                return top;
            }

            // ---- contract Item ladder ---------------------------------------------------------------------------

            // Reference and Core-safety checks only; ladder.uptime belongs to the contracts task. The ascending index order of the contract is a
            // property of the canonical form and the catalog (entries are a permutable family, FR-029/FR-030), not of the document order.
            private void ValidateContractItemLadder()
            {
                var ladder = _doc.ContractItemLadder;
                if (ladder == null)
                {
                    Add("value.null", "/contractItemLadder", null, "contractItemLadder is missing.");
                    return;
                }

                CheckRange(ladder.PerRunCap, 0, ExactBound.Value, "/contractItemLadder/perRunCap", null, "perRunCap must be 0 to 10^18.");
                if (ladder.Entries == null)
                {
                    Add("value.null", "/contractItemLadder/entries", null, "entries is missing.");
                    return;
                }

                if (ladder.Entries.Count > ContractTableEntries)
                {
                    Add("value.out-of-range", "/contractItemLadder/entries", null, "The ladder has at most 1,024 entries.");
                    return;
                }

                var pairs = new HashSet<string>(StringComparer.Ordinal);
                var perItem = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var index = 0; index < ladder.Entries.Count; index++)
                {
                    var entry = ladder.Entries[index];
                    var path = Pointer("/contractItemLadder/entries", index);
                    if (entry == null)
                    {
                        Add("value.null", path, null, "Ladder entry is missing.");
                        continue;
                    }

                    CheckIdentity(entry.ItemId, path + "/itemId", null, null);
                    var itemKnown = IsCanonicalId(entry.ItemId) && _itemIds.Contains(entry.ItemId);
                    if (IsCanonicalId(entry.ItemId) && !itemKnown)
                    {
                        Add("contract.reward.item.unknown", path + "/itemId", null, $"Unknown Item '{Clip(entry.ItemId)}'.");
                    }

                    if (entry.Index < 0 || entry.Index >= ContractTableEntries)
                    {
                        Add("value.out-of-range", path + "/index", null, "A ladder index must be 0 to 1,023.");
                    }
                    else if (itemKnown)
                    {
                        if (!pairs.Add(entry.Index.ToString(CultureInfo.InvariantCulture) + "|" + entry.ItemId))
                        {
                            Add("value.out-of-range", path + "/index", null, "An Item is listed twice at one ladder index.");
                        }

                        perItem.TryGetValue(entry.ItemId, out var count);
                        perItem[entry.ItemId] = count + 1;
                        if (count + 1 == 257)
                        {
                            Add("value.out-of-range", path, null, "An Item may appear in at most 256 ladder entries.");
                        }
                    }
                }
            }

            // ---- prestige milestones ------------------------------------------------------------------------

            // milestone.order: every fraction lies strictly inside (0, 10^6) and the list strictly ascends. A fraction outside the bounds is reported
            // once, without an ordering comparison.
            private void ValidateMilestones()
            {
                var milestones = _doc.Milestones;
                if (milestones == null)
                {
                    Add("value.null", "/milestones", null, "milestones is missing.");
                    return;
                }

                if (milestones.Count > 64)
                {
                    Add("value.out-of-range", "/milestones", null, "There are at most 64 milestones.");
                    return;
                }

                long previous = 0;
                for (var index = 0; index < milestones.Count; index++)
                {
                    var path = Pointer("/milestones", index);
                    if (milestones[index] == null)
                    {
                        Add("value.null", path, null, "Milestone entry is missing.");
                        continue;
                    }

                    var fraction = milestones[index].FractionPpm;
                    if (fraction <= 0 || fraction >= Ppm)
                    {
                        Add("milestone.order", path + "/fractionPpm", null, "fractionPpm must be strictly between 0 and 10^6.");
                        continue;
                    }

                    if (fraction <= previous)
                    {
                        Add("milestone.order", path + "/fractionPpm", null, "Milestones must strictly ascend.");
                    }

                    previous = fraction;
                }
            }

            // ---- items --------------------------------------------------------------------------------------

            private void ValidateItems()
            {
                if (_doc.Items == null)
                {
                    return;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var tiersUsable = new bool[4];
                for (var index = 0; index < _doc.Items.Count; index++)
                {
                    var item = _doc.Items[index];
                    var path = Pointer("/items", index);
                    if (item == null)
                    {
                        Add("value.null", path, null, "Item entry is missing.");
                        continue;
                    }

                    var before = _totalAdded;
                    var entryId = KnownEntryId(item.Id);
                    CheckIdentity(item.Id, path + "/id", null, seen);
                    CheckReservedId(item.Id, path + "/id", entryId);
                    CheckPresentationKey(item.PresentationKey, path + "/presentationKey", entryId);

                    var tierOk = item.Tier >= 1 && item.Tier <= 3;
                    if (!tierOk)
                    {
                        Add("item.tier.invalid", path + "/tier", entryId, "tier must be 1 to 3.");
                    }

                    if (item.DurationMs < 1)
                    {
                        Add("item.duration.non-positive", path + "/durationMs", entryId, "durationMs must be positive.");
                    }
                    else if (item.DurationMs > RunLimits.MaxTimedDurationMs)
                    {
                        Add("item.duration.out-of-range", path + "/durationMs", entryId, "durationMs must not exceed seven days (604800000).");
                    }

                    ValidateItemTarget(item.Target, path + "/target", entryId);
                    ValidateEffect(item.Effect, EffectSource.Item, path + "/effect", entryId);
                    if (item.Effect != null && item.Effect.Kind == "machineOutput" && item.Target != null && item.Target.Kind != "machineType")
                    {
                        Add("effect.target.invalid", path + "/target", entryId, "A machineOutput Item must target a machine type.");
                    }

                    if (tierOk && before == _totalAdded && IsCanonicalId(item.Id) && _obtainableItems.Contains(item.Id))
                    {
                        tiersUsable[item.Tier] = true;
                    }
                }

                var missing = new List<string>();
                for (var tier = 1; tier <= 3; tier++)
                {
                    if (!tiersUsable[tier])
                    {
                        missing.Add(tier.ToString(CultureInfo.InvariantCulture));
                    }
                }

                if (missing.Count > 0)
                {
                    Add(
                        "item.tiers.incomplete",
                        "/items",
                        null,
                        "Every Item tier from 1 to 3 needs a usable Item that a contract reward grants; missing tier(s): " + string.Join(", ", missing) + ".");
                }
            }

            private void ValidateItemTarget(RunItemTargetDto target, string path, string entryId)
            {
                if (target == null)
                {
                    Add("item.target.invalid", path, entryId, "An Item needs a target.");
                    return;
                }

                if (target.Kind == "factory")
                {
                    if (target.AllowedMachineIds != null)
                    {
                        Add("item.target.invalid", path + "/allowedMachineIds", entryId, "A factory target has no allowedMachineIds.");
                    }

                    return;
                }

                if (target.Kind != "machineType")
                {
                    Add("item.target.invalid", path + "/kind", entryId, $"Unknown Item target kind '{Clip(target.Kind)}'.");
                    return;
                }

                if (target.AllowedMachineIds == null || target.AllowedMachineIds.Count == 0)
                {
                    Add("item.target.invalid", path + "/allowedMachineIds", entryId, "A machineType target needs a non-empty allowedMachineIds.");
                    return;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < target.AllowedMachineIds.Count; index++)
                {
                    var id = target.AllowedMachineIds[index];
                    var idPath = Pointer(path + "/allowedMachineIds", index);
                    CheckIdentity(id, idPath, entryId, null);
                    if (!IsCanonicalId(id))
                    {
                        continue;
                    }

                    if (!seen.Add(id))
                    {
                        Add("item.target.invalid", idPath, entryId, $"Machine '{Clip(id)}' is listed twice.");
                    }
                    else if (!_machineIds.Contains(id))
                    {
                        Add("item.target.invalid", idPath, entryId, $"Unknown machine '{Clip(id)}'.");
                    }
                }
            }

            // ---- research and industry ------------------------------------------------------------------------

            private void ValidateResearch()
            {
                if (_doc.Research == null)
                {
                    return;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < _doc.Research.Count; index++)
                {
                    var research = _doc.Research[index];
                    var path = Pointer("/research", index);
                    if (research == null)
                    {
                        Add("value.null", path, null, "Research entry is missing.");
                        continue;
                    }

                    var entryId = KnownEntryId(research.Id);
                    CheckIdentity(research.Id, path + "/id", null, seen);
                    CheckReservedId(research.Id, path + "/id", entryId);

                    var maxLevelOk = research.MaxLevel >= 1 && research.MaxLevel <= MaxDefinitionLevel;
                    if (!maxLevelOk)
                    {
                        Add("upgrade.max-level.invalid", path + "/maxLevel", entryId, "maxLevel must be 1 to 1,024.");
                    }

                    EvaluateCurve(research.RpCost, path + "/rpCost", entryId, maxLevelOk ? research.MaxLevel : 1);
                    ValidatePrerequisites(research, path, entryId);
                    if (IsCanonicalId(research.Id) && _cyclicResearch.Contains(research.Id))
                    {
                        Add("research.prerequisite.cycle", path + "/prerequisites", entryId, "The prerequisites of this Research form a cycle.");
                    }

                    ValidateEffect(research.Effect, EffectSource.Research, path + "/effect", entryId);
                }
            }

            private void ValidatePrerequisites(RunResearchDto research, string path, string entryId)
            {
                if (research.Prerequisites == null)
                {
                    return;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < research.Prerequisites.Count; index++)
                {
                    var prerequisite = research.Prerequisites[index];
                    var prerequisitePath = Pointer(path + "/prerequisites", index);
                    if (prerequisite == null)
                    {
                        Add("value.null", prerequisitePath, entryId, "Prerequisite entry is missing.");
                        continue;
                    }

                    CheckIdentity(prerequisite.Id, prerequisitePath + "/id", entryId, seen);
                    if (!IsCanonicalId(prerequisite.Id))
                    {
                        continue;
                    }

                    if (!_research.TryGetValue(prerequisite.Id, out var target))
                    {
                        Add("research.prerequisite.unknown", prerequisitePath + "/id", entryId, $"Unknown Research '{Clip(prerequisite.Id)}'.");
                        continue;
                    }

                    var targetKnownMax = target.MaxLevel >= 1 && target.MaxLevel <= MaxDefinitionLevel;
                    if (prerequisite.Level < 1 || (targetKnownMax && prerequisite.Level > target.MaxLevel))
                    {
                        Add(
                            "research.prerequisite.level-invalid",
                            prerequisitePath + "/level",
                            entryId,
                            "A prerequisite level must be 1 to the prerequisite's maxLevel.");
                    }
                }
            }

            private void ValidateIndustryUpgrades()
            {
                if (_doc.IndustryUpgrades == null)
                {
                    return;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var levelOneCosts = new List<long>();
                var startingStorage = new List<StorageUpgrade>();
                for (var index = 0; index < _doc.IndustryUpgrades.Count; index++)
                {
                    var upgrade = _doc.IndustryUpgrades[index];
                    var path = Pointer("/industryUpgrades", index);
                    if (upgrade == null)
                    {
                        Add("value.null", path, null, "Industry upgrade entry is missing.");
                        continue;
                    }

                    var entryId = KnownEntryId(upgrade.Id);
                    CheckIdentity(upgrade.Id, path + "/id", null, seen);

                    var maxLevelOk = upgrade.MaxLevel >= 1 && upgrade.MaxLevel <= MaxDefinitionLevel;
                    if (!maxLevelOk)
                    {
                        Add("upgrade.max-level.invalid", path + "/maxLevel", entryId, "maxLevel must be 1 to 1,024.");
                    }

                    var table = EvaluateCurve(upgrade.IpCost, path + "/ipCost", entryId, maxLevelOk ? upgrade.MaxLevel : 1);
                    if (table != null)
                    {
                        levelOneCosts.Add(table[0]);
                    }

                    CheckRange(upgrade.DecayPpm, 0, Ppm, path + "/decayPpm", entryId, "decayPpm must be 0 to 10^6.");
                    CheckRange(upgrade.PracticalCeilingLevel, 0, ExactBound.Value, path + "/practicalCeilingLevel", entryId, "practicalCeilingLevel must be 0 to 10^18.");
                    CheckRange(upgrade.MinGainPpm, 0, ExactBound.Value, path + "/minGainPpm", entryId, "minGainPpm must be 0 to 10^18.");

                    ValidateEffect(upgrade.Effect, EffectSource.Industry, path + "/effect", entryId);

                    ValidateIndustryLevels(upgrade, maxLevelOk, path, entryId, startingStorage);
                }

                ValidateStartingStorage(startingStorage);

                if (levelOneCosts.Count == 0 || !PrestigeIsUsable(out var threshold, out var coefficient, out var divisor))
                {
                    return;
                }

                // IP = ISqrt(floor(c^2 x FV / d)); cost <= IP  <=>  cost^2 x d <= c^2 x FV (exact, arbitrary precision).
                var reachable = new BigInteger(coefficient) * coefficient * threshold;
                foreach (var cost in levelOneCosts)
                {
                    if (new BigInteger(cost) * cost * divisor <= reachable)
                    {
                        return;
                    }
                }

                Add(
                    "industry.first-affordable.missing",
                    "/industryUpgrades",
                    null,
                    "No level-1 Industry cost is affordable with the IP earned at the prestige threshold.");
            }

            private sealed class StorageUpgrade
            {
                public string Path;
                public string EntryId;
                public long[] Table;
            }

            /// <summary>
            /// The decay-aware checks of one Industry upgrade (T331): the per-level value table, <c>speed.level.dead</c> for a level that adds nothing after
            /// the iterated floors, and <c>industry.ceiling.level</c> (<c>practicalCeilingLevel</c> is the first level whose marginal value is below
            /// <c>minGainPpm</c>, or the last level when none is). The <c>startingStorageLevel</c> upgrades are collected for
            /// <see cref="ValidateStartingStorage"/>, which sums them.
            /// </summary>
            private void ValidateIndustryLevels(RunIndustryUpgradeDto upgrade, bool maxLevelOk, string path, string entryId, List<StorageUpgrade> startingStorage)
            {
                var effect = upgrade.Effect;
                if (!maxLevelOk || effect == null || effect.Value < 0 || effect.Value > ExactBound.Value ||
                    !IsKnownEffectKind(EffectSource.Industry, effect.Kind) ||
                    upgrade.DecayPpm < 0 || upgrade.DecayPpm > Ppm ||
                    !IndustryValueTable.TryBuild(effect.Kind, effect.Value, upgrade.DecayPpm, upgrade.MaxLevel, out var table))
                {
                    return;
                }

                if (IndustryValueTable.IsDecaying(upgrade.DecayPpm))
                {
                    for (var level = 1; level <= table.Length; level++)
                    {
                        if (effect.Value > 0 && IndustryValueTable.GainPpm(effect.Kind, table, level) == 0)
                        {
                            Add(
                                "speed.level.dead",
                                path + "/maxLevel",
                                entryId,
                                $"Level {level} adds nothing: the decayed value rounds down to zero (decayPpm {upgrade.DecayPpm}).");
                            break;
                        }
                    }
                }

                if (effect.Kind == "startingStorageLevel" && effect.Value >= 1)
                {
                    startingStorage.Add(new StorageUpgrade { Path = path + "/maxLevel", EntryId = entryId, Table = table });
                }

                // industry.ceiling.level: a legacy upgrade (revision below 3) that declares neither a ceiling nor a minimum gain is not checked.
                if (upgrade.PracticalCeilingLevel != 0 || upgrade.MinGainPpm != 0 || _doc.ContentRevision >= RatioRulesRevision)
                {
                    var expected = IndustryValueTable.PracticalCeiling(effect.Kind, table, upgrade.MinGainPpm);
                    if (upgrade.PracticalCeilingLevel != expected)
                    {
                        Add(
                            "industry.ceiling.level",
                            path + "/practicalCeilingLevel",
                            entryId,
                            $"practicalCeilingLevel must be {expected}: the first level whose marginal value is below minGainPpm, or the last level when none is.");
                    }
                }
            }

            /// <summary>
            /// <c>speed.level.dead</c>, extended: the start storage level is clamped to <c>storageLevels</c>. A <c>startingStorageLevel</c> upgrade whose
            /// last level adds nothing is dead: the start level (1 + the decayed amounts of every level below its last, plus every other
            /// <c>startingStorageLevel</c> upgrade at its maximum) already reaches the top.
            /// </summary>
            private void ValidateStartingStorage(List<StorageUpgrade> upgrades)
            {
                if (upgrades.Count == 0 || _doc.StorageLevels == null || _doc.StorageLevels.Count == 0)
                {
                    return;
                }

                var total = BigInteger.Zero;
                foreach (var upgrade in upgrades)
                {
                    foreach (var amount in upgrade.Table)
                    {
                        total += amount;
                    }
                }

                foreach (var upgrade in upgrades)
                {
                    var lastLevel = upgrade.Table[upgrade.Table.Length - 1];
                    if (1 + total - lastLevel >= _doc.StorageLevels.Count)
                    {
                        Add(
                            "speed.level.dead",
                            upgrade.Path,
                            upgrade.EntryId,
                            "The last level adds no storage level: the start storage level already reaches the top of storageLevels one level earlier.");
                    }
                }
            }

            /// <summary>
            /// <c>cost.floor</c>: for every machine, and for the expansion track, the combined <c>upgradeCost</c> multiplier of every Research and
            /// Industry level at its maximum may not fall below the content floor (<see cref="CostFloorPpm"/>, 0.5x). It folds in checked
            /// fixed-point math at 10^18, floored per level. A decaying Industry upgrade (<c>decayPpm</c> below 10^6, T331) folds the decayed
            /// per-level factors of its value table, not the undecayed value, so the discount is not overstated. The catalog has no build-price discount, so the build price carries no multiplier here.
            /// </summary>
            private void ValidateCostFloor()
            {
                var sources = new List<SpeedSource>();
                if (_doc.Research != null)
                {
                    foreach (var research in _doc.Research)
                    {
                        AddCostSource(sources, research == null ? null : research.Effect, research == null ? 0 : research.MaxLevel);
                    }
                }

                if (_doc.IndustryUpgrades != null)
                {
                    foreach (var upgrade in _doc.IndustryUpgrades)
                    {
                        AddCostSource(sources, upgrade == null ? null : upgrade.Effect, upgrade == null ? 0 : upgrade.MaxLevel, upgrade == null ? 0 : upgrade.DecayPpm);
                    }
                }

                if (sources.Count == 0)
                {
                    return;
                }

                // The shared tracks are folded once: every allMachines source for the machines, every expansion source for the expansion track.
                // A machine then folds only the sources that name it.
                var allMachines = CostFloorScale;
                var expansion = CostFloorScale;
                var perMachine = new Dictionary<string, List<SpeedSource>>(StringComparer.Ordinal);
                foreach (var source in sources)
                {
                    if (source.TargetKind == "allMachines")
                    {
                        allMachines = FoldCost(allMachines, source);
                    }
                    else if (source.TargetKind == "expansion")
                    {
                        expansion = FoldCost(expansion, source);
                    }
                    else
                    {
                        if (!perMachine.TryGetValue(source.TargetId, out var list))
                        {
                            list = new List<SpeedSource>();
                            perMachine.Add(source.TargetId, list);
                        }

                        list.Add(source);
                    }
                }

                var floor = CostFloorPpm * (CostFloorScale / Ppm);
                if (_doc.Machines != null)
                {
                    for (var index = 0; index < _doc.Machines.Count; index++)
                    {
                        var machine = _doc.Machines[index];
                        if (machine == null || !IsCanonicalId(machine.Id))
                        {
                            continue;
                        }

                        var acc = allMachines;
                        if (perMachine.TryGetValue(machine.Id, out var own))
                        {
                            foreach (var source in own)
                            {
                                acc = FoldCost(acc, source);
                            }
                        }

                        if (acc < floor)
                        {
                            Add("cost.floor", Pointer("/machines", index), machine.Id, "The combined upgrade-cost discount of Research and Industry at their maximum levels is below the 0.5x floor.");
                        }
                    }
                }

                if (expansion < floor)
                {
                    Add("cost.floor", "/storageLevels", null, "The combined expansion-cost discount of Research and Industry at their maximum levels is below the 0.5x floor.");
                }
            }

            private static void AddCostSource(List<SpeedSource> sources, RunEffectDto effect, int maxLevel, long decayPpm = 0)
            {
                if (effect == null || effect.Kind != "upgradeCost" || effect.Target == null || effect.Value < 100_000 || effect.Value >= Ppm ||
                    maxLevel < 1 || maxLevel > MaxDefinitionLevel)
                {
                    return;
                }

                if (effect.Target.Kind == "allMachines" || effect.Target.Kind == "expansion" ||
                    (effect.Target.Kind == "machine" && effect.Target.Id != null))
                {
                    sources.Add(new SpeedSource
                    {
                        TargetKind = effect.Target.Kind,
                        TargetId = effect.Target.Id,
                        Ppm = effect.Value,
                        Levels = maxLevel,
                        Table = DecayTable(effect, maxLevel, decayPpm),
                    });
                }
            }

            // One source at its maximum level: a floored fixed-point multiply per level. The value only decreases, so once it is below the floor
            // it stays there and the fold stops (a failed multiply cannot happen for a factor below 10^6, the result is 0 at worst).
            private static long FoldCost(long acc, SpeedSource source)
            {
                var floor = CostFloorPpm * (CostFloorScale / Ppm);
                for (var level = 0; level < source.Levels && acc >= floor; level++)
                {
                    if (!MulDiv.MulDivFloor(acc, source.Table == null ? source.Ppm : source.Table[level], Ppm, out acc))
                    {
                        return 0;
                    }
                }

                return acc;
            }

            private bool PrestigeIsUsable(out long threshold, out long coefficient, out long divisor)
            {
                threshold = 0;
                coefficient = 0;
                divisor = 0;
                var prestige = _doc.Prestige;
                if (prestige == null || prestige.IpCurve == null || prestige.Threshold == null || prestige.IpCurve.Kind != "sqrt" ||
                    prestige.Threshold.Base < 1 || prestige.Threshold.Base > ExactBound.Value ||
                    prestige.IpCurve.Coefficient < 1 || prestige.IpCurve.Coefficient > ExactBound.Value ||
                    prestige.IpCurve.Divisor < 1 || prestige.IpCurve.Divisor > ExactBound.Value)
                {
                    return false;
                }

                threshold = prestige.Threshold.Base;
                coefficient = prestige.IpCurve.Coefficient;
                divisor = prestige.IpCurve.Divisor;
                return true;
            }

            // ---- effects ---------------------------------------------------------------------------------------

            private void ValidateEffect(RunEffectDto effect, EffectSource source, string path, string entryId)
            {
                if (effect == null)
                {
                    Add("value.null", path, entryId, "Effect is missing.");
                    return;
                }

                if (!IsKnownEffectKind(source, effect.Kind))
                {
                    Add("effect.kind.unknown", path + "/kind", entryId, $"Unknown effect kind '{Clip(effect.Kind)}' for this source.");
                    return;
                }

                var hasTargetMember = source != EffectSource.Item && effect.Kind != "startingCash" && effect.Kind != "startingStorageLevel";
                if (hasTargetMember)
                {
                    ValidateEffectTarget(effect, path, entryId);
                }
                else if (effect.Target != null)
                {
                    Add("effect.target.invalid", path + "/target", entryId, "This effect has no target.");
                }

                if (!EffectValueInRange(effect))
                {
                    Add(
                        "effect.value.out-of-range",
                        path + "/" + (effect.ValueMember ?? "value"),
                        entryId,
                        "The effect value is outside the range allowed for its kind.");
                }
            }

            private static bool IsKnownEffectKind(EffectSource source, string kind)
            {
                switch (kind)
                {
                    case "machineSpeed":
                        return true;
                    case "machineOutput":
                        return source != EffectSource.Industry;
                    case "salePrice":
                    case "upgradeCost":
                        return source != EffectSource.Item;
                    case "startingCash":
                    case "startingStorageLevel":
                        return source == EffectSource.Industry;
                    default:
                        return false;
                }
            }

            private void ValidateEffectTarget(RunEffectDto effect, string path, string entryId)
            {
                var target = effect.Target;
                if (target == null)
                {
                    Add("effect.target.invalid", path + "/target", entryId, "This effect needs a target.");
                    return;
                }

                string[] allowed;
                switch (effect.Kind)
                {
                    case "machineSpeed":
                        allowed = new[] { "allMachines", "machine" };
                        break;
                    case "machineOutput":
                        allowed = new[] { "machine" };
                        break;
                    case "salePrice":
                        allowed = new[] { "allResources", "resource" };
                        break;
                    default:
                        allowed = new[] { "allMachines", "machine", "expansion" };
                        break;
                }

                var targetPath = path + "/target";
                if (Array.IndexOf(allowed, target.Kind) < 0)
                {
                    Add("effect.target.invalid", targetPath + "/kind", entryId, $"Target kind '{Clip(target.Kind)}' is not allowed for effect kind '{effect.Kind}'.");
                    return;
                }

                var needsId = target.Kind == "machine" || target.Kind == "resource";
                if (!needsId)
                {
                    if (target.Id != null)
                    {
                        Add("effect.target.invalid", targetPath + "/id", entryId, $"Target kind '{target.Kind}' has no id.");
                    }

                    return;
                }

                CheckIdentity(target.Id, targetPath + "/id", entryId, null);
                if (!IsCanonicalId(target.Id))
                {
                    return;
                }

                var known = target.Kind == "machine" ? _machineIds.Contains(target.Id) : _resourceIds.Contains(target.Id);
                if (!known)
                {
                    Add("effect.target.invalid", targetPath + "/id", entryId, $"Unknown {target.Kind} '{Clip(target.Id)}'.");
                }
            }

            private bool EffectValueInRange(RunEffectDto effect)
            {
                var value = effect.Value;
                switch (effect.Kind)
                {
                    case "machineSpeed":
                    case "salePrice":
                        return value > Ppm && value <= ExactBound.Value;
                    case "upgradeCost":
                        return value >= 100_000 && value < Ppm;
                    case "machineOutput":
                        return value >= 2 && value <= (_outputBoundOk ? _outputBound : MaxOutputFactorBound);
                    default:
                        return value >= 1 && value <= ExactBound.Value;
                }
            }

            // ---- overdrive, prestige, offline policy ------------------------------------------------------------

            private void ValidateOverdrive()
            {
                var overdrive = _doc.Overdrive;
                if (overdrive == null)
                {
                    Add("value.null", "/overdrive", null, "overdrive is missing.");
                    return;
                }

                if (overdrive.DurationMs < 1)
                {
                    Add("overdrive.value.invalid", "/overdrive/durationMs", null, "durationMs must be positive.");
                }
                else if (overdrive.DurationMs > RunLimits.MaxTimedDurationMs)
                {
                    Add("overdrive.duration.out-of-range", "/overdrive/durationMs", null, "durationMs must not exceed seven days (604800000).");
                }

                if (overdrive.CooldownMs < 0)
                {
                    Add("overdrive.value.invalid", "/overdrive/cooldownMs", null, "cooldownMs must not be negative.");
                }
                else if (overdrive.CooldownMs > RunLimits.MaxTimedDurationMs)
                {
                    Add("overdrive.cooldown.out-of-range", "/overdrive/cooldownMs", null, "cooldownMs must not exceed seven days (604800000).");
                }

                if (overdrive.SpeedPpm <= Ppm || overdrive.SpeedPpm > ExactBound.Value)
                {
                    Add("overdrive.value.invalid", "/overdrive/speedPpm", null, "speedPpm must exceed 10^6 and not exceed 10^18.");
                }

                ValidateUnlock(overdrive.Unlock, "/overdrive/unlock", null, RunState.OverdriveUnlockId);
            }

            private void ValidatePrestige()
            {
                var prestige = _doc.Prestige;
                if (prestige == null)
                {
                    Add("value.null", "/prestige", null, "prestige is missing.");
                    return;
                }

                var threshold = prestige.Threshold;
                if (threshold == null)
                {
                    Add("value.null", "/prestige/threshold", null, "threshold is missing.");
                }
                else
                {
                    if (threshold.Base < 1 || threshold.Base > ExactBound.Value)
                    {
                        Add("prestige.threshold.invalid", "/prestige/threshold/base", null, "The threshold base must be 1 to 10^18.");
                    }

                    CheckRange(threshold.GrowthPpm, Ppm, ExactBound.Value, "/prestige/threshold/growthPpm", null, "growthPpm must be 10^6 to 10^18.");
                    CheckRange(threshold.MaxIndex, 1, ExactBound.Value, "/prestige/threshold/maxIndex", null, "maxIndex must be 1 to 10^18.");

                    // prestige.threshold.bound (T331): T(maxIndex), with the iterated floors of the runtime, may not exceed 10^18. Checked only for a valid
                    // base, growth and maxIndex (each is reported by its own code otherwise).
                    if (threshold.Base >= 1 && threshold.Base <= ExactBound.Value && threshold.GrowthPpm >= Ppm && threshold.GrowthPpm <= ExactBound.Value &&
                        threshold.MaxIndex >= 1 && threshold.MaxIndex <= ExactBound.Value &&
                        !IdleFactory.Run.Application.PrestigeThreshold.TryFor(new ThresholdDef(threshold.Base, threshold.GrowthPpm, threshold.MaxIndex), threshold.MaxIndex, out _))
                    {
                        Add("prestige.threshold.bound", "/prestige/threshold/maxIndex", null, "The threshold at maxIndex exceeds 10^18.");
                    }
                }

                var curve = prestige.IpCurve;
                if (curve == null)
                {
                    Add("value.null", "/prestige/ipCurve", null, "ipCurve is missing.");
                    return;
                }

                if (curve.Kind != "sqrt")
                {
                    Add("prestige.curve.unsupported", "/prestige/ipCurve/kind", null, $"Unsupported IP curve kind '{Clip(curve.Kind)}'; only sqrt is supported.");
                }

                if (curve.Coefficient < 1 || curve.Coefficient > ExactBound.Value)
                {
                    Add("value.out-of-range", "/prestige/ipCurve/coefficient", null, "coefficient must be 1 to 10^18.");
                }

                if (curve.Divisor < 1 || curve.Divisor > ExactBound.Value)
                {
                    Add("value.out-of-range", "/prestige/ipCurve/divisor", null, "divisor must be 1 to 10^18.");
                }
            }

            private void ValidateOfflinePolicies()
            {
                var policies = _doc.OfflinePolicies;
                var policy = policies != null && policies.Count == 1 ? policies[0] : null;
                if (policy == null || policy.Revision != 1 || policy.EfficiencyNumerator != 4 ||
                    policy.EfficiencyDenominator != 5 || policy.MaxCreditedTicks != 288_000_000_000L)
                {
                    Add(
                        "policy.revision-1.required",
                        "/offlinePolicies",
                        null,
                        "The catalog requires exactly offline policy revision 1 with 4/5 efficiency and an eight-hour cap.");
                }
            }

            // ---- shared rules ---------------------------------------------------------------------------------------

            private void ValidateUnlock(RunUnlockDto unlock, string path, string entryId, string candidateId)
            {
                var before = _totalAdded;
                ValidateUnlockShape(unlock, path, entryId);
                if (before == _totalAdded && IsCanonicalId(candidateId) && !_unlockable.Contains(candidateId))
                {
                    Add(
                        "unlock.unreachable",
                        path,
                        entryId,
                        "This unlock can never be satisfied: following the unlock conditions from runStart.initialUnlocks reaches a dead end or a cycle.");
                }
            }

            private void ValidateUnlockShape(RunUnlockDto unlock, string path, string entryId)
            {
                if (unlock == null)
                {
                    Add("value.null", path, entryId, "Unlock is missing.");
                    return;
                }

                switch (unlock.Kind)
                {
                    case "always":
                        return;
                    case "researchPurchased":
                        CheckIdentity(unlock.Id, path + "/id", entryId, null);
                        if (IsCanonicalId(unlock.Id) && !_research.ContainsKey(unlock.Id))
                        {
                            Add("ref.research.unknown", path + "/id", entryId, $"Unknown Research '{Clip(unlock.Id)}'.");
                        }

                        return;
                    case "resourceProduced":
                        CheckIdentity(unlock.Id, path + "/id", entryId, null);
                        if (IsCanonicalId(unlock.Id) && !_resourceIds.Contains(unlock.Id))
                        {
                            Add("ref.resource.unknown", path + "/id", entryId, $"Unknown resource '{Clip(unlock.Id)}'.");
                        }

                        return;
                    case "factoryLevelAtLeast":
                        if (!unlock.Level.HasValue || unlock.Level.Value < 1 || unlock.Level.Value > _factoryLevelCount)
                        {
                            Add("unlock.level.invalid", path + "/level", entryId, "The required factory level must be 1 to the number of factory levels.");
                        }

                        return;
                    default:
                        Add("unlock.kind.unknown", path + "/kind", entryId, $"Unknown unlock kind '{Clip(unlock.Kind)}'.");
                        return;
                }
            }

            /// <summary>
            /// Evaluates the curve table (<paramref name="entries"/> values, matching the domain <c>Curve.Precompute</c>).
            /// Returns null when the curve is invalid; the diagnostics are reported here.
            /// </summary>
            private long[] EvaluateCurve(RunCurveDto curve, string path, string entryId, int entries)
            {
                if (curve == null)
                {
                    Add("value.null", path, entryId, "Curve is missing.");
                    return null;
                }

                var ok = true;
                if (curve.Base < 0 || curve.Base > ExactBound.Value)
                {
                    ok = false;
                    Add("value.out-of-range", path + "/base", entryId, "A curve base must be 0 to 10^18.");
                }

                if (curve.GrowthPpm < Ppm || curve.GrowthPpm > ExactBound.Value)
                {
                    ok = false;
                    Add("curve.growth.invalid", path + "/growthPpm", entryId, "growthPpm must be 10^6 to 10^18 (10^6 is flat).");
                }

                if (curve.Rounding != "ceil" && curve.Rounding != "floor")
                {
                    ok = false;
                    Add("value.out-of-range", path + "/rounding", entryId, "rounding must be ceil or floor.");
                }

                if (!ok)
                {
                    return null;
                }

                var table = new long[entries];
                table[0] = curve.Base;
                for (var i = 1; i < entries; i++)
                {
                    long next;
                    var fits = curve.Rounding == "ceil"
                        ? MulDiv.MulDivCeil(table[i - 1], curve.GrowthPpm, Ppm, out next)
                        : MulDiv.MulDivFloor(table[i - 1], curve.GrowthPpm, Ppm, out next);
                    if (!fits)
                    {
                        Add("curve.overflow", path, entryId, $"The curve exceeds 10^18 at index {i}.");
                        return null;
                    }

                    if (curve.GrowthPpm > Ppm && next <= table[i - 1])
                    {
                        Add("curve.stalled", path, entryId, $"The growing curve does not strictly increase at index {i}; declare growthPpm 10^6 for a flat curve.");
                        return null;
                    }

                    table[i] = next;
                }

                return table;
            }

            /// <summary>Reports <c>value.out-of-range</c> when <paramref name="value"/> lies outside [<paramref name="min"/>, <paramref name="max"/>].</summary>
            private void CheckRange(long value, long min, long max, string path, string entryId, string message, string code = "value.out-of-range")
            {
                if (value < min || value > max)
                {
                    Add(code, path, entryId, message);
                }
            }

            private void CheckPresentationKey(string key, string path, string entryId)
            {
                if (_keys != null && key != null && !_keys.IsResolved(key))
                {
                    Add("presentation.key.unresolved", path, entryId, $"Presentation key '{Clip(key)}' does not resolve.");
                }
            }

            private void CheckIdentity(string value, string path, string entryId, ISet<string> seen)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Add("id.empty", path, entryId ?? KnownEntryId(value), "Identifier must not be empty or whitespace.");
                    return;
                }

                if (value.Length > GameplayContentLimits.MaximumIdentifierLength)
                {
                    Add(
                        "id.too-long",
                        path,
                        entryId ?? value,
                        $"Identifier exceeds {GameplayContentLimits.MaximumIdentifierLength} UTF-16 code units.");
                }

                var normalized = NormalizedId(value);
                if (normalized == null || !StringComparer.Ordinal.Equals(value, normalized))
                {
                    Add("id.not-canonical", path, entryId ?? value, "Identifier must already be trimmed and Unicode NFC-normalized.");
                }

                if (seen != null && normalized != null && !seen.Add(normalized))
                {
                    Add("id.duplicate", path, entryId ?? value, $"Identifier '{Clip(value)}' is repeated in its family.");
                }
            }
        }

        // ---- static helpers -------------------------------------------------------------------------------------

        private static string NormalizedId(string value)
        {
            if (value == null)
            {
                return null;
            }

            try
            {
                return value.Trim().Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static bool IsCanonicalId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = NormalizedId(value);
            return normalized != null &&
                   normalized.Length > 0 &&
                   value.Length <= GameplayContentLimits.MaximumIdentifierLength &&
                   StringComparer.Ordinal.Equals(value, normalized);
        }

        // Values echoed into diagnostics are bounded to 128 UTF-16 code units (an ellipsis marks the cut).
        private static string Clip(string value)
        {
            if (value == null || value.Length <= MaxEchoedLength)
            {
                return value;
            }

            var length = MaxEchoedLength - 1;
            if (char.IsHighSurrogate(value[length - 1]))
            {
                length--;
            }

            return value.Substring(0, length) + "…";
        }

        private static string KnownEntryId(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private static string OneLine(string value) => (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");

        private static string Pointer(string parent, int index) =>
            parent + "/" + index.ToString(CultureInfo.InvariantCulture);
    }
}
