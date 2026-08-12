# Architecture Migration Phase 2: Stable Input and Configuration Snapshots

Phase 2 separates assembly interpretation from busbar design without changing any engineering formula or SolidWorks API call. The production output remains `TopToDown.exe`, and all code is still compiled into one assembly.

## Scope

The runtime flow is now:

```text
SolidWorks assembly
  -> AssemblyReferencePointScanner
  -> List<FoundPoint> (raw CAD scan result)
  -> AssemblySnapshotFactory
  -> AssemblySnapshot (normalized devices, rated current, ports, coordinates)
  -> BusbarPlanBuilder
  -> BusbarPlan
  -> SolidWorks builders / validation / report

Program settings
  -> configuration preflight
  -> EngineeringConfigurationSnapshot
  -> BusbarPlanBuilder
```

## Assembly snapshot

`AssemblySnapshot` contains the normalized input required by planning:

- one recognized fuse;
- ordered breaker devices;
- stable device identity and original SolidWorks component name;
- breaker rated current;
- named ports and assembly-space coordinates.

Component-name recognition and rated-current parsing now exist only in `App/AssemblySnapshotFactory.cs`. `BusbarPlanBuilder` does not inspect or parse SolidWorks component names. A test proves that the planner accepts normalized breaker names without current tokens when `RatedCurrentA` is already present.

`FoundPoint` remains as a transitional raw scanner DTO. It is not part of the planning contract anymore.

## Engineering configuration snapshot

`EngineeringConfigurationSnapshot` takes a deep copy of the current `BusbarSettings` after configuration preflight succeeds. The snapshot freezes one planning run against later mutation of:

- busbar profiles and ABC/N branch selection tables;
- fastener dimensions and standard lengths;
- collector layout and extension parameters;
- sheet-metal parameters;
- arrangement overrides and double-clamp parameters.

The approved overlap-hole matrix is copied into `BusbarOverlapRuleCatalog` for the same run. Both planning and preflight query this catalog instead of reaching back to a static matrix independently.

The data source is still the hardcoded configuration in `Program.cs`, identified as `hardcoded-v1`. Excel and JSON are intentionally not integrated in this phase.

`BusbarSettings` remains a transitional internal representation used by existing planners and validators. Replacing every method signature would add risk without improving the Phase 2 boundary.

## Behavior preserved

Phase 2 does not change:

- device naming rules accepted at the SolidWorks input boundary;
- branch profile selection or single/double-clamp selection;
- collector positions, lengths and extensions;
- route, bend compensation or contact-face formulas;
- hole patterns, diameters, positions or cut behavior;
- fastener selection;
- SolidWorks sketch, sheet-metal, assembly or verification APIs;
- CLI commands and `TopToDown.exe` output name.

## Validation criteria

Phase 2 is accepted only when all of the following pass:

1. Debug and Release automated tests pass with no skipped tests.
2. The approved representative plan remains byte-for-byte unchanged.
3. Baseline SHA-256 remains `FBF95E3CF2CD381D331B189860B43E80FDCD9CFDE32688627D22151E7FC70812`.
4. Snapshot tests prove device normalization, configuration isolation and planner independence from component-name current parsing.
5. Real `--validate` completes with `errors=0, warnings=0` and does not modify the assembly.
6. The Phase 0 SolidWorks modeling gate is run before Phase 3 begins.

## Rollback

Phase 2 can be reverted as one isolated phase. Restore the runner call to the former raw-point planning entry, remove the snapshot files/tests, and restore the former device-recognition methods in `BusbarPlanBuilder`. No SolidWorks geometry code or project data needs to be reverted.

## Remaining transitional dependencies

- `BusbarSettings` is still mutable and shared by existing planners through per-run copies.
- `SolidWorksGenerationRunner` still combines application orchestration, CAD operations and report dispatch.
- `SheetMetalOptions.FromRules` still couples a domain model to the rules namespace.
- Physical project/assembly boundaries remain deferred until Phase 6.

## Validation record

Executed on 2026-08-12:

- Debug tests: 20/20 passed, 0 skipped.
- Release tests: 20/20 passed, 0 skipped.
- Approved planning snapshot SHA-256 remained `FBF95E3CF2CD381D331B189860B43E80FDCD9CFDE32688627D22151E7FC70812`.
- `git diff --check` passed; only repository line-ending conversion warnings were reported.
- Planning source contains no `FoundPoint`, component-name hint, or rated-current regular-expression dependency.
- Real `--validate` against the temporary acceptance assembly scanned 30 reference points and passed with `errors=0, warnings=0`; validation did not modify the assembly.
- Existing 28-part geometry verification passed with `errors=0, warnings=0`.
- Complete 28-part regeneration, staged verification, assembly replacement and final geometry verification passed with `errors=0, warnings=0`.
- A second independent `--verify-geometry` run after generation passed with `errors=0, warnings=0`.
- Production report export succeeded at `C:\Users\10718\AppData\Local\Temp\SWApiDesign_Phase1_20260812_125641\Reports\Busbar_ProductionReport_20260812_150731.xlsx`.
- All SolidWorks validation used the temporary acceptance copy. The repository assembly `SWtopToDown/APITest.SLDASM` was not opened or modified by Phase 2 validation.
