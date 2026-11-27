using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.Presentation.Look;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F007
{
    /// <summary>
    /// T609 (ARCH-11, contracts/look-profile.md rule 5): an unknown persisted profile id falls back to the shipped default with a log entry, a release build
    /// ignores the selection key altogether, and a source that is not ready yet changes nothing. The DevTools-assembly variants of these cases (the real
    /// <c>DevLookProfileSource</c> and its file) run under <c>scripts/test-unity.ps1 -DevTools</c>.
    /// </summary>
    public sealed class LookProfileInvalidSelectionTests
    {
        private LookProfileTestKit kit;
        private List<string> log;
        private PresentationSpriteCatalog sceneCatalog;

        [SetUp]
        public void SetUp()
        {
            kit = new LookProfileTestKit();
            log = new List<string>();
            sceneCatalog = kit.CatalogFor(LookProfileRequirementTable.F006(), true);
        }

        [TearDown]
        public void TearDown() => kit.Dispose();

        private LookProfile Prototype(string id)
        {
            var table = LookProfileTestKit.SmallTable();
            return kit.Profile(id, true, kit.CatalogFor(table, false), table);
        }

        private LookProfileResolver Resolver(ILookProfileSource source, bool honor) =>
            new LookProfileResolver(kit.F006(), sceneCatalog, source, honor, log.Add);

        [Test]
        public void AnUnknownPersistedIdFallsBackToTheShippedDefaultAndLogsOnce()
        {
            var source = new FakeLookProfileSource { Selected = "F007-Q" }.Hold(Prototype(LookProfile.PrototypeAId));
            var resolver = Resolver(source, true);

            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(resolver.Active, Is.SameAs(resolver.ShippedDefault));
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.F006Id));
            Assert.That(resolver.ActiveCatalog, Is.SameAs(sceneCatalog));
            Assert.That(log, Is.EqualTo(new[] { LookProfileResolver.UnknownSelectionCode + ": F007-Q" }));

            // The same unknown id is reported once: a later refresh, or the same id selected again, adds nothing.
            source.Select("F007-Q");
            resolver.Refresh();
            Assert.That(log.Count, Is.EqualTo(1));

            // Another unknown id is a new entry.
            source.Select("F007-R");
            Assert.That(log.Count, Is.EqualTo(2));
            Assert.That(log[1], Is.EqualTo(LookProfileResolver.UnknownSelectionCode + ": F007-R"));
            Assert.That(resolver.Active, Is.SameAs(resolver.ShippedDefault));
        }

        [Test]
        public void ThePersistedIdIsBoundedAndPrintableInTheLog()
        {
            var nasty = "F007-" + new string('x', 400) + "\u0007\né";
            var source = new FakeLookProfileSource { Selected = nasty };
            var resolver = Resolver(source, true);

            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(log.Count, Is.EqualTo(1));
            var line = log[0];
            Assert.That(line, Does.StartWith(LookProfileResolver.UnknownSelectionCode + ": F007-"));
            Assert.That(line.Length, Is.LessThanOrEqualTo(LookProfileResolver.UnknownSelectionCode.Length + 2 + LookProfileResolver.MaxLoggedIdLength));
            Assert.That(line.All(c => c >= ' ' && c <= '~'), Is.True, "no control or non-ASCII character reaches the log");

            var control = new FakeLookProfileSource { Selected = "A\u0007Bé" };
            log.Clear();
            Assert.That(Resolver(control, true).Resolve(), Is.True);
            Assert.That(log, Is.EqualTo(new[] { LookProfileResolver.UnknownSelectionCode + ": A?B?" }));
        }

        [Test]
        public void AReleaseBuildIgnoresTheSelectionKeyWhetherItIsKnownOrNot()
        {
            var prototype = Prototype(LookProfile.PrototypeAId);
            var known = new FakeLookProfileSource { Selected = LookProfile.PrototypeAId }.Hold(prototype);
            var resolver = Resolver(known, false);
            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(resolver.Active, Is.SameAs(resolver.ShippedDefault), "a release build never draws a prototype profile");

            var unknown = new FakeLookProfileSource { Selected = "F007-Q" };
            var releaseResolver = Resolver(unknown, false);
            var applied = 0;
            releaseResolver.Applied += _ => applied++;
            Assert.That(releaseResolver.Resolve(), Is.True, releaseResolver.Error);
            unknown.Select(LookProfile.PrototypeAId);
            unknown.Select("F007-Z");
            Assert.That(releaseResolver.Active, Is.SameAs(releaseResolver.ShippedDefault), "a later change of the key is ignored too");
            Assert.That(applied, Is.Zero);
            Assert.That(log, Is.Empty, "the key is not even read, so an unknown value has nothing to log");
        }

        [Test]
        public void ASourceThatIsNotReadyKeepsTheShippedDefaultSilentlyUntilItIsReady()
        {
            var prototype = Prototype(LookProfile.PrototypeAId);
            var source = new FakeLookProfileSource { Ready = false, Selected = LookProfile.PrototypeAId };
            var resolver = Resolver(source, true);
            var applied = new List<string>();
            resolver.Applied += profile => applied.Add(profile.Id);

            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.F006Id));
            Assert.That(log, Is.Empty, "a persisted prototype id is not unknown while the holder scene is still loading");

            source.Hold(prototype);
            source.MakeReady();
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.PrototypeAId), "the persisted selection applies once the profiles are known");
            Assert.That(applied, Is.EqualTo(new[] { LookProfile.PrototypeAId }));
            Assert.That(log, Is.Empty);
        }

        [Test]
        public void ANotReadySourceWhoseSelectionNeverBecomesKnownLogsOnceItIsReady()
        {
            var source = new FakeLookProfileSource { Ready = false, Selected = "F007-Q" };
            var resolver = Resolver(source, true);
            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(log, Is.Empty);

            source.MakeReady();
            Assert.That(resolver.Active, Is.SameAs(resolver.ShippedDefault));
            Assert.That(log, Is.EqualTo(new[] { LookProfileResolver.UnknownSelectionCode + ": F007-Q" }));
        }

        [Test]
        public void AnEmptyNullOrShippedIdSelectsTheShippedDefaultWithoutALogEntry()
        {
            foreach (var id in new string[] { null, string.Empty, LookProfile.F006Id })
            {
                var resolver = Resolver(new FakeLookProfileSource { Selected = id }, true);
                Assert.That(resolver.Resolve(), Is.True, resolver.Error);
                Assert.That(resolver.Active, Is.SameAs(resolver.ShippedDefault));
            }

            Assert.That(log, Is.Empty);
        }

        [Test]
        public void ASelectionWhoseProfileCarriesAnotherIdIsTreatedAsUnknown()
        {
            var mislabelled = Prototype(LookProfile.PrototypeBId);
            // A source that hands out a profile under an id it does not carry: the resolver does not trust it.
            var lying = new LyingSource(LookProfile.PrototypeAId, mislabelled);
            var resolver = Resolver(lying, true);

            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(resolver.Active, Is.SameAs(resolver.ShippedDefault));
            Assert.That(log.Single(), Does.StartWith(LookProfileResolver.UnknownSelectionCode));
        }

        [Test]
        public void ADisposedResolverNoLongerFollowsItsSource()
        {
            var prototype = Prototype(LookProfile.PrototypeAId);
            var source = new FakeLookProfileSource().Hold(prototype);
            var resolver = Resolver(source, true);
            Assert.That(resolver.Resolve(), Is.True, resolver.Error);

            resolver.Dispose();
            source.Select(LookProfile.PrototypeAId);
            Assert.That(resolver.Active, Is.SameAs(resolver.ShippedDefault));
        }

        private sealed class LyingSource : ILookProfileSource
        {
            private readonly string id;
            private readonly LookProfile profile;

            public LyingSource(string id, LookProfile profile)
            {
                this.id = id;
                this.profile = profile;
            }

            public bool IsReady => true;

            public string SelectedProfileId => id;

            public event System.Action Changed
            {
                add { }
                remove { }
            }

            public bool TryGetProfile(string requested, out LookProfile found)
            {
                found = profile;
                return true;
            }
        }
    }
}
