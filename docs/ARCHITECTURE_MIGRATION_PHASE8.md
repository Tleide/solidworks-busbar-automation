# Architecture Migration Phase 8: Reporting Assembly and Final Boundary

Phase 8 extracts production reporting into `BusbarAutomation.Reporting.dll`. This completes the planned physical separation of reusable non-CAD code while leaving the proven SolidWorks adapter and CLI composition root together.

## Scope

After Phase 8 the production dependency graph is:

```text
BusbarAutomation.Core.dll
  -> Domain / Rules / Planning

BusbarAutomation.Application.dll
  -> input normalization and planning workflow
  -> BusbarAutomation.Core.dll

BusbarAutomation.Reporting.dll
  -> production workbook generation and output-path selection
  -> BusbarAutomation.Core.dll

TopToDown.exe
  -> CLI and Program
  -> SolidWorks scanner and CAD builders
  -> BusbarAutomation.Application.dll
  -> BusbarAutomation.Reporting.dll
  -> BusbarAutomation.Core.dll
```

The two Reporting source files are moved without content changes. The executable remains the composition root and the only production assembly that references SolidWorks Interop.

## Boundary decisions

`BusbarAutomation.Reporting` owns:

- transforming a complete `BusbarManufacturingPlan` into the seven production workbook sheets;
- writing the Open XML `.xlsx` package without Excel or a third-party package;
- choosing the assembly-adjacent `Reports` output directory.

The Reporting project references only the .NET Framework libraries required for file, ZIP, LINQ, and XML operations plus `BusbarAutomation.Core`. It does not reference Application, TopToDown, SolidWorks, or repository `ReferenceDLL` files.

Existing reporting types remain `internal`. Explicit friend access is granted to `TopToDown` and `TopToDown.Tests`; no public SDK, report interface, DI container, or presenter abstraction is introduced.

## Behavior preserved

Phase 8 does not change:

- report sheet names, order, columns, formulas, values, file naming, or output directory;
- device recognition, planning, busbar profiles, paths, holes, or fastener selection;
- command-line parsing or the behavior of `--export-report`;
- any SolidWorks API call, feature order, generated part, component replacement, or geometry verification.

## Automated tests

`Phase8ReportingAssemblyBoundaryTests` verifies:

- Reporting references Core and contains no SolidWorks or executable dependency;
- TopToDown references Reporting and no longer compiles Reporting source files;
- the representative 28-busbar manufacturing plan exports a readable `.xlsx` package containing `xl/workbook.xml` and seven worksheet XML files.

The existing non-CAD source test follows the new physical Reporting directory.

## Validation criteria

Phase 8 is accepted only when all of the following pass:

1. Debug and Release tests pass: `36/36`.
2. Build output is ordered as `Core -> Application / Reporting -> TopToDown -> Tests`.
3. The approved planning baseline remains unchanged with SHA-256 `FBF95E3CF2CD381D331B189860B43E80FDCD9CFDE32688627D22151E7FC70812`.
4. Core, Application, and Reporting compiled assemblies contain no SolidWorks Interop reference.
5. `--validate` reports `errors=0, warnings=0, info=60`.
6. `--export-report` creates a workbook and reports that the assembly was not modified.
7. Existing geometry passes `--verify-geometry` with `errors=0, warnings=0, info=283`.
8. The repository assembly hash and last-write time remain unchanged by validation.

## Validation record

Executed on 2026-08-12.

- Debug automated tests: 36/36 passed.
- Release automated tests: 36/36 passed.
- The new report smoke test generated and opened the seven-sheet workbook package successfully.
- Validation-only mode passed with `errors=0, warnings=0, info=60`.
- Report-only mode exported `Busbar_ProductionReport_20260812_204144.xlsx` beside the active temporary validation assembly and completed without modifying the assembly.
- Existing 28-part geometry verification passed with `errors=0, warnings=0, info=283`.
- Repository `SWtopToDown/APITest.SLDASM` retained SHA-256 `452358940C9801667DC3AC64D6649B470567048F5409151ED4B0431E81ED8A2B` and the same last-write time throughout the command-chain verification.
- Complete regeneration was not repeated because Reporting sources were content-preserving moves and no planning or CAD implementation changed.

## Risk and rollback

The risks are limited to assembly loading and report output:

- `BusbarAutomation.Reporting.dll` could be absent from the executable directory;
- Reporting could accidentally gain a CAD or executable dependency;
- TopToDown could compile a second source copy;
- report-only mode could fail to produce a valid workbook.

Build output, boundary tests, and the workbook smoke test detect these failures. Rollback is mechanical: move the two files back to `TopToDown/Reporting`, restore their compile entries, remove the Reporting project references and friend assembly entry, and delete the Reporting project and Phase 8 test.

## Migration result

The Phase 0 to Phase 8 architecture migration is complete at the intended boundary:

- stable domain, rules, and planning are physically isolated in Core;
- reusable planning orchestration is physically isolated in Application;
- reusable production reporting is physically isolated in Reporting;
- SolidWorks-specific code remains concentrated in one executable adapter;
- no speculative generic CAD abstraction or UI framework has been introduced.

Further work should return to approved engineering rules and product features. A new physical CAD contract should be extracted only when a second backend such as NXOpen is actually implemented.
