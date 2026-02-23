# TASK-ITER-0006-002 推理主线 REQ/ADR 基线文档

- ID: `TASK-ITER-0006-002`
- Status: `Done`
- Iteration: `ITER-0006`
- Requirement: `REQ-0004`, `REQ-0005`, `REQ-0006`, `REQ-0007`
- ADR: `ADR-0005`, `ADR-0006`, `ADR-0007`, `ADR-0008`, `ADR-0009`, `ADR-0010`

## Objective

为“训练到推理”长期主线建立第一版需求与架构决策文档，锁定实现边界和约束。

## Implementation Notes

- REQ 覆盖检测推理、OCR 推理、实验查询对比、ML.NET 范围边界
- ADR 覆盖推理入口、模型产物 metadata、推理报告、查询仓库、WinForms 推理结构、ML.NET 边界
- 文档内容需达到“后续实现无需再做架构性决策”的程度

## Test Plan

- 文档审阅清单：目标、范围、约束、验收、风险是否齐全
- 检查 REQ 与 ADR 的引用链是否完整

## Done Definition

- 相关 REQ/ADR 文档创建完成并可直接指导下阶段任务拆分

## Result

- 已新增 `REQ-0004`~`REQ-0007` 与 `ADR-0005`~`ADR-0010`
