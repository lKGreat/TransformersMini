# Repository Guidelines（中文团队版）

## 项目结构与模块组织

- `docs/`：需求驱动开发的主入口（PRD、ADR、迭代、任务、验收）。任何功能先改文档再落代码。
- `specs/schemas/`：配置与运行元数据的 JSON Schema。
- `configs/`：示例配置（按任务分类，如 `detection/`、`ocr/`）。
- `src/`：源码按职责分层：
  - `SharedKernel` / `Contracts` / `Domain` / `Application`
  - `Infrastructure*`（存储、后端能力、平台装配）
  - `DataAdapters.*`（COCO、OCR manifest）
  - `Training.Tasks.*`（Detection、OCR）
  - `TransformersMini.Cli` / `TransformersMini.WinForms`
- `tests/`：`Unit`、`Integration`、`Contracts` 三类测试。
- `runs/`：本地产物目录（运行记录、报告、配置快照）。

## 构建、测试与本地开发命令

- `dotnet build .\TransformersMini.slnx -c Release`：构建全部项目。
- `dotnet test .\TransformersMini.slnx -c Release`：运行全部测试。
- `dotnet run --project .\src\TransformersMini.Cli\TransformersMini.Cli.csproj -- run --config .\configs\detection\sample.det.train.json --dry-run`：验证配置与统一入口（不执行真实训练）。
- `dotnet run --project .\src\TransformersMini.WinForms\TransformersMini.WinForms.csproj`：启动 WinForms 工作台。
- 强制质量门槛：提交前必须执行 `dotnet build .\TransformersMini.slnx -c Release`，结果必须为 `0错误 0警告`（警告按阻塞处理）。

## 编码风格与命名约定

- 技术栈：C# / .NET 10，开启 `Nullable` 与 `ImplicitUsings`。
- 缩进 4 空格；默认使用 UTF-8；无必要不要引入非 ASCII 字符。
- 协作语言要求：面向团队的回复、评审意见、说明文档默认使用中文。
- 注释语言要求：新增/修改代码注释必须使用中文，并确保文件编码为 UTF-8，禁止出现乱码注释。
- 命名规范：
  - 类型 / 接口：`PascalCase`（如 `ITrainingOrchestrator`）
  - 方法 / 属性：`PascalCase`
  - 私有字段：`_camelCase`
  - 文件名与主类型名一致
- 约束：`Cli`、`WinForms` 仅做宿主层；业务编排放在 `Application`，契约放在 `Contracts`。

## 测试规范

- 测试框架：xUnit。
- 放置原则：
  - `Tests.Unit`：纯逻辑、配置校验、领域规则
  - `Tests.Integration`：编排流程、存储、端到端 stub
  - `Tests.Contracts`：Schema、样例配置、契约兼容性
- 命名建议：`方法_场景_预期结果`，例如 `LoadAsync_AppliesForcedModeAndDevice`。
- 新功能至少补 1 个成功路径测试 + 1 个失败路径测试。

## 提交与 Pull Request 规范

- 当前仓库历史较少，建议统一使用 Conventional Commits：
  - `feat: ...`、`fix: ...`、`docs: ...`、`refactor: ...`、`test: ...`
- PR 必须包含：
  - 变更目的与范围
  - 关联文档路径（`docs/requirements`、`docs/adrs`、`docs/iterations`、`docs/tasks`）
  - 构建/测试结果（命令与结论）
  - `0错误 0警告` 编译确认
  - WinForms 界面改动截图（如涉及 UI）

## 安全与配置注意事项

- 不要提交真实数据集、密钥、模型权重、大体积产物。
- 实验输出统一放 `runs/`，数据集放 `data/`（按需加入忽略规则）。
- 优先通过 `configs/` 配置驱动，不要在代码里写死本地路径。
- 若终端或编辑器出现中文乱码，先确认文件编码与控制台编码为 UTF-8，再进行修改提交。
