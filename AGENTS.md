# Backend Agent Instructions

Read root `/AGENTS.md` first.
Read `/docs/16-backend-foundation.md` before changing the backend solution, projects, build settings, or foundational configuration.
Read `/docs/17-backend-clean-architecture.md` before changing backend project references, layer responsibilities, or dependency direction.
Read `/docs/18-data-access-strategy.md` before changing persistence, database packages, migrations, transactions, or database query architecture.
Read `/docs/19-database-foundation.md` before changing the DbContext, EF Core packages/tooling, SQL Server configuration, schema conventions, or migration workflow.
Read `/docs/20-authentication-architecture.md` before changing Identity entities, authentication endpoints, cookie/session behavior, password or lockout policy, antiforgery handling, or account lifecycle behavior.
Read `/docs/21-backend-application-api-quality.md` before adding or changing backend commands, queries, handlers, validators, pipeline behaviors, Identity capability abstractions, controller responsibilities, exception handling, or ProblemDetails conventions.
Read `/docs/22-backend-authorization.md` before changing permissions, roles, authorization policies, protected CQRS requests, permission persistence/resolution, `/me` permission data, or access-control boundaries.
Read `/docs/23-audit-logging.md` before adding or changing audit intent, audit metadata, audit persistence, auditable requests, audit transaction semantics, append-only enforcement, or security/business logging boundaries.
Read `/docs/24-local-development-authentication.md` before changing Development identity fixtures, their command gating, local authentication setup, or the frontend/backend local session workflow.
Read `/docs/25-mfa-authentication.md` before changing MFA policy, authenticator enrollment, MFA/recovery challenges, temporary MFA cookies, or MFA frontend/backend flows.
Read `/docs/26-user-management.md` before changing user-directory, provisioning, profile/status/MFA administration, or User Management API contracts.
Read `/docs/27-password-recovery.md` before changing forgot/reset password endpoints, Identity reset tokens, password-recovery email delivery, trusted reset URLs, or related configuration.
Read `/docs/28-dynamic-navigation.md` before changing navigation metadata persistence, navigation filtering, hierarchy, or the navigation API.
Read `/docs/30-form-template-administration.md` before changing form-template uploads, PDF validation, script policy, template administration endpoints, or private template content handling.

Applies to `/backend`.

Technology:
- .NET 10
- ASP.NET Core Web API
- Clean Architecture
- SQL Server / Azure SQL
- Azure Blob Storage
- Azure Key Vault

Expected projects:

```text
src/
├── CareComply.Api/
├── CareComply.Application/
├── CareComply.Domain/
└── CareComply.Infrastructure/
```

Dependency:
- Domain -> no infrastructure implementation
- Application -> Domain
- Infrastructure -> Application/Domain as required
- Api -> composition

Do not automatically add:
- MediatR
- generic repositories
- Unit of Work abstraction
- event bus
- microservices
- message broker
- CQRS frameworks

Authorization:
Backend is security authority.
Every protected operation enforces permission.
Use the canonical Application `Permissions` constants, endpoint plus CQRS defense in depth, and the scoped server-side resolver. Never authorize from role names, frontend state, client headers, or permission claims cached in the authentication cookie.

Documents:
Never store protected files in `wwwroot`.
Use private Azure Blob.
Authorize all access.

Logging:
Never log secrets, passwords, access tokens, or unnecessary PHI.
Business audit must be explicit and allowlisted. Never serialize arbitrary requests, results, entities, exceptions, credentials, cookies, tokens, or request bodies into audit storage. `IAuditExecution` is not a generic Unit of Work.

Authentication:
Use the approved ASP.NET Core Identity cookie architecture. Keep authentication separate from permission authorization, require antiforgery validation for authenticated unsafe requests, and never add public registration or seeded credentials without an approved task.

APIs:
Follow `/docs/07-api-standards.md`.
Use ProblemDetails.
Use DTOs/contracts.

Database:
Follow `/docs/06-database-rules.md`.
EF Core is the sole approved Phase 1 data-access technology.
CareComply uses EF Core Code First with SQL Server/Azure SQL; EF migrations are the controlled schema history.
Do not install or use Dapper without a new approved architecture decision.
Do not add generic repositories, a generic Unit of Work, or repository-per-table architecture.
Do not run migrations or `EnsureCreated` automatically during API startup.
