using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.PlayMode.F006;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.Presentation.Look;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode.F007
{
    /// <summary>A look-profile source driven by hand (the DevTools source is define-gated and cannot be used by this assembly).</summary>
    internal sealed class ParitySource : ILookProfileSource
    {
        private readonly Dictionary<string, LookProfile> held = new Dictionary<string, LookProfile>(StringComparer.Ordinal);

        public string Selected { get; set; }

        public bool IsReady => true;

        public string SelectedProfileId => Selected;

        public event Action Changed;

        public ParitySource Hold(LookProfile profile)
        {
            held[profile.Id] = profile;
            return this;
        }

        public bool TryGetProfile(string id, out LookProfile profile) => held.TryGetValue(id ?? string.Empty, out profile);

        public void Select(string id)
        {
            Selected = id;
            Changed?.Invoke();
        }
    }

    /// <summary>What one scripted run recorded: a hash per tick of the full authoritative state, the tap results, the final save bytes and a tally of the inputs.</summary>
    internal sealed class ParityOutcome
    {
        public readonly List<string> TickHashes = new List<string>();
        public readonly List<string> Taps = new List<string>();
        public readonly List<string> Applied = new List<string>();
        public readonly Dictionary<string, int> Tally = new Dictionary<string, int>(StringComparer.Ordinal);
        public byte[] SaveBytes;
        public byte[] MidRunSaveBytes;
        public readonly List<string> Reasons = new List<string>();
        public string ActiveAtEnd;
        public Sprite SlotZeroSpriteAfterSwitch;
        public PresentationSpriteEntry MinerEntryAfterSwitch;

        public int Count(string kind) => Tally.TryGetValue(kind, out var value) ? value : 0;
    }

    /// <summary>
    /// T609 (R19 amendment, QA-1, QA-2, ARCH-7): the harness of the look-profile determinism parity test. One scripted run drives a real
    /// <see cref="RunSession"/> over the packaged Iron content with a scaled prestige threshold: the same seed, no wall clock (the run advances only by the
    /// ticks the script gives it), and scripted inputs (build, upgrade, auto-sell, Overdrive, an offline-length gap, a prestige). After every tick the
    /// presentation of the active look profile draws that state through the real world views, taps go through <see cref="WorldTapResolver"/>, and a hash of
    /// the full authoritative state (the Save V3 bytes of the run and the permanent state plus every machine read-model field and cycle counter) is recorded.
    /// A profile that is only presentation yields the same hashes, tap results and save bytes as any other.
    /// </summary>
    internal sealed class LookParityRig : IDisposable
    {
        public const long Second = 10_000_000L;
        public const int Ticks = 90;
        public const int GapTick = 45;
        public const int PrestigeTick = 60;
        public const int MidRunSaveTick = 40;
        public const long StartingCash = 1_000_000_000L;

        private static readonly string[] BuildOrder =
        {
            "machine.miner", "machine.smelter", "machine.miner", "machine.press", "machine.assembler", "machine.smelter",
        };

        private readonly DeckTestRig deckRig = new DeckTestRig();

        public List<string> Log { get; } = new List<string>();

        public static Guid Id(int n) => new Guid(n, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);

        /// <summary>
        /// Runs the script. <paramref name="startProfile"/> is the id the source selects before start-up; at <paramref name="switchTick"/> (when not
        /// negative) the source switches to <paramref name="switchTo"/>. <paramref name="planted"/> runs in the presentation step of every tick with the
        /// active profile's id: the negative control uses it to play a "profile" that changes the simulation.
        /// </summary>
        public ParityOutcome Run(string startProfile, int switchTick, string switchTo, Action<int, string, RunSession, RunCommands> planted)
        {
            var outcome = new ParityOutcome();
            var table = LookProfileRequirementTable.F006();
            var shipped = LookProfile.Create(LookProfile.F006Id, false, deckRig.Catalog(), table);
            var prototypeA = LookProfile.Create(LookProfile.PrototypeAId, true, deckRig.Catalog(), table);
            var prototypeB = LookProfile.Create(LookProfile.PrototypeBId, true, deckRig.Catalog(), table);
            deckRig.Owned.Add(shipped);
            deckRig.Owned.Add(prototypeA);
            deckRig.Owned.Add(prototypeB);
            var source = new ParitySource { Selected = startProfile }.Hold(prototypeA).Hold(prototypeB);
            var resolver = new LookProfileResolver(shipped, null, source, true, Log.Add);
            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(resolver.Active.Id, Is.EqualTo(startProfile));

            // The simulation: fixed ids, a fixed start instant, and no clock. Only Advance moves it.
            var catalog = RunPlayerFixtures.ScaledCatalog();
            var permanent = new PermanentState(catalog);
            var start = NextRunStart.Compute(catalog, permanent);
            var state = new RunState(catalog, Id(1), 1, null, StartingCash, 0, start.StorageLevel, 1, catalog.RunStart.InitialUnlocks);
            Assert.That(new RunSimulator(catalog).Advance(state, permanent, 0, RunPlayerFixtures.StartUtcMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            var session = new RunSession(catalog, state, permanent, RunPlayerFixtures.StartUtcMs, null);
            var nextInstance = 100;
            var nextRun = 7000;
            var commands = new RunCommands(session, () => Id(nextInstance++), () => Id(nextRun++));

            // The views, composed and switched through the same LookProfileWorldViews path the bootstrap uses.
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var grid = Host("parity-grid").AddComponent<SlotGridView>();
            var deck = Host("parity-deck").AddComponent<FactoryDeckView>();
            var links = Host("parity-links").AddComponent<DepotLinkView>();
            var deposit = Host("parity-deposit").AddComponent<DepositView>();
            var views = new LookProfileWorldViews(grid, deck, deckRig.Settings(), null, null, links, deposit, null, null);
            views.Apply(resolver.ActiveCatalog);
            grid.SetLayout(layout);
            resolver.Applied += profile =>
            {
                outcome.Applied.Add(profile.Id);
                views.Apply(resolver.ActiveCatalog);
            };

            string KeyOf(string machineId) => catalog.Machines.TryGetValue(machineId, out var machine) ? machine.PresentationKey : string.Empty;
            var boosted = new HashSet<string>(StringComparer.Ordinal);

            for (var tick = 0; tick < Ticks; tick++)
            {
                var trace = new StringBuilder();
                if (tick == switchTick)
                {
                    source.Select(switchTo);
                }

                Script(tick, session, commands, outcome, trace);
                Assert.That(session.Advance(5 * Second).IsSuccess, Is.True, "tick " + tick);

                // The presentation of the active profile: the same snapshot, the same visuals, drawn through that profile's catalog.
                var snapshot = RunSnapshots.Factory(session);
                var overdriveActive = RunSnapshots.Overdrive(session).State == OverdriveStage.Active;
                grid.Render(SlotGridModel.DescribeAll(snapshot, KeyOf, overdriveActive, boosted, -1));
                planted?.Invoke(tick, resolver.Active.Id, session, commands);
                if (tick == switchTick)
                {
                    outcome.SlotZeroSpriteAfterSwitch = grid.RendererOf(0, SlotGridView.SlotRendererRole.Machine)?.sprite;
                    resolver.ActiveCatalog.TryGetEntry(DeckTestRig.MinerKey, out outcome.MinerEntryAfterSwitch);
                }

                if (tick % 10 == 0)
                {
                    Tap(tick, grid.Layout, outcome);
                }

                if (tick == MidRunSaveTick)
                {
                    outcome.MidRunSaveBytes = SaveBytes(session, catalog);
                }

                outcome.TickHashes.Add(HashOf(session, catalog, trace));
            }

            outcome.SaveBytes = SaveBytes(session, catalog);
            outcome.ActiveAtEnd = resolver.Active.Id;
            resolver.Dispose();
            return outcome;
        }

        private GameObject Host(string name)
        {
            var host = new GameObject(name);
            deckRig.Owned.Add(host);
            return host;
        }

        public void Dispose() => deckRig.Dispose();

        private static void Script(int tick, RunSession session, RunCommands commands, ParityOutcome outcome, StringBuilder trace)
        {
            void Record<T>(string kind, RunCommandResult<T> result)
            {
                trace.Append(kind).Append(result.IsSuccess ? ":ok;" : ":" + result.ReasonCode + ";");
                if (result.IsSuccess)
                {
                    outcome.Tally[kind] = outcome.Count(kind) + 1;
                }
                else if (!outcome.Reasons.Contains(kind + ":" + result.ReasonCode))
                {
                    outcome.Reasons.Add(kind + ":" + result.ReasonCode);
                }
            }

            var state = session.State;
            if (tick % 2 == 0 && state.Machines.Count < BuildOrder.Length)
            {
                var free = -1;
                for (var slot = 0; slot < state.SlotCount && free < 0; slot++)
                {
                    free = state.MachineAtSlot(slot) == null ? slot : -1;
                }

                if (free >= 0)
                {
                    Record("build", commands.Build(free, BuildOrder[state.Machines.Count]));
                }
            }

            if (tick % 4 == 3 && state.Machines.Count > 0)
            {
                Record("upgrade", commands.Upgrade(state.Machines[(tick / 4) % state.Machines.Count].InstanceId));
            }

            if (tick == 4 || tick == 6)
            {
                // Overdrive unlocks at factory level 2.
                Record("factory", commands.UpgradeFactory());
            }

            if (tick == 10)
            {
                Record("autosell", commands.SetAutoSell("iron.ore", true));
            }

            if (tick == 14 || tick == 20 || tick == 34 || tick == 48 || tick == 56 || tick == 70 || tick == 80)
            {
                Record("overdrive", commands.ActivateOverdrive());
            }

            if ((tick == 24 || tick == 26) && state.Machines.Count > 0)
            {
                Record("active", commands.SetActive(state.Machines[0].InstanceId, tick == 26));
            }

            if (tick == GapTick)
            {
                // An offline-length gap: one hour of run time passes in a single advance.
                var gap = session.Advance(3600 * Second);
                trace.Append("gap:").Append(gap.IsSuccess ? "ok;" : gap.ReasonCode + ";");
                outcome.Tally["gap"] = outcome.Count("gap") + (gap.IsSuccess ? 1 : 0);
            }

            if (tick == PrestigeTick)
            {
                var confirmed = commands.ConfirmPrestige();
                Record("prestige-confirm", confirmed);
                if (confirmed.IsSuccess)
                {
                    Record("prestige", commands.PromotePrestige());
                }
            }
        }

        // Taps at the centre of every machine's rectangle, and one far outside the world, through the same resolver the input path uses.
        private static void Tap(int tick, FactoryWorldLayout layout, ParityOutcome outcome)
        {
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var source = WorldTapResolver.Resolve(layout, null, layout.MachineRectOf(new BuildingLocationId(i)).center, out var target);
                outcome.Taps.Add(tick + ":" + i + ":" + source + ":" + (source == WorldTapSource.None ? -1 : target.Value));
            }

            var outside = WorldTapResolver.Resolve(layout, null, new Vector2(1000f, 1000f), out var none);
            outcome.Taps.Add(tick + ":outside:" + outside + ":" + (outside == WorldTapSource.None ? -1 : none.Value));
        }

        private static byte[] SaveBytes(RunSession session, RunCatalog catalog)
        {
            var meta = new SaveV2Meta(Id(1), 1, session.TimelineUtcMs, catalog.OfflinePolicy.Revision, null, catalog.ContentRevision, null);
            var written = SaveV2Mapper.Serialize(meta, session.State, session.Permanent);
            if (!written.IsSuccess)
            {
                throw new InvalidOperationException("The state did not serialize: " + written.Code);
            }

            return written.Bytes;
        }

        // The full authoritative state of this tick: the canonical save bytes of the run and the permanent state, every field of the machine read model,
        // the completed-cycle counters, and what the scripted inputs of the tick returned.
        private static string HashOf(RunSession session, RunCatalog catalog, StringBuilder trace)
        {
            var text = new StringBuilder();
            text.Append(Convert.ToBase64String(SaveBytes(session, catalog))).Append('|').Append(trace).Append('|');
            var buffer = new MachineStateView[64];
            var count = session.CopyMachineStates(buffer);
            for (var i = 0; i < count; i++)
            {
                var view = buffer[i];
                text.Append(view.SlotIndex).Append(',').Append(view.MachineTypeIndex).Append(',').Append(view.Level).Append(',').Append(view.IsActive)
                    .Append(',').Append(view.Phase).Append(',').Append(view.Blocker).Append(',').Append(view.BatchFactor).Append(',')
                    .Append(view.ProgressPpm).Append(',').Append(view.CompletedCycles).Append(';');
            }

            foreach (var machine in session.State.Machines)
            {
                text.Append(session.CompletedCyclesOf(machine.InstanceId)).Append(';');
            }

            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", string.Empty);
            }
        }
    }
}
