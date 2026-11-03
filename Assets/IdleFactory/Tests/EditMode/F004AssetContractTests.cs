using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IdleFactory.Editor.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T430 (FR-022, FR-023; research R10; contracts/art-pipeline.md "Unity side"): the F004 sprite import rules and the Sprite
    /// Atlas v2 contract. Production art does not exist yet (T434 to T440), so the importer is proven on throw-away fixtures
    /// (PNGs and a manifest under a temporary folder of the F004 art path, deleted by each test), and the shipped manifest and
    /// atlases are validated whenever they exist. The Sprite Atlas v2 packer mode is a recorded settings decision
    /// (<c>ProjectSettings/EditorSettings.asset</c>, <c>IDLEFACTORY_ALLOW_SETTINGS_NOISE=1</c>); this class is the revert
    /// assertion, so a settings restore that drops it fails here. Catalog resolution (SC-003) and the golden key list belong to
    /// T440 with the production art.
    /// </summary>
    public sealed class F004AssetContractTests
    {
        private const string F003SpriteRoot = "Assets/IdleFactory/Art/Development/F003/Sprites";

        // ---- settings decision ------------------------------------------------------------------------------------

        [Test]
        public void SpriteAtlasV2PackerModeIsEnabledAsRecordedSettingsDecision()
        {
            Assert.That(EditorSettings.spritePackerMode, Is.EqualTo(SpritePackerMode.SpriteAtlasV2),
                "Sprite Atlas v2 packing is a deliberate EditorSettings.asset decision (T430, research R10); restoring ProjectSettings from noise must keep it.");
            Assert.That(File.ReadAllText("ProjectSettings/EditorSettings.asset"), Does.Contain("m_SpritePackerMode: 5"));
        }

        [Test]
        public void PostprocessorIsVersionedAndUsesTheManifestSchemaOfThePipeline()
        {
            Assert.That(F004ImportRules.PostprocessorVersion, Is.GreaterThan(0u));
            Assert.That(new F004SpriteImportPostprocessor().GetVersion(), Is.EqualTo(F004ImportRules.PostprocessorVersion));
            Assert.That(F004ImportRules.SupportedSchemaVersion, Is.EqualTo(1));
            Assert.That(F004ImportRules.ManifestPath, Is.EqualTo("Assets/IdleFactory/Art/F004/import-manifest.json"));
        }

        // ---- path scoping -----------------------------------------------------------------------------------------

        [Test]
        public void OnlyF004ArtPngsOutsideTheSpikeFolderAreGoverned()
        {
            Assert.That(F004ImportRules.IsGovernedPath("Assets/IdleFactory/Art/F004/Machines/F003-MACHINE-MINER_00.png"), Is.True);
            Assert.That(F004ImportRules.IsGovernedPath("Assets/IdleFactory/Art/F004/Spike/D24/a.png"), Is.False);
            Assert.That(F004ImportRules.IsGovernedPath(F003SpriteRoot + "/f003-machine-miner-v01.png"), Is.False);
            Assert.That(F004ImportRules.IsGovernedPath("Assets/IdleFactory/Art/F004/Machines/readme.json"), Is.False);
            Assert.That(F004ImportRules.IsGovernedPath(null), Is.False);
        }

        [Test]
        public void F003PlaceholdersKeepTheirOwnPixelsPerUnit()
        {
            var pngs = Directory.GetFiles(F003SpriteRoot, "*.png");
            Assert.That(pngs, Is.Not.Empty);
            foreach (var png in pngs)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(png.Replace('\\', '/'));
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(64f), png);
            }
        }

        // ---- entry validation (pure rules) ------------------------------------------------------------------------

        [Test]
        public void ValidEntryHasNoDiagnostics()
        {
            Assert.That(F004ImportRules.ValidateEntry(Entry("F004-A", 24, 80, 64, 40, 8, "a_00.png")), Is.Empty);
        }

        [Test]
        public void NonIntegralPivotIsRejectedWithAClearDiagnostic()
        {
            var problems = F004ImportRules.ValidateEntry(Entry("F004-A", 24, 80, 64, 40.5f, 8, "a_00.png"));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("F004-A").And.Contain("not a whole pixel").And.Contain("rule P1"));
        }

        [Test]
        public void PivotWithinTheIntegralToleranceIsAccepted()
        {
            // 5 / 24 = 0.208333 rounded to six digits is not float-exact; the tolerance is 1e-3 art px.
            var entry = Entry("F004-A", 24, 24, 24, 12, 5, "a_00.png");
            entry.pivot = new[] { 0.5f, 0.208333f };
            Assert.That(F004ImportRules.ValidateEntry(entry), Is.Empty);
            entry.pivot = new[] { 0.5f, 0.2085f };
            Assert.That(F004ImportRules.ValidateEntry(entry), Is.Not.Empty);
        }

        [Test]
        public void DensityMustBeAnArtDensityNotTheF003Ppu()
        {
            var problems = F004ImportRules.ValidateEntry(Entry("F004-A", 64, 80, 64, 40, 8, "a_00.png"));
            Assert.That(problems, Has.Some.Contain("ppu 64"));
        }

        [Test]
        public void IntentionallyEmptyEntryNeedsNoPngAndRejectsAttachedFiles()
        {
            var empty = new F004ManifestEntry { assetId = "F003-BADGE-RUNNING", intentionallyEmpty = true };
            Assert.That(F004ImportRules.ValidateEntry(empty), Is.Empty);

            var withFrames = new F004ManifestEntry
            {
                assetId = "F003-BADGE-RUNNING",
                intentionallyEmpty = true,
                frames = new[] { new F004ManifestFrame { file = "x.png", sha256 = new string('a', 64) } },
            };
            Assert.That(F004ImportRules.ValidateEntry(withFrames), Has.Some.Contain("intentionallyEmpty"));

            var withPng = new F004ManifestEntry { assetId = "F003-BADGE-RUNNING", intentionallyEmpty = true, png = "x.png" };
            Assert.That(F004ImportRules.ValidateEntry(withPng), Has.Some.Contain("intentionallyEmpty"));
        }

        [Test]
        public void EntryWithoutFramesOrWithABadHashIsRejected()
        {
            var noFrames = Entry("F004-A", 24, 80, 64, 40, 8, "a_00.png");
            noFrames.frames = Array.Empty<F004ManifestFrame>();
            Assert.That(F004ImportRules.ValidateEntry(noFrames), Has.Some.Contain("no frames"));

            var badHash = Entry("F004-A", 24, 80, 64, 40, 8, "a_00.png");
            badHash.frames[0].sha256 = "abc";
            Assert.That(F004ImportRules.ValidateEntry(badHash), Has.Some.Contain("sha256"));
        }

        [Test]
        public void UnsupportedSchemaVersionIsRejected()
        {
            Assert.That(F004ImportRules.TryParse("{\"schemaVersion\":2,\"entries\":[]}", out _, out var error), Is.False);
            Assert.That(error, Does.Contain("schemaVersion 2"));
            Assert.That(F004ImportRules.TryParse("{\"schemaVersion\":1,\"entries\":[]}", out var manifest, out _), Is.True);
            Assert.That(manifest.entries, Is.Empty);
        }

        // ---- importer on fixtures ---------------------------------------------------------------------------------

        [Test]
        public void ListedPngsImportWithDensityPivotPointFilterNoMipsAndUncompressedRgba32()
        {
            using (var fixture = new Fixture())
            {
                var a0 = fixture.AddPng("a_00.png", 80, 64);
                var a1 = fixture.AddPng("a_01.png", 80, 64);
                fixture.WriteManifest(
                    ManifestJson(1, EntryJson("F004-A", 24, 80, 64, 0.5f, 0.125f, a0, a1), EmptyEntryJson("F003-BADGE-RUNNING")));
                fixture.ImportPngs();

                foreach (var path in new[] { a0.Path, a1.Path })
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(24f), path);
                    Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(importer.mipmapEnabled, Is.False, path);
                    Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
                    Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);

                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(24f), path);
                    Assert.That(sprite.pivot.x, Is.EqualTo(40f).Within(0.01f), path);
                    Assert.That(sprite.pivot.y, Is.EqualTo(8f).Within(0.01f), path);
                    Assert.That(sprite.texture.format, Is.EqualTo(TextureFormat.RGBA32), path);
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(sprite.texture.mipmapCount, Is.EqualTo(1), path);
                }
            }
        }

        [Test]
        public void IntegralPivotFromAFractionAcceptedWithinTolerance()
        {
            using (var fixture = new Fixture())
            {
                var png = fixture.AddPng("a_00.png", 24, 24);
                fixture.WriteManifest(ManifestJson(1, EntryJson("F004-A", 24, 24, 24, 0.5f, 0.208333f, png)));
                fixture.ImportPngs();

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png.Path);
                Assert.That(((TextureImporter)AssetImporter.GetAtPath(png.Path)).spritePixelsPerUnit, Is.EqualTo(24f));
                Assert.That(sprite.pivot.y, Is.EqualTo(5f).Within(0.01f));
            }
        }

        [Test]
        public void ManifestEditReimportsTheSpriteWithTheNewPivot()
        {
            using (var fixture = new Fixture())
            {
                var png = fixture.AddPng("a_00.png", 80, 64);
                fixture.WriteManifest(ManifestJson(1, EntryJson("F004-A", 24, 80, 64, 0.5f, 0.125f, png)));
                fixture.ImportPngs();
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(png.Path).pivot.y, Is.EqualTo(8f).Within(0.01f));

                fixture.WriteManifest(ManifestJson(1, EntryJson("F004-A", 24, 80, 64, 0.5f, 0.15625f, png)));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(png.Path).pivot.y, Is.EqualTo(10f).Within(0.01f),
                    "the manifest is an import dependency of its PNGs");
            }
        }

        [Test]
        public void NonIntegralPivotAppliesNothingAndLogsTheDiagnostic()
        {
            using (var fixture = new Fixture())
            {
                var png = fixture.AddPng("a_00.png", 80, 64);
                fixture.WriteManifest(ManifestJson(1, EntryJson("F004-A", 24, 80, 64, 0.50625f, 0.125f, png)));
                LogAssert.Expect(LogType.Warning, new Regex("F004 import rejected .*a_00\\.png.*F004-A.*not a whole pixel"));
                fixture.ImportPngs();

                Assert.That(((TextureImporter)AssetImporter.GetAtPath(png.Path)).spritePixelsPerUnit, Is.EqualTo(100f));
            }
        }

        [Test]
        public void HashMismatchAppliesNothingAndLogsTheDiagnostic()
        {
            using (var fixture = new Fixture())
            {
                var png = fixture.AddPng("a_00.png", 80, 64);
                var stale = new FixturePng { Path = png.Path, FileName = png.FileName, Sha256 = new string('0', 64) };
                fixture.WriteManifest(ManifestJson(1, EntryJson("F004-A", 24, 80, 64, 0.5f, 0.125f, stale)));
                LogAssert.Expect(LogType.Warning, new Regex("F004 import rejected .*a_00\\.png.*does not match the manifest hash"));
                fixture.ImportPngs();

                Assert.That(((TextureImporter)AssetImporter.GetAtPath(png.Path)).spritePixelsPerUnit, Is.EqualTo(100f));
            }
        }

        [Test]
        public void WrongSchemaVersionAppliesNothing()
        {
            using (var fixture = new Fixture())
            {
                var png = fixture.AddPng("a_00.png", 80, 64);
                fixture.WriteManifest(ManifestJson(2, EntryJson("F004-A", 24, 80, 64, 0.5f, 0.125f, png)));
                LogAssert.Expect(LogType.Warning, new Regex("F004 import rejected .*a_00\\.png.*schemaVersion 2"));
                fixture.ImportPngs();

                Assert.That(((TextureImporter)AssetImporter.GetAtPath(png.Path)).spritePixelsPerUnit, Is.EqualTo(100f));
            }
        }

        [Test]
        public void F003DensityInTheManifestIsRejected()
        {
            using (var fixture = new Fixture())
            {
                var png = fixture.AddPng("a_00.png", 80, 64);
                fixture.WriteManifest(ManifestJson(1, EntryJson("F004-A", 64, 80, 64, 0.5f, 0.125f, png)));
                LogAssert.Expect(LogType.Warning, new Regex("F004 import rejected .*ppu 64 is not an art density"));
                fixture.ImportPngs();

                Assert.That(((TextureImporter)AssetImporter.GetAtPath(png.Path)).spritePixelsPerUnit, Is.EqualTo(100f));
            }
        }

        [Test]
        public void CanvasThatDiffersFromThePngSizeIsReported()
        {
            using (var fixture = new Fixture())
            {
                var png = fixture.AddPng("a_00.png", 80, 64);
                fixture.WriteManifest(ManifestJson(1, EntryJson("F004-A", 24, 96, 64, 0.5f, 0.125f, png)));
                LogAssert.Expect(LogType.Warning, new Regex("F004 import rejected .*a_00\\.png.*80x64.*canvas is 96x64"));
                fixture.ImportPngs();

                // Consistent with the other rejection paths: nothing is applied, the importer stays at defaults.
                var importer = (TextureImporter)AssetImporter.GetAtPath(png.Path);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f));
                Assert.That(importer.filterMode, Is.Not.EqualTo(FilterMode.Point));
                Assert.That(importer.textureType, Is.Not.EqualTo(TextureImporterType.Sprite));
            }
        }

        [Test]
        public void UnlistedPngIsLeftUntouchedWithAWarning()
        {
            using (var fixture = new Fixture())
            {
                var listed = fixture.AddPng("a_00.png", 80, 64);
                var unlisted = fixture.AddPng("b_00.png", 80, 64);
                fixture.WriteManifest(ManifestJson(1, EntryJson("F004-A", 24, 80, 64, 0.5f, 0.125f, listed)));
                LogAssert.Expect(LogType.Warning, new Regex("F004 import skipped .*b_00\\.png.*no manifest entry"));
                fixture.ImportPngs();

                Assert.That(((TextureImporter)AssetImporter.GetAtPath(listed.Path)).spritePixelsPerUnit, Is.EqualTo(24f));
                Assert.That(((TextureImporter)AssetImporter.GetAtPath(unlisted.Path)).spritePixelsPerUnit, Is.EqualTo(100f));
            }
        }

        [Test]
        public void IntentionallyEmptyEntryWithoutPngImportsCleanlyAlongsideOtherEntries()
        {
            using (var fixture = new Fixture())
            {
                var png = fixture.AddPng("a_00.png", 80, 64);
                fixture.WriteManifest(ManifestJson(1, EmptyEntryJson("F003-BADGE-RUNNING"), EntryJson("F004-A", 24, 80, 64, 0.5f, 0.125f, png)));
                fixture.ImportPngs();

                Assert.That(((TextureImporter)AssetImporter.GetAtPath(png.Path)).spritePixelsPerUnit, Is.EqualTo(24f));
            }
        }

        [Test]
        public void IntentionallyEmptyEntryWithAPngIsRejected()
        {
            using (var fixture = new Fixture())
            {
                var png = fixture.AddPng("a_00.png", 80, 64);
                var contradictory = "{\"assetId\":\"F003-BADGE-RUNNING\",\"intentionallyEmpty\":true,\"frames\":[{\"file\":\"" + png.FileName
                    + "\",\"sha256\":\"" + png.Sha256 + "\"}]}";
                fixture.WriteManifest(ManifestJson(1, contradictory));
                LogAssert.Expect(LogType.Warning, new Regex("F004 import rejected .*intentionallyEmpty entry must not carry"));
                fixture.ImportPngs();

                Assert.That(((TextureImporter)AssetImporter.GetAtPath(png.Path)).spritePixelsPerUnit, Is.EqualTo(100f));
            }
        }

        // ---- shipped manifest -------------------------------------------------------------------------------------

        [Test]
        public void EveryShippedManifestEntryIsImportedWithItsDeclaredSettings()
        {
            if (!File.Exists(F004ImportRules.ManifestPath))
            {
                return; // No production art until T440; the fixture tests above prove the rules.
            }

            Assert.That(F004ImportRules.TryParse(File.ReadAllText(F004ImportRules.ManifestPath), out var manifest, out var error), Is.True, error);
            foreach (var entry in manifest.entries ?? Array.Empty<F004ManifestEntry>())
            {
                Assert.That(F004ImportRules.ValidateEntry(entry), Is.Empty, entry?.assetId);
                if (entry.intentionallyEmpty)
                {
                    continue;
                }

                foreach (var frame in entry.frames)
                {
                    var path = F004ImportRules.ArtRoot + "/" + frame.file.Replace('\\', '/');
                    Assert.That(F004ImportRules.Sha256Hex(File.ReadAllBytes(path)), Is.EqualTo(frame.sha256).IgnoreCase, path + " (hand edit)");
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Assert.That(importer.spritePixelsPerUnit, Is.EqualTo((float)entry.ppu), path);
                    Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(importer.mipmapEnabled, Is.False, path);
                    Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
                    Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                    Assert.That(importer.isReadable, Is.False, path);
                    var importerSettings = new TextureImporterSettings();
                    importer.ReadTextureSettings(importerSettings);
                    Assert.That(importerSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), path);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Assert.That(sprite.rect.width, Is.EqualTo((float)entry.canvas[0]), path);
                    Assert.That(sprite.rect.height, Is.EqualTo((float)entry.canvas[1]), path);
                    var px = F004ImportRules.PivotPixels(entry);
                    Assert.That(sprite.pivot.x, Is.EqualTo(px.x).Within(0.01f), path);
                    Assert.That(sprite.pivot.y, Is.EqualTo(px.y).Within(0.01f), path);
                }
            }
        }

        [Test]
        public void EveryGovernedPngUnderTheF004ArtPathIsListedInTheShippedManifest()
        {
            var governed = Directory.GetFiles(F004ImportRules.ArtRoot, "*.png", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .Where(F004ImportRules.IsGovernedPath)
                .Where(path => !IsFixturePath(path))
                .ToList();
            var listed = new HashSet<string>(StringComparer.Ordinal);
            if (File.Exists(F004ImportRules.ManifestPath)
                && F004ImportRules.TryParse(File.ReadAllText(F004ImportRules.ManifestPath), out var manifest, out _))
            {
                foreach (var entry in manifest.entries ?? Array.Empty<F004ManifestEntry>())
                {
                    foreach (var frame in entry?.frames ?? Array.Empty<F004ManifestFrame>())
                    {
                        listed.Add(F004ImportRules.ArtRoot + "/" + frame.file.Replace('\\', '/'));
                    }
                }
            }

            Assert.That(governed.Where(path => !listed.Contains(path)).ToList(), Is.Empty,
                "every PNG under Art/F004 (outside Spike) must be listed in import-manifest.json");
        }

        [Test]
        public void AtMostOneImportManifestExistsUnderTheF004ArtPath()
        {
            var manifests = Directory.GetFiles(F004ImportRules.ArtRoot, F004ImportRules.ManifestFileName, SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .Where(path => !IsFixturePath(path))
                .ToList();
            Assert.That(manifests, Has.Count.LessThanOrEqualTo(1));
            if (manifests.Count == 1)
            {
                Assert.That(manifests[0], Is.EqualTo(F004ImportRules.ManifestPath));
            }
        }

        // ---- sprite atlas v2 --------------------------------------------------------------------------------------

        [Test]
        public void EveryShippedSpriteAtlasMatchesTheF004Contract()
        {
            foreach (var path in F004SpriteAtlasSettings.FindAtlasPaths())
            {
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
                Assert.That(F004SpriteAtlasSettings.Validate(importer), Is.Empty, path);
            }
        }

        [Test]
        public void FamilyAtlasIsCreatedWithDefaultStandaloneAndAndroidOverrides()
        {
            using (var fixture = new Fixture())
            {
                var atlasPath = fixture.Folder + "/family" + F004SpriteAtlasSettings.AtlasExtension;
                var importer = F004SpriteAtlasSettings.CreateFamilyAtlas(atlasPath);

                Assert.That(F004SpriteAtlasSettings.Validate(importer), Is.Empty);
                Assert.That(importer.packingSettings.padding, Is.EqualTo(2));
                Assert.That(importer.packingSettings.enableRotation, Is.False);
                Assert.That(importer.textureSettings.generateMipMaps, Is.False);
                Assert.That(importer.textureSettings.filterMode, Is.EqualTo(FilterMode.Point));
                foreach (var platform in new[] { "DefaultTexturePlatform", "Standalone", "Android" })
                {
                    var settings = importer.GetPlatformSettings(platform);
                    Assert.That(settings.overridden, Is.True, platform);
                    Assert.That(settings.format, Is.EqualTo(TextureImporterFormat.RGBA32), platform);
                    Assert.That(settings.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), platform);
                    Assert.That(settings.maxTextureSize, Is.LessThanOrEqualTo(1024), platform);
                }

                Assert.That(F004SpriteAtlasSettings.FindAtlasPaths(), Does.Contain(atlasPath));
            }
        }

        [Test]
        public void AtlasValidationReportsEveryViolation()
        {
            using (var fixture = new Fixture())
            {
                var atlasPath = fixture.Folder + "/bad" + F004SpriteAtlasSettings.AtlasExtension;
                var importer = F004SpriteAtlasSettings.CreateFamilyAtlas(atlasPath);

                var packing = importer.packingSettings;
                packing.padding = 4;
                packing.enableRotation = true;
                importer.packingSettings = packing;
                var texture = importer.textureSettings;
                texture.generateMipMaps = true;
                texture.filterMode = FilterMode.Bilinear;
                importer.textureSettings = texture;
                importer.SetPlatformSettings(new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = 2048,
                    format = TextureImporterFormat.ASTC_6x6,
                    textureCompression = TextureImporterCompression.Compressed,
                });

                var problems = F004SpriteAtlasSettings.Validate(importer);
                Assert.That(problems, Has.Some.Contain("padding 4"));
                Assert.That(problems, Has.Some.Contain("rotation"));
                Assert.That(problems, Has.Some.Contain("mipmaps"));
                Assert.That(problems, Has.Some.Contain("filter Bilinear"));
                Assert.That(problems, Has.Some.Contain("Android: format"));
                Assert.That(problems, Has.Some.Contain("Android: compression"));
                Assert.That(problems, Has.Some.Contain("Android: max page 2048"));
            }
        }

        // ---- helpers ----------------------------------------------------------------------------------------------

        private static bool IsFixturePath(string path)
        {
            return path.Contains("/_ContractFixture_");
        }

        private static F004ManifestEntry Entry(string id, int ppu, int width, int height, float pivotPxX, float pivotPxY, string file)
        {
            return new F004ManifestEntry
            {
                assetId = id,
                ppu = ppu,
                canvas = new[] { width, height },
                pivot = new[] { pivotPxX / width, pivotPxY / height },
                frames = new[] { new F004ManifestFrame { file = file, sha256 = new string('a', 64) } },
            };
        }

        private static string Num(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string EntryJson(string id, int ppu, int width, int height, float pivotX, float pivotY, params FixturePng[] frames)
        {
            var builder = new StringBuilder();
            builder.Append("{\"assetId\":\"").Append(id).Append("\",\"route\":\"scripted\",\"ppu\":").Append(ppu)
                .Append(",\"fps\":0,\"canvas\":[").Append(width).Append(',').Append(height)
                .Append("],\"footprintPx\":[").Append(width).Append(',').Append(height)
                .Append("],\"pivot\":[").Append(Num(pivotX)).Append(',').Append(Num(pivotY))
                .Append("],\"palette\":{\"id\":\"p\",\"version\":\"1.0.0\"},\"frames\":[");
            builder.Append(string.Join(",", frames.Select(f => "{\"file\":\"" + f.FileName + "\",\"sha256\":\"" + f.Sha256 + "\"}")));
            builder.Append("]}");
            return builder.ToString();
        }

        private static string EmptyEntryJson(string id)
        {
            return "{\"assetId\":\"" + id + "\",\"intentionallyEmpty\":true}";
        }

        private static string ManifestJson(int schemaVersion, params string[] entries)
        {
            return "{\"schemaVersion\":" + schemaVersion + ",\"entries\":[" + string.Join(",", entries) + "]}";
        }

        private sealed class FixturePng
        {
            public string Path;
            public string FileName;
            public string Sha256;
        }

        /// <summary>A throw-away folder under the F004 art path with its own manifest; deleted on dispose.</summary>
        private sealed class Fixture : IDisposable
        {
            private readonly List<FixturePng> _pngs = new List<FixturePng>();

            public Fixture()
            {
                var name = "_ContractFixture_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                Folder = AssetDatabase.CreateFolder(F004ImportRules.ArtRoot, name) == null
                    ? null
                    : F004ImportRules.ArtRoot + "/" + name;
                Assert.That(Folder, Is.Not.Null);
            }

            public string Folder { get; }

            public FixturePng AddPng(string fileName, int width, int height)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color32[width * height];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = (i + fileName.Length) % 3 == 0 ? new Color32(200, 120, 40, 255) : new Color32(0, 0, 0, 0);
                }

                texture.SetPixels32(pixels);
                var bytes = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);
                var png = new FixturePng { Path = Folder + "/" + fileName, FileName = fileName, Sha256 = F004ImportRules.Sha256Hex(bytes) };
                File.WriteAllBytes(png.Path, bytes);
                _pngs.Add(png);
                return png;
            }

            public void WriteManifest(string json)
            {
                var path = Folder + "/" + F004ImportRules.ManifestFileName;
                File.WriteAllText(path, json);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            public void ImportPngs()
            {
                foreach (var png in _pngs)
                {
                    AssetDatabase.ImportAsset(png.Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }
            }

            public void Dispose()
            {
                AssetDatabase.DeleteAsset(Folder);
                AssetDatabase.Refresh();
                Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False, "fixture folder must be removed");
            }
        }
    }
}
