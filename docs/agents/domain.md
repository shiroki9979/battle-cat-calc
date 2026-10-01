# Domain Docs

Created: 2026-10-01
Last updated: 2026-10-01

## Layout

This repository uses a single context:

- `CONTEXT.md`: root domain glossary and model.
- `docs/adr/`: architecture decision records.

## Before Exploring

Read root `CONTEXT.md` and ADRs relevant to the area being explored.

If these optional documents are absent, continue without reporting their
absence or proposing their creation upfront. The domain-modeling skill
creates them when domain terms or decisions are resolved.

## Vocabulary

Use terms defined in `CONTEXT.md` when naming domain concepts in issues,
proposals, hypotheses, and tests.

For a missing term, reconsider whether it fits the project's language.
Record genuine glossary gaps for domain-modeling.

## ADR Conflicts

Explicitly identify proposals that contradict an existing ADR, citing
the decision and explaining why reconsideration is warranted.

## Answering Mechanism Questions

For questions about seed tracking, track switching, duplicate rares,
guarantees, or multiple pools, consult the relevant source before answering:

- `docs/gacha_mechanics_zh_TW.md` for mechanism details.
- `docs/gacha_mechanics_player_guide_zh_TW.md` for player-facing explanations.
- `docs/design_logic.md` for search states and transitions.
- `docs/api_reference.md` for published interfaces.

Cite the document and section supporting each rule. State when the sources do
not establish an answer. When a question combines game rules and code behavior,
explain the rule first, then identify the corresponding module or method. For
explanation-only requests, answer without changing files.
