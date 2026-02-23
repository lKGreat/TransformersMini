# ADR-0004 Dual Backend Strategy (ML.NET + TorchSharp)

- ID: `ADR-0004`
- Status: `Accepted`
- Related Requirements: `REQ-0001`

## Decision

- Keep ML.NET and TorchSharp as parallel backends behind a shared capability interface.
- TorchSharp is the first real training backend target.
- ML.NET backend remains capability-visible in foundation iteration.
