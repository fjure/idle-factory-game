using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T307: the optional session-log port. The sink is optional, its absence changes nothing, a throwing sink
    /// changes nothing, and every event record carries only allowlisted fields (data-model "Session log").
    /// Reflection is used here only; product code never reflects.
    /// </summary>
    public sealed class RunEventSinkTests
    {
        private sealed class RecordingSink : IRunEventSink
        {
            public int Count;
            public readonly List<RunSampleEvent> Samples = new List<RunSampleEvent>();
            public void OnHeader(in RunHeaderEvent e) => Count++;
            public void OnSample(in RunSampleEvent e) { Count++; Samples.Add(e); }
            public void OnMilestone(in RunMilestoneEvent e) => Count++;
            public void OnSale(in RunSaleEvent e) => Count++;
            public void OnMining(in RunMiningEvent e) => Count++;
            public void OnContract(in RunContractEvent e) => Count++;
            public void OnPrestige(in RunPrestigeEvent e) => Count++;
            public void OnOfflineReturn(in RunOfflineReturnEvent e) => Count++;
            public void OnBottleneck(in RunBottleneckEvent e) => Count++;
            public void OnPurchase(in RunPurchaseEvent e) => Count++;
            public void OnClock(in RunClockEvent e) => Count++;
            public void OnLifecycle(in RunLifecycleEvent e) => Count++;
        }

        private sealed class ThrowingSink : IRunEventSink
        {
            public void OnHeader(in RunHeaderEvent e) => throw new InvalidOperationException("sink failure");
            public void OnSample(in RunSampleEvent e) => throw new InvalidOperationException("sink failure");
            public void OnMilestone(in RunMilestoneEvent e) => throw new InvalidOperationException("sink failure");
            public void OnSale(in RunSaleEvent e) => throw new InvalidOperationException("sink failure");
            public void OnMining(in RunMiningEvent e) => throw new InvalidOperationException("sink failure");
            public void OnContract(in RunContractEvent e) => throw new InvalidOperationException("sink failure");
            public void OnPrestige(in RunPrestigeEvent e) => throw new InvalidOperationException("sink failure");
            public void OnOfflineReturn(in RunOfflineReturnEvent e) => throw new InvalidOperationException("sink failure");
            public void OnBottleneck(in RunBottleneckEvent e) => throw new InvalidOperationException("sink failure");
            public void OnPurchase(in RunPurchaseEvent e) => throw new InvalidOperationException("sink failure");
            public void OnClock(in RunClockEvent e) => throw new InvalidOperationException("sink failure");
            public void OnLifecycle(in RunLifecycleEvent e) => throw new InvalidOperationException("sink failure");
        }

        private static void RaiseAll(IRunEventSink events)
        {
            var machines = new[] { new RunMachineCount(RunCommandFixtures.Miner, 1, 1) };
            events.OnHeader(new RunHeaderEvent(3, 1, RunBuildKind.Editor, RunPlatformClass.Desktop, 1, new RunSessionId(1UL, 2UL)));
            events.OnSample(new RunSampleEvent(10, 100, 50, machines, 1, 1));
            events.OnMilestone(new RunMilestoneEvent(RunMilestoneKind.FirstGear, 10));
            events.OnSale(new RunSaleEvent("iron.ore", 3, 9, true));
            events.OnMining(new RunMiningEvent("iron.ore", 4, 1, 0, 2));
            events.OnContract(new RunContractEvent(RunContractEventKind.Accept, "offer.a", 0, 5, 50, 1_000_000));
            events.OnPrestige(new RunPrestigeEvent(100, 90, 2, 5, new[] { new RunIndustryLevel("industry.a", 1) }));
            events.OnOfflineReturn(new RunOfflineReturnEvent(1, 1, false, 0, 0, 0, 0, false));
            events.OnBottleneck(new RunBottleneckEvent("iron.ore", 1, 2));
            events.OnPurchase(new RunPurchaseEvent(RunPurchaseKind.Build, RunCommandFixtures.Miner, 1, 10, 0));
            events.OnClock(new RunClockEvent(RunClockKind.Backward, -5, false));
            events.OnLifecycle(new RunLifecycleEvent(RunLifecycleKind.Start, 0));
        }

        /// <summary>The same commands on a fresh rig whose session uses <paramref name="sink"/> (null: the 4-argument constructor).</summary>
        private static string RunScenario(IRunEventSink sink, bool useFiveArgConstructor, bool raiseEvents)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            rig.Session = useFiveArgConstructor
                ? new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs, sink)
                : new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs);
            rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++));

            if (raiseEvents) RaiseAll(rig.Session.Events);
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            rig.Advance(30_000_000);
            if (raiseEvents) RaiseAll(rig.Session.Events);
            Assert.That(rig.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(rig.Build(1, RunCommandFixtures.Smelter).IsSuccess, Is.True);
            rig.Advance(50_000_000);
            if (raiseEvents) RaiseAll(rig.Session.Events);

            return RunCommandFixtures.Snapshot(rig.State) + "timeline=" + rig.Session.TimelineUtcMs + " ticks=" + rig.Session.ActiveTicksSinceAnchor +
                " rev=" + rig.Session.StructureRevision + "/" + rig.Session.QuantityRevision;
        }

        [Test]
        public void RunWithoutASinkEqualsNoOpAndRecordingSinks()
        {
            string none = RunScenario(null, false, raiseEvents: true);
            string explicitNull = RunScenario(null, true, raiseEvents: true);
            string noOp = RunScenario(NoOpRunEventSink.Instance, true, raiseEvents: true);
            var recording = new RecordingSink();
            string recorded = RunScenario(recording, true, raiseEvents: true);

            Assert.That(explicitNull, Is.EqualTo(none));
            Assert.That(noOp, Is.EqualTo(none));
            Assert.That(recorded, Is.EqualTo(none));
            Assert.That(recording.Count, Is.EqualTo(36), "12 records raised three times reach the recording sink");
        }

        [Test]
        public void RaisingNothingAtAllGivesTheSameRunAsRaisingEverything()
        {
            Assert.That(RunScenario(new RecordingSink(), true, raiseEvents: true), Is.EqualTo(RunScenario(null, false, raiseEvents: false)));
        }

        [Test]
        public void AThrowingSinkChangesNothingAndNeverPropagates()
        {
            string baseline = RunScenario(null, false, raiseEvents: false);
            string withThrowing = null;
            Assert.DoesNotThrow(() => withThrowing = RunScenario(new ThrowingSink(), true, raiseEvents: true));
            Assert.That(withThrowing, Is.EqualTo(baseline));
        }

        [Test]
        public void AThrowingSinkIsCountedAsDroppedEvents()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            var session = new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs, new ThrowingSink());
            RaiseAll(session.Events);
            Assert.That(session.DroppedEvents, Is.EqualTo(12));

            var healthy = new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs, new RecordingSink());
            RaiseAll(healthy.Events);
            Assert.That(healthy.DroppedEvents, Is.EqualTo(0));
        }

        [Test]
        public void EventsIsNeverNullAndDefaultsToANoOp()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            Assert.That(rig.Session.Events, Is.Not.Null);
            Assert.DoesNotThrow(() => RaiseAll(rig.Session.Events));
            Assert.That(rig.Session.DroppedEvents, Is.EqualTo(0));
        }

        [Test]
        public void CollectionsInRecordsAreCopiedAndReadOnly()
        {
            var source = new[] { new RunMachineCount("machine.miner", 2, 3) };
            var sample = new RunSampleEvent(1, 2, 3, source, 1, 1);
            source[0] = new RunMachineCount("machine.other", 9, 9);

            Assert.That(sample.Machines.Count, Is.EqualTo(1));
            Assert.That(sample.Machines[0].MachineId, Is.EqualTo("machine.miner"));
            Assert.That(sample.Machines is Array, Is.False, "a cast back to an array must not expose a writable store");
            Assert.That(((System.Collections.IList)sample.Machines).IsReadOnly, Is.True);

            var empty = new RunSampleEvent(1, 2, 3, null, 1, 1);
            Assert.That(empty.Machines, Is.Not.Null);
            Assert.That(empty.Machines.Count, Is.EqualTo(0));
            Assert.That(new RunPrestigeEvent(1, 1, 1, 1, null).IndustryPurchases.Count, Is.EqualTo(0));
        }

        private sealed class OomSink : IRunEventSink
        {
            public void OnHeader(in RunHeaderEvent e) { }
            public void OnSample(in RunSampleEvent e) { }
            public void OnMilestone(in RunMilestoneEvent e) { }
            public void OnSale(in RunSaleEvent e) { }
            public void OnMining(in RunMiningEvent e) { }
            public void OnContract(in RunContractEvent e) { }
            public void OnPrestige(in RunPrestigeEvent e) { }
            public void OnOfflineReturn(in RunOfflineReturnEvent e) { }
            public void OnBottleneck(in RunBottleneckEvent e) { }
            public void OnPurchase(in RunPurchaseEvent e) { }
            public void OnClock(in RunClockEvent e) { }
            public void OnLifecycle(in RunLifecycleEvent e) => throw new OutOfMemoryException();
        }

        [Test]
        public void OutOfMemoryPropagatesAndIsNotCountedAsDropped()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            var session = new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs, new OomSink());
            Assert.Throws<OutOfMemoryException>(() => session.Events.OnLifecycle(new RunLifecycleEvent(RunLifecycleKind.Start, 0)));
            Assert.That(session.DroppedEvents, Is.EqualTo(0));
        }

        // ---- allowlist ----

        private static IEnumerable<Type> EventTypes()
        {
            Assembly core = typeof(RunSession).Assembly;
            return core.GetTypes().Where(t => t.Namespace == "IdleFactory.Run.Application" &&
                (t.Name == nameof(RunMachineCount) || t.Name == nameof(RunSessionId) || t.Name == nameof(RunIndustryLevel) || (t.Name.StartsWith("Run", StringComparison.Ordinal) && t.Name.EndsWith("Event", StringComparison.Ordinal))) &&
                t.IsValueType && !t.IsEnum);
        }

        private static bool IsAllowedMember(string name, Type type)
        {
            if (type == typeof(long) || type == typeof(ulong) || type == typeof(RunSessionId) || type == typeof(int) || type == typeof(bool) || type.IsEnum) return true;
            if (type == typeof(string)) return name.EndsWith("Id", StringComparison.Ordinal);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                Type element = type.GetGenericArguments()[0];
                return element.IsValueType && !element.IsEnum && EventTypes().Contains(element);
            }
            return false;
        }

        [Test]
        public void EveryEventTypeIsAReadonlyStructOfAllowlistedMembersOnly()
        {
            List<Type> types = EventTypes().ToList();
            Assert.That(types.Count, Is.EqualTo(15), "12 session-log records plus the list element structs and the session id");
            var failures = new List<string>();
            foreach (Type t in types)
            {
                if (!t.IsValueType || !t.GetCustomAttributes(false).Any(a => a.GetType().Name == "IsReadOnlyAttribute"))
                    failures.Add(t.Name + " is not a readonly struct");
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    if (!f.IsInitOnly) failures.Add(t.Name + "." + f.Name + " is not readonly");
                foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (p.CanWrite) failures.Add(t.Name + "." + p.Name + " has a setter");
                    if (!IsAllowedMember(p.Name, p.PropertyType)) failures.Add(t.Name + "." + p.Name + " : " + p.PropertyType.Name + " is not allowlisted");
                }
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    // Auto-property backing fields are checked through their property above.
                    if (f.Name.StartsWith("<", StringComparison.Ordinal)) continue;
                    if (!IsAllowedMember(f.Name, f.FieldType)) failures.Add(t.Name + "." + f.Name + " : " + f.FieldType.Name + " is not allowlisted");
                }
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void SinkMethodsReturnNothingAndTakeOnlyEventRecordsByReadonlyReference()
        {
            foreach (MethodInfo m in typeof(IRunEventSink).GetMethods())
            {
                Assert.That(m.ReturnType, Is.EqualTo(typeof(void)), m.Name);
                ParameterInfo[] ps = m.GetParameters();
                Assert.That(ps.Length, Is.EqualTo(1), m.Name);
                Assert.That(ps[0].IsIn, Is.True, m.Name);
                Assert.That(EventTypes(), Does.Contain(ps[0].ParameterType.GetElementType()), m.Name);
            }
            Assert.That(typeof(IRunEventSink).GetMethods().Length, Is.EqualTo(12));
        }
    }
}
