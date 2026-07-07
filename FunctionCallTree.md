# 函数调用树

当前 `TopToDown` 只保留一条主流程。无参数运行时会直接执行完整铜排生成；`--preview` 只生成骨架预览线。

## 1. Main 调用树

```text
Main(args)
├─ ConfigureFromArgs(args)
└─ RunSolidWorksGeneration()
   ├─ GetOrStartSolidWorks()
   ├─ GetActiveOrOpenAssembly(swApp)
   ├─ [默认实体生成时] DeleteExistingBusbarComponents(model, assembly)
   ├─ ScanReferencePoints(swApp, model, assembly)
   │  ├─ DumpModelFeatures(swApp, assemblyModel, null, null, foundPoints)
   │  │  └─ TryReadReferencePoint(...)
   │  └─ foreach component
   │     └─ DumpModelFeatures(swApp, componentModel, componentName, transform, foundPoints)
   │        └─ TryReadReferencePoint(...)
   │           └─ TransformPoint(swApp, point, transform)
   ├─ BusbarPlanBuilder.BuildPlanFromScannedAssembly(foundPoints, PhaseNames, Settings)
   │  ├─ ManualBusbarRuleSet.CreateDefault(...)
   │  ├─ new ManualPortRuleProvider(rules)
   │  ├─ new CollectorLayoutPlanner(rules, settings)
   │  ├─ new BusbarRoutePlanner(settings)
   │  ├─ new ContactTopologyResolver()
   │  ├─ FindFuseComponent(...)
   │  ├─ FindLoubaoGroups(...)
   │  ├─ foreach phase A/B/C
   │  │  ├─ ManualPortRuleProvider.CreateFuseOutPort(...)
   │  │  ├─ ManualPortRuleProvider.CreateLoubaoInPort(...)
   │  │  ├─ CollectorLayoutPlanner.CreateLayout(...)
   │  │  │  └─ BusbarLengthController.Calculate(...)
   │  │  ├─ CollectorLayoutPlanner.CreateTap(...)
   │  │  ├─ CreateBusbar(MainFeed)
   │  │  │  ├─ BusbarRoutePlanner.CreateRoute(...)
   │  │  │  └─ ContactTopologyResolver.CreateSheetMetalSketchLine(...)
   │  │  ├─ CreateBusbar(Branch)
   │  │  │  ├─ BusbarRoutePlanner.CreateRoute(...)
   │  │  │  └─ ContactTopologyResolver.CreateSheetMetalSketchLine(...)
   │  │  └─ CreateCollectorBusbar(...)
   │  │     └─ ContactTopologyResolver.CreateSheetMetalSketchLine(...)
   │  └─ AddNeutralCollectorAndBranches(...)
   │     ├─ CreateNeutralLoubaoInputs(...)
   │     ├─ CollectorLayoutPlanner.CreateLayout(N)
   │     ├─ CreateBusbar(N Branch)
   │     └─ CreateCollectorBusbar(N)
   ├─ [如果 --preview]
   │  └─ CreateBusbarPreviewPart(swApp, model, assembly, plan)
   └─ [默认生成实体]
      ├─ SelectBusbarsForSheetMetalBatch(plan)
      └─ CreateBusbarSheetMetalParts(swApp, model, assembly, busbars)
         └─ foreach busbar
            └─ CreateBusbarSheetMetalPart(...)
               ├─ NewPartDocument(swApp)
               ├─ CreateBusbarSheetMetalFeature(...)
               │  ├─ GetOpenProfilePlane(...)
               │  ├─ CreateSheetMetalOpenProfileSketch(...)
               │  └─ CreateSheetMetalBaseFlangeFromSelectedSketch(...)
               ├─ CreateBusbarMountingHoles(...)
               │  └─ CreateBusbarMountingHole(...)
               │     ├─ GetHoleSketchPlane(...)
               │     ├─ CreateOffsetPlane(...)
               │     ├─ CreateDirectedBlindCutFromActiveSketch(...)
               │     └─ CreateDirectedBlindCutFromSketch(...) fallback
               ├─ SaveBusbarSheetMetalPart(...)
               ├─ InsertPartIntoAssembly(...)
               └─ CloseBusbarPartDocument(...)
```

## 2. 主要函数说明

| 函数 | 位置 | 输入 | 输出 | 职责 |
| --- | --- | --- | --- | --- |
| `ConfigureFromArgs` | `Program.cs` | 命令行参数 | 全局运行开关 | 支持 `--verbose`、`--keep-existing`、`--preview`。 |
| `RunSolidWorksGeneration` | `SolidWorks/SolidWorksGenerationRunner.cs` | 全局设置和运行开关 | 生成流程执行结果 | 当前 SolidWorks 后端主线。 |
| `ScanReferencePoints` | `SolidWorks/AssemblyScanner.cs` | SW 装配体 | `List<FoundPoint>` | 扫描装配体和组件参考点。 |
| `BuildPlanFromScannedAssembly` | `Planning/BusbarPlanBuilder.cs` | 参考点、相序、设置 | `BusbarPlan` | 生成所有铜排业务对象。 |
| `SelectBusbarsForSheetMetalBatch` | `SolidWorks/BusbarBatchBuilder.cs` | `BusbarPlan` | `List<Busbar>` | 按生成顺序选择主排、汇流排、分支排和 N 排。 |
| `CreateBusbarSheetMetalPart` | `SolidWorks/BusbarBatchBuilder.cs` | 单根 `Busbar` | SolidWorks 零件 | 创建钣金实体、孔、保存并装配。 |
| `CreateSheetMetalBaseFlangeFromSelectedSketch` | `SolidWorks/SheetMetalFeatureBuilder.cs` | 草图、规格 | `Feature` | 调用 SW API 生成 MidPlane 钣金。 |
| `CreateBusbarMountingHole` | `SolidWorks/MountingHoleBuilder.cs` | `ConnectionPort` | Cut Feature | 按孔中心画圆并切除。 |

## 3. CAD 后端抽象方向

当前流程仍由 `RunSolidWorksGeneration` 直接驱动 SolidWorks 方法。后续可将：

```text
Busbar
-> SheetMetalPartSpec
-> ICadSheetMetalBuilder.CreateSheetMetalPart(spec)
```

作为新的 CAD 后端边界。SolidWorks 后端实现该接口；如果后续新增 UG/NXOpen，则新增另一个实现，不改 `Domain`、`Rules`、`Planning`。
