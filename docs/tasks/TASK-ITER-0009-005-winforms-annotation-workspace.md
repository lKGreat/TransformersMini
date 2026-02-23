# TASK-ITER-0009-005 WinForms 增强标注工作台

- ID: `TASK-ITER-0009-005`
- Status: `Todo`
- Iteration: `ITER-0009`
- Requirement: `REQ-0004`, `REQ-0005`
- ADR: `ADR-0009`

## Objective

内置增强标注能力，支持框选/拖拽/类别编辑、批量操作、撤销重做、推理结果导入，并实现 COCO/YOLO 双格式同等读写。

## Implementation Notes

- 新增 `AnnotationWorkspaceForm`（三文件：`.cs` + `.Designer.cs` + `.resx`）
- 新增 `IAnnotationService` 接口与 `AnnotationService` 实现，负责：
  - 从图像目录创建会话
  - COCO 导入/导出
  - YOLO 导入/导出
  - 推理结果（`inference-samples.jsonl`）导入为标注初稿
- 新增 `AnnotationSession`、`AnnotationImageDocument`、`AnnotationBox`、`AnnotationVersion` 强类型模型（`Contracts/Runtime`）
- 能力清单：
  - 框选绘制、框选中、拖拽移动、删除
  - 上一张/下一张、类别下拉选择
  - 撤销/重做（会话快照）
  - 复制上张框、整图批量改类
  - 一键导入推理结果为初始标注
- 内部统一模型 + 双向适配器，避免 UI 层直接处理 COCO/YOLO 两套结构

## Dependencies

- 推理报告格式稳定（`inference-samples.jsonl` 含 `detections` 数组）
- 检测推理任务输出 `imagePath`/`sampleId`、`detections`（含 x/y/width/height/classId/score）

## Test Plan

- 手工验收：加载图像目录、框选、移动、删除、撤销重做、导入推理、导出 COCO、导出 YOLO
- COCO 导出后导入验证一致性
- YOLO 导出后导入验证一致性

## Done Definition

- 标注工作台可完整使用
- COCO/YOLO 双格式可相互导入验证
- 推理结果可加载为标注初稿并继续人工修订
