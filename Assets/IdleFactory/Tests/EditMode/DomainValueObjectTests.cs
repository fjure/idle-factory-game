using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Domain;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class DomainValueObjectTests
    {
        [Test]
        public void AuthoredIdentifiersNormalizeWhitespaceAndUnicodeWithoutChangingCase()
        {
            var composed = new ResourceId("  d\u00e9v.input  ");
            var decomposed = new ResourceId("de\u0301v.input");

            Assert.That(composed.Value, Is.EqualTo("d\u00e9v.input"));
            Assert.That(decomposed, Is.EqualTo(composed));
            Assert.That(new ResourceId("dev.input"), Is.Not.EqualTo(new ResourceId("DEV.INPUT")));
            Assert.That(new RecipeId(" dev.recipe.basic ").Value, Is.EqualTo("dev.recipe.basic"));
            Assert.That(new BuildingDefinitionId(" dev.machine.basic ").Value, Is.EqualTo("dev.machine.basic"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void AuthoredIdentifiersRejectMissingValues(string value)
        {
            Assert.That(() => new ResourceId(value), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new RecipeId(value), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new BuildingDefinitionId(value), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void AuthoredIdentifiersUseOrdinalEqualityOrderingAndHashing()
        {
            var resource = new ResourceId("dev.input");
            var sameResource = new ResourceId(" dev.input ");
            var differentCase = new ResourceId("DEV.INPUT");
            var quantities = new Dictionary<ResourceId, long> { [resource] = 4 };

            Assert.That(resource, Is.EqualTo(sameResource));
            Assert.That(resource.GetHashCode(), Is.EqualTo(sameResource.GetHashCode()));
            Assert.That(quantities[sameResource], Is.EqualTo(4));
            Assert.That(quantities.ContainsKey(differentCase), Is.False);
            Assert.That(resource.CompareTo(differentCase), Is.GreaterThan(0));
            Assert.That(new RecipeId("dev.recipe"), Is.EqualTo(new RecipeId("dev.recipe")));
            Assert.That(new BuildingDefinitionId("dev.machine"), Is.EqualTo(new BuildingDefinitionId("dev.machine")));
        }

        [Test]
        public void PersistentIdentifiersUseCanonicalNonEmptyUuidText()
        {
            const string uppercase = "8FCB0DC1-46D3-4869-B3EE-6E86B808BC24";

            var building = new BuildingInstanceId("  " + uppercase + "  ");
            var checkpoint = new CheckpointId(uppercase);

            Assert.That(building.Value, Is.EqualTo(uppercase.ToLowerInvariant()));
            Assert.That(checkpoint.Value, Is.EqualTo(uppercase.ToLowerInvariant()));
            Assert.That(new BuildingInstanceId(building.Value), Is.EqualTo(building));
            Assert.That(new CheckpointId(checkpoint.Value), Is.EqualTo(checkpoint));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-uuid")]
        [TestCase("00000000-0000-0000-0000-000000000000")]
        public void PersistentIdentifiersRejectInvalidUuidText(string value)
        {
            Assert.That(() => new BuildingInstanceId(value), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new CheckpointId(value), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void OfflinePolicyRevisionMustBePositive()
        {
            Assert.That(new OfflinePolicyRevision(1).Value, Is.EqualTo(1));
            Assert.That(() => new OfflinePolicyRevision(0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new OfflinePolicyRevision(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void DefaultOfflinePolicyRevisionFailsFastForEveryConsumedApi()
        {
            var invalid = default(OfflinePolicyRevision);
            var valid = new OfflinePolicyRevision(1);

            Assert.That(() => invalid.Value, Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => invalid.CompareTo(valid), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => invalid.Equals(valid), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => valid.Equals(invalid), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => invalid.GetHashCode(), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => invalid.ToString(), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void GridPositionOffsetsCoordinatesWithCheckedArithmetic()
        {
            Assert.That(new GridPosition(-3, 4).Offset(2, -6), Is.EqualTo(new GridPosition(-1, -2)));
            Assert.That(
                () => new GridPosition(int.MaxValue, 0).Offset(1, 0),
                Throws.TypeOf<OverflowException>());
            Assert.That(
                () => new GridPosition(0, int.MinValue).Offset(0, -1),
                Throws.TypeOf<OverflowException>());
        }

        [TestCase(0, 1)]
        [TestCase(1, 0)]
        [TestCase(-1, 1)]
        public void FootprintRequiresPositiveDimensions(int width, int height)
        {
            Assert.That(() => new Footprint(width, height), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void DefaultFootprintFailsFastForEveryConsumedApi()
        {
            var invalid = default(Footprint);
            var valid = new Footprint(1, 1);

            Assert.That(() => invalid.Width, Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => invalid.Height, Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => invalid.Equals(valid), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => valid.Equals(invalid), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => invalid.GetHashCode(), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => invalid.ToString(), Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => invalid.OccupiedCells(new GridPosition(0, 0), 0).ToArray(),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void UnitFootprintsAreValidAtBothInt32CoordinateBoundaries()
        {
            var footprint = new Footprint(1, 1);

            CollectionAssert.AreEqual(
                new[] { new GridPosition(int.MaxValue, int.MaxValue) },
                footprint.OccupiedCells(new GridPosition(int.MaxValue, int.MaxValue), 0).ToArray());
            CollectionAssert.AreEqual(
                new[] { new GridPosition(int.MinValue, int.MinValue) },
                footprint.OccupiedCells(new GridPosition(int.MinValue, int.MinValue), 3).ToArray());
        }

        [Test]
        public void LargerFootprintIsValidAtInt32MinimumBoundary()
        {
            var cells = new Footprint(2, 2)
                .OccupiedCells(new GridPosition(int.MinValue, int.MinValue), 0)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    new GridPosition(int.MinValue, int.MinValue),
                    new GridPosition(int.MinValue + 1, int.MinValue),
                    new GridPosition(int.MinValue, int.MinValue + 1),
                    new GridPosition(int.MinValue + 1, int.MinValue + 1),
                },
                cells);
        }

        [Test]
        public void OccupiedCellsAreDeterministicRowMajorAtZeroAndHalfTurns()
        {
            var footprint = new Footprint(2, 3);
            var expected = new[]
            {
                new GridPosition(-2, -1), new GridPosition(-1, -1),
                new GridPosition(-2, 0), new GridPosition(-1, 0),
                new GridPosition(-2, 1), new GridPosition(-1, 1),
            };

            CollectionAssert.AreEqual(expected, footprint.OccupiedCells(new GridPosition(-2, -1), 0).ToArray());
            CollectionAssert.AreEqual(expected, footprint.OccupiedCells(new GridPosition(-2, -1), 2).ToArray());
        }

        [Test]
        public void OccupiedCellsSwapDimensionsAtQuarterTurns()
        {
            var footprint = new Footprint(2, 3);
            var expected = new[]
            {
                new GridPosition(5, 7), new GridPosition(6, 7), new GridPosition(7, 7),
                new GridPosition(5, 8), new GridPosition(6, 8), new GridPosition(7, 8),
            };

            CollectionAssert.AreEqual(expected, footprint.OccupiedCells(new GridPosition(5, 7), 1).ToArray());
            CollectionAssert.AreEqual(expected, footprint.OccupiedCells(new GridPosition(5, 7), 3).ToArray());
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(5)]
        public void OccupiedCellsRejectUnsupportedOrientations(int orientationQuarterTurns)
        {
            var footprint = new Footprint(2, 2);

            Assert.That(
                () => footprint.OccupiedCells(new GridPosition(0, 0), orientationQuarterTurns).ToArray(),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void OccupiedCellsRejectCoordinateOverflowBeforeReturningAnyCell(int orientationQuarterTurns)
        {
            var footprint = new Footprint(2, 3);

            Assert.That(
                () => footprint.OccupiedCells(
                        new GridPosition(int.MaxValue - 1, int.MaxValue - 1),
                        orientationQuarterTurns)
                    .ToArray(),
                Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void OccupiedCellsRejectIndependentHorizontalAndVerticalOverflow()
        {
            Assert.That(
                () => new Footprint(2, 1)
                    .OccupiedCells(new GridPosition(int.MaxValue, 0), 0)
                    .ToArray(),
                Throws.TypeOf<OverflowException>());
            Assert.That(
                () => new Footprint(1, 2)
                    .OccupiedCells(new GridPosition(0, int.MaxValue), 0)
                    .ToArray(),
                Throws.TypeOf<OverflowException>());
        }
    }
}
