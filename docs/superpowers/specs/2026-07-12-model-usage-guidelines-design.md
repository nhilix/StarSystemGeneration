# Model usage guidelines — design

Date: 2026-07-12

## Problem

Main-session model choice has been ad hoc (whatever `/model` default happened
to be, previously Fable). Fable carries a weekly usage cap tighter than
Sonnet/Opus. Running whole slice sessions — brainstorming, implementation,
review — natively in Fable burns that cap fast, and a large share of that
work (grunt implementation, exploration, mechanical drafting) doesn't need
Fable-tier reasoning to be done well.

## Decision

The main session stays the **orchestrator**, always on Fable (user-driven via
`/model`, not something Claude can switch itself). The orchestrator's own
direct token spend is kept to low-volume, high-judgment work only:
user dialogue, brainstorming Q&A, scope/merge decisions, kickoff-prompt
authoring, HANDOFF.md wrap-up. Everything else is delegated to subagents
with an explicit model pinned per role, so volume work never touches the
Fable budget.

### Role → model table

| Role | Executor | Model | Why |
|---|---|---|---|
| Orchestrator (main session) | Main session | Fable, user-driven via `/model` | User dialogue, brainstorming Q&A, scope/merge decisions, kickoff-prompt authoring — low-volume, high-judgment, stays resident the whole slice |
| Spec authoring (brainstorming's design doc) | Opus subagent | Opus | Takes the orchestrator's distilled decisions + design tree context, writes the spec prose |
| Plan authoring (writing-plans' implementation plan) | Opus subagent | Opus | Only invoked on slices that already call for a written plan (Slice B-style exceptions) |
| Slice implementation tasks | subagent-driven-development subagents | Sonnet default, Opus escalation | Bulk of the token volume; kept off Fable entirely |
| Fresh-eyes whole-branch review | One subagent, pre-merge | Fable | Highest-stakes single pass — worth spending the quota here |
| Explore/research lookups | Explore subagent | Sonnet (default) | No reason to spend Fable or deliberate per-call |

### Sonnet vs. Opus escalation for implementation tasks

Default every implementation task to Sonnet. Escalate a single task to Opus
when it hits any of:

- Touches conservation/determinism invariants (money, hash rolls, iteration
  order) — SIMHEALTH-flagged territory.
- Cross-cutting change spanning multiple `src/Core` subsystems in one task.
- The task itself is a design judgment call, not mechanical implementation
  (e.g., resolving an ambiguity the plan left open).

The escalation call is made per-task at dispatch time in subagent-driven-
development, not decided up front for the whole slice.

### Workflow integration

No phase is added or removed from the slice-session workflow in `CLAUDE.md`.
What changes is who executes steps that previously assumed the orchestrator
did the work directly:

- Step 1 (read kickoff prompt) — orchestrator reads it directly; small,
  unchanged.
- Design/amendment work inside a slice — delegated to an Opus subagent
  instead of drafted inline.
- Step 3 (implementation) — always routed through subagent-driven-
  development now, never inline orchestrator edits, even for small slices.
- Step 4's fresh-eyes whole-branch review subagent — explicitly pinned to
  `model: fable` instead of inheriting default.
- Step 7 (wrap-up: HANDOFF.md, next kickoff prompt) — orchestrator does this
  directly; small, judgment-heavy, unchanged.

Two inline pointers get added to the numbered workflow list itself (steps 3
and 4) so the policy isn't easy to miss when skimming, plus a standalone
`## Model usage` section holding the full table and escalation rule.

## Out of scope

- No change to which slices get a written plan at all (Slice B exception
  stays as-is) — this only pins the model once a plan/spec is being written.
- No automated enforcement; this is documented policy for the orchestrator
  to follow, not a hook or lint rule.
