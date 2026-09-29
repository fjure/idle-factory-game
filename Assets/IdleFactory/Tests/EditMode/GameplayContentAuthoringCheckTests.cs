using System;
using System.IO;
using System.Linq;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class GameplayContentAuthoringCheckTests
    {
        private const string ContentPathVariable = "IDLEFACTORY_CONTENT_CHECK_PATH";

        [Test]
        public void ValidatesConfiguredDocumentOrSucceedsAsNoOpWhenUnset()
        {
            var path = Environment.GetEnvironmentVariable(ContentPathVariable);
            if (string.IsNullOrWhiteSpace(path))
            {
                Assert.Pass(
                    $"Authoring check not configured: {ContentPathVariable} is unset (no-op, nothing was validated)");
            }

            Assert.That(Path.IsPathRooted(path), Is.True, $"{ContentPathVariable} must be an absolute path.");
            Assert.That(File.Exists(path), Is.True, $"{ContentPathVariable} does not name an existing file.");

            var result = GameplayContentLoader.Load(File.ReadAllBytes(path));
            foreach (var diagnostic in result.Diagnostics)
            {
                TestContext.Progress.WriteLine(
                    $"{diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}");
            }

            Assert.That(
                result.IsSuccess,
                Is.True,
                string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}")));

            Assert.Pass($"Authoring check validated {path}");
        }
    }
}
