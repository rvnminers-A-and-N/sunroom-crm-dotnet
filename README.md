# Sunroom CRM — .NET API

The shared REST API backend for [Sunroom CRM](https://sunroomcrm.net), built with ASP.NET Core 8, Entity Framework Core, and SQL Server. This API is consumed by the Angular, React, Vue, and Blazor frontends, and provides JWT-based authentication, full CRUD over the CRM domain, and AI-powered features through pluggable Ollama or stub implementations.

## About Sunroom CRM

Sunroom CRM is a multi-frontend CRM platform designed to demonstrate the same business requirements implemented across multiple modern frameworks — all sharing this single .NET 8 REST API and SQL Server database. The project showcases how different frontend ecosystems approach the same real-world problems: authentication, CRUD operations, real-time data visualization, drag-and-drop workflows, role-based access control, and AI-powered features.

### The Full Stack

| Repository | Technology | Description |
|------------|------------|-------------|
| **sunroom-crm-dotnet** (this repo) | .NET 8, EF Core, SQL Server | Shared REST API with JWT auth, AI endpoints, and Docker support |
| [sunroom-crm-angular](https://github.com/rvnminers-A-and-N/sunroom-crm-angular) | Angular 21, Material, Vitest | Angular frontend with 100% test coverage |
| [sunroom-crm-react](https://github.com/rvnminers-A-and-N/sunroom-crm-react) | React 19, shadcn/ui, Vitest | React frontend with 100% test coverage |
| [sunroom-crm-vue](https://github.com/rvnminers-A-and-N/sunroom-crm-vue) | Vue 3, Vuetify 4, Vitest | Vue frontend with 100% test coverage |
| [sunroom-crm-blazor](https://github.com/rvnminers-A-and-N/sunroom-crm-blazor) | Blazor Web App, .NET 8, MudBlazor | Blazor frontend with 100% test coverage |
| [sunroom-crm-laravel](https://github.com/rvnminers-A-and-N/sunroom-crm-laravel) | Laravel 13, Livewire 3 | Laravel full-stack implementation |

## Tech Stack

| Layer         | Technology                                          |
|---------------|-----------------------------------------------------|
| Framework     | ASP.NET Core 8 (Web API with controllers)           |
| ORM           | Entity Framework Core 8                             |
| Database      | SQL Server 2025 (production), InMemory (tests)      |
| Auth          | JWT Bearer tokens with BCrypt-hashed passwords      |
| AI            | Ollama integration with stub fallback               |
| API Docs      | Swashbuckle (Swagger / OpenAPI)                     |
| Testing       | xUnit + FluentAssertions + Moq + Bogus + EF Core InMemory |
| Integration   | Microsoft.AspNetCore.Mvc.Testing (`WebApplicationFactory`) |
| Coverage      | Coverlet + ReportGenerator                          |
| Container     | Docker + Docker Compose                             |
| CI/CD         | GitHub Actions                                      |
| Language      | C# 12 / .NET 8                                      |

## Features

- **JWT Authentication** — Login, registration, and token refresh with BCrypt-hashed passwords
- **Role-Based Authorization** — `Admin` and `User` roles enforced via `[Authorize]` attributes and policy-based access
- **Contacts API** — Full CRUD with search, tag filtering, pagination, and sorting
- **Companies API** — Company management with associated contacts and deals
- **Deals API** — CRUD plus stage transitions for the Kanban pipeline
- **Activities API** — Activity log linked to contacts and deals with type filtering
- **Dashboard API** — Aggregated metrics for stat cards and charts (pipeline value, deals by stage, recent activity)
- **AI Endpoints** — Natural language search, activity summarization, and deal insights via pluggable `IAiService` (Ollama or stub)
- **Tags API** — Tag CRUD with many-to-many relationships to contacts and deals
- **Users API** — Admin-only user management (list, update role, delete)
- **OpenAPI Documentation** — Swagger UI exposed at `/swagger` in Development
- **Docker Support** — Multi-stage Dockerfile and Compose file with SQL Server 2025 and a healthcheck

## Getting Started

### Prerequisites

- .NET 8 SDK
- Docker + Docker Compose (recommended) **or** SQL Server LocalDB / a remote SQL Server instance
- *(Optional)* [Ollama](https://ollama.ai/) running locally for live AI features (otherwise the stub service is used)

### Setup with Docker (recommended)

```bash
git clone https://github.com/rvnminers-A-and-N/sunroom-crm-dotnet.git
cd sunroom-crm-dotnet
cp .env.example .env   # Set SA_PASSWORD
docker compose up -d --build
```

This brings up SQL Server 2025 and the API together. The API is available at `http://localhost:5236` and Swagger UI at `http://localhost:5236/swagger`. Migrations run automatically on startup and the database is seeded with an admin user, sample companies, contacts, deals, activities, and tags.

### Setup without Docker

```bash
git clone https://github.com/rvnminers-A-and-N/sunroom-crm-dotnet.git
cd sunroom-crm-dotnet
dotnet restore
dotnet ef database update --project SunroomCrm.Infrastructure --startup-project SunroomCrm.Api
dotnet run --project SunroomCrm.Api
```

Configure your connection string in `SunroomCrm.Api/appsettings.Development.json` or via the `ConnectionStrings__DefaultConnection` environment variable.

### Default Credentials

The seed data creates an admin user you can log in with:

```
Email:    admin@sunroomcrm.net
Password: password123
```

## Available Commands

| Command                                                                   | Description                                       |
|---------------------------------------------------------------------------|---------------------------------------------------|
| `dotnet run --project SunroomCrm.Api`                                     | Start the API on http://localhost:5236            |
| `dotnet build SunroomCrm.sln`                                             | Build the entire solution                         |
| `dotnet test SunroomCrm.sln`                                              | Run the full xUnit test suite                     |
| `dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings` | Run tests with coverage collection                |
| `dotnet ef migrations add <Name> --project SunroomCrm.Infrastructure --startup-project SunroomCrm.Api` | Add a new EF Core migration                       |
| `dotnet ef database update --project SunroomCrm.Infrastructure --startup-project SunroomCrm.Api` | Apply migrations to the database                  |
| `docker compose up -d --build`                                            | Start API and SQL Server in Docker                |
| `docker compose down -v`                                                  | Stop containers and remove volumes (resets DB)    |

## Testing

516 tests across the unit and integration test suites, exercising controllers, repositories, services, middleware, EF Core configuration, and end-to-end HTTP request flows. Tests use [xUnit](https://xunit.net/) with [FluentAssertions](https://fluentassertions.com/) for expressive assertions, [Moq](https://github.com/devlooped/moq) for service mocking, [Bogus](https://github.com/bchavez/Bogus) for realistic test data generation, and EF Core's InMemory provider for isolated database tests.

### Unit Tests

Unit tests cover individual layers in isolation:

- **Controllers** — Each controller's actions are tested with mocked repositories and services
- **Repositories** — Each repository is tested against an InMemory `AppDbContext` with seeded data
- **Services** — `TokenService`, `OllamaAiService`, and `StubAiService` tested in isolation
- **Middleware** — Custom middleware (exception handling, request logging) tested with `DefaultHttpContext`
- **Data** — `AppDbContext` configuration and entity relationships verified

### Integration Tests

Integration tests use `WebApplicationFactory<Program>` from `Microsoft.AspNetCore.Mvc.Testing` to spin up the entire API in-process with an InMemory database. They exercise the full HTTP request pipeline — routing, model binding, authentication, authorization, controllers, repositories, and EF Core — to assert real end-to-end behavior:

- **Auth** — Login, registration, and JWT issuance flows
- **Contacts / Companies / Deals / Activities** — Full CRUD HTTP cycles with auth
- **Tags / AI / Dashboard** — Endpoint behavior including role-based access
- **Users / Admin** — Admin-only operations and role enforcement
- **Tenant Isolation** — Each test gets a fresh database scope so tests don't bleed state

```bash
dotnet test SunroomCrm.sln --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

## CI/CD Pipeline

GitHub Actions runs a single `Build, Test & Coverage` job on every push and pull request to `main`:

- Restores NuGet packages with caching
- Verifies code formatting with `dotnet format --verify-no-changes`
- Builds the solution in Release configuration
- Runs the full xUnit test suite with coverlet coverage collection
- Generates a ReportGenerator HTML coverage report
- Appends the Markdown summary to the GitHub Actions job summary
- Enforces a configurable line coverage gate (`MIN_LINE_COVERAGE` env var, currently 80%)
- Uploads test results (`.trx`) and the full coverage report as build artifacts

## Architecture

The solution follows a clean three-layer architecture with a shared core, a data/infrastructure layer, and the API host:

```
SunroomCrm.Core/                  # Domain layer (no external dependencies)
  Entities/                       # User, Company, Contact, Deal, Activity, Tag, AiInsight
  DTOs/                           # Request/response contracts grouped by feature
    Activities/                   # Activity DTOs
    AI/                           # AI request and response DTOs
    Auth/                         # Login, register, and token DTOs
    Common/                       # Shared response wrappers
    Companies/ Contacts/ Dashboard/ Deals/ Tags/   # Feature-scoped DTOs
  Enums/                          # Domain enums (DealStage, ActivityType, UserRole)
  Interfaces/                     # Repository and service abstractions
SunroomCrm.Infrastructure/        # Data access and external services
  Data/                           # AppDbContext, EF Core configuration, SeedData
  Migrations/                     # EF Core migrations
  Repositories/                   # IRepository implementations (Activity, Company, Contact, Deal, Tag, User, AiInsight)
  Services/                       # TokenService (JWT), OllamaAiService, StubAiService
SunroomCrm.Api/                   # ASP.NET Core Web API host
  Controllers/                    # 9 controllers (Activities, AI, Auth, Companies, Contacts, Dashboard, Deals, Tags, Users)
  Middleware/                     # Custom middleware (exception handling, request logging)
  Extensions/                     # Service registration extensions
  Program.cs                      # Composition root with DI, JWT, Swagger, CORS
SunroomCrm.Tests/                 # xUnit test project
  Unit/                           # Unit tests for controllers, repositories, services, middleware, data
  Integration/                    # End-to-end tests via WebApplicationFactory
  Helpers/                        # Test fixtures, Bogus data builders, auth helpers
.github/workflows/                # CI pipeline configuration
docker-compose.yml                # SQL Server 2025 + API orchestration
Dockerfile                        # Multi-stage build for the API image
```

### Key Patterns

- **Clean Architecture** — `Core` defines entities, DTOs, and interfaces with no framework dependencies; `Infrastructure` implements repositories and external service clients; `Api` is the composition root and HTTP entry point
- **Repository Pattern** — Each aggregate root has an `IRepository` abstraction with an EF Core implementation, keeping controllers free of `DbContext` references
- **DTO Mapping** — Controllers accept and return DTOs, never entities, so the public contract is decoupled from the database schema
- **JWT Authentication** — `TokenService` issues signed JWTs with role claims; `AuthController` handles login/register; `[Authorize]` and policy-based attributes enforce access control
- **Pluggable AI Service** — `IAiService` is implemented by both `OllamaAiService` (live LLM via local Ollama) and `StubAiService` (deterministic responses for tests and offline use); the active implementation is selected via configuration
- **Bogus Data Builders** — Test helpers use [Bogus](https://github.com/bchavez/Bogus) to generate realistic seed data so tests assert against meaningful inputs without hand-coded fixtures
- **WebApplicationFactory** — Integration tests spin up the entire app in-process with an InMemory database, asserting real HTTP behavior including auth, routing, and middleware
- **Custom Middleware** — Exception-handling middleware converts unhandled exceptions to JSON `ProblemDetails`; request logging middleware captures method, path, status, and duration
- **EF Core Migrations** — Schema changes are tracked through versioned migrations; the Docker entrypoint applies pending migrations on startup
- **SQL Server Healthcheck** — Compose uses a log-based healthcheck (`grep "SQL Server is now ready"`) so the API only starts after SQL Server finishes initialization

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
