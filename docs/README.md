# Docs-Driven Workflow

This repository uses `docs/` as the primary driver of implementation work.

## Hard rule (doc-first)

- No implementation work may begin until the corresponding `REQ/ADR/ITER/TASK` documents are created or updated.
- Code changes must be traceable back to a `TASK-ITER-xxxx-yyy` document.
- Iteration completion requires an `AC-ITER-xxxx.md` evidence record.

## Required flow

1. Create or update a requirement in `docs/requirements/`.
2. Add/adjust architecture decisions in `docs/adrs/` when interfaces or structure change.
3. Plan the iteration in `docs/iterations/`.
4. Break work into executable tasks in `docs/tasks/`.
5. Record acceptance evidence in `docs/acceptance/`.

## Naming

- Requirement: `REQ-xxxx-*.md`
- ADR: `ADR-xxxx-*.md`
- Iteration: `ITER-xxxx-*.md`
- Task: `TASK-ITER-xxxx-yyy-*.md`
- Acceptance: `AC-ITER-xxxx.md`
