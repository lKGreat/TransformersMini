# ADR-0003 Runtime Storage and Run Tracking

- ID: `ADR-0003`
- Status: `Accepted`
- Related Requirements: `REQ-0001`

## Decision

- Use filesystem run directories under `runs/`.
- Target long-term metadata storage in SQLite.
- Foundation iteration uses a file index repository stub to unblock end-to-end flow.
