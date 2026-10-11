using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// Pins <c>docs/contracts/run-reason-codes.md</c> (AR-16, QA-15, #355): the codes of the table equal the public
    /// string constants of <see cref="RunReasons"/>, <see cref="RunPersistenceReasons"/> and
    /// <see cref="RunOfflineSummary.ValueLimitCode"/>, each row names the right source class, and every rejection code
    /// has its own player copy in <see cref="RunRejectionCopy"/>.
    ///
    /// Rule for "rejection code": a row whose Source column is <c>RunReasons</c> (a command rejected next to a
    /// control). Persistence outcomes are shown by the startup and prestige panels with their own copy, and the
    /// offline summary code is not a rejection, so neither needs a <see cref="RunRejectionCopy"/> entry. A code has
    /// copy when <see cref="RunRejectionCopy.For"/> returns something other than the generic fallback.
    /// Reflection is used here only; product code never reflects.
    /// </summary>
    public sealed class ReasonCodeContractTests
    {
        private const string TableRelativeToRoot = "docs/contracts/run-reason-codes.md";
        private const string RunReasonsSource = "RunReasons";
        private const string PersistenceSource = "RunPersistence";
        private const string SummarySource = "RunOfflineSummary";

        private static readonly Regex Row = new Regex(@"^\|\s*`(?<code>[^`]+)`\s*\|\s*(?<source>[A-Za-z]+)\s*\|", RegexOptions.Compiled);

        [Test]
        public void TableCodesEqualTheCodeConstants()
        {
            var problems = Compare(ParseTable(), CodeConstants());
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void EveryRejectionCodeHasPlayerCopy()
        {
            var problems = MissingCopy(ParseTable().Where(r => r.Value == RunReasonsSource).Select(r => r.Key));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void StableRejectionListIsInTheTable()
        {
            var table = ParseTable();
            foreach (var code in RunReasons.All)
            {
                Assert.That(table.ContainsKey(code), Is.True, code + " is in RunReasons.All but not in the table");
                Assert.That(table[code], Is.EqualTo(RunReasonsSource), code);
            }
        }

        // QA-15: the comparison itself must fail for an unknown code, without touching the real table.
        [Test]
        public void ComparisonFailsForACodeMissingFromTheTable()
        {
            var table = ParseTable();
            var code = CodeConstants();
            code["injected.unknown-code"] = RunReasonsSource;
            var problems = Compare(table, code);
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("injected.unknown-code"));
        }

        [Test]
        public void ComparisonFailsForATableCodeMissingFromCode()
        {
            var table = ParseTable();
            table["injected.table-only"] = RunReasonsSource;
            var problems = Compare(table, CodeConstants());
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("injected.table-only"));
        }

        [Test]
        public void ComparisonFailsForAWrongSource()
        {
            var table = ParseTable();
            table["slot.occupied"] = PersistenceSource;
            var problems = Compare(table, CodeConstants());
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("slot.occupied"));
        }

        [Test]
        public void CopyCheckFailsForARejectionCodeWithoutCopy()
        {
            var problems = MissingCopy(new[] { RunReasons.SlotOccupied, "injected.no-copy" });
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("injected.no-copy"));
        }

        /// <summary>Differences between the table (code to source) and the code (code to source); empty when equal.</summary>
        private static List<string> Compare(IReadOnlyDictionary<string, string> table, IReadOnlyDictionary<string, string> code)
        {
            var problems = new List<string>();
            foreach (var pair in code.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (!table.TryGetValue(pair.Key, out var source))
                {
                    problems.Add("In code but not in the table: " + pair.Key + " (" + pair.Value + ")");
                }
                else if (source != pair.Value)
                {
                    problems.Add("Wrong source for " + pair.Key + ": table says " + source + ", code says " + pair.Value);
                }
            }

            foreach (var key in table.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!code.ContainsKey(key))
                {
                    problems.Add("In the table but not in code: " + key);
                }
            }

            return problems;
        }

        private static List<string> MissingCopy(IEnumerable<string> rejectionCodes)
        {
            var fallback = RunRejectionCopy.For("injected.fallback-probe");
            var problems = new List<string>();
            foreach (var code in rejectionCodes)
            {
                var copy = RunRejectionCopy.For(code);
                if (string.IsNullOrWhiteSpace(copy) || copy == fallback)
                {
                    problems.Add("No player copy in RunRejectionCopy for rejection code " + code);
                }
            }

            return problems;
        }

        /// <summary>Every code constant of the run application, with the class that owns it.</summary>
        private static Dictionary<string, string> CodeConstants()
        {
            var codes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var value in StringConstants(typeof(RunReasons)))
            {
                codes[value] = RunReasonsSource;
            }

            // Persistence constants that alias a RunReasons code (transition.pending, command.invalid) keep RunReasons.
            foreach (var value in StringConstants(typeof(RunPersistenceReasons)))
            {
                if (!codes.ContainsKey(value))
                {
                    codes[value] = PersistenceSource;
                }
            }

            codes[RunOfflineSummary.ValueLimitCode] = SummarySource;
            return codes;
        }

        private static IEnumerable<string> StringConstants(Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue());
        }

        private static Dictionary<string, string> ParseTable()
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var path = Path.Combine(root, TableRelativeToRoot.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(path), Is.True, "Missing " + path);
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(path))
            {
                var match = Row.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var code = match.Groups["code"].Value;
                Assert.That(table.ContainsKey(code), Is.False, "Duplicate row " + code);
                table[code] = match.Groups["source"].Value;
            }

            Assert.That(table, Is.Not.Empty, "No rows parsed from " + path);
            return table;
        }
    }
}
