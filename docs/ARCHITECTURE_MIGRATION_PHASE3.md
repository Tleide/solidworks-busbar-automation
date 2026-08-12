# Architecture Migration Phase 3: Design and Manufacturing Plans

Phase 3 separates logical busbar design from manufacturing preparation without changing any engineering formula, hole result, SolidWorks API call, report layout, or CLI command. The production output remains `TopToDown.exe`, and all code is still compiled into one assembly.

## Runtime flow

```text
AssemblySnapshot + EngineeringConfigurationSnapshot
  -> BusbarPlanBuilder.BuildDesignPlan
  -> BusbarDesignPlan
       devices and selected profiles
       collector layouts and tap topology
       busbar endpoints and logical centerlines
       overlap mounting-hole definitions
  -> BusbarManufacturingPlanner.Build
  -> BusbarManufacturingPlan
       per-busbar sheet-metal options
       compensated sheet-metal sketch lines
       selected fastener joints
  -> preflight / report / SolidWorks preview and generation
```

## Boundary decisions

`BusbarPlanBuilder` no longer creates:

- `SheetMetalOptions`;
- `SheetMetalSketchLine`;
- collector fastener selections.

`BusbarManufacturingPlanner` is now the only step that derives those values. This also removes `SheetMetalOptions.FromRules`, so the sheet-metal domain model no longer reaches into the rules namespace to construct itself.

Overlap holes remain in the design step for now. Their pattern and positions are produced while the connected busbar and collector tap topology are updated together. Moving them separately in this phase would require copying or synchronizing a mutable port graph and would increase regression risk without providing a useful boundary yet.

## Transitional object sharing

The two plan containers are explicit, but Phase 3 deliberately reuses the same `Busbar` instances. Manufacturing planning fills `SheetMetal` and `SheetMetalSketchLine` on the design busbars instead of cloning the complete route, port, profile, and hole graph.

This is a controlled transitional limitation:

- there is one authoritative route and hole graph;
- no conversion layer can drift from the approved geometry;
- repeated manufacturing planning replaces derived values and does not accumulate points or fasteners;
- consumers receive `BusbarManufacturingPlan`, so CAD and reporting cannot accidentally run before manufacturing preparation.

Deep immutable plan models are deferred until a real requirement justifies their conversion cost, such as concurrent plan variants, persistence, or a second CAD backend.

## Behavior preserved

Phase 3 does not change:

- device recognition or rated-current selection;
- ABC/N profile and single/double-clamp rules;
- collector positions, lengths, extensions, routes, or bend compensation;
- hole pattern, diameter, coordinate, face, sketch plane, or cut direction;
- bend radius, K factor, sheet-metal width side, or thicken direction;
- fastener selection formulas and standard lengths;
- report workbook content;
- SolidWorks generation, replacement, assembly, or geometry verification APIs;
- `TopToDown.exe` and existing command-line options.

## Validation criteria

Phase 3 is accepted only when all of the following pass:

1. Debug and Release automated tests pass with no skipped tests.
2. The representative manufacturing snapshot remains byte-for-byte unchanged.
3. Baseline SHA-256 remains `FBF95E3CF2CD381D331B189860B43E80FDCD9CFDE32688627D22151E7FC70812`.
4. A design-only test proves all 28 busbars have no sheet-metal options or sketch line before manufacturing planning.
5. A manufacturing test proves all 28 busbars receive sheet-metal data and all 30 fastener joints remain valid.
6. Repeating manufacturing planning produces the same snapshot without accumulated derived data.
7. Real `--validate` passes without modifying the acceptance assembly.
8. Existing generated geometry passes `--verify-geometry`.
9. A complete 28-part generation and final geometry verification pass on a temporary acceptance copy.

## Rollback

Phase 3 is isolated from SolidWorks geometry code. To roll it back:

1. restore `BusbarPlanBuilder.BuildPlan` as the single planning entry;
2. move sheet-metal sketch preparation and fastener planning back to the end of that method;
3. restore consumers to the former `BusbarPlan` type;
4. remove `BusbarPlans.cs`, `BusbarManufacturingPlanner.cs`, and the Phase 3 boundary tests.

No CAD feature builder, generated part, or production assembly needs to be reverted.

## Remaining work

- Preflight validation and reporting are still separate consumers but remain dispatched by `SolidWorksGenerationRunner`; Phase 4 will move application orchestration out of the CAD namespace.
- SolidWorks builders remain partial files around one builder class; Phase 5 will split them incrementally without changing feature creation order.
- Physical project and assembly boundaries remain deferred until Phase 6.

## Validation record

Executed on 2026-08-12:

- Debug tests: 23/23 passed, 0 skipped.
- Release tests: 23/23 passed, 0 skipped.
- Approved snapshot SHA-256 remained `FBF95E3CF2CD381D331B189860B43E80FDCD9CFDE32688627D22151E7FC70812`.
- Design/manufacturing boundary and repeated-build tests passed.
- Real `--validate` scanned 30 reference points and passed with `errors=0, warnings=0, info=60`.
- Existing 28-part geometry passed `--verify-geometry` with `errors=0, warnings=0, info=283`.
- Complete generation created all 28 new sheet-metal parts and exported `Busbar_ProductionReport_20260812_165520.xlsx`.
- Independent post-generation `--verify-geometry` passed with `errors=0, warnings=0, info=283`.
- SolidWorks acceptance used `C:\Users\10718\AppData\Local\Temp\SWApiDesign_Phase3_20260812\APITest.SLDASM`; the repository assembly was not used or overwritten.
