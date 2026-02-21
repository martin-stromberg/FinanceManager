# .NET 10 Upgrade Plan

## Table of Contents
- [Executive Summary](#executive-summary)
- [Migration Strategy](#migration-strategy)
- [Detailed Dependency Analysis](#detailed-dependency-analysis)
- [Project-by-Project Plans](#project-by-project-plans)
- [Package Update Reference](#package-update-reference)
- [Breaking Changes Catalog](#breaking-changes-catalog)
- [Testing & Validation Strategy](#testing--validation-strategy)
- [Risk Management](#risk-management)
- [Complexity & Effort Assessment](#complexity--effort-assessment)
- [Source Control Strategy](#source-control-strategy)
- [Success Criteria](#success-criteria)

## Executive Summary
This plan upgrades the FinanceManager solution from `net9.0` to `net10.0` across all projects in a single coordinated operation.

**Scope**
- Projects: 7 total (Class Libraries, Blazor/AspNetCore web app, and test projects)
- Target framework: `net10.0` for all projects
- Issues identified: 468 total (Mandatory: 53, Potential: 413, Optional: 2)
- Affected files: 74
- Key feature area: IdentityModel & claims-based security (noted in web/infrastructure)

**Complexity Classification**: **Complex**
- Dependency depth is 5 levels (Shared → Domain → Application → Infrastructure → Web → Tests)
- High issue volume with mandatory API incompatibilities in `FinanceManager.Infrastructure` and `FinanceManager.Web`
- Large projects by file count (`FinanceManager.Web` 351 files, `FinanceManager.Infrastructure` 224 files, `FinanceManager.Shared` 174 files)

**Selected Strategy**: **All-At-Once Strategy**
- Single coordinated upgrade to keep the dependency chain consistent and avoid mixed framework states.
- Requires a dedicated upgrade branch and a unified build/test cycle after all updates.

**Expected Iterations**: 8 total (skeleton + dependency analysis + strategy + project detail batches + final criteria).

## Migration Strategy
**Approach**: **All-At-Once Strategy** — upgrade all projects simultaneously to `net10.0` with a single coordinated package update and build/repair cycle.

**Rationale**
- Single dependency chain with no cycles.
- Unified framework state reduces mismatch risk across the layered architecture.
- Assessment recommends consistent package alignment to .NET 10 versions.

**Phases (logical, not project-by-project):**
1. **Preparation**
   - Ensure .NET 10 SDK is available and global.json (if present) aligns with `net10.0`.
   - Confirm upgrade branch and commit state are ready.
2. **Atomic Upgrade (single coordinated batch)**
   - Update `TargetFramework` to `net10.0` for all projects.
   - Update all packages with suggested versions (see Package Update Reference).
   - Resolve breaking changes and compilation errors, starting with foundation projects in dependency order.
3. **Test Validation**
   - Run unit and integration tests after the solution builds cleanly.

**Dependency-based ordering (for fixes within the atomic phase):**
`Shared` → `Domain` → `Application` → `Infrastructure` → `Web` → `Tests`.

**Parallelization**: Projects are upgraded together; any fixes follow dependency order to avoid cascading compile errors.

## Detailed Dependency Analysis
**Dependency graph summary (bottom-up):**
- **Level 0 (Foundation)**: `FinanceManager.Shared`
- **Level 1**: `FinanceManager.Domain` → depends on `Shared`
- **Level 2**: `FinanceManager.Application` → depends on `Domain`, `Shared`
- **Level 3**: `FinanceManager.Infrastructure` → depends on `Domain`, `Application`
- **Level 4**: `FinanceManager.Web` → depends on `Shared`, `Infrastructure`, `Domain`, `Application`
- **Level 5 (Tests)**: `FinanceManager.Tests`, `FinanceManager.Tests.Integration` → depend on `Web` and lower layers

**Critical path:** `Shared` → `Domain` → `Application` → `Infrastructure` → `Web` → `Tests`

**Circular dependencies:** None reported in assessment.

**All-at-once implication:** All projects update to `net10.0` together, but dependency order must be respected when resolving build errors (foundation libraries first, tests last).

## Project-by-Project Plans

### Project: `FinanceManager.Shared`
**Current State**: `net9.0`, ClassLibrary, 174 files, 189 issues (primarily behavioral changes).
**Target State**: `net10.0`.
**Dependencies**: none. **Dependents**: Domain, Application, Infrastructure, Web, Tests, Tests.Integration.
**Risk Level**: High (large file count, foundational dependency, many behavioral-change issues).

**Migration Steps**
1. Update `TargetFramework` to `net10.0`.
2. Review behavioral change issues (`Api.0003`) affecting shared utilities and extension methods.
3. Validate public API compatibility for downstream projects.
4. Build as part of the atomic solution build.

**Package Updates**: No explicit package updates listed for this project in assessment.

**Validation Checklist**
- [ ] Builds without errors after global upgrade
- [ ] No behavioral change regressions in shared helpers

---

### Project: `FinanceManager.Domain`
**Current State**: `net9.0`, ClassLibrary, 42 files, 6 issues.
**Target State**: `net10.0`.
**Dependencies**: `FinanceManager.Shared`. **Dependents**: Application, Infrastructure, Web, Tests.
**Risk Level**: Low.

**Migration Steps**
1. Update `TargetFramework` to `net10.0`.
2. Apply package updates listed below.
3. Review any API changes surfaced by compilation.

**Package Updates (Suggested Versions)**
| Package | Current | Target | Reason |
|---|---|---|---|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 9.0.11 | 10.0.3 | Framework alignment |

**Validation Checklist**
- [ ] Builds without errors after global upgrade

---

### Project: `FinanceManager.Application`
**Current State**: `net9.0`, ClassLibrary, 55 files, 6 issues.
**Target State**: `net10.0`.
**Dependencies**: `FinanceManager.Domain`, `FinanceManager.Shared`. **Dependents**: Infrastructure, Web, Tests.
**Risk Level**: Low–Medium.

**Migration Steps**
1. Update `TargetFramework` to `net10.0`.
2. Apply package updates listed below.
3. Address any compile-time API changes (`Api.0002`) discovered during build.

**Package Updates (Suggested Versions)**
| Package | Current | Target | Reason |
|---|---|---|---|
| `Microsoft.AspNetCore.Cryptography.KeyDerivation` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.EntityFrameworkCore` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.Extensions.Caching.Memory` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.Extensions.Hosting.Abstractions` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.Extensions.Identity.Core` | 9.0.11 | 10.0.3 | Framework alignment |

**Validation Checklist**
- [ ] Builds without errors after global upgrade

---

### Project: `FinanceManager.Infrastructure`
**Current State**: `net9.0`, ClassLibrary, 224 files, 41 issues.
**Target State**: `net10.0`.
**Dependencies**: `FinanceManager.Domain`, `FinanceManager.Application`. **Dependents**: Web, Tests.
**Risk Level**: High (mandatory binary incompatibilities, IdentityModel usage).

**Migration Steps**
1. Update `TargetFramework` to `net10.0`.
2. Apply package updates listed below.
3. Resolve `Api.0001` (binary incompatibilities) and `Api.0002` (source incompatibilities) first.
4. Review behavioral changes (`Api.0003`), especially security/token handling.

**Package Updates (Suggested Versions)**
| Package | Current | Target | Reason |
|---|---|---|---|
| `Microsoft.EntityFrameworkCore` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.EntityFrameworkCore.Design` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.Extensions.Http` | 9.0.11 | 10.0.3 | Framework alignment |

**Validation Checklist**
- [ ] Builds without errors after global upgrade
- [ ] IdentityModel/claims handling reviewed for behavioral changes

---

### Project: `FinanceManager.Web` (Blazor)
**Current State**: `net9.0`, AspNetCore (Blazor), 351 files, 147 issues.
**Target State**: `net10.0`.
**Dependencies**: `Shared`, `Infrastructure`, `Domain`, `Application`. **Dependents**: Tests, Tests.Integration.
**Risk Level**: High (largest project, mandatory binary incompatibilities, IdentityModel usage).

**Migration Steps**
1. Update `TargetFramework` to `net10.0`.
2. Apply package updates listed below.
3. Resolve `Api.0001`/`Api.0002` incompatibilities, prioritizing authentication, authorization, and hosting pipeline.
4. Review behavioral changes (`Api.0003`) affecting auth, DI, and startup configuration.

**Package Updates (Suggested Versions)**
| Package | Current | Target | Reason |
|---|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.AspNetCore.Cryptography.KeyDerivation` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.EntityFrameworkCore` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.EntityFrameworkCore.Design` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.Extensions.Caching.Memory` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.Extensions.Hosting.Abstractions` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.Extensions.Http` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.Extensions.Identity.Core` | 9.0.11 | 10.0.3 | Framework alignment |

**Validation Checklist**
- [ ] Builds without errors after global upgrade
- [ ] Blazor app starts and authentication flows compile and run

---

### Project: `FinanceManager.Tests`
**Current State**: `net9.0`, DotNetCoreApp, 107 files, 76 issues.
**Target State**: `net10.0`.
**Dependencies**: Web, Infrastructure, Domain, Application, Shared.
**Risk Level**: Medium (test framework and behavioral-change issues).

**Migration Steps**
1. Update `TargetFramework` to `net10.0`.
2. Apply package updates listed below.
3. Address any test failures due to API or behavior changes in upstream projects.

**Package Updates (Suggested Versions)**
| Package | Current | Target | Reason |
|---|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.0.11 | 10.0.3 | Framework alignment |
| `Microsoft.EntityFrameworkCore.InMemory` | 9.0.11 | 10.0.3 | Framework alignment |

**Outdated Package (No suggested version)**
- `xunit` 2.9.3 is marked obsolete. ⚠️ Requires validation of the recommended replacement/version.

**Validation Checklist**
- [ ] All unit tests pass

---

### Project: `FinanceManager.Tests.Integration`
**Current State**: `net9.0`, DotNetCoreApp, 27 files, 3 issues.
**Target State**: `net10.0`.
**Dependencies**: Web, Shared.
**Risk Level**: Low.

**Migration Steps**
1. Update `TargetFramework` to `net10.0`.
2. Apply package updates listed below.

**Package Updates (Suggested Versions)**
| Package | Current | Target | Reason |
|---|---|---|---|
| `Microsoft.AspNetCore.Mvc.Testing` | 9.0.11 | 10.0.3 | Framework alignment |

**Outdated Package (No suggested version)**
- `xunit` 2.9.3 is marked obsolete in assessment. ⚠️ Requires validation of the recommended replacement/version.

**Validation Checklist**
- [ ] All integration tests pass

## Package Update Reference
### Common Package Updates (Suggested Versions)
| Package | Current | Target | Projects Affected | Update Reason |
|---|---|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.0.11 | 10.0.3 | Web, Tests | Framework alignment |
| `Microsoft.AspNetCore.Cryptography.KeyDerivation` | 9.0.11 | 10.0.3 | Application, Domain, Infrastructure, Web, Tests | Framework alignment |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 9.0.11 | 10.0.3 | Domain, Infrastructure, Web, Tests | Framework alignment |
| `Microsoft.AspNetCore.Mvc.Testing` | 9.0.11 | 10.0.3 | Tests.Integration | Framework alignment |
| `Microsoft.EntityFrameworkCore` | 9.0.11 | 10.0.3 | Application, Domain, Infrastructure, Web, Tests | Framework alignment |
| `Microsoft.EntityFrameworkCore.Design` | 9.0.11 | 10.0.3 | Infrastructure, Web | Framework alignment |
| `Microsoft.EntityFrameworkCore.InMemory` | 9.0.11 | 10.0.3 | Tests | Framework alignment |
| `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.11 | 10.0.3 | Infrastructure, Web, Tests | Framework alignment |
| `Microsoft.Extensions.Caching.Memory` | 9.0.11 | 10.0.3 | Application, Domain, Infrastructure, Web, Tests | Framework alignment |
| `Microsoft.Extensions.Hosting.Abstractions` | 9.0.11 | 10.0.3 | Application, Infrastructure, Web, Tests | Framework alignment |
| `Microsoft.Extensions.Http` | 9.0.11 | 10.0.3 | Infrastructure, Web, Tests | Framework alignment |
| `Microsoft.Extensions.Identity.Core` | 9.0.11 | 10.0.3 | Application, Domain, Infrastructure, Web, Tests | Framework alignment |

### Outdated Packages (No Suggested Version)
| Package | Current | Projects Affected | Guidance |
|---|---|---|---|
| `xunit` | 2.9.3 | Tests, Tests.Integration | ⚠️ Marked obsolete; verify replacement/version in upstream docs |

## Breaking Changes Catalog
**Primary breaking-change categories (from assessment):**
- **Api.0001 (Binary incompatible)** — requires recompilation; impacts `FinanceManager.Infrastructure` and `FinanceManager.Web` most heavily.
- **Api.0002 (Source incompatible)** — code changes required to compile under `net10.0`.
- **Api.0003 (Behavioral changes)** — runtime behavior differences; most prevalent in `FinanceManager.Shared`, `FinanceManager.Web`, and `FinanceManager.Tests`.

**Key focus areas**
- Authentication/authorization and token handling (IdentityModel & claims-based security).
- Hosting and DI configuration changes for .NET 10.
- EF Core and provider behavior changes (`Microsoft.EntityFrameworkCore.*`).

**Reference**
- .NET breaking changes: https://go.microsoft.com/fwlink/?linkid=2262679

## Testing & Validation Strategy
**After Atomic Upgrade Build**
- Build entire solution; resolve all compile-time issues before testing.

**Test Projects to Execute**
- `FinanceManager.Tests`
- `FinanceManager.Tests.Integration`

**Validation Checklist (solution-wide)**
- [ ] All projects build with `net10.0` and no errors
- [ ] No unresolved package dependency conflicts
- [ ] Unit tests pass (`FinanceManager.Tests`)
- [ ] Integration tests pass (`FinanceManager.Tests.Integration`)
- [ ] No warnings promoted to errors (NU1605/CS1591)

## Risk Management
### High-Risk Items
| Project | Risk Level | Risk Description | Mitigation |
|---|---|---|---|
| `FinanceManager.Shared` | High | Widespread behavioral-change issues; foundational library | Validate shared APIs early in build-fix cycle; add focused unit tests for critical helpers |
| `FinanceManager.Infrastructure` | High | Binary/source incompatibilities and IdentityModel usage | Resolve `Api.0001/Api.0002` first; review auth/token handling paths |
| `FinanceManager.Web` | High | Largest project, auth pipeline changes, Blazor runtime behavior | Prioritize build fixes in auth/DI/startup; run integration tests after compile fixes |

### Security Vulnerabilities
- No security vulnerability findings were reported in the assessment.

### Contingency Plans
- If API changes cause widespread failures, isolate by dependency order and validate upstream projects before downstream fixes.
- If IdentityModel changes impact authentication flows, add targeted tests around token validation and claims mapping.

## Complexity & Effort Assessment
### Per-Project Complexity
| Project | Complexity | Drivers |
|---|---|---|
| `FinanceManager.Shared` | High | 189 issues, many behavioral changes, foundational dependency |
| `FinanceManager.Domain` | Low | Small file count, limited package updates |
| `FinanceManager.Application` | Low–Medium | Multiple package updates, moderate size |
| `FinanceManager.Infrastructure` | High | Mandatory binary/source incompatibilities, IdentityModel usage |
| `FinanceManager.Web` | High | Largest project, auth pipeline changes, many API issues |
| `FinanceManager.Tests` | Medium | Test framework changes, behavioral issues |
| `FinanceManager.Tests.Integration` | Low | Small scope, few updates |

**Overall Complexity**: Complex (depth 5 dependency chain; high issue volume).

## Source Control Strategy
- Use the existing upgrade branch `79-upgrade-auf-net-10-2` for the entire atomic upgrade.
- Prefer a **single commit** that includes all TargetFramework updates, package updates, and compilation fixes.
- If multiple commits are necessary, keep them tightly scoped (e.g., `chore: upgrade framework/packages`, `fix: resolve net10 build errors`).
- Require PR review and CI validation before merging to mainline.

## Success Criteria
### Technical
- All projects target `net10.0`.
- All package updates with suggested versions are applied.
- Solution builds with zero errors and no unresolved dependency conflicts.
- All tests pass (`FinanceManager.Tests`, `FinanceManager.Tests.Integration`).

### Quality
- No new warnings promoted to errors (NU1605/CS1591).
- Identity/authentication flows compile and operate with expected behavior.

### Process
- All-at-once strategy executed as a single coordinated upgrade.
- Source control strategy followed with upgrade branch and review.
