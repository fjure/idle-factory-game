using System;
using System.IO;
using System.Linq;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T136 (issue #213): the authoring gate for a gameplay content document of either version. Set
    /// <c>IDLEFACTORY_CONTENT_CHECK_PATH</c> to an absolute file path; the document goes through
    /// <see cref="GameplayContentDispatcher"/> (a version-3 document is read, validated and built; a version-1 document uses
    /// the Feature 002 loader). With the variable unset the test passes as a no-op and says so, so a green run is never
    /// mistaken for a checked document.
    /// </summary>
    public sealed class RunContentAuthoringCheckTests
    {
        private const string ContentPathVariable = "IDLEFACTORY_CONTENT_CHECK_PATH";

        [Test]
        public void ValidatesConfiguredDocumentOrReportsNotConfigured()
        {
            var path = Environment.GetEnvironmentVariable(ContentPathVariable);
            if (string.IsNullOrWhiteSpace(path))
            {
                var line = $"Run content authoring check not configured: {ContentPathVariable} is unset (no-op, nothing was validated).";
                TestContext.Progress.WriteLine(line);
                Assert.Pass(line);
            }

            Assert.That(Path.IsPathRooted(path), Is.True, $"{ContentPathVariable} must be an absolute path.");
            Assert.That(File.Exists(path), Is.True, $"{ContentPathVariable} does not name an existing file.");

            var result = GameplayContentDispatcher.Load(File.ReadAllBytes(path));
            foreach (var diagnostic in result.Diagnostics)
            {
                TestContext.Progress.WriteLine($"{diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}");
            }

            Assert.That(
                result.IsSuccess,
                Is.True,
                string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}")));
            Assert.Pass($"Run content authoring check validated {path} as content version {result.SchemaVersion}.");
        }
    }
}
