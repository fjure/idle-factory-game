using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class GameplayContentSchemaConsistencyTests
    {
        [Test]
        public void SchemaRequiredMembersMatchReaderAndDocumentedProperties()
        {
            var schema = ReadSchema();
            var definitions = Object(schema["$defs"]);
            var guide = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "content", "json-content-pipeline.md"));

            AssertPropertyNames(schema["properties"], "contentSchemaVersion", "resources", "recipes", "buildings", "offlinePolicies");
            AssertPropertyNames(Object(definitions["resourceAmount"])["properties"], "resourceId", "amount");
            AssertPropertyNames(Object(definitions["resourceCapacity"])["properties"], "resourceId", "capacity");
            AssertPropertyNames(Object(definitions["resource"])["properties"], "id", "developmentLabel", "presentationKey", "isDevelopmentOnly");
            AssertPropertyNames(Object(definitions["recipe"])["properties"], "id", "inputs", "outputs", "durationTicks", "isDevelopmentOnly");
            AssertPropertyNames(
                Object(definitions["building"])["properties"],
                "id", "footprintWidth", "footprintHeight", "allowedOrientations",
                "recipeId", "inputBuffer", "outputBuffer", "presentationKey", "isDevelopmentOnly");
            AssertPropertyNames(
                Object(definitions["offlinePolicy"])["properties"],
                "revision", "efficiencyNumerator", "efficiencyDenominator", "maxCreditedTicks");
            Assert.That(Object(schema)["additionalProperties"], Is.EqualTo(false));
            foreach (var objectDefinition in new[] { "resourceAmount", "resourceCapacity", "resource", "recipe", "building", "offlinePolicy" })
            {
                Assert.That(Object(definitions[objectDefinition])["additionalProperties"], Is.EqualTo(false), objectDefinition);
            }

            AssertReaderMembers("RequiredDocumentMembers", schema["required"]);
            AssertReaderMembers("RequiredResourceMembers", Object(definitions["resource"])["required"]);
            AssertReaderMembers("RequiredRecipeMembers", Object(definitions["recipe"])["required"]);
            AssertReaderMembers("RequiredAmountMembers", Object(definitions["resourceAmount"])["required"]);
            AssertReaderMembers("RequiredBuildingMembers", Object(definitions["building"])["required"]);
            AssertReaderMembers("RequiredCapacityMembers", Object(definitions["resourceCapacity"])["required"]);
            AssertReaderMembers("RequiredPolicyMembers", Object(definitions["offlinePolicy"])["required"]);

            AssertPropertiesDocumented(Object(schema["properties"]), guide);
            foreach (var definition in definitions.Values.Select(Object))
            {
                if (definition.ContainsKey("properties"))
                {
                    AssertPropertiesDocumented(Object(definition["properties"]), guide);
                }
            }

            var resource = Object(definitions["resource"]);
            var resourceProperties = Object(resource["properties"]);
            Assert.That((string)Object(resourceProperties["presentationKey"])["$ref"], Is.EqualTo("#/$defs/presentationKey"));
            Assert.That(Strings(resource["required"]), Does.Not.Contain("presentationKey"));

            var building = Object(definitions["building"]);
            Assert.That(Strings(building["required"]), Does.Contain("presentationKey"));

            foreach (var diagnosticCode in DiagnosticCodes)
            {
                Assert.That(guide, Does.Contain(diagnosticCode), $"Author guide is missing diagnostic code '{diagnosticCode}'.");
            }
        }

        [Test]
        public void SchemaLimitsAndNumericBoundsMatchRuntimeConstants()
        {
            var schema = ReadSchema();
            var properties = Object(schema["properties"]);
            var definitions = Object(schema["$defs"]);

            Assert.That(Number(Object(properties["resources"])["minItems"]), Is.EqualTo(1));
            Assert.That(Number(Object(properties["resources"])["maxItems"]), Is.EqualTo(GameplayContentLimits.MaximumTopLevelEntryCount));
            Assert.That(Number(Object(properties["recipes"])["minItems"]), Is.EqualTo(1));
            Assert.That(Number(Object(properties["recipes"])["maxItems"]), Is.EqualTo(GameplayContentLimits.MaximumTopLevelEntryCount));
            Assert.That(Number(Object(properties["buildings"])["minItems"]), Is.EqualTo(1));
            Assert.That(Number(Object(properties["buildings"])["maxItems"]), Is.EqualTo(GameplayContentLimits.MaximumTopLevelEntryCount));
            Assert.That(Number(Object(properties["offlinePolicies"])["minItems"]), Is.EqualTo(1));
            Assert.That(Number(Object(properties["offlinePolicies"])["maxItems"]), Is.EqualTo(GameplayContentLimits.MaximumTopLevelEntryCount));
            Assert.That(Number(Object(definitions["id"])["maxLength"]), Is.EqualTo(GameplayContentLimits.MaximumIdentifierLength));
            Assert.That(Number(Object(definitions["id"])["minLength"]), Is.EqualTo(1));
            Assert.That(Number(Object(definitions["label"])["maxLength"]), Is.EqualTo(GameplayContentLimits.MaximumIdentifierLength));
            Assert.That(Number(Object(definitions["label"])["minLength"]), Is.EqualTo(1));
            Assert.That(Number(Object(definitions["presentationKey"])["maxLength"]), Is.EqualTo(GameplayContentLimits.MaximumIdentifierLength));
            Assert.That(Number(Object(definitions["presentationKey"])["minLength"]), Is.EqualTo(1));

            var amount = Object(Object(definitions["resourceAmount"])["properties"]);
            Assert.That(Number(Object(amount["amount"])["minimum"]), Is.EqualTo(1));
            Assert.That(Number(Object(amount["amount"])["maximum"]), Is.EqualTo(long.MaxValue));
            var capacity = Object(Object(definitions["resourceCapacity"])["properties"]);
            Assert.That(Number(Object(capacity["capacity"])["minimum"]), Is.EqualTo(0));
            Assert.That(Number(Object(capacity["capacity"])["maximum"]), Is.EqualTo(long.MaxValue));

            var recipe = Object(Object(definitions["recipe"])["properties"]);
            Assert.That(Number(Object(recipe["inputs"])["minItems"]), Is.EqualTo(1));
            Assert.That(Number(Object(recipe["inputs"])["maxItems"]), Is.EqualTo(GameplayContentLimits.MaximumNestedEntryCount));
            Assert.That(Number(Object(recipe["outputs"])["minItems"]), Is.EqualTo(1));
            Assert.That(Number(Object(recipe["outputs"])["maxItems"]), Is.EqualTo(GameplayContentLimits.MaximumNestedEntryCount));
            Assert.That(Number(Object(recipe["durationTicks"])["minimum"]), Is.EqualTo(1));
            Assert.That(Number(Object(recipe["durationTicks"])["maximum"]), Is.EqualTo(long.MaxValue));
            var building = Object(Object(definitions["building"])["properties"]);
            Assert.That(Number(Object(building["inputBuffer"])["minItems"]), Is.EqualTo(1));
            Assert.That(Number(Object(building["inputBuffer"])["maxItems"]), Is.EqualTo(GameplayContentLimits.MaximumNestedEntryCount));
            Assert.That(Number(Object(building["outputBuffer"])["minItems"]), Is.EqualTo(1));
            Assert.That(Number(Object(building["outputBuffer"])["maxItems"]), Is.EqualTo(GameplayContentLimits.MaximumNestedEntryCount));
            Assert.That(Number(Object(building["allowedOrientations"])["minItems"]), Is.EqualTo(1));
            Assert.That(Number(Object(building["allowedOrientations"])["maxItems"]), Is.EqualTo(4));
            Assert.That(Object(building["allowedOrientations"])["uniqueItems"], Is.EqualTo(true));
            Assert.That(Number(Object(Object(building["allowedOrientations"])["items"])["minimum"]), Is.EqualTo(0));
            Assert.That(Number(Object(Object(building["allowedOrientations"])["items"])["maximum"]), Is.EqualTo(3));
            Assert.That(Number(Object(building["footprintWidth"])["minimum"]), Is.EqualTo(1));
            Assert.That(Number(Object(building["footprintWidth"])["maximum"]), Is.EqualTo(int.MaxValue));
            Assert.That(Number(Object(building["footprintHeight"])["minimum"]), Is.EqualTo(1));
            Assert.That(Number(Object(building["footprintHeight"])["maximum"]), Is.EqualTo(int.MaxValue));

            var policy = Object(Object(definitions["offlinePolicy"])["properties"]);
            Assert.That(Number(Object(policy["revision"])["minimum"]), Is.EqualTo(1));
            Assert.That(Number(Object(policy["revision"])["maximum"]), Is.EqualTo(int.MaxValue));
            Assert.That(Number(Object(policy["efficiencyNumerator"])["minimum"]), Is.EqualTo(1));
            Assert.That(Number(Object(policy["efficiencyNumerator"])["maximum"]), Is.EqualTo(long.MaxValue));
            Assert.That(Number(Object(policy["efficiencyDenominator"])["minimum"]), Is.EqualTo(1));
            Assert.That(Number(Object(policy["efficiencyDenominator"])["maximum"]), Is.EqualTo(long.MaxValue));
            Assert.That(Number(Object(policy["maxCreditedTicks"])["minimum"]), Is.EqualTo(1));
            Assert.That(Number(Object(policy["maxCreditedTicks"])["maximum"]), Is.EqualTo(long.MaxValue));
            Assert.That(Number(Object(properties["contentSchemaVersion"])["const"]), Is.EqualTo(1));

            var guide = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "content", "json-content-pipeline.md"));
            Assert.That(guide, Does.Contain("up to 100"));
            Assert.That(guide, Does.Contain("truncation note"));
        }

        private static readonly string[] DiagnosticCodes =
        {
            "id.empty", "id.too-long", "id.not-canonical", "id.duplicate",
            "ref.resource.unknown", "ref.recipe.unknown",
            "recipe.amount.non-positive", "recipe.duration.non-positive", "recipe.resource.duplicate",
            "recipe.inputs.empty", "recipe.outputs.empty",
            "footprint.non-positive", "orientation.invalid", "orientation.duplicate", "orientation.empty",
            "buffer.capacity.negative", "buffer.resource.duplicate", "buffer.empty",
            "buffer.missing-recipe-resource", "building.producing-requires-buffers",
            "policy.revision.non-positive", "policy.revision.duplicate", "policy.ratio.invalid",
            "policy.cap.non-positive", "policy.revision-1.required", "content.internal",
        };

        private static IDictionary<string, object> ReadSchema() =>
            Object(new JsonObjectParser(File.ReadAllText(Path.Combine(
                RepositoryRoot(),
                "Assets",
                "IdleFactory",
                "Content",
                "Schemas",
                "gameplay-content-v1.schema.json"))).Parse());

        private static string RepositoryRoot() => Directory.GetCurrentDirectory();

        private static void AssertReaderMembers(string fieldName, object schemaArray)
        {
            var field = typeof(GameplayContentReader).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, $"Reader member array '{fieldName}' was not found.");
            var runtime = (string[])field.GetValue(null);
            CollectionAssert.AreEqual(runtime, Strings(schemaArray), $"Schema list differs from reader constant '{fieldName}'.");
        }

        private static void AssertPropertyNames(object properties, params string[] expected)
        {
            CollectionAssert.AreEquivalent(expected, Object(properties).Keys);
        }

        private static void AssertPropertiesDocumented(IDictionary<string, object> properties, string guide)
        {
            foreach (var name in properties.Keys)
            {
                Assert.That(
                    Regex.IsMatch(guide, @"\b" + Regex.Escape(name) + @"\b"),
                    Is.True,
                    $"Author guide does not document schema property '{name}'.");
            }
        }

        private static IDictionary<string, object> Object(object value) =>
            (IDictionary<string, object>)value;

        private static IList<object> Array(object value) => (IList<object>)value;

        private static string[] Strings(object value) => Array(value).Cast<string>().ToArray();

        private static long Number(object value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);

        /// <summary>
        /// Tiny test-only JSON reader for the schema. It reads generic JSON data without
        /// adding a runtime dependency or validating documents against the schema.
        /// </summary>
        private sealed class JsonObjectParser
        {
            private readonly string text;
            private int position;

            public JsonObjectParser(string text)
            {
                this.text = text ?? throw new ArgumentNullException(nameof(text));
            }

            public object Parse()
            {
                var value = ReadValue();
                SkipWhitespace();
                if (position != text.Length)
                {
                    throw new FormatException("Unexpected content after the schema JSON value.");
                }

                return value;
            }

            private object ReadValue()
            {
                SkipWhitespace();
                if (position >= text.Length)
                {
                    throw new FormatException("Unexpected end of schema JSON.");
                }

                switch (text[position])
                {
                    case '{':
                        return ReadObject();
                    case '[':
                        return ReadArray();
                    case '"':
                        return ReadString();
                    case 't':
                        ReadLiteral("true");
                        return true;
                    case 'f':
                        ReadLiteral("false");
                        return false;
                    case 'n':
                        ReadLiteral("null");
                        return null;
                    default:
                        return ReadNumber();
                }
            }

            private IDictionary<string, object> ReadObject()
            {
                Require('{');
                SkipWhitespace();
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                if (TryConsume('}'))
                {
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    var name = ReadString();
                    SkipWhitespace();
                    Require(':');
                    if (!result.TryAdd(name, ReadValue()))
                    {
                        throw new FormatException($"Duplicate schema member '{name}'.");
                    }

                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    Require(',');
                }
            }

            private IList<object> ReadArray()
            {
                Require('[');
                SkipWhitespace();
                var result = new List<object>();
                if (TryConsume(']'))
                {
                    return result;
                }

                while (true)
                {
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    Require(',');
                }
            }

            private string ReadString()
            {
                Require('"');
                var start = position;
                while (position < text.Length)
                {
                    var character = text[position++];
                    if (character == '\\')
                    {
                        if (position >= text.Length)
                        {
                            throw new FormatException("Incomplete escape in schema JSON string.");
                        }

                        position++;
                    }
                    else if (character == '"')
                    {
                        return text.Substring(start, position - start - 1);
                    }
                    else if (character < 0x20)
                    {
                        throw new FormatException("Unescaped control character in schema JSON string.");
                    }
                }

                throw new FormatException("Unterminated schema JSON string.");
            }

            private object ReadNumber()
            {
                var start = position;
                while (position < text.Length && "-+0123456789.eE".IndexOf(text[position]) >= 0)
                {
                    position++;
                }

                if (start == position)
                {
                    throw new FormatException($"Expected a JSON value at offset {position}.");
                }

                var number = text.Substring(start, position - start);
                if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    return integer;
                }

                if (decimal.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalNumber))
                {
                    return decimalNumber;
                }

                throw new FormatException($"Invalid schema JSON number '{number}'.");
            }

            private void ReadLiteral(string expected)
            {
                if (position + expected.Length > text.Length ||
                    !string.Equals(text.Substring(position, expected.Length), expected, StringComparison.Ordinal))
                {
                    throw new FormatException($"Expected '{expected}' at offset {position}.");
                }

                position += expected.Length;
            }

            private void SkipWhitespace()
            {
                while (position < text.Length && char.IsWhiteSpace(text[position]))
                {
                    position++;
                }
            }

            private void Require(char expected)
            {
                SkipWhitespace();
                if (!TryConsume(expected))
                {
                    throw new FormatException($"Expected '{expected}' at offset {position}.");
                }
            }

            private bool TryConsume(char expected)
            {
                if (position >= text.Length || text[position] != expected)
                {
                    return false;
                }

                position++;
                return true;
            }
        }
    }
}
