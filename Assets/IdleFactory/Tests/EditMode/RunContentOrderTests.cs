using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using IdleFactory.Run.Content;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T136 (issue #213, FR-029/FR-030, plan.md determinism case b): JSON member and entry permutations of a version-3
    /// document give the identical <see cref="RunCatalog"/>. Lists whose order is part of the meaning (the level tables,
    /// speed milestones, prestige milestones and the sell steps) are never permuted. The one ordered view of a
    /// permutable family, <see cref="RunCatalog.ResearchInContentOrder"/> (OD-8, the authors' order), is checked
    /// separately: it follows the document order and is excluded from the permutation-invariant dump. Lists without
    /// semantic order (recipe inputs and outputs, research prerequisites, allowedMachineIds, initialUnlocks) are compared as
    /// sets; order-meaningful lists (level tables, milestones, sell steps) are compared in order. The
    /// builder does not canonicalize them in Map.
    /// </summary>
    public sealed class RunContentOrderTests
    {
        private static readonly HashSet<string> OrderedMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "storageLevels", "factoryLevels", "milestones", "sellSteps",
        };

        private static string BuildDump(JObject document)
        {
            var result = GameplayContentDispatcher.Load(RunContentExample.Bytes(document));
            Assert.That(
                result.IsSuccess,
                Is.True,
                string.Join(" | ", result.Diagnostics.Select(d => d.Code + "@" + d.Path)));
            return Dump(result.RunCatalog, null);
        }

        [Test]
        public void MemberPermutationGivesTheIdenticalCatalog()
        {
            var baseline = BuildDump(RunContentExample.Valid());
            var permuted = BuildDump(Permute(RunContentExample.Valid(), false));

            Assert.That(permuted, Is.EqualTo(baseline));
        }

        [Test]
        public void EntryPermutationGivesTheIdenticalCatalog()
        {
            var baseline = BuildDump(RunContentExample.Valid());
            var permuted = BuildDump(Permute(RunContentExample.Valid(), true));

            Assert.That(permuted, Is.EqualTo(baseline));
        }

        [Test]
        public void MemberAndEntryPermutationTogetherGiveTheIdenticalCatalog()
        {
            var document = RunContentExample.Valid();
            var baseline = BuildDump(document);
            var once = Permute(document, true);
            var twice = Permute(once, true);

            Assert.That(BuildDump(once), Is.EqualTo(baseline));
            Assert.That(BuildDump(twice), Is.EqualTo(baseline));
        }

        [Test]
        public void ResearchInContentOrderFollowsTheDocumentOrder()
        {
            var document = Permute(RunContentExample.Valid(), true);
            var expected = new List<string>();
            foreach (var entry in document["research"])
            {
                expected.Add((string)entry["id"]);
            }

            var result = GameplayContentDispatcher.Load(RunContentExample.Bytes(document));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.RunCatalog.ResearchInContentOrder.Select(r => r.Id), Is.EqualTo(expected));
            Assert.That(result.RunCatalog.Research.Keys.OrderBy(k => k, StringComparer.Ordinal), Is.EqualTo(expected.OrderBy(k => k, StringComparer.Ordinal)));
        }

        /// <summary>Rebuilds a document with reversed members and, when asked, reversed order-free arrays.</summary>
        private static JObject Permute(JObject source, bool entries) => (JObject)PermuteToken(source, null, entries);

        private static JToken PermuteToken(JToken token, string name, bool entries)
        {
            if (token is JObject obj)
            {
                var copy = new JObject();
                foreach (var property in obj.Properties().Reverse())
                {
                    copy.Add(property.Name, PermuteToken(property.Value, property.Name, entries));
                }

                return copy;
            }

            if (token is JArray array)
            {
                var items = array.ToList();
                if (entries && (name == null || !OrderedMembers.Contains(name)))
                {
                    items.Reverse();
                }

                var copy = new JArray();
                foreach (var item in items)
                {
                    copy.Add(PermuteToken(item, null, entries));
                }

                return copy;
            }

            return token.DeepClone();
        }

        /// <summary>
        /// Order-insensitive canonical text of an object graph: dictionaries by ordinal key, lists of objects by their own
        /// dump, lists of numbers in order (the precomputed tables).
        /// </summary>
        private static string Dump(object value, string memberName)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string text:
                    return "\"" + text + "\"";
                case IFormattable number:
                    return number.ToString(null, CultureInfo.InvariantCulture);
                case bool flag:
                    return flag ? "true" : "false";
            }

            var type = value.GetType();
            if (value is IEnumerable sequence)
            {
                var entries = new List<string>();
                var numeric = true;
                foreach (var item in sequence)
                {
                    if (item != null && item.GetType().IsGenericType && item.GetType().GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                    {
                        var key = item.GetType().GetProperty("Key").GetValue(item);
                        entries.Add(Dump(key, null) + "=>" + Dump(item.GetType().GetProperty("Value").GetValue(item), null));
                        numeric = false;
                        continue;
                    }

                    numeric &= item is long || item is int;
                    entries.Add(Dump(item, null));
                }

                if (!numeric)
                {
                    entries.Sort(StringComparer.Ordinal);
                }

                return "[" + string.Join(",", entries) + "]";
            }

            var builder = new StringBuilder(type.Name).Append('{');
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (property.Name == nameof(RunCatalog.ResearchInContentOrder) || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                builder.Append(property.Name).Append('=').Append(Dump(property.GetValue(value), property.Name)).Append(';');
            }

            return builder.Append('}').ToString();
        }
    }
}
