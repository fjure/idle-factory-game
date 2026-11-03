using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using IdleFactory.Unity.Presentation;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T409 (FR-004, FR-006, FR-025; research R12, R13): pre-switch (Built-in pipeline) screenshot baselines. Each screen-class
    /// test captures the four frames of <see cref="RenderCaptureHarness"/> and compares them with the committed PNGs under
    /// <c>Fixtures/Screenshots/F004-B1/</c> within the phase-0 tolerance. Set <c>IDLEFACTORY_RENDER_BASELINE_WRITE=1</c> to
    /// write the PNGs instead of comparing (only for a deliberate re-baseline on the pre-switch commit).
    /// </summary>
    public sealed class RenderParityTests
    {
        public static IEnumerable<ShellScreen> Screens() => RenderCaptureHarness.Screens();

        private bool _allowAsyncCompilation;

        // T411: on a cold shader cache the first URP frame was captured with draws skipped while shaders compiled
        // asynchronously, which failed one parity frame once. Compile synchronously for the capture.
        [SetUp]
        public void DisableAsyncShaderCompilation()
        {
            _allowAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
        }

        [TearDown]
        public void RestoreAsyncShaderCompilation()
        {
            ShaderUtil.allowAsyncCompilation = _allowAsyncCompilation;
        }

        [UnityTest]
        public IEnumerator TheCapturedFramesMatchTheCommittedPostSwitchBaselines([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            Debug.Log("RENDER-FIXTURE " + W004VFixture.Name + " hash=" + W004VFixture.Hash() + " base=" + W003PlayerFixtures.Stress.Scenario + ":" + W003PlayerFixtures.Stress.Sha256);
            var captured = new Dictionary<string, CapturedImage>();
            var metrics = new List<string>();
            yield return RenderCaptureHarness.Capture(screen, captured, metrics);
            foreach (var line in metrics)
            {
                Debug.Log(line);
            }

            var slug = RenderCaptureHarness.Slug(screen);
            var stems = RenderCaptureHarness.AllStems().Where(stem => stem.Contains("-" + slug + "-")).ToList();
            Assert.That(captured.Keys, Is.EquivalentTo(stems), slug + ": the harness produces exactly the baseline set of this screen class");

            if (RenderBaselines.WriteMode)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RenderBaselines.FullPath("x")));
                foreach (var pair in captured)
                {
                    File.WriteAllBytes(RenderBaselines.FullPath(pair.Key), RenderBaselines.Encode(pair.Value));
                    Debug.Log("RENDER-BASELINE written " + RenderBaselines.AssetPath(pair.Key));
                }

                yield break;
            }

            var failures = new List<string>();
            foreach (var pair in captured)
            {
                Assert.That(File.Exists(RenderBaselines.FullPath(pair.Key)), Is.True, "the baseline " + RenderBaselines.AssetPath(pair.Key) + " is committed");
                var result = RenderParity.Compare(RenderBaselines.Load(pair.Key), pair.Value);
                Debug.Log("RENDER-PARITY " + pair.Key + ": " + result);
                if (!result.Passes)
                {
                    Directory.CreateDirectory(RenderBaselines.FailureDirectory);
                    File.WriteAllBytes(Path.Combine(RenderBaselines.FailureDirectory, pair.Key + ".actual.png"), RenderBaselines.Encode(pair.Value));
                    failures.Add(pair.Key + ": " + result);
                }
            }

            Assert.That(failures, Is.Empty, slug + ": frames outside the tolerance (actual frames in " + RenderBaselines.FailureDirectory + ")");
        }

        // -------------------------------------------------------------------------------------------------------
        // The comparison helper
        // -------------------------------------------------------------------------------------------------------

        private static CapturedImage Flat(int width, int height, Color32 color)
        {
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
            return new CapturedImage(width, height, pixels);
        }

        [Test]
        public void IdenticalFramesPass()
        {
            var result = RenderParity.Compare(Flat(100, 100, new Color32(10, 20, 30, 255)), Flat(100, 100, new Color32(10, 20, 30, 255)));
            Assert.That(result.Passes, Is.True);
            Assert.That(result.Differing, Is.EqualTo(0));
        }

        // T457 (phase0-review A6, F1): the comparator is R13-literal (owner decision, Issue #523). A pixel counts as differing only
        // above 2/255, and any such pixel already breaks the 2/255 channel rule, so the 0.1 % budget never decides. The tests below
        // are labelled for what they really prove, and each one can fail.

        [Test]
        public void ADeltaOfTwoOnOnePixelIsNotCountedAsDifferingAndPasses()
        {
            var expected = Flat(100, 100, new Color32(10, 20, 30, 255));
            var actual = Flat(100, 100, new Color32(10, 20, 30, 255));
            actual.Pixels[0] = new Color32(12, 18, 30, 255);
            var result = RenderParity.Compare(expected, actual);
            Assert.That(result.MaxChannelDelta, Is.EqualTo(2));
            Assert.That(result.Differing, Is.EqualTo(0), "a delta of 2 does not count as differing (R13)");
            Assert.That(result.Passes, Is.True, result.ToString());
        }

        [Test]
        public void AChannelDifferenceOfThreeFails()
        {
            var expected = Flat(100, 100, new Color32(10, 20, 30, 255));
            var actual = Flat(100, 100, new Color32(10, 20, 30, 255));
            actual.Pixels[5] = new Color32(13, 20, 30, 255);
            var result = RenderParity.Compare(expected, actual);
            Assert.That(result.Differing, Is.EqualTo(1), "a delta of 3 counts as differing");
            Assert.That(result.Passes, Is.False);
        }

        [Test]
        public void ADeltaOfThreeOnElevenPixelsIsCountedAndFailsOnTheChannelRule()
        {
            var expected = Flat(100, 100, new Color32(10, 20, 30, 255));
            var actual = Flat(100, 100, new Color32(10, 20, 30, 255));
            for (var i = 0; i < 11; i++) actual.Pixels[i] = new Color32(13, 20, 30, 255);
            var result = RenderParity.Compare(expected, actual);
            Assert.That(result.Differing, Is.EqualTo(11));
            Assert.That(result.MaxChannelDelta, Is.EqualTo(3));
            Assert.That(result.Passes, Is.False, "fails because of the channel rule; the fraction budget cannot be reached on its own under R13");
        }

        [Test]
        public void AnAlphaOnlyDeltaOfThreeFails()
        {
            var expected = Flat(100, 100, new Color32(10, 20, 30, 255));
            var actual = Flat(100, 100, new Color32(10, 20, 30, 255));
            actual.Pixels[42] = new Color32(10, 20, 30, 252);
            var result = RenderParity.Compare(expected, actual);
            Assert.That(result.Differing, Is.EqualTo(1));
            Assert.That(result.Passes, Is.False);
        }

        [Test]
        public void EveryPixelShiftedByOneOverTheWholeFramePasses()
        {
            // The behaviour the R13-literal owner decision introduced: a uniform 1/255 shift is not a difference at all.
            var expected = Flat(100, 100, new Color32(10, 20, 30, 255));
            var actual = Flat(100, 100, new Color32(11, 19, 31, 254));
            var result = RenderParity.Compare(expected, actual);
            Assert.That(result.Differing, Is.EqualTo(0));
            Assert.That(result.MaxChannelDelta, Is.EqualTo(1));
            Assert.That(result.Passes, Is.True, result.ToString());
        }

        [Test]
        public void EveryPixelShiftedByTwoOverTheWholeFramePassesButByThreeFails()
        {
            var expected = Flat(100, 100, new Color32(10, 20, 30, 255));
            var shiftedByTwo = Flat(100, 100, new Color32(12, 20, 28, 255));
            Assert.That(RenderParity.Compare(expected, shiftedByTwo).Passes, Is.True, "2/255 everywhere is inside the tolerance");

            var shiftedByThree = Flat(100, 100, new Color32(13, 20, 30, 255));
            var result = RenderParity.Compare(expected, shiftedByThree);
            Assert.That(result.Differing, Is.EqualTo(result.Pixels), "3/255 everywhere counts every pixel");
            Assert.That(result.Passes, Is.False);
        }

        [Test]
        public void DifferentSizesFail()
        {
            Assert.That(RenderParity.Compare(Flat(10, 10, default), Flat(10, 11, default)).Passes, Is.False);
        }

        [Test]
        public void ThePngRoundTripKeepsEveryPixel()
        {
            var image = Flat(32, 16, new Color32(1, 2, 3, 255));
            image.Pixels[7] = new Color32(200, 100, 50, 255);
            var bytes = RenderBaselines.Encode(image);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.That(texture.LoadImage(bytes, false), Is.True);
                var back = new CapturedImage(texture.width, texture.height, texture.GetPixels32());
                var result = RenderParity.Compare(image, back);
                Assert.That(result.SameSize && result.Differing == 0, Is.True, result.ToString());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // The fixture and the baseline files
        // -------------------------------------------------------------------------------------------------------

        [Test]
        public void TheW004VFixtureIsPinned()
        {
            Assert.That(W004VFixture.Hash(), Is.EqualTo(W004VFixture.ExpectedHash), "W004-V definition: " + W004VFixture.Canonical());
            Assert.That(W004VFixture.OverdriveAt(W004VFixture.SelectedSlot) && W004VFixture.BoostedAt(W004VFixture.SelectedSlot), Is.True, "the selected slot carries the densest overlay");
            var seen = Enumerable.Range(0, SlotGridLayout.SlotCount).Select(slot => W004VFixture.StateOf(slot).Name).Distinct().Count();
            Assert.That(seen, Is.EqualTo(W004VFixture.States.Length), "all six machine states appear");
        }

        [Test]
        public void TheB1PreSwitchBaselinesAreUnchanged()
        {
            // reviews/baselines-b1-manifest.md section 2: B1 is the before-evidence of T444 and SC-001 and is never re-baselined.
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RenderBaselines.B1Directory));
            var names = Directory.GetFiles(folder, "*.png").Select(Path.GetFileName).ToList();
            names.Sort(StringComparer.Ordinal);
            Assert.That(names.Count, Is.EqualTo(36));
            var listing = new System.Text.StringBuilder();
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                foreach (var name in names)
                {
                    var hash = string.Concat(sha.ComputeHash(File.ReadAllBytes(Path.Combine(folder, name))).Select(b => b.ToString("x2")));
                    listing.Append(hash).Append("  ").Append(name).Append((char)10);
                }

                var total = string.Concat(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(listing.ToString())).Select(b => b.ToString("x2")));
                Assert.That(total, Is.EqualTo("81201a30cdfb404c9fb03425aa2cc3646485fa9ee68488838730675d9a67f65a"), "the F004-B1 listing hash of the manifest");
            }
        }

        [Test]
        public void EveryBaselineExistsAndKeepsAReadableUncompressedImporterOutsideEveryAtlas()
        {
            long totalBytes = 0;
            var atlasText = AssetDatabase.FindAssets("t:SpriteAtlas")
                .Select(guid => File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetDatabase.GUIDToAssetPath(guid)))))
                .ToList();
            var stems = RenderCaptureHarness.AllStems().ToList();
            Assert.That(stems.Count, Is.EqualTo(RenderCaptureHarness.Screens().Count() * 4));
            foreach (var directory in new[] { RenderBaselines.B1Directory, RenderBaselines.Directory })
            foreach (var stem in stems)
            {
                var path = RenderBaselines.AssetPath(stem, directory);
                Assert.That(File.Exists(RenderBaselines.FullPath(stem, directory)), Is.True, path + " exists");
                totalBytes += new FileInfo(RenderBaselines.FullPath(stem, directory)).Length;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path + " has a texture importer");
                Assert.That(importer.isReadable, Is.True, path + " stays readable");
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path + " stays uncompressed");
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default), path + " is a plain texture, not a sprite");
                Assert.That(importer.mipmapEnabled, Is.False, path + " has no mipmaps");
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), path + " keeps its exact size");
                Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(2048), path + " is not downscaled on import");
                foreach (var guid in new[] { AssetDatabase.AssetPathToGUID(path), AssetDatabase.AssetPathToGUID(directory) })
                {
                    Assert.That(atlasText.Any(text => text.Contains(guid)), Is.False, path + " is not packed into any atlas");
                }
            }

            Debug.Log("RENDER-BASELINE-SIZE files=" + stems.Count * 2 + " bytes=" + totalBytes);
        }
    }
}
#endif
