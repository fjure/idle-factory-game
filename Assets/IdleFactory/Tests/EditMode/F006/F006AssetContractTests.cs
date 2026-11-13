using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Editor.Art;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T526 (Feature 006, FR-004, FR-044, SC-008; research R06, R17; Art Bible 21.4 and 21.8): the contract of the imported F006 world art
    /// under <c>Assets/IdleFactory/Art/F006/</c> and of the K4 badge tails swapped into <c>Assets/IdleFactory/Art/F004/cues/</c>.
    /// It covers the import settings of every sprite, its provenance (each imported PNG traces to its approved source manifest by key,
    /// family and SHA-256), the two Sprite Atlas v2 assets (membership, one page, sizes, uncompressed), the texture arithmetic of the Art
    /// Bible (40 world frames, 43 FX frames, padded pixel totals), the catalog rows (every key validates through
    /// <see cref="PresentationSpriteCatalog"/>), the V2 finding (deposit rest-s3 touches canvas row 0 and is not clipped), the K4 tail
    /// pixels and the same-change rule of P2-F5 (<see cref="FactoryWorldLayout.KeepOutOf"/> uses the 23 x 27 badge extent).
    /// </summary>
    public sealed class F006AssetContractTests
    {
        private const int WorldFrames = 40;
        private const int FxFrames = 43;
        private const int Density = 24;
        private const int PaddingPerSide = 2;
        private const string DepositRestS3 = "f006-deposit-rest-s3";

        private static readonly string[] CueAssetIds =
        {
            "f003-badge-inactive", "f003-badge-missing-input", "f003-badge-paused", "f003-badge-storage-full", "f003-badge-waiting",
        };

        [Serializable]
        private sealed class ShippedFrame
        {
            public string file;
            public string sha256;
        }

        [Serializable]
        private sealed class ShippedEntry
        {
            public string assetId;
            public string key;
            public string family;
            public string source;
            public int ppu;
            public int fps;
            public int[] canvas;
            public float[] pivot;
            public ShippedFrame[] frames;
        }

        [Serializable]
        private sealed class ShippedManifest
        {
            public int schemaVersion;
            public ShippedEntry[] entries;
        }

        private static ShippedManifest ReadShipped()
        {
            Assert.That(File.Exists(F006ArtIntegration.ManifestPath), Is.True, "T526 ships the F006 import manifest");
            return JsonUtility.FromJson<ShippedManifest>(File.ReadAllText(F006ArtIntegration.ManifestPath));
        }

        private static IEnumerable<ShippedEntry> Entries(params string[] families) =>
            ReadShipped().entries.Where(entry => families.Contains(entry.family));

        private static string[] WorldFamilies => F006ArtIntegration.WorldFamilies;

        private static int FrameCount(IEnumerable<ShippedEntry> entries) => entries.Sum(entry => entry.frames.Length);

        // The packing rule of Art Bible 21.4: every frame is counted at (w + 4) x (h + 4) art px.
        private static int PaddedPixels(IEnumerable<ShippedEntry> entries) =>
            entries.Sum(entry => entry.frames.Length * (entry.canvas[0] + 2 * PaddingPerSide) * (entry.canvas[1] + 2 * PaddingPerSide));

        private static int OpaqueCanvasPixels(IEnumerable<ShippedEntry> entries) =>
            entries.Sum(entry => entry.frames.Length * entry.canvas[0] * entry.canvas[1]);

        private static string AtlasPath(string name) => F006ArtIntegration.AtlasFolder + "/" + name + F004SpriteAtlasSettings.AtlasExtension;

        private static SpriteAtlas LoadAtlas(string name)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath(name));
            Assert.That(atlas, Is.Not.Null, AtlasPath(name));
            return atlas;
        }

        private static Sprite LoadSprite(ShippedFrame frame)
        {
            var path = F006ArtIntegration.ArtRoot + "/" + frame.file;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, path + " imports as a sprite");
            return sprite;
        }

        private static Texture2D ReadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), Is.True, path);
            return texture;
        }

        // ---- provenance and manifest ----------------------------------------------------------------------------------

        [Test]
        public void TheShippedManifestListsEverySourceEntryWithItsKeyFamilyAndHashes()
        {
            var shipped = ReadShipped();
            Assert.That(shipped.schemaVersion, Is.EqualTo(F004ImportRules.SupportedSchemaVersion));
            Assert.That(shipped.entries.Length, Is.EqualTo(48), "40 world assets and 8 FX assets");
            var byAsset = shipped.entries.ToDictionary(entry => entry.assetId, StringComparer.Ordinal);
            Assert.That(byAsset.Count, Is.EqualTo(shipped.entries.Length), "asset ids are unique");
            Assert.That(shipped.entries.Select(entry => entry.key).Distinct().Count(), Is.EqualTo(shipped.entries.Length), "keys are unique");

            var expected = 0;
            foreach (var folder in F006ArtIntegration.FamilyNames)
            {
                var sourceManifest = F006ArtIntegration.SourceRoot + "/" + folder + "/manifest.json";
                foreach (var source in F004ArtIntegration.ReadSourceEntries(sourceManifest))
                {
                    expected++;
                    Assert.That(byAsset.TryGetValue(source.AssetId, out var entry), Is.True, source.AssetId);
                    Assert.That(entry.key, Is.EqualTo(F006ArtIntegration.KeyOf(source.AssetId)), source.AssetId);
                    Assert.That(entry.family, Is.EqualTo(F006ArtIntegration.FamilyOf(source.AssetId, folder)), source.AssetId);
                    Assert.That(entry.source, Is.EqualTo(sourceManifest), source.AssetId + " traces to its approved source manifest");
                    Assert.That(entry.frames.Select(frame => frame.sha256), Is.EqualTo(source.FrameHashes), source.AssetId + " keeps the source hashes");
                    Assert.That(entry.ppu, Is.EqualTo(Density), source.AssetId);
                }
            }

            Assert.That(shipped.entries.Length, Is.EqualTo(expected), "the shipped manifest holds no entry without an approved source");
            Assert.That(shipped.entries.Count(entry => entry.assetId == "f006-fx-vein-chip" && entry.family == "fx"), Is.EqualTo(1),
                "the vein chip is authored with the deposit but is an FX asset");
        }

        [Test]
        public void EveryImportedPngIsAByteForByteCopyOfItsApprovedSourceFrame()
        {
            var checkedFrames = 0;
            foreach (var entry in ReadShipped().entries)
            {
                var sourceFolder = Path.GetDirectoryName(entry.source).Replace('\\', '/');
                foreach (var frame in entry.frames)
                {
                    var imported = F006ArtIntegration.ArtRoot + "/" + frame.file;
                    var importedHash = F004ImportRules.Sha256Hex(File.ReadAllBytes(imported));
                    Assert.That(importedHash, Is.EqualTo(frame.sha256).IgnoreCase, imported + " (hand edit)");
                    var sourcePath = sourceFolder + "/" + entry.assetId + "/" + Path.GetFileName(frame.file);
                    Assert.That(F004ImportRules.Sha256Hex(File.ReadAllBytes(sourcePath)), Is.EqualTo(frame.sha256).IgnoreCase, sourcePath);
                    checkedFrames++;
                }
            }

            Assert.That(checkedFrames, Is.EqualTo(WorldFrames + FxFrames));
        }

        [Test]
        public void EveryPngUnderTheF006ArtPathIsListedInTheShippedManifest()
        {
            var listed = new HashSet<string>(
                ReadShipped().entries.SelectMany(entry => entry.frames).Select(frame => F006ArtIntegration.ArtRoot + "/" + frame.file),
                StringComparer.Ordinal);
            var governed = Directory.GetFiles(F006ArtIntegration.ArtRoot, "*.png", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .ToList();
            Assert.That(governed, Is.Not.Empty);
            Assert.That(governed.Where(path => !listed.Contains(path)).ToList(), Is.Empty, "every PNG under Art/F006 is listed");
            Assert.That(listed.Count, Is.EqualTo(governed.Count));
        }

        [Test]
        public void TheF006PostprocessorGovernsOnlyTheF006PathAndTheF004OneNeverTouchesIt()
        {
            Assert.That(F006SpriteImportPostprocessor.IsGovernedPath(F006ArtIntegration.ArtRoot + "/ground/f006-ground-base_00.png"), Is.True);
            Assert.That(F006SpriteImportPostprocessor.IsGovernedPath(F004ImportRules.ArtRoot + "/cues/f003-badge-waiting_00.png"), Is.False);
            Assert.That(F004ImportRules.IsGovernedPath(F006ArtIntegration.ArtRoot + "/ground/f006-ground-base_00.png"), Is.False);
            Assert.That(F006SpriteImportPostprocessor.IsGovernedPath(F006ArtIntegration.ArtRoot + "/ground/readme.txt"), Is.False);
        }

        // ---- import settings --------------------------------------------------------------------------------------------

        [Test]
        public void EverySpriteIsImportedAtPpu24WithPointFilteringNoCompressionAndItsManifestPivot()
        {
            var checkedFrames = 0;
            foreach (var entry in ReadShipped().entries)
            {
                foreach (var frame in entry.frames)
                {
                    var path = F006ArtIntegration.ArtRoot + "/" + frame.file;
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                    Assert.That(importer.spritePixelsPerUnit, Is.EqualTo((float)Density), path);
                    Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(importer.mipmapEnabled, Is.False, path);
                    Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
                    Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                    Assert.That(importer.isReadable, Is.False, path);
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), path);

                    var sprite = LoadSprite(frame);
                    Assert.That(sprite.rect.width, Is.EqualTo((float)entry.canvas[0]), path);
                    Assert.That(sprite.rect.height, Is.EqualTo((float)entry.canvas[1]), path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo((float)Density), path);
                    Assert.That(sprite.pivot.x, Is.EqualTo(entry.pivot[0] * entry.canvas[0]).Within(0.01f), path + " pivot x");
                    Assert.That(sprite.pivot.y, Is.EqualTo(entry.pivot[1] * entry.canvas[1]).Within(0.01f), path + " pivot y");
                    checkedFrames++;
                }
            }

            Assert.That(checkedFrames, Is.EqualTo(WorldFrames + FxFrames));
        }

        // ---- texture arithmetic and atlases ----------------------------------------------------------------------------

        [Test]
        public void TheSpriteCountsMatchTheArtBibleTable()
        {
            var perFamily = new Dictionary<string, int>
            {
                { "ground", 3 }, { "foundation", 2 }, { "edge", 11 }, { "depot", 12 }, { "link", 4 }, { "prop", 4 }, { "deposit", 4 },
            };
            foreach (var pair in perFamily)
            {
                Assert.That(FrameCount(Entries(pair.Key)), Is.EqualTo(pair.Value), pair.Key);
            }

            var world = Entries(WorldFamilies).ToList();
            Assert.That(FrameCount(world), Is.EqualTo(WorldFrames));
            Assert.That(world.All(entry => entry.frames.Length == 1), Is.True, "the world families are static");
            Assert.That(OpaqueCanvasPixels(world), Is.EqualTo(170124));
            Assert.That(PaddedPixels(world), Is.EqualTo(190956));

            var fx = Entries("fx").ToList();
            Assert.That(FrameCount(fx), Is.EqualTo(FxFrames), "42 frames of the section 21.4 table plus the P2-F2 completion highlight");
            Assert.That(fx.Select(entry => entry.assetId), Does.Contain("f006-fx-completion-highlight"));
            Assert.That(fx.Single(entry => entry.assetId == "f006-fx-completion-highlight").frames.Length, Is.EqualTo(1));
            Assert.That(OpaqueCanvasPixels(fx), Is.EqualTo(45816));
            Assert.That(PaddedPixels(fx), Is.EqualTo(57144), "P2-F2: (68+4)(36+4) = 2,880 padded px more than the 54,264 of the 42-frame plan");
        }

        [Test]
        public void BothAtlasesAreV2AssetsWithTheUncompressedPointFilteredContract()
        {
            foreach (var name in new[] { F006ArtIntegration.WorldAtlasName, F006ArtIntegration.FxAtlasName })
            {
                var importer = AssetImporter.GetAtPath(AtlasPath(name)) as SpriteAtlasImporter;
                Assert.That(importer, Is.Not.Null, name);
                Assert.That(F004SpriteAtlasSettings.Validate(importer), Is.Empty, name);
                Assert.That(importer.packingSettings.enableTightPacking, Is.False, name + ": full-rect packing keeps the whole canvas");
                Assert.That(importer.packingSettings.enableRotation, Is.False, name);
                Assert.That(importer.packingSettings.padding, Is.GreaterThanOrEqualTo(PaddingPerSide), name);
                Assert.That(importer.textureSettings.filterMode, Is.EqualTo(FilterMode.Point), name);
                Assert.That(importer.textureSettings.generateMipMaps, Is.False, name);
                foreach (var platform in F004SpriteAtlasSettings.Platforms)
                {
                    var settings = importer.GetPlatformSettings(platform);
                    Assert.That(settings.overridden, Is.True, name + " " + platform);
                    Assert.That(settings.format, Is.EqualTo(TextureImporterFormat.RGBA32), name + " " + platform);
                    Assert.That(settings.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), name + " " + platform);
                    Assert.That(settings.maxTextureSize, Is.LessThanOrEqualTo(F004SpriteAtlasSettings.MaxPageSize), name + " " + platform);
                }
            }
        }

        [Test]
        public void TheWorldAtlasPacksTheSevenStaticFamiliesAndTheFxAtlasPacksTheFxFamily()
        {
            var world = LoadAtlas(F006ArtIntegration.WorldAtlasName);
            var worldPackables = SpriteAtlasExtensions.GetPackables(world).Select(AssetDatabase.GetAssetPath).ToList();
            Assert.That(worldPackables, Is.EquivalentTo(WorldFamilies.Select(family => F006ArtIntegration.ArtRoot + "/" + family)));
            Assert.That(world.spriteCount, Is.EqualTo(WorldFrames), "every static frame, no FX");

            var fx = LoadAtlas(F006ArtIntegration.FxAtlasName);
            var fxPackables = SpriteAtlasExtensions.GetPackables(fx).Select(AssetDatabase.GetAssetPath).ToList();
            Assert.That(fxPackables, Is.EqualTo(new[] { F006ArtIntegration.ArtRoot + "/fx" }));
            Assert.That(fx.spriteCount, Is.EqualTo(FxFrames), "every FX frame, including the completion highlight and the vein chip");
        }

        [Test]
        public void EveryFrameBindsToItsAtlasAndOnlyToIt()
        {
            var world = LoadAtlas(F006ArtIntegration.WorldAtlasName);
            var fx = LoadAtlas(F006ArtIntegration.FxAtlasName);
            foreach (var entry in ReadShipped().entries)
            {
                var own = entry.family == "fx" ? fx : world;
                var other = entry.family == "fx" ? world : fx;
                foreach (var frame in entry.frames)
                {
                    var sprite = LoadSprite(frame);
                    Assert.That(own.CanBindTo(sprite), Is.True, frame.file + " is packed in its own atlas");
                    Assert.That(other.CanBindTo(sprite), Is.False, frame.file + " is not in the other atlas");
                }
            }
        }

        [Test]
        public void EachAtlasHasExactlyOnePageWithinBudget7AndTheFramesFitIt()
        {
            foreach (var pair in new[]
                     {
                         new KeyValuePair<string, int>(F006ArtIntegration.WorldAtlasName, PaddedPixels(Entries(WorldFamilies))),
                         new KeyValuePair<string, int>(F006ArtIntegration.FxAtlasName, PaddedPixels(Entries("fx"))),
                     })
            {
                var pages = AssetDatabase.LoadAllAssetsAtPath(AtlasPath(pair.Key)).OfType<Texture2D>().ToList();
                Assert.That(pages, Has.Count.EqualTo(1), pair.Key + " is one page (P2-F2: the FX atlas holds 43 frames on one page)");
                var page = pages[0];
                Assert.That(page.width, Is.LessThanOrEqualTo(F004SpriteAtlasSettings.MaxPageSize), pair.Key);
                Assert.That(page.height, Is.LessThanOrEqualTo(F004SpriteAtlasSettings.MaxPageSize), pair.Key);
                Assert.That(page.width * page.height, Is.GreaterThanOrEqualTo(pair.Value),
                    pair.Key + " page " + page.width + " x " + page.height + " holds the " + pair.Value + " padded px of its frames");
                Assert.That(page.filterMode, Is.EqualTo(FilterMode.Point), pair.Key);
                TestContext.WriteLine("[T526] " + pair.Key + " atlas page " + page.width + " x " + page.height + ", padded frames " + pair.Value + " px, fill "
                    + (100.0 * pair.Value / (page.width * page.height)).ToString("0.0") + " %");
            }
        }

        [Test]
        public void TheDepositRestS3SpriteKeepsItsTopCanvasRowThroughTheWorldAtlasPadding()
        {
            // Art Direction V2: rest-s3 has opaque pixels on canvas row 0, so a tight or clipped pack would cut it.
            var entry = Entries("deposit").Single(candidate => candidate.assetId == DepositRestS3);
            var png = ReadPng(F006ArtIntegration.ArtRoot + "/" + entry.frames[0].file);
            var topRow = png.height - 1;
            var opaqueInTopRow = Enumerable.Range(0, png.width).Count(x => png.GetPixel(x, topRow).a > 0f);
            Assert.That(opaqueInTopRow, Is.GreaterThan(0), "the source sprite touches canvas row 0");

            var sprite = LoadSprite(entry.frames[0]);
            Assert.That(sprite.rect.height, Is.EqualTo(34f), "the imported sprite is the full 48 x 34 canvas");
            var packed = LoadAtlas(F006ArtIntegration.WorldAtlasName).GetSprite(sprite.name);
            Assert.That(packed, Is.Not.Null, "the sprite is a member of the world atlas");
            Assert.That(packed.textureRect.width, Is.EqualTo(48f));
            Assert.That(packed.textureRect.height, Is.EqualTo(34f), "the packed rect is not clipped at the top row");
            UnityEngine.Object.DestroyImmediate(packed);
            UnityEngine.Object.DestroyImmediate(png);
        }

        // ---- catalog keys -----------------------------------------------------------------------------------------------

        [Test]
        public void EveryF006KeyValidatesThroughItsRegistryRow()
        {
            var catalogEntries = new List<PresentationSpriteEntry>();
            foreach (var entry in ReadShipped().entries)
            {
                Assert.That(entry.key, Does.StartWith(PresentationSpriteCatalog.F006Prefix));
                var frames = entry.frames.Select(LoadSprite).ToList();
                catalogEntries.Add(new PresentationSpriteEntry(entry.key, frames, entry.fps > 0 ? entry.fps : (float?)null));
            }

            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(catalogEntries);
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Diagnostics, Is.Empty);
            Assert.That(catalog.Count, Is.EqualTo(48));
            foreach (var entry in ReadShipped().entries)
            {
                Assert.That(catalog.TryGetEntry(entry.key, out var registered), Is.True, entry.key);
                Assert.That(registered.FrameCount, Is.EqualTo(entry.frames.Length), entry.key);
                Assert.That(registered.FramesPerSecond ?? 0f, Is.EqualTo((float)entry.fps), entry.key);
            }
        }

        [Test]
        public void ARegisteredF006KeyWithAWrongSizedFrameFailsItsRow()
        {
            var entry = Entries("ground").First();
            var wrong = LoadSprite(Entries("depot").First().frames[0]);
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(new[] { new PresentationSpriteEntry(entry.key, wrong) });
            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Diagnostics, Has.Some.Contain(PresentationSpriteCatalog.KeyUnresolvedCode));
        }

        // ---- K4 tail swap (P2-F5, F-11) --------------------------------------------------------------------------------

        [Test]
        public void TheImportedF004CueSpritesAreTheApprovedK4TailSprites()
        {
            var f004 = JsonUtility.FromJson<ShippedManifest>(File.ReadAllText(F004ImportRules.ManifestPath));
            var sourceEntries = F004ArtIntegration.ReadSourceEntries(F006ArtIntegration.CueSourceFolder + "/manifest.json");
            Assert.That(sourceEntries.Select(entry => entry.AssetId), Is.EquivalentTo(CueAssetIds));
            foreach (var source in sourceEntries)
            {
                var shipped = f004.entries.Single(entry => entry.assetId == source.AssetId);
                Assert.That(shipped.frames.Select(frame => frame.sha256), Is.EqualTo(source.FrameHashes), source.AssetId + " carries the K4 hash");
                var path = F004ImportRules.ArtRoot + "/" + shipped.frames[0].file;
                Assert.That(F004ImportRules.Sha256Hex(File.ReadAllBytes(path)), Is.EqualTo(source.FrameHashes[0]).IgnoreCase, path);
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null, path);
            }
        }

        [Test]
        public void EachK4CueSpriteCarriesTheSymmetricTailBelowTheBadgeBox()
        {
            // Art Direction K4 pixel review: tail rows 33 to 36 (from the top) are 7, 5, 3 and 1 px wide, centred on column 59, and nothing
            // lies below row 36; the 80 x 64 canvas, the box (rows 10 to 31) and the pivot are unchanged.
            foreach (var id in CueAssetIds)
            {
                var png = ReadPng(F004ImportRules.ArtRoot + "/cues/" + id + "_00.png");
                Assert.That(png.width, Is.EqualTo(80), id);
                Assert.That(png.height, Is.EqualTo(64), id);
                int OpaqueIn(int rowFromTop) => Enumerable.Range(0, png.width).Count(x => png.GetPixel(x, png.height - 1 - rowFromTop).a > 0f);
                Assert.That(new[] { OpaqueIn(33), OpaqueIn(34), OpaqueIn(35), OpaqueIn(36) }, Is.EqualTo(new[] { 7, 5, 3, 1 }), id + " tail rows");
                Assert.That(png.GetPixel(59, png.height - 1 - 36).a, Is.GreaterThan(0f), id + " tip on column 59");
                for (var row = 37; row < png.height; row++)
                {
                    Assert.That(OpaqueIn(row), Is.EqualTo(0), id + " row " + row);
                }

                UnityEngine.Object.DestroyImmediate(png);
            }
        }

        [Test]
        public void KeepOutBoxZeroIsTheBadgeExtentSoNoDecorationMayCoverTheTail()
        {
            var extent = StateCueGeometry.BadgeExtentBox();
            var badge = StateCueGeometry.BadgeBox();
            Assert.That(extent.width * Density, Is.EqualTo(23f).Within(1e-3f));
            Assert.That(extent.height * Density, Is.EqualTo(27f).Within(1e-3f), "the 23 x 27 badge extent of the K4 tail");
            foreach (var name in TestLayouts.Names)
            {
                var layout = TestLayouts.ByName(name);
                for (var i = 0; i < layout.LocationCount; i++)
                {
                    var id = new BuildingLocationId(i);
                    var keepOut = layout.KeepOutOf(id, 0);
                    Assert.That(keepOut.position, Is.EqualTo(layout.AnchorOf(id) + extent.position), name + " " + id);
                    Assert.That(keepOut.size, Is.EqualTo(extent.size), name + " " + id);
                    var tail = new Rect(keepOut.xMin, keepOut.yMin, keepOut.width, badge.yMin - extent.yMin);
                    Assert.That(tail.height * Density, Is.EqualTo(4f).Within(1e-3f), "the tail is 4 art px tall");
                    Assert.That(keepOut.yMax, Is.EqualTo(layout.AnchorOf(id).y + badge.yMax).Within(1e-4f), "the top edge does not move");
                }
            }
        }
    }
}
