using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The missing-art path after T476 (owner decision 2026-11-05, option D: the Feature 003 placeholder sprites are deleted). There is no
    /// fallback frame. A key whose frames are missing or fail the per-key requirement registry (T428) makes the catalog fail closed with
    /// <c>presentation.key.unresolved: KEY</c>, logged once per key and never with a path; start-up then reports
    /// <c>presentation.catalog.invalid</c> and no gameplay control is offered. Nothing is substituted and nothing is drawn blank. The class
    /// keeps its name so that the contract and quickstart references to it stay stable.
    /// </summary>
    public sealed class PresentationCatalogFallbackTests
    {
        private const string Slot = "F003-SLOT-OCCUPIED-BASE";
        private const string Machine = "F003-MACHINE-MINER";
        private const string Badge = "F003-BADGE-WAITING";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void DestroyCreatedAssets()
        {
            foreach (var asset in created)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            created.Clear();
        }

        private Sprite Make(string name, float worldWidth, float worldHeight, float pivotX, float pivotY, float pixelsPerUnit)
        {
            var width = Mathf.RoundToInt(worldWidth * pixelsPerUnit);
            var height = Mathf.RoundToInt(worldHeight * pixelsPerUnit);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(pivotX, pivotY), pixelsPerUnit);
            sprite.name = name;
            created.Add(texture);
            created.Add(sprite);
            return sprite;
        }

        // The F004 shape: the full 80 x 64 art-pixel canvas layer at D = PPU = 24, footprint pivot (40, 15).
        private Sprite SlotCanvas() => Make("f004-slot", 80f / 24f, 64f / 24f, 40f / 80f, 15f / 64f, 24f);

        private Sprite BadgeCanvas() => Make("f004-badge", 80f / 24f, 64f / 24f, 40f / 80f, 15f / 64f, 24f);

        // A 64 PPU tight development sprite of the legacy shape (2 x 2 cells, bottom-left pivot): legal in a development or unit-test catalog.
        private Sprite SlotTight() => Make("tight-slot", 2f, 2f, 0f, 0f, 64f);

        private static PresentationSpriteCatalog Catalog(params PresentationSpriteEntry[] entries)
        {
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(entries);
            return catalog;
        }

        private static void AssertFailedClosed(PresentationSpriteCatalog catalog, string key, string reason)
        {
            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.StartWith("presentation.key.unresolved"));
            Assert.That(catalog.Error, Does.Contain(reason));
            Assert.That(catalog.Diagnostics, Is.EqualTo(new[] { "presentation.key.unresolved: " + key }));
            Assert.That(catalog.TryGet(key, out _), Is.False, "a failed catalog resolves nothing");
            Assert.That(catalog.TryGetEntry(key, out _), Is.False);
            Assert.That(() => catalog.Get(key), Throws.InvalidOperationException.With.Message.StartsWith("presentation.key.unresolved"));
            Assert.That(catalog.Diagnostics[0], Does.Not.Contain("Assets/").And.Not.Contain("\\").And.Not.Contain(":/"), "no asset path in a diagnostic");
        }

        [Test]
        public void TheCanvasShapeAndATightDevelopmentSpriteEachSatisfyTheSameRowAndNothingIsDiagnosed()
        {
            var canvas = SlotCanvas();
            var fromCanvas = Catalog(new PresentationSpriteEntry(Slot, canvas));
            Assert.That(fromCanvas.IsValid, Is.True, fromCanvas.Error);
            Assert.That(fromCanvas.Get(Slot), Is.SameAs(canvas));
            Assert.That(fromCanvas.Diagnostics, Is.Empty);

            var tight = SlotTight();
            var fromTight = Catalog(new PresentationSpriteEntry(Slot, tight));
            Assert.That(fromTight.IsValid, Is.True, fromTight.Error);
            Assert.That(fromTight.Get(Slot), Is.SameAs(tight));
            Assert.That(tight.pixelsPerUnit, Is.EqualTo(64f), "a tight development sprite keeps its own PPU");
            Assert.That(fromTight.Diagnostics, Is.Empty);
        }

        [Test]
        public void AFailingFrameSetFailsClosedWithTheUnresolvedCodeOnceAndWithoutAPath()
        {
            var catalog = new PresentationSpriteCatalog();
            var wrongSize = Make("f004-slot", 3f, 2f, 0f, 0f, 24f);
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + Slot);
            catalog.Configure(new[] { new PresentationSpriteEntry(Slot, wrongSize) });

            AssertFailedClosed(catalog, Slot, "world size");

            // Validating again must not log a second time.
            Assert.That(catalog.Validate(), Is.False);
            Assert.That(catalog.Diagnostics, Has.Count.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AWrongPivotIsReportedWithItsReasonAndFailsClosed()
        {
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + Machine);
            var catalog = Catalog(new PresentationSpriteEntry(Machine, Make("f004-machine", 80f / 24f, 64f / 24f, 0.5f, 0.5f, 24f)));

            AssertFailedClosed(catalog, Machine, "pivot");
        }

        [Test]
        public void ATooLongAnimationAndANullFrameFailClosedInsteadOfShowingAStaticFrame()
        {
            var tooMany = Enumerable.Range(0, 17).Select(_ => Make("f004-machine", 80f / 24f, 64f / 24f, 0.5f, 15f / 64f, 24f)).ToArray();
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + Machine);
            var catalog = Catalog(new PresentationSpriteEntry(Machine, tooMany, 6f));
            AssertFailedClosed(catalog, Machine, "frame(s)");

            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + Machine);
            var withNull = Catalog(new PresentationSpriteEntry(Machine, new Sprite[] { null }, 6f));
            AssertFailedClosed(withNull, Machine, "null frame");

            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + Machine);
            var empty = Catalog(new PresentationSpriteEntry(Machine, new Sprite[0], 6f));
            AssertFailedClosed(empty, Machine, "no frame");
        }

        [Test]
        public void OneWrongKeyOfAPartialSetFailsTheWholeCatalogAndNamesOnlyThatKey()
        {
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + Slot);
            var catalog = Catalog(
                new PresentationSpriteEntry(Slot, Make("f004-slot", 80f / 24f, 64f / 24f, 0.5f, 0.5f, 24f)),
                new PresentationSpriteEntry(Badge, BadgeCanvas()));

            Assert.That(catalog.IsValid, Is.False, "one failing key fails the catalog; the valid key does not rescue it");
            Assert.That(catalog.Diagnostics, Is.EqualTo(new[] { "presentation.key.unresolved: " + Slot }));
            Assert.That(catalog.TryGet(Badge, out _), Is.False, "a failed catalog resolves no key, so nothing is drawn from a half-valid set");
        }

        [Test]
        public void AFailedCatalogRecoversWhenTheArtIsCorrected()
        {
            var catalog = new PresentationSpriteCatalog();
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + Slot);
            catalog.Configure(new[] { new PresentationSpriteEntry(Slot, Make("broken", 1f, 1f, 0f, 0f, 24f)) });
            Assert.That(catalog.IsValid, Is.False);

            var corrected = SlotCanvas();
            catalog.Configure(new[] { new PresentationSpriteEntry(Slot, corrected) });
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Get(Slot), Is.SameAs(corrected));
        }

        [Test]
        public void AdditiveF004KeysAreValidatedByTheirOwnRowAndStructurallyWithoutOne()
        {
            var cue = Make("f004-cue", 0.5f, 0.5f, 0.5f, 0.5f, 24f);
            var row = new PresentationSpriteRequirement("F004-CUE-RUNNING", 0.5f, 0.5f, 0.5f, 0.5f, 1, 1, true);

            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(new[] { new PresentationSpriteEntry("F004-CUE-RUNNING", cue) }, new[] { row });
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Get("F004-CUE-RUNNING"), Is.SameAs(cue));

            var wrong = new PresentationSpriteCatalog();
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: F004-CUE-RUNNING");
            wrong.Configure(new[] { new PresentationSpriteEntry("F004-CUE-RUNNING", Make("f004-cue", 1f, 1f, 0.5f, 0.5f, 24f)) }, new[] { row });
            Assert.That(wrong.IsValid, Is.False);

            var missing = new PresentationSpriteCatalog();
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: F004-CUE-RUNNING");
            missing.Configure(new PresentationSpriteEntry[0], new[] { row });
            Assert.That(missing.IsValid, Is.False, "a required row whose key is not registered fails closed");
            Assert.That(missing.Error, Does.Contain("Missing required presentation mapping 'F004-CUE-RUNNING'"));

            var unregistered = Catalog(new PresentationSpriteEntry(PresentationSpriteCatalog.F004Prefix + "ANY", cue));
            Assert.That(unregistered.IsValid, Is.True, unregistered.Error);
            Assert.That(unregistered.Count, Is.EqualTo(1));
        }

        [Test]
        public void ADevelopmentOrUnitTestCatalogStaysLegalWithoutTheShippedKeysAndOnlyARegistryRowCatalogRequiresThem()
        {
            var smaller = Catalog(new PresentationSpriteEntry(Slot, SlotCanvas()));
            Assert.That(smaller.IsValid, Is.True, "a development or unit-test catalog may omit keys; the views then draw nothing for them");
            Assert.That(smaller.TryGet(Badge, out _), Is.False);

            // The shipped scene sets RequireRegistryRows: the F003 rows are required there, so the first missing key fails it closed.
            var strict = new PresentationSpriteCatalog { RequireRegistryRows = true };
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"^presentation\.key\.unresolved: F00[34]-"));
            strict.Configure(new[] { new PresentationSpriteEntry(Slot, SlotCanvas()) });
            Assert.That(strict.IsValid, Is.False);
            Assert.That(strict.Error, Does.StartWith("presentation.key.unresolved").And.Contain("Missing required presentation mapping 'F00"));
        }
    }
}
