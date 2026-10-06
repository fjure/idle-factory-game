using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T110: the presentation sprite catalog is resolution-agnostic. A requirement validates a sprite's world size
    /// (pixel size divided by pixels per unit) and normalized pivot, so the same key accepts art at 64 PPU (today) or
    /// 32 PPU (the Feature 004 restyle) without code changes. An entry may hold an ordered frame list.
    /// </summary>
    public sealed class PresentationSpriteCatalogTests
    {
        private readonly struct Row
        {
            public Row(string key, string name, float worldWidth, float worldHeight, float pivotX, float pivotY)
            {
                Key = key;
                Name = name;
                WorldWidth = worldWidth;
                WorldHeight = worldHeight;
                PivotX = pivotX;
                PivotY = pivotY;
            }

            public string Key { get; }
            public string Name { get; }
            public float WorldWidth { get; }
            public float WorldHeight { get; }
            public float PivotX { get; }
            public float PivotY { get; }
        }

        // The approved F001 set: world size in world units (1 cell = 1 unit) and normalized pivot.
        private static readonly Row[] Approved =
        {
            new Row(PresentationSpriteCatalog.GridTile, "f001-grid-tile-1x1-v01", 1f, 1f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.BoundaryEdge, "f001-grid-boundary-edge-v01", 1f, 1f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.BoundaryCorner, "f001-grid-boundary-corner-v01", 1f, 1f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.D001Normal, "f001-machine-d001-normal-v01", 2f, 2f, 0f, 0f),
            new Row(PresentationSpriteCatalog.D001Selected, "f001-machine-d001-selected-v01", 2f, 2f, 0f, 0f),
            new Row(PresentationSpriteCatalog.AlternateNormal, "f001-machine-alt-3x1-normal-v01", 3f, 1f, 0f, 0f),
            new Row(PresentationSpriteCatalog.AlternateSelected, "f001-machine-alt-3x1-selected-v01", 3f, 1f, 0f, 0f),
            new Row(PresentationSpriteCatalog.D001GhostValid, "f001-ghost-d001-valid-v01", 2f, 2f, 0f, 0f),
            new Row(PresentationSpriteCatalog.D001GhostInvalid, "f001-ghost-d001-invalid-v01", 2f, 2f, 0f, 0f),
            new Row(PresentationSpriteCatalog.AlternateGhostValid, "f001-ghost-alt-3x1-valid-v01", 3f, 1f, 0f, 0f),
            new Row(PresentationSpriteCatalog.AlternateGhostInvalid, "f001-ghost-alt-3x1-invalid-v01", 3f, 1f, 0f, 0f),
            new Row(PresentationSpriteCatalog.ResourceInput, "f001-resource-dev-input-v01", 0.5f, 0.5f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.ResourceOutput, "f001-resource-dev-output-v01", 0.5f, 0.5f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.BadgeNormal, "f001-badge-normal-v01", 1f, 0.5f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.BadgeSelected, "f001-badge-selected-v01", 1f, 0.5f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.BadgePlacementValid, "f001-badge-placement-valid-v01", 1f, 0.5f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.BadgePlacementInvalid, "f001-badge-placement-invalid-v01", 1f, 0.5f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.BadgeProducing, "f001-badge-producing-v01", 1f, 0.5f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.BadgeMissingInput, "f001-badge-missing-input-v01", 1f, 0.5f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.BadgeFullOutput, "f001-badge-full-output-v01", 1f, 0.5f, 0.5f, 0.5f),
            new Row(PresentationSpriteCatalog.BadgeInactive, "f001-badge-inactive-v01", 1f, 0.5f, 0.5f, 0.5f),
        };

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

        private Sprite MakeSprite(string name, float worldWidth, float worldHeight, float pivotX, float pivotY, float pixelsPerUnit)
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

        private Sprite MakeApproved(Row row, float pixelsPerUnit) =>
            MakeSprite(row.Name, row.WorldWidth, row.WorldHeight, row.PivotX, row.PivotY, pixelsPerUnit);

        private List<PresentationSpriteEntry> ApprovedEntries(float pixelsPerUnit) =>
            Approved.Select(row => new PresentationSpriteEntry(row.Key, MakeApproved(row, pixelsPerUnit))).ToList();

        private static PresentationSpriteCatalog Catalog(IEnumerable<PresentationSpriteEntry> entries)
        {
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(entries);
            return catalog;
        }

        private static List<PresentationSpriteEntry> Replace(List<PresentationSpriteEntry> entries, string key, PresentationSpriteEntry replacement)
        {
            var index = entries.FindIndex(entry => entry.Key == key);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), key);
            entries[index] = replacement;
            return entries;
        }

        [Test]
        public void TheApprovedSetHasTwentyOneF001Requirements()
        {
            Assert.That(Approved.Length, Is.EqualTo(21));
            var catalog = Catalog(ApprovedEntries(64f));
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Count, Is.EqualTo(21));
        }

        [Test]
        public void TheSameKeysValidateAt64And32PixelsPerUnitWithEqualWorldSize()
        {
            var at64 = Catalog(ApprovedEntries(64f));
            var at32 = Catalog(ApprovedEntries(32f));

            Assert.That(at64.IsValid, Is.True, at64.Error);
            Assert.That(at32.IsValid, Is.True, at32.Error);
            foreach (var row in Approved)
            {
                var sprite64 = at64.Get(row.Key);
                var sprite32 = at32.Get(row.Key);
                Assert.That(sprite32.rect.width / sprite32.pixelsPerUnit,
                    Is.EqualTo(sprite64.rect.width / sprite64.pixelsPerUnit).Within(0.001f), row.Key);
                Assert.That(sprite32.rect.height / sprite32.pixelsPerUnit,
                    Is.EqualTo(sprite64.rect.height / sprite64.pixelsPerUnit).Within(0.001f), row.Key);
            }

            var machine64 = at64.Get(PresentationSpriteCatalog.D001Normal);
            var machine32 = at32.Get(PresentationSpriteCatalog.D001Normal);
            Assert.That(machine64.rect.width, Is.EqualTo(128f));
            Assert.That(machine32.rect.width, Is.EqualTo(64f));
        }

        [Test]
        public void AWrongWorldSizeFailsValidationAndNamesTheKey()
        {
            var entries = Replace(
                ApprovedEntries(64f),
                PresentationSpriteCatalog.D001Normal,
                new PresentationSpriteEntry(
                    PresentationSpriteCatalog.D001Normal,
                    MakeSprite("f001-machine-d001-normal-v01", 3f, 2f, 0f, 0f, 64f)));

            var catalog = Catalog(entries);

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.Contain(PresentationSpriteCatalog.D001Normal));
            Assert.That(catalog.Error, Does.Contain("world size"));
            Assert.That(catalog.TryGet(PresentationSpriteCatalog.D001Normal, out _), Is.False);
        }

        [Test]
        public void AWrongPivotFailsValidationAndNamesTheKey()
        {
            var entries = Replace(
                ApprovedEntries(64f),
                PresentationSpriteCatalog.D001Normal,
                new PresentationSpriteEntry(
                    PresentationSpriteCatalog.D001Normal,
                    MakeSprite("f001-machine-d001-normal-v01", 2f, 2f, 0.5f, 0.5f, 64f)));

            var catalog = Catalog(entries);

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.Contain(PresentationSpriteCatalog.D001Normal));
            Assert.That(catalog.Error, Does.Contain("pivot"));
        }

        [Test]
        public void AnEmptyFrameListFailsValidationAndNamesTheKey()
        {
            var entries = Replace(
                ApprovedEntries(64f),
                PresentationSpriteCatalog.BadgeProducing,
                new PresentationSpriteEntry(PresentationSpriteCatalog.BadgeProducing, new Sprite[0], null));

            var catalog = Catalog(entries);

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.Contain(PresentationSpriteCatalog.BadgeProducing));
            Assert.That(catalog.Error, Does.Contain("no frame"));
        }

        [Test]
        public void ANullFrameFailsValidationAndNamesTheKey()
        {
            var valid = MakeApproved(Approved.Single(row => row.Key == PresentationSpriteCatalog.BadgeProducing), 64f);
            var entries = Replace(
                ApprovedEntries(64f),
                PresentationSpriteCatalog.BadgeProducing,
                new PresentationSpriteEntry(PresentationSpriteCatalog.BadgeProducing, new[] { valid, null }, 4f));

            var catalog = Catalog(entries);

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.Contain(PresentationSpriteCatalog.BadgeProducing));
            Assert.That(catalog.Error, Does.Contain("null frame"));

            var singleNull = Replace(
                ApprovedEntries(64f),
                PresentationSpriteCatalog.BadgeProducing,
                new PresentationSpriteEntry(PresentationSpriteCatalog.BadgeProducing, (Sprite)null));
            var nullSprite = Catalog(singleNull);
            Assert.That(nullSprite.IsValid, Is.False);
            Assert.That(nullSprite.Error, Does.Contain(PresentationSpriteCatalog.BadgeProducing));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        public void ANonPositiveFramesPerSecondFailsValidationAndNamesTheKey(float framesPerSecond)
        {
            var row = Approved.Single(candidate => candidate.Key == PresentationSpriteCatalog.BadgeProducing);
            var entries = Replace(
                ApprovedEntries(64f),
                PresentationSpriteCatalog.BadgeProducing,
                new PresentationSpriteEntry(
                    PresentationSpriteCatalog.BadgeProducing,
                    new[] { MakeApproved(row, 64f), MakeApproved(row, 64f) },
                    framesPerSecond));

            var catalog = Catalog(entries);

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.Contain(PresentationSpriteCatalog.BadgeProducing));
            Assert.That(catalog.Error, Does.Contain("frames-per-second"));
        }

        [Test]
        public void AOneFrameEntryEqualsTheSingleSpriteBehavior()
        {
            var row = Approved.Single(candidate => candidate.Key == PresentationSpriteCatalog.BadgeProducing);
            var sprite = MakeApproved(row, 64f);
            var single = new PresentationSpriteEntry(row.Key, sprite);
            var oneFrame = new PresentationSpriteEntry(row.Key, new[] { sprite }, null);

            Assert.That(single.FrameCount, Is.EqualTo(1));
            Assert.That(oneFrame.FrameCount, Is.EqualTo(1));
            Assert.That(oneFrame.Sprite, Is.SameAs(single.Sprite));
            Assert.That(oneFrame.GetFrame(0), Is.SameAs(single.GetFrame(0)));
            Assert.That(single.FramesPerSecond, Is.Null);
            Assert.That(oneFrame.FramesPerSecond, Is.Null);

            var fromSingle = Catalog(Replace(ApprovedEntries(64f), row.Key, single));
            var fromList = Catalog(Replace(ApprovedEntries(64f), row.Key, oneFrame));
            Assert.That(fromSingle.IsValid, Is.True, fromSingle.Error);
            Assert.That(fromList.IsValid, Is.True, fromList.Error);
            Assert.That(fromList.Get(row.Key), Is.SameAs(fromSingle.Get(row.Key)));
        }

        [Test]
        public void AnAnimatedEntryKeepsItsFrameOrderAndNonAnimatingConsumersUseFrameZero()
        {
            var row = Approved.Single(candidate => candidate.Key == PresentationSpriteCatalog.BadgeProducing);
            var first = MakeApproved(row, 64f);
            var second = MakeApproved(row, 64f);
            var third = MakeApproved(row, 64f);
            var catalog = Catalog(Replace(
                ApprovedEntries(64f),
                row.Key,
                new PresentationSpriteEntry(row.Key, new[] { first, second, third }, 6f)));

            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Get(row.Key), Is.SameAs(first));
            Assert.That(catalog.TryGetEntry(row.Key, out var entry), Is.True);
            Assert.That(entry.FrameCount, Is.EqualTo(3));
            Assert.That(entry.GetFrame(1), Is.SameAs(second));
            Assert.That(entry.GetFrame(2), Is.SameAs(third));
            Assert.That(entry.FramesPerSecond, Is.EqualTo(6f));
        }

        [Test]
        public void EveryFrameOfAnAnimatedEntryIsValidated()
        {
            var row = Approved.Single(candidate => candidate.Key == PresentationSpriteCatalog.BadgeProducing);
            var wrongSize = MakeSprite(row.Name, 2f, 2f, 0.5f, 0.5f, 64f);
            var catalog = Catalog(Replace(
                ApprovedEntries(64f),
                row.Key,
                new PresentationSpriteEntry(row.Key, new[] { MakeApproved(row, 64f), wrongSize }, 6f)));

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.Contain(row.Key));
            Assert.That(catalog.Error, Does.Contain("world size"));
        }

        [Test]
        public void ARequiredF001KeyThatIsMissingFailsValidationAndNamesTheKey()
        {
            var entries = ApprovedEntries(64f);
            entries.RemoveAll(entry => entry.Key == PresentationSpriteCatalog.GridTile);

            var catalog = Catalog(entries);

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.Contain(PresentationSpriteCatalog.GridTile));
        }

        [Test]
        public void TheWrongApprovedSpriteForAKeyFails()
        {
            var selected = Approved.Single(row => row.Key == PresentationSpriteCatalog.D001Selected);
            var entries = Replace(
                ApprovedEntries(64f),
                PresentationSpriteCatalog.D001Normal,
                new PresentationSpriteEntry(PresentationSpriteCatalog.D001Normal, MakeApproved(selected, 64f)));

            var catalog = Catalog(entries);

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.Contain("wrong approved sprite"));
            Assert.That(catalog.Error, Does.Contain(PresentationSpriteCatalog.D001Normal));
        }

        [Test]
        public void ADuplicateKeyFails()
        {
            var entries = ApprovedEntries(64f);
            entries.Add(entries[0]);

            var catalog = Catalog(entries);

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.Contain("Duplicate"));
        }

        [Test]
        public void F003KeysAreAcceptedAsExtraEntries()
        {
            var machine = MakeSprite("f003-machine-miner-v01", 2f, 2f, 0.5f, 0.5f, 64f);
            var entries = ApprovedEntries(64f);
            entries.Add(new PresentationSpriteEntry("F003-MACHINE-MINER", machine));
            var catalog = Catalog(entries);

            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Count, Is.EqualTo(22));
            Assert.That(catalog.Get("F003-MACHINE-MINER"), Is.SameAs(machine));
        }

        [Test]
        public void AnUnregisteredF003KeyAndAnUnknownKeyStayUnresolved()
        {
            var catalog = Catalog(ApprovedEntries(64f));

            Assert.That(catalog.TryGet("F003-RES-IRON-ORE", out var sprite), Is.False);
            Assert.That(sprite, Is.Null);
            Assert.That(catalog.TryGetEntry("F003-RES-IRON-ORE", out _), Is.False);
            Assert.That(catalog.TryGet("F003-NOT-IN-THE-CONTENT", out _), Is.False);
            Assert.That(catalog.TryGet("MISSING-SPRITE", out _), Is.False);
            Assert.That(catalog.TryGet(null, out _), Is.False);
            Assert.That(() => catalog.Get("MISSING-SPRITE"), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void AnUnvalidatedCatalogResolvesNothing()
        {
            var catalog = new PresentationSpriteCatalog();

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.TryGet("F003-RES-IRON-ORE", out _), Is.False);
            Assert.That(catalog.TryGet(PresentationSpriteCatalog.GridTile, out _), Is.False);
        }
    }
}
