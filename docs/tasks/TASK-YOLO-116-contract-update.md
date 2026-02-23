# TASK-YOLO-116 DetectionModelMetadata 契约扩展

- ID: `TASK-YOLO-116`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

扩展 DetectionModelMetadata 契约，支持 YoloDetectionModel 的新架构参数。

## Implementation Notes

文件：`src/TransformersMini.Contracts/ModelMetadata/DetectionModelMetadata.cs`

新增字段（可选，保持向后兼容）：
- `BackboneScale`（string，nano/small/medium）
- `RegMax`（int，DFL reg_max）
- `NumClasses`（int）
- `Strides`（int[]，如 [8, 16, 32]）

对应更新 `DetectionModelMetadataDto` record。

## Done Definition

- 新字段可序列化/反序列化，旧 JSON（无新字段）仍可正常加载，build 无 warning
