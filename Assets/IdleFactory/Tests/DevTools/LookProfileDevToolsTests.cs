using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.DevTools;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.Presentation.Look;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.DevTools
{
    /// <summary>
    /// T609 (QA-4, ARCH-6, ARCH-11): the development look-profile seam in the DevTools assembly. These cases need <c>IDLEFACTORY_DEV</c>, so they run only
    /// under <c>scripts/test-unity.ps1 -DevTools</c> (temporary <c>csc.rsp</c> plus <c>-assemblyNames IdleFactory.DevToolsTests</c>), whose gate fails on a
    /// run that discovers no test. The first case proves the runner really defined the symbol.
    /// </summary>
    public sealed class LookProfileDevToolsTests
    {
        private string root;
        private readonly List<Object> owned = new List<Object>();
        private readonly List<string> log = new List<string>();

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Application.temporaryCachePath, "t609-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            log.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            owned.Clear();
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
        }

        private LookProfile Profile(string id, bool prototype)
        {
            var profile = LookProfile.Create(id, prototype, new PresentationSpriteCatalog(), LookProfileRequirementTable.FromRows(Array.Empty<PresentationSpriteRequirement>()));
            owned.Add(profile);
            return profile;
        }

        private LookProfileResolver Resolver(ILookProfileSource source) =>
            new LookProfileResolver(Profile(LookProfile.F006Id, false), null, source, LookProfileResolver.DevelopmentBuild, log.Add);

        private static void Send(VisualElement target, EventBase evt)
        {
            using (evt)
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        [Test]
        public void TheDevToolsRunnerDefinesTheDevelopmentSymbolSoTheResolverHonoursTheSource()
        {
            Assert.That(LookProfileResolver.DevelopmentBuild, Is.True, "this assembly only compiles with IDLEFACTORY_DEV");
        }

        [Test]
        public void TheSourceIsInjectedBeforeTheSceneLoadsLikeTheTimeScaleSource()
        {
            Assert.That(DevToolsRuntime.LookSource, Is.Not.Null);
            Assert.That(CoreGameFoundationBootstrap.DefaultLookProfileSource, Is.SameAs(DevToolsRuntime.LookSource));
            Assert.That(DevToolsRuntime.TimeScale, Is.Not.Null);
        }

        [Test]
        public void TheSelectionPersistsInTheDevFileAndAFreshSourceReadsItBack()
        {
            var source = new DevLookProfileSource(root);
            Assert.That(source.SelectedProfileId, Is.Null);
            Assert.That(source.IsReady, Is.False);
            var changed = 0;
            source.Changed += () => changed++;

            source.Select(LookProfile.PrototypeAId);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(File.ReadAllText(source.FilePath), Is.EqualTo(LookProfile.PrototypeAId));
            Assert.That(source.FilePath, Does.EndWith(DevLookProfileSource.FileName));
            Assert.That(new DevLookProfileSource(root).SelectedProfileId, Is.EqualTo(LookProfile.PrototypeAId));

            source.Select(LookProfile.PrototypeAId);
            Assert.That(changed, Is.EqualTo(1), "selecting the same id again changes nothing");
        }

        [Test]
        public void AnUnreadableOrOversizedFileReadsAsNoSelection()
        {
            var path = DevLookProfileSource.PathFor(root);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, new string('x', 4000));
            var oversized = new DevLookProfileSource(root);
            Assert.That(oversized.SelectedProfileId.Length, Is.LessThanOrEqualTo(DevLookProfileSource.MaxFileBytes), "the read is bounded");

            File.WriteAllText(path, "F007-A\r\nsecond line");
            Assert.That(new DevLookProfileSource(root).SelectedProfileId, Is.EqualTo("F007-A"), "only the first line counts");
        }

        [Test]
        public void AnUnknownPersistedIdFallsBackToTheShippedDefaultWithALogEntry()
        {
            var path = DevLookProfileSource.PathFor(root);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "F007-Q");
            var source = new DevLookProfileSource(root);
            var resolver = Resolver(source);

            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.F006Id));
            Assert.That(log, Is.Empty, "the holder scene has not been read yet, so the id is not known to be unknown");

            source.MarkReady();
            Assert.That(resolver.Active, Is.SameAs(resolver.ShippedDefault));
            Assert.That(log, Is.EqualTo(new[] { LookProfileResolver.UnknownSelectionCode + ": F007-Q" }));
        }

        [Test]
        public void APersistedPrototypeIdAppliesOnceTheHolderSceneRegistersItsProfiles()
        {
            var path = DevLookProfileSource.PathFor(root);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, LookProfile.PrototypeAId);
            var source = new DevLookProfileSource(root);
            var resolver = Resolver(source);
            var applied = new List<string>();
            resolver.Applied += profile => applied.Add(profile.Id);
            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.F006Id));

            var holderObject = new GameObject("look-profile-holder");
            owned.Add(holderObject);
            holderObject.AddComponent<LookProfileHolder>().SetProfiles(new[] { Profile(LookProfile.PrototypeAId, true), Profile(LookProfile.PrototypeBId, true) });
            LookProfileHolder.RegisterLoaded(source);

            Assert.That(source.IsReady, Is.True);
            Assert.That(source.Profiles.Select(p => p.Id), Is.EquivalentTo(new[] { LookProfile.PrototypeAId, LookProfile.PrototypeBId }));
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.PrototypeAId));
            Assert.That(applied, Is.EqualTo(new[] { LookProfile.PrototypeAId }));
            Assert.That(log, Is.Empty);
        }

        [Test]
        public void ARegisteredIdIsHeldOnceAndASourceWithoutAHolderSceneIsReadyWithNoProfile()
        {
            var source = new DevLookProfileSource(root);
            var first = Profile(LookProfile.PrototypeAId, true);
            var second = Profile(LookProfile.PrototypeAId, true);
            source.Register(new[] { first, second, null });
            Assert.That(source.Profiles.Count, Is.EqualTo(1));
            Assert.That(source.TryGetProfile(LookProfile.PrototypeAId, out var held), Is.True);
            Assert.That(held, Is.SameAs(first), "the first profile of an id wins");

            var bare = new DevLookProfileSource(root);
            bare.MarkReady();
            Assert.That(bare.IsReady, Is.True);
            Assert.That(bare.Profiles, Is.Empty);
        }

        [Test]
        public void TheOverlayShowsNoProfileRowWithOnlyTheShippedDefaultAndOneButtonPerProfileOtherwise()
        {
            var source = new DevLookProfileSource(root);
            var host = new GameObject("dev-overlay-look");
            owned.Add(host);
            var overlay = DevToolsOverlay.Create(new DevTimeScale(), host, source);

            Assert.That(overlay.ProfileRow, Is.Not.Null);
            Assert.That(overlay.ProfileRow.style.display.value, Is.EqualTo(DisplayStyle.None), "nothing to choose between yet");

            source.Register(new[] { Profile(LookProfile.PrototypeAId, true) });
            Assert.That(overlay.ProfileRow.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(overlay.ProfileButtons.Select(b => b.text), Is.EqualTo(new[] { LookProfile.F006Id, LookProfile.PrototypeAId }));
            Assert.That(overlay.ProfileButtons.All(b => b.style.minHeight.value.value >= 44f && b.style.minWidth.value.value >= 44f && b.focusable), Is.True);
            Assert.That(overlay.ProfileButtons[0].tooltip, Does.EndWith("selected"), "the shipped default is selected while nothing else is");
        }

        [Test]
        public void AProfileButtonSelectsAndPersistsTheProfileAndTheSelectionIsNotShownByColourAlone()
        {
            var source = new DevLookProfileSource(root);
            source.Register(new[] { Profile(LookProfile.PrototypeAId, true) });
            var host = new GameObject("dev-overlay-look-click");
            owned.Add(host);
            var overlay = DevToolsOverlay.Create(new DevTimeScale(), host, source);

            Send(overlay.ProfileButtons[1], new NavigationSubmitEvent());

            Assert.That(source.SelectedProfileId, Is.EqualTo(LookProfile.PrototypeAId));
            Assert.That(File.ReadAllText(source.FilePath), Is.EqualTo(LookProfile.PrototypeAId));
            Assert.That(overlay.ProfileButtons[1].tooltip, Does.EndWith("selected"));
            Assert.That(overlay.ProfileButtons[1].style.unityFontStyleAndWeight.value, Is.EqualTo(FontStyle.Bold));
            Assert.That(overlay.ProfileButtons[1].style.borderTopWidth.value, Is.GreaterThan(0f), "a border as well as a colour marks the selection");
            Assert.That(overlay.ProfileButtons[0].style.borderTopWidth.value, Is.Zero);

            Send(overlay.ProfileButtons[0], new NavigationSubmitEvent());
            Assert.That(source.SelectedProfileId, Is.EqualTo(LookProfile.F006Id));
        }

        [Test]
        public void ASwitchThroughTheDevSourceRaisesAppliedOnTheBootstrapOwnedResolverPath()
        {
            var source = new DevLookProfileSource(root);
            source.Register(new[] { Profile(LookProfile.PrototypeAId, true) });
            var resolver = Resolver(source);
            var applied = new List<string>();
            resolver.Applied += profile => applied.Add(profile.Id);
            Assert.That(resolver.Resolve(), Is.True, resolver.Error);

            source.Select(LookProfile.PrototypeAId);
            source.Select(LookProfile.F006Id);

            Assert.That(applied, Is.EqualTo(new[] { LookProfile.PrototypeAId, LookProfile.F006Id }));
            Assert.That(resolver.Active, Is.SameAs(resolver.ShippedDefault));
        }
    }
}
