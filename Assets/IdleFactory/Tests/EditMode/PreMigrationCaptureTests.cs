using System.Linq;
using IdleFactory.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Unity.Content;
using NUnit.Framework;
using UnityEditor;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// One-time reviewable verification (R09, T070) that the hand-written
    /// <see cref="AcceptedFoundationCatalogFixture"/> equals, field by field, the catalog
    /// <see cref="DefinitionCatalogBuilder"/> produces today from the still-live
    /// ScriptableObject development content. This proves the fixture is an accurate
    /// pre-migration capture before the JSON content authoring migration (002) replaces
    /// the ScriptableObject path.
    ///
    /// This test is deleted by T079 once the ScriptableObject path is retired; it is not
    /// meant to remain as regression coverage past that point.
    /// </summary>
    public sealed class PreMigrationCaptureTests
    {
        private const string CatalogPath = "Assets/IdleFactory/Content/Development/DevelopmentCatalog.asset";

        [Test]
        public void AcceptedFixtureMatchesLiveScriptableObjectCatalogFieldByField()
        {
            var catalogAsset = AssetDatabase.LoadAssetAtPath<DefinitionCatalogAsset>(CatalogPath);
            Assert.That(catalogAsset, Is.Not.Null, $"Expected a DefinitionCatalogAsset at {CatalogPath}");

            var actual = DefinitionCatalogBuilder.Build(catalogAsset);
            var expected = AcceptedFoundationCatalogFixture.CreateCatalog();

            Assert.That(actual.Resources.Count, Is.EqualTo(expected.Resources.Count));
            Assert.That(actual.Recipes.Count, Is.EqualTo(expected.Recipes.Count));
            Assert.That(actual.Buildings.Count, Is.EqualTo(expected.Buildings.Count));
            Assert.That(actual.OfflinePolicies.Count, Is.EqualTo(expected.OfflinePolicies.Count));

            AssertResourcesEqual(
                actual.Resolve(AcceptedFoundationCatalogFixture.Input),
                expected.Resolve(AcceptedFoundationCatalogFixture.Input));
            AssertResourcesEqual(
                actual.Resolve(AcceptedFoundationCatalogFixture.Output),
                expected.Resolve(AcceptedFoundationCatalogFixture.Output));

            AssertRecipesEqual(
                actual.Resolve(AcceptedFoundationCatalogFixture.Recipe),
                expected.Resolve(AcceptedFoundationCatalogFixture.Recipe));

            AssertBuildingsEqual(
                actual.Resolve(AcceptedFoundationCatalogFixture.Building),
                expected.Resolve(AcceptedFoundationCatalogFixture.Building));

            AssertPoliciesEqual(
                actual.Resolve(AcceptedFoundationCatalogFixture.OfflineRevision),
                expected.Resolve(AcceptedFoundationCatalogFixture.OfflineRevision));
        }

        private static void AssertResourcesEqual(ResourceDefinition actual, ResourceDefinition expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.DevelopmentLabel, Is.EqualTo(expected.DevelopmentLabel));
            Assert.That(actual.PresentationKey, Is.EqualTo(expected.PresentationKey));
            Assert.That(actual.IsDevelopmentOnly, Is.EqualTo(expected.IsDevelopmentOnly));
        }

        private static void AssertRecipesEqual(RecipeDefinition actual, RecipeDefinition expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.DurationTicks, Is.EqualTo(expected.DurationTicks));
            Assert.That(actual.IsDevelopmentOnly, Is.EqualTo(expected.IsDevelopmentOnly));

            Assert.That(actual.Inputs, Has.Count.EqualTo(expected.Inputs.Count));
            AssertAmountsEqual(actual.Inputs.Single(), expected.Inputs.Single());

            Assert.That(actual.Outputs, Has.Count.EqualTo(expected.Outputs.Count));
            AssertAmountsEqual(actual.Outputs.Single(), expected.Outputs.Single());
        }

        private static void AssertAmountsEqual(ResourceAmountDefinition actual, ResourceAmountDefinition expected)
        {
            Assert.That(actual.ResourceId, Is.EqualTo(expected.ResourceId));
            Assert.That(actual.Amount, Is.EqualTo(expected.Amount));
        }

        private static void AssertBuildingsEqual(BuildingDefinition actual, BuildingDefinition expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.Footprint, Is.EqualTo(expected.Footprint));
            Assert.That(actual.AllowedOrientations, Is.EqualTo(expected.AllowedOrientations));
            Assert.That(actual.RecipeId, Is.EqualTo(expected.RecipeId));
            Assert.That(actual.PresentationKey, Is.EqualTo(expected.PresentationKey));
            Assert.That(actual.IsDevelopmentOnly, Is.EqualTo(expected.IsDevelopmentOnly));

            Assert.That(actual.InputBuffer, Is.Not.Null);
            Assert.That(expected.InputBuffer, Is.Not.Null);
            Assert.That(actual.InputBuffer.Role, Is.EqualTo(expected.InputBuffer.Role));
            Assert.That(actual.InputBuffer.Capacities, Has.Count.EqualTo(expected.InputBuffer.Capacities.Count));
            Assert.That(
                actual.InputBuffer.Capacities[AcceptedFoundationCatalogFixture.Input],
                Is.EqualTo(expected.InputBuffer.Capacities[AcceptedFoundationCatalogFixture.Input]));

            Assert.That(actual.OutputBuffer, Is.Not.Null);
            Assert.That(expected.OutputBuffer, Is.Not.Null);
            Assert.That(actual.OutputBuffer.Role, Is.EqualTo(expected.OutputBuffer.Role));
            Assert.That(actual.OutputBuffer.Capacities, Has.Count.EqualTo(expected.OutputBuffer.Capacities.Count));
            Assert.That(
                actual.OutputBuffer.Capacities[AcceptedFoundationCatalogFixture.Output],
                Is.EqualTo(expected.OutputBuffer.Capacities[AcceptedFoundationCatalogFixture.Output]));
        }

        private static void AssertPoliciesEqual(OfflinePolicyDefinition actual, OfflinePolicyDefinition expected)
        {
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.EfficiencyNumerator, Is.EqualTo(expected.EfficiencyNumerator));
            Assert.That(actual.EfficiencyDenominator, Is.EqualTo(expected.EfficiencyDenominator));
            Assert.That(actual.MaxCreditedTicks, Is.EqualTo(expected.MaxCreditedTicks));
        }
    }
}
