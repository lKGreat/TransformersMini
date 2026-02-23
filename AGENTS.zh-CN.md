# Repository Guidelines（中文团队版）

## 项目结构与模块组织

- `docs/`：需求驱动开发的主入口（PRD / ADR / 迭代 / 任务 / 验收）。
- `specs/schemas/`：配置与运行元数据的 JSON Schema。
- `configs/`：按任务划分的示例配置（`detection/`、`ocr/`）。
- `src/`：按职责分层：`SharedKernel`、`Contracts`、`Domain`、`Application`、`Infrastructure*`、`DataAdapters.*`、`Training.Tasks.*`、`Cli`、`WinForms`。
- `tests/`：`Unit`、`Integration`、`Contracts` 三类测试。
- `runs/`：本地运行产物目录（报告、日志、SQLite、模型等）。

## 构建、测试与本地开发命令

- `dotnet build .\TransformersMini.slnx -c Release`：构建全部项目。
- `dotnet test .\TransformersMini.slnx -c Release`：运行全部测试。
- `dotnet run --project .\src\TransformersMini.Cli\TransformersMini.Cli.csproj -- run --config .\configs\detection\sample.det.train.json --dry-run`：验证配置与统一入口。
- `dotnet run --project .\src\TransformersMini.WinForms\TransformersMini.WinForms.csproj`：启动 WinForms 工作台。
- 强制质量门槛：提交前必须执行 `dotnet build .\TransformersMini.slnx -c Release`，结果必须为 `0错误 0警告`。

## 编码风格与命名规范

- 技术栈：C# / .NET 10，开启 `Nullable` 与 `ImplicitUsings`。
- 缩进 4 空格，文件编码统一 UTF-8。
- 协作语言默认中文；评审意见、说明文档、Agent 回复均使用中文。
- 新增/修改代码注释必须使用中文，并确保 UTF-8 编码，禁止乱码。
- 强类型实现要求（强制）：
  - 所有新增实现优先使用强类型模型/契约/泛型接口。
  - 禁止在核心逻辑中使用 `object` + 强转的弱类型实现方式（除非框架边界无法避免）。
  - 禁止装箱/拆箱操作（包括非泛型集合、`object` 中转、热路径隐式装箱）。
- 命名规范：类型/方法/属性使用 `PascalCase`，私有字段使用 `_camelCase`。
- `Cli` 与 `WinForms` 仅做宿主层，业务编排逻辑放在 `Application` 与相关契约层。

## WinForms 约束（强制）

- WinForms 窗体必须遵循三文件结构：
  - 代码文件：`*.cs`
  - 设计器文件：`*.Designer.cs`
  - 资源文件：`*.resx`
- `*.Designer.cs` 必须符合 WinForms 官方生成结构，确保 Visual Studio 设计器可正常预览。
- 禁止使用破坏设计器预览的非标准手写 Designer 结构。

## 测试规范

- 测试框架：xUnit。
- 放置原则：
  - `Tests.Unit`：纯逻辑、规则、配置解析
  - `Tests.Integration`：编排流程、存储、端到端冒烟
  - `Tests.Contracts`：Schema、示例配置、契约一致性
- 测试命名建议：`方法_场景_预期结果`（如 `LoadAsync_AppliesForcedModeAndDevice`）。
- 新功能至少补 1 个成功路径测试 + 1 个失败路径测试。

## 提交与 PR 规范

- 使用 Conventional Commits（如 `feat:`、`fix:`、`docs:`、`refactor:`、`test:`）。
- PR 必须包含：
  - 变更目的与范围
  - 关联 `docs/` 文档路径（需求/ADR/迭代/任务）
  - 构建/测试证据
  - `0错误 0警告` 编译确认
  - 涉及 WinForms UI 变更时附截图

- 使用 PowerShell 读取文本文件时（如 `Get-Content`、`Select-String` 等），必须显式指定 UTF-8（如 `-Encoding utf8`），避免中文乱码。

## 安全与配置注意事项

- 禁止提交真实数据集、密钥、模型权重、大体积产物。
- 优先使用 `configs/` 配置驱动，避免代码硬编码路径。
- 若出现中文乱码，提交前先检查文件编码与终端/编辑器编码是否为 UTF-8。

