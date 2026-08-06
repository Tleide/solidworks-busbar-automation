# 函数调用树

当前 `TopToDown` 由一个 Runner 协调扫描、规划、建模、校验和报表。无参数运行完整生成；其他模式由 `GenerationOptionsParser` 严格解析。

## 1. 主调用树

```text
Program.Main(args)
├─ GenerationOptionsParser.Parse(args)
└─ new SolidWorksGenerationRunner(Settings, options).Run()
   ├─ BusbarPreflightValidator.ValidateConfiguration(settings)
   ├─ SolidWorksSession.GetOrStartSolidWorks()
   ├─ SolidWorksSession.GetActiveOrOpenAssembly(swApp)
   ├─ new AssemblyReferencePointScanner(verbose).Scan(...)
   │  ├─ ReadReferencePointTemplates(...)
   │  ├─ ScanComponentReferencePoints(...)
   │  └─ AddFoundPoints(...) / TransformPoint(...)
   ├─ BusbarPlanBuilder.BuildPlanFromScannedAssembly(...)
   │  ├─ 识别刀熔与漏保额定电流
   │  ├─ ManualPortRuleProvider 创建端口
   │  ├─ CollectorLayoutPlanner 计算汇流排位置与长度
   │  ├─ BusbarRoutePlanner 生成路径
   │  ├─ ContactTopologyResolver 生成钣金草图线
   │  ├─ BusbarOverlapHolePlanner 展开搭接孔
   │  └─ FastenerPlanBuilder 生成螺栓计划
   ├─ BusbarPreflightValidator.ValidatePlan(...)
   ├─ [--validate] 输出预检后结束
   ├─ [--export-report] ProductionReportExporter.Export(...)
   ├─ [--verify-geometry] BusbarGeometryVerifier.Verify(...)
   ├─ [--preview] SolidWorksBusbarPartBuilder.CreateBusbarPreviewPart(...)
   └─ [完整生成] SolidWorksBusbarPartBuilder.CreateBusbarSheetMetalParts(...)
      ├─ SelectBusbarsForSheetMetalBatch(plan)
      ├─ foreach busbar: CreateBusbarSheetMetalPart(...)
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
   └─ [完整且校验通过] ProductionReportExporter.Export(...)
```

## 2. 关键入口

| 函数 | 位置 | 作用 |
| --- | --- | --- |
| `Parse` | `App/GenerationOptionsParser.cs` | 解析参数并拒绝未知参数、冲突模式和无效组合。 |
| `Run` | `SolidWorks/SolidWorksGenerationRunner.cs` | 当前 SolidWorks 后端的唯一流程协调入口。 |
| `Scan` | `SolidWorks/AssemblyScanner.cs` | 读取命名参考点并转换为装配体坐标。 |
| `BuildPlanFromScannedAssembly` | `Planning/BusbarPlanBuilder.cs` | 从参考点与配置建立完整 `BusbarPlan`。 |
| `ValidatePlan` | `Planning/BusbarPreflightValidator.cs` | 在 CAD 建模前检查规划契约。 |
| `CreateBusbarSheetMetalParts` | `SolidWorks/BusbarBatchBuilder.cs` | 分阶段生成、暂存验证和替换组件。 |
| `CreateBusbarMountingHole` | `SolidWorks/MountingHoleBuilder.cs` | 在计算出的实体表面创建定向厚度切除。 |
| `Verify` / `VerifyStaged` | `SolidWorks/BusbarGeometryVerifier.cs` | 检查实体包络、实际圆柱孔贯穿和双排表面贴合。 |

## 3. 修改定位

- 改额定电流与铜排规格：`Program.cs` 的 `PhaseBranchRules` / `NeutralBranchRules`。
- 改汇流排位置与 X- 外伸：`Program.cs` 参数和 `Planning/CollectorLayoutPlanner.cs`。
- 改单双排路径：`Planning/BusbarPlanBuilder.cs`、`BusbarRoutePlanner.cs`、`ContactTopologyResolver.cs`。
- 改搭接孔型：`Rules/BusbarOverlapRuleMatrix.cs`、`Planning/BusbarOverlapHolePlanner.cs`。
- 改 SolidWorks 孔草图面或切除：`SolidWorks/MountingHoleBuilder.cs`，同时更新实体校验与实机测试。
- 改报表：`Reporting/ProductionReportExporter.cs`。

更细的函数说明见 [docs/SCRIPT_FUNCTION_GUIDE.md](docs/SCRIPT_FUNCTION_GUIDE.md)。
