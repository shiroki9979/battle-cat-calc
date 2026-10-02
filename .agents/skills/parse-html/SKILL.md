---
name: parse-html
description: Create or update BCParser pytest cases from a real HTML fixture. Use when adding a gacha fixture, checking parser output against fixture data, or responding to a bc.godfat.org HTML structure change.
---

# Test BCParser with an HTML fixture

Created: 2026-10-01
Last updated: 2026-10-02

## Input

Use the fixture named by the user under `tests/fixtures/`. If none is named,
list the available HTML fixtures and ask which one to use. Note any fields or
behaviors the user wants checked.

## Workflow

1. Read the fixture and the relevant parsing rules in
   `src/battle_cat_calc/parser.py`. Inspect the source HTML for known slot
   values and track transitions.
2. Run `BCParser` on the fixture to locate the corresponding `track_a` and
   `track_b` slots. Compare its output with the HTML. Derive expected values
   from the fixture or documented rules, never solely from parser output.
3. Add focused public-interface tests, following `tests/test_snapshot.py` for
   fixture loading and `tests/test_parser.py` for parser usage. Check only
   behaviors present in the fixture and relevant to the requested change:
   track lengths, representative `GachaSlot` fields, duplicate rare switches,
   `ParseStatus` values, or final-slot behavior.
4. Run the affected pytest file, then report the result and any fixture values
   that could not be verified independently.

Read fixture text with `Path.read_text(encoding="utf-8")`. Keep expected values
literal and traceable to the HTML or documented rules. Avoid placeholders and
assumed track lengths.

When checking duplicate rare slots, restore the `rowspan` grid before reading
cells: the table stretches a neighbouring slot to three rows to line up with a
duplicate rare slot, and covered cells shift the remaining `<td>` elements left.
A slot spanning three rows is not necessarily a duplicate rare slot. Record the
row numbers you relied on in the test comment.
