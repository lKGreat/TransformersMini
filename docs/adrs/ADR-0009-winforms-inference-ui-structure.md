# ADR-0009 WinForms 推理界面结构与约束

- ID: `ADR-0009`
- Status: `Proposed`
- Date: `2026-02-23`
- Related Requirements: `REQ-0004`, `REQ-0005`, `REQ-0006`

## Context

长期主线将新增批量推理与单图推理 UI。团队已明确 WinForms 必须遵守三文件结构约束，并保证设计器可预览。

## Decision

- 新增或重构 WinForms 界面必须采用标准三文件结构：
  - `*.cs`
  - `*.Designer.cs`
  - `*.resx`
- 设计器布局为主，业务逻辑留在代码文件，资源文字/图标放 `.resx`
- 推理 UI 优先新增独立表单/用户控件，避免 `MainForm` 单文件继续膨胀

## Consequences

- 需要在迭代任务中显式列出 Designer 改动点
- 现有手写布局的后续改造要分阶段进行，避免一次性重构风险

## Alternatives Considered

- 全手写 WinForms 控件布局
  - 缺点：与团队规则冲突，设计器不可视化维护成本高

## Follow-up

- 规划推理面板原型与控件清单
- 增加 WinForms 结构检查清单到迭代验收
