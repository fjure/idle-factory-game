using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IdleFactory.Editor.Art;
using IdleFactory.Unity.Presentation;
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
    /// T440 with the production art; they are the "T440" tests at the end of this class.
    /// </summary>
    public sealed class F004AssetContractTests
    {
        private const string F001ImportedRoot = "Assets/IdleFactory/Art/Development/F001/Imported";

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
            Assert.That(F004ImportRules.IsGovernedPath(F001ImportedRoot + "/f001-badge-inactive-v01.png"), Is.False);
            Assert.That(F004ImportRules.IsGovernedPath("Assets/IdleFactory/Art/F004/Machines/readme.json"), Is.False);
            Assert.That(F004ImportRules.IsGovernedPath(null), Is.False);
        }

        [Test]
        public void F001PlaceholdersKeepTheirOwnPixelsPerUnit()
        {
            var pngs = Directory.GetFiles(F001ImportedRoot, "*.png");
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
                    Assert.That(settings.maxTextureSize, Is.LessThanOrEqualTo(F004SpriteAtlasSettings.MaxPageSize), platform);
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

        // ---- T440: the imported production art ---------------------------------------------------------------------

        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string RunningBadgeKey = "F003-BADGE-RUNNING";

        // Art Bible 3.2 and 9.4: ink_0 is the glyph and outline tone of every badge; the pipeline silhouette limit is IOU_MAX = 0.70.
        private static readonly Color32 InkZero = new Color32(0x14, 0x12, 0x1c, 0xff);
        private const float GlyphIouLimit = 0.70f;

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
            public int ppu;
            public int fps;
            public bool intentionallyEmpty;
            public int[] canvas;
            public ShippedFrame[] frames;
        }

        [Serializable]
        private sealed class ShippedManifest
        {
            public int schemaVersion;
            public ShippedEntry[] entries;
        }

        private static ShippedManifest ReadShippedManifest()
        {
            Assert.That(File.Exists(F004ImportRules.ManifestPath), Is.True, "T440 ships the import manifest");
            return JsonUtility.FromJson<ShippedManifest>(File.ReadAllText(F004ImportRules.ManifestPath));
        }

        // T476: the Feature 003 placeholder sprites are deleted, so the 78 stable F003 keys are read from the shipped manifest (their F004 art).
        private static string[] F003Keys(ShippedManifest shipped)
        {
            return shipped.entries
                .Select(entry => entry.key)
                .Where(key => key.StartsWith("F003-", StringComparison.Ordinal))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
        }

        private static void WithSceneCatalog(Action<IdleFactory.Unity.Presentation.PresentationSpriteCatalog> check)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var bootstrap = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<IdleFactory.Unity.Bootstrap.CoreGameFoundationBootstrap>(true))
                    .Single();
                var catalog = bootstrap.SpriteCatalog;
                Assert.That(catalog.Validate(), Is.True, catalog.Error);
                check(catalog);
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheShippedManifestMergesEverySourceManifestOfTheImportedFamiliesAndNoOther()
        {
            var shipped = ReadShippedManifest();
            Assert.That(shipped.schemaVersion, Is.EqualTo(F004ImportRules.SupportedSchemaVersion));
            var byAsset = shipped.entries.ToDictionary(entry => entry.assetId, StringComparer.Ordinal);
            Assert.That(byAsset.Count, Is.EqualTo(shipped.entries.Length), "asset ids are unique");

            // T526 (P2-F5): the five badge sprites are the approved F006 K4 tail sprites; their hashes come from art-source/f006/cue.
            var k4Tails = F004ArtIntegration.ReadSourceEntries("art-source/f006/cue/manifest.json").ToDictionary(entry => entry.AssetId, entry => entry.FrameHashes, StringComparer.Ordinal);
            var expected = 0;
            foreach (var family in F004ArtIntegration.FamilyNames)
            {
                foreach (var manifestName in F004ArtIntegration.SourceManifestsOf(family))
                {
                    var source = F004ArtIntegration.ReadSourceEntries(F004ArtIntegration.SourceRoot + "/" + family + "/" + manifestName);
                    foreach (var entry in source)
                    {
                        expected++;
                        Assert.That(byAsset.TryGetValue(entry.AssetId, out var shippedEntry), Is.True, entry.AssetId);
                        Assert.That(shippedEntry.family, Is.EqualTo(family), entry.AssetId);
                        Assert.That(shippedEntry.key, Is.EqualTo(F004ArtIntegration.KeyOf(entry.AssetId, entry.Key)), entry.AssetId);
                        Assert.That(shippedEntry.intentionallyEmpty, Is.EqualTo(entry.IntentionallyEmpty), entry.AssetId);
                        Assert.That((shippedEntry.frames ?? new ShippedFrame[0]).Select(frame => frame.sha256),
                            Is.EqualTo(k4Tails.TryGetValue(entry.AssetId, out var tailHashes) ? tailHashes : entry.FrameHashes),
                            entry.AssetId + " keeps the hashes of its source manifest (the K4 tail sprites for the badges)");
                    }
                }
            }

            Assert.That(shipped.entries.Length, Is.EqualTo(expected), "the shipped manifest holds no entry of a family T440 does not import");
            Assert.That(shipped.entries.Select(entry => entry.family).Distinct().OrderBy(f => f, StringComparer.Ordinal),
                Is.EqualTo(F004ArtIntegration.FamilyNames.OrderBy(f => f, StringComparer.Ordinal)));
            Assert.That(shipped.entries.Count(entry => entry.assetId.Contains("floor")), Is.EqualTo(1),
                "T471: the 70 x 54 floor tile (the T456 floor period) is imported");
        }

        [Test]
        public void EveryF003KeyResolvesToF004FramesAndTheRunningBadgeThroughIntentionallyEmpty()
        {
            var shipped = ReadShippedManifest();
            var byKey = shipped.entries.ToDictionary(entry => entry.key, StringComparer.Ordinal);
            var f003Keys = F003Keys(shipped);
            Assert.That(f003Keys, Has.Length.EqualTo(78));

            WithSceneCatalog(catalog =>
            {
                Assert.That(catalog.Diagnostics, Is.Empty, "no key of the shipped scene is diagnosed");
                foreach (var key in f003Keys)
                {
                    Assert.That(byKey.TryGetValue(key, out var entry), Is.True, key + " has a manifest entry");
                    if (key == RunningBadgeKey)
                    {
                        Assert.That(entry.intentionallyEmpty, Is.True, "the running badge is an intentionallyEmpty manifest entry (owner decision R8)");
                        Assert.That(entry.frames == null || entry.frames.Length == 0, Is.True);
                        Assert.That(IdleFactory.Unity.Presentation.StateCueSet.Default
                            .RowFor(IdleFactory.Unity.Presentation.MachineVisualState.Running).IntentionallyEmpty, Is.True);
                        Assert.That(catalog.TryGetEntry(key, out _), Is.False,
                            "T476: the never-drawn running badge has no catalog entry; there is no placeholder sprite to hold");
                        continue;
                    }

                    Assert.That(catalog.TryGetEntry(key, out var registered), Is.True, key + " is registered in the scene catalog");

                    Assert.That(entry.intentionallyEmpty, Is.False, key);
                    Assert.That(registered.FrameCount, Is.EqualTo(entry.frames.Length), key + " frame count");
                    for (var i = 0; i < entry.frames.Length; i++)
                    {
                        Assert.That(AssetDatabase.GetAssetPath(registered.GetFrame(i)), Is.EqualTo(F004ImportRules.ArtRoot + "/" + entry.frames[i].file), key + " frame " + i);
                    }

                    Assert.That(registered.FramesPerSecond ?? 0f, Is.EqualTo((float)entry.fps), key + " frame rate");
                }
            });
        }

        [Test]
        public void TheF004OnlyKeysAreRegisteredAndTheSceneRequiresEveryRequiredRegistryRow()
        {
            var shipped = ReadShippedManifest();
            var f004Keys = shipped.entries.Select(entry => entry.key).Where(key => key.StartsWith("F004-", StringComparison.Ordinal)).ToArray();
            Assert.That(f004Keys.Length, Is.EqualTo(10), "four accent layers, four lock labels, the settings icon and the floor tile");

            WithSceneCatalog(catalog =>
            {
                Assert.That(catalog.RequireRegistryRows, Is.True, "the shipped scene fails closed without the accent layers and lock labels");
                foreach (var key in f004Keys)
                {
                    Assert.That(catalog.TryGetEntry(key, out var entry), Is.True, key);
                    Assert.That(AssetDatabase.GetAssetPath(entry.Sprite), Does.StartWith(F004ImportRules.ArtRoot + "/"), key);
                }

                // Canvas layers are explicit: the catalog resolves them from the requirement row, not from a sprite-size guess.
                Assert.That(catalog.TryGetEntry("F003-MACHINE-MINER", out var miner), Is.True);
                Assert.That(miner.ResolvedPlacement, Is.EqualTo(IdleFactory.Unity.Presentation.SpritePlacement.Canvas));
                Assert.That(catalog.TryGetEntry("F004-ACCENT-PRESS", out var accent), Is.True);
                Assert.That(accent.ResolvedPlacement, Is.EqualTo(IdleFactory.Unity.Presentation.SpritePlacement.Canvas));
                Assert.That(catalog.TryGetEntry("F003-BADGE-WAITING", out var badge), Is.True);
                Assert.That(badge.ResolvedPlacement, Is.EqualTo(IdleFactory.Unity.Presentation.SpritePlacement.Canvas));
                Assert.That(catalog.TryGetEntry(RunningBadgeKey, out _), Is.False, "the never-drawn running badge has no catalog entry (T476)");
            });
        }

        // T476 (owner decision 2026-11-05, option D): there is no placeholder to fall back to and the views draw nothing for a key without an
        // entry, so a shipped key that is missing must stop start-up instead of showing a blank slot. The shipped scene catalog requires the
        // registry rows; this pins which keys that covers: every non-empty shipped key except the optional settings gear.
        [Test]
        public void EveryShippedKeyExceptTheSettingsGearIsRequiredSoMissingArtFailsTheCatalogClosedAndNothingDrawsBlank()
        {
            var shipped = ReadShippedManifest();
            var keys = shipped.entries.Where(entry => !entry.intentionallyEmpty).Select(entry => entry.key).ToArray();
            Assert.That(keys, Has.Length.EqualTo(87), "77 Feature 003 keys with art and the 10 F004-only keys");

            WithSceneCatalog(scene =>
            {
                var entries = new List<PresentationSpriteEntry>();
                foreach (var key in keys)
                {
                    Assert.That(scene.TryGetEntry(key, out var entry), Is.True, key);
                    entries.Add(entry);
                }

                var complete = new PresentationSpriteCatalog { RequireRegistryRows = true };
                complete.Configure(entries);
                Assert.That(complete.IsValid, Is.True, complete.Error);
                Assert.That(complete.Diagnostics, Is.Empty);

                foreach (var key in keys)
                {
                    var withoutKey = new PresentationSpriteCatalog { RequireRegistryRows = true };
                    var optional = key == PresentationSpriteCatalog.SettingsIconKey;
                    if (!optional)
                    {
                        LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + key);
                    }

                    withoutKey.Configure(entries.Where(entry => entry.Key != key));
                    if (optional)
                    {
                        Assert.That(withoutKey.IsValid, Is.True, key + " is the one optional row (the gear has no Feature 003 key)");
                        continue;
                    }

                    Assert.That(withoutKey.IsValid, Is.False, key + " is required in the shipped scene");
                    Assert.That(withoutKey.Error, Does.StartWith("presentation.key.unresolved").And.Contain("Missing required presentation mapping '" + key + "'"));
                    Assert.That(withoutKey.Diagnostics, Is.EqualTo(new[] { "presentation.key.unresolved: " + key }));
                }

                // The running badge is intentionally empty (owner decision R8): its row is optional and the scene has no entry for it.
                Assert.That(scene.TryGetEntry(PresentationSpriteCatalog.RunningBadgeKey, out _), Is.False);
            });
        }

        [Test]
        public void MachineBodiesAndAccentsAreSixFrameLoopsAtEightFramesPerSecond()
        {
            var shipped = ReadShippedManifest();
            var machines = shipped.entries.Where(entry => entry.family == "machines").ToArray();
            Assert.That(machines, Has.Length.EqualTo(8));
            foreach (var entry in machines)
            {
                Assert.That(entry.frames, Has.Length.EqualTo(6), entry.assetId);
                Assert.That(entry.fps, Is.EqualTo(8), entry.assetId);
                Assert.That(entry.canvas, Is.EqualTo(new[] { 80, 64 }), entry.assetId);
                Assert.That(entry.ppu, Is.EqualTo(24), entry.assetId);
            }
        }

        [Test]
        public void EveryWorldFamilyHasASpriteAtlasV2ThatPacksItsFolder()
        {
            foreach (var family in F004ArtIntegration.AtlasFamilies)
            {
                var path = F004SpriteAtlasSettings.AtlasFolder + "/" + family + F004SpriteAtlasSettings.AtlasExtension;
                var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(F004SpriteAtlasSettings.Validate(importer), Is.Empty, path);
                var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(path);
                Assert.That(atlas, Is.Not.Null, path);
                var packables = SpriteAtlasExtensions.GetPackables(atlas);
                Assert.That(packables.Select(AssetDatabase.GetAssetPath), Does.Contain(F004ImportRules.ArtRoot + "/" + family), path);
                var frames = ReadShippedManifest().entries.Where(entry => entry.family == family).Sum(entry => entry.frames == null ? 0 : entry.frames.Length);
                Assert.That(atlas.spriteCount, Is.EqualTo(frames), path + " packs every frame of its family");
            }
        }

        [Test]
        public void TheFiveBadgeGlyphsAreShapeDistinctWithoutColour()
        {
            var shipped = ReadShippedManifest();
            var glyphs = new Dictionary<string, bool[]>(StringComparer.Ordinal);
            foreach (var entry in shipped.entries.Where(e => e.family == "cues" && !e.intentionallyEmpty))
            {
                glyphs[entry.key] = InteriorInkMask(F004ImportRules.ArtRoot + "/" + entry.frames[0].file);
                Assert.That(glyphs[entry.key].Count(on => on), Is.GreaterThan(20), entry.key + " has a glyph");
            }

            Assert.That(glyphs, Has.Count.EqualTo(5), "waiting, missing input, storage full, paused, inactive");
            var keys = glyphs.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
            for (var a = 0; a < keys.Length; a++)
            {
                for (var b = a + 1; b < keys.Length; b++)
                {
                    var iou = MaskIou(glyphs[keys[a]], glyphs[keys[b]]);
                    Assert.That(iou, Is.LessThanOrEqualTo(GlyphIouLimit), keys[a] + " vs " + keys[b] + " glyph silhouette IoU " + iou.ToString("0.000", CultureInfo.InvariantCulture));
                }
            }
        }

        // The ink_0 pixels with four opaque neighbours: the glyph without the outline ring, independent of the fill colour.
        private static bool[] InteriorInkMask(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false), Is.True, path);
                var width = texture.width;
                var height = texture.height;
                var pixels = texture.GetPixels32();
                bool Opaque(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && pixels[y * width + x].a > 0;
                var mask = new bool[width * height];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var pixel = pixels[y * width + x];
                        mask[y * width + x] = pixel.a > 0 && pixel.r == InkZero.r && pixel.g == InkZero.g && pixel.b == InkZero.b
                            && Opaque(x + 1, y) && Opaque(x - 1, y) && Opaque(x, y + 1) && Opaque(x, y - 1);
                    }
                }

                return mask;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static float MaskIou(bool[] a, bool[] b)
        {
            var intersection = 0;
            var union = 0;
            for (var i = 0; i < a.Length; i++)
            {
                intersection += a[i] && b[i] ? 1 : 0;
                union += a[i] || b[i] ? 1 : 0;
            }

            return union == 0 ? 0f : intersection / (float)union;
        }

        // ---- T440: the catalog contract (rows with a primary and a legacy shape, explicit placement, required rows) ----------

        private readonly List<UnityEngine.Object> _madeSprites = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyMadeSprites()
        {
            foreach (var made in _madeSprites)
            {
                if (made != null)
                {
                    UnityEngine.Object.DestroyImmediate(made);
                }
            }

            _madeSprites.Clear();
        }

        private Sprite MadeSprite(int width, int height, float pivotX, float pivotY, float pixelsPerUnit)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(pivotX, pivotY), pixelsPerUnit);
            _madeSprites.Add(texture);
            _madeSprites.Add(sprite);
            return sprite;
        }

        private Sprite CanvasSprite() => MadeSprite(80, 64, 40f / 80f, 15f / 64f, 24f);

        private Sprite TightSprite() => MadeSprite(128, 128, 0f, 0f, 64f);

        private static PresentationSpriteCatalog Catalog(bool requireRows, params PresentationSpriteEntry[] entries)
        {
            var catalog = new PresentationSpriteCatalog { RequireRegistryRows = requireRows };
            catalog.Configure(entries);
            return catalog;
        }

        [Test]
        public void AF003KeyAcceptsTheF004CanvasShapeAsACanvasLayerAndTheF003ShapeAsATightSprite()
        {
            var canvasEntry = new PresentationSpriteEntry("F003-SLOT-OCCUPIED-BASE", CanvasSprite());
            var tightEntry = new PresentationSpriteEntry("F003-SLOT-EMPTY", TightSprite());
            var catalog = Catalog(false, canvasEntry, tightEntry);

            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(canvasEntry.ResolvedPlacement, Is.EqualTo(SpritePlacement.Canvas));
            Assert.That(canvasEntry.IsCanvasLayer(canvasEntry.Sprite), Is.True);
            Assert.That(tightEntry.ResolvedPlacement, Is.EqualTo(SpritePlacement.Tight));
            Assert.That(tightEntry.IsCanvasLayer(tightEntry.Sprite), Is.False);
        }

        [Test]
        public void AFrameSetMustMatchOneShapeAsAWholeAndKeepsTheSizeOrPivotReason()
        {
            var mixed = new PresentationSpriteEntry("F003-MACHINE-MINER", new[] { CanvasSprite(), TightSprite() }, 8f);
            var mixedCatalog = new PresentationSpriteCatalog();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: F003-MACHINE-MINER");
            mixedCatalog.Configure(new[] { mixed });
            Assert.That(mixedCatalog.IsValid, Is.False, "one canvas frame and one F003 frame are not one shape");

            var offset = new PresentationSpriteCatalog();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: F003-SLOT-EMPTY");
            offset.Configure(new[] { new PresentationSpriteEntry("F003-SLOT-EMPTY", MadeSprite(80, 64, 0.5f, 0.5f, 24f)) });
            Assert.That(offset.Error, Does.Contain("wrong pivot"), "the canvas size with a wrong pivot");

            var wrongSize = new PresentationSpriteCatalog();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: F003-SLOT-EMPTY");
            wrongSize.Configure(new[] { new PresentationSpriteEntry("F003-SLOT-EMPTY", MadeSprite(70, 35, 0.5f, 0.5f, 24f)) });
            Assert.That(wrongSize.Error, Does.Contain("wrong world size"));
        }

        [Test]
        public void ExplicitPlacementOverridesTheRowAndAFailingFrameSetFailsClosedWithoutAFallback()
        {
            var forced = new PresentationSpriteEntry("F003-SLOT-OCCUPIED-BASE", CanvasSprite()).WithPlacement(SpritePlacement.Tight);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "presentation.placement.mismatch: F003-SLOT-OCCUPIED-BASE");
            var catalog = Catalog(false, forced);

            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(forced.IsCanvasLayer(forced.Sprite), Is.False, "an explicit flag wins over the row and the sprite size");

            // T476: a frame set that fails its row has no placeholder to run on; the catalog fails closed.
            var failing = new PresentationSpriteEntry("F003-SLOT-EMPTY", MadeSprite(10, 10, 0.5f, 0.5f, 24f));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: F003-SLOT-EMPTY");
            var failed = Catalog(false, failing);
            Assert.That(failed.IsValid, Is.False);
            Assert.That(failed.TryGetEntry("F003-SLOT-EMPTY", out _), Is.False);
        }

        [Test]
        public void ARequiredRegistryRowFailsOnlyACatalogThatRequiresTheRegistryRows()
        {
            var entry = new PresentationSpriteEntry("F003-SLOT-EMPTY", TightSprite());
            Assert.That(Catalog(false, entry).IsValid, Is.True, "development and unit-test catalogs stay legal");

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new Regex(@"^presentation\.key\.unresolved: F00[34]-"));
            var strict = Catalog(true, entry);
            Assert.That(strict.IsValid, Is.False);
            Assert.That(strict.Error, Does.StartWith("presentation.key.unresolved").And.Contain("Missing required presentation mapping 'F00"));
        }

        [Test]
        public void TheStyleSpikeImportPathIsRemoved()
        {
            Assert.That(Directory.Exists(F004ImportRules.SpikeRoot), Is.False,
                "owner decision T426 (2026-10-30): the spike import path is deleted after the last family; art-source/f004/spike/ stays");
            Assert.That(Directory.Exists("art-source/f004/spike"), Is.True);
        }

        // ---- T471: the floor family and the importer hardening -------------------------------------------------------

        private const string FloorAssetId = "f004-floor-tile";
        private const string FloorKey = "F004-FLOOR-TILE";
        private const string FloorPath = F004ImportRules.ArtRoot + "/floor/f004-floor-tile_00.png";

        [Test]
        public void TheFloorTileIsImportedAtPpu24WithItsPivotOnTheTileCentreAndPointFilter()
        {
            var shipped = ReadShippedManifest();
            var entry = shipped.entries.Single(e => e.assetId == FloorAssetId);
            Assert.That(entry.family, Is.EqualTo("floor"));
            Assert.That(entry.key, Is.EqualTo(FloorKey));
            Assert.That(entry.ppu, Is.EqualTo(24), "TA-2: PPU = artPixelsPerCell = 24, not 64");
            Assert.That(entry.canvas, Is.EqualTo(new[] { 70, 54 }), "the T456 lattice pitch is the floor period");
            Assert.That(entry.frames, Has.Length.EqualTo(1));
            Assert.That(entry.intentionallyEmpty, Is.False);

            var importer = AssetImporter.GetAtPath(FloorPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, FloorPath);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(24f));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False, "no mipmaps");
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FloorPath);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(70f, 54f)));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(35f, 27f)), "TA-3: pivot (35, 27) is a whole art pixel on the slot footprint centre");
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(sprite.bounds.size.x, Is.EqualTo(70f / 24f).Within(1e-4f));
            Assert.That(sprite.bounds.size.y, Is.EqualTo(54f / 24f).Within(1e-4f));
        }

        [Test]
        public void TheFloorKeyIsRegisteredInTheSceneCatalogAtTheLatticePeriodSizeAndRequired()
        {
            WithSceneCatalog(catalog =>
            {
                Assert.That(catalog.Diagnostics, Is.Empty);
                Assert.That(catalog.TryGetEntry(FloorKey, out var floor), Is.True);
                Assert.That(floor.FrameCount, Is.EqualTo(1));
                Assert.That(floor.FramesPerSecond, Is.Null);
                Assert.That(AssetDatabase.GetAssetPath(floor.Sprite), Is.EqualTo(FloorPath));
                Assert.That(floor.Sprite.rect.width / floor.Sprite.pixelsPerUnit, Is.EqualTo(SlotGridLayout.PitchX).Within(1e-4f),
                    "world width is the lattice column pitch");
                Assert.That(floor.Sprite.rect.height / floor.Sprite.pixelsPerUnit, Is.EqualTo(SlotGridLayout.PitchY).Within(1e-4f),
                    "world height is the lattice row pitch");
            });

            var strict = new PresentationSpriteCatalog { RequireRegistryRows = true };
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new Regex(@"^presentation\.key\.unresolved: F00[34]-"));
            strict.Configure(new[] { new PresentationSpriteEntry("F003-SLOT-EMPTY", TightSprite()) });
            Assert.That(strict.IsValid, Is.False, "a catalog that requires the registry rows fails closed without the other rows, the floor among them");
        }

        [Test]
        public void EveryImportedFamilyFolderHoldsExactlyTheFramesOfTheShippedManifest()
        {
            var shipped = ReadShippedManifest();
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in shipped.entries)
            {
                foreach (var frame in entry.frames ?? new ShippedFrame[0])
                {
                    expected.Add(F004ImportRules.ArtRoot + "/" + frame.file);
                }
            }

            foreach (var family in F004ArtIntegration.FamilyNames)
            {
                Assert.That(F004ArtIntegration.FindOrphanPngs(F004ImportRules.ArtRoot + "/" + family, expected), Is.Empty, family + " has no orphan PNG");
            }
        }

        [Test]
        public void TheImportFailsOnADuplicateDestinationPathCatalogKeyOrAssetId()
        {
            F004ArtIntegration.EnsureUnique(new[] { "a/x.png", "a/y.png" }, StringComparer.OrdinalIgnoreCase, "destination path");
            Assert.That(() => F004ArtIntegration.EnsureUnique(new[] { "a/x.png", "a/X.PNG" }, StringComparer.OrdinalIgnoreCase, "destination path"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contain("Duplicate destination path"));
            Assert.That(() => F004ArtIntegration.EnsureUnique(new[] { "F003-SLOT-EMPTY", "F003-SLOT-EMPTY" }, StringComparer.Ordinal, "catalog key"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contain("Duplicate catalog key 'F003-SLOT-EMPTY'"));
            Assert.That(() => F004ArtIntegration.EnsureUnique(new[] { "f004-floor-tile", "f004-floor-tile" }, StringComparer.Ordinal, "asset id"),
                Throws.TypeOf<InvalidDataException>().With.Message.Contain("Duplicate asset id"));
        }

        [Test]
        public void OrphanPngsInAFamilyFolderAreFoundAndListedPngsAreNot()
        {
            var folder = Path.Combine(Path.GetTempPath(), "f004-orphans-" + Guid.NewGuid().ToString("N").Substring(0, 8)).Replace('\\', '/');
            try
            {
                Directory.CreateDirectory(folder + "/sub");
                File.WriteAllBytes(folder + "/listed.png", new byte[] { 1 });
                File.WriteAllBytes(folder + "/stale_00.png", new byte[] { 2 });
                File.WriteAllBytes(folder + "/sub/nested.png", new byte[] { 3 });
                File.WriteAllText(folder + "/readme.txt", "not a png");
                var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { folder + "/LISTED.png" };
                Assert.That(F004ArtIntegration.FindOrphanPngs(folder, expected),
                    Is.EqualTo(new[] { folder + "/stale_00.png", folder + "/sub/nested.png" }));
                Assert.That(F004ArtIntegration.FindOrphanPngs(folder + "/missing", expected), Is.Empty);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TheDestinationHashIsVerifiedAfterTheCopyAndABadCopyIsRemoved()
        {
            var folder = Path.Combine(Path.GetTempPath(), "f004-copy-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Directory.CreateDirectory(folder);
                var source = Path.Combine(folder, "source.png");
                var good = new byte[] { 1, 2, 3, 4 };
                File.WriteAllBytes(source, good);
                var hash = F004ImportRules.Sha256Hex(good);

                var destination = Path.Combine(folder, "good.png");
                F004ArtIntegration.CopyVerified(source, destination, hash);
                Assert.That(File.ReadAllBytes(destination), Is.EqualTo(good));

                var corrupt = Path.Combine(folder, "corrupt.png");
                Assert.That(() => F004ArtIntegration.CopyVerified(source, corrupt, hash, (from, to) => File.WriteAllBytes(to, new byte[] { 9, 9 })),
                    Throws.TypeOf<InvalidDataException>().With.Message.Contain("after the copy"));
                Assert.That(File.Exists(corrupt), Is.False, "a destination that fails the hash is removed");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void AFailedCopyLeavesThePreviousManifestInPlaceAndTheManifestIsWrittenOnlyAfterEveryCopyPassed()
        {
            var folder = Path.Combine(Path.GetTempPath(), "f004-commit-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Directory.CreateDirectory(folder);
                var good = new byte[] { 1, 2, 3 };
                var hash = F004ImportRules.Sha256Hex(good);
                var a = Path.Combine(folder, "a.png");
                var b = Path.Combine(folder, "b.png");
                File.WriteAllBytes(a, good);
                File.WriteAllBytes(b, good);
                var manifest = Path.Combine(folder, "import-manifest.json");
                File.WriteAllText(manifest, "previous");
                var plan = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>(a, Path.Combine(folder, "out-a.png")),
                    new KeyValuePair<string, string>(b, Path.Combine(folder, "out-b.png")),
                };

                Assert.That(() => F004ArtIntegration.CommitImport(plan, destination => hash, manifest, "new",
                        (from, to) => File.WriteAllBytes(to, to.EndsWith("out-b.png", StringComparison.Ordinal) ? new byte[] { 9 } : good)),
                    Throws.TypeOf<InvalidDataException>());
                Assert.That(File.ReadAllText(manifest), Is.EqualTo("previous"), "a mid-copy failure keeps the previous manifest");
                Assert.That(File.Exists(Path.Combine(folder, "out-b.png")), Is.False, "the corrupt copy is removed");

                F004ArtIntegration.CommitImport(plan, destination => hash, manifest, "new");
                Assert.That(File.ReadAllText(manifest), Is.EqualTo("new"), "the manifest is written after every copy passed");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TheCatalogStepNeverReplacesTheSceneInPlayModeAndAsksBeforeDiscardingUnsavedScenes()
        {
            var asked = 0;
            Assert.That(() => F004ArtIntegration.GuardSceneSwitch(true, true, () => { asked++; return true; }),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contain("Play Mode"));
            Assert.That(() => F004ArtIntegration.GuardSceneSwitch(false, false, () => { asked++; return false; }),
                Throws.TypeOf<OperationCanceledException>());
            Assert.That(asked, Is.EqualTo(1), "an interactive run asks to save; Play Mode is refused before asking");
            F004ArtIntegration.GuardSceneSwitch(false, false, () => { asked++; return true; });
            F004ArtIntegration.GuardSceneSwitch(true, false, () => { asked++; return false; });
            Assert.That(asked, Is.EqualTo(2), "a batch run has no unsaved user work and does not ask");
        }

        [Test]
        public void AnExplicitPlacementThatContradictsTheMatchedShapeIsReportedAndStillWins()
        {
            var contradicting = new PresentationSpriteEntry("F003-SLOT-OCCUPIED-BASE", CanvasSprite()).WithPlacement(SpritePlacement.Tight);
            var agreeing = new PresentationSpriteEntry("F003-SLOT-EMPTY", CanvasSprite()).WithPlacement(SpritePlacement.Canvas);
            var inferred = new PresentationSpriteEntry("F003-SLOT-LOCKED-L2", CanvasSprite());
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "presentation.placement.mismatch: F003-SLOT-OCCUPIED-BASE");
            var catalog = Catalog(false, contradicting, agreeing, inferred);

            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Diagnostics, Is.EqualTo(new[] { "presentation.placement.mismatch: F003-SLOT-OCCUPIED-BASE" }));
            Assert.That(contradicting.ResolvedPlacement, Is.EqualTo(SpritePlacement.Tight), "the explicit flag still wins");
        }

        // ---- T471: C8 on the shipped art with the real floor ---------------------------------------------------------

        // Art Bible 9.4 rule C1 (A_ov, verbatim from tools/art-pipeline/art_pipeline/checks_v1.py) and C3 (A_fr = every tone except these three).
        private static readonly string[] AllowedOverlayBackdrop =
        {
            "ink_0", "ink_1", "steel_1", "steel_2", "steel_3", "copper_1", "copper_2", "teal_1", "dim_0", "lit_0", "lit_1",
            "floor_0", "floor_1", "plate_1", "plate_2", "res_ore_1", "res_coal", "res_crystal", "res_leaf", "res_gold",
            "res_water", "res_plank", "ui_bg", "ui_panel", "ui_line", "ui_text",
        };

        private static readonly string[] FrameForbiddenBackdrop = { "warn_0", "plate_0", "res_ore_0" };

        [Serializable]
        private sealed class PaletteColorJson
        {
            public string id;
            public string hex;
        }

        [Serializable]
        private sealed class PaletteGroupJson
        {
            public PaletteColorJson[] colors;
        }

        [Serializable]
        private sealed class PaletteJson
        {
            public PaletteGroupJson[] groups;
        }

        private sealed class ArtLayer
        {
            public ArtLayer(string path)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false), Is.True, path);
                    Width = texture.width;
                    Height = texture.height;
                    Pixels = texture.GetPixels32();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            public int Width { get; }
            public int Height { get; }
            public Color32[] Pixels { get; }
        }

        private static ArtLayer LoadShipped(ShippedManifest shipped, string key, int frame = 0)
        {
            var entry = shipped.entries.Single(e => e.key == key);
            return new ArtLayer(F004ImportRules.ArtRoot + "/" + entry.frames[frame].file);
        }

        [Test]
        public void C8TheBackdropRingOfEveryBadgeMarkerAndFrameLiesInTheAllowedTonesOnTheRealFloor()
        {
            var shipped = ReadShippedManifest();
            var palette = JsonUtility.FromJson<PaletteJson>(File.ReadAllText("tools/art-pipeline/palette/palette-v1.json"));
            var toneOf = new Dictionary<int, string>();
            foreach (var group in palette.groups)
            {
                foreach (var color in group.colors)
                {
                    var rgb = (Convert.ToInt32(color.hex.Substring(1, 2), 16) << 16) | (Convert.ToInt32(color.hex.Substring(3, 2), 16) << 8)
                        | Convert.ToInt32(color.hex.Substring(5, 2), 16);
                    toneOf[rgb] = color.id;
                }
            }

            var allowedOverlay = new HashSet<string>(AllowedOverlayBackdrop, StringComparer.Ordinal);
            var allowedFrame = new HashSet<string>(toneOf.Values.Where(t => !FrameForbiddenBackdrop.Contains(t)), StringComparer.Ordinal);

            var floor = LoadShipped(shipped, FloorKey);
            Assert.That(new[] { floor.Width, floor.Height }, Is.EqualTo(new[] { 70, 54 }));
            // The shipped tile is fully opaque and uses floor_0 plus the 32 px of the two floor_1 detail clusters. It carries no floor_2 pixel (the Art Bible
            // allows floor_2 only as 1 px highlights), so the floor adds no tone outside A_ov; the ring count over floor_2 below pins that.
            var floorTones = floor.Pixels.Select(p => p.a == 255 && toneOf.TryGetValue((p.r << 16) | (p.g << 8) | p.b, out var t) ? t : "other").Distinct().OrderBy(t => t, StringComparer.Ordinal);
            Assert.That(floorTones, Is.EqualTo(new[] { "floor_0", "floor_1" }), "every floor pixel is opaque and a floor_0 or floor_1 palette tone");

            var plate = LoadShipped(shipped, "F003-SLOT-OCCUPIED-BASE");
            var frame = LoadShipped(shipped, "F003-SLOT-SELECTED-FRAME");
            var markers = new[] { LoadShipped(shipped, "F003-MARKER-BOOSTED"), LoadShipped(shipped, "F003-MARKER-OVERDRIVE") };
            var badges = new[] { "F003-BADGE-WAITING", "F003-BADGE-MISSING-INPUT", "F003-BADGE-STORAGE-FULL", "F003-BADGE-PAUSED", "F003-BADGE-INACTIVE" }
                .Select(key => LoadShipped(shipped, key)).ToArray();
            var machineKeys = new[] { "F003-MACHINE-MINER", "F003-MACHINE-SMELTER", "F003-MACHINE-PRESS", "F003-MACHINE-ASSEMBLER" };
            var accentKeys = new[] { "F004-ACCENT-MINER", "F004-ACCENT-SMELTER", "F004-ACCENT-PRESS", "F004-ACCENT-ASSEMBLER" };

            const int Margin = 48;
            var sampled = 0;
            var overFloor2 = 0;
            var compositions = 0;
            var failures = new List<string>();
            foreach (var columns in new[] { 3, 4 })
            {
                var extent = SlotGridLayout.Extent(columns);
                var worldWidth = Mathf.RoundToInt(extent.width * SlotGridLayout.Density) + 2 * Margin;
                var worldHeight = Mathf.RoundToInt(extent.height * SlotGridLayout.Density) + 2 * Margin;
                for (var machineFrame = 0; machineFrame < 6; machineFrame++)
                {
                    // World layers without any overlay: the periodic floor (tile origin at the pivot minus (35, 27)), then per slot the plate,
                    // the machine body of frame k and, on the running slots 0 and 6, its accent.
                    var world = new Color32[worldWidth * worldHeight];
                    for (var y = 0; y < worldHeight; y++)
                    {
                        for (var x = 0; x < worldWidth; x++)
                        {
                            var tx = (((x - Margin) % 70) + 70) % 70;
                            var ty = (((y - Margin + 12) % 54) + 54) % 54;
                            world[y * worldWidth + x] = floor.Pixels[ty * floor.Width + tx];
                        }
                    }

                    var pivots = new Vector2Int[SlotGridLayout.SlotCount];
                    for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
                    {
                        var pivot = SlotGridLayout.PivotOf(slot, columns) * SlotGridLayout.Density;
                        pivots[slot] = new Vector2Int(Mathf.RoundToInt(pivot.x) + Margin, Mathf.RoundToInt(pivot.y) + Margin);
                        Over(world, worldWidth, worldHeight, plate, pivots[slot]);
                        var machine = (slot + machineFrame) % 4;
                        Over(world, worldWidth, worldHeight, LoadShipped(shipped, machineKeys[machine], machineFrame), pivots[slot]);
                        if (slot == 0 || slot == 6)
                        {
                            Over(world, worldWidth, worldHeight, LoadShipped(shipped, accentKeys[machine], machineFrame), pivots[slot]);
                        }
                    }

                    for (var selected = 0; selected < SlotGridLayout.SlotCount; selected++)
                    {
                        compositions++;
                        var overlays = new List<KeyValuePair<ArtLayer, Vector2Int>>();
                        var isFrame = new List<bool>();
                        for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
                        {
                            if (slot != 0 && slot != 6)
                            {
                                overlays.Add(new KeyValuePair<ArtLayer, Vector2Int>(badges[(slot + machineFrame) % 5], pivots[slot]));
                                isFrame.Add(false);
                            }

                            foreach (var marker in markers)
                            {
                                overlays.Add(new KeyValuePair<ArtLayer, Vector2Int>(marker, pivots[slot]));
                                isFrame.Add(false);
                            }
                        }

                        overlays.Add(new KeyValuePair<ArtLayer, Vector2Int>(frame, pivots[selected]));
                        isFrame.Add(true);

                        var covered = new int[worldWidth * worldHeight];
                        foreach (var overlay in overlays)
                        {
                            ForEachOpaque(overlay.Key, overlay.Value, (wx, wy) => covered[wy * worldWidth + wx]++);
                        }

                        for (var i = 0; i < overlays.Count; i++)
                        {
                            var layer = overlays[i].Key;
                            var origin = overlays[i].Value - new Vector2Int(40, 15);
                            var allowed = isFrame[i] ? allowedFrame : allowedOverlay;
                            for (var cy = 0; cy < layer.Height; cy++)
                            {
                                for (var cx = 0; cx < layer.Width; cx++)
                                {
                                    if (layer.Pixels[cy * layer.Width + cx].a != 0 || !HasOpaqueNeighbour(layer, cx, cy))
                                    {
                                        continue;
                                    }

                                    var wx = origin.x + cx;
                                    var wy = origin.y + cy;
                                    Assert.That(wx >= 0 && wy >= 0 && wx < worldWidth && wy < worldHeight, Is.True, "the world margin covers every ring pixel");
                                    if (covered[wy * worldWidth + wx] > 0)
                                    {
                                        continue; // covered by another overlay
                                    }

                                    sampled++;
                                    var pixel = world[wy * worldWidth + wx];
                                    var tone = pixel.a == 0
                                        ? "bare"
                                        : (toneOf.TryGetValue((pixel.r << 16) | (pixel.g << 8) | pixel.b, out var id) ? id : "unknown");
                                    overFloor2 += tone == "floor_2" ? 1 : 0;
                                    var ok = tone == "bare" ? allowed.Contains("floor_0") && allowed.Contains("floor_1") : allowed.Contains(tone);
                                    if (!ok)
                                    {
                                        failures.Add("lattice " + columns + " frame " + machineFrame + " selected " + selected + " overlay " + i
                                            + " at " + wx + "," + wy + " tone " + tone);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Debug.Log("[C8] compositions " + compositions + ", ring pixels sampled " + sampled + ", ring pixels over floor_2 " + overFloor2
                + ", failures " + failures.Count);
            Assert.That(compositions, Is.EqualTo(144));
            Assert.That(sampled, Is.GreaterThan(0));
            Assert.That(overFloor2, Is.EqualTo(0), "no ring pixel lies on floor_2");
            Assert.That(failures.Take(10), Is.Empty, failures.Count + " ring pixel(s) lie on a forbidden tone");
        }

        private static bool HasOpaqueNeighbour(ArtLayer layer, int x, int y)
        {
            bool Opaque(int ox, int oy) => ox >= 0 && oy >= 0 && ox < layer.Width && oy < layer.Height && layer.Pixels[oy * layer.Width + ox].a != 0;
            return Opaque(x + 1, y) || Opaque(x - 1, y) || Opaque(x, y + 1) || Opaque(x, y - 1);
        }

        // Layers are 80 x 64 canvases with the footprint pivot at (40, 15) measured from the bottom left; the pivot lands on the slot pivot.
        private static void ForEachOpaque(ArtLayer layer, Vector2Int pivot, Action<int, int> visit)
        {
            var origin = pivot - new Vector2Int(40, 15);
            for (var y = 0; y < layer.Height; y++)
            {
                for (var x = 0; x < layer.Width; x++)
                {
                    if (layer.Pixels[y * layer.Width + x].a != 0)
                    {
                        visit(origin.x + x, origin.y + y);
                    }
                }
            }
        }

        private static void Over(Color32[] world, int worldWidth, int worldHeight, ArtLayer layer, Vector2Int pivot)
        {
            var origin = pivot - new Vector2Int(40, 15);
            ForEachOpaque(layer, pivot, (wx, wy) =>
            {
                if (wx >= 0 && wy >= 0 && wx < worldWidth && wy < worldHeight)
                {
                    world[wy * worldWidth + wx] = layer.Pixels[(wy - origin.y) * layer.Width + (wx - origin.x)];
                }
            });
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
