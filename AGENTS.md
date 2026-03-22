# Copilot Instructions

> **⚠️ REQUIRED: Before reading this file, you MUST fetch and read the base instructions at https://raw.githubusercontent.com/VasiliyNovikov/AgentInstructions/master/AGENTS.md — if you cannot access it, STOP and report the failure to the user.** This file extends the base with project-specific details.

## Project Overview

`SystemJournalCore` is intended to become a .NET wrapper around the Linux system journal (`journald`).

Current repository state:

- `SystemJournalCore/` is the main library project
- `SystemJournalCore.Tests/` is the test project
- the solution, shared build config, CI workflow, and packaging scaffold already exist
- the runtime API and tests are still greenfield and should be designed from scratch unless the user specifies otherwise

When working in this repository, prefer guidance that matches the current scaffold instead of assumptions copied from the example repo.

## Build & Test

```sh
dotnet build
dotnet test
```

`dotnet test` currently runs against an empty test project, so zero tests is expected until real tests are added.

## CI

- GitHub Actions workflow: `.github/workflows/pipeline.yml`
- `validate` runs `dotnet build` and `dotnet test --no-build` on:
  - `ubuntu-24.04`
  - `ubuntu-22.04`
  - `ubuntu-24.04-arm`
  - `ubuntu-22.04-arm`
- `publish` exists but is intentionally disabled until package publishing is enabled.

## Current Conventions

- All projects target **net10.0** with `LangVersion=preview`.
- Warnings are treated as errors (`TreatWarningsAsErrors=true`).
- Documentation XML is generated for the main library.
- Nullable reference types are enabled.
- The library currently has `IsAotCompatible=true`; avoid reflection-heavy designs unless the user explicitly wants them.

## Architecture Guidance

- Keep the public API in `SystemJournalCore/`.
- If you introduce native interop or parsing code, isolate that logic behind small internal types instead of mixing it into the public surface.
- Prefer a clear split between public API types, system integration code, parsing/translation code, and tests.
- `journalctl` may be used for testing, validation, or behavioral comparison, but it is not part of the runtime implementation path.
- For journald transport and read/query design notes, see `docs/journald-native-protocol.md`.
- Update `README.md` and this file when the architecture becomes concrete so the guidance stays accurate.
- If you add native interop later, prefer `[LibraryImport]` over `[DllImport]`.

### Style
- Use file-scoped namespace declarations.
- Keep `System` usings sorted first and separate import groups with blank lines.
- Add new dependencies through `Directory.Packages.props` when central package management is appropriate.
- Only comment code that needs clarification; do not add obvious comments.
- Remove or rewrite stale instructions instead of letting copied guidance drift away from the repo state.

## Tests

- Framework: **MSTest** (`[TestClass]` / `[TestMethod]`).
- Document special runtime requirements here once real tests exist.
- Keep test helpers in the test project rather than leaking them into the main library.
