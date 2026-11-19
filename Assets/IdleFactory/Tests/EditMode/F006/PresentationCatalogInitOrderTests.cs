using System;
using System.IO;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T531 carry C-6 (Architect 3, p3b-followups): the registry initializer of <see cref="PresentationSpriteCatalog"/> reads <c>PlacedF006Keys</c> and
    /// <c>PlacedF006LinkKeys</c>, and C# runs static field initializers in declaration order. A reorder that put <c>Registry</c> first would read a null array and
    /// either throw a type initializer exception or silently leave the placed rows optional. The first test proves the outcome (every placed row is required), the
    /// second the cause (the declaration order in the source), so a reorder fails with a message that names the rule.
    /// </summary>
    public sealed class PresentationCatalogInitOrderTests
    {
        private const string SourcePath = "Assets/IdleFactory/Unity/Presentation/PresentationSpriteCatalog.cs";

        [Test]
        public void EveryPlacedF006RowIsRegisteredAndRequired()
        {
            foreach (var key in PresentationSpriteCatalog.PlacedF006Keys)
            {
                Assert.That(PresentationSpriteCatalog.TryGetRegistryRequirement(key, out var row), Is.True, key);
                Assert.That(row.Required, Is.True, key + " is placed by the deck, so its row is required");
            }

            foreach (var key in PresentationSpriteCatalog.PlacedF006LinkKeys)
            {
                Assert.That(PresentationSpriteCatalog.TryGetRegistryRequirement(key, out var row), Is.True, key);
                Assert.That(row.Required, Is.True, key + " is placed by the link, depot or deposit view, so its row is required");
            }
        }

        [Test]
        public void ThePlacedKeyArraysAreDeclaredBeforeTheRegistry()
        {
            var source = File.ReadAllText(SourcePath);
            var registry = source.IndexOf("Registry = BuildRegistry()", StringComparison.Ordinal);
            var placed = source.IndexOf("string[] PlacedF006Keys =", StringComparison.Ordinal);
            var link = source.IndexOf("string[] PlacedF006LinkKeys =", StringComparison.Ordinal);
            Assert.That(registry, Is.GreaterThan(0), "the Registry field is declared with its BuildRegistry() initializer");
            Assert.That(placed, Is.GreaterThan(0).And.LessThan(registry), "PlacedF006Keys must be declared before Registry (static initializer order, T531 C-6)");
            Assert.That(link, Is.GreaterThan(0).And.LessThan(registry), "PlacedF006LinkKeys must be declared before Registry (static initializer order, T531 C-6)");
        }
    }
}
