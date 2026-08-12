# 函数调用树

当前 `TopToDown` 由 CLI 组合根协调扫描、规划、建模、校验和报表。无参数运行完整生成；其他模式由 `GenerationOptionsParser` 严格解析。

## 1. 主调用树

```text
Program.Main(args)
├─ GenerationOptionsParser.Parse(args)
└─ new Cli.SolidWorksGenerationRunner(Settings, options).Run()
   ├─ new BusbarPlanningWorkflow(settings, phaseNames)
   │  └─ BusbarPreflightValidator.ValidateConfiguration(settings)
   ├─ SolidWorksSession.GetOrStartSolidWorks()
   ├─ SolidWorksSession.GetActiveOrOpenAssembly(swApp)
   ├─ new AssemblyReferencePointScanner(verbose).Scan(...)
   │  ├─ ReadReferencePointTemplates(...)
   │  ├─ ScanComponentReferencePoints(...)
   │  └─ AddFoundPoints(...) / TransformPoint(...)
   ├─ BusbarPlanningWorkflow.Build(scannedPoints, assemblyPath)
   │  ├─ AssemblySnapshotFactory.FromFoundPoints(...)
   │  │  └─ 识别刀熔、漏保、额定电流和命名端口
   │  ├─ BusbarPlanBuilder.BuildDesignPlan(...)
   │  │  ├─ ManualPortRuleProvider 创建端口
   │  │  ├─ CollectorLayoutPlanner 计算汇流排位置与长度
   │  │  ├─ BusbarRoutePlanner 生成路径
   │  │  └─ BusbarOverlapHolePlanner 展开搭接孔
   │  ├─ BusbarManufacturingPlanner.Build(...)
   │  │  ├─ ContactTopologyResolver 生成钣金草图线
   │  │  ├─ 建立每根铜排的钣金参数快照
   │  │  └─ FastenerPlanBuilder 生成螺栓计划
   │  └─ BusbarPreflightValidator.ValidatePlan(...)
   ├─ PreflightConsolePresenter.Print(report)
   ├─ [--validate] 输出预检后结束
   ├─ [--export-report] ProductionReportService.ExportForAssembly(...)
   ├─ [--verify-geometry] BusbarGeometryVerifier.Verify(...)
   ├─ [--preview] SolidWorksBusbarPartBuilder.CreateBusbarPreviewPart(...)
   └─ [完整生成] SolidWorksBusbarBatchGenerator
      ├─ SelectBusbars(plan)
      └─ Generate(...)
         ├─ foreach busbar: SolidWorksBusbarPartBuilder.CreateStagedSheetMetalPart(...)
         │  ├─ NewPartDocument(...)
         │  ├─ CreateSheetMetalOpenProfileSketch(...)
         │  ├─ CreateSheetMetalBaseFlangeFromSelectedSketch(...)
         │  ├─ CreateBusbarMountingHoles(...)
         │  ├─ SaveBusbarSheetMetalPart(...)
         │  ├─ InsertPartIntoAssembly(..., "StagedBusbar_*")
         │  └─ finally CloseBusbarPartDocument(...)
         ├─ BusbarGeometryVerifier.VerifyStaged(...)
         ├─ [失败] GeneratedComponentManager.DeleteSelected(...) + 删除本轮文件
         ├─ [通过] 重命名暂存组件
         └─ GeneratedComponentManager.DeleteExistingBusbars(...)
   ├─ BusbarGeometryVerifier.Verify(...)
   └─ [完整且校验通过] ProductionReportService.ExportForAssembly(...)
```

## 2. 关键入口

| 函数 | 位置 | 作用 |
| --- | --- | --- |
| `Parse` | `Cli/GenerationOptionsParser.cs` | 解析参数并拒绝未知参数、冲突模式和无效组合。 |
| `Run` | `Cli/SolidWorksGenerationRunner.cs` | 当前 CLI 组合根的流程协调入口；SolidWorks 文件夹只保留 CAD 操作。 |
| `Build` | `App/BusbarPlanningWorkflow.cs` | 统一执行配置快照、输入标准化、设计/制造规划和生成前预检。 |
| `Print` | `Cli/PreflightConsolePresenter.cs` | 将结构化预检报告渲染到控制台。 |
| `Scan` | `SolidWorks/AssemblyScanner.cs` | 读取命名参考点并转换为装配体坐标。 |
| `FromFoundPoints` | `App/AssemblySnapshotFactory.cs` | 将 SolidWorks 扫描结果标准化为设备、额定电流和命名端口。 |
| `BuildDesignPlan` | `BusbarAutomation.Core/Planning/BusbarPlanBuilder.cs` | 从标准化装配输入建立布局、逻辑路径和搭接孔。 |
| `Build` | `BusbarAutomation.Core/Planning/BusbarManufacturingPlanner.cs` | 补齐钣金草图线、钣金参数和螺栓选型。 |
| `ValidatePlan` | `BusbarAutomation.Core/Planning/BusbarPreflightValidator.cs` | 在 CAD 建模前检查规划契约。 |
| `SelectBusbars` / `Generate` | `SolidWorks/BusbarBatchGenerator.cs` | 固定批次顺序、`--only` 筛选、分阶段生成、暂存验证和替换组件。 |
| `CreateStagedSheetMetalPart` | `SolidWorks/BusbarPartBuilder.cs` | 创建单根零件、生成钣金与孔、保存，并在零件仍打开时暂存插入装配体。 |
| `CreateBusbarMountingHole` | `SolidWorks/MountingHoleBuilder.cs` | 在计算出的实体表面创建定向厚度切除。 |
| `Verify` / `VerifyStaged` | `SolidWorks/BusbarGeometryVerifier.cs` | 检查实体包络、实际圆柱孔贯穿和双排表面贴合。 |
| `ExportForAssembly` | `Reporting/ProductionReportService.cs` | 根据装配路径选择 `Reports` 目录，再调用报表 exporter。 |

## 3. 修改定位

- 改额定电流与铜排规格：`Program.cs` 的 `PhaseBranchRules` / `NeutralBranchRules`。
- 改汇流排位置与 X- 外伸：`Program.cs` 参数和 `BusbarAutomation.Core/Planning/CollectorLayoutPlanner.cs`。
- 改单双排路径：`BusbarAutomation.Core/Planning/BusbarPlanBuilder.cs`、`BusbarRoutePlanner.cs`、`ContactTopologyResolver.cs`。
- 改搭接孔型：`BusbarAutomation.Core/Rules/BusbarOverlapRuleMatrix.cs`、`Planning/BusbarOverlapHolePlanner.cs`。
- 改 SolidWorks 孔草图面或切除：`SolidWorks/MountingHoleBuilder.cs`，同时更新实体校验与实机测试。
- 改报表：`Reporting/ProductionReportExporter.cs`。

更细的函数说明见 [docs/SCRIPT_FUNCTION_GUIDE.md](docs/SCRIPT_FUNCTION_GUIDE.md)。
