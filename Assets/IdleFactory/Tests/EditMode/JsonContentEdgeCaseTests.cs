using System;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T085: JSON content edge cases exercised through the real <see cref="GameplayContentLoader"/> path.
    /// </summary>
    public sealed class JsonContentEdgeCaseTests
    {
        private const string PackagedContentRelativeToAssets = "IdleFactory/Content/Development/development-catalog.json";
        private const string DepartureFixtureRelativeToAssets =
            "IdleFactory/Tests/EditMode/Fixtures/SaveV1/05-checkpoint-departure-offline-return.json";

        private static readonly DateTimeOffset DepartureUtc =
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private static string Document(string resources, string durationTicks) =>
            "{\"contentSchemaVersion\":1," +
            "\"resources\":[" + resources + "]," +
            "\"recipes\":[{\"id\":\"r.one\"," +
            "\"inputs\":[{\"resourceId\":\"dev.input\",\"amount\":1}]," +
            "\"outputs\":[{\"resourceId\":\"Dev.Input\",\"amount\":1}]," +
            "\"durationTicks\":" + durationTicks + ",\"isDevelopmentOnly\":false}]," +
            "\"buildings\":[{\"id\":\"b.one\",\"footprintWidth\":1,\"footprintHeight\":1,\"allowedOrientations\":[0]," +
            "\"recipeId\":\"r.one\",\"inputBuffer\":[{\"resourceId\":\"dev.input\",\"capacity\":4}]," +
            "\"outputBuffer\":[{\"resourceId\":\"Dev.Input\",\"capacity\":1}]," +
            "\"presentationKey\":\"K-B\",\"isDevelopmentOnly\":false}]," +
            "\"offlinePolicies\":[{\"revision\":1,\"efficiencyNumerator\":4,\"efficiencyDenominator\":5,\"maxCreditedTicks\":288000000000}]}";

        private const string CaseDistinctResources =
            "{\"id\":\"dev.input\",\"developmentLabel\":\"lower\",\"presentationKey\":\"K-LOWER\",\"isDevelopmentOnly\":false}," +
            "{\"id\":\"Dev.Input\",\"developmentLabel\":\"upper\",\"presentationKey\":\"K-UPPER\",\"isDevelopmentOnly\":false}";

        [Test]
        public void IdsDifferingOnlyByLetterCaseAreDistinctAndResolveSeparately()
        {
            var loaded = GameplayContentLoader.Load(Encoding.UTF8.GetBytes(Document(CaseDistinctResources, "1")));

            Assert.That(loaded.IsSuccess, Is.True,
                string.Join("; ", loaded.Diagnostics.Select(d => d.Code + " " + d.Path)));
            var lower = new ResourceId("dev.input");
            var upper = new ResourceId("Dev.Input");
            Assert.That(lower, Is.Not.EqualTo(upper));
            Assert.That(loaded.Catalog.Resources.Count, Is.EqualTo(2));
            Assert.That(loaded.Catalog.Resources.ContainsKey(lower), Is.True);
            Assert.That(loaded.Catalog.Resources.ContainsKey(upper), Is.True);
            Assert.That(loaded.Catalog.Resolve(lower), Is.Not.SameAs(loaded.Catalog.Resolve(upper)));
        }

        [Test]
        public void DurationTicksAboveInt64MaxIsRejectedAsOutOfRangeAtItsPointer()
        {
            var loaded = GameplayContentLoader.Load(
                Encoding.UTF8.GetBytes(Document(CaseDistinctResources, "9223372036854775808")));

            Assert.That(loaded.IsSuccess, Is.False);
            var diagnostic = loaded.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo("value.out-of-range"));
            Assert.That(diagnostic.Path, Is.EqualTo("/recipes/0/durationTicks"));
        }

        [Test]
        public void OfflineReturnLongerThanEightHoursIsClippedOnJsonBuiltCatalog()
        {
            var checkpoint = LoadDepartureCheckpoint();

            var result = new OfflineReconciler().Reconcile(
                checkpoint, DepartureUtc.AddHours(10), new CheckpointId(Guid.NewGuid()));

            Assert.That(result.Status, Is.EqualTo(OfflineReconciliationStatus.CandidateReady));
            Assert.That(result.Summary.MeasuredTicks, Is.EqualTo(360000000000L));
            Assert.That(result.Summary.CreditedTicks, Is.EqualTo(288000000000L));
            Assert.That(result.Summary.EffectiveTicks, Is.EqualTo(230400000000L));
        }

        [TestCase(1L, 0L)]
        [TestCase(3L, 2L)]
        [TestCase(7L, 5L)]
        public void OfflineEffectiveTicksAreFlooredWhenFourFifthsIsNotAnInteger(long absenceTicks, long expectedEffective)
        {
            var checkpoint = LoadDepartureCheckpoint();

            var result = new OfflineReconciler().Reconcile(
                checkpoint, DepartureUtc.AddTicks(absenceTicks), new CheckpointId(Guid.NewGuid()));

            Assert.That(result.Status, Is.EqualTo(OfflineReconciliationStatus.CandidateReady));
            Assert.That(result.Summary.CreditedTicks, Is.EqualTo(absenceTicks));
            Assert.That(result.Summary.EffectiveTicks, Is.EqualTo(expectedEffective));
        }

        private static Checkpoint LoadDepartureCheckpoint()
        {
            var catalogLoad = GameplayContentLoader.Load(
                File.ReadAllBytes(Path.Combine(Application.dataPath, PackagedContentRelativeToAssets)));
            Assert.That(catalogLoad.IsSuccess, Is.True);
            var bytes = File.ReadAllBytes(Path.Combine(Application.dataPath, DepartureFixtureRelativeToAssets));
            var read = SaveV1Mapper.Deserialize(bytes, catalogLoad.Catalog);
            Assert.That(read.Succeeded, Is.True, read.Message);
            return read.Checkpoint;
        }
    }
}
