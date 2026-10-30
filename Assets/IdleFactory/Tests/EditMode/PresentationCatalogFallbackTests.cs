using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T428: the per-key requirement registry replaces the hard-coded F001 table, and a key whose primary (F004) frames
    /// fail validation runs on its retained F003 fallback frames only when those validate (<c>presentation.art.fallback</c>,
    /// once per key, no paths); otherwise start-up fails closed (<c>presentation.key.unresolved</c>).
    /// </summary>
    public sealed class PresentationCatalogFallbackTests
    {
        private const string SpriteRoot = "Assets/IdleFactory/Art/Development/F003/Sprites";
        private const string Slot = "F003-SLOT-OCCUPIED-BASE";
        private const string Machine = "F003-MACHINE-MINER";
        private const string Badge = "F003-BADGE-RUNNING";

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

        // F004 art: D = 24 = PPU, bottom-left pivot, 2x2 world units (48x48 px).
        private Sprite SlotPrimary() => Make("f004-slot", 2f, 2f, 0f, 0f, 24f);

        // F003 placeholder: 64 PPU, 128x128, bottom-left pivot, the same 2x2 world units.
        private Sprite SlotFallback() => Make("f003-slot", 2f, 2f, 0f, 0f, 64f);

        private Sprite BadgePrimary() => Make("f004-badge", 72f / 64f, 0.5f, 0.5f, 0.5f, 24f);

        private Sprite BadgeFallback() => Make("f003-badge", 72f / 64f, 0.5f, 0.5f, 0.5f, 64f);

        private static PresentationSpriteCatalog Catalog(params PresentationSpriteEntry[] entries)
        {
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(entries);
            return catalog;
        }

        [Test]
        public void PrimaryFramesAtTwentyFourPixelsPerUnitAndTheFallbackAtSixtyFourBothSatisfyTheSameRow()
        {
            var primary = SlotPrimary();
            var fallback = SlotFallback();
            var catalog = Catalog(new PresentationSpriteEntry(Slot, primary).WithFallback(fallback));

            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Get(Slot), Is.SameAs(primary));
            Assert.That(catalog.IsFallback(Slot), Is.False);
            Assert.That(catalog.Diagnostics, Is.Empty);
            Assert.That(primary.pixelsPerUnit, Is.EqualTo(24f));
            Assert.That(fallback.pixelsPerUnit, Is.EqualTo(64f));
        }

        [Test]
        public void AFailingPrimaryFallsBackToTheValidFallbackAndLogsOncePerKeyWithoutAPath()
        {
            var wrongSize = Make("f004-slot", 3f, 2f, 0f, 0f, 24f);
            var fallback = SlotFallback();
            var catalog = new PresentationSpriteCatalog();
            var entry = new PresentationSpriteEntry(Slot, wrongSize).WithFallback(fallback);

            LogAssert.Expect(LogType.Warning, "presentation.art.fallback: " + Slot);
            catalog.Configure(new[] { entry });

            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Get(Slot), Is.SameAs(fallback));
            Assert.That(catalog.Get(Slot).pixelsPerUnit, Is.EqualTo(64f), "the fallback keeps its own PPU");
            Assert.That(catalog.IsFallback(Slot), Is.True);
            Assert.That(catalog.FallbackKeys, Is.EquivalentTo(new[] { Slot }));
            Assert.That(catalog.Diagnostics, Is.EqualTo(new[] { "presentation.art.fallback: " + Slot }));

            // Validating again must not log a second time.
            Assert.That(catalog.Validate(), Is.True);
            Assert.That(catalog.Diagnostics, Has.Count.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
            Assert.That(catalog.Diagnostics[0], Does.Not.Contain("Assets/").And.Not.Contain("\\").And.Not.Contain(":/"));
        }

        [Test]
        public void ASingleWrongKeyOfAPartialSetFallsBackWhileTheOthersKeepTheirPrimaryFrames()
        {
            var badgePrimary = BadgePrimary();
            var catalog = new PresentationSpriteCatalog();
            LogAssert.Expect(LogType.Warning, "presentation.art.fallback: " + Slot);
            catalog.Configure(new[]
            {
                new PresentationSpriteEntry(Slot, Make("f004-slot", 2f, 2f, 0.5f, 0.5f, 24f)).WithFallback(SlotFallback()),
                new PresentationSpriteEntry(Badge, badgePrimary).WithFallback(BadgeFallback()),
            });

            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.IsFallback(Slot), Is.True, "a primary with an offset pivot fails");
            Assert.That(catalog.IsFallback(Badge), Is.False);
            Assert.That(catalog.Get(Badge), Is.SameAs(badgePrimary));
        }

        [Test]
        public void AFallbackThatAlsoFailsFailsClosedWithTheUnresolvedCode()
        {
            var catalog = new PresentationSpriteCatalog();
            var badFallback = Make("f003-slot", 2f, 2f, 0.5f, 0.5f, 64f);
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + Slot);

            catalog.Configure(new[] { new PresentationSpriteEntry(Slot, Make("f004-slot", 1f, 1f, 0f, 0f, 24f)).WithFallback(badFallback) });

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.StartWith("presentation.key.unresolved"));
            Assert.That(catalog.Error, Does.Contain(Slot));
            Assert.That(catalog.Diagnostics, Is.EqualTo(new[] { "presentation.key.unresolved: " + Slot }));
            Assert.That(catalog.TryGet(Slot, out _), Is.False);
        }

        [Test]
        public void AFailingPrimaryWithoutAFallbackFailsClosedWithTheUnresolvedCode()
        {
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + Slot);
            var catalog = Catalog(new PresentationSpriteEntry(Slot, Make("f004-slot", 2f, 3f, 0f, 0f, 24f)));

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.StartWith("presentation.key.unresolved"));
            Assert.That(catalog.Error, Does.Contain("world size"));
        }

        [Test]
        public void AFallbackWithAnOffsetPivotIsNotUsed()
        {
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + Machine);
            var offset = Make("f003-machine", 2f, 2f, 0.25f, 0f, 64f);
            var catalog = Catalog(new PresentationSpriteEntry(Machine, Make("f004-machine", 2f, 2f, 0.5f, 0.5f, 24f)).WithFallback(offset));

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.StartWith("presentation.key.unresolved"));
            Assert.That(catalog.Error, Does.Contain("pivot"));
        }

        [Test]
        public void AnAnimatedPrimaryWithAWrongFrameCountOrFrameFallsBackToTheStaticPlaceholder()
        {
            var catalog = new PresentationSpriteCatalog();
            var tooMany = Enumerable.Range(0, 17).Select(_ => Make("f004-machine", 2f, 2f, 0f, 0f, 24f)).ToArray();
            var fallback = Make("f003-machine", 2f, 2f, 0f, 0f, 64f);
            LogAssert.Expect(LogType.Warning, "presentation.art.fallback: " + Machine);

            catalog.Configure(new[] { new PresentationSpriteEntry(Machine, tooMany, 6f).WithFallback(fallback) });

            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.TryGetEntry(Machine, out var entry), Is.True);
            Assert.That(entry.FrameCount, Is.EqualTo(1));
            Assert.That(entry.FramesPerSecond, Is.Null, "a fallback does not animate");
            Assert.That(entry.Sprite, Is.SameAs(fallback));
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

            var unregistered = Catalog(new PresentationSpriteEntry(PresentationSpriteCatalog.F004Prefix + "ANY", cue));
            Assert.That(unregistered.IsValid, Is.True, unregistered.Error);
            Assert.That(unregistered.Count, Is.EqualTo(1));
        }

        [Test]
        public void EveryF003KeyHasARegistryRowThatAcceptsItsApprovedArtAndRejectsAWrongShape()
        {
            var keys = new List<string>();
            var sprites = new List<Sprite>();
            foreach (var path in Directory.GetFiles(SpriteRoot, "*.png").Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                Assert.That(name, Does.EndWith("-v01"));
                keys.Add(name.Substring(0, name.Length - 4).ToUpperInvariant());
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(sprite, Is.Not.Null, path);
                sprites.Add(sprite);
            }

            Assert.That(keys, Has.Count.EqualTo(78));

            // Every key the views use is in the approved set (slot grid, locked levels, machine bodies).
            foreach (var viewKey in new[]
                     {
                         SlotGridModel.SlotEmptyKey, SlotGridModel.SlotOccupiedKey, SlotGridModel.SlotSelectedFrameKey,
                         SlotGridModel.BadgeRunningKey, SlotGridModel.BadgeWaitingKey, SlotGridModel.BadgeMissingInputKey,
                         SlotGridModel.BadgeStorageFullKey, SlotGridModel.BadgePausedKey, SlotGridModel.BadgeInactiveKey,
                         SlotGridModel.MarkerBoostedKey, SlotGridModel.MarkerOverdriveKey,
                         SlotGridModel.LockedKey(2), SlotGridModel.LockedKey(3), SlotGridModel.LockedKey(4), SlotGridModel.LockedKey(5),
                     })
            {
                Assert.That(keys, Does.Contain(viewKey));
            }

            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(keys.Select((key, i) => new PresentationSpriteEntry(key, sprites[i])));
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Diagnostics, Is.Empty);

            var wrong = Make("wrong", 7f, 5f, 0.1f, 0.1f, 64f);
            foreach (var key in keys)
            {
                var single = new PresentationSpriteCatalog();
                LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + key);
                single.Configure(new[] { new PresentationSpriteEntry(key, wrong) });
                Assert.That(single.IsValid, Is.False, key + " has no registry row");
            }
        }

        [Test]
        public void TheRealF003SpritesWorkAsTheRetainedFallbackOfAnyKeyWhenThePrimaryIsBroken()
        {
            var wrong = Make("wrong", 7f, 5f, 0.1f, 0.1f, 24f);
            foreach (var path in Directory.GetFiles(SpriteRoot, "*.png").Select(p => p.Replace('\\', '/')))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var key = name.Substring(0, name.Length - 4).ToUpperInvariant();
                var fallback = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var catalog = new PresentationSpriteCatalog();
                LogAssert.Expect(LogType.Warning, "presentation.art.fallback: " + key);
                catalog.Configure(new[] { new PresentationSpriteEntry(key, wrong).WithFallback(fallback) });
                Assert.That(catalog.IsValid, Is.True, key + ": " + catalog.Error);
                Assert.That(catalog.Get(key), Is.SameAs(fallback), key);
            }
        }
    }
}
