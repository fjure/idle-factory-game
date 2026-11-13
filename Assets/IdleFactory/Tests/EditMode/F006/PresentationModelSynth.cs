using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>One machine of a synthetic read model: every value the presentation models read is set directly.</summary>
    internal sealed class SynthMachine
    {
        public int Slot;
        public string MachineId = RunCommandFixtures.Miner;
        public bool Active = true;
        public MachinePhase Phase = MachinePhase.Running;
        public MachineBlocker Blocker = MachineBlocker.None;
        public long CycleTicks = 20_000_000;
        public bool UpgradeAvailable;
        public bool UpgradeAffordable;
        public long Completed;
        public int Instance;

        /// <summary>The resources of the MissingInput blockers (a machine can be short of more than one input).</summary>
        public string[] Missing = new string[0];
    }

    /// <summary>One resource of a synthetic rates snapshot. The coverage is set directly: consumption is 10^6, so production equals the ppm value.</summary>
    internal sealed class SynthResource
    {
        public string Id;
        public bool HasDemand = true;
        public long CoveragePpm;
        public bool AutoSold;
        public long Units;
    }

    /// <summary>
    /// Builds the read models of Feature 006's presentation-model tests without a session (the snapshot constructors are internal, and the test
    /// assembly sees them through InternalsVisibleTo). A run is simulated by a real session in the end-to-end cases; these builders give exact control
    /// over states and coverages.
    /// </summary>
    internal static class PresentationModelSynth
    {
        private static readonly RunCatalog Catalog = RunCommandFixtures.Catalog();

        public sealed class Models
        {
            public RunFactorySnapshot Factory;
            public MachineStateView[] Views;
            public RatesSnapshot Rates;
        }

        public static string[] ResourceIds => new[] { "iron.ore", "iron.bar", "iron.plate", "iron.gear" };

        public static List<SynthResource> Resources(params long[] coveragePpm)
        {
            var ids = ResourceIds;
            var list = new List<SynthResource>();
            for (var i = 0; i < ids.Length; i++)
            {
                list.Add(new SynthResource { Id = ids[i], CoveragePpm = i < coveragePpm.Length ? coveragePpm[i] : 0 });
            }

            return list;
        }

        public static Models Build(long structureRevision, long quantityRevision, IList<SynthMachine> machines, IList<SynthResource> resources = null)
        {
            resources = resources ?? Resources();
            var instances = new MachineInstance[machines.Count];
            var views = new MachineStateView[machines.Count];
            var snapshots = new MachineSnapshot[machines.Count];
            var diagnoses = new MachineDiagnosis[machines.Count];
            for (var i = 0; i < machines.Count; i++)
            {
                var synth = machines[i];
                var id = synth.Instance != 0 ? synth.Instance : 100 + i;
                MachineDef def = Catalog.Machines[synth.MachineId];
                long recipeCycle = Catalog.Recipes[def.RecipeId].DurationTicks;
                instances[i] = new MachineInstance(RunSimHarness.Id(id), def, synth.Slot, 1, synth.Active, 0, recipeCycle, 10);
                views[i] = new MachineStateView(synth.Slot, 0, 1, synth.Active, synth.Phase, synth.Blocker, 1, 0, synth.Completed);
                var rates = new MachineRates(1, synth.CycleTicks, new ResourceRate[0], new ResourceRate[0]);
                var upgrade = new UpgradePreview(synth.UpgradeAvailable, 2, 10, synth.UpgradeAffordable, null, null, false, 0, 0);
                snapshots[i] = new MachineSnapshot(instances[i].InstanceId, def, 0, instances[i], views[i], i, 0, rates, upgrade);

                var blockers = new List<MachineBlockerInfo>();
                foreach (var resource in synth.Missing)
                {
                    blockers.Add(new MachineBlockerInfo(MachineBlocker.MissingInput, resource, 2, 0, false, 0, 0, new CorrectiveAction[0]));
                }

                diagnoses[i] = new MachineDiagnosis(instances[i], views[i], rates, blockers.ToArray(), default);
            }

            var rateList = new ResourceRates[resources.Count];
            for (var i = 0; i < rateList.Length; i++)
            {
                var r = resources[i];
                // Consumption 10^6 gives coverage = production; no demand is consumption 0.
                rateList[i] = new ResourceRates(r.Id, r.Units, r.AutoSold, r.CoveragePpm, r.HasDemand ? 1_000_000 : 0);
            }

            var factory = new RunFactorySnapshot(
                structureRevision, quantityRevision, 0, 1, 12, new SlotSnapshot[0], snapshots, new Guid[0], new MachineTypeSnapshot[0], null, null);
            var rateSnapshot = new RatesSnapshot(structureRevision, quantityRevision, 0, 1000, 0, rateList, diagnoses);
            return new Models { Factory = factory, Views = views, Rates = rateSnapshot };
        }
    }
}
