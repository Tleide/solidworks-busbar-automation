# Architecture Migration Phase 5: CAD Batch and Part Builder Boundary

Phase 5 separates SolidWorks batch lifecycle management from single-part construction. The change preserves planning formulas, feature creation order, hole geometry, staged verification, and component replacement behavior.

## Scope

Before Phase 5, `SolidWorksBusbarPartBuilder` owned both batch state and the SolidWorks operations used to create one part.

After Phase 5:

```text
Cli.SolidWorksGenerationRunner
  -> SolidWorksBusbarBatchGenerator
      -> SelectBusbars
      -> Generate
          -> SolidWorksBusbarPartBuilder.CreateStagedSheetMetalPart
          -> BusbarGeometryVerifier.VerifyStaged
          -> GeneratedComponentManager
  -> BusbarGeometryVerifier.Verify
```

`SolidWorksBusbarBatchGenerator` owns:

- canonical main-feed, collector, branch, and neutral generation order;
- `--only` filtering without changing canonical order;
- staged component collection and geometry verification;
- staged rollback and generated-file cleanup;
- component naming and replacement of existing generated busbars.

`SolidWorksBusbarPartBuilder` owns one-part construction:

1. Create and activate a part document.
2. Create the open-profile sheet-metal sketch.
3. Create the base flange.
4. Create mounting-hole cuts.
5. Rebuild and log the part bounds.
6. Save the part.
7. Insert it into the assembly while the part document remains open.
8. Close the temporary part document in `finally`.

The builder keeps the existing partial files for sketch, sheet-metal, hole, persistence, preview, and geometry helpers. Splitting those tested feature implementations into more classes is not required by this phase.

## Behavior preserved

Phase 5 does not change:

- busbar profiles, layout coordinates, paths, contact compensation, or end margins;
- branch single/double-clamp selection or ABC/N behavior;
- hole count, diameter, position, sketch plane, or cut direction;
- sheet-metal radius, K factor, width mode, or SolidWorks feature order;
- the requirement to insert a newly saved part before closing its document;
- staged geometry verification, rollback, old-component replacement, or final verification;
- command-line behavior for full generation, `--only`, `--keep-existing`, `--preview`, `--validate`, `--verify-geometry`, and `--export-report`.

## Automated boundary tests

`Phase5CadBuilderBoundaryTests` protects the behavior most likely to drift during this split:

- a complete plan selects all 28 busbars in the approved manufacturing order;
- `--only` input order does not reorder the canonical batch.

The approved planning baseline SHA-256 remains:

```text
FBF95E3CF2CD381D331B189860B43E80FDCD9CFDE32688627D22151E7FC70812
```

## Validation criteria

Phase 5 is accepted only when all of the following pass:

1. Debug automated tests pass: `29/29`.
2. Release automated tests pass: `29/29`.
3. The approved planning baseline SHA-256 is unchanged.
4. No source or project file references the removed `BusbarBatchBuilder.cs` or its old batch method names.
5. Validation-only mode reports `errors=0, warnings=0, info=60` without modifying the assembly.
6. A local A-phase collector plus lower/upper branch generation reports `errors=0, warnings=0, info=38`, with through holes and both clamp surfaces correct.
7. Complete generation creates all 28 parts, passes staged verification, replaces the previous generated set, and reports final geometry `errors=0, warnings=0, info=283`.
8. Independent `--verify-geometry` reports `errors=0, warnings=0, info=283`.

## Risk and rollback

The main risk is accidentally changing the order around `SaveBusbarSheetMetalPart`, `InsertPartIntoAssembly`, and `CloseBusbarPartDocument`. `AddComponent5` has previously been unreliable when the newly saved part was closed before insertion, so this order remains inside one builder method.

Rollback is limited to the Phase 5 boundary files:

1. Restore `SolidWorks/BusbarBatchBuilder.cs`.
2. Remove `SolidWorks/BusbarBatchGenerator.cs` and `SolidWorks/BusbarPartBuilder.cs`.
3. Restore the former builder construction and calls in `Cli/SolidWorksGenerationRunner.cs`.
4. Restore the project file compile entry and remove the Phase 5 tests.

No planning, rule, geometry formula, feature helper, generated part, or assembly file needs to be reverted.

## Validation record

Executed on 2026-08-12 against:

```text
C:\Users\10718\AppData\Local\Temp\SWApiDesign_Phase3_20260812\APITest.SLDASM
```

The repository assembly `SWtopToDown/APITest.SLDASM` was not used for acceptance testing and is not part of this phase.

- Debug automated tests: 29/29 passed.
- Release automated tests: 29/29 passed.
- Planning baseline SHA-256 unchanged.
- Configuration and plan preflight: errors=0, warnings=0, info=60.
- Three-part A-phase clamp generation: errors=0, warnings=0, info=38.
- Complete 28-part generation, staged verification, replacement, and report export succeeded.
- Final and independent geometry verification: errors=0, warnings=0, info=283.
- Report: `C:\Users\10718\AppData\Local\Temp\SWApiDesign_Phase3_20260812\Reports\Busbar_ProductionReport_20260812_190734.xlsx`.

## Remaining limits

- The SolidWorks builder remains a partial class because its feature implementations share tightly related private CAD helpers. Further splitting is useful only when a concrete maintenance problem appears.
- Batch generation and part construction still compile into the same executable. Physical project separation is deferred until the namespace boundaries have remained stable through later phases.
- COM and real geometry behavior cannot be proved by unit tests; any future change to sketch planes, feature order, cuts, save/insert order, or component replacement still requires a real SolidWorks acceptance run.
