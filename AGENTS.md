# Repository Guidelines

## Project Structure & Module Organization

- `docs/`: requirement-driven source of truth (PRD / ADR / iterations / tasks / acceptance).
- `specs/schemas/`: JSON schemas for configs and runtime metadata.
- `configs/`: sample configs by task (`detection/`, `ocr/`).
- `src/`: layered codebase (`SharedKernel`, `Contracts`, `Domain`, `Application`, `Infrastructure*`, `DataAdapters.*`, `Training.Tasks.*`, `Cli`, `WinForms`).
- `tests/`: `Unit`, `Integration`, `Contracts`.
- `runs/`: generated local runtime outputs (artifacts, reports, sqlite db).

## Build, Test, and Development Commands

- `dotnet build .\TransformersMini.slnx -c Release`: build all projects.
- `dotnet test .\TransformersMini.slnx -c Release`: run all tests.
- `dotnet run --project .\src\TransformersMini.Cli\TransformersMini.Cli.csproj -- run --config .\configs\detection\sample.det.train.json --dry-run`: validate config/orchestration.
- `dotnet run --project .\src\TransformersMini.WinForms\TransformersMini.WinForms.csproj`: launch WinForms workbench.
- Mandatory quality gate: before commit/PR, `dotnet build .\TransformersMini.slnx -c Release` must be `0 errors, 0 warnings`.

## Coding Style & Naming Conventions

- C# / .NET 10 with `Nullable` and `ImplicitUsings` enabled.
- Indentation: 4 spaces. Files must be UTF-8.
- Collaboration language default is Chinese; contributor-facing notes and agent responses should be Chinese.
- New/updated code comments must be Chinese and UTF-8 (no garbled text).
- Strong typing is mandatory: prefer explicit typed models/contracts; avoid `object`-based payload handling in core logic unless unavoidable.
- No boxing/unboxing in new implementations (including hidden boxing through non-generic collections or `object` casts in hot paths).
- Naming: `PascalCase` for types/methods/properties, `_camelCase` for private fields.
- Keep `Cli` and `WinForms` as thin hosts; orchestration/business logic belongs in `Application` and contracts.

## WinForms Rules

- WinForms forms must follow the 3-file convention:
  - code-behind file (`*.cs`)
  - designer file (`*.Designer.cs`)
  - resource file (`*.resx`)
- Designer files must follow official WinForms generated structure so they can be opened/previewed in the Visual Studio designer.
- Do not handcraft non-standard designer patterns that break designer preview.

## Testing Guidelines

- Framework: xUnit.
- Place tests by scope: pure logic in `Tests.Unit`, orchestration/storage in `Tests.Integration`, schema/config contract checks in `Tests.Contracts`.
- Test names should describe behavior (e.g., `LoadAsync_AppliesForcedModeAndDevice`).
- New features should include at least one success-path and one failure-path test.

## Commit & Pull Request Guidelines

- Use Conventional Commits (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`).
- PRs should include purpose/scope, linked `docs/` paths, build/test evidence, `0 errors / 0 warnings` confirmation, and WinForms screenshots for UI changes.

- When reading text files in PowerShell (for example `Get-Content`, `Select-String` pipelines), explicitly use UTF-8 (`-Encoding utf8`) to avoid garbled Chinese text.

## Security & Configuration Tips

- Do not commit real datasets, secrets, large model weights, or generated runtime artifacts.
- Prefer `configs/` over hard-coded paths.
- If Chinese text appears garbled, check file and terminal/editor encoding before committing.

