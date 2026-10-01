using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T136 (issue #213): version routing of <see cref="GameplayContentDispatcher"/> and the all-or-nothing
    /// <see cref="RunCatalogBuilder"/> (FR-029, FR-030, US6 scenarios 1-2).
    /// </summary>
    public sealed class GameplayContentDispatcherTests
    {
        private static byte[] V1Bytes() => File.ReadAllBytes(Path.Combine(
            RunContentExample.RepositoryRoot(), "Assets", "IdleFactory", "Content", "Development", "development-catalog.json"));

        private static string Describe(IEnumerable<ContentDiagnostic> diagnostics) =>
            string.Join(" | ", diagnostics.Select(d => d.Code + "@" + d.Path));

        private static string[] Sorted(IEnumerable<string> ids) => ids.OrderBy(i => i, StringComparer.Ordinal).ToArray();

        [Test]
        public void Version2RoutesToTheRunCatalog()
        {
            var result = GameplayContentDispatcher.Load(RunContentExample.Bytes(RunContentExample.Valid()));

            Assert.That(result.IsSuccess, Is.True, Describe(result.Diagnostics));
            Assert.That(result.SchemaVersion, Is.EqualTo(2));
            Assert.That(result.RunCatalog, Is.Not.Null);
            Assert.That(result.LegacyCatalog, Is.Null);
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void Version1RoutesToTheUnchangedLegacyLoader()
        {
            var bytes = V1Bytes();
            var direct = GameplayContentLoader.Load(bytes);
            var result = GameplayContentDispatcher.Load(bytes);

            Assert.That(direct.IsSuccess, Is.True);
            Assert.That(result.IsSuccess, Is.True, Describe(result.Diagnostics));
            Assert.That(result.SchemaVersion, Is.EqualTo(1));
            Assert.That(result.RunCatalog, Is.Null);
            Assert.That(result.LegacyCatalog, Is.Not.Null);
            Assert.That(Sorted(result.LegacyCatalog.Resources.Keys.Select(k => k.Value)), Is.EqualTo(Sorted(direct.Catalog.Resources.Keys.Select(k => k.Value))));
            Assert.That(Sorted(result.LegacyCatalog.Recipes.Keys.Select(k => k.Value)), Is.EqualTo(Sorted(direct.Catalog.Recipes.Keys.Select(k => k.Value))));
            Assert.That(Sorted(result.LegacyCatalog.Buildings.Keys.Select(k => k.Value)), Is.EqualTo(Sorted(direct.Catalog.Buildings.Keys.Select(k => k.Value))));
        }

        [Test]
        public void Version1RejectionKeepsTheLegacyDiagnostics()
        {
            var text = Encoding.UTF8.GetString(V1Bytes()).Replace("\"recipeId\"", "\"recipeIdX\"");
            var bytes = Encoding.UTF8.GetBytes(text);
            var direct = GameplayContentLoader.Load(bytes);
            var result = GameplayContentDispatcher.Load(bytes);

            Assert.That(direct.IsSuccess, Is.False);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.SchemaVersion, Is.EqualTo(1));
            Assert.That(Describe(result.Diagnostics), Is.EqualTo(Describe(direct.Diagnostics)));
        }

        [TestCase("{\"contentSchemaVersion\":3}")]
        [TestCase("{\"contentSchemaVersion\":0}")]
        [TestCase("{\"contentSchemaVersion\":-1}")]
        [TestCase("{\"contentSchemaVersion\":2.0}")]
        [TestCase("{\"contentSchemaVersion\":\"2\"}")]
        [TestCase("{\"contentSchemaVersion\":null}")]
        [TestCase("{\"contentSchemaVersion\":true}")]
        [TestCase("{\"contentSchemaVersion\":99999999999999999999}")]
        public void AnyOtherVersionIsUnsupported(string json)
        {
            var result = GameplayContentDispatcher.Load(Encoding.UTF8.GetBytes(json));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.SchemaVersion, Is.EqualTo(0));
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("content.version.unsupported"));
            Assert.That(result.Diagnostics[0].Path, Is.EqualTo("/contentSchemaVersion"));
        }

        [Test]
        public void MissingVersionIsReported()
        {
            var result = GameplayContentDispatcher.Load(Encoding.UTF8.GetBytes("{\"other\":1}"));

            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("content.version.missing"));
        }

        [Test]
        public void DuplicateVersionIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1,\"x\":[1,{\"a\":2}],\"contentSchemaVersion\":2}";
            var result = GameplayContentDispatcher.Load(Encoding.UTF8.GetBytes(json));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("member.duplicate"));
        }

        [Test]
        public void EnvelopeFailuresAreRejectedByThePeek()
        {
            var bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(RunContentExample.Bytes(RunContentExample.Valid())).ToArray();
            Assert.That(GameplayContentDispatcher.Load(bom).Diagnostics.Single().Code, Is.EqualTo("content.encoding"));
            Assert.That(GameplayContentDispatcher.Load(new byte[] { 0x7B, 0xC3, 0x28, 0x7D }).Diagnostics.Single().Code, Is.EqualTo("content.encoding"));
            Assert.That(GameplayContentDispatcher.Load(null).Diagnostics.Single().Code, Is.EqualTo("content.empty"));
            Assert.That(GameplayContentDispatcher.Load(new byte[0]).Diagnostics.Single().Code, Is.EqualTo("content.empty"));
            Assert.That(GameplayContentDispatcher.Load(new byte[1024 * 1024 + 1]).Diagnostics.Single().Code, Is.EqualTo("content.too-large"));
            Assert.That(GameplayContentDispatcher.Load(Encoding.UTF8.GetBytes("[1]")).Diagnostics.Single().Code, Is.EqualTo("json.malformed"));
            Assert.That(GameplayContentDispatcher.Load(Encoding.UTF8.GetBytes("{\"contentSchemaVersion\":2,\"a\":[")).Diagnostics.Single().Code, Is.EqualTo("json.malformed"));
        }

        [Test]
        public void DiagnosticsNeverCarryDocumentText()
        {
            var secret = "C:\\\\Users\\\\player\\\\secret-file.json";
            var results = new[]
            {
                GameplayContentDispatcher.Load(Encoding.UTF8.GetBytes("{\"contentSchemaVersion\":\"" + secret + "\"}")),
                GameplayContentDispatcher.Load(Encoding.UTF8.GetBytes("{\"contentSchemaVersion\":" + secret + "}")),
                GameplayContentDispatcher.Load(Encoding.UTF8.GetBytes("{\"contentSchemaVersion\":2,\"" + secret + "\":}")),
            };

            foreach (var result in results)
            {
                foreach (var d in result.Diagnostics)
                {
                    Assert.That(d.Message + d.Path, Does.Not.Contain("secret-file"));
                }
            }
        }

        [Test]
        public void ValidationViolationsRejectTheWholeDocumentWithNoCatalog()
        {
            var document = RunContentExample.Valid();
            document["runStart"]["removalRefundPpm"] = 1_000_000;
            var result = GameplayContentDispatcher.Load(RunContentExample.Bytes(document));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.SchemaVersion, Is.EqualTo(2));
            Assert.That(result.RunCatalog, Is.Null);
            Assert.That(result.LegacyCatalog, Is.Null);
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("runstart.refund.out-of-range"));
        }

        [Test]
        public void StructuralFailureOfAVersion2DocumentIsReportedOnce()
        {
            var document = RunContentExample.Valid();
            document.Remove("prestige");
            var result = GameplayContentDispatcher.Load(RunContentExample.Bytes(document));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConstructorFailuresMapToTheStaticInternalCode()
        {
            var read = RunContentReader.Read(RunContentExample.Bytes(RunContentExample.Valid()));
            Assert.That(read.IsSuccess, Is.True);

            var result = RunCatalogBuilder.Build(
                read.Document,
                null,
                _ => throw new InvalidOperationException("C:\\secret-file.json carries document text"));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Catalog, Is.Null);
            var diagnostic = result.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo("content.internal"));
            Assert.That(diagnostic.Message, Does.Not.Contain("secret-file"));
            Assert.That(diagnostic.Path, Is.Empty);
        }

        [Test]
        public void BuiltCatalogReflectsTheDocument()
        {
            var read = RunContentReader.Read(RunContentExample.Bytes(RunContentExample.Valid()));
            var built = RunCatalogBuilder.Build(read.Document);

            Assert.That(built.IsSuccess, Is.True, Describe(built.Diagnostics));
            var catalog = built.Catalog;
            Assert.That(catalog.ContentRevision, Is.EqualTo(read.Document.ContentRevision));
            Assert.That(catalog.Resources.Count, Is.EqualTo(read.Document.Resources.Count));
            Assert.That(catalog.Machines.Count, Is.EqualTo(read.Document.Machines.Count));
            Assert.That(catalog.Items.Count, Is.EqualTo(read.Document.Items.Count));
            Assert.That(catalog.ContractOffers.Count, Is.EqualTo(read.Document.Contracts.Count));
            Assert.That(catalog.ResearchInContentOrder.Select(r => r.Id), Is.EqualTo(read.Document.Research.Select(r => r.Id)));
            Assert.That(catalog.RunStart.StartingCash, Is.EqualTo(read.Document.RunStart.StartingCash));
            Assert.That(catalog.Prestige.Coefficient, Is.EqualTo(read.Document.Prestige.IpCurve.Coefficient));
        }
    }
}
