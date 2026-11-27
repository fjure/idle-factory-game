using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation.Look;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode.F007
{
    /// <summary>
    /// T609 (research R19 amendment, QA-1, QA-2, ARCH-7, contracts/look-profile.md rule 1): a look profile is presentation only. The same scripted run (build,
    /// upgrade, auto-sell, Overdrive, an offline-length gap, a prestige) under F006 and under each other profile yields the same per-tick hash of the full
    /// authoritative state, the same tap results through <c>WorldTapResolver</c> and the same save bytes, also when the profile is switched in the middle of the
    /// run. A planted profile that changes the state is detected, and the <c>Look/</c> folder has no path to a simulation write API.
    /// </summary>
    public sealed class LookProfileDeterminismParityTests
    {
        private LookParityRig rig;
        private ParityOutcome baseline;

        [SetUp]
        public void SetUp()
        {
            rig = new LookParityRig();
            baseline = rig.Run(LookProfile.F006Id, -1, null, null);
        }

        [TearDown]
        public void TearDown() => rig.Dispose();

        private static void AssertSame(ParityOutcome expected, ParityOutcome actual, string what)
        {
            Assert.That(actual.TickHashes, Has.Count.EqualTo(expected.TickHashes.Count), what);
            for (var tick = 0; tick < expected.TickHashes.Count; tick++)
            {
                Assert.That(actual.TickHashes[tick], Is.EqualTo(expected.TickHashes[tick]), what + ": state hash of tick " + tick);
            }

            Assert.That(actual.Taps, Is.EqualTo(expected.Taps), what + ": tap results (location id sequences)");
            Assert.That(actual.MidRunSaveBytes, Is.EqualTo(expected.MidRunSaveBytes), what + ": save bytes in the middle of the run");
            Assert.That(actual.SaveBytes, Is.EqualTo(expected.SaveBytes), what + ": save bytes at the end");
        }

        [Test]
        public void TheScriptedRunExercisesBuildUpgradeAutoSellOverdriveGapAndPrestige()
        {
            Assert.That(baseline.TickHashes, Has.Count.EqualTo(LookParityRig.Ticks));
            Assert.That(baseline.Count("build"), Is.GreaterThanOrEqualTo(3), "machines were built");
            Assert.That(baseline.Count("upgrade"), Is.GreaterThanOrEqualTo(1), "a machine was upgraded");
            Assert.That(baseline.Count("autosell"), Is.EqualTo(1));
            Assert.That(baseline.Count("overdrive"), Is.GreaterThanOrEqualTo(1), "Overdrive ran; rejected: " + string.Join(", ", baseline.Reasons));
            Assert.That(baseline.Count("gap"), Is.EqualTo(1), "the offline-length gap was applied");
            Assert.That(baseline.Count("prestige"), Is.EqualTo(1), "the run prestiged once");
            Assert.That(baseline.TickHashes.Distinct().Count(), Is.EqualTo(baseline.TickHashes.Count), "the state moves on every tick");
            Assert.That(baseline.Taps.Any(tap => tap.Contains(":Machine:")), Is.True, "taps hit machines through WorldTapResolver");
            Assert.That(baseline.Taps.Any(tap => tap.Contains(":None:-1")), Is.True, "and a tap outside the world hits nothing");
        }

        [Test]
        public void TheHarnessIsDeterministicSoAMismatchCanOnlyComeFromTheProfile()
        {
            var again = rig.Run(LookProfile.F006Id, -1, null, null);
            AssertSame(baseline, again, "F006 twice");
        }

        [Test]
        public void EveryProfileYieldsTheSameStateHashesTapSequencesAndSaveBytesAsF006()
        {
            foreach (var id in new[] { LookProfile.PrototypeAId, LookProfile.PrototypeBId })
            {
                var other = rig.Run(id, -1, null, null);
                Assert.That(other.ActiveAtEnd, Is.EqualTo(id));
                AssertSame(baseline, other, id);
            }
        }

        [Test]
        public void AMidRunSwitchChangesNothingInTheSimulationAndRedrawsWithTheNewProfile()
        {
            var switched = rig.Run(LookProfile.F006Id, 20, LookProfile.PrototypeAId, null);

            Assert.That(switched.Applied, Is.EqualTo(new[] { LookProfile.PrototypeAId }));
            Assert.That(switched.ActiveAtEnd, Is.EqualTo(LookProfile.PrototypeAId));
            AssertSame(baseline, switched, "F006 to F007-A at tick 20");

            Assert.That(switched.MinerEntryAfterSwitch, Is.Not.Null, "the new profile carries the Miner body");
            Assert.That(switched.SlotZeroSpriteAfterSwitch, Is.Not.Null, "slot 0 draws a Miner after the switch");
            var frames = Enumerable.Range(0, switched.MinerEntryAfterSwitch.FrameCount).Select(switched.MinerEntryAfterSwitch.GetFrame);
            Assert.That(frames, Does.Contain(switched.SlotZeroSpriteAfterSwitch), "the world views drew the new profile's art on the very tick of the switch");
        }

        [Test]
        public void ASwitchBackAndForthIsPresentationOnlyToo()
        {
            var thereAndBack = rig.Run(LookProfile.F006Id, 30, LookProfile.PrototypeBId, null);
            AssertSame(baseline, thereAndBack, "F006 to F007-B at tick 30");
            var fromPrototype = rig.Run(LookProfile.PrototypeAId, 50, LookProfile.F006Id, null);
            Assert.That(fromPrototype.Applied, Is.EqualTo(new[] { LookProfile.F006Id }));
            AssertSame(baseline, fromPrototype, "F007-A to F006 at tick 50");
        }

        [Test]
        public void ANegativeControlWithAPlantedMutatingProfileIsDetected()
        {
            const int plantedTick = 30;

            // The planted profile plays a profile that changes the simulation: from tick 30 it switches auto-sell off, which a real profile must never do.
            void Planted(int tick, string activeId, RunSession session, RunCommands commands)
            {
                if (activeId == LookProfile.PrototypeBId && tick == plantedTick)
                {
                    Assert.That(commands.SetAutoSell("iron.ore", false).IsSuccess, Is.True);
                }
            }

            var mutated = rig.Run(LookProfile.PrototypeBId, -1, null, Planted);
            Assert.That(mutated.TickHashes[plantedTick - 1], Is.EqualTo(baseline.TickHashes[plantedTick - 1]), "identical until the mutation");
            Assert.That(mutated.TickHashes[plantedTick], Is.Not.EqualTo(baseline.TickHashes[plantedTick]), "the state hash catches the mutation on its tick");
            Assert.That(mutated.MidRunSaveBytes, Is.Not.EqualTo(baseline.MidRunSaveBytes), "and the save bytes differ");

            // The control is gated by the profile: the same hook under F006 leaves everything equal, so the detection is not an artefact of the harness.
            var unplanted = rig.Run(LookProfile.F006Id, -1, null, Planted);
            AssertSame(baseline, unplanted, "the planted hook under F006");
        }

        [Test]
        public void TheLookFolderHasNoPathToASimulationWriteApi()
        {
            var directory = Path.Combine(Application.dataPath, "IdleFactory", "Unity", "Presentation", "Look");
            var files = Directory.GetFiles(directory, "*.cs");
            Assert.That(files, Is.Not.Empty);
            var forbidden = new[]
            {
                "using IdleFactory.Run", "using IdleFactory.Core", "using IdleFactory.Domain", "IdleFactory.Run.", "IdleFactory.Core.",
                "RunCommands", "RunSession", "RunState", "PermanentState", "RunSimulator", "IRunCheckpointStore", "RunPersistence",
                "SaveV1", "SaveV2", "SaveV3", "Checkpoint", "System.IO", "persistentDataPath",
            };
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                foreach (var token in forbidden)
                {
                    Assert.That(text, Does.Not.Contain(token), Path.GetFileName(file) + " must not mention " + token);
                }
            }
        }

        [Test]
        public void NoMemberOfTheLookTypesTakesOrReturnsATypeOfTheSimulationAssembly()
        {
            var assembly = typeof(LookProfileResolver).Assembly;
            var lookTypes = assembly.GetTypes().Where(type => type.Namespace == typeof(LookProfileResolver).Namespace).ToArray();
            Assert.That(lookTypes.Length, Is.GreaterThanOrEqualTo(6));
            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var type in lookTypes)
            {
                var used = type.GetFields(All).Select(field => field.FieldType)
                    .Concat(type.GetProperties(All).Select(property => property.PropertyType))
                    .Concat(type.GetMethods(All).SelectMany(method => method.GetParameters().Select(p => p.ParameterType).Concat(new[] { method.ReturnType })));
                foreach (var used0 in used)
                {
                    var element = used0.HasElementType ? used0.GetElementType() : used0;
                    Assert.That(element.Assembly.GetName().Name, Is.Not.EqualTo("IdleFactory.Core"), type.Name + " uses " + element.FullName);
                }
            }
        }
    }
}
