# Architecture Migration Phase 6: Physical Core Assembly

Phase 6 creates the first physical production assembly boundary. The stable domain, rule, and planning code now compiles into `BusbarAutomation.Core.dll`; SolidWorks automation remains in `TopToDown.exe`.

## Scope

Before Phase 6, namespace rules expressed the intended dependency direction, but all production source compiled into one executable.

After Phase 6:

```text
BusbarAutomation.Core.dll
  -> Domain
  -> Rules
  -> Planning

TopToDown.exe
  -> Application
  -> Reporting
  -> SolidWorks adapter
  -> CLI and Program
  -> project reference: BusbarAutomation.Core.dll
```

Only one class library is introduced. Application, Reporting, SolidWorks, and CLI remain in the executable so this phase does not multiply projects or change runtime composition.

## Boundary decisions

`BusbarAutomation.Core` references only `System` and `System.Core`. It has no SolidWorks Interop or repository `ReferenceDLL` reference.

Existing Core types remain `internal`. The new assembly grants access only to the existing `TopToDown` and `TopToDown.Tests` assemblies through `InternalsVisibleTo`. This avoids changing the complete business model into a public API before another real consumer exists.

The source namespaces are unchanged:

```text
BusbarAutomation.Core.Domain
BusbarAutomation.Core.Rules
BusbarAutomation.Core.Planning
```

No conversion DTO, interface layer, dependency-injection container, or CAD abstraction is added.

## Behavior preserved

Phase 6 does not change:

- device recognition, current parsing, profiles, or single/double-clamp selection;
- collector positions, lengths, routes, contact compensation, or end margins;
- hole patterns, diameters, positions, sketch planes, or cut directions;
- sheet-metal radius, K factor, width mode, or feature order;
- fastener selection, preflight messages, report content, or CLI options;
- SolidWorks scanning, part generation, staged verification, replacement, or final geometry verification.

All Domain, Rules, and Planning source files are moved without content changes.

## Automated boundary tests

`Phase6PhysicalProjectBoundaryTests` verifies:

- the Core project file contains no SolidWorks or `ReferenceDLL` reference;
- the compiled Core assembly does not reference SolidWorks Interop;
- `TopToDown.csproj` references the Core project;
- the executable no longer compiles `Domain`, `Rules`, or `Planning` source paths.

The Phase 4 source boundary test now follows the real two-project directory layout.

## Validation criteria

Phase 6 is accepted only when all of the following pass:

1. Debug and Release tests pass: `31/31`.
2. Build output is ordered as `BusbarAutomation.Core.dll -> TopToDown.exe -> TopToDown.Tests.dll`.
3. The approved planning baseline remains byte-for-byte unchanged with SHA-256 `FBF95E3CF2CD381D331B189860B43E80FDCD9CFDE32688627D22151E7FC70812`.
4. Core has no source, project, or compiled assembly reference to SolidWorks.
5. Validation-only mode reports `errors=0, warnings=0, info=60` without modifying the assembly.
6. Existing approved geometry passes `--verify-geometry` with `errors=0, warnings=0, info=283`.
7. No SolidWorks feature implementation or planning formula differs from Phase 5.

Because this phase only moves unchanged pure C# source behind a project reference, a new 28-part generation is required only if runtime loading, validation, or existing-geometry verification exposes a regression. It is not necessary to recreate identical parts solely because their planning types now come from a DLL.

## Risk and rollback

The risks are assembly loading and accessibility, not geometry:

- `BusbarAutomation.Core.dll` could be missing beside `TopToDown.exe`;
- an internal Core type could become inaccessible to the executable or tests;
- the executable could accidentally keep compiling a second copy of Core source.

The build and boundary tests detect these failures before SolidWorks starts.

Rollback is mechanical:

1. Move `Domain`, `Rules`, and `Planning` back under `TopToDown`.
2. Restore their compile entries in `TopToDown.csproj`.
3. Remove the Core project reference and project.
4. Remove the Core solution entry and Phase 6 tests.

No business source content, CAD source, generated part, or assembly file needs to be reverted.

## Validation record

Executed on 2026-08-12.

- Debug automated tests: 31/31 passed.
- Release automated tests: 31/31 passed.
- Core, executable, and test projects compiled successfully in dependency order.
- Phase 4 and Phase 6 source/project boundary tests passed.
- Planning snapshot test passed without approving or rewriting the baseline; SHA-256 remained `FBF95E3CF2CD381D331B189860B43E80FDCD9CFDE32688627D22151E7FC70812`.
- Validation-only mode passed with `errors=0, warnings=0, info=60`.
- Existing 28-part geometry verification passed with `errors=0, warnings=0, info=283`.
- The repository assembly hash and last-write time were unchanged by both SolidWorks verification commands.
- A complete regeneration was not repeated because all planning sources were content-preserving moves, no CAD source changed, and the compiled DLL boundary was exercised by both planning and full existing-geometry verification.

The repository assembly `SWtopToDown/APITest.SLDASM` is not part of this phase and must remain unstaged.

## Remaining limits

- `Application` and `Reporting` remain in `TopToDown.exe`; splitting them is deferred until it reduces a concrete UI or packaging dependency.
- Core types are not a public SDK. A public API should be designed only when a second executable, UI project, or CAD backend actually consumes it.
- SolidWorks remains the only CAD backend. No generic CAD interface is restored in this phase.
