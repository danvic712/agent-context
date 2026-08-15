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
