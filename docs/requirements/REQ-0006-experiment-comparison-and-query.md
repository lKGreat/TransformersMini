# REQ-0006 实验检索、对比与错误分析平台

- ID: `REQ-0006`
- Status: `Draft`
- Owner: `pending`
- Related ADRs: `ADR-0003`, `ADR-0007`, `ADR-0008`, `ADR-0009`
- Related Iteration: `ITER-0006+`

## Background

当前运行记录已写入 SQLite 与 `runs/`，WinForms 也具备基础列表与详情展示，但随着训练与推理 run 增加，需要更强的查询、对比和错误分析能力。

## Goal

构建实验平台能力：可筛选、可对比、可复现、可错误分析，覆盖检测与 OCR 两类任务。

## Success Criteria

- 基于标签、任务、模式、时间、指标的查询可用
- WinForms 可对比 2-3 个 run 的关键参数和指标
- 检测/OCR 的错误样本摘要可查看
- 查询不依赖全量逐条 `GetRunAsync` 扫描

## In Scope

- SQLite 查询接口与过滤模型
- WinForms 筛选/对比视图（轻量版）
- 检测/OCR 错误摘要展示
- 复现实验命令与配置快照展示

## Out of Scope

- Web 端实验平台
- 团队协作权限系统

## Constraints

- 不破坏现有 `IRunRepository` 契约兼容性（优先新增查询接口）
- UI 首期以 WinForms 为主

## Acceptance Criteria

- 查询接口有单元/集成测试
- WinForms 能完成筛选、对比、错误摘要查看
- 文档与验收证据完整

## Risks

- 查询能力扩展过快导致接口复杂度失控
- 报告格式演进导致错误摘要逻辑脆弱

## Change Log

- `2026-02-23`：初版需求草案创建
