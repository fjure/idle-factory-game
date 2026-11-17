using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IdleFactory.Editor.Art;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Serialization;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T527 wiring (Feature 006): the settings items P1-F1, P1-F4 and P1-F6, the scene catalog registration with the placed rows required (A-13), the
    /// hub glyph import (B-17) and the atlas page inventory for the SetPass budget (A-10).
    /// </summary>
    public sealed class DeckWiringTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";

        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            LatticeLayoutBuilder.TryUseSettings(null, out _);
            foreach (var item in owned)
            {
                UnityEngine.Object.DestroyImmediate(item);
            }

            owned.Clear();
        }

        private WorldCompositionSettings NewSettings(Action<WorldCompositionSettings> edit = null)
        {
            var settings = ScriptableObject.CreateInstance<WorldCompositionSettings>();
            owned.Add(settings);
            edit?.Invoke(settings);
            return settings;
        }

        // ---- P1-F4 ---------------------------------------------------------------------------------------------------------------

        [Test]
        public void ValidateCatchesTheDepositSharingTheDepotsCollectorColumn()
        {
            var shared = NewSettings(s => s.depositRule = new MarginCellRule(MarginSide.Right, 2));
            var problems = new LatticeLayoutBuilder(LatticeLayoutBuilder.ShippedLocationCount, WorldOrientation.Portrait, shared).Build().Validate();
            Assert.That(problems.Any(p => p.Contains("collector column")), Is.True, string.Join("; ", problems));

            var left = new LatticeLayoutBuilder(LatticeLayoutBuilder.ShippedLocationCount, WorldOrientation.Portrait, NewSettings(s => s.depositRule = new MarginCellRule(MarginSide.Left, -1))).Build();
            Assert.That(left.Validate(), Is.Empty, "deposit Left with the depot Right is valid");
            var bottom = new LatticeLayoutBuilder(LatticeLayoutBuilder.ShippedLocationCount, WorldOrientation.Portrait, NewSettings(s => s.depositRule = new MarginCellRule(MarginSide.Bottom, 1))).Build();
            Assert.That(bottom.Validate(), Is.Empty, "deposit Bottom is valid");
        }

        // ---- P1-F6 ---------------------------------------------------------------------------------------------------------------

        [Test]
        public void TheMarginIsNamedInWorldUnitsAndTheOldSerializedNameIsKept()
        {
            var type = typeof(WorldCompositionSettings);
            Assert.That(type.GetField("marginCells"), Is.Null, "the field is renamed");
            var field = type.GetField("marginWorldUnits");
            Assert.That(field, Is.Not.Null);
            var former = field.GetCustomAttribute<FormerlySerializedAsAttribute>();
            Assert.That(former, Is.Not.Null);
            Assert.That(former.oldName, Is.EqualTo("marginCells"), "an asset written before T527 still loads");
            Assert.That(DeckTestSupport.Settings().marginWorldUnits, Is.EqualTo(SlotGridLayout.HalfCellMargin));
            Assert.That(DeckTestSupport.Settings().ComposeFor(WorldOrientation.Portrait).MarginWorldUnits, Is.EqualTo(0.5f));
        }

        // ---- P1-F1 ---------------------------------------------------------------------------------------------------------------

        [Test]
        public void TheAuthoredAssetIsValidAndReproducesTheF004GeometryAndLayoutIds()
        {
            var asset = DeckTestSupport.Settings();
            Assert.That(asset.Validate(), Is.Empty);
            foreach (var orientation in new[] { WorldOrientation.Portrait, WorldOrientation.Landscape })
            {
                var defaults = LatticeLayoutBuilder.Shipped(orientation);
                Assert.That(LatticeLayoutBuilder.TryUseSettings(asset, out var problems), Is.True, string.Join("; ", problems));
                var authored = LatticeLayoutBuilder.Shipped(orientation);
                Assert.That(authored.LayoutId, Is.EqualTo("lattice-12x" + LatticeLayoutBuilder.ColumnsFor(orientation)), "an authored composition equal to the defaults keeps the F004 layout id");
                Assert.That(authored.Bounds, Is.EqualTo(defaults.Bounds));
                Assert.That(authored.Margin, Is.EqualTo(defaults.Margin));
                Assert.That(authored.DepotCell, Is.EqualTo(defaults.DepotCell));
                Assert.That(authored.DepositCell, Is.EqualTo(defaults.DepositCell));
                LatticeLayoutBuilder.TryUseSettings(null, out _);
            }
        }

        [Test]
        public void ACompositionThatDiffersFromTheDefaultsCarriesItsOwnLayoutIdAndTheShippedCacheFollowsTheSettings()
        {
            var edited = NewSettings(s => s.marginWorldUnits = 1.25f);
            var before = LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait);
            Assert.That(LatticeLayoutBuilder.TryUseSettings(edited, out _), Is.True);
            var after = LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait);
            Assert.That(after.LayoutId, Does.StartWith("lattice-12x3+c"), "the composition hash is part of the layout id");
            Assert.That(after.LayoutId, Is.Not.EqualTo(before.LayoutId), "the camera refits on the id");
            Assert.That(after.Margin, Is.EqualTo(1.25f));
            Assert.That(LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait), Is.SameAs(after), "cached for the same composition");

            edited.marginWorldUnits = 2f;
            var third = LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait);
            Assert.That(third.Margin, Is.EqualTo(2f), "the cache notices an edited value");
            Assert.That(third.LayoutId, Is.Not.EqualTo(after.LayoutId));

            LatticeLayoutBuilder.TryUseSettings(null, out _);
            Assert.That(LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait).LayoutId, Is.EqualTo(before.LayoutId));
            Assert.That(LatticeLayoutBuilder.ActiveSettings, Is.Null);
        }

        [Test]
        public void AnInvalidCompositionIsRejectedWithItsProblemsAndKeepsThePreviousOne()
        {
            var good = NewSettings();
            Assert.That(LatticeLayoutBuilder.TryUseSettings(good, out _), Is.True);
            var bad = NewSettings(s => s.marginWorldUnits = float.NaN);
            Assert.That(LatticeLayoutBuilder.TryUseSettings(bad, out var problems), Is.False);
            Assert.That(problems, Is.Not.Empty);
            Assert.That(LatticeLayoutBuilder.ActiveSettings, Is.SameAs(good));
        }

        [Test]
        public void TheSceneWiresTheAuthoredCompositionAssetIntoTheBootstrap()
        {
            WithScene((bootstrap, _) =>
            {
                var field = typeof(CoreGameFoundationBootstrap).GetField("worldComposition", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(field, Is.Not.Null);
                var asset = (WorldCompositionSettings)field.GetValue(bootstrap);
                Assert.That(asset, Is.Not.Null, "the scene references the composition asset");
                Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo(WorldCompositionSettings.AssetPath));
            });
        }

        // ---- A-13: catalog rows --------------------------------------------------------------------------------------------------

        [Test]
        public void ThePlacedF006RowsAreRequiredAndTheOtherWorldRowsAreNotYet()
        {
            Assert.That(PresentationSpriteCatalog.PlacedF006Keys, Has.Length.EqualTo(18), "3 ground, 4 props and 11 edge pieces");
            foreach (var key in PresentationSpriteCatalog.PlacedF006Keys)
            {
                Assert.That(PresentationSpriteCatalog.TryGetRegistryRequirement(key, out var row), Is.True, key);
                Assert.That(row.Required, Is.True, key + " is placed by the deck, so the shipped catalog fails closed without it");
            }

            Assert.That(PresentationSpriteCatalog.TryGetRegistryRequirement(PresentationSpriteCatalog.OfficeIconKey, out var office), Is.True);
            Assert.That(office.Required, Is.True, "the hub entry icon");
            foreach (var key in new[] { "F006-DEPOT-S1-EMPTY", "F006-DEPOSIT-REST-S1", "F006-LINK-TRUNK", "F006-FX-SPARK", "F006-FOUNDATION-FLAT" })
            {
                Assert.That(PresentationSpriteCatalog.TryGetRegistryRequirement(key, out var row), Is.True, key);
                Assert.That(row.Required, Is.False, key + " is placed by T528 or later");
            }

            Assert.That(PresentationSpriteCatalog.TryGetRegistryRequirement("F004-FLOOR-TILE", out _), Is.False, "the F004 floor row is removed");
        }

        [Test]
        public void TheSceneCatalogRegistersEveryImportedF006KeyAndIsValidWithTheRequiredRows()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(F006ArtIntegration.ManifestPath));
            Assert.That(manifest.entries.Length, Is.EqualTo(49), "40 static world frames, 8 FX entries and the hub glyph");
            WithScene((bootstrap, catalog) =>
            {
                Assert.That(catalog.RequireRegistryRows, Is.True);
                Assert.That(catalog.Diagnostics, Is.Empty);
                foreach (var entry in manifest.entries)
                {
                    Assert.That(catalog.TryGetEntry(entry.key, out var registered), Is.True, entry.key);
                    Assert.That(registered.FrameCount, Is.EqualTo(entry.frames.Length), entry.key);
                    for (var i = 0; i < entry.frames.Length; i++)
                    {
                        Assert.That(AssetDatabase.GetAssetPath(registered.GetFrame(i)), Is.EqualTo(F006ArtIntegration.ArtRoot + "/" + entry.frames[i].file), entry.key);
                    }

                    Assert.That(registered.FramesPerSecond.HasValue, Is.EqualTo(entry.fps > 0), entry.key);
                }

                Assert.That(catalog.TryGetEntry("F004-FLOOR-TILE", out _), Is.False);
            });
        }

        [Serializable]
        private sealed class Frame
        {
            public string file;
        }

        [Serializable]
        private sealed class ManifestEntry
        {
            public string key;
            public string family;
            public int fps;
            public Frame[] frames;
        }

        [Serializable]
        private sealed class Manifest
        {
            public ManifestEntry[] entries;
        }

        // ---- B-17: the Office glyph ----------------------------------------------------------------------------------------------

        [Test]
        public void TheOfficeGlyphIsImportedAt24PpuRegisteredAndUsedAsTheHubEntryIconDistinctFromTheContractsIcon()
        {
            Assert.That(HubCards.EntryIconKey, Is.EqualTo(PresentationSpriteCatalog.OfficeIconKey));
            Assert.That(HubCards.IconKeyOf(HubCard.Contracts), Is.EqualTo("F003-UI-ICON-TAB-CONTRACTS"));
            Assert.That(HubCards.EntryIconKey, Is.Not.EqualTo(HubCards.IconKeyOf(HubCard.Contracts)));

            var path = F006ArtIntegration.ArtRoot + "/ui/f006-ui-office_00.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(24f));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(24f, 24f)));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(12f, 12f)));

            WithScene((bootstrap, catalog) =>
            {
                Assert.That(catalog.TryGet(HubCards.EntryIconKey, out var office), Is.True);
                Assert.That(catalog.TryGet(HubCards.IconKeyOf(HubCard.Contracts), out var contracts), Is.True);
                Assert.That(office, Is.Not.SameAs(contracts));
                Assert.That(AssetDatabase.GetAssetPath(office), Is.EqualTo(path));

                // Distinct glyphs: compare the opaque pixel masks (both are 24 x 24 ui_text glyphs). At least a fifth of the union differs.
                var a = OpaqueMask(office);
                var b = OpaqueMask(contracts);
                int union = 0, different = 0;
                for (var i = 0; i < a.Length; i++)
                {
                    union += a[i] || b[i] ? 1 : 0;
                    different += a[i] != b[i] ? 1 : 0;
                }

                Assert.That(union, Is.GreaterThan(0));
                Assert.That(different / (double)union, Is.GreaterThan(0.2), "the hub glyph and the Contracts glyph differ in at least 20 % of their pixels");
            });
        }

        private static bool[] OpaqueMask(Sprite sprite)
        {
            var path = AssetDatabase.GetAssetPath(sprite);
            var pixels = DeckTestSupport.Pixels(path);
            Assert.That(new[] { pixels.Width, pixels.Height }, Is.EqualTo(new[] { 24, 24 }), path);
            return pixels.Pixels.Select(p => p.a > 0).ToArray();
        }

        // ---- A-10: atlas page inventory ------------------------------------------------------------------------------------------

        [Test]
        public void TheAtlasPageInventoryIsRecordedForTheSetPassBudgetAndEveryPageStaysWithinTheCap()
        {
            var report = new StringBuilder("[T527 atlas pages] SetPass-relevant atlas and page inventory (Art Bible 21.9 item 8: page <= 512 x 512 RGBA32 = 1 MiB)\n");
            var totalPages = 0;
            var paths = F004SpriteAtlasSettings.FindAtlasPaths();
            paths.AddRange(Directory.GetFiles(F006ArtIntegration.AtlasFolder, "*" + F004SpriteAtlasSettings.AtlasExtension).Select(f => f.Replace((char)92, '/')));
            foreach (var path in paths.OrderBy(p => p, StringComparer.Ordinal))
            {
                var pages = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().ToList();
                totalPages += pages.Count;
                foreach (var page in pages)
                {
                    Assert.That(page.width * page.height * 4, Is.LessThanOrEqualTo(F004SpriteAtlasSettings.MaxPageBytes), path);
                }

                report.AppendLine(Path.GetFileNameWithoutExtension(path) + ": " + pages.Count + " page(s) " + string.Join(", ", pages.Select(p => p.width + "x" + p.height)));
            }

            Assert.That(paths.Any(p => p.EndsWith("/floor.spriteatlasv2", StringComparison.Ordinal)), Is.False, "the F004 floor atlas is gone");
            Assert.That(paths.Count, Is.EqualTo(6), "four F004 atlases (machines, slots, cues, markers) and the F006 world and fx atlases");
            report.AppendLine("atlases " + paths.Count + ", pages " + totalPages);
            Debug.Log(report.ToString());
        }

        // ---------------------------------------------------------------------------------------------------------------------------

        private static void WithScene(Action<CoreGameFoundationBootstrap, PresentationSpriteCatalog> check)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var bootstrap = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreGameFoundationBootstrap>(true)).Single();
                var catalog = bootstrap.SpriteCatalog;
                Assert.That(catalog.Validate(), Is.True, catalog.Error);
                check(bootstrap, catalog);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
