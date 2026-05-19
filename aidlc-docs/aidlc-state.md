# AI-DLC State Tracking — Dayswork

## Project Information
- **Project Name**: Dayswork
- **Description**: Stardew Valley SMAPI mod that lets the player hire generic worker NPCs from the bulletin board
- **Project Type**: Greenfield
- **Start Date**: 2026-05-18
- **Current Phase**: CONSTRUCTION
- **Current Stage**: U-04 Geometry & Domain Primitives — Functional Design (Awaiting Approval)

## Workspace State
- **Existing Code**: No
- **Programming Languages**: (to be: C# / .NET targeting Stardew Valley + SMAPI)
- **Build System**: (to be: MSBuild / `dotnet`)
- **Project Structure**: Empty (greenfield)
- **Reverse Engineering Needed**: No
- **Workspace Root**: `C:\Users\kwood\Repos\dayswork`

## Code Location Rules
- **Application Code**: Workspace root (NEVER in `aidlc-docs/`)
- **Documentation**: `aidlc-docs/` only
- **Source spec**: `aidlc-docs/inception/source-spec.md` (copy of user-provided design doc)

## User Profile (informs onboarding-level docs)
- Experienced software engineer
- New to C# / .NET
- New to Stardew Valley modding / SMAPI ecosystem
- Implication: tech stack rationale, project scaffolding, and SMAPI conventions need to be documented explicitly during Inception and Construction.

## Extension Configuration
| Extension | Enabled | Mode | Decided At |
|---|---|---|---|
| Security Baseline | No | — | Requirements Analysis (Q28: B — no network/PII/auth surface) |
| Property-Based Testing | Yes | Partial — enforces PBT-02, PBT-03, PBT-07, PBT-08, PBT-09; remainder advisory | Requirements Analysis (Q29: B) |

**PBT framework**: FsCheck (per PBT-09 recommendation for C#/.NET; integrates with xUnit chosen in Q4).

## Stage Progress
### INCEPTION PHASE
- [x] Workspace Detection
- [ ] Reverse Engineering (N/A — greenfield)
- [x] Requirements Analysis (approved 2026-05-18)
- [x] User Stories (approved 2026-05-18)
- [x] Workflow Planning (approved 2026-05-18)
- [x] Application Design (approved 2026-05-18, with live-docs verification addendum)
- [x] Units Generation — approved 2026-05-18

### CONSTRUCTION PHASE (per-unit loop)
- [ ] Functional Design — **EXECUTE** (per unit)
- [ ] NFR Requirements — **EXECUTE** (per unit)
- [ ] NFR Design — **EXECUTE** (per unit)
- [ ] Infrastructure Design — **SKIP** (no cloud / container / IaC; SMAPI is the platform)
- [ ] Code Generation — **EXECUTE** (per unit, always)
- [ ] Build and Test — **EXECUTE** (after all units complete)

### OPERATIONS PHASE
- [ ] Operations — **PLACEHOLDER** (v1 ships without deployment automation)
