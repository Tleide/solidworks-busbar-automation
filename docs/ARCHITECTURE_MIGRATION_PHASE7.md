# Architecture Migration Phase 7: Application Assembly

Phase 7 extracts input normalization and the planning workflow into `BusbarAutomation.Application.dll`. This gives a future UI a reusable application boundary without moving any SolidWorks API code or duplicating planning logic.

## Scope

After Phase 7 the production dependency graph is:

```text
BusbarAutomation.Core.dll
  -> Domain / Rules / Planning

BusbarAutomation.Application.dll
  -> AssemblySnapshotFactory
  -> BusbarPlanningWorkflow
  -> GenerationOptions
  -> BusbarAutomation.Core.dll

TopToDown.exe
  -> CLI and Program
  -> SolidWorks scanner and CAD builders
  -> Reporting
  -> BusbarAutomation.Application.dll
  -> BusbarAutomation.Core.dll
```

The executable remains the SolidWorks composition root. Reporting and CAD are intentionally not extracted in this phase.

## Boundary decisions

`BusbarAutomation.Application` contains the workflow that is useful to CLI and future UI consumers:

- `AssemblySnapshotFactory` converts scanner output into normalized assembly input;
- `BusbarPlanningWorkflow` freezes configuration, builds design/manufacturing plans, and returns structured preflight results;
- `GenerationOptions` stores per-run mode flags.

The application project references only `System`, `System.Core`, and `BusbarAutomation.Core`. It has no SolidWorks, Reporting, CLI, or executable reference.

Existing application types remain `internal`. `InternalsVisibleTo` grants access only to `TopToDown` and `TopToDown.Tests`; the project is reusable as an application boundary, but it is not prematurely presented as a public SDK.

No UI framework, presenter abstraction, dependency-injection container, or command-handler hierarchy is introduced.

## Behavior preserved

Phase 7 does not change:

- device recognition and rated-current parsing;
- branch profile and single/double-clamp selection;
- collector positions, lengths, routes, and contact compensation;
- overlap-hole patterns, dimensions, positions, and cut directions;
- fastener planning and preflight messages;
- SolidWorks scanning, preview, generation, staged verification, replacement, and geometry verification;
- report contents and all CLI modes.

The three Application source files are moved without content changes.

## Automated boundary tests

`Phase7ApplicationAssemblyBoundaryTests` verifies:

- the Application project references Core and has no SolidWorks or `ReferenceDLL` reference;
- the compiled Application assembly references Core but not SolidWorks or `TopToDown`;
- `TopToDown.csproj` references Application and no longer compiles `App` source files.

The existing non-CAD source test follows the new physical directories.

## Validation criteria

Phase 7 is accepted only when all of the following pass:

1. Debug and Release tests pass: `33/33`.
2. Build output is ordered as `Core -> Application -> TopToDown -> Tests`.
3. The approved planning baseline remains byte-for-byte unchanged with SHA-256 `FBF95E3CF2CD381D331B189860B43E80FDCD9CFDE32688627D22151E7FC70812`.
4. Application and Core compiled assemblies contain no SolidWorks Interop reference.
5. Validation-only mode reports `errors=0, warnings=0, info=60` without modifying the assembly.
6. Existing approved geometry passes `--verify-geometry` with `errors=0, warnings=0, info=283`.
7. No planning formula, feature builder, generated part, or CAD call order changes.

Because this phase moves only three unchanged application source files and the new DLL boundary is exercised by both planning and full geometry verification, identical 28-part regeneration is not repeated solely for the move.

## Risk and rollback

The risks are assembly loading and accidental dependency inversion:

- `BusbarAutomation.Application.dll` could be missing beside `TopToDown.exe`;
- the application assembly could reference the executable or SolidWorks;
- the main project could keep a second copy of `App` source;
- internal types could become inaccessible across the new boundary.

Build output and boundary tests detect these failures before SolidWorks generation.

Rollback is mechanical:

1. Move `App` back under `TopToDown`.
2. Restore the three `App` compile entries in `TopToDown.csproj`.
3. Remove Application project references, solution entry, and `InternalsVisibleTo` entry.
4. Remove the Application project and Phase 7 test.

No Core source, planning rule, CAD source, generated part, or assembly file needs to be reverted.

## Validation record

Executed on 2026-08-12.

- Debug automated tests: 33/33 passed.
- Release automated tests: 33/33 passed.
- Core, Application, executable, and test projects compiled successfully in dependency order.
- Planning snapshot test passed without approval or rewrite; baseline SHA-256 remained `FBF95E3CF2CD381D331B189860B43E80FDCD9CFDE32688627D22151E7FC70812`.
- Validation-only mode passed with `errors=0, warnings=0, info=60`.
- Existing 28-part geometry verification passed with `errors=0, warnings=0, info=283`.
- The repository assembly hash and last-write time were unchanged by both SolidWorks verification commands.
- Complete regeneration was not repeated because all Application sources were content-preserving moves, no Core or CAD implementation changed, and the new assembly boundary was exercised by planning and full existing-geometry verification.
- Repository assembly `SWtopToDown/APITest.SLDASM` remains unstaged and is not part of this phase.

## Remaining limits

- Reporting remains in `TopToDown.exe` because it still participates in the current CLI output flow and has no second consumer yet.
- `GenerationOptions` and planning results remain internal. A UI-facing public contract should be designed when the UI project is actually started.
- SolidWorks remains the only CAD backend; no generic CAD interface is added.
