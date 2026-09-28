---
title: Canonical JSON content form
type: decision
sources: [S008, S015, S016]
updated: 2026-09-29
---

# Canonical JSON content form

T074 adds the packaged development catalog from the independent accepted fixture. Its Edit Mode test loads the document through `GameplayContentLoader`, compares the result field by field with the fixture, and validates the complete authoring-guide example. (S008, S015, S016)

The same test compares the authored document with a test-only canonical rendering. Stable member order, indentation and collection order make review diffs predictable, while non-canonical whitespace remains a formatting issue rather than a runtime load failure. T074 leaves the bootstrap unchanged. (S008, S015)

Canonical form follows the documented two-space indentation, LF endings and final newline; documented member order; ordinal sorting of top-level entries and nested amounts or capacities; and omission of redundant defaults. (S008)

Related: [JSON content authoring](./json-content-authoring.md), [gameplay content validation](./gameplay-content-validation.md).
