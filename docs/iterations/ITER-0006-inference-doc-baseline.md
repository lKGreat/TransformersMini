# ITER-0006 推理主线文档基线与接口冻结

- ID: `ITER-0006`
- Status: `Completed`
- Start: `2026-02-23`
- End: `2026-02-23`
- Related Requirements: `REQ-0004`, `REQ-0005`, `REQ-0006`, `REQ-0007`
- Related ADRs: `ADR-0005`, `ADR-0006`, `ADR-0007`, `ADR-0008`, `ADR-0009`, `ADR-0010`

## Objective

在实施推理主线之前，先完成长期主线的文档基线、接口边界与任务拆分，确保后续实现严格对齐文档。

## Scope

- 新增推理与实验平台相关 REQ/ADR 文档
- 明确训练到推理链路的接口边界（编排、产物、报告、查询）
- 产出后续 2-3 个迭代的任务卡骨架与验收标准

## Non-Goals

- 实现批量推理或单图推理代码
- 实现 SQLite 查询仓库代码

## Tasks

- `TASK-ITER-0006-001-doc-state-alignment-and-roadmap-refresh.md`
- `TASK-ITER-0006-002-inference-req-adr-baseline.md`
- `TASK-ITER-0006-003-iteration-task-breakdown-for-inference-mainline.md`
- `TASK-ITER-0006-004-acceptance-and-evidence-template-for-doc-first-execution.md`

## Risks

- 文档过于高层，无法直接指导实现
- 文档编号与现有任务/验收不一致导致追踪困难

## Exit Criteria

- `REQ/ADR/ITER/TASK` 链完整可执行
- 后续实现无需再做架构性临时决策

## Delivered

- 推理与实验平台长期主线 REQ 文档（`REQ-0004`~`REQ-0007`）
- 推理主线关键 ADR 文档（`ADR-0005`~`ADR-0010`）
- `ITER-0004` / `ITER-0005` 回填与 `ITER-0006` 文档基线
- 后续推理主线迭代与任务卡骨架（`ITER-0007`~`ITER-0009` + 任务卡）
- 验收证据模板强化与 `AC-ITER-0006` 草案回填
