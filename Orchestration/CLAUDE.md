# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

This is an early-stage .NET 10 solution, largely unmodified from the `dotnet new webapi` template. Most of
the domain logic is stubbed out (`NotImplementedException`, empty classes) and the API controller/model
still contains the template's WeatherForecast sample rather than real endpoints. Expect to be building out
core functionality, not just extending a working system.

## Commands

```bash
# Restore, build, run (from repo root)
dotnet build Orchestration.sln
dotnet run --project Orchestration.WebAPI

# Build a single project
dotnet build Orchestration.DomainServices/Orchestration.DomainServices.csproj

# Docker (see compose.yaml / Orchestration.WebAPI/Dockerfile)
docker compose up --build
```

There are no test projects in the solution yet — `dotnet test` has nothing to run.

The WebAPI listens on `http://localhost:5199` (and `https://localhost:7160` under the `https` launch
profile); see `Orchestration.WebAPI/Properties/launchSettings.json`. `Orchestration.WebAPI/Orchestration.WebAPI.http`
has example requests for use with the VS Code/Rider HTTP client.

## Architecture

Three-project layered solution, referenced top-down:

- **Orchestration.Domain** — plain domain models/enums, no dependencies (e.g. `Models/Instrument.cs`,
  `Enums/InstrumentStatus.cs`). Records and enums live here.
- **Orchestration.DomainServices** — business logic. Service interfaces live under `BusinessLogic/Core/`
  (e.g. `IInstrumentService`), implementations at the project root (e.g. `InstrumentService`). Intended
  in-memory persistence stub is `BusinessLogic/InMemoryDataStore.cs` (currently empty).
- **Orchestration.WebAPI** — ASP.NET Core Web API host. Controllers under `Controllers/`. Standard
  minimal-hosting `Program.cs` (controllers + OpenAPI, no auth/persistence wired up yet).

**Important gotcha:** none of the `.csproj` files currently declare `<ProjectReference>` entries between
these three projects — Domain, DomainServices, and WebAPI compile as independent, unlinked assemblies. This
is easy to miss because the solution still builds cleanly: `InstrumentService.cs` and `IInstrumentService.cs`
both have a stray `using System.Diagnostics.Metrics;`, and BCL's `System.Diagnostics.Metrics.Instrument`
silently satisfies the unqualified `Instrument` reference instead of `Orchestration.Domain.Models.Instrument`.
When wiring these projects together for real, add the missing `ProjectReference`s and remove that using
directive — otherwise the code compiles against the wrong `Instrument` type entirely. `InstrumentsController`
in WebAPI is not yet wired to `IInstrumentService`, and no DI registration for it exists in `Program.cs`.

Target framework across all projects is `net10.0` with nullable reference types and implicit usings enabled.
