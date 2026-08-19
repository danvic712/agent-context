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

## PostgreSQL persistence naming

All new PostgreSQL schema introduced by a feature must use unquoted lowercase
`snake_case` identifiers. This rule applies to tables, columns, sequences,
primary keys, foreign keys, indexes, unique constraints, and check constraints;
do not rely on EF Core's default CLR-name conversion or introduce quoted
PascalCase/camelCase identifiers.

- Table names are plural nouns (`inference_configurations`,
  `inference_routes`, `inference_providers`).
- Primary keys use `id`; foreign-key columns use the referenced entity name
  plus `_id` (`inference_configuration_id`, `provider_id`).
- Timestamp columns use the UTC suffix (`created_at_utc`, `updated_at_utc`).
- Indexes use `ix_<table>_<columns>`; unique constraints use
  `uq_<table>_<columns>`; foreign keys use
  `fk_<dependent_table>_<principal_table>_<column>`; check constraints use
  `ck_<table>_<purpose>`.
- EF Core mappings must explicitly set the database names with `ToTable`,
  `HasColumnName`, `HasIndex`, and `HasConstraintName` (or equivalent
  conventions) so generated migrations and the live PostgreSQL schema are
  visibly compliant.
- The three-table Inference implementation must use these exact persistence
  names: `inference_configurations`, `inference_routes`, and
  `inference_providers`. Route bindings belong in
  `inference_routes`; `inference_providers` must not contain a reverse
  `inference_configuration_id` foreign key.
- New migrations must be reviewed for lowercase `snake_case` identifiers
  before they are merged. Existing legacy tables are not renamed as part of
  an MVP feature unless the ticket explicitly includes that migration.

## Configuration — platform preferences and inference live in the database

Platform preferences (language and theme) are stored in the `settings` table,
while Learning Engine inference configuration follows ADR 0009 and is stored
in `inference_configurations`, `inference_routes`, and `inference_providers`.
Neither belongs in `appsettings.json`, environment variables, or compose config
as the runtime source of truth. Changes apply without a restart.

- The `AppSetting` entity (`Domain/Entities`) is a key/value row for preferences;
  keys are declared in `Application/Settings/SettingKeys.cs`.
- Inference contracts and DTOs live in `Application/Contracts` and
  `Application/Dtos`; persistence and verification are implemented in the
  `Application/Inference` feature folder and exposed under `/api/inference`.
- Provider API keys are protected before persistence and are write-only at the
  REST boundary. Reads return configured/masked state only.
- Services resolve the active inference routes per call; do not cache provider
  credentials or route configuration at construction time.
