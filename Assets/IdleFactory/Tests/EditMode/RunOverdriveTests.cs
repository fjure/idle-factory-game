using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// ActivateOverdrive and the OverdriveStatus (T101, contracts/run-application.md, FR-017, US3 scenario 2): the
    /// unlock gate on the reserved ID (T140 handoff), the timing of activeUntil and cooldownUntil at the exact
    /// instants, no reactivation while active, the speed effect and its rescale at both edges, the status read
    /// model, and atomic rejection with the structural flags. The fixture Overdrive lasts 30 s, cools down for 300 s
    /// and doubles the speed.
    /// </summary>
    public sealed class RunOverdriveTests
    {
        private const long Ms = RunSession.TicksPerMs;
        private const long Start = RunCommandFixtures.StartUtcMs;

        private static RunCommandFixtures.Rig Rig(bool locked = false, long cash = 100_000, int factoryLevel = 1, bool withMiner = false)
        {
            var o = new RunCommandFixtures.Options { OverdriveLocked = locked };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(o, cash, factoryLevel: factoryLevel);
            rig.Advance(0);
            if (withMiner) Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            return rig;
        }

        private static OverdriveActivationResult Activate(RunCommandFixtures.Rig rig)
        {
            RunCommandResult<OverdriveActivationResult> r = rig.Commands.ActivateOverdrive();
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            return r.Value;
        }

        private static string Full(RunCommandFixtures.Rig rig) =>
            RunCommandFixtures.Snapshot(rig.State) + "cash=" + rig.State.Cash + " od=" + rig.State.Overdrive.ActiveUntilUtcMs + "/" +
            rig.State.Overdrive.CooldownUntilUtcMs + " tl=" + rig.Session.ActiveTicksSinceAnchor;

        private static void AssertRejected(RunCommandFixtures.Rig rig, string code)
        {
            string before = Full(rig);
            long structure = rig.Session.StructureRevision, quantity = rig.Session.QuantityRevision;

            RunCommandResult<OverdriveActivationResult> result = rig.Commands.ActivateOverdrive();

            Assert.That(result.IsSuccess, Is.False, "expected " + code);
            Assert.That(result.ReasonCode, Is.EqualTo(code));
            Assert.That(result.Value, Is.EqualTo(default(OverdriveActivationResult)));
            Assert.That(Full(rig), Is.EqualTo(before), "a rejection never changes state");
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure));
            Assert.That(rig.Session.QuantityRevision, Is.EqualTo(quantity));
        }

        [Test]
        public void Activate_SetsActiveUntilAndCooldownUntil_FromTheTimeline()
        {
            var rig = Rig();
            rig.Advance(5_000 * Ms);
            long structure = rig.Session.StructureRevision;

            OverdriveActivationResult r = Activate(rig);

            long now = Start + 5_000;
            Assert.That(r.StartUtcMs, Is.EqualTo(now));
            Assert.That(r.ActiveUntilUtcMs, Is.EqualTo(now + 30_000));
            Assert.That(r.CooldownUntilUtcMs, Is.EqualTo(now + 30_000 + 300_000), "start + duration + cooldown");
            Assert.That(rig.State.Overdrive.ActiveUntilUtcMs, Is.EqualTo(r.ActiveUntilUtcMs));
            Assert.That(rig.State.Overdrive.CooldownUntilUtcMs, Is.EqualTo(r.CooldownUntilUtcMs));
            Assert.That(rig.State.Overdrive.IsConsistent, Is.True);
            Assert.That(rig.Session.StructureRevision, Is.GreaterThan(structure));
        }

        [Test]
        public void Activate_WhileActive_IsOverdriveActive_UntilTheLastMillisecond()
        {
            var rig = Rig();
            Activate(rig);

            AssertRejected(rig, RunReasons.OverdriveActive);
            rig.Advance(29_999 * Ms);
            AssertRejected(rig, RunReasons.OverdriveActive);
        }

        [Test]
        public void Activate_AtTheExactEndOfTheBoost_IsCoolingDown_UntilTheExactCooldownInstant()
        {
            var rig = Rig();
            OverdriveActivationResult first = Activate(rig);

            rig.Advance(30_000 * Ms);
            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(first.ActiveUntilUtcMs));
            AssertRejected(rig, RunReasons.OverdriveCoolingDown);

            rig.Advance(299_999 * Ms);
            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(first.CooldownUntilUtcMs - 1));
            AssertRejected(rig, RunReasons.OverdriveCoolingDown);

            rig.Advance(1 * Ms);
            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(first.CooldownUntilUtcMs));
            OverdriveActivationResult second = Activate(rig);
            Assert.That(second.StartUtcMs, Is.EqualTo(first.CooldownUntilUtcMs), "timeline >= cooldownUntil is enough");
            Assert.That(second.ActiveUntilUtcMs, Is.EqualTo(first.CooldownUntilUtcMs + 30_000));
        }

        [Test]
        public void Overdrive_HalvesTheCycleWhileActive_AndRestoresItAtTheExactEnd()
        {
            var rig = Rig(withMiner: true);
            MachineInstance miner = rig.State.FindMachine(rig.Id(1));
            rig.Advance(10_000_000);
            Assert.That(miner.ProgressTicks, Is.EqualTo(10_000_000));

            Activate(rig);
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(10_000_000), "x2");
            Assert.That(miner.ProgressTicks, Is.EqualTo(5_000_000), "the completed half is kept");

            rig.Advance(30_000 * Ms - 1 * Ms);
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(10_000_000), "still boosted one millisecond before the end");
            rig.Advance(1 * Ms);
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(20_000_000), "back to the normal cycle at the exact end");
        }

        [Test]
        public void Overdrive_ProducesDoubleOverItsWindow()
        {
            var rig = Rig(withMiner: true);
            Activate(rig);
            long ore = rig.State.Inventory.Units("iron.ore");

            rig.Advance(30_000 * Ms);

            Assert.That(rig.State.Inventory.Units("iron.ore") - ore, Is.EqualTo(30_000 * Ms / 10_000_000 * 2), "one cycle per second, 2 ore each");
        }

        [Test]
        public void Activate_WhileLocked_IsRejected_AndUnlocksOnceTheConditionHolds()
        {
            var rig = Rig(locked: true, cash: 1_000_000);
            Assert.That(rig.State.IsUnlocked(RunState.OverdriveUnlockId), Is.False);
            AssertRejected(rig, RunReasons.CommandInvalid);
            Assert.That(RunSnapshots.Overdrive(rig.Session).State, Is.EqualTo(OverdriveStage.Locked));

            for (int i = 0; i < 4; i++) Assert.That(rig.Commands.UpgradeFactory().IsSuccess, Is.True);

            Assert.That(rig.State.IsUnlocked(RunState.OverdriveUnlockId), Is.True, "factory level 5 unlocks it at the command settle");
            Activate(rig);
        }

        [Test]
        public void Activate_LifecycleBlocked_IsRejectedFirst()
        {
            var rig = Rig();
            rig.Session.SetLifecycleBlocked(true);

            AssertRejected(rig, RunReasons.LifecycleBlocked);
        }

        [Test]
        public void Activate_LifecycleBlocked_BeatsTheLockedGate()
        {
            var rig = Rig(locked: true);
            rig.Session.SetLifecycleBlocked(true);

            AssertRejected(rig, RunReasons.LifecycleBlocked);
        }

        [Test]
        public void Activate_WhenTheCooldownWouldExceedTheBound_IsValueLimit()
        {
            var rig = Rig();
            var late = new RunSession(rig.Catalog, rig.State, rig.Session.Permanent, ExactBound.Value - 100_000);
            var commands = new RunCommands(late);
            string before = Full(rig);

            RunCommandResult<OverdriveActivationResult> r = commands.ActivateOverdrive();

            Assert.That(r.IsSuccess, Is.False);
            Assert.That(r.ReasonCode, Is.EqualTo(RunReasons.ValueLimit));
            Assert.That(Full(rig), Is.EqualTo(before));
            Assert.That(rig.State.Overdrive.ActiveUntilUtcMs, Is.Null);
            Assert.That(late.StructureRevision, Is.Zero);
        }

        [Test]
        public void Status_FollowsReadyActiveCoolingDownAndReady()
        {
            var rig = Rig();
            OverdriveStatus ready = RunSnapshots.Overdrive(rig.Session);
            Assert.That(ready.State, Is.EqualTo(OverdriveStage.Ready));
            Assert.That(ready.CanActivate, Is.True);
            Assert.That(ready.ActivateReason, Is.Null);
            Assert.That((ready.DurationMs, ready.CooldownMs, ready.SpeedPpm), Is.EqualTo((30_000L, 300_000L, 2_000_000L)));

            Activate(rig);
            rig.Advance(10_000 * Ms);
            OverdriveStatus active = RunSnapshots.Overdrive(rig.Session);
            Assert.That(active.State, Is.EqualTo(OverdriveStage.Active));
            Assert.That(active.RemainingMs, Is.EqualTo(20_000));
            Assert.That(active.CanActivate, Is.False);
            Assert.That(active.ActivateReason, Is.EqualTo(RunReasons.OverdriveActive));

            rig.Advance(20_000 * Ms);
            OverdriveStatus cooling = RunSnapshots.Overdrive(rig.Session);
            Assert.That(cooling.State, Is.EqualTo(OverdriveStage.CoolingDown));
            Assert.That(cooling.RemainingMs, Is.EqualTo(300_000));
            Assert.That(cooling.ActivateReason, Is.EqualTo(RunReasons.OverdriveCoolingDown));

            rig.Advance(300_000 * Ms);
            OverdriveStatus again = RunSnapshots.Overdrive(rig.Session);
            Assert.That(again.State, Is.EqualTo(OverdriveStage.Ready));
            Assert.That(again.RemainingMs, Is.Zero);
            Assert.That(again.CanActivate, Is.True);
        }
    }
}
