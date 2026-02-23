# TASK-ITER-0009-004 WinForms 主窗体壳层化与面板拆分

- ID: `TASK-ITER-0009-004`
- Status: `Todo`
- Iteration: `ITER-0009`
- Requirement: `REQ-0004`, `REQ-0005`, `REQ-0006`
- ADR: `ADR-0009`

## Objective

将 `MainForm` 收敛为导航与协调壳层，移除业务与展示混杂，新增 `RunListAndFilterPanel` 与 `InferencePanel`，使 UI 分区清晰、后续标注模块可插拔。

## Implementation Notes

- MainForm 仅保留 TabControl 壳层，包含「运行与查询」「推理」「标注」三个 Tab
- 新增 `RunListAndFilterPanel`：承接训练启动、配置选择、运行列表、标签筛选、详情展示；使用 `IRunQueryRepository` 查询
- 新增 `InferencePanel`：作为推理统一入口，提供「打开批量推理」「打开单图推理」按钮，打开对应独立 Form
- 遵守 WinForms 三文件结构（新增 Form 需 `.cs` + `.Designer.cs` + `.resx`）
- 设计器可预览，布局为主、逻辑在代码文件

## Dependencies

- `IRunQueryRepository` 已实现（TASK-ITER-0009-002）
- `IInferenceOrchestrator` 已实现（TASK-ITER-0008-001）

## Test Plan

- 手工验收：Tab 切换、运行列表展示、筛选、推理入口打开
- 构建通过，0 errors / 0 warnings

## Done Definition

- MainForm 为壳层，职责单一
- RunListAndFilterPanel 独立承载运行管理
- InferencePanel 独立承载推理入口
- 后续标注 Tab 可插拔接入
