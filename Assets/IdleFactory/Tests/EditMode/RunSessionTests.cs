using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// <see cref="RunSession"/> (T099, contracts/run-application.md, R01, R05, R14): the timeline anchor, the
    /// boundary events derived from the state's timers, the two revisions, settle after a command, the atomic
    /// value.limit rejection, the stable reason codes and the allocation-free hot reads (SC-008).
    /// </summary>
    public sealed class RunSessionTests
    {
        private const long Start = RunSimHarness.StartUtcMs;
        private const long TicksPerSecond = RunSimHarness.TicksPerSecond;

        private static readonly ConflictKey FactorySpeed = new ConflictKey("machineSpeed", "factory");

        // ------------------------------------------------------------------------------------------------
        // Builders
        // ------------------------------------------------------------------------------------------------

        private static RunSession NewSession(RunCatalog catalog, RunState state, long anchor = Start) =>
            new RunSession(catalog, state, new PermanentState(catalog), anchor);

        /// <summary>Two Miners, a Smelter, a Press and an Assembler in slot order; Gear sells automatically.</summary>
        private static RunState Factory(RunCatalog catalog, bool timers)
        {
            RunState s = RunSimHarness.NewState(catalog, 25);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            RunSimHarness.Add(s, "machine.miner", 1, 2);
            RunSimHarness.Add(s, "machine.smelter", 2, 3);
            RunSimHarness.Add(s, "machine.press", 3, 4);
            RunSimHarness.Add(s, "machine.assembler", 4, 5);
            Assert.That(s.TrySetAutoSell("iron.gear", true), Is.EqualTo(RunStateResult.Ok));
            if (timers)
            {
                Assert.That(s.TryAddEffect(new ActiveEffect("item.boost", FactorySpeed, Start, Start + 60_000)), Is.EqualTo(RunStateResult.Ok));
                Assert.That(s.Overdrive.TryActivate(Start), Is.EqualTo(RunStateResult.Ok));
                Assert.That(s.Contracts.TryAccept("contract.timed-bar", Start), Is.EqualTo(RunStateResult.Ok));
            }
            return s;
        }

        private static RunCatalog TimerCatalog() =>
            RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { TimedContractDurationMs = 40_000 });

        private static void AssertAdvance(RunSession session, long ticks)
        {
            RunCommandResult<long> r = session.Advance(ticks);
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
        }

        // ------------------------------------------------------------------------------------------------
        // Timeline anchor
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Timeline_IsAnchorPlusActiveTicks_AndKeepsTheSubMillisecondRemainder()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunSession session = NewSession(catalog, RunSimHarness.NewState(catalog));

            Assert.That(session.TimelineUtcMs, Is.EqualTo(Start));
            AssertAdvance(session, 25_000);
            Assert.That(session.ActiveTicksSinceAnchor, Is.EqualTo(25_000));
            Assert.That(session.TimelineUtcMs, Is.EqualTo(Start + 2), "floor(25 000 / 10 000) ms");

            AssertAdvance(session, 5_000);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(Start + 3), "two halves of a millisecond add up (no per-call flooring)");
            Assert.That(session.Advance(TicksPerSecond).Value, Is.EqualTo(Start + 1003));
            Assert.That(session.AnchorUtcMs, Is.EqualTo(Start));
        }

        [Test]
        public void RebindAnchor_MovesTheTimelineForwardAndNeverBackwards()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunSession session = NewSession(catalog, RunSimHarness.NewState(catalog));
            AssertAdvance(session, 25_000);

            RunCommandResult<long> later = session.RebindAnchor(Start + 10_000);
            Assert.That(later.IsSuccess, Is.True);
            Assert.That(later.Value, Is.EqualTo(Start + 10_000));
            Assert.That(session.AnchorUtcMs, Is.EqualTo(Start + 10_000));
            Assert.That(session.ActiveTicksSinceAnchor, Is.Zero, "the anchor restarts the active time");
            AssertAdvance(session, TicksPerSecond);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(Start + 11_000), "timeline = anchor + active ticks after a rebind");

            RunCommandResult<long> backward = session.RebindAnchor(Start);
            Assert.That(backward.IsSuccess, Is.True);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(Start + 11_000), "a backward clock keeps the timeline");
            Assert.That(session.AnchorUtcMs, Is.EqualTo(Start + 10_000));

            RunCommandResult<long> outside = session.RebindAnchor(ExactBound.Value + 1);
            Assert.That(outside.ReasonCode, Is.EqualTo(RunReasons.ValueLimit));
            Assert.That(session.TimelineUtcMs, Is.EqualTo(Start + 11_000));
        }

        [Test]
        public void Constructor_RejectsAStateOfAnotherCatalog()
        {
            RunCatalog a = RunSimHarness.Catalog();
            RunCatalog b = RunSimHarness.Catalog();
            Assert.Throws<ArgumentException>(() => new RunSession(a, RunSimHarness.NewState(b), null, Start));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunSession(a, RunSimHarness.NewState(a), null, -1));
        }

        // ------------------------------------------------------------------------------------------------
        // Boundary events derived from the timers
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void EffectExpiry_HappensAtExactlyItsEndInstant()
        {
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { BoostItemDurationMs = 5 });
            RunState state = RunSimHarness.NewState(catalog);
            Assert.That(state.TryAddEffect(new ActiveEffect("item.boost", FactorySpeed, Start, Start + 5)), Is.EqualTo(RunStateResult.Ok));
            RunSession session = NewSession(catalog, state);

            AssertAdvance(session, 49_999);
            Assert.That(state.Effects.Count, Is.EqualTo(1), "one tick before the end the effect still runs");
            AssertAdvance(session, 1);
            Assert.That(state.Effects.Count, Is.Zero, "the boundary at the end of the advance belongs to that advance");
        }

        [Test]
        public void ABoundaryAlreadyDueAtTheStart_IsProcessedAtOffsetZero()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState state = RunSimHarness.NewState(catalog);
            Assert.That(state.TryAddEffect(new ActiveEffect("item.boost", FactorySpeed, Start, Start + 60_000)), Is.EqualTo(RunStateResult.Ok));
            RunSession session = NewSession(catalog, state);
            session.RebindAnchor(Start + 100_000);

            AssertAdvance(session, 1);

            Assert.That(state.Effects.Count, Is.Zero);
        }

        [Test]
        public void ContractDeadline_IsInclusive_AndExpiresOneMillisecondLater()
        {
            RunCatalog catalog = TimerCatalog();
            RunState state = RunSimHarness.NewState(catalog);
            Assert.That(state.Contracts.TryAccept("contract.timed-bar", Start), Is.EqualTo(RunStateResult.Ok));
            long deadline = state.Contracts.Active.DeadlineUtcMs.Value;
            RunSession session = NewSession(catalog, state);

            AssertAdvance(session, (deadline - Start) * RunSession.TicksPerMs);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(deadline));
            Assert.That(state.Contracts.Active, Is.Not.Null, "a delivery at timeline == deadline is still possible");
            Assert.That(state.Contracts.CheckComplete(session.TimelineUtcMs), Is.Not.EqualTo(RunStateResult.ContractExpired));

            AssertAdvance(session, RunSession.TicksPerMs);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(deadline + 1));
            Assert.That(state.Contracts.Active, Is.Null, "deadline + 1 ms expires the contract");
        }

        [Test]
        public void Overdrive_EndsAfterItsDuration_AndReturnsToReadyAfterTheCooldown()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState state = Factory(catalog, false);
            RunSession session = NewSession(catalog, state);
            session.BeginCommand();
            Assert.That(state.Overdrive.TryActivate(session.TimelineUtcMs), Is.EqualTo(RunStateResult.Ok));
            Assert.That(session.EndCommand(true, true).IsSuccess, Is.True);
            long activeUntil = state.Overdrive.ActiveUntilUtcMs.Value;
            long cooldownUntil = state.Overdrive.CooldownUntilUtcMs.Value;

            AssertAdvance(session, (activeUntil - Start) * RunSession.TicksPerMs - 1);
            Assert.That(state.Overdrive.IsActive(session.TimelineUtcMs), Is.True);
            AssertAdvance(session, 1);
            Assert.That(state.Overdrive.IsActive(session.TimelineUtcMs), Is.False);
            Assert.That(state.Overdrive.CooldownUntilUtcMs, Is.EqualTo(cooldownUntil), "still cooling down");

            AssertAdvance(session, (cooldownUntil - activeUntil) * RunSession.TicksPerMs);
            Assert.That(state.Overdrive.CooldownUntilUtcMs, Is.Null, "the cooldown end clears the timers");
        }

        [Test]
        public void SessionAdvance_EqualsTheDirectSimulatorPath_ForAnyPartitionIncludingEventInstants()
        {
            const long total = 900_000_000; // 90 s: contract at 40 s, effect at 60 s, Overdrive active phase at 30 s
            var cutSets = new[]
            {
                new long[0],
                new long[] { 400_000_000, 400_010_000, 600_000_000, 600_010_000 },
                new long[] { 123_456_789, 300_000_000, 777_777_777 },
            };
            string reference = null;
            foreach (long[] cuts in cutSets)
            {
                // Through the session.
                RunCatalog catalog = TimerCatalog();
                RunState state = Factory(catalog, true);
                RunSession session = NewSession(catalog, state);
                long at = 0;
                foreach (long cut in cuts.Concat(new[] { total }))
                {
                    AssertAdvance(session, cut - at);
                    at = cut;
                }
                string viaSession = RunSimHarness.Fingerprint(state, session.Permanent);
                Assert.That(session.TimelineUtcMs, Is.EqualTo(Start + total / RunSession.TicksPerMs));
                Assert.That(state.Effects.Count, Is.Zero);
                Assert.That(state.Contracts.Active, Is.Null);

                // Through the simulator with test-built events (T098 harness).
                RunCatalog catalog2 = TimerCatalog();
                RunState state2 = Factory(catalog2, true);
                var permanent2 = new PermanentState(catalog2);
                var sim = new RunSimulator(catalog2);
                at = 0;
                foreach (long cut in cuts.Concat(new[] { total }))
                {
                    Assert.That(RunSimHarness.AdvanceAt(sim, state2, permanent2, Start, at, cut - at), Is.EqualTo(RunAdvanceResult.Ok));
                    at = cut;
                }
                string direct = RunSimHarness.Fingerprint(state2, permanent2);

                Assert.That(viaSession, Is.EqualTo(direct), "session events equal the harness events; cuts: " + string.Join(",", cuts));
                reference = reference ?? viaSession;
                Assert.That(viaSession, Is.EqualTo(reference), "the partition does not change the result");
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Revisions
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void ProductionChangesTheQuantityRevisionOnly()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunSession session = NewSession(catalog, Factory(catalog, false));
            long structure = session.StructureRevision;
            long quantity = session.QuantityRevision;

            AssertAdvance(session, TicksPerSecond);

            Assert.That(session.QuantityRevision, Is.GreaterThan(quantity), "progress and stock changed");
            Assert.That(session.StructureRevision, Is.EqualTo(structure), "nothing structural happened");
        }

        [Test]
        public void AdvancingAnIdleFactory_ChangesNoRevision()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunSession session = NewSession(catalog, RunSimHarness.NewState(catalog));

            AssertAdvance(session, 10 * TicksPerSecond);

            Assert.That(session.QuantityRevision, Is.Zero);
            Assert.That(session.StructureRevision, Is.Zero);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(Start + 10_000), "time itself is not a quantity");
        }

        [Test]
        public void ExpiringAnEffect_ChangesTheStructureRevisionOnly()
        {
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { BoostItemDurationMs = 1_000 });
            RunState state = RunSimHarness.NewState(catalog);
            Assert.That(state.TryAddEffect(new ActiveEffect("item.boost", FactorySpeed, Start, Start + 1_000)), Is.EqualTo(RunStateResult.Ok));
            RunSession session = NewSession(catalog, state);

            AssertAdvance(session, TicksPerSecond / 2);
            Assert.That(session.StructureRevision, Is.Zero, "the effect is still running");
            AssertAdvance(session, TicksPerSecond / 2);

            Assert.That(session.StructureRevision, Is.EqualTo(1));
            Assert.That(session.QuantityRevision, Is.Zero, "no machine, no stock: no quantity changed");
        }

        [Test]
        public void ContractExpiryAndOverdriveStatusChanges_AreStructural()
        {
            RunCatalog catalog = TimerCatalog();
            RunState state = RunSimHarness.NewState(catalog);
            Assert.That(state.Contracts.TryAccept("contract.timed-bar", Start), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Overdrive.TryActivate(Start), Is.EqualTo(RunStateResult.Ok));
            RunSession session = NewSession(catalog, state);

            AssertAdvance(session, 29_000 * RunSession.TicksPerMs);
            Assert.That(session.StructureRevision, Is.Zero, "still inside every phase");
            AssertAdvance(session, 2_000 * RunSession.TicksPerMs);
            Assert.That(session.StructureRevision, Is.EqualTo(1), "Overdrive left its active phase");
            AssertAdvance(session, 10_000 * RunSession.TicksPerMs);
            Assert.That(session.StructureRevision, Is.EqualTo(2), "the contract expired at 40 s + 1 ms");
            AssertAdvance(session, RunSession.TicksPerMs);
            Assert.That(session.StructureRevision, Is.EqualTo(2));
            Assert.That(session.QuantityRevision, Is.Zero);
        }

        [Test]
        public void ACommand_ChangesEachRevisionOnlyForItsKindOfChange()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunSession session = NewSession(catalog, RunSimHarness.NewState(catalog));

            // Structural only: a setting.
            session.BeginCommand();
            Assert.That(session.State.TrySetAutoSell("iron.bar", true), Is.EqualTo(RunStateResult.Ok));
            Assert.That(session.EndCommand(0, true).IsSuccess, Is.True);
            Assert.That(session.StructureRevision, Is.EqualTo(1));
            Assert.That(session.QuantityRevision, Is.Zero);

            // Quantity only: Cash.
            session.BeginCommand();
            Assert.That(session.State.TryAddCash(5), Is.EqualTo(RunStateResult.Ok));
            Assert.That(session.EndCommand(0, false).IsSuccess, Is.True);
            Assert.That(session.StructureRevision, Is.EqualTo(1));
            Assert.That(session.QuantityRevision, Is.EqualTo(1));

            // Both: a machine costs Cash and adds a machine.
            session.BeginCommand();
            var miner = new MachineInstance(RunSimHarness.Id(1), catalog.Machines["machine.miner"], 0, 1, true, 0,
                catalog.Recipes["recipe.mine-iron-ore"].DurationTicks, 10);
            Assert.That(session.State.TryBuildMachine(miner, 10), Is.EqualTo(RunStateResult.Ok));
            Assert.That(session.EndCommand(0, true).IsSuccess, Is.True);
            Assert.That(session.StructureRevision, Is.EqualTo(2));
            Assert.That(session.QuantityRevision, Is.EqualTo(2));
        }

        [Test]
        public void ARejectedAdvance_ChangesNeitherRevisionNorTheTimeline()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState state = Factory(catalog, false);
            RunSession session = NewSession(catalog, state);
            AssertAdvance(session, TicksPerSecond);
            string before = RunSimHarness.Fingerprint(state);
            long structure = session.StructureRevision, quantity = session.QuantityRevision, ticks = session.ActiveTicksSinceAnchor;

            RunCommandResult<long> negative = session.Advance(-1);

            Assert.That(negative.IsSuccess, Is.False);
            Assert.That(negative.ReasonCode, Is.EqualTo(RunReasons.CommandInvalid));
            Assert.That(RunSimHarness.Fingerprint(state), Is.EqualTo(before));
            Assert.That(session.StructureRevision, Is.EqualTo(structure));
            Assert.That(session.QuantityRevision, Is.EqualTo(quantity));
            Assert.That(session.ActiveTicksSinceAnchor, Is.EqualTo(ticks));
        }

        // ------------------------------------------------------------------------------------------------
        // Settle after a command (R01 steps 3-5)
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Settle_SellsAutoSellStockFirst_ThenEvaluatesTheRunPredicate()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState state = RunSimHarness.NewState(catalog, 25);
            RunSimHarness.Add(state, "machine.press", 0, 1, progress: 1_000_000);
            RunSimHarness.Stock(state, "iron.bar", 4);
            RunSession session = NewSession(catalog, state);
            var views = new MachineStateView[4];

            Assert.That(session.CopyMachineStates(views), Is.EqualTo(1));
            Assert.That(views[0].Phase, Is.EqualTo(MachinePhase.Running), "four Bars feed the Press before the command");

            long ticks = session.ActiveTicksSinceAnchor;
            session.BeginCommand();
            Assert.That(state.TrySetAutoSell("iron.bar", true), Is.EqualTo(RunStateResult.Ok));
            RunCommandResult<int> result = session.EndCommand(7, true);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(7));
            Assert.That(state.Cash, Is.EqualTo(25 + 4 * 3), "auto-sell just enabled sells the whole Bar stock at once");
            Assert.That(state.Inventory.Units("iron.bar"), Is.Zero);
            Assert.That(session.CopyMachineStates(views), Is.EqualTo(1));
            Assert.That(views[0].Phase, Is.EqualTo(MachinePhase.Paused), "the run predicate sees the stock after the sale");
            Assert.That(views[0].Blocker, Is.EqualTo(MachineBlocker.MissingInput));
            Assert.That(session.ActiveTicksSinceAnchor, Is.EqualTo(ticks), "a command does not advance time");
            Assert.That(session.QuantityRevision, Is.EqualTo(1));
        }

        [Test]
        public void Settle_CompletesAWaitingMachineWhoseInputsJustArrived()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState state = RunSimHarness.NewState(catalog);
            RunSimHarness.Add(state, "machine.smelter", 0, 1, progress: 20_000_000);
            RunSession session = NewSession(catalog, state);
            var views = new MachineStateView[2];
            session.CopyMachineStates(views);
            Assert.That(views[0].Phase, Is.EqualTo(MachinePhase.Waiting));
            Assert.That(views[0].Blocker, Is.EqualTo(MachineBlocker.MissingInput));

            session.BeginCommand();
            RunSimHarness.Stock(state, "iron.ore", 2);
            session.EndCommand(0, false);

            Assert.That(state.Inventory.Units("iron.bar"), Is.EqualTo(1));
            Assert.That(state.Inventory.Units("iron.ore"), Is.Zero);
            Assert.That(state.Machines[0].ProgressTicks, Is.Zero, "the machine completed and restarted");
            session.CopyMachineStates(views);
            Assert.That(views[0].Phase, Is.EqualTo(MachinePhase.Paused));
        }

        [Test]
        public void Settle_ExpiresADueBoundaryAtTheCommandTimestamp()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState state = RunSimHarness.NewState(catalog);
            Assert.That(state.TryAddEffect(new ActiveEffect("item.boost", FactorySpeed, Start, Start + 60_000)), Is.EqualTo(RunStateResult.Ok));
            RunSession session = NewSession(catalog, state);
            session.RebindAnchor(Start + 60_000);

            session.BeginCommand();
            session.EndCommand(0, false);

            Assert.That(state.Effects.Count, Is.Zero);
            Assert.That(session.StructureRevision, Is.EqualTo(1), "the expiry is a structural change");
        }

        // ------------------------------------------------------------------------------------------------
        // value.limit and lifecycle.blocked
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void AnOverflowingAdvance_ReturnsValueLimit_AndChangesNothing()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState state = RunSimHarness.NewState(catalog, ExactBound.Value - 1);
            RunSimHarness.Add(state, "machine.miner", 0, 1, progress: 10_000_000);
            Assert.That(state.TrySetAutoSell("iron.ore", true), Is.EqualTo(RunStateResult.Ok));
            RunSession session = NewSession(catalog, state);
            string before = RunSimHarness.Fingerprint(state);

            RunCommandResult<long> result = session.Advance(2 * TicksPerSecond);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(RunCommandOutcome.Rejected));
            Assert.That(result.ReasonCode, Is.EqualTo(RunReasons.ValueLimit), "the sale would leave the exact bound");
            Assert.That(RunSimHarness.Fingerprint(state), Is.EqualTo(before), "an overflowing advance is atomic");
            Assert.That(session.ActiveTicksSinceAnchor, Is.Zero);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(Start));
            Assert.That(session.StructureRevision, Is.Zero);
            Assert.That(session.QuantityRevision, Is.Zero);

            // The session stays usable: an advance that does not reach the sale succeeds.
            AssertAdvance(session, TicksPerSecond / 2);
        }

        [Test]
        public void ATimelineBeyondTheExactBound_ReturnsValueLimit_AndChangesNothing()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState state = Factory(catalog, false);
            RunSession session = NewSession(catalog, state, ExactBound.Value - 5);
            string before = RunSimHarness.Fingerprint(state);

            RunCommandResult<long> result = session.Advance(100_000);

            Assert.That(result.ReasonCode, Is.EqualTo(RunReasons.ValueLimit));
            Assert.That(RunSimHarness.Fingerprint(state), Is.EqualTo(before));
            Assert.That(session.ActiveTicksSinceAnchor, Is.Zero);
            Assert.That(session.QuantityRevision, Is.Zero);
            AssertAdvance(session, 40_000);
        }

        [Test]
        public void ABlockedLifecycle_RejectsAdvance_AndChangesNothing()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState state = Factory(catalog, false);
            RunSession session = NewSession(catalog, state);
            string before = RunSimHarness.Fingerprint(state);

            session.SetLifecycleBlocked(true);
            RunCommandResult<long> blocked = session.Advance(TicksPerSecond);

            Assert.That(session.IsLifecycleBlocked, Is.True);
            Assert.That(blocked.ReasonCode, Is.EqualTo(RunReasons.LifecycleBlocked));
            Assert.That(RunSimHarness.Fingerprint(state), Is.EqualTo(before));
            Assert.That(session.QuantityRevision, Is.Zero);

            session.SetLifecycleBlocked(false);
            AssertAdvance(session, TicksPerSecond);
        }

        // ------------------------------------------------------------------------------------------------
        // Reason codes
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void ReasonCodes_AreTheTwentyFourStableCodesOfTheContract()
        {
            string[] expected =
            {
                "slot.occupied", "slot.unavailable", "machine.locked", "cash.insufficient", "level.max",
                "confirmation.required", "priority.not-permutation", "resource.locked", "contract.active-exists",
                "contract.none-active", "contract.expired", "stock.insufficient", "item.none", "item.target.invalid",
                "effect.conflict", "research.prerequisite", "rp.insufficient", "overdrive.cooling-down",
                "overdrive.active", "ip.insufficient", "prestige.below-threshold", "transition.pending", "value.limit",
                "lifecycle.blocked",
            };
            Assert.That(RunReasons.All, Is.EqualTo(expected));
            Assert.That(RunReasons.All.Distinct().Count(), Is.EqualTo(24));
            Assert.That(RunReasons.All, Does.Not.Contain(RunReasons.CommandInvalid));
        }

        [Test]
        public void EveryDomainResult_MapsToACodeAndTheDocumentedOnesToTheirOwn()
        {
            var documented = new Dictionary<RunStateResult, string>
            {
                { RunStateResult.SlotOccupied, "slot.occupied" },
                { RunStateResult.SlotUnavailable, "slot.unavailable" },
                { RunStateResult.CashInsufficient, "cash.insufficient" },
                { RunStateResult.LevelMax, "level.max" },
                { RunStateResult.NotPermutation, "priority.not-permutation" },
                { RunStateResult.ResourceLocked, "resource.locked" },
                { RunStateResult.ContractActiveExists, "contract.active-exists" },
                { RunStateResult.ContractNoneActive, "contract.none-active" },
                { RunStateResult.ContractExpired, "contract.expired" },
                { RunStateResult.StockInsufficient, "stock.insufficient" },
                { RunStateResult.ItemNone, "item.none" },
                { RunStateResult.EffectConflict, "effect.conflict" },
                { RunStateResult.RpInsufficient, "rp.insufficient" },
                { RunStateResult.OverdriveCoolingDown, "overdrive.cooling-down" },
                { RunStateResult.OverdriveActive, "overdrive.active" },
                { RunStateResult.IpInsufficient, "ip.insufficient" },
                { RunStateResult.ValueLimit, "value.limit" },
                { RunStateResult.CapacityExceeded, "storage.full" },
            };
            Assert.That(RunReasons.From(RunStateResult.Ok), Is.Null);
            foreach (RunStateResult result in Enum.GetValues(typeof(RunStateResult)).Cast<RunStateResult>())
            {
                if (result == RunStateResult.Ok) continue;
                string code = RunReasons.From(result);
                Assert.That(code, Is.Not.Null.And.Not.Empty, result.ToString());
                if (documented.TryGetValue(result, out string expected)) Assert.That(code, Is.EqualTo(expected), result.ToString());
                else Assert.That(code, Is.EqualTo(RunReasons.CommandInvalid), result + " has no code of its own");
            }
        }

        [Test]
        public void SimulatorResults_MapToValueLimitAndCommandInvalid()
        {
            Assert.That(RunReasons.From(RunAdvanceResult.Ok), Is.Null);
            Assert.That(RunReasons.From(RunAdvanceResult.ArithmeticOverflow), Is.EqualTo(RunReasons.ValueLimit));
            Assert.That(RunReasons.From(RunAdvanceResult.InvalidArgument), Is.EqualTo(RunReasons.CommandInvalid));
        }

        [Test]
        public void RunCommandResult_CarriesTheOutcomeTheCodeAndTheValue()
        {
            RunCommandResult<int> ok = RunCommandResult.Success(42);
            Assert.That(ok.IsSuccess, Is.True);
            Assert.That(ok.Outcome, Is.EqualTo(RunCommandOutcome.Success));
            Assert.That(ok.ReasonCode, Is.Null);
            Assert.That(ok.Value, Is.EqualTo(42));

            RunCommandResult<int> rejected = RunCommandResult.Reject<int>(RunReasons.ItemTargetInvalid);
            Assert.That(rejected.IsSuccess, Is.False);
            Assert.That(rejected.Outcome, Is.EqualTo(RunCommandOutcome.Rejected));
            Assert.That(rejected.ReasonCode, Is.EqualTo("item.target.invalid"));
            Assert.That(rejected.Value, Is.Zero);
            Assert.Throws<ArgumentException>(() => RunCommandResult.Reject<int>(null));
        }

        // ------------------------------------------------------------------------------------------------
        // Hot reads
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void HotReads_ReportCashRpInventoryAndMachineStates()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState state = RunSimHarness.NewState(catalog, 123);
            RunSimHarness.Add(state, "machine.miner", 0, 1, progress: 10_000_000);
            RunSimHarness.Add(state, "machine.smelter", 1, 2, active: false);
            RunSimHarness.Add(state, "machine.press", 2, 3, progress: 40_000_000);
            RunSimHarness.Add(state, "machine.assembler", 3, 4);
            RunSimHarness.Stock(state, "iron.ore", 4);
            Assert.That(state.TryAddRp(9), Is.EqualTo(RunStateResult.Ok));
            RunSession session = NewSession(catalog, state);
            AssertAdvance(session, 0);

            Assert.That(session.ReadCash(), Is.EqualTo(123));
            Assert.That(session.ReadRp(), Is.EqualTo(9));

            var stock = new long[8];
            Assert.That(session.CopyInventory(stock), Is.EqualTo(4));
            for (int i = 0; i < session.InventoryCount; i++)
                Assert.That(stock[i], Is.EqualTo(state.Inventory.Units(session.InventoryId(i))), session.InventoryId(i));
            Assert.That(session.CopyInventory(new long[2]), Is.EqualTo(4), "a short buffer is filled and the full count is reported");

            var views = new MachineStateView[8];
            Assert.That(session.CopyMachineStates(views), Is.EqualTo(4));
            Assert.That(session.CopyMachineStates(new MachineStateView[2]), Is.EqualTo(4));

            MachineStateView miner = views[0];
            Assert.That(session.MachineTypeId(miner.MachineTypeIndex), Is.EqualTo("machine.miner"));
            Assert.That(miner.SlotIndex, Is.EqualTo(0));
            Assert.That(miner.Level, Is.EqualTo(1));
            Assert.That(miner.IsActive, Is.True);
            Assert.That(miner.Phase, Is.EqualTo(MachinePhase.Running));
            Assert.That(miner.Blocker, Is.EqualTo(MachineBlocker.None));
            Assert.That(miner.BatchFactor, Is.EqualTo(1));
            Assert.That(miner.ProgressPpm, Is.EqualTo(500_000));

            Assert.That(views[1].Phase, Is.EqualTo(MachinePhase.Paused));
            Assert.That(views[1].Blocker, Is.EqualTo(MachineBlocker.Inactive));
            Assert.That(views[1].IsActive, Is.False);
            Assert.That(session.MachineTypeId(views[1].MachineTypeIndex), Is.EqualTo("machine.smelter"));

            Assert.That(views[2].Phase, Is.EqualTo(MachinePhase.Waiting));
            Assert.That(views[2].Blocker, Is.EqualTo(MachineBlocker.MissingInput));
            Assert.That(views[2].ProgressPpm, Is.EqualTo(1_000_000));

            Assert.That(views[3].Phase, Is.EqualTo(MachinePhase.Paused));
            Assert.That(views[3].Blocker, Is.EqualTo(MachineBlocker.MissingInput));
            Assert.That(views[3].ProgressPpm, Is.Zero);
            Assert.That(session.MachineTypeCount, Is.EqualTo(4));
        }

        [Test]
        public void MachineState_ReportsAFullStoreOnlyWhenNotEvenOneUnitFits()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState state = RunSimHarness.NewState(catalog);
            RunSimHarness.Add(state, "machine.miner", 0, 1);
            RunSimHarness.Stock(state, "iron.ore", 1000);
            RunSession session = NewSession(catalog, state);
            var views = new MachineStateView[2];

            session.CopyMachineStates(views);

            Assert.That(views[0].Phase, Is.EqualTo(MachinePhase.Paused));
            Assert.That(views[0].Blocker, Is.EqualTo(MachineBlocker.StorageFull));
        }

        [Test]
        public void MachineState_ReportsTheBatchFactorOfTheComposedSpeed()
        {
            var options = new RunSimHarness.CatalogOptions
            {
                RecipeDurations = new long[] { 1_000_000, 20_000_000, 40_000_000, 50_000_000 },
            };
            RunCatalog catalog = RunSimHarness.Catalog(options);
            RunState state = RunSimHarness.NewState(catalog);
            RunSimHarness.Add(state, "machine.miner", 0, 1);
            RunSession session = NewSession(catalog, state);
            var views = new MachineStateView[2];

            session.CopyMachineStates(views);
            Assert.That(views[0].BatchFactor, Is.EqualTo(1));

            session.BeginCommand();
            Assert.That(state.Overdrive.TryActivate(session.TimelineUtcMs), Is.EqualTo(RunStateResult.Ok));
            session.EndCommand(0, true);
            session.CopyMachineStates(views);
            Assert.That(views[0].BatchFactor, Is.EqualTo(2), "double speed at the minimum cycle doubles the batch");

            AssertAdvance(session, 31_000 * RunSession.TicksPerMs);
            session.CopyMachineStates(views);
            Assert.That(views[0].BatchFactor, Is.EqualTo(1), "Overdrive ended");
        }

        // ------------------------------------------------------------------------------------------------
        // Allocations (SC-008)
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void HotReads_HaveZeroGcAllocSamples()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunSession session = NewSession(catalog, Factory(catalog, false));
            var stock = new long[8];
            var views = new MachineStateView[8];
            long cash = 0, rp = 0;
            int inventory = 0, machines = 0;

            // Warm-up: production reaches every resource, the buffers grow, JIT compiles the paths.
            for (int i = 0; i < 40; i++)
            {
                AssertAdvance(session, TicksPerSecond);
                session.ReadCash();
                session.CopyInventory(stock);
                session.CopyMachineStates(views);
            }

            var counts = new int[6];
            for (int i = 0; i < counts.Length; i++)
            {
                AssertAdvance(session, TicksPerSecond); // dirties the machine read model; the copy below rebuilds it
                counts[i] = GcAllocationCounter.Count(() =>
                {
                    cash = session.ReadCash();
                    rp = session.ReadRp();
                    inventory = session.CopyInventory(stock);
                    machines = session.CopyMachineStates(views);
                });
            }

            Assert.That(counts, Is.All.Zero, "GC.Alloc samples per hot read set (ReadCash, ReadRp, CopyInventory, CopyMachineStates)");
            Assert.That(cash, Is.GreaterThan(0));
            Assert.That(rp, Is.Zero);
            Assert.That(inventory, Is.EqualTo(4));
            Assert.That(machines, Is.EqualTo(5));
        }

        [Test]
        public void WarmedAdvance_HasZeroGcAllocSamples_IncludingTheBoundaryEventPath()
        {
            var options = new RunSimHarness.CatalogOptions
            {
                BoostItemDurationMs = 500,
                OverdriveDurationMs = 300,
                OverdriveCooldownMs = 400,
                TimedContractDurationMs = 300,
            };
            RunCatalog catalog = RunSimHarness.Catalog(options);
            RunState state = Factory(catalog, false);
            RunSession session = NewSession(catalog, state);

            // Each interval of one second brings an effect end, an Overdrive end, its cooldown end and a contract
            // expiry, so the event list is filled and processed inside every measured advance.
            void Prepare()
            {
                long now = session.TimelineUtcMs;
                session.BeginCommand();
                Assert.That(state.TryAddEffect(new ActiveEffect("item.boost", FactorySpeed, now, now + 500)), Is.EqualTo(RunStateResult.Ok));
                Assert.That(state.Overdrive.TryActivate(now), Is.EqualTo(RunStateResult.Ok));
                Assert.That(state.Contracts.TryAccept("contract.timed-bar", now), Is.EqualTo(RunStateResult.Ok));
                Assert.That(session.EndCommand(0, true).IsSuccess, Is.True);
            }

            for (int i = 0; i < 40; i++)
            {
                Prepare();
                AssertAdvance(session, TicksPerSecond);
            }

            var counts = new int[6];
            for (int i = 0; i < counts.Length; i++)
            {
                Prepare();
                Assert.That(state.Effects.Count, Is.EqualTo(1));
                RunCommandResult<long> result = default;
                counts[i] = GcAllocationCounter.Count(() => result = session.Advance(TicksPerSecond));
                Assert.That(result.IsSuccess, Is.True, result.ReasonCode);
                Assert.That(state.Effects.Count, Is.Zero, "the effect event was processed");
                Assert.That(state.Contracts.Active, Is.Null, "the contract event was processed");
                Assert.That(state.Overdrive.CooldownUntilUtcMs, Is.Null, "the Overdrive events were processed");
            }

            Assert.That(counts, Is.All.Zero, "GC.Alloc samples per warmed session Advance with effect, Overdrive and contract events");
        }

        [Test]
        public void WarmedAdvance_OnTheW003Workload_HasZeroGcAllocSamples()
        {
            W003Scenario sc = W003Workload.Build(W003Variant.Reference);
            var session = new RunSession(sc.Catalog, sc.State, sc.Permanent, sc.StartUtcMs);
            for (int i = 0; i < 40; i++) AssertAdvance(session, TicksPerSecond);

            var counts = new int[6];
            for (int i = 0; i < counts.Length; i++)
            {
                RunCommandResult<long> result = default;
                counts[i] = GcAllocationCounter.Count(() => result = session.Advance(TicksPerSecond));
                Assert.That(result.IsSuccess, Is.True, result.ReasonCode);
            }

            Assert.That(counts, Is.All.Zero);
        }
    }
}
