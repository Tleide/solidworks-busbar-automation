# Architecture Migration Phase 4: Application Workflow and CAD Boundary

Phase 4 moves workflow orchestration out of the SolidWorks adapter namespace while preserving the existing planning formulas, CAD feature order, command-line modes, and generated geometry.

## Scope

Before Phase 4, `SolidWorksGenerationRunner` coordinated configuration validation, assembly scanning, planning, preflight, CAD generation, geometry verification, and report export from the CAD folder.

After Phase 4:

```text
Program
  -> Cli.SolidWorksGenerationRunner              composition root
      -> Application.BusbarPlanningWorkflow     configuration + planning + preflight
      -> SolidWorksSession                       CAD session
      -> AssemblyReferencePointScanner          CAD input adapter
      -> SolidWorksBusbarBatchGenerator          CAD batch generation
          -> SolidWorksBusbarPartBuilder         single-part CAD generation
      -> BusbarGeometryVerifier                  CAD read-only verification
      -> Reporting.ProductionReportService       report destination
          -> ProductionReportExporter            workbook generation
```

## Boundary decisions

`App/BusbarPlanningWorkflow.cs` owns the complete non-CAD planning sequence:

1. Validate startup settings.
2. Create `EngineeringConfigurationSnapshot`.
3. Convert scanned `FoundPoint` values into `AssemblySnapshot`.
4. Build `BusbarDesignPlan`.
5. Build `BusbarManufacturingPlan`.
6. Validate the manufacturing plan.
7. Return `BusbarPlanningResult` containing the plan and structured `BusbarPreflightReport`.

`Cli/PreflightConsolePresenter.cs` owns console formatting. `BusbarPreflightReport` is a result object and has no console dependency, so a future UI can consume the same messages without parsing terminal output.

`Reporting/ProductionReportService.cs` owns the small application concern of choosing the `Reports` directory from the assembly path. `ProductionReportExporter` remains responsible for workbook content.

`Cli/SolidWorksGenerationRunner.cs` is the composition root because the current executable still has one SolidWorks backend. It may reference both application and CAD types. The CAD folder contains only SolidWorks operations and no longer owns the top-level workflow coordinator.

At the end of Phase 4, `SolidWorksBusbarPartBuilder` received only the CAD-relevant generation settings and a report sink. It no longer depended on the full `GenerationOptions` object or on the application workflow type. Phase 5 subsequently moved those batch settings into `SolidWorksBusbarBatchGenerator`.

## Behavior preserved

Phase 4 does not change:

- supported component names or rated-current parsing;
- ABC/N branch profiles and single/double-clamp selection;
- collector positions, lengths, extensions, paths, or contact compensation;
- hole type, diameter, position, sketch face, or cut direction;
- sheet-metal radius, K factor, width mode, or generation order;
- staged insertion, replacement, cleanup, geometry verification, or report workbook content;
- command-line modes: default generation, `--validate`, `--verify-geometry`, `--export-report`, `--preview`, `--only`, and `--keep-existing`.

## Validation criteria

Phase 4 is accepted only when all of the following pass:

1. Debug tests pass: `27/27`.
2. Release tests pass: `27/27`.
3. The approved planning baseline remains SHA-256 `FBF95E3CF2CD381D331B189860B43E80FDCD9CFDE32688627D22151E7FC70812`.
4. The boundary test confirms `App`, `Domain`, `Planning`, `Reporting`, and `Rules` contain no `SolidWorks.Interop` references.
5. Temporary acceptance assembly `C:\Users\10718\AppData\Local\Temp\SWApiDesign_Phase3_20260812\APITest.SLDASM` passes `--validate` with `errors=0, warnings=0, info=60` and is unchanged by validation.
6. The same existing generated geometry passes `--verify-geometry` with `errors=0, warnings=0, info=283`.
7. Complete generation creates and stages 28 busbars, replaces the previous generated set only after staged verification, completes final geometry verification with `errors=0, warnings=0, info=283`, and exports a production report.
8. An independent post-generation `--verify-geometry` run passes with `errors=0, warnings=0, info=283`.

## Rollback

The phase is isolated to orchestration and presentation boundaries. To roll it back:

1. Restore `SolidWorksGenerationRunner.cs` to the `SolidWorks` directory and namespace.
2. Restore the former direct calls to `BusbarPreflightValidator`, `ProductionReportExporter`, and the old builder constructor.
3. Restore `BusbarPreflightReport.PrintToConsole()`.
4. Remove `BusbarPlanningWorkflow`, `PreflightConsolePresenter`, `ProductionReportService`, and the Phase 4 tests.

No route planner, feature builder, generated part, or business-rule file needs to be reverted.

## Known transitional limits

- The application and CAD layers are still compiled into one .NET Framework executable. Physical project separation is deferred until the boundaries survive another phase.
- `Cli.SolidWorksGenerationRunner` still contains execution-mode branching. Extracting separate command handlers now would add indirection without a second UI or backend requirement.
- `SolidWorksBusbarPartBuilder` still contained batch coordination at this phase boundary. Phase 5 separates batch lifecycle from single-part construction without changing feature implementations.

## Validation record

Executed on 2026-08-12 against the temporary acceptance assembly. The repository assembly `SWtopToDown/APITest.SLDASM` was not used for SolidWorks validation.

- Debug automated tests: 27/27 passed.
- Release automated tests: 27/27 passed.
- Planning baseline SHA-256 unchanged.
- Configuration and plan preflight: errors=0, warnings=0, info=60.
- Existing geometry verification before generation: errors=0, warnings=0, info=283.
- Complete 28-busbar generation, staged verification, replacement, final geometry verification, and report export succeeded.
- Independent post-generation geometry verification: errors=0, warnings=0, info=283.
- Report: `C:\Users\10718\AppData\Local\Temp\SWApiDesign_Phase3_20260812\Reports\Busbar_ProductionReport_20260812_182104.xlsx`.
