using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>T540 (FR-035, research R14): milestone detection by snapshot diff, offline separation, and the copy with its fallback.</summary>
    public sealed class MilestoneWatcherTests
    {
        private static readonly Guid RunA = new Guid(1, 0, 0, new byte[8]);
        private static readonly Guid RunB = new Guid(2, 0, 0, new byte[8]);

        [Test]
        public void TheFirstObservationOnlySetsTheBaseline()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(3, 8, 12, RunA, 0);
            Assert.That(watcher.PendingCount, Is.Zero, "a start, a restore or a recovery never raises a milestone");
            Assert.That(watcher.TryDequeue(out _), Is.False);
        }

        [Test]
        public void ALevelUpRaisesOneMilestoneWithTheNewlyOpenedLocations()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(1, 4, 12, RunA, 0);
            watcher.Observe(1, 4, 12, RunA, 0);
            Assert.That(watcher.PendingCount, Is.Zero, "no change, no milestone");
            watcher.Observe(2, 6, 12, RunA, 0);
            Assert.That(watcher.TryDequeue(out var m), Is.True);
            Assert.That(m.Kind, Is.EqualTo(MilestoneKind.FactoryLevelUp));
            Assert.That(m.Level, Is.EqualTo(2));
            Assert.That(m.FirstNewLocation, Is.EqualTo(4));
            Assert.That(m.NewlyUnlockedCount, Is.EqualTo(2));
            Assert.That(m.StageRaised, Is.False, "6 of 12 is still Partial");
            Assert.That(m.ArisenOffline, Is.False);
            Assert.That(watcher.TryDequeue(out _), Is.False);

            watcher.Observe(3, 8, 12, RunA, 0);
            Assert.That(watcher.TryDequeue(out var raised), Is.True);
            Assert.That(raised.StageRaised, Is.True, "8 of 12 reaches Established");
            Assert.That(raised.Stage, Is.EqualTo(2));
        }

        [Test]
        public void ALevelUpWithoutNewLocationsHasNoneOpened()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(5, 12, 12, RunA, 0);
            watcher.Observe(6, 12, 12, RunA, 0);
            Assert.That(watcher.TryDequeue(out var m), Is.True);
            Assert.That(m.NewlyUnlockedCount, Is.Zero);
            Assert.That(m.FirstNewLocation, Is.EqualTo(-1));
            Assert.That(m.StageRaised, Is.False);
        }

        [Test]
        public void AFallingLevelOrTheSameLevelRaisesNothing()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(3, 8, 12, RunA, 0);
            watcher.Observe(2, 6, 12, RunA, 0);
            watcher.Observe(2, 6, 12, RunA, 0);
            Assert.That(watcher.PendingCount, Is.Zero);
            watcher.Observe(3, 8, 12, RunA, 0);
            Assert.That(watcher.PendingCount, Is.EqualTo(1), "the baseline followed the fall, so rising again is a level-up");
        }

        [Test]
        public void APrestigeIsARunChangeWithAHigherPrestigeCountAndRaisesNoLevelUp()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(5, 12, 12, RunA, 2);
            watcher.Observe(1, 4, 12, RunB, 3);
            Assert.That(watcher.PendingCount, Is.EqualTo(1), "the lower level of the new run is not a level-up and no level-up is raised");
            watcher.TryDequeue(out var m);
            Assert.That(m.Kind, Is.EqualTo(MilestoneKind.PrestigeCompleted));
            Assert.That(m.NewlyUnlockedCount, Is.Zero);
            Assert.That(m.StageRaised, Is.False);

            watcher.Observe(2, 6, 12, RunB, 3);
            Assert.That(watcher.TryDequeue(out var next), Is.True, "the new run is the baseline: its first level-up is detected");
            Assert.That(next.Kind, Is.EqualTo(MilestoneKind.FactoryLevelUp));
        }

        [Test]
        public void ARunChangeWithoutAHigherPrestigeCountIsARestoreAndRaisesNothing()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(5, 12, 12, RunA, 2);
            watcher.Observe(1, 4, 12, RunB, 2);
            Assert.That(watcher.PendingCount, Is.Zero, "a restored or recovered run is not a prestige");
        }

        [Test]
        public void AnUnknownPrestigeCountLetsTheRunChangeAlone()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(5, 12, 12, RunA);
            watcher.Observe(1, 4, 12, RunB);
            watcher.TryDequeue(out var m);
            Assert.That(m.Kind, Is.EqualTo(MilestoneKind.PrestigeCompleted));
        }

        [Test]
        public void AnOfflineMilestoneIsReturnedFlaggedAndNeverQueuedForTheBanner()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(1, 4, 12, RunA, 0);
            var found = watcher.ReconcileOffline(2, 6, 12, RunA, 0);
            Assert.That(found.Count, Is.EqualTo(1));
            Assert.That(found[0].ArisenOffline, Is.True);
            Assert.That(found[0].Level, Is.EqualTo(2));
            Assert.That(watcher.PendingCount, Is.Zero, "no banner burst after an offline return");

            watcher.Observe(2, 6, 12, RunA, 0);
            Assert.That(watcher.PendingCount, Is.Zero, "the baseline moved: the next live snapshot is not a second milestone");
            Assert.That(watcher.ReconcileOffline(2, 6, 12, RunA, 0), Is.Empty, "an unchanged return lists nothing");
        }

        [Test]
        public void ABurstNeverGrowsTheQueueBeyondItsLimit()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(1, 4, 100, RunA, 0);
            for (var level = 2; level <= 20; level++)
            {
                watcher.Observe(level, 4 + level, 100, RunA, 0);
            }

            Assert.That(watcher.PendingCount, Is.EqualTo(MilestoneWatcher.MaxQueued));
            watcher.TryDequeue(out var oldest);
            Assert.That(oldest.Level, Is.EqualTo(20 - MilestoneWatcher.MaxQueued + 1), "the oldest were dropped");
        }

        [Test]
        public void TheStageRaisedFlagFollowsTheOffsetStageRuleOfTheShippedFactory()
        {
            // u0 = 4 of 12: the levels 1 to 5 open 4, 6, 8, 10 and 12 locations, so the stage rises at level 3 (Partial) and at level 4 (Established).
            var watcher = new MilestoneWatcher();
            watcher.Observe(1, 4, 12, RunA, 0, 4);
            var raised = new List<bool>();
            foreach (var (level, unlocked) in new[] { (2, 6), (3, 8), (4, 10), (5, 12) })
            {
                watcher.Observe(level, unlocked, 12, RunA, 0, 4);
                Assert.That(watcher.TryDequeue(out var m), Is.True);
                raised.Add(m.StageRaised);
            }

            Assert.That(raised, Is.EqualTo(new[] { false, true, true, false }));
        }

        [Test]
        public void AResetRebaselinesSoARestoredHigherLevelOfTheSameRunRaisesNoBanner()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(1, 4, 12, RunA, 0, 4);
            watcher.Reset();
            watcher.Observe(4, 10, 12, RunA, 0, 4);
            Assert.That(watcher.PendingCount, Is.Zero, "a load or a restore that carries a higher factory level in the same run is not a level-up");
            watcher.Observe(5, 12, 12, RunA, 0, 4);
            Assert.That(watcher.PendingCount, Is.EqualTo(1), "play after the restore is detected again");
        }

        [Test]
        public void AResetRebaselinesSoAnotherRunWithAHigherPrestigeCountRaisesNoBanner()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(5, 12, 12, RunA, 2, 4);
            watcher.Reset();
            watcher.Observe(1, 4, 12, RunB, 5, 4);
            Assert.That(watcher.PendingCount, Is.Zero, "a restore of a later run is not a prestige the player completed");
            Assert.That(watcher.BaselinePrestigeCount, Is.EqualTo(5));
        }

        [Test]
        public void WithoutAResetTheSameRestoreWouldHaveBeenTakenForAMilestone()
        {
            var watcher = new MilestoneWatcher();
            watcher.Observe(1, 4, 12, RunA, 0, 4);
            watcher.Observe(4, 10, 12, RunA, 0, 4);
            Assert.That(watcher.PendingCount, Is.EqualTo(1), "this is the false banner the lifecycle re-baseline prevents");
        }

        [Test]
        public void TheLifecycleRebasesOnAColdStartAndOnAReturnWithoutAnOfflineSummaryOnly()
        {
            Assert.That(IdleFactory.Unity.Bootstrap.CoreGameFoundationBootstrap.ShouldRebaseMilestones(IdleFactory.Unity.Bootstrap.LifecycleReturnKind.ColdStart, false), Is.True);
            Assert.That(IdleFactory.Unity.Bootstrap.CoreGameFoundationBootstrap.ShouldRebaseMilestones(IdleFactory.Unity.Bootstrap.LifecycleReturnKind.ColdStart, true), Is.True);
            Assert.That(IdleFactory.Unity.Bootstrap.CoreGameFoundationBootstrap.ShouldRebaseMilestones(IdleFactory.Unity.Bootstrap.LifecycleReturnKind.Return, false), Is.True, "a restore or reload");
            Assert.That(IdleFactory.Unity.Bootstrap.CoreGameFoundationBootstrap.ShouldRebaseMilestones(IdleFactory.Unity.Bootstrap.LifecycleReturnKind.Return, true), Is.False, "an offline return reconciles its own milestones");
            Assert.That(IdleFactory.Unity.Bootstrap.CoreGameFoundationBootstrap.ShouldRebaseMilestones(IdleFactory.Unity.Bootstrap.LifecycleReturnKind.ConfigurationChange, false), Is.False);
            Assert.That(IdleFactory.Unity.Bootstrap.CoreGameFoundationBootstrap.ShouldRebaseMilestones(IdleFactory.Unity.Bootstrap.LifecycleReturnKind.None, false), Is.False);
        }

        [Test]
        public void ASingleNewLocationUsesTheSingularCopy()
        {
            var copy = new MilestoneCopy();
            var one = new PendingMilestone(MilestoneKind.FactoryLevelUp, 3, 6, 1, false, 1, false);
            Assert.That(copy.Primary(one), Is.EqualTo("Level 3. 1 new location opens."));
            var two = new PendingMilestone(MilestoneKind.FactoryLevelUp, 3, 6, 2, false, 1, false);
            Assert.That(copy.Primary(two), Is.EqualTo("Level 3. 2 new locations open."));
            Assert.That(MilestoneCopy.ShippedKeys, Does.Contain(MilestoneCopy.LevelWithOneUnlockKey));
            var warnings = new List<string>();
            var gap = new MilestoneCopy(new Dictionary<string, string> { { MilestoneCopy.LevelWithUnlocksKey, "Level {n}. {k} new locations open." } }, warnings.Add);
            Assert.That(gap.Primary(one), Is.EqualTo(MilestoneCopy.FallbackText), "a missing singular key falls back with a warning");
            Assert.That(warnings.Single(), Does.Contain("B-01s"));
        }

        // ---- copy ---------------------------------------------------------------------------------------------------------------

        [Test]
        public void TheCopyIsTheExactWorldCopyStringsWithTheirFields()
        {
            var copy = new MilestoneCopy();
            var level = new PendingMilestone(MilestoneKind.FactoryLevelUp, 3, 6, 2, true, 2, false);
            Assert.That(copy.Primary(level), Is.EqualTo("Level 3. 2 new locations open."));
            Assert.That(copy.Secondary(level), Is.EqualTo("Deck extended."));
            Assert.That(copy.Full(level), Is.EqualTo("Level 3. 2 new locations open. Deck extended."));
            var plain = new PendingMilestone(MilestoneKind.FactoryLevelUp, 6, -1, 0, false, 2, false);
            Assert.That(copy.Primary(plain), Is.EqualTo("Level 6 reached."));
            Assert.That(copy.Secondary(plain), Is.Empty);
            var prestige = new PendingMilestone(MilestoneKind.PrestigeCompleted, 1, -1, 0, false, 1, false);
            Assert.That(copy.Primary(prestige), Is.EqualTo("New run begins."));
            Assert.That(copy.Secondary(prestige), Is.Empty, "no deck line after a prestige");
        }

        [Test]
        public void AMissingCopyKeyFallsBackWithOneWarningAndNeverThrows()
        {
            var warnings = new List<string>();
            var copy = new MilestoneCopy(new Dictionary<string, string> { { MilestoneCopy.PrestigeKey, "New run begins." } }, warnings.Add);
            var level = new PendingMilestone(MilestoneKind.FactoryLevelUp, 3, 6, 2, true, 2, false);
            Assert.That(copy.Primary(level), Is.EqualTo(MilestoneCopy.FallbackText));
            Assert.That(copy.Primary(level), Is.EqualTo(MilestoneCopy.FallbackText));
            Assert.That(copy.Secondary(level), Is.EqualTo(MilestoneCopy.FallbackText));
            Assert.That(warnings.Count, Is.EqualTo(2), "one warning per missing key (B-01 and B-04), not per call");
            Assert.That(warnings[0], Does.Contain("B-01"));
        }

        [Test]
        public void TheDefaultWarningSinkIsTheUnityLog()
        {
            var copy = new MilestoneCopy(new Dictionary<string, string>(), null);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("B-02"));
            Assert.That(copy.Primary(new PendingMilestone(MilestoneKind.FactoryLevelUp, 2, -1, 0, false, 0, false)), Is.EqualTo(MilestoneCopy.FallbackText));
        }

        [Test]
        public void EveryShippedKeyHasText()
        {
            foreach (var key in new[] { MilestoneCopy.LevelWithUnlocksKey, MilestoneCopy.LevelOnlyKey, MilestoneCopy.PrestigeKey, MilestoneCopy.DeckExtendedKey })
            {
                Assert.That(MilestoneCopy.ShippedKeys, Does.Contain(key));
            }
        }
    }
}
