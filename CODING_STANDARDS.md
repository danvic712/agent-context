# Coding Standards

Documented coding standards for this repository. These override any generic
baseline when reviewing code (see the `code-review` skill).

## C# — interfaces and implementations are separated

Backend C# follows a strict interface/implementation split:

- **Interfaces (and cross-boundary exceptions) live in a `Contracts` folder**
  inside the owning layer, under the `…Contracts` namespace. Current home:
  `src/AgentContext.Application/Contracts` (`AgentContext.Application.Contracts`).
- **Shared data contracts (DTOs: request/result records) live in a sibling
  `Dtos` folder** at the same level as `Contracts`, under the `…Dtos` namespace.
  Current home: `src/AgentContext.Application/Dtos`
  (`AgentContext.Application.Dtos`).
- **DTOs are one type per file.** A file in `Dtos/` declares exactly one DTO
  (record) — never bundle several DTOs into a single `…Dtos.cs` file. Name the
  file after the type (`SaveSessionRequest.cs` declares `SaveSessionRequest`).
- **Application-layer enums live in a sibling `Enums` folder** at the same level
  as `Contracts`/`Dtos`, under the `…Enums` namespace. Current home:
  `src/AgentContext.Application/Enums` (`AgentContext.Application.Enums`).
  Domain enums stay in `AgentContext.Domain/Enums.cs`; an enum is moved to the
  application layer only when it exists for application logic (e.g.
  `PipelineOutcome`), not for a domain concept.
- **Implementations live next to their feature** in the layer's feature folders
  (`…Setup`, `…Sessions`, …), under the feature namespace
  (`AgentContext.Application.Setup`, `AgentContext.Application.Sessions`, …).
- Implementation classes reference their interface via
  `using AgentContext.Application.Contracts;` and their DTOs via
  `using AgentContext.Application.Dtos;` — never by declaring the interface
  in the implementation file.
- A new application service therefore lands as two files:
  - `Contracts/IXxxAppService.cs` — the interface
  - `XxxAppService.cs` — the implementation, in the feature folder
  - DTOs used by the interface go in `Dtos/`

Reason: consumers (REST controllers, MCP tools, tests) depend only on the
contracts + dtos namespaces; the implementation graph stays swappable and the
seam (the `AddApplicationServices` boundary) stays visible in one place.

## Naming

- Application services end in `AppService` (`SetupAppService`, not `SetupService`).

## Configuration — platform settings live in the database

Platform settings (the LLM endpoint for the Learning Engine, ADR 0003, and
future settings) are **stored in the `settings` table**, not in
`appsettings.json`, environment variables, or compose config. The platform is
setter-uppable at runtime: a settings change applies without a restart.

- The `AppSetting` entity (`Domain/Entities`) is a key/value row; keys are
  declared in `Application/Settings/SettingKeys.cs` (`llm.baseUrl`, …).
- Settings are read/written through the `ISettingsAppService`
  (`Application/Contracts`) seam, implemented by `SettingsAppService`
  (`Application/Settings`). Validation happens on write
  (`SaveLlmOptionsAsync` throws on invalid input); a missing or invalid store
  reads back as `null` so consumers can idle instead of failing.
- Services that depend on settings resolve them per call (e.g. `LlmClient`
  re-reads the endpoint on every extraction/embedding call) — never cache
  settings at construction time.
- The settings REST/UI surface is a later ticket; do not reintroduce app
  configuration as the source of truth.
