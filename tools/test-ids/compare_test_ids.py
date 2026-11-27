#!/usr/bin/env python3
"""Compare C# NUnit test ids under Assets/IdleFactory/Tests/ between two git refs.

Feature 007, T638 (QA-5, R19, FR-036). Every test id present at --base but missing at --head must be
listed in the rebaseline map as an exact-id row with status "moved" or "removed".

Exit codes: 0 pass, 1 unmapped missing ids (or unsatisfied map rows), 2 usage, git or map error.

Id format
---------
  <namespace>.<Class>[.<Nested>].<Method>              for [Test], [UnityTest], [TestCaseSource]
  <namespace>.<Class>[.<Nested>].<Method>(<arg text>)  for each [TestCase(...)] (one id per attribute)
Argument text is the source text between the attribute parentheses with whitespace runs collapsed.
Nested classes are joined with "." (NUnit itself uses "+"). [UnityTest] ids are included, like [Test].
A method carrying [Test] and [TestCase] yields both the plain id and every argument id.

Map semantics
-------------
Only exact-id rows with status "moved" or "removed" satisfy a missing id. A row whose Old id is a method id
without "(...)" also covers every [TestCase] id of that method. Class-level rows (Old id ending in ".*" or
"::*", or containing "/") never satisfy anything, whatever their status: list each id when a class moves
(use --emit-rows for a paste-ready draft). "moved" additionally requires the new id to exist at head.

Parsing limits (a light C# scanner, not a compiler)
---------------------------------------------------
* Comments, string/char literals (regular, verbatim, interpolated, raw) and preprocessor lines are skipped.
  Preprocessor conditions are NOT evaluated: both #if branches are scanned, so a conditional that changes
  brace balance breaks the class stack.
* Attributes are recognised only directly inside a type body (class/struct/record/interface) when they
  start a member. Attributes are matched by simple name (Test, NUnit.Framework.Test, TestAttribute).
* The test method is the last identifier before the first '(' after the attribute lists; properties,
  fields and attributes on local functions are ignored. Partial classes are not merged.
* [TestCaseSource], [Theory] and [Values] expansions are not enumerated; [TestCaseSource] alone
  contributes the plain method id, [Theory] is not scanned.
* Duplicate ids (overloads, identical TestCase text) collapse to one id in the set.
"""
from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path

TESTS_ROOT = "Assets/IdleFactory/Tests/"
DEFAULT_MAP = "specs/007-stylized-industrial-visual-reboot/reviews/rebaseline-map.md"
WORKING_TREE = "WORKTREE"
TYPE_KEYWORDS = {"class", "struct", "record", "interface"}
PLAIN_ATTRS = {"Test", "UnityTest", "TestCaseSource"}
VALID_STATUSES = {"moved", "removed", "not yet moved", "out of scope"}
MAP_HEADER = ["old id", "new id or removed", "reason", "task", "status"]


class ToolError(Exception):
    """Usage, git or map-format problem (exit 2)."""


# ----------------------------------------------------------------------------- tokenizer

_IDENT_RE = re.compile(r"[A-Za-z_@][A-Za-z0-9_]*")
_NUM_RE = re.compile(r"[0-9][A-Za-z0-9_.]*")
_STR_START_RE = re.compile(r'(\$@|@\$|\$|@)?"')


def _skip_regular_string(s: str, i: int) -> int:
    """i points at the opening quote; returns the index after the closing quote."""
    n = len(s)
    i += 1
    while i < n:
        c = s[i]
        if c == "\\":
            i += 2
        elif c == '"':
            return i + 1
        elif c == "\n":
            return i
        else:
            i += 1
    return n


def _skip_verbatim_string(s: str, i: int) -> int:
    n = len(s)
    i += 1
    while i < n:
        if s[i] == '"':
            if i + 1 < n and s[i + 1] == '"':
                i += 2
                continue
            return i + 1
        i += 1
    return n


def _skip_interpolated(s: str, i: int, verbatim: bool) -> int:
    """i points at the opening quote of an interpolated string."""
    n = len(s)
    i += 1
    while i < n:
        c = s[i]
        if not verbatim and c == "\\":
            i += 2
        elif c == '"':
            if verbatim and i + 1 < n and s[i + 1] == '"':
                i += 2
                continue
            return i + 1
        elif c == "{":
            if i + 1 < n and s[i + 1] == "{":
                i += 2
                continue
            depth = 1
            i += 1
            while i < n and depth:
                d = s[i]
                if d == '"':
                    i = _skip_regular_string(s, i)
                    continue
                if d == "{":
                    depth += 1
                elif d == "}":
                    depth -= 1
                i += 1
        elif c == "\n" and not verbatim:
            return i
        else:
            i += 1
    return n


def _skip_raw_string(s: str, i: int) -> int:
    j = i
    while j < len(s) and s[j] == '"':
        j += 1
    quotes = j - i
    end = s.find('"' * quotes, j)
    return len(s) if end < 0 else end + quotes


def tokenize(s: str) -> list[tuple[str, str, int, int]]:
    """Returns (kind, text, start, end); kind in ident, str, punct, num. Comments are dropped."""
    toks: list[tuple[str, str, int, int]] = []
    n = len(s)
    i = 0
    line_start = True
    while i < n:
        c = s[i]
        if c == "\n":
            line_start = True
            i += 1
            continue
        if c in " \t\r﻿":
            i += 1
            continue
        if c == "#" and line_start:
            while i < n and s[i] != "\n":
                i += 1
            continue
        line_start = False
        if s.startswith("//", i):
            while i < n and s[i] != "\n":
                i += 1
            continue
        if s.startswith("/*", i):
            end = s.find("*/", i + 2)
            i = n if end < 0 else end + 2
            continue
        if c in '$@"':
            m = _STR_START_RE.match(s, i)
            if m:
                prefix = m.group(1) or ""
                q = i + len(prefix)
                if s.startswith('"""', q):
                    end = _skip_raw_string(s, q)
                elif "$" in prefix:
                    end = _skip_interpolated(s, q, "@" in prefix)
                elif "@" in prefix:
                    end = _skip_verbatim_string(s, q)
                else:
                    end = _skip_regular_string(s, q)
                toks.append(("str", s[i:end], i, end))
                i = end
                continue
        if c == "'":
            j = i + 1
            while j < n and s[j] != "'" and s[j] != "\n":
                j += 2 if s[j] == "\\" else 1
            end = min(j + 1, n)
            toks.append(("str", s[i:end], i, end))
            i = end
            continue
        m = _IDENT_RE.match(s, i)
        if m:
            toks.append(("ident", m.group(0), i, m.end()))
            i = m.end()
            continue
        m = _NUM_RE.match(s, i)
        if m:
            toks.append(("num", m.group(0), i, m.end()))
            i = m.end()
            continue
        toks.append(("punct", c, i, i + 1))
        i += 1
    return toks


# ----------------------------------------------------------------------------- id extraction

def _attr_name(name: str) -> str:
    last = name.split(".")[-1]
    return last[:-9] if last.endswith("Attribute") and len(last) > 9 else last


def _parse_attribute_list(toks, src, lo, hi):
    """toks[lo:hi] is the content of one [...] list. Returns [(name, argtext or None)]."""
    attrs = []
    parts, cur, depth = [], [], 0
    for t in toks[lo:hi]:
        if t[0] == "punct" and t[1] in "([{":
            depth += 1
        elif t[0] == "punct" and t[1] in ")]}":
            depth -= 1
        if t[0] == "punct" and t[1] == "," and depth == 0:
            parts.append(cur)
            cur = []
        else:
            cur.append(t)
    parts.append(cur)
    for part in parts:
        if not part:
            continue
        j = 0
        if len(part) > 2 and part[0][0] == "ident" and part[1][1] == ":":
            j = 2  # attribute target such as "return:"
        name = ""
        while j < len(part) and part[j][0] == "ident":
            name += part[j][1]
            if j + 1 < len(part) and part[j + 1][1] == ".":
                name += "."
                j += 2
            else:
                j += 1
                break
        if not name:
            continue
        args = None
        if j < len(part) and part[j][1] == "(":
            depth, end_k = 0, len(part) - 1
            for k in range(j, len(part)):
                if part[k][0] == "punct" and part[k][1] in "([{":
                    depth += 1
                elif part[k][0] == "punct" and part[k][1] in ")]}":
                    depth -= 1
                    if depth == 0:
                        end_k = k
                        break
            args = re.sub(r"\s+", " ", src[part[j][3]:part[end_k][2]]).strip()
        attrs.append((_attr_name(name), args))
    return attrs


def extract_ids(src: str) -> list[str]:
    """All test ids declared in one C# source text (may contain duplicates)."""
    toks = tokenize(src)
    n = len(toks)
    ids: list[str] = []
    stack: list[tuple[str | None, str | None]] = []  # (kind, name)
    file_ns = ""
    pending: tuple[str, str] | None = None
    prev = None  # previous significant token text
    i = 0

    def qualified(extra: str) -> str:
        parts = [file_ns] if file_ns else []
        parts += [name for _, name in stack if name]
        parts.append(extra)
        return ".".join(parts)

    while i < n:
        kind, text, _, _ = toks[i]
        if kind == "ident" and text == "namespace" and prev != ".":
            j, name = i + 1, ""
            while j < n and (toks[j][0] == "ident" or toks[j][1] == "."):
                name += toks[j][1]
                j += 1
            if j < n and toks[j][1] == ";":
                file_ns = name
                i, prev = j + 1, ";"
                continue
            pending = ("ns", name)
            i, prev = j, "ident"
            continue
        if kind == "ident" and text in TYPE_KEYWORDS and pending is None and prev not in (".", "typeof"):
            j = i + 1
            if j < n and toks[j][1] in ("struct", "class"):
                j += 1
            if j < n and toks[j][0] == "ident":
                pending = ("type", toks[j][1])
                i, prev = j + 1, "ident"
                continue
        if kind == "punct" and text == "{":
            stack.append(pending if pending else (None, None))
            pending = None
        elif kind == "punct" and text == "}":
            if stack:
                stack.pop()
            pending = None
        elif kind == "punct" and text == ";":
            pending = None
        elif (kind == "punct" and text == "[" and stack and stack[-1][0] == "type"
              and prev in (None, "{", "}", ";", "]")):
            attrs = []
            j = i
            while j < n and toks[j][0] == "punct" and toks[j][1] == "[":
                depth, k = 0, j
                for k in range(j, n):
                    if toks[k][0] == "punct" and toks[k][1] == "[":
                        depth += 1
                    elif toks[k][0] == "punct" and toks[k][1] == "]":
                        depth -= 1
                        if depth == 0:
                            break
                attrs += _parse_attribute_list(toks, src, j + 1, k)
                j = k + 1
            # member name: last identifier before the first '(' at angle depth 0
            name, angle, k = None, 0, j
            while k < n:
                tk, tt = toks[k][0], toks[k][1]
                if tk == "punct":
                    if tt == "<":
                        angle += 1
                    elif tt == ">":
                        angle = max(0, angle - 1)
                    elif angle == 0 and tt in "({=;}":
                        break
                elif tk == "ident" and angle == 0:
                    name = tt
                k += 1
            if k < n and toks[k][1] == "(" and name:
                if any(a in PLAIN_ATTRS for a, _ in attrs):
                    ids.append(qualified(name))
                for a, args in attrs:
                    if a == "TestCase":
                        ids.append(qualified(f"{name}({args or ''})"))
            i, prev = k, "ident"
            continue
        prev = text
        i += 1
    return ids


# ----------------------------------------------------------------------------- git access

def _git(repo: str, *args: str, input_bytes: bytes | None = None) -> bytes:
    try:
        res = subprocess.run(["git", "-C", repo, *args], input=input_bytes,
                             stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    except OSError as exc:
        raise ToolError(f"cannot run git: {exc}")
    if res.returncode != 0:
        raise ToolError(f"git {' '.join(args)} failed: {res.stderr.decode('utf-8', 'replace').strip()}")
    return res.stdout


def read_sources(repo: str, ref: str) -> dict[str, str]:
    """path -> text for every .cs file under TESTS_ROOT at ref (or the working tree)."""
    sources: dict[str, str] = {}
    if ref == WORKING_TREE:
        top = Path(_git(repo, "rev-parse", "--show-toplevel").decode().strip())
        for dirpath, _, files in os.walk(top / TESTS_ROOT):
            for f in files:
                if f.endswith(".cs"):
                    p = Path(dirpath) / f
                    sources[p.relative_to(top).as_posix()] = p.read_bytes().decode("utf-8-sig", "replace")
        return sources
    listing = _git(repo, "ls-tree", "-r", "--name-only", "-z", ref, "--", TESTS_ROOT)
    paths = [p for p in listing.decode("utf-8", "replace").split("\0") if p.endswith(".cs")]
    if not paths:
        return sources
    request = "".join(f"{ref}:{p}\n" for p in paths).encode()
    out = _git(repo, "cat-file", "--batch", input_bytes=request)
    pos = 0
    for p in paths:
        eol = out.index(b"\n", pos)
        header = out[pos:eol].decode().split()
        if len(header) != 3 or header[1] != "blob":
            raise ToolError(f"unexpected cat-file output for {p}: {out[pos:eol]!r}")
        size = int(header[2])
        sources[p] = out[eol + 1:eol + 1 + size].decode("utf-8-sig", "replace")
        pos = eol + 1 + size + 1
    return sources


def collect_ids(repo: str, ref: str) -> set[str]:
    ids: set[str] = set()
    for text in read_sources(repo, ref).values():
        ids.update(extract_ids(text))
    return ids


# ----------------------------------------------------------------------------- map

def _split_row(line: str) -> list[str]:
    body = line.strip()
    if body.startswith("|"):
        body = body[1:]
    if body.endswith("|") and not body.endswith("\\|"):
        body = body[:-1]
    return [c.replace("\\|", "|").strip().strip("`").strip() for c in re.split(r"(?<!\\)\|", body)]


def parse_map(text: str) -> list[dict]:
    """Rows of the table whose header is: Old id | New id or removed | Reason | Task | Status."""
    rows: list[dict] = []
    in_table = False
    for lineno, line in enumerate(text.splitlines(), 1):
        if not line.lstrip().startswith("|"):
            in_table = False
            continue
        cells = _split_row(line)
        if not in_table:
            if [c.lower() for c in cells] == MAP_HEADER:
                in_table = True
            continue
        if cells and all(re.fullmatch(r":?-{2,}:?", c) for c in cells if c):
            continue
        if len(cells) != 5:
            raise ToolError(f"map line {lineno}: expected 5 cells, found {len(cells)}: {line.strip()}")
        old, new, reason, task, status = cells
        if not old:
            continue
        if status.lower() not in VALID_STATUSES:
            raise ToolError(f"map line {lineno}: unknown status '{status}' (allowed: {sorted(VALID_STATUSES)})")
        rows.append({"old": old, "new": new, "reason": reason, "task": task,
                     "status": status.lower(), "line": lineno})
    return rows


def is_class_level(old: str) -> bool:
    return old.endswith(".*") or old.endswith("::*") or "/" in old


def evaluate(base: set[str], head: set[str], rows: list[dict]):
    """Returns (unmapped missing ids, problems with satisfying rows)."""
    exact = {r["old"]: r for r in rows
             if not is_class_level(r["old"]) and r["status"] in ("moved", "removed")}
    unmapped, problems = [], []
    for tid in sorted(base - head):
        row = exact.get(tid) or exact.get(tid.split("(", 1)[0])  # a method row covers all its cases
        if row is None:
            unmapped.append(tid)
            continue
        if row["status"] == "moved" and row["new"] not in head:
            problems.append(f"map line {row['line']}: '{tid}' is 'moved' but new id '{row['new']}' does not exist at head")
        if row["status"] == "removed" and row["new"].lower() != "removed":
            problems.append(f"map line {row['line']}: '{tid}' is 'removed' but column 2 is '{row['new']}', not 'removed'")
    return unmapped, problems


# ----------------------------------------------------------------------------- main

def run(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description="Compare C# NUnit test ids between two git refs against the rebaseline map.")
    ap.add_argument("--base", default="origin/main", help="ref providing the baseline ids (default origin/main)")
    ap.add_argument("--head", default="HEAD", help=f"ref to check, or {WORKING_TREE} for the working tree (default HEAD)")
    ap.add_argument("--map", default=None, help=f"rebaseline map path (default {DEFAULT_MAP} in the working tree)")
    ap.add_argument("--repo", default=".", help="repository directory (default .)")
    ap.add_argument("--emit-rows", action="store_true", help="print paste-ready map rows for unmapped ids")
    try:
        args = ap.parse_args(argv)
    except SystemExit as exc:
        return 2 if exc.code else 0
    try:
        top = Path(_git(args.repo, "rev-parse", "--show-toplevel").decode().strip())
        map_path = (Path(args.repo) / args.map) if args.map else (top / DEFAULT_MAP)
        if not map_path.is_file():
            raise ToolError(f"rebaseline map not found: {map_path}")
        rows = parse_map(map_path.read_text(encoding="utf-8-sig"))
        base = collect_ids(args.repo, args.base)
        head = collect_ids(args.repo, args.head)
    except ToolError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    if not base:
        print(f"ERROR: no test ids found at base '{args.base}'; refusing to pass on an empty baseline", file=sys.stderr)
        return 2
    unmapped, problems = evaluate(base, head, rows)
    print(f"test ids: base {args.base}={len(base)} head {args.head}={len(head)} "
          f"missing={len(base - head)} added={len(head - base)} map_rows={len(rows)}")
    if unmapped or problems:
        for tid in unmapped:
            print(f"UNMAPPED missing test id: {tid}")
        for p in problems:
            print(f"MAP PROBLEM: {p}")
        if args.emit_rows:
            for tid in unmapped:
                print(f"| `{tid}` | <new id or removed> | <reason> | <task> | moved |")
        print(f"FAIL: {len(unmapped)} unmapped, {len(problems)} map problems")
        return 1
    print("PASS: every missing test id is mapped")
    return 0


if __name__ == "__main__":
    sys.exit(run(sys.argv[1:]))
