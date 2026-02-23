# ITER-0003 Detection MVP

- ID: `ITER-0003`
- Status: `InProgress`

## Objective

Implement real detection training with TorchSharp and COCO.

## Delivered (Current)

- TorchSharp detection training/validate/test entry on unified orchestrator
- COCO adapter image + annotations parsing and metadata projection
- Real image tensor loading via ImageSharp
- COCO target tensor generation with `first/largest/average` box strategies
- Preprocessing snapshot persisted to reports, tags, and run events

## Remaining Scope

- Multi-box target encoding (`topK`)
- Detection head upgrade beyond tiny CNN baseline
- Non-placeholder evaluation metrics (IoU/precision/recall or better)
