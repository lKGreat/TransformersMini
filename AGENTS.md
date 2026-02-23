# Repository Guidelines

## Project Structure & Module Organization

- `docs/`: requirement-driven development source of truth (PRD, ADR, iteration, task, acceptance).
- `specs/schemas/`: JSON schemas for configs and run metadata.
- `configs/`: sample runtime configs (`detection/`, `ocr/`).
- `src/`: application code, split by responsibility:
  - `TransformersMini.Contracts`, `SharedKernel`, `Domain`, `Application`
  - `Infrastructure*` (storage, backend integrations)
  - `DataAdapters.*` (COCO / OCR manifest)
  - `Training.Tasks.*` (Detection / OCR)
  - `TransformersMini.Cli`, `TransformersMini.WinForms`
- `tests/`: `Unit`, `Integration`, `Contracts` test projects.
- `runs/`: local run outputs and metadata index (generated artifacts).

## Build, Test, and Development Commands

- `dotnet build .\TransformersMini.slnx -c Release`: build all projects.
- `dotnet test .\TransformersMini.slnx -c Release`: run all tests.
- `dotnet run --project .\src\TransformersMini.Cli\TransformersMini.Cli.csproj -- run --config .\configs\detection\sample.det.train.json --dry-run`: validate config and orchestration without real training.
- `dotnet run --project .\src\TransformersMini.WinForms\TransformersMini.WinForms.csproj`: launch WinForms workbench.
- Mandatory quality gate: before commit/PR, `dotnet build .\TransformersMini.slnx -c Release` must be `0 errors, 0 warnings` (warnings are blocking).

## Coding Style & Naming Conventions

- Language: C# / .NET 10, `Nullable` and `ImplicitUsings` enabled (`Directory.Build.props`).
- Indentation: 4 spaces; UTF-8 text files; prefer ASCII unless file already uses Unicode.
- Collaboration language: contributor-facing discussion, review notes, and agent responses should be in Chinese.
- Code comments rule: new/updated comments must use Chinese and be saved as UTF-8 to avoid garbled text.
- Naming:
  - Types/interfaces: `PascalCase` (`ITrainingOrchestrator`)
  - Methods/properties: `PascalCase`
  - Private fields: `_camelCase`
  - Files match primary type name.
- Keep host projects thin (`Cli`, `WinForms`); business flow belongs in `Application` and contracts.

## Testing Guidelines

- Framework: xUnit (`tests/*`).
- Add tests by scope: pure logic in `Tests.Unit`, orchestration/storage flows in `Tests.Integration`, JSON/schema/config checks in `Tests.Contracts`.
- Test names should describe behavior, e.g. `LoadAsync_AppliesForcedModeAndDevice`.
- For new features, include at least one acceptance-path test and one failure-path test.

## Commit & Pull Request Guidelines

- Repository history is currently minimal; use Conventional Commits going forward (e.g., `feat: add sqlite run repository`, `fix: handle enum config parsing`).
- PRs should include:
  - purpose and scope
  - linked requirement/ADR/iteration/task docs (paths under `docs/`)
  - test/build evidence (`dotnet build`, `dotnet test`)
  - confirmation that build result is `0 errors, 0 warnings`
  - screenshots for WinForms UI changes

## Security & Configuration Tips

- Do not commit real datasets, secrets, or large model weights.
- Keep local experiment outputs under `runs/` and dataset files under `data/` (ignored as needed).
- Prefer config files in `configs/` over hard-coded paths.
- If terminal/editor encoding issues appear, explicitly use UTF-8 before editing Chinese comments or docs.
