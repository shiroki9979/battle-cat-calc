# Agent Instructions

Created: 2026-10-01
Last updated: 2026-10-01

## Agent skills

### Language and code

- Reply in Traditional Chinese by default; use English for code strings and error messages.
- Add Python type hints to new interfaces. Use dataclasses for data models when they fit; `Track` remains a `UserList`.
- Keep comments focused on why a decision matters.

### Parser tests

- For parser changes, test through `BCParser` with real HTML in `tests/fixtures/` when fixture behavior matters.
- Derive expected values from the HTML or documented rules, then compare them with parser output. Parser output alone is not an independent oracle.
- Use `@pytest.mark.asyncio(loop_scope="function")` for async fetcher tests.
- For tests generated from an HTML fixture, read `.agents/skills/parse-html/SKILL.md`.

### Issue tracker

Track issues and specs in GitHub Issues. Before tracker operations, read
`docs/agents/issue-tracker.md`.

### Triage labels

Use the five default triage labels. Before triaging, read
`docs/agents/triage-labels.md`.

### Domain docs

Use single-context layout: root `CONTEXT.md` and `docs/adr/`.
Before exploring the codebase, read `docs/agents/domain.md`.
For questions about gacha mechanics, follow its source-based answer guidance.
