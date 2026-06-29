# 函数调用树

本文档从 `Main()` 开始梳理当前项目的主要调用关系。当前项目存在两条生成路线：

- V2 主线：`--sheetmetal-v2-all`，当前应优先理解和继续演进的路线。
- V1/旧线：默认无 V2 参数时运行，仍保留旧的 `BusbarRoute` 生成和旧钣金/扫掠逻辑。

为了后续维护清晰，本文先展开 V2 主线，再列出旧线和诊断工具。

## 1. 顶层调用树

```text
Main(args)
├─ ConfigureFromArgs(args)
├─ [如果 --plan-v2-demo]
│  └─ BusbarPlanningDemoV2.Run()
├─ GetOrStartSolidWorks()
├─ GetActiveOrOpenAssembly(swApp)
├─ [如果 --plan-v2-assembly]
│  ├─ ScanReferencePoints(swApp, model, assembly)
│  └─ BusbarPlanningDemoV2.RunFromScannedAssembly(scannedPoints, PhaseNames, Settings)
├─ [如果 --preview-v2-assembly]
│  ├─ ScanReferencePoints(swApp, model, assembly)
│  ├─ BusbarPlanningDemoV2.BuildPlanFromScannedAssembly(scannedPoints, PhaseNames, Settings)
│  └─ CreateBusbarV2PreviewPart(swApp, model, assembly, plan)
├─ [如果 --sheetmetal-v2-first-main]
│  ├─ ScanReferencePoints(swApp, model, assembly)
│  ├─ BusbarPlanningDemoV2.BuildPlanFromScannedAssembly(scannedPoints, PhaseNames, Settings)
│  └─ CreateBusbarV2SheetMetalPart(swApp, model, assembly, firstMainFeed)
├─ [如果 --sheetmetal-v2-all]
│  ├─ DeleteExistingBusbarComponents(model, assembly)
│  ├─ ScanReferencePoints(swApp, model, assembly)
│  ├─ BusbarPlanningDemoV2.BuildPlanFromScannedAssembly(scannedPoints, PhaseNames, Settings)
│  ├─ SelectBusbarsForV2SheetMetalBatch(plan)
│  └─ CreateBusbarV2SheetMetalParts(swApp, model, assembly, busbars)
├─ [如果 --demo-open-line]
│  └─ RunSheetMetalOpenLineDemo(swApp, model, assembly)
├─ [如果 --demo-bent-line]
│  └─ RunSheetMetalBentLineDemo(swApp, model, assembly)
└─ [默认旧 V1 路线]
   ├─ DeleteExistingBusbarComponents(model, assembly)
   ├─ ScanReferencePoints(swApp, model, assembly)
   ├─ BuildComplexRoutes(foundPoints)
   └─ foreach route: CreateBusbarSolidPart(swApp, model, assembly, route)
```

## 2. V2 主流程调用树

### 2.1 `--sheetmetal-v2-all` 主路径

```text
Main
├─ ConfigureFromArgs
├─ GetOrStartSolidWorks
├─ GetActiveOrOpenAssembly
├─ DeleteExistingBusbarComponents
├─ ScanReferencePoints
│  ├─ DumpModelFeatures(assembly model)
│  │  └─ TryReadReferencePoint
│  │     └─ TransformPoint (仅组件点需要)
│  └─ foreach component
│     └─ DumpModelFeatures(component model)
│        └─ TryReadReferencePoint
│           └─ TransformPoint
├─ BusbarPlanningDemoV2.BuildPlanFromScannedAssembly
│  ├─ ManualBusbarRuleSet.CreateDefault
│  ├─ new ManualPortRuleProvider
│  ├─ new CollectorLayoutPlannerV2
│  │  └─ new BusbarLengthControllerV2
│  ├─ new BusbarRoutePlannerV2
│  ├─ new ContactTopologyResolver
│  ├─ FindFuseComponent
│  ├─ FindLoubaoGroups
│  ├─ foreach phase A/B/C
│     ├─ FindRequiredPoint(phase_OUT)
│     ├─ ManualPortRuleProvider.CreateFuseOutPort
│     ├─ FindRequiredPoint(each loubao phase_IN)
│     ├─ ManualPortRuleProvider.CreateLoubaoInPort
│     ├─ ApplyBranchDevicePortRules
│     ├─ CollectorLayoutPlannerV2.CreateLayout
│     │  ├─ CreateConnectionExtents
│     │  └─ BusbarLengthControllerV2.Calculate
│     ├─ CollectorLayoutPlannerV2.CreateTap(mainFeed)
│     ├─ ApplyMainFeedCollectorTapRules
│     ├─ ApplyCollectorTapHoleRules
│     ├─ CreateBusbar(MainFeed)
│     │  ├─ BusbarRoutePlannerV2.CreateRoute
│     │  │  └─ CreateMainFeedRoute
│     │  │     └─ CalculateMainFeedRouteDecision
│     │  │        ├─ CalculateMainFeedLeadOutY
│     │  │        └─ CalculateMainFeedApproachZ
│     │  ├─ ContactTopologyResolver.CreateSheetMetalSketchLine
│     │  │  ├─ AddEndMargins
│     │  │  └─ ApplyThicknessTransition
│     │  │     ├─ ResolveForTransition
│     │  │     ├─ ResolveMainFeedTopology
│     │  │     ├─ ShouldCompensateStart
│     │  │     ├─ GetEndpointTangent
│     │  │     ├─ ChooseThicknessNormal
│     │  │     └─ MoveEndpointRun
│     │  └─ AddMountingPortIfNeeded(start/end)
│     ├─ foreach loubao input
│     │  ├─ CollectorLayoutPlannerV2.CreateTap(branch)
│     │  ├─ ApplyBranchCollectorTapRules
│     │  ├─ ApplyCollectorTapHoleRules
│     │  └─ CreateBusbar(Branch)
│     │     ├─ BusbarRoutePlannerV2.CreateRoute
│     │     │  └─ CreateSimpleRoute
│     │     ├─ ContactTopologyResolver.CreateSheetMetalSketchLine
│     │     └─ AddMountingPortIfNeeded(start/end)
│     └─ CreateCollectorBusbar
│        ├─ CreateCollectorEndPort(start/end)
│        ├─ ContactTopologyResolver.CreateSheetMetalSketchLine
│        └─ MountingPorts = collector.TapPorts with holes
│  └─ AddNeutralCollectorAndBranches
│     ├─ CreateNeutralLoubaoInputs
│     │  ├─ Find N_IN for each loubao
│     │  └─ ManualPortRuleProvider.CreateLoubaoInPort(N)
│     ├─ CollectorLayoutPlannerV2.CreateLayout(N)
│     │  ├─ CreateConnectionExtents
│     │  └─ BusbarLengthControllerV2.Calculate
│     ├─ foreach neutral input
│     │  ├─ CollectorLayoutPlannerV2.CreateTap(N branch)
│     │  └─ CreateBusbar(N Branch)
│     │     ├─ BusbarRoutePlannerV2.CreateRoute
│     │     ├─ ContactTopologyResolver.CreateSheetMetalSketchLine
│     │     └─ AddMountingPortIfNeeded(start/end)
│     └─ CreateCollectorBusbar(N Collector)
├─ SelectBusbarsForV2SheetMetalBatch
│  ├─ FindRequiredV2Busbar(A/B/C MainFeed)
│  ├─ FindRequiredV2Busbar(A/B/C Collector)
│  ├─ FindOptionalV2Busbars(N Collector / N Branch)
│  └─ FindV2Busbars(A/B/C Branch)
└─ CreateBusbarV2SheetMetalParts
   └─ foreach busbar: CreateBusbarV2SheetMetalPart
      ├─ NewPartDocument
      ├─ ActivateDocument(part)
      ├─ CreateBusbarV2SheetMetalFeature
      │  ├─ GetOpenProfilePlane
      │  ├─ CreateV2SheetMetalOpenProfileSketch
      │  │  ├─ CreateOffsetPlane
      │  │  │  ├─ FindDefaultPlane
      │  │  │  └─ FindLastFeatureByType
      │  │  ├─ ModelPointToSketchPoint
      │  │  ├─ FlattenSketchPoint
      │  │  └─ CreateLineOrThrow
      │  ├─ CreateSheetMetalBaseFlangeFromSelectedSketch
      │  │  ├─ CreateKFactorBendAllowance
      │  │  ├─ GetSheetMetalBaseFlangeExtent
      │  │  └─ InsertSheetMetalBaseFlange2
      │  └─ ApplySheetMetalParametersToCreatedFeature
      │     ├─ FindFirstFeatureByType("SheetMetal")
      │     ├─ CreateKFactorBendAllowance
      │     └─ ModifyDefinition
      ├─ CreateBusbarV2MountingHoles
      │  └─ foreach MountingPort: CreateBusbarV2MountingHole
      │     ├─ GetHoleSketchPlane
      │     ├─ CreateOffsetPlane
      │     ├─ ModelPointToSketchPoint
      │     ├─ FlattenSketchPoint
      │     ├─ CreateCircleByRadius
      │     ├─ CreateDirectedBlindCutFromActiveSketch
      │     │  ├─ ShouldReverseHoleCutDirection
      │     │  └─ TryCreateBlindCutFromCurrentSelection
      │     │     └─ FeatureCut4
      │     └─ fallback: CreateDirectedBlindCutFromSketch
      │        ├─ SelectSketchForCut
      │        └─ TryCreateBlindCut / TryCreateBlindCutWithScope
      ├─ SaveBusbarV2SheetMetalPart
      ├─ InsertDemoPartIntoAssembly
      └─ CloseBusbarPartDocument
```

## 3. 主要函数说明

### `Main(string[] args)`

- 输入：命令行参数。
- 输出：无直接返回；副作用是生成/保存/插入 SolidWorks 零件，或打印预览/调试信息。
- 功能：应用入口，根据参数选择 V2 规划、V2 预览、V2 生成、旧线生成或 Demo。
- 调用者：操作系统/控制台。
- 被调用者：`ConfigureFromArgs`、`GetOrStartSolidWorks`、`GetActiveOrOpenAssembly`、各运行模式函数。

### `ConfigureFromArgs(string[] args)`

- 输入：命令行参数数组。
- 输出：无返回；修改静态开关和 `Settings.ModelingMode`。
- 功能：识别 `--sheetmetal-v2-all`、`--preview-v2-assembly`、`--keep-existing` 等运行模式。
- 调用者：`Main`。
- 被调用者：`SameText`。

### `GetOrStartSolidWorks()`

- 输入：无。
- 输出：`SldWorks` COM 对象。
- 功能：优先连接正在运行的 SolidWorks；找不到时启动新实例并显示。
- 调用者：`Main`。
- 被调用者：`Marshal.GetActiveObject`、`Activator.CreateInstance`。

### `GetActiveOrOpenAssembly(SldWorks swApp)`

- 输入：SolidWorks 应用对象。
- 输出：活动或切换后的 `ModelDoc2` 装配体。
- 功能：若当前活动文档是装配体则直接返回；否则从已打开文档中找一个装配体并激活。
- 调用者：`Main`。
- 被调用者：`ActivateDocument`。

### `DeleteExistingBusbarComponents(ModelDoc2 model, AssemblyDoc assembly)`

- 输入：装配体文档和装配体对象。
- 输出：无返回；删除名称以 `Busbar_` 开头的组件。
- 功能：运行前清理旧生成件，避免重复装配。
- 调用者：`Main` 的 V2 全量路径和默认旧路径。
- 被调用者：SolidWorks 选择和 `EditDelete` API。

### `ScanReferencePoints(SldWorks swApp, ModelDoc2 model, AssemblyDoc assembly)`

- 输入：SW 应用、装配体文档、装配体对象。
- 输出：`List<FoundPoint>`。
- 功能：扫描装配体自身和每个组件内部的命名参考点，并统一转为装配体坐标。
- 调用者：`Main` 的规划、预览、生成路径。
- 被调用者：`DumpModelFeatures`、`TryReadReferencePoint`、`TransformPoint`。

### `DumpModelFeatures(...)`

- 输入：SW 应用、模型、组件名、组件变换、结果列表。
- 输出：无直接返回；向 `foundPoints` 添加结果。
- 功能：遍历 Feature 树，对每个 Feature 尝试读取参考点。
- 调用者：`ScanReferencePoints`。
- 被调用者：`TryReadReferencePoint`。

### `TryReadReferencePoint(...)`

- 输入：Feature、组件名、组件变换、结果列表。
- 输出：无直接返回；读取成功时添加 `FoundPoint`。
- 功能：识别 `RefPoint`，读取局部坐标，必要时转换到装配坐标。
- 调用者：`DumpModelFeatures`。
- 被调用者：`TransformPoint`。

### `TransformPoint(SldWorks swApp, Point3 point, MathTransform transform)`

- 输入：局部点、组件变换。
- 输出：装配体坐标点。
- 功能：使用 SolidWorks `MathUtility` 将组件局部点乘以 `Transform2`。
- 调用者：`TryReadReferencePoint`，诊断工具也有同名实现。
- 被调用者：SolidWorks Math API。

## 4. V2 规划函数说明

### `BusbarPlanningDemoV2.BuildPlanFromScannedAssembly(...)`

- 输入：扫描点、相名数组、全局设置。
- 输出：`BusbarPlanV2`。
- 功能：把原始扫描点转换为完整铜排规划，包括三根转接排、ABC 汇流排、ABC 分支排；如果存在完整 `N_IN`，再追加 N 汇流排和 N 分支排。
- 调用者：`Main` 的 V2 路线。
- 被调用者：规则、端口、汇流排、路径、拓扑等 V2 规划类。

### `AddNeutralCollectorAndBranches(...)`

- 输入：当前规划、扫描点、漏保组、N 相序号、端口规则器、汇流排规划器、路径规划器、拓扑解析器、规则和设置。
- 输出：无直接返回；向 `BusbarPlanV2.Collectors` 和 `BusbarPlanV2.Busbars` 追加 N 排对象。
- 功能：在 ABC 主流程后追加 N 汇流排和 N 分支排。没有 `N_IN` 时跳过；只有部分漏保存在 `N_IN` 时抛出错误；完整 `N_IN` 时生成一根 N 汇流排和每个漏保一根 N 分支排。
- 调用者：`BusbarPlanningDemoV2.BuildPlanFromScannedAssembly`。
- 被调用者：`CreateNeutralLoubaoInputs`、`CollectorLayoutPlannerV2.CreateLayout`、`CollectorLayoutPlannerV2.CreateTap`、`CreateBusbar`、`CreateCollectorBusbar`。

### `CreateNeutralLoubaoInputs(...)`

- 输入：扫描点、漏保组、端口规则器。
- 输出：N 相漏保输入端口列表。
- 功能：逐个漏保查找 `N_IN` 参考点，并转换为 `ConnectionPort`。如果所有漏保都没有 `N_IN`，返回空列表；如果只有部分漏保缺失 `N_IN`，抛出明确异常。
- 调用者：`AddNeutralCollectorAndBranches`。
- 被调用者：`ManualPortRuleProvider.CreateLoubaoInPort`。

### `ManualBusbarRuleSet.CreateDefault(CabinetTopologyKind topologyKind)`

- 输入：结构类型，例如 `TypicalDesign`。
- 输出：手动规则集合。
- 功能：生成默认面、孔径、端部裕度、K 因子、MidPlane、搭接面等规则。
- 调用者：`BuildPlanFromScannedAssembly`、`Run`。
- 被调用者：无。

### `ManualPortRuleProvider.CreateFuseOutPort(...)`

- 输入：相名、扫描到的刀熔 OUT 点。
- 输出：`ConnectionPort`。
- 功能：把 `A_OUT/B_OUT/C_OUT` 转为带连接面、引出方向、孔径、端部裕度的业务端口。
- 调用者：`BuildPlanFromScannedAssembly`。
- 被调用者：`CreateDevicePort`、`ManualBusbarRuleSet.GetFuseOutFace`。

### `ManualPortRuleProvider.CreateLoubaoInPort(...)`

- 输入：相名、漏保序号、扫描到的漏保 IN 点。
- 输出：`ConnectionPort`。
- 功能：把漏保 IN 点转为业务端口，默认贴 `Front`，默认 Y+ 引出。
- 调用者：`BuildPlanFromScannedAssembly`。
- 被调用者：`CreateDevicePort`、`ManualBusbarRuleSet.GetLoubaoInFace`。

### `ManualPortRuleProvider.CreateCollectorTapPort(...)`

- 输入：相名、Tap 名称、Tap 坐标、搭接面。
- 输出：`ConnectionPort`。
- 功能：创建汇流排上的连接点。
- 调用者：`CollectorLayoutPlannerV2.CreateTap`。
- 被调用者：无。

### `CollectorLayoutPlannerV2.CreateLayout(...)`

- 输入：相名、相序、刀熔端口、该相所有漏保端口。
- 输出：`CollectorLayoutV2`。
- 功能：计算该相汇流排 Y/Z 位置和 X 起止范围。
- 调用者：`BuildPlanFromScannedAssembly`。
- 被调用者：`CreateConnectionExtents`、`BusbarLengthControllerV2.Calculate`。

### `BusbarLengthControllerV2.Calculate(...)`

- 输入：与汇流排连接的所有铜排 X 向占用范围。
- 输出：`CollectorLengthRangeV2`。
- 功能：计算汇流排长度。当前规则是 X+ 侧到最外侧铜排外边界，X- 侧在最外侧铜排外边界基础上再延伸 50mm。
- 调用者：`CollectorLayoutPlannerV2.CreateLayout`。
- 被调用者：LINQ `Max/Min`。

### `CollectorLayoutPlannerV2.CreateTap(...)`

- 输入：端口规则器、相名、Tap 名称、X 坐标、汇流排布局、搭接面。
- 输出：`ConnectionPort`。
- 功能：在汇流排中心线上创建一个 Tap 端口，并加入 `collector.TapPorts`。
- 调用者：`BuildPlanFromScannedAssembly`。
- 被调用者：`ManualPortRuleProvider.CreateCollectorTapPort`。

### `BusbarRoutePlannerV2.CreateRoute(...)`

- 输入：铜排类型、规格、起点端口、终点端口、轴顺序。
- 输出：`List<Point3>` 逻辑中心线。
- 功能：生成孔中心意义上的业务路径，不直接处理钣金厚度补偿。
- 调用者：`CreateBusbar`。
- 被调用者：`CreateMainFeedRoute` 或 `CreateSimpleRoute`。

### `CreateMainFeedRoute(...)`

- 输入：转接排规格、刀熔 OUT 端口、汇流排 Tap 端口。
- 输出：转接排逻辑路径。
- 功能：典设箱刀熔下端路径：先 Y-，再 Z- 到过渡面，再 Y+ 到汇流排高度，最后 Z- 到汇流排。
- 调用者：`BusbarRoutePlannerV2.CreateRoute`。
- 被调用者：`CalculateMainFeedRouteDecision`。

### `CreateSimpleRoute(...)`

- 输入：起点端口、终点端口、轴顺序。
- 输出：普通两轴折线路径。
- 功能：当前用于分支排，默认先 Y 后 Z。
- 调用者：`BusbarRoutePlannerV2.CreateRoute`。
- 被调用者：`Add`。

### `ContactTopologyResolver.CreateSheetMetalSketchLine(BusbarV2 busbar)`

- 输入：含逻辑中心线的铜排对象。
- 输出：最终用于 SolidWorks 2D 草图的中心线。
- 功能：先给端部增加裕度，再根据同侧/异侧拓扑执行厚度转换补偿。
- 调用者：`CreateBusbar`、`CreateCollectorBusbar`。
- 被调用者：`AddEndMargins`、`ApplyThicknessTransition`。

### `ContactTopologyResolver.Resolve(BusbarV2 busbar)`

- 输入：铜排对象。
- 输出：`SameSide` 或 `DifferentSide`。
- 功能：判断同侧/异侧。目前转接排按规则表判断，分支排和汇流排默认同侧。
- 调用者：打印、补偿逻辑。
- 被调用者：`ResolveMainFeedTopology`。

### `ApplyThicknessTransition(...)`

- 输入：铜排和已添加端部裕度的草图线。
- 输出：无返回；原地修改 `sketchLine`。
- 功能：异侧时按补偿策略移动起点或终点附近的一段点，避免搭接穿模。
- 调用者：`CreateSheetMetalSketchLine`。
- 被调用者：`ResolveForTransition`、`ShouldCompensateStart`、`FindPointIndex`、`GetEndpointTangent`、`ChooseThicknessNormal`、`MoveEndpointRun`。

## 5. V2 钣金生成函数说明

### `SelectBusbarsForV2SheetMetalBatch(BusbarPlanV2 plan)`

- 输入：完整 V2 规划。
- 输出：按生成顺序排序的铜排列表。
- 功能：选择三根转接排、ABC 汇流排、可选 N 汇流排、ABC 分支排和可选 N 分支排。当前典设箱完整 `N_IN` 场景下总计 19 根。
- 调用者：`Main --sheetmetal-v2-all`。
- 被调用者：`FindRequiredV2Busbar`、`FindOptionalV2Busbars`、`FindV2Busbars`。

### `CreateBusbarV2SheetMetalParts(...)`

- 输入：SW 应用、装配体文档、装配体对象、铜排列表。
- 输出：无返回；逐个生成零件并插入装配。
- 功能：批处理 V2 铜排实体生成。
- 调用者：`Main --sheetmetal-v2-all`。
- 被调用者：`CreateBusbarV2SheetMetalPart`。

### `CreateBusbarV2SheetMetalPart(...)`

- 输入：SW 应用、装配体、单根 `BusbarV2`。
- 输出：无返回；生成一个 `SLDPRT` 并插回装配。
- 功能：单根铜排生成完整闭环。
- 调用者：`CreateBusbarV2SheetMetalParts`、`Main --sheetmetal-v2-first-main`。
- 被调用者：`NewPartDocument`、`ActivateDocument`、`CreateBusbarV2SheetMetalFeature`、`CreateBusbarV2MountingHoles`、`SaveBusbarV2SheetMetalPart`、`InsertDemoPartIntoAssembly`、`CloseBusbarPartDocument`。

### `CreateBusbarV2SheetMetalFeature(...)`

- 输入：SW 应用、零件文档、单根铜排。
- 输出：钣金 Feature。
- 功能：把 `BusbarV2.SheetMetalSketchLine` 转成开放轮廓草图，再生成基体法兰。
- 调用者：`CreateBusbarV2SheetMetalPart`。
- 被调用者：`GetOpenProfilePlane`、`CreateV2SheetMetalOpenProfileSketch`、`CreateSheetMetalBaseFlangeFromSelectedSketch`、`ApplySheetMetalParametersToCreatedFeature`。

### `GetOpenProfilePlane(BusbarRoute route)`

- 输入：路线点。
- 输出：`SheetMetalOpenProfilePlane`。
- 功能：判断路线是否位于固定 X/Y/Z 平面，并选择 Right/Top/Front 偏移草图平面。
- 调用者：V1 和 V2 开放轮廓钣金生成。
- 被调用者：`AllSameCoordinate`。

### `CreateV2SheetMetalOpenProfileSketch(...)`

- 输入：SW 应用、零件文档、铜排、草图平面描述。
- 输出：Sketch Feature。
- 功能：创建偏移平面，在 2D 草图中把装配坐标点转换成草图坐标并画开放折线。
- 调用者：`CreateBusbarV2SheetMetalFeature`。
- 被调用者：`CreateOffsetPlane`、`ModelPointToSketchPoint`、`FlattenSketchPoint`、`CreateLineOrThrow`。

### `CreateSheetMetalBaseFlangeFromSelectedSketch(...)`

- 输入：零件文档、已选草图、规格、铜排类型。
- 输出：钣金基体法兰 Feature。
- 功能：调用 SolidWorks `InsertSheetMetalBaseFlange2`，使用真实 Mid Plane 生成钣金。
- 调用者：`CreateBusbarV2SheetMetalFeature`、V1 开放轮廓路径。
- 被调用者：`CreateKFactorBendAllowance`、`GetSheetMetalBaseFlangeExtent`。

### `GetSheetMetalBaseFlangeExtent(...)`

- 输入：铜排规格、宽度方向模式。
- 输出：`SheetMetalBaseFlangeExtent`。
- 功能：封装 SolidWorks 基体法兰的宽度方向参数。当前中心线模式使用 `swEndCondMidPlane`。
- 调用者：`CreateSheetMetalBaseFlangeFromSelectedSketch`。
- 被调用者：无。

### `ApplySheetMetalParametersToCreatedFeature(...)`

- 输入：新建 Feature、零件文档、铜排规格。
- 输出：无返回；修改钣金 Feature Definition。
- 功能：强制写入厚度、折弯半径、K 因子、自动释放槽等参数。
- 调用者：钣金生成函数。
- 被调用者：`FindFirstFeatureByType`、`CreateKFactorBendAllowance`。

## 6. 孔生成函数说明

### `CreateBusbarV2MountingHoles(...)`

- 输入：SW 应用、零件文档、铜排。
- 输出：无返回；创建所有孔。
- 功能：优先使用 `busbar.MountingPorts`，没有时退回起终端口。
- 调用者：`CreateBusbarV2SheetMetalPart`。
- 被调用者：`CreateBusbarV2MountingHole`。

### `CreateBusbarV2MountingHole(...)`

- 输入：SW 应用、零件文档、铜排、端口、孔序号。
- 输出：无直接返回；创建孔草图和切除特征。
- 功能：根据端口面创建孔草图平面，把孔中心从模型坐标转到草图坐标，画圆并切除。
- 调用者：`CreateBusbarV2MountingHoles`。
- 被调用者：`GetHoleSketchPlane`、`CreateOffsetPlane`、`ModelPointToSketchPoint`、`CreateDirectedBlindCutFromActiveSketch`、fallback 切除函数。

### `GetHoleSketchPlane(ConnectionPort port)`

- 输入：端口。
- 输出：孔草图平面描述。
- 功能：`Upper/Lower` 走 Top 平面，`Left/Right` 走 Right 平面，`Front/Back` 走 Front 平面。
- 调用者：`CreateBusbarV2MountingHole`。
- 被调用者：无。

### `CreateDirectedBlindCutFromActiveSketch(...)`

- 输入：零件文档、特征名、端口、切除深度。
- 输出：切除 Feature 或 null。
- 功能：保持活动草图状态直接调用 `FeatureCut4`，这是当前已验证稳定的孔切除方式。
- 调用者：`CreateBusbarV2MountingHole`。
- 被调用者：`ShouldReverseHoleCutDirection`、`TryCreateBlindCutFromCurrentSelection`。

### `CreateDirectedBlindCutFromSketch(...)`

- 输入：零件文档、已有草图 Feature、特征名、端口、切除深度。
- 输出：切除 Feature 或 null。
- 功能：活动草图切除失败时的 fallback，会尝试多组 FeatureCut4 参数。
- 调用者：`CreateBusbarV2MountingHole`。
- 被调用者：`TryCreateBlindCut`、`TryCreateBlindCutWithScope`、`SelectSketchForCut`。

## 7. 保存与装配函数说明

### `SaveBusbarV2SheetMetalPart(...)`

- 输入：零件文档、装配体文档、铜排。
- 输出：保存后的 `SLDPRT` 路径。
- 功能：按装配体所在目录保存生成件，文件名包含铜排名、规格和时间戳。
- 调用者：`CreateBusbarV2SheetMetalPart`。
- 被调用者：`ModelDocExtension.SaveAs`。

### `InsertDemoPartIntoAssembly(...)`

- 输入：SW 应用、装配体文档、装配体对象、零件路径、组件名。
- 输出：无返回；插入组件。
- 功能：把新零件以单位变换插入装配体，并改名。
- 调用者：V2 预览、V2 生成、Demo。
- 被调用者：`ActivateDocument`、`AssemblyDoc.AddComponent5`。
- 备注：虽然函数名带 `Demo`，但当前 V2 正式生成也在使用它。

### `CloseBusbarPartDocument(...)`

- 输入：SW 应用、装配体文档、零件文档。
- 输出：无返回；关闭零件并切回装配体。
- 功能：避免生成多根铜排时打开大量零件窗口。
- 调用者：所有生成路径。
- 被调用者：`ActivateDocument`、`SldWorks.CloseDoc`。

## 8. V1 旧路线调用树

旧路线目前还在 `Program.cs` 中，主要用于历史兼容。它的问题是业务路径、补偿、钣金方式耦合在同一个类中，后续建议归档或删除。

```text
Main 默认路径
├─ DeleteExistingBusbarComponents
├─ ScanReferencePoints
├─ BuildComplexRoutes
│  ├─ FindFuseComponent
│  ├─ FindLoubaoGroups
│  └─ foreach phase
│     ├─ FindRequiredPoint(fuse OUT)
│     ├─ FindRequiredPoint(loubao IN)
│     ├─ BuildCollectorLayout
│     ├─ BuildMainFeedRoute
│     │  ├─ GetMainFeedTerminalRoutePoint
│     │  └─ CalculateMainCollectorLap
│     ├─ BuildCollectorRoute
│     └─ BuildBranchRoute
│        └─ CalculateBranchCollectorLapY
└─ foreach BusbarRoute: CreateBusbarSolidPart
   ├─ NewPartDocument
   ├─ CreateBusbarFeature
   │  ├─ [OpenProfile] CreateSheetMetalOpenProfileBaseFlangeFeature
   │  └─ [SweptFlange] CreatePart3DPathSketch + CreatePartProfileSketch + CreateSweptFlangeFeature
   ├─ SaveBusbarPart
   ├─ InsertBusbarPartIntoAssembly
   └─ CloseBusbarPartDocument
```

## 9. 预览和 Demo 路线

### V2 预览

```text
Main --preview-v2-assembly
├─ ScanReferencePoints
├─ BusbarPlanningDemoV2.BuildPlanFromScannedAssembly
└─ CreateBusbarV2PreviewPart
   ├─ NewPartDocument
   ├─ CreateBusbarV2PreviewSketches
   │  ├─ CreatePreview3DPolylineSketch(Collector center)
   │  ├─ CreatePreview3DPolylineSketch(Logical)
   │  └─ CreatePreview3DPolylineSketch(SheetMetal)
   ├─ SaveDemoBusbarPart
   ├─ InsertDemoPartIntoAssembly
   └─ CloseBusbarPartDocument
```

### 钣金开放线 Demo

```text
Main --demo-open-line
└─ RunSheetMetalOpenLineDemo
   ├─ NewPartDocument
   ├─ CreateSheetMetalOpenLineBaseFlangeDemo
   │  ├─ FindDefaultPlane
   │  ├─ CreateLineOrThrow
   │  ├─ CreateSheetMetalBaseFlangeFromActiveOpenProfile
   │  └─ ApplySheetMetalParametersToCreatedFeature
   ├─ SaveDemoBusbarPart
   ├─ InsertDemoPartIntoAssembly
   └─ CloseBusbarPartDocument
```

### 钣金折线 Demo

```text
Main --demo-bent-line
└─ RunSheetMetalBentLineDemo
   ├─ NewPartDocument
   ├─ CreateSheetMetalBentLineBaseFlangeDemo
   │  ├─ FindDefaultPlane
   │  ├─ CreateLineOrThrow x2
   │  ├─ CreateSheetMetalBaseFlangeFromActiveOpenProfile
   │  └─ ApplySheetMetalParametersToCreatedFeature
   ├─ SaveDemoBusbarPart
   ├─ InsertDemoPartIntoAssembly
   └─ CloseBusbarPartDocument
```

## 10. FeatureExtract 诊断工具调用树

```text
FeatureExtract.Program.Main
├─ GetRunningSolidWorks
├─ PrintDocumentHeader
├─ [Part]
│  └─ ScanModelFeatures(model)
│     ├─ TryPrintReferencePoint
│     │  └─ TransformPoint (如果有 ownerTransform)
│     └─ TryPrintCoordinateSystem
└─ [Assembly]
   ├─ ScanModelFeatures(assembly self)
   └─ ScanAssembly
      └─ foreach component
         └─ ScanModelFeatures(component model, component.Transform2)
            ├─ TryPrintReferencePoint
            │  └─ TransformPoint
            └─ TryPrintCoordinateSystem
```

### `FeatureExtract.Program.Main()`

- 输入：无命令行参数。
- 输出：控制台打印。
- 功能：扫描当前 SolidWorks 文档的特征和参考点，用于排查点名、坐标和组件加载状态。
- 调用者：操作系统/控制台。
- 被调用者：`GetRunningSolidWorks`、`PrintDocumentHeader`、`ScanModelFeatures`、`ScanAssembly`。

## 11. 当前调用关系中的风险点

- `Program.Main` 分支较多，继续增加功能会更难判断当前运行的是哪条线。
- V2 正式生成仍调用 `InsertDemoPartIntoAssembly`，命名不准确。
- V2 业务规划虽然集中在 `BusbarFramework.cs`，但入口类 `BusbarPlanningDemoV2` 命名仍带 Demo。
- `ContactTopologyResolver.CreateSheetMetalSketchLine` 既加端部裕度又做拓扑补偿，后续需要拆分。
- `CreateBusbarV2MountingHole` 含有多个 fallback 切除策略，建议封装成独立 `HoleBuilder`，否则主生成流程会越来越重。
