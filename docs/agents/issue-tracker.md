# Issue Tracker: GitHub

Created: 2026-10-01
Last updated: 2026-10-01

Issues and specs live in GitHub Issues for `shiroki9979/battle-cat-calc`.
Use the `gh` CLI from this clone; it infers the repository from the remote.

## Conventions

- Create: `gh issue create --title "..." --body-file <path>`
- Read: `gh issue view <number> --comments`
- List: `gh issue list --state open --json number,title,body,labels,comments`
  with appropriate label and state filters.
- Comment: `gh issue comment <number> --body-file <path>`
- Add labels: `gh issue edit <number> --add-label "..."`
- Remove labels: `gh issue edit <number> --remove-label "..."`
- Close: `gh issue close <number> --comment "..."`

For multiline bodies, write the exact text to a temporary file and pass
`--body-file`. Fetch labels when evaluating an issue.

## Pull Requests as a Triage Surface

**PRs as a request surface: no.**

If enabled later, use the corresponding `gh pr` operations.
Read PRs with `gh pr view <number> --comments` and `gh pr diff <number>`.

For external PR triage, include author associations `CONTRIBUTOR`,
`FIRST_TIME_CONTRIBUTOR`, and `NONE`; exclude `OWNER`, `MEMBER`, and
`COLLABORATOR`.

Issues and PRs share a number space. Resolve ambiguous references with
`gh pr view <number>`, falling back to `gh issue view <number>`.

## Skill Operations

- "Publish to the issue tracker": create a GitHub issue.
- "Fetch the relevant ticket": run `gh issue view <number> --comments`.

## Wayfinding Operations

- Map: one issue labelled `wayfinder:map`, with Notes, Decisions-so-far,
  and Fog in its body.
- Child ticket: link it as a GitHub sub-issue. If unavailable, add it to
  the map's task list and put `Part of #<map>` in the child body.
- Ticket labels: `wayfinder:research`, `wayfinder:prototype`,
  `wayfinder:grilling`, or `wayfinder:task`.
- Blocking: use native issue dependencies. Add a blocker with
  `gh api --method POST repos/<owner>/<repo>/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-db-id>`.
  Obtain the database ID with
  `gh api repos/<owner>/<repo>/issues/<number> --jq .id`.
  If dependencies are unavailable, use a `Blocked by: #<number>` line.
- Frontier: choose the first open, unassigned child in map order whose
  blockers are all closed. For native dependencies, inspect
  `issue_dependencies_summary.blocked_by`.
- Claim: `gh issue edit <number> --add-assignee @me`, the session's first write.
- Resolve: comment with the answer, close the ticket, and append a brief
  result and link to the map's Decisions-so-far.
