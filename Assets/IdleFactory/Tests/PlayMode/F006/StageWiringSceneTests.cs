#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T540 (Feature 006, FR-035, FR-036, SC-006, research R14, R15): the development stage and the milestones in the shipped game. The real authored scene is started
    /// against an isolated persistence root with plenty of Cash; the bootstrap composes the deck, the depot and the banner at runtime, the shell presenter derives the
    /// stage from the factory snapshot and the watcher raises the milestone. Nothing is wired by the test: it proves that the authored scene (its sprite catalog, its
    /// composition asset, its shell styles) carries everything the feature needs.
    /// </summary>
    public sealed class StageWiringSceneTests
    {
        private string root;
        private RunSceneClock clock;
        private RunSceneRun run;

        [SetUp]
        public void CreateRoot()
        {
            root = RunSceneRun.NewRoot();
            clock = new RunSceneClock();
            run = null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (run != null)
            {
                yield return run.Stop();
            }

            RunSceneRun.DeleteRoot(root);
        }

        private static byte[] ContentWithCash()
        {
            var text = Encoding.UTF8.GetString(RunSceneRun.IronBytes());
            var cash = new Regex("\"startingCash\"\\s*:\\s*\\d+");
            Assert.That(cash.IsMatch(text), Is.True);
            return Encoding.UTF8.GetBytes(cash.Replace(text, "\"startingCash\": 1000000000", 1));
        }

        private IEnumerator StartRun()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start(ContentWithCash);
            yield return null;
            yield return null;
            var bootstrap = run.Bootstrap;
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.RunShell.IsReady, Is.True, bootstrap.RunShell.Error);
            Assert.That(bootstrap.DeckView, Is.Not.Null);
            Assert.That(bootstrap.RunShell.MilestoneBanner, Is.Not.Null, "the shell builds its banner from the authored UXML and USS");
        }

        [UnityTest]
        public IEnumerator TheAuthoredSceneDrivesTheDeckTheDepotAndTheDepositFromTheFactoryLevel()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;
            var deck = bootstrap.DeckView;
            var presenter = bootstrap.ShellPresenter;

            // The shipped Iron content starts with 4 of 12 locations (u0 = 4): the first factory is Early with the open deck, a railing and no wall (owner decision 2026-11-18).
            Assert.That(presenter.Development.Stage, Is.EqualTo(0));
            Assert.That(deck.Stage, Is.EqualTo(0));
            Assert.That(bootstrap.DepotLinkView.Stage, Is.EqualTo(0));
            Assert.That(deck.Plan.Dressed, Is.True, "the dressed plan: the margin ring is lit by stage");
            Assert.That(deck.Plan.LitRingCells, Is.Empty, "the open deck: no lit margin yet");
            Assert.That(deck.Plan.Edges.All(e => e.Key.Contains("RAILING")), Is.True, "Early draws the railing and no wall or gantry");
            Assert.That(bootstrap.RunShell.MilestoneBanner.IsShown, Is.False, "starting the run is not a milestone");
            var camera = bootstrap.FactoryCamera;
            var cameraPosition = camera.transform.position;
            var cameraSize = camera.orthographicSize;
            Assert.That(bootstrap.RunShell.TryGetUnobstructedScreenRect(Screen.height, out var rectBefore), Is.True);

            Assert.That(bootstrap.RunCommands.UpgradeFactory().IsSuccess, Is.True);
            yield return Frames(4);
            Assert.That(presenter.Development.Stage, Is.EqualTo(0), "6 of 12 is still Early");
            var banner = bootstrap.RunShell.MilestoneBanner;
            Assert.That(banner.IsShown, Is.True, "level 2 raised a banner");
            Assert.That(banner.PrimaryLabel.text, Is.EqualTo("Level 2. 2 new locations open."));
            Assert.That(banner.SecondaryLabel.text, Is.Empty, "no stage change, no deck line");
            Assert.That(deck.ActiveHighlightCount, Is.EqualTo(2), "the two new foundations are highlighted");
            Assert.That(deck.RevealActive, Is.False, "the stage did not rise");
            Assert.That(bootstrap.RunShell.TryGetUnobstructedScreenRect(Screen.height, out var rectDuring), Is.True);
            Assert.That(rectDuring, Is.EqualTo(rectBefore), "the banner does not change the rectangle the camera frames");
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition), "and the camera does not move");
            Assert.That(camera.orthographicSize, Is.EqualTo(cameraSize).Within(1e-4f));

            Assert.That(bootstrap.RunCommands.UpgradeFactory().IsSuccess, Is.True);
            yield return Frames(4);
            Assert.That(presenter.Development.Stage, Is.EqualTo(1), "8 of 12 is Partial");
            Assert.That(deck.Stage, Is.EqualTo(1));
            Assert.That(bootstrap.DepotLinkView.Stage, Is.EqualTo(1));
            Assert.That(deck.Plan.Edges.First().Key, Does.Contain("WALL"), "Partial draws the wall edge");
            Assert.That(deck.Plan.LitRingCells.Count, Is.EqualTo(DeckPlanner.LitRingCount(DeckPlanner.RingCellsInLightingOrder(bootstrap.SlotGrid.Layout).Count, 1)));
            Assert.That(banner.PrimaryLabel.text, Is.EqualTo("Level 3. 2 new locations open."));
            Assert.That(banner.SecondaryLabel.text, Is.EqualTo("Deck extended."), "the stage rose");
            Assert.That(deck.RevealActive || deck.BaseTilemap.color.r >= 0.99f, Is.True, "the deck reveal ran in normal motion");

            Assert.That(bootstrap.RunCommands.UpgradeFactory().IsSuccess, Is.True);
            yield return Frames(4);
            Assert.That(presenter.Development.Stage, Is.EqualTo(2), "10 of 12 is Established");
            Assert.That(deck.Stage, Is.EqualTo(2));
            Assert.That(bootstrap.DepotLinkView.Stage, Is.EqualTo(2));
            Assert.That(bootstrap.DepotLinkView.ShownDepotKey, Does.Contain("S3"), "the Established depot variant");
            Assert.That(deck.Plan.Edges.First().Key, Does.Contain("GANTRY"));
            Assert.That(deck.Plan.LitRingCells.Count, Is.EqualTo(DeckPlanner.RingCellsInLightingOrder(bootstrap.SlotGrid.Layout).Count), "the whole ring is lit");
            Assert.That(banner.PrimaryLabel.text, Is.EqualTo("Level 4. 2 new locations open."));
            Assert.That(banner.SecondaryLabel.text, Is.EqualTo("Deck extended."));

            // The banner goes by itself within 2.5 s of the shell clock; the camera framing is the same after it.
            bootstrap.RunShell.TickMilestoneBanner(2.6f);
            Assert.That(banner.IsShown, Is.False);
            Assert.That(bootstrap.RunShell.TryGetUnobstructedScreenRect(Screen.height, out var rectAfter), Is.True);
            Assert.That(rectAfter, Is.EqualTo(rectBefore));
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(camera.orthographicSize, Is.EqualTo(cameraSize).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator UnderReducedMotionTheBannerIsStaticAndTheDeckDoesNotEase()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;
            bootstrap.DisplayPreferences.TrySave(ReducedMotionPreference.On);
            yield return Frames(30);
            Assert.That(bootstrap.AnimationClock.ReducedMotion, Is.True);
            Assert.That(bootstrap.RunCommands.UpgradeFactory().IsSuccess, Is.True);
            Assert.That(bootstrap.RunCommands.UpgradeFactory().IsSuccess, Is.True);
            yield return Frames(4);
            var banner = bootstrap.RunShell.MilestoneBanner;
            Assert.That(banner.IsShown, Is.True);
            Assert.That(banner.Root.style.opacity.value, Is.EqualTo(1f), "static: no fade-in");
            Assert.That(bootstrap.DeckView.RevealActive, Is.False, "no deck easing");
            Assert.That(bootstrap.DeckView.BaseTilemap.color.r, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(bootstrap.DeckView.ActiveHighlightCount, Is.GreaterThan(0), "the static foundation highlight still shows");
        }

        [UnityTest]
        public IEnumerator TheBannerCoversNeitherCashNorOverdriveInTheAuthoredScene()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;
            Assert.That(bootstrap.RunCommands.UpgradeFactory().IsSuccess, Is.True);
            yield return Frames(6);
            var shell = bootstrap.RunShell;
            Assert.That(shell.MilestoneBanner.IsShown, Is.True);
            var box = shell.MilestoneBanner.Root.worldBound;
            foreach (var other in new UnityEngine.UIElements.VisualElement[] { shell.CashButton, shell.StoreButton, shell.OverdriveButton, shell.SettingsButton })
            {
                var o = other.worldBound;
                if (other.resolvedStyle.display != UnityEngine.UIElements.DisplayStyle.None && o.width > 0f)
                {
                    Assert.That(box.Overlaps(o), Is.False, "the banner does not cover " + other.name);
                }
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }
    }
}
#endif
