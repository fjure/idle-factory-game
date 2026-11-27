using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.Presentation.Look;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.EditMode.F007
{
    /// <summary>
    /// T609 (contracts/look-profile.md rules 3 and 5, ARCH-5): the look-profile resolver validates a profile's catalog against the profile's own requirement
    /// table and fails closed (a missing key or a wrong size or pivot, with no fallback to another profile), and the F006 profile resolves every sprite
    /// exactly as the catalog did before profiles existed. These cases are profile-agnostic, so they run in the normal Edit Mode suite under F006.
    /// </summary>
    public sealed class LookProfileResolutionTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string F006AssetPath = "Assets/IdleFactory/Art/F007/Profiles/F006.asset";

        private LookProfileTestKit kit;
        private List<string> log;

        [SetUp]
        public void SetUp()
        {
            kit = new LookProfileTestKit();
            log = new List<string>();
        }

        [TearDown]
        public void TearDown() => kit.Dispose();

        private LookProfileResolver Resolver(LookProfile shipped, PresentationSpriteCatalog sceneCatalog, ILookProfileSource source = null, bool honor = false) =>
            new LookProfileResolver(shipped, sceneCatalog, source, honor, log.Add);

        [Test]
        public void TheF006TableIsTheStaticRegistryRowForRow()
        {
            var table = LookProfileRequirementTable.F006();
            Assert.That(table.Source, Is.EqualTo(LookRequirementSource.F006Registry));
            Assert.That(table.Count, Is.EqualTo(PresentationSpriteCatalog.F006RegistryRows.Count));
            Assert.That(table.Count, Is.GreaterThan(100), "the registry carries the F001, F003, F004 and F006 rows");
            foreach (var row in table.Rows)
            {
                Assert.That(PresentationSpriteCatalog.TryGetRegistryRequirement(row.Key, out var registry), Is.True, row.Key);
                Assert.That(table.TryGet(row.Key, out var viaTable), Is.True, row.Key);
                Assert.That(viaTable.WorldWidth, Is.EqualTo(registry.WorldWidth), row.Key);
                Assert.That(viaTable.WorldHeight, Is.EqualTo(registry.WorldHeight), row.Key);
                Assert.That(viaTable.PivotX, Is.EqualTo(registry.PivotX), row.Key);
                Assert.That(viaTable.PivotY, Is.EqualTo(registry.PivotY), row.Key);
                Assert.That(viaTable.MinFrames, Is.EqualTo(registry.MinFrames), row.Key);
                Assert.That(viaTable.MaxFrames, Is.EqualTo(registry.MaxFrames), row.Key);
                Assert.That(viaTable.Required, Is.EqualTo(registry.Required), row.Key);
                Assert.That(viaTable.HasLegacyShape, Is.EqualTo(registry.HasLegacyShape), row.Key);
            }

            Assert.That(table.TryGet("NO-SUCH-KEY", out _), Is.False);
            Assert.That(table.TryGet(null, out _), Is.False);
        }

        [Test]
        public void F006ParityEverySpriteTheResolverReturnsIsTheCatalogsOwnSprite()
        {
            var table = LookProfileRequirementTable.F006();
            var catalog = kit.CatalogFor(table, true);
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            var resolver = Resolver(kit.F006(), catalog);

            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.F006Id));
            Assert.That(resolver.ActiveCatalog, Is.SameAs(catalog));
            foreach (var row in table.Rows)
            {
                Assert.That(catalog.TryGet(row.Key, out var direct), Is.True, row.Key);
                Assert.That(resolver.TryGetSprite(row.Key, out var resolved), Is.True, row.Key);
                Assert.That(resolved, Is.SameAs(direct), "F006 parity of " + row.Key);
            }

            Assert.That(resolver.TryGetSprite("NO-SUCH-KEY", out var none), Is.False);
            Assert.That(none, Is.Null);
        }

        [Test]
        public void TheShippedSceneCatalogResolvesUnderTheF006AssetExactlyAsTheCatalogDoesDirectly()
        {
            var asset = AssetDatabase.LoadAssetAtPath<LookProfile>(F006AssetPath);
            Assert.That(asset, Is.Not.Null, "the F006 profile asset is created through Unity tooling at " + F006AssetPath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var bootstrap = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameFoundationBootstrap>(true))
                    .Single();
                var catalog = bootstrap.SpriteCatalog;
                var resolver = Resolver(asset, catalog);

                Assert.That(resolver.Resolve(), Is.True, resolver.Error);
                Assert.That(resolver.ActiveCatalog, Is.SameAs(catalog));
                Assert.That(catalog.Count, Is.GreaterThan(0));
                var compared = 0;
                foreach (var row in catalog.RequirementTable.Rows)
                {
                    var direct = catalog.TryGet(row.Key, out var expected);
                    Assert.That(resolver.TryGetSprite(row.Key, out var resolved), Is.EqualTo(direct), row.Key);
                    Assert.That(resolved, Is.SameAs(expected), "F006 parity of " + row.Key);
                    compared += direct ? 1 : 0;
                }

                Assert.That(compared, Is.GreaterThan(50), "the shipped scene registers the F003, F004 and F006 art");
                Assert.That(resolver.ArtPixelsPerCell, Is.EqualTo(bootstrap.ZoomSettings.artPixelsPerCell), "F006 keeps the authored camera inputs");
                Assert.That(resolver.ZoomFor(bootstrap.ZoomSettings), Is.SameAs(bootstrap.ZoomSettings));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void AMissingRequiredKeyFailsClosedWithTheCatalogCode()
        {
            var table = LookProfileTestKit.SmallTable();
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: T-DEPOT");
            var catalog = kit.CatalogFor(table, false, new[] { "T-DEPOT" });
            var profile = kit.Profile(LookProfile.ProductionId, false, catalog, table);
            var devResolver = new LookProfileResolver(profile, null, null, false, log.Add);
            Assert.That(devResolver.Resolve(), Is.False);
            Assert.That(devResolver.Error, Does.Contain(PresentationSpriteCatalog.KeyUnresolvedCode));
            Assert.That(devResolver.Error, Does.Contain("T-DEPOT"));
            Assert.That(devResolver.IsValid, Is.False);
            Assert.That(devResolver.Active, Is.Null);
            Assert.That(devResolver.ActiveCatalog, Is.Null);
            Assert.That(devResolver.TryGetSprite("T-MACHINE", out _), Is.False, "nothing resolves while the profile is invalid");
        }

        [Test]
        public void AWrongSizeOrPivotOrFrameCountFailsClosed()
        {
            var table = LookProfileTestKit.SmallTable();

            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: T-DEPOT");
            var wrongSize = kit.CatalogFor(table, false, null, r => r.Key == "T-DEPOT" ? kit.MadeSprite(40, 54, 0.5f, 0.5f) : kit.MadeSprite(Mathf.RoundToInt(r.WorldWidth * 24f), Mathf.RoundToInt(r.WorldHeight * 24f), r.PivotX, r.PivotY));
            Assert.That(wrongSize.IsValid, Is.False);
            Assert.That(wrongSize.Error, Does.Contain("wrong world size"));

            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: T-GLYPH");
            var wrongPivot = kit.CatalogFor(table, false, null, r => r.Key == "T-GLYPH" ? kit.MadeSprite(24, 24, 0f, 0f) : kit.MadeSprite(Mathf.RoundToInt(r.WorldWidth * 24f), Mathf.RoundToInt(r.WorldHeight * 24f), r.PivotX, r.PivotY));
            Assert.That(wrongPivot.IsValid, Is.False);
            Assert.That(wrongPivot.Error, Does.Contain("wrong pivot"));

            var tooMany = new PresentationSpriteCatalog();
            tooMany.UseRequirementTable(table);
            var frames = Enumerable.Range(0, 5).Select(_ => kit.MadeSprite(80, 64, 0.5f, 15f / 64f)).ToArray();
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: T-MACHINE");
            tooMany.Configure(new[] { new PresentationSpriteEntry("T-MACHINE", frames, 8f) });
            Assert.That(tooMany.IsValid, Is.False);
            Assert.That(tooMany.Error, Does.Contain("frame(s)"));

            var profile = kit.Profile(LookProfile.ProductionId, false, wrongSize, table);
            var resolver = new LookProfileResolver(profile, null, null, false, log.Add);
            Assert.That(resolver.Resolve(), Is.False);
            Assert.That(resolver.Error, Does.Contain("wrong world size"));
        }

        [Test]
        public void ATableIsPerProfileSoAKeyOfOneProfileIsNotRequiredByTheOther()
        {
            var smallTable = LookProfileTestKit.SmallTable();
            var smallCatalog = kit.CatalogFor(smallTable, false);
            var prototype = kit.Profile(LookProfile.ProductionId, false, smallCatalog, smallTable);
            var resolver = new LookProfileResolver(prototype, null, null, false, log.Add);

            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(resolver.TryGetSprite("T-MACHINE", out _), Is.True);
            Assert.That(resolver.TryGetSprite("F003-MACHINE-MINER", out _), Is.False, "no F006 key leaks into a profile that does not declare it");
            Assert.That(smallCatalog.RequirementTable.Source, Is.EqualTo(LookRequirementSource.Authored));
        }

        [Test]
        public void ANoFallbackStartUpErrorWhenTheSelectedProfileIsInvalidAndTheShippedDefaultIsNotUsed()
        {
            var table = LookProfileTestKit.SmallTable();
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: T-GLYPH");
            var broken = kit.Profile(LookProfile.PrototypeAId, true, kit.CatalogFor(table, false, new[] { "T-GLYPH" }), table);
            var goodTable = LookProfileRequirementTable.F006();
            var sceneCatalog = kit.CatalogFor(goodTable, true);
            var source = new FakeLookProfileSource { Selected = LookProfile.PrototypeAId }.Hold(broken);
            var resolver = Resolver(kit.F006(), sceneCatalog, source, true);

            Assert.That(resolver.Resolve(), Is.False, "the selected profile is invalid; the resolver does not quietly draw F006 instead");
            Assert.That(resolver.Active, Is.Null);
            Assert.That(resolver.Error, Does.Contain("T-GLYPH"));
        }

        [Test]
        public void ASwitchToAnInvalidProfileKeepsTheCurrentOneAndReportsItAndAValidSwitchRaisesApplied()
        {
            var table = LookProfileTestKit.SmallTable();
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: T-GLYPH");
            var broken = kit.Profile(LookProfile.PrototypeBId, true, kit.CatalogFor(table, false, new[] { "T-GLYPH" }), table);
            var twin = kit.Profile(LookProfile.PrototypeAId, true, kit.CatalogFor(table, false), table, 48);
            var sceneCatalog = kit.CatalogFor(LookProfileRequirementTable.F006(), true);
            var source = new FakeLookProfileSource().Hold(broken).Hold(twin);
            var resolver = Resolver(kit.F006(), sceneCatalog, source, true);
            var applied = new List<string>();
            resolver.Applied += profile => applied.Add(profile.Id);
            Assert.That(resolver.Resolve(), Is.True, resolver.Error);

            source.Select(LookProfile.PrototypeBId);
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.F006Id), "a failed switch keeps the current profile");
            Assert.That(resolver.Error, Does.Contain("T-GLYPH"));
            Assert.That(log.Any(line => line.Contains("T-GLYPH")), Is.True, "and says so in the log");
            Assert.That(applied, Is.Empty);

            source.Select(LookProfile.PrototypeAId);
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.PrototypeAId));
            Assert.That(resolver.ActiveCatalog, Is.SameAs(twin.SpriteCatalog));
            Assert.That(resolver.ArtPixelsPerCell, Is.EqualTo(48));
            Assert.That(resolver.Error, Is.Empty);
            Assert.That(applied, Is.EqualTo(new[] { LookProfile.PrototypeAId }));

            source.Select(LookProfile.F006Id);
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.F006Id));
            Assert.That(resolver.ActiveCatalog, Is.SameAs(sceneCatalog));
            Assert.That(applied, Is.EqualTo(new[] { LookProfile.PrototypeAId, LookProfile.F006Id }));
        }

        [Test]
        public void ProfileFieldRulesFailClosed()
        {
            var table = LookProfileTestKit.SmallTable();
            var catalog = kit.CatalogFor(table, false);

            var prototypeAsDefault = kit.Profile(LookProfile.PrototypeAId, true, catalog, table);
            var resolver = new LookProfileResolver(prototypeAsDefault, null, null, false, log.Add);
            Assert.That(resolver.Resolve(), Is.False);
            Assert.That(resolver.Error, Does.StartWith(LookProfileResolver.ShippedPrototypeCode), "the shipped default is never a prototype");

            Problem(kit.Profile("F007-Z", false, catalog, table), "unknown profile id");
            Problem(kit.Profile(LookProfile.ProductionId, false, catalog, table, 0), "pixelsPerUnit");

            var continuous = kit.Profile(LookProfile.PrototypeBId, true, catalog, table);
            continuous.ConfigureRendering(LookWorldRenderer.Renderer2D, LookCameraPolicy.Continuous, LookFiltering.Bilinear);
            Problem(continuous, "Continuous");

            var universalRelease = kit.Profile(LookProfile.ProductionId, false, catalog, table);
            universalRelease.ConfigureRendering(LookWorldRenderer.Universal, LookCameraPolicy.IntegerScale, LookFiltering.Point);
            Problem(universalRelease, "Universal");

            var universalPrototype = kit.Profile(LookProfile.PrototypeCId, true, catalog, table);
            universalPrototype.ConfigureRendering(LookWorldRenderer.Universal, LookCameraPolicy.IntegerScale, LookFiltering.Bilinear);
            Assert.That(universalPrototype.FirstProblem(), Is.Null, "a prototype may name the Universal renderer (rule 9)");

            var emptyKey = kit.Profile(LookProfile.PrototypeAId, true, catalog, LookProfileRequirementTable.FromRows(new[]
            {
                new PresentationSpriteRequirement(string.Empty, 1f, 1f, 0.5f, 0.5f, 1, 1, true),
            }));
            Problem(emptyKey, "no key");
        }

        private static void Problem(LookProfile profile, string expectedFragment)
        {
            var problem = profile.FirstProblem();
            Assert.That(problem, Is.Not.Null, profile.Id);
            Assert.That(problem, Does.Contain(expectedFragment));
        }

        [Test]
        public void ZoomForReturnsTheAuthoredSettingsWhenThePixelsPerUnitAgreeAndACopyOtherwise()
        {
            var authored = new RunZoomSettings { artPixelsPerCell = 24, readableCellPoints = 22, maxScale = 5 };
            var table = LookProfileTestKit.SmallTable();
            var same = new LookProfileResolver(kit.Profile(LookProfile.F006Id, false, kit.CatalogFor(table, false), table, 24), null, null, false, log.Add);
            Assert.That(same.Resolve(), Is.True, same.Error);
            Assert.That(same.ZoomFor(authored), Is.SameAs(authored));

            var wide = new LookProfileResolver(kit.Profile(LookProfile.ProductionId, false, kit.CatalogFor(table, false), table, 48), null, null, false, log.Add);
            Assert.That(wide.Resolve(), Is.True, wide.Error);
            var copy = wide.ZoomFor(authored);
            Assert.That(copy, Is.Not.SameAs(authored));
            Assert.That(copy.artPixelsPerCell, Is.EqualTo(48));
            Assert.That(copy.readableCellPoints, Is.EqualTo(22));
            Assert.That(copy.maxScale, Is.EqualTo(5));
            Assert.That(authored.artPixelsPerCell, Is.EqualTo(24), "the authored settings object is never modified");
        }

        [Test]
        public void AnAuthoredTableReportsItsFirstProblem()
        {
            Assert.That(LookProfileTestKit.SmallTable().FirstProblem(), Is.Null);
            Assert.That(LookProfileRequirementTable.F006().FirstProblem(), Is.Null);
            var duplicate = LookProfileRequirementTable.FromRows(new[]
            {
                new PresentationSpriteRequirement("A", 1f, 1f, 0.5f, 0.5f, 1, 1, true),
                new PresentationSpriteRequirement("A", 1f, 1f, 0.5f, 0.5f, 1, 1, true),
            });
            Assert.That(duplicate.FirstProblem(), Does.Contain("Duplicate"));
            var flat = LookProfileRequirementTable.FromRows(new[] { new PresentationSpriteRequirement("A", 0f, 1f, 0.5f, 0.5f, 1, 1, true) });
            Assert.That(flat.FirstProblem(), Does.Contain("non-positive"));
            var range = LookProfileRequirementTable.FromRows(new[] { new PresentationSpriteRequirement("A", 1f, 1f, 0.5f, 0.5f, 2, 1, true) });
            Assert.That(range.FirstProblem(), Does.Contain("frame range"));
            Assert.That(LookProfileTestKit.SmallTable().Rows.Count, Is.EqualTo(3));
        }

        [Test]
        public void ResolvingWritesNoSettingOfAProfilesSerializedCatalog()
        {
            var table = LookProfileTestKit.SmallTable();
            var prototypeCatalog = kit.CatalogFor(table, false);
            var prototype = kit.Profile(LookProfile.PrototypeAId, true, prototypeCatalog, table);
            var sceneCatalog = kit.CatalogFor(LookProfileRequirementTable.F006(), true);
            var source = new FakeLookProfileSource { Selected = LookProfile.PrototypeAId }.Hold(prototype);
            var resolver = Resolver(kit.F006(), sceneCatalog, source, true);

            Assert.That(prototypeCatalog.RequireRegistryRows, Is.False);
            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.PrototypeAId));
            Assert.That(prototypeCatalog.RequireRegistryRows, Is.False, "resolution never mutates the serialized catalog of a profile");
            Assert.That(sceneCatalog.RequireRegistryRows, Is.True, "and leaves the shipped catalog's own setting");
        }

        [Test]
        public void AnAuthoredTableEnforcesItsRequiredRowsWithoutTheRegistryRowsSetting()
        {
            var table = LookProfileTestKit.SmallTable();
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: T-GLYPH");
            var catalog = kit.CatalogFor(table, false, new[] { "T-GLYPH" });
            Assert.That(catalog.RequireRegistryRows, Is.False);
            Assert.That(catalog.IsValid, Is.False, "a required row of an authored table is missing");
            Assert.That(catalog.Error, Does.Contain("T-GLYPH"));

            var f006Catalog = kit.CatalogFor(LookProfileRequirementTable.F006(), false, new[] { "F003-MACHINE-MINER" });
            Assert.That(f006Catalog.Validate(), Is.True, "the F006 table keeps its setting: a development catalog stays legal when smaller");
        }

        [Test]
        public void ASwitchToAProfileThatLacksAContentKeyIsRejectedLikeAtStartUp()
        {
            var table = LookProfileRequirementTable.FromRows(new[]
            {
                new PresentationSpriteRequirement("T-MACHINE", 80f / 24f, 64f / 24f, 0.5f, 15f / 64f, 1, 4, true, true),
                new PresentationSpriteRequirement("T-GLYPH", 1f, 1f, 0.5f, 0.5f, 1, 1, false),
            });
            var lacking = kit.Profile(LookProfile.PrototypeBId, true, kit.CatalogFor(table, false, new[] { "T-GLYPH" }), table);
            var complete = kit.Profile(LookProfile.PrototypeAId, true, kit.CatalogFor(table, false), table);
            var sceneCatalog = kit.CatalogFor(LookProfileRequirementTable.F006(), true);
            var source = new FakeLookProfileSource().Hold(lacking).Hold(complete);
            var resolver = Resolver(kit.F006(), sceneCatalog, source, true);
            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            var applied = new List<string>();
            resolver.Applied += profile => applied.Add(profile.Id);

            // The content (here: a stand-in for it) needs the key T-GLYPH, which the table of the profile does not require but the content does.
            resolver.SetContentKeyCheck(candidate => candidate.TryGet("T-GLYPH", out _) ? null : "presentation.key.unresolved: T-GLYPH");

            source.Select(LookProfile.PrototypeBId);
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.F006Id), "the switch is rejected and the current profile stays");
            Assert.That(resolver.Error, Is.EqualTo("presentation.key.unresolved: T-GLYPH"));
            Assert.That(log, Does.Contain("presentation.key.unresolved: T-GLYPH"));
            Assert.That(applied, Is.Empty);

            source.Select(LookProfile.PrototypeAId);
            Assert.That(resolver.Active.Id, Is.EqualTo(LookProfile.PrototypeAId), "a profile that carries every content key is accepted");
            Assert.That(applied, Is.EqualTo(new[] { LookProfile.PrototypeAId }));
        }

        [Test]
        public void ResolvingASpriteAllocatesNothing()
        {
            var table = LookProfileRequirementTable.F006();
            var catalog = kit.CatalogFor(table, true);
            var resolver = Resolver(kit.F006(), catalog);
            Assert.That(resolver.Resolve(), Is.True, resolver.Error);
            var keys = table.Rows.Select(r => r.Key).Take(24).ToArray();
            var found = 0;
            for (var warm = 0; warm < 8; warm++)
            {
                foreach (var key in keys)
                {
                    found += resolver.TryGetSprite(key, out _) ? 1 : 0;
                }
            }

            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var round = 0; round < 200; round++)
                {
                    for (var i = 0; i < keys.Length; i++)
                    {
                        found += resolver.TryGetSprite(keys[i], out _) ? 1 : 0;
                    }
                }
            });
            Assert.That(allocations, Is.Zero, "resolution of a key allocates nothing in the steady state");
            Assert.That(found, Is.GreaterThan(0));
        }
    }
}
