using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Shared access to the version-3 example document. The source of truth is the complete example in the author
    /// guide (docs/content/json-content-pipeline.md, "v3: a complete, valid, annotated example"), read at test time so
    /// that the reader, the validator and the guide cannot drift apart.
    /// </summary>
    internal static class RunContentExample
    {
        public static string RepositoryRoot() => Directory.GetCurrentDirectory();

        public static string GuideText() =>
            File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "content", "json-content-pipeline.md"));

        public static string GuideJson()
        {
            var text = GuideText();
            var heading = text.IndexOf("## v3: a complete, valid, annotated example", StringComparison.Ordinal);
            Assert.That(heading, Is.GreaterThanOrEqualTo(0), "The guide has no v3 example section.");
            var start = text.IndexOf("```json", heading, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "The v3 example has no json block.");
            start += "```json".Length;
            var end = text.IndexOf("```", start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), "The v3 example json block is not closed.");
            return text.Substring(start, end - start);
        }

        /// <summary>The guide's example, unchanged.</summary>
        public static JObject Guide() => JObject.Parse(GuideJson());

        /// <summary>The guide example, unmodified: it is the valid baseline for every test.</summary>
        public static JObject Valid() => Guide();

        public static byte[] Bytes(JObject document) => Encoding.UTF8.GetBytes(document.ToString(Formatting.None));

        public static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

        /// <summary>Replaces exactly one occurrence in the compact serialization, for raw edits JObject cannot express.</summary>
        public static string ReplaceOnce(string json, string oldText, string newText)
        {
            var first = json.IndexOf(oldText, StringComparison.Ordinal);
            Assert.That(first, Is.GreaterThanOrEqualTo(0), "Text to replace not found: " + oldText);
            Assert.That(json.IndexOf(oldText, first + 1, StringComparison.Ordinal), Is.LessThan(0), "Text to replace is not unique: " + oldText);
            return json.Substring(0, first) + newText + json.Substring(first + oldText.Length);
        }
    }
}
