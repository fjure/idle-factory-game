using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T512 (Feature 006, FR-037, SC-011): a source guard that keeps the 12-location assumption out of new world code. Everything that
    /// measures, places or frames the world reads <c>FactoryWorldLayout</c>, so a fixed slot count (<c>SlotGridLayout.SlotCount</c>, a
    /// comparison, loop bound, modulo or array length of 12) in the guarded paths fails here. The F004 code and tests that really are about
    /// the 12 shipped slots are listed as exempt by file, each with its reason; an exemption whose file is gone fails too.
    /// <para>
    /// How it finds the lines: <c>git grep --untracked -n -I -F -e 12 -e SlotCount</c> over the guarded path specs gives the candidate lines
    /// (tracked and not-yet-committed files alike), and a regular expression decides which candidates are a fixed slot count. When git is
    /// not available (it cannot start, times out, or fails with an error other than "no match") the same candidate lines come from reading
    /// the guarded files directly, so the guard never turns into a silent pass or a spurious failure without git. The detector itself has
    /// positive and negative cases below.
    /// </para>
    /// </summary>
    public sealed class NoFixedSlotCountGuardTests
    {
        private const int GitTimeoutMilliseconds = 30000;

        /// <summary>The guarded path specs (relative to the repository root): the world layer, the camera seam and the F006 tests.</summary>
        private static readonly string[] Guarded =
        {
            "Assets/IdleFactory/Unity/Presentation/World/*.cs",
            "Assets/IdleFactory/Unity/Presentation/CameraScaleModel.cs",
            "Assets/IdleFactory/Unity/Presentation/RunCameraFraming.cs",
            "Assets/IdleFactory/Unity/Presentation/FactoryCameraController.cs",
            "Assets/IdleFactory/Tests/EditMode/F006/*.cs",
            "Assets/IdleFactory/Tests/PlayMode/F006/*.cs",
            // The existing F004 tests about the 12 shipped slots are scanned so that the exemption list below is real, not a comment.
            "Assets/IdleFactory/Tests/EditMode/SlotGridLatticeTests.cs",
            "Assets/IdleFactory/Tests/EditMode/CameraScaleModelTests.cs",
            "Assets/IdleFactory/Tests/EditMode/ScreenClassScaleTableTests.cs",
            "Assets/IdleFactory/Tests/PlayMode/SlotGridTests.cs",
            "Assets/IdleFactory/Tests/PlayMode/NavigationInputTests.cs",
            "Assets/IdleFactory/Tests/PlayMode/RunHudLayoutTests.cs",
            "Assets/IdleFactory/Tests/PlayMode/ScreenClassScalePlayTests.cs",
        };

        /// <summary>The F004 12-slot code and tests, exempt by file with the reason.</summary>
        private static readonly Dictionary<string, string> Exempt = new Dictionary<string, string>
        {
            ["Assets/IdleFactory/Unity/Presentation/World/SlotLattice.cs"] = "SlotGridLayout: the F004 12-slot lattice itself (the parity reference)",
            ["Assets/IdleFactory/Tests/EditMode/F006/LatticeGeometryExportTests.cs"] = "T505 export of the F004 12-slot geometry",
            ["Assets/IdleFactory/Tests/PlayMode/F006/BaselineRig.cs"] = "T507 baseline matrix of the 12 shipped slots",
            ["Assets/IdleFactory/Tests/EditMode/F006/NoFixedSlotCountGuardTests.cs"] = "this guard: its detector patterns and positive cases spell the forbidden forms",
            ["Assets/IdleFactory/Tests/EditMode/SlotGridLatticeTests.cs"] = "F004 12-slot lattice tests",
            ["Assets/IdleFactory/Tests/EditMode/CameraScaleModelTests.cs"] = "F004 camera tests on the 12-slot lattice",
            ["Assets/IdleFactory/Tests/EditMode/ScreenClassScaleTableTests.cs"] = "F004 screen-class table for the 12-slot lattice",
            ["Assets/IdleFactory/Tests/PlayMode/SlotGridTests.cs"] = "F004 12-slot view tests",
            ["Assets/IdleFactory/Tests/PlayMode/NavigationInputTests.cs"] = "F004 12-slot navigation tests",
            ["Assets/IdleFactory/Tests/PlayMode/RunHudLayoutTests.cs"] = "F004 12-slot HUD framing tests",
            ["Assets/IdleFactory/Tests/PlayMode/ScreenClassScalePlayTests.cs"] = "F004 12-slot screen-class tests",
        };

        private static readonly Regex[] FixedCount =
        {
            new Regex(@"SlotGridLayout\s*\.\s*SlotCount", RegexOptions.Compiled),
            new Regex(@"\b(Length|Count|LocationCount|SlotCount|slotCount|locationCount)\s*(==|!=|<=?|>=?)\s*12\b", RegexOptions.Compiled),
            new Regex(@"\b12\s*(==|!=|<=?|>=?)\s*(\w+\.)*(Length|Count|LocationCount|SlotCount|slotCount|locationCount)\b", RegexOptions.Compiled),
            new Regex(@"\bnew\s+[\w.]+(<[^>]*>)?\s*\[\s*12\s*\]", RegexOptions.Compiled),
            new Regex(@"(<|<=|%|\*|/)\s*12\b", RegexOptions.Compiled),
            new Regex(@"\bEnumerable\s*\.\s*Range\s*\(\s*0\s*,\s*12\s*\)", RegexOptions.Compiled),
            new Regex(@"\b(const|static readonly)\s+int\s+\w*(Slot|Location)\w*\s*=\s*12\b", RegexOptions.Compiled),
        };

        /// <summary>True when the source line fixes the number of slots or locations at 12 (or names the F004 constant). Comments are skipped.</summary>
        internal static bool IsFixedSlotCount(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var code = line;
            var comment = code.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0)
            {
                code = code.Substring(0, comment);
            }

            if (code.TrimStart().StartsWith("*", StringComparison.Ordinal) || code.TrimStart().StartsWith("/*", StringComparison.Ordinal))
            {
                return false;
            }

            // The layout builder's single sanctioned name for the shipped count (a named constant, not an assumption in the layout code).
            if (code.Contains("ShippedLocationCount"))
            {
                return false;
            }

            return FixedCount.Any(pattern => pattern.IsMatch(code));
        }

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>Candidate lines (path, line number, text) through git grep, or null when git cannot answer.</summary>
        private static List<(string Path, int Line, string Text)> CandidatesThroughGit(string root)
        {
            var arguments = new List<string> { "grep", "--untracked", "-n", "-I", "-F", "-e", "12", "-e", "SlotCount", "--" };
            // A wildcard spec uses the glob magic so that "*" stops at a slash, as the file scan below does.
            arguments.AddRange(Guarded.Select(spec => spec.Contains("*") ? ":(glob)" + spec : spec));
            var info = new ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            try
            {
                using (var process = Process.Start(info))
                {
                    if (process == null)
                    {
                        return null;
                    }

                    var errors = process.StandardError.ReadToEndAsync();
                    var output = process.StandardOutput.ReadToEndAsync();
                    if (!process.WaitForExit(GitTimeoutMilliseconds))
                    {
                        try { process.Kill(); } catch (InvalidOperationException) { }
                        return null;
                    }

                    // 0: matches; 1: no match (a valid answer); anything else (not a repository, bad pathspec) is "git cannot answer".
                    if (process.ExitCode != 0 && process.ExitCode != 1)
                    {
                        return null;
                    }

                    var result = new List<(string, int, string)>();
                    foreach (var line in output.Result.Split('\n'))
                    {
                        var parts = line.TrimEnd('\r').Split(new[] { ':' }, 3);
                        if (parts.Length == 3 && int.TryParse(parts[1], out var number))
                        {
                            result.Add((parts[0].Replace('\\', '/'), number, parts[2]));
                        }
                    }

                    return result;
                }
            }
            catch (Win32Exception)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>The same candidate lines read from the files, for a machine or a checkout without git.</summary>
        private static List<(string Path, int Line, string Text)> CandidatesThroughFiles(string root)
        {
            var result = new List<(string, int, string)>();
            foreach (var spec in Guarded)
            {
                var directory = Path.GetDirectoryName(spec) ?? string.Empty;
                var pattern = Path.GetFileName(spec);
                var folder = Path.Combine(root, directory);
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(folder, pattern))
                {
                    var relative = (directory + "/" + Path.GetFileName(file)).Replace('\\', '/');
                    var lines = File.ReadAllLines(file);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains("12") || lines[i].Contains("SlotCount"))
                        {
                            result.Add((relative, i + 1, lines[i]));
                        }
                    }
                }
            }

            return result;
        }

        private static List<(string Path, int Line, string Text)> Candidates(string root, out string source)
        {
            var viaGit = CandidatesThroughGit(root);
            if (viaGit != null)
            {
                source = "git grep";
                return viaGit;
            }

            source = "file scan (git unavailable)";
            return CandidatesThroughFiles(root);
        }

        [Test]
        public void NoNewWorldCodeOrTestFixesTheSlotCountAtTwelve()
        {
            var root = RepositoryRoot();
            var candidates = Candidates(root, out var source);
            var offenders = candidates
                .Where(candidate => !Exempt.ContainsKey(candidate.Path) && IsFixedSlotCount(candidate.Text))
                .Select(candidate => $"{candidate.Path}:{candidate.Line}: {candidate.Text.Trim()}")
                .ToList();
            Assert.That(offenders, Is.Empty,
                "A fixed slot count of 12 in new code (found through " + source + "). Read FactoryWorldLayout (LocationCount, Bounds, TryHit) instead; " +
                "an F004 12-slot test is exempted in NoFixedSlotCountGuardTests.Exempt with its reason:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void EveryExemptionNamesAnExistingFileAndEveryGuardedSpecMatchesFiles()
        {
            var root = RepositoryRoot();
            foreach (var pair in Exempt)
            {
                Assert.That(File.Exists(Path.Combine(root, pair.Key)), Is.True, "stale exemption: " + pair.Key + " (" + pair.Value + ")");
                Assert.That(Guarded.Any(spec => spec == pair.Key || (spec.EndsWith("/*.cs", StringComparison.Ordinal) && pair.Key.StartsWith(spec.Substring(0, spec.Length - 4), StringComparison.Ordinal))),
                    Is.True, "the exempt file is not under a guarded spec, so the exemption does nothing: " + pair.Key);
            }

            foreach (var spec in Guarded)
            {
                var folder = Path.Combine(root, Path.GetDirectoryName(spec) ?? string.Empty);
                Assert.That(Directory.Exists(folder) && Directory.GetFiles(folder, Path.GetFileName(spec)).Length > 0, Is.True, "the guarded spec matches no file: " + spec);
            }
        }

        [Test]
        public void TheTwoWaysOfFindingCandidatesAgreeWhenGitIsAvailable()
        {
            var root = RepositoryRoot();
            var viaGit = CandidatesThroughGit(root);
            if (viaGit == null)
            {
                Assert.Ignore("git is not available here; the file scan is the guard.");
            }

            var fromGit = new HashSet<string>(viaGit.Where(c => IsFixedSlotCount(c.Text)).Select(c => c.Path + ":" + c.Line));
            var fromFiles = new HashSet<string>(CandidatesThroughFiles(root).Where(c => IsFixedSlotCount(c.Text)).Select(c => c.Path + ":" + c.Line));
            Assert.That(fromGit, Is.EquivalentTo(fromFiles), "git grep and the file scan must find the same fixed slot counts");
        }

        [TestCase("for (var i = 0; i < SlotGridLayout.SlotCount; i++)")]
        [TestCase("if (slots.Length == 12) return;")]
        [TestCase("if (12 == layout.LocationCount) return;")]
        [TestCase("var slots = new SlotRenderers[12];")]
        [TestCase("var row = index / 12;")]
        [TestCase("for (var i = 0; i < 12; i++)")]
        [TestCase("var all = Enumerable.Range(0, 12).ToArray();")]
        [TestCase("private const int SlotTotal = 12;")]
        [TestCase("return count % 12;")]
        public void TheDetectorFlagsAFixedSlotCount(string line)
        {
            Assert.That(IsFixedSlotCount(line), Is.True, line);
        }

        [TestCase("for (var i = 0; i < layout.LocationCount; i++)")]
        [TestCase("public const int ShippedLocationCount = SlotGridLayout.SlotCount;")]
        [TestCase("var size = 12 points of text;")]
        [TestCase("Assert.That(size, Is.EqualTo(12f));")]
        [TestCase("var font = 12;")]
        [TestCase("// for (var i = 0; i < SlotGridLayout.SlotCount; i++)")]
        [TestCase("/// the 12 slots of F004 (SlotGridLayout.SlotCount)")]
        [TestCase("    * SlotGridLayout.SlotCount in a doc comment")]
        [TestCase("")]
        public void TheDetectorIgnoresOtherTwelvesAndComments(string line)
        {
            Assert.That(IsFixedSlotCount(line), Is.False, line);
        }
    }
}
