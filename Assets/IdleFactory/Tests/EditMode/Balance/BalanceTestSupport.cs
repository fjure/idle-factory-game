using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Tests.EditMode.Balance;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>Asserts the gates of <see cref="TraceGates"/> and writes the transitional ones as the one reporting line the project uses for them.</summary>
    internal static class GateAssert
    {
        /// <summary>
        /// Fails on a gate that fails and asserts at this revision. A content-tuned gate that fails below revision 3 is written as a
        /// <c>REPORTED (transitional ...)</c> line with its measured value (owner decision 2026-10-22, #408): it asserts from <c>contentRevision</c> 3.
        /// </summary>
        public static void AssertAll(IReadOnlyList<GateResult> gates, int contentRevision)
        {
            foreach (GateResult g in gates)
            {
                if (!g.IsTransitionalMiss(contentRevision)) continue;
                string line = "REPORTED (transitional until contentRevision " + TraceGates.AssertsFromRevision + ", T341/T345) gate '" + g.Id + "': " + g.Measured;
                TestContext.Out.WriteLine(line);
                TestContext.Progress.WriteLine(line);
            }
            List<GateResult> failing = gates.Where(g => g.Fails(contentRevision)).ToList();
            Assert.That(failing, Is.Empty, string.Join(" | ", failing.Select(g => g.Id + ": " + g.Measured)));
            Assert.That(gates.Where(g => g.IsTransitionalMiss(contentRevision)).Select(g => g.Id).Except(TraceGates.ContentTunedIds), Is.Empty,
                "only a content-tuned gate may be transitional");
        }
    }

    /// <summary>Whether a heavy balance test was asked for: by category, by test file, or by the opt-in variable.</summary>
    internal static class BalanceOptIn
    {
        public const string RunVariable = "IDLEFACTORY_BALANCE_FULL";

        /// <summary>
        /// The test runner script counts a skipped test as a failure, so <c>[Explicit]</c> cannot keep a heavy test out of the default run. The test
        /// reads the editor's own command line instead: <c>-testCategory BalanceFull</c> (the documented run, <c>-Suite EditMode -Category BalanceFull</c>),
        /// a <c>-testFilter</c> that names the class (<c>-TestFile</c>), or <c>IDLEFACTORY_BALANCE_FULL=1</c>. Without one of them it passes at once.
        /// </summary>
        public static bool BalanceFullSelected()
        {
            if (Environment.GetEnvironmentVariable(RunVariable) == "1") return true;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], "-testCategory", StringComparison.OrdinalIgnoreCase) &&
                    args[i + 1].Split(';').Any(c => string.Equals(c.Trim(), "BalanceFull", StringComparison.Ordinal))) return true;
                if (string.Equals(args[i], "-testFilter", StringComparison.OrdinalIgnoreCase) && args[i + 1].Contains("BalanceTraceFullTests")) return true;
            }
            return false;
        }
    }
}
