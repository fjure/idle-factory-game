"""Tests for compare_test_ids.py. Fixtures are built in temporary git repositories, never the real one."""
from __future__ import annotations

import subprocess
from pathlib import Path

import pytest

import compare_test_ids as cti

TESTS_DIR = "Assets/IdleFactory/Tests/EditMode"
MAP_REL = "specs/map.md"
MAP_HEADER = "| Old id | New id or removed | Reason | Task | Status |\n|---|---|---|---|---|\n"

BASE_SRC = """\
using NUnit.Framework;

namespace Fx.Tests
{
    public class AlphaTests
    {
        [Test]
        public void Keeps() { }

        [Test]
        public void Goes() { }

        // [Test] public void InComment() { }

        [Test, Category("slow")]
        public void Combined() { }

        [Test]
        [Category("x")]
        public void Stacked() { }

        [TestCase(1, "a|b")]
        [TestCase( 2,   "c" )]
        public void Cases(int n, string s) { }

        [UnityTest]
        public System.Collections.IEnumerator Routine() { yield return null; }

        public class Inner
        {
            [Test]
            public void Nested() { }
        }

        public void NotATest() { var s = "[Test] public void InString()"; }
    }
}
"""


def git(repo: Path, *args: str) -> None:
    subprocess.run(["git", "-C", str(repo), *args], check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)


@pytest.fixture
def repo(tmp_path: Path) -> Path:
    git(tmp_path, "init", "-q", "-b", "main")
    git(tmp_path, "config", "user.email", "t@example.invalid")
    git(tmp_path, "config", "user.name", "t")
    git(tmp_path, "config", "commit.gpgsign", "false")
    (tmp_path / TESTS_DIR).mkdir(parents=True)
    (tmp_path / TESTS_DIR / "AlphaTests.cs").write_text(BASE_SRC, encoding="utf-8")
    (tmp_path / "specs").mkdir()
    (tmp_path / MAP_REL).write_text(MAP_HEADER, encoding="utf-8")
    git(tmp_path, "add", "-A")
    git(tmp_path, "commit", "-q", "-m", "base")
    git(tmp_path, "tag", "base")
    return tmp_path


def commit_head(repo: Path, src: str, map_rows: str = "") -> None:
    (repo / TESTS_DIR / "AlphaTests.cs").write_text(src, encoding="utf-8")
    (repo / MAP_REL).write_text(MAP_HEADER + map_rows, encoding="utf-8")
    git(repo, "add", "-A")
    git(repo, "commit", "-q", "-m", "head")


def check(repo: Path, *extra: str) -> int:
    return cti.run(["--repo", str(repo), "--base", "base", "--head", "HEAD", "--map", MAP_REL, *extra])


# ---- extraction -----------------------------------------------------------------------------

def test_extract_ids_covers_attribute_forms():
    ids = set(cti.extract_ids(BASE_SRC))
    assert ids == {
        "Fx.Tests.AlphaTests.Keeps",
        "Fx.Tests.AlphaTests.Goes",
        "Fx.Tests.AlphaTests.Combined",       # [Test, Category("slow")]
        "Fx.Tests.AlphaTests.Stacked",        # attributes on separate lines
        "Fx.Tests.AlphaTests.Cases(1, \"a|b\")",
        "Fx.Tests.AlphaTests.Cases(2, \"c\")",  # whitespace collapsed
        "Fx.Tests.AlphaTests.Routine",        # [UnityTest]
        "Fx.Tests.AlphaTests.Inner.Nested",   # nested class
    }


def test_extract_ids_file_scoped_namespace():
    src = "namespace A.B;\npublic class C { [Test] public void M() {} [TestCase(\"x\")] public void N(string s) {} }\n"
    assert set(cti.extract_ids(src)) == {"A.B.C.M", "A.B.C.N(\"x\")"}


def test_extract_ids_test_with_testcase_yields_both():
    src = "namespace N { class C { [Test][TestCase(3)] public void M(int x) {} } }"
    assert set(cti.extract_ids(src)) == {"N.C.M", "N.C.M(3)"}


def test_extract_ids_ignores_non_tests_and_brace_in_strings():
    src = ('namespace N { class C { void Helper() { var s = "}}{"; var c = \'}\'; }\n'
           '  [SetUp] public void Init() {}\n  [Test] public void Real() {} } }')
    assert cti.extract_ids(src) == ["N.C.Real"]


# ---- map parsing ----------------------------------------------------------------------------

def test_parse_map_rows_pipes_and_status():
    text = (MAP_HEADER
            + "| `N.C.M(\"a\\|b\")` | `N.C.M2(\"a\\|b\")` | rename | T1 | moved |\n"
            + "| `N.C.Gone` | removed | obsolete | T2 | removed |\n"
            + "| `Cls.*` | - | candidate | T3 | not yet moved |\n")
    rows = cti.parse_map(text)
    assert [r["old"] for r in rows] == ['N.C.M("a|b")', "N.C.Gone", "Cls.*"]
    assert rows[0]["new"] == 'N.C.M2("a|b")'
    assert rows[2]["status"] == "not yet moved"


def test_parse_map_rejects_unknown_status_and_bad_cell_count():
    with pytest.raises(cti.ToolError):
        cti.parse_map(MAP_HEADER + "| a | b | c | d | maybe |\n")
    with pytest.raises(cti.ToolError):
        cti.parse_map(MAP_HEADER + "| a | b | c |\n")


def test_parse_map_ignores_other_tables():
    text = "| x | y |\n|---|---|\n| 1 | 2 |\n\n" + MAP_HEADER + "| a | removed | r | t | removed |\n"
    assert [r["old"] for r in cti.parse_map(text)] == ["a"]


# ---- end to end in a temporary git repository -----------------------------------------------

def test_unchanged_head_passes(repo, capsys):
    assert check(repo) == 0
    assert "PASS" in capsys.readouterr().out


def test_deleted_test_without_mapping_fails(repo, capsys):
    commit_head(repo, BASE_SRC.replace("        [Test]\n        public void Goes() { }\n", ""))
    assert check(repo) == 1
    out = capsys.readouterr().out
    assert "UNMAPPED missing test id: Fx.Tests.AlphaTests.Goes" in out


def test_renamed_test_fails_then_passes_once_mapped(repo):
    renamed = BASE_SRC.replace("void Keeps()", "void KeepsRenamed()")
    commit_head(repo, renamed)
    assert check(repo) == 1
    rows = "| Fx.Tests.AlphaTests.Keeps | Fx.Tests.AlphaTests.KeepsRenamed | rename | T1 | moved |\n"
    commit_head(repo, renamed, rows)
    assert check(repo) == 0


def test_removed_row_satisfies_deleted_id(repo):
    commit_head(repo, BASE_SRC.replace("public void Goes() { }", "public void Other() { }").replace("[Test]\n        public void Other", "public void Other"))
    rows = "| Fx.Tests.AlphaTests.Goes | removed | obsolete | T1 | removed |\n"
    commit_head(repo, BASE_SRC.replace("        [Test]\n        public void Goes() { }\n", ""), rows)
    assert check(repo) == 0


def test_moved_row_with_nonexistent_new_id_fails(repo, capsys):
    renamed = BASE_SRC.replace("void Keeps()", "void KeepsRenamed()")
    rows = "| Fx.Tests.AlphaTests.Keeps | Fx.Tests.AlphaTests.Typo | rename | T1 | moved |\n"
    commit_head(repo, renamed, rows)
    assert check(repo) == 1
    assert "does not exist at head" in capsys.readouterr().out


def test_class_level_and_unmoved_rows_do_not_satisfy(repo):
    head = BASE_SRC.replace("void Keeps()", "void KeepsRenamed()")
    rows = ("| AlphaTests.* | - | candidate | T1 | moved |\n"
            "| Fx.Tests.AlphaTests.Keeps | Fx.Tests.AlphaTests.KeepsRenamed | r | T1 | not yet moved |\n")
    commit_head(repo, head, rows)
    assert check(repo) == 1


def test_testcase_argument_id_change_needs_mapping(repo, capsys):
    head = BASE_SRC.replace('[TestCase( 2,   "c" )]', '[TestCase(2, "d")]')
    commit_head(repo, head)
    assert check(repo) == 1
    assert 'Cases(2, "c")' in capsys.readouterr().out
    rows = '| Fx.Tests.AlphaTests.Cases(2, "c") | Fx.Tests.AlphaTests.Cases(2, "d") | arg change | T1 | moved |\n'
    commit_head(repo, head, rows)
    assert check(repo) == 0


def test_method_row_covers_all_testcase_ids(repo):
    head = BASE_SRC.replace("public void Cases(", "public void CasesRenamed(")
    rows = "| Fx.Tests.AlphaTests.Cases | removed | replaced | T1 | removed |\n"
    commit_head(repo, head, rows)
    assert check(repo) == 0


def test_added_ids_are_fine(repo):
    commit_head(repo, BASE_SRC.replace("public void Keeps() { }", "public void Keeps() { }\n[Test] public void Fresh() { }"))
    assert check(repo) == 0


def test_working_tree_head(repo):
    (repo / TESTS_DIR / "AlphaTests.cs").write_text(BASE_SRC.replace("void Goes()", "void Gone()"), encoding="utf-8")
    assert cti.run(["--repo", str(repo), "--base", "base", "--head", "WORKTREE", "--map", MAP_REL]) == 1


def test_usage_and_git_errors_exit_2(repo, tmp_path):
    assert cti.run(["--repo", str(repo), "--base", "no-such-ref", "--head", "HEAD", "--map", MAP_REL]) == 2
    assert cti.run(["--repo", str(repo), "--base", "base", "--head", "HEAD", "--map", "missing.md"]) == 2
    assert cti.run(["--bogus-flag"]) == 2
    assert cti.run(["--repo", str(tmp_path / "nope"), "--map", MAP_REL]) == 2


def test_emit_rows_prints_draft(repo, capsys):
    commit_head(repo, BASE_SRC.replace("        [Test]\n        public void Goes() { }\n", ""))
    assert check(repo, "--emit-rows") == 1
    assert "| `Fx.Tests.AlphaTests.Goes` |" in capsys.readouterr().out
