using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T513 (Feature 006, research R06, FR-014): the world sorting layers exist in the pinned order. Assertions use
    /// <see cref="SortingLayer.layers"/> names and positions, plus <see cref="SortingLayer.IsValid"/> and
    /// <see cref="SortingLayer.GetLayerValueFromName"/>. <see cref="SortingLayer.NameToID"/> is deliberately not used as an
    /// existence check: it returns 0 for an unknown name, and 0 is also the id of Default.
    /// </summary>
    public sealed class SortingLayerGuardTests
    {
        private static readonly string[] ExpectedOrder =
        {
            "WorldBase", "WorldDeck", "WorldLinks", "Default", "Overlay", "Effects",
        };

        private static string[] ActualNames() => SortingLayer.layers.Select(l => l.name).ToArray();

        [Test]
        public void SortingLayers_AreExactlyTheExpectedSetInOrder()
        {
            CollectionAssert.AreEqual(ExpectedOrder, ActualNames(),
                "TagManager sorting layers must be WorldBase, WorldDeck, WorldLinks, Default, Overlay, Effects (R06).");
        }

        [TestCaseSource(nameof(ExpectedOrder))]
        public void EachLayer_IsValidAndSitsAtItsOrderedPosition(string name)
        {
            int id = SortingLayer.NameToID(name);
            Assert.That(SortingLayer.IsValid(id), Is.True, name + " must resolve to a valid layer id.");
            Assert.That(SortingLayer.IDToName(id), Is.EqualTo(name), name + " id must map back to its own name.");
            Assert.That(SortingLayer.GetLayerValueFromName(name), Is.EqualTo(System.Array.IndexOf(ExpectedOrder, name) - System.Array.IndexOf(ExpectedOrder, "Default")),
                name + " must keep its ordered position (layer values are relative to Default, which is 0).");
        }

        [Test]
        public void ExistingLayers_KeepTheirUniqueIds()
        {
            Assert.That(SortingLayer.NameToID("Default"), Is.EqualTo(0), "Default's id must stay 0.");
            Assert.That(SortingLayer.NameToID("Overlay"), Is.EqualTo(1332175961), "Overlay's uniqueID must not change.");
        }

        [Test]
        public void ZeroIdIsAmbiguous_UnknownNamesMustBeDetectedByIsValidOrNameNotNameToId()
        {
            const string unknown = "NoSuchSortingLayer";
            Assert.That(SortingLayer.NameToID(unknown), Is.EqualTo(0), "Unity returns 0 for unknown names (same as Default).");
            Assert.That(ActualNames(), Does.Not.Contain(unknown));
            Assert.That(SortingLayer.IsValid(SortingLayer.NameToID(unknown)), Is.True,
                "The id 0 reads as valid, so NameToID != 0 and IsValid(NameToID) cannot prove existence; assert names.");
        }
    }
}
