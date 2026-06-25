# 架构重构建议

本文档基于当前代码结构提出重构建议。目标不是为了“好看地拆文件”，而是让项目能支持更多配电箱结构、更多搭接方式、自动汇流排长度/位置优化，并避免后续继续用局部偏移补丁修问题。

当前最重要的判断：

```text
V2 建模主流程已经跑通，应该保护这条主线。
重构应该围绕 V2 做渐进拆分，而不是推翻重写。
```

## 1. 当前耦合严重的位置

### 1.1 `Program.cs` 同时承担过多职责

当前 `Program.cs` 包含：

- 命令行参数解析。
- SolidWorks 连接和文档切换。
- 装配体扫描。
- V1 旧路径规划。
- V1/V2 钣金草图创建。
- SolidWorks Sheet Metal API 调用。
- 孔草图和切除。
- 保存零件。
- 插入装配体。
- 基础数据模型，例如 `Point3`、`BusbarProfile`、`BusbarSettings`。

风险：

- 任何新增功能都容易继续塞进 `Program.cs`。
- 业务逻辑和 SW API 逻辑互相干扰，调试难度高。
- 旧 V1 和新 V2 共存，容易误改旧逻辑或误以为旧逻辑仍是主线。
- 基础模型定义在入口文件里，导致 `BusbarFramework.cs` 反向依赖入口层概念。

建议：

- `Program.cs` 最终只保留入口和流程编排。
- 把 SolidWorks 操作和业务规划完全拆开。

### 1.2 `BusbarFramework.cs` 已经是业务层，但仍然是“大文件”

当前 `BusbarFramework.cs` 包含：

- 枚举。
- 配置。
- 数据模型。
- 规则提供器。
- 汇流排布局。
- 汇流排长度控制。
- 路径规划。
- 同侧/异侧判断。
- 厚度补偿。
- Demo/从扫描点构建计划。

风险：

- 这个文件会成为新的巨石。
- `ContactTopologyResolver` 同时做了拓扑判断、端部裕度、厚度补偿。
- `BusbarPlanningDemoV2` 虽然现在是主规划入口之一，但命名仍带 Demo，会误导维护者。

建议：

- 把 `BusbarFramework.cs` 拆成 Models、Rules、Planning、Topology、Compensation、Application Service。
- 把 `BusbarPlanningDemoV2` 重命名为 `BusbarPlanBuilderV2` 或 `BusbarPlanningService`。

### 1.3 SW API 与业务逻辑混在一起

典型例子：

- `CreateBusbarV2SheetMetalPart` 同时负责创建零件、生成钣金、打孔、保存、插入装配体。
- `CreateBusbarV2MountingHole` 同时负责业务孔语义、草图平面选择、坐标变换、画圆、FeatureCut4 多策略重试。
- `GetOpenProfilePlane` 既是几何判断，也和 SW 默认平面命名绑定。

风险：

- 业务规则改动可能引发 SolidWorks API 侧问题。
- API 调用失败时难以判断是业务点位错、草图平面错，还是 Feature 参数错。
- 单元测试很难写，因为大部分函数直接依赖 SolidWorks COM。

建议：

- 纯业务层只产出 `BusbarPlanV2`。
- SW 几何层只消费 `BusbarV2.SheetMetalSketchLine` 和 `MountingPorts`。
- API 失败日志集中在 SW Adapter/Builder 层。

### 1.4 补偿逻辑还没有完全独立

当前 `ContactTopologyResolver.CreateSheetMetalSketchLine` 做了：

```text
逻辑中心线
-> AddEndMargins
-> ApplyThicknessTransition
-> SheetMetalSketchLine
```

其中 `ApplyThicknessTransition` 又包含：

- 判断是否同侧/异侧。
- 判断补偿起点还是终点。
- 推导补偿方向。
- 移动端部点段。

风险：

- 后续增加南网结构、下搭、侧搭时，容易继续在这个类里堆 if。
- 很难单独验证“同侧/异侧判断正确”还是“补偿方向正确”。

建议拆为：

```text
ContactTopologyResolver
    只判断 SameSide / DifferentSide

EndMarginResolver
    只根据端口裕度扩展中心线

ThicknessTransitionResolver
    根据拓扑、策略、厚度、局部路径，输出补偿后的草图线
```

### 1.5 手动规则硬编码

当前规则来自：

```text
ManualBusbarRuleSet.CreateDefault(CabinetTopologyKind.TypicalDesign)
```

风险：

- 要切换南网结构必须改代码或新增参数。
- 不同项目的端口面、孔径、裕度、搭接面无法配置化。
- 后续 Excel 数据层不容易接入。

建议：

- 先保留 `ManualBusbarRuleSet`，但增加 JSON/Excel 导入接口。
- 当前默认规则可以作为配置模板。
- 运行时通过 CLI 或配置文件选择 `TypicalDesign` / `SouthernGrid`。

### 1.6 旧 V1 与 V2 共存但边界不清

当前旧 V1 仍包含：

- `BuildComplexRoutes`
- `BuildMainFeedRoute`
- `BuildCollectorRoute`
- `BuildBranchRoute`
- `CreateBusbarSolidPart`
- `CreateSweptFlangeFeature`

风险：

- 新开发者不清楚哪条是主线。
- 同样概念在 V1/V2 中有两套实现。
- 旧补偿思路可能被误用到新 V2。

建议：

- 在代码层明确标记 `LegacyV1`。
- 确认 V2 稳定后，将 V1 移到 `Legacy/` 或直接删除。
- Demo 入口保留时放到 `Experiments/`，不要混在主入口中。

### 1.7 命名不准确

典型例子：

- `InsertDemoPartIntoAssembly` 已经用于 V2 正式生成。
- `BusbarPlanningDemoV2.BuildPlanFromScannedAssembly` 实际承担主规划构建职责。
- `CreateBusbarSolidPart` 在当前钣金路径下不一定是 Solid，名称不准确。

风险：

- 命名会误导架构判断。
- 后续重构时很难判断函数是否仍可删除。

建议：

- `InsertDemoPartIntoAssembly` -> `InsertPartIntoAssembly`。
- `BusbarPlanningDemoV2` -> `BusbarPlanBuilderV2` 或 `BusbarPlanningService`。
- `CreateBusbarSolidPart` -> 如果保留旧线，改为 `CreateLegacyBusbarPart`。

## 2. 推荐目标分层架构

```text
Layer 0: Application Layer
Layer 1: SolidWorks Session Layer
Layer 2: Assembly Scan Layer
Layer 3: Port Rule Layer
Layer 4: Collector Planning Layer
Layer 5: Connection Planning Layer
Layer 6: Topology Layer
Layer 7: Compensation Layer
Layer 8: Path Planning Layer
Layer 9: Geometry / Sheet Metal Layer
Layer 10: Hole Builder Layer
Layer 11: Persistence / Assembly Layer
Layer 12: Diagnostics Layer
```

下面逐层说明。

## 3. Layer 0: Application Layer

### 职责

- 解析命令行参数。
- 读取配置。
- 选择运行模式。
- 串联完整流程。
- 统一错误处理和日志。

### 输入

- CLI 参数。
- 配置文件路径或默认规则。

### 输出

- 运行结果报告。
- 调用下层服务产生的装配体变化。

### 当前对应

- `Program.Main`
- `ConfigureFromArgs`

### 建议类

```text
AppRunner
CommandLineOptions
GenerationMode
```

## 4. Layer 1: SolidWorks Session Layer

### 职责

- 连接或启动 SolidWorks。
- 获取当前活动装配体。
- 切换文档。
- 关闭生成的零件文档。

### 输入

- 无或目标文档信息。

### 输出

- `SldWorks`
- `ModelDoc2`
- `AssemblyDoc`

### 当前对应

- `GetOrStartSolidWorks`
- `GetActiveOrOpenAssembly`
- `ActivateDocument`
- `CloseBusbarPartDocument`

### 建议类

```text
SolidWorksSession
DocumentActivator
```

### 原则

这一层可以依赖 SolidWorks Interop；业务层不能依赖这一层。

## 5. Layer 2: Assembly Scan Layer

### 职责

- 扫描装配体和组件。
- 提取命名参考点。
- 坐标转换。
- 输出统一的扫描数据。

### 输入

- SW 装配体。

### 输出

- `List<FoundPoint>`。

### 当前对应

- `ScanReferencePoints`
- `DumpModelFeatures`
- `TryReadReferencePoint`
- `TransformPoint`
- `FeatureExtract.Program` 中的类似逻辑。

### 建议类

```text
ReferencePointScanner
AssemblyCoordinateTransformer
FeatureScanLogger
```

### 改进方向

- 主工程和 `FeatureExtract` 共享同一套扫描核心。
- 诊断工具只负责打印，扫描逻辑不要复制两份。

## 6. Layer 3: Port Rule Layer

### 职责

- 把扫描点转换成业务端口。
- 赋予连接面、引出方向、端部裕度、孔径。
- 支持典设、南网等不同结构规则。

### 输入

- `FoundPoint`
- `ManualBusbarRuleSet` 或外部配置。

### 输出

- `ConnectionPort`

### 当前对应

- `ManualBusbarRuleSet`
- `ManualPortRuleProvider`
- `ApplyMainFeedCollectorTapRules`
- `ApplyBranchDevicePortRules`
- `ApplyBranchCollectorTapRules`
- `ApplyCollectorTapHoleRules`

### 建议类

```text
IPortRuleProvider
ManualPortRuleProvider
ConfiguredPortRuleProvider
PortRuleSet
HoleRule
EndMarginRule
```

### 关键原则

端口是业务语义边界。后续任何铜排都应从 `ConnectionPort` 生成，而不是直接从 `FoundPoint` 生成。

## 7. Layer 4: Collector Planning Layer

### 职责

- 计算汇流排中心位置。
- 计算汇流排长度。
- 生成汇流排 Tap 点。
- 未来支持自动优化汇流排位置。

### 输入

- 刀熔端口。
- 漏保端口。
- 结构规则。
- 汇流排布置参数。

### 输出

- `CollectorLayoutV2`
- `CollectorTap` 端口。

### 当前对应

- `CollectorLayoutPlannerV2`
- `BusbarLengthControllerV2`

### 建议类

```text
CollectorLayoutPlanner
CollectorLengthController
CollectorPositionOptimizer
CollectorTapPlanner
```

### 可扩展点

- 当前 `collectorY/Z` 是简单公式。
- 后续可加入设备包络、最小间距、安全距离和排布目标函数。

## 8. Layer 5: Connection Planning Layer

### 职责

- 建立设备端口到汇流排 Tap 的连接关系。
- 决定生成哪些铜排。
- 决定每根铜排类型、规格和孔规则。

### 输入

- `ConnectionPort` 列表。
- `CollectorLayoutV2`。
- 配置规则。

### 输出

- `BusbarV2` 列表。

### 当前对应

- `BusbarPlanningDemoV2.BuildPlanFromScannedAssembly`
- `CreateBusbar`
- `CreateCollectorBusbar`

### 建议类

```text
BusbarPlanBuilder
BusbarConnectionPlanner
BusbarFactory
```

### 改进方向

当前 `BuildPlanFromScannedAssembly` 既识别设备、又规划汇流排、又创建铜排，应拆成几个服务按顺序调用。

## 9. Layer 6: Topology Layer

### 职责

- 判断两端连接属于同侧还是异侧。
- 不直接修改点位。
- 为补偿层提供拓扑结果。

### 输入

- `BusbarV2`
- 起终端口面。
- 工程结构类型。
- 搭接方式。

### 输出

- `ContactTopologyKind.SameSide` 或 `DifferentSide`。

### 当前对应

- `ContactTopologyResolver.Resolve`
- `ResolveMainFeedTopology`

### 建议类

```text
IContactTopologyResolver
ManualTopologyResolver
GeometryTopologyResolver
TopologyRuleTable
```

### 关键设计

不要把“是否补偿”直接写在工程场景判断里。应保持：

```text
工程场景 -> 同侧/异侧 -> 补偿策略 -> 建模点位
```

## 10. Layer 7: Compensation Layer

### 职责

- 增加端部裕度。
- 根据拓扑关系执行厚度转换。
- 输出最终钣金草图线。

### 输入

- `LogicalCenterline`
- `ConnectionPort.EndMarginMm`
- `ContactTopologyKind`
- `ThicknessTransitionPolicy`
- 铜排厚度。

### 输出

- `SheetMetalSketchLine`

### 当前对应

- `ContactTopologyResolver.CreateSheetMetalSketchLine`
- `AddEndMargins`
- `ApplyThicknessTransition`
- `MoveEndpointRun`

### 建议类

```text
EndMarginResolver
ThicknessTransitionResolver
SheetMetalSketchLineResolver
```

### 改进方向

将补偿方向从固定面法向进一步升级为：

```text
端点附近路径切向 + 连接面法向 + 草图平面法向 -> 厚度转换方向
```

这样可以支持更多非典设结构。

## 11. Layer 8: Path Planning Layer

### 职责

- 生成业务逻辑中心线。
- 只负责路径，不负责钣金厚度补偿。
- 支持不同路径策略。

### 输入

- 起点端口。
- 终点端口。
- 铜排类型。
- 路径策略。

### 输出

- `LogicalCenterline`

### 当前对应

- `BusbarRoutePlannerV2`
- `CreateMainFeedRoute`
- `CreateSimpleRoute`
- `CalculateMainFeedRouteDecision`

### 建议类

```text
IBusbarRoutePlanner
MainFeedRoutePlanner
OrthogonalRoutePlanner
ObstacleAvoidingRoutePlanner
RouteDecision
```

### 可扩展点

当前转接排的 `LeadOutY` 和 `ApproachZ` 已经独立成函数，是很好的接口雏形。后续可替换为自动计算。

## 12. Layer 9: Geometry / Sheet Metal Layer

### 职责

- 把钣金草图线变成 SolidWorks 草图。
- 调用 Sheet Metal Base Flange。
- 设置 R、K、厚度、MidPlane。

### 输入

- `SheetMetalSketchLine`
- `BusbarProfile`
- `SheetMetalOptions`

### 输出

- SolidWorks 钣金 Feature。

### 当前对应

- `CreateBusbarV2SheetMetalFeature`
- `CreateV2SheetMetalOpenProfileSketch`
- `CreateSheetMetalBaseFlangeFromSelectedSketch`
- `GetSheetMetalBaseFlangeExtent`
- `ApplySheetMetalParametersToCreatedFeature`

### 建议类

```text
SheetMetalPartBuilder
OpenProfileSketchBuilder
SheetMetalBaseFlangeBuilder
SketchPlaneResolver
SheetMetalParameterApplier
```

### 必须保留的经验

- 使用 `swEndCondMidPlane`。
- `Dist1 = Width`，`Dist2 = 0`。
- 不要用两个 Blind 半宽模拟 MidPlane。

## 13. Layer 10: Hole Builder Layer

### 职责

- 根据端口孔中心创建孔草图。
- 选择孔草图平面。
- 创建切除特征。

### 输入

- `MountingPorts`
- 孔径。
- 孔中心。
- 铜排厚度。

### 输出

- Cut Feature。

### 当前对应

- `CreateBusbarV2MountingHoles`
- `CreateBusbarV2MountingHole`
- `GetHoleSketchPlane`
- `CreateDirectedBlindCutFromActiveSketch`
- `TryCreateBlindCutFromCurrentSelection`

### 建议类

```text
HoleBuilder
HoleSketchPlaneResolver
CutFeatureBuilder
```

### 必须保留的经验

- 优先保持活动草图并立即 `FeatureCut4`。
- 重新选择草图切除只作为 fallback。

## 14. Layer 11: Persistence / Assembly Layer

### 职责

- 保存零件。
- 插入装配体。
- 设置组件变换。
- 关闭零件文档。

### 输入

- 零件文档。
- 装配体文档。
- 零件命名策略。

### 输出

- 文件路径。
- 装配体组件。

### 当前对应

- `SaveBusbarV2SheetMetalPart`
- `SaveDemoBusbarPart`
- `SaveBusbarPart`
- `InsertDemoPartIntoAssembly`
- `InsertBusbarPartIntoAssembly`
- `CloseBusbarPartDocument`

### 建议类

```text
PartFileSaver
AssemblyInserter
GeneratedPartNamer
```

## 15. Layer 12: Diagnostics Layer

### 职责

- 扫描 SW 文档。
- 打印参考点、坐标系、特征。
- 生成预览草图。
- 输出路径和补偿日志。

### 输入

- SW 文档。
- `BusbarPlanV2`。

### 输出

- 控制台日志。
- 预览零件。

### 当前对应

- `FeatureExtract.Program`
- `CreateBusbarV2PreviewPart`
- `CreateBusbarV2PreviewSketches`
- `PrintBusbar`
- `PrintCollector`

### 建议类

```text
FeatureExtractTool
BusbarPlanPreviewBuilder
GenerationLogger
```

## 16. 推荐目录结构

可以逐步演进为：

```text
C#/TopToDown/TopToDown
├─ Program.cs
├─ Application
│  ├─ AppRunner.cs
│  ├─ CommandLineOptions.cs
│  └─ GenerationMode.cs
├─ Models
│  ├─ Point3.cs
│  ├─ BusbarProfile.cs
│  ├─ FoundPoint.cs
│  ├─ ConnectionPort.cs
│  ├─ BusbarV2.cs
│  ├─ BusbarPlanV2.cs
│  └─ CollectorLayoutV2.cs
├─ Rules
│  ├─ ManualBusbarRuleSet.cs
│  ├─ IPortRuleProvider.cs
│  └─ ManualPortRuleProvider.cs
├─ Planning
│  ├─ BusbarPlanBuilderV2.cs
│  ├─ CollectorLayoutPlanner.cs
│  ├─ CollectorLengthController.cs
│  ├─ BusbarConnectionPlanner.cs
│  └─ BusbarRoutePlannerV2.cs
├─ Topology
│  ├─ IContactTopologyResolver.cs
│  ├─ ManualTopologyResolver.cs
│  └─ TopologyRuleTable.cs
├─ Compensation
│  ├─ EndMarginResolver.cs
│  ├─ ThicknessTransitionResolver.cs
│  └─ SheetMetalSketchLineResolver.cs
├─ SolidWorks
│  ├─ SolidWorksSession.cs
│  ├─ ReferencePointScanner.cs
│  ├─ OpenProfileSketchBuilder.cs
│  ├─ SheetMetalBuilder.cs
│  ├─ HoleBuilder.cs
│  ├─ PartFileSaver.cs
│  └─ AssemblyInserter.cs
├─ Diagnostics
│  ├─ BusbarPlanPreviewBuilder.cs
│  └─ GenerationLogger.cs
└─ Legacy
   └─ V1RouteBuilder.cs
```

## 17. 未来扩展性分析

### 17.1 增加南方电网配电箱结构

当前支持程度：部分支持。

已有基础：

- `CabinetTopologyKind` 已有 `TypicalDesign` 和 `SouthernGrid`。
- `ManualBusbarRuleSet.GetFuseOutFace` 已按结构类型返回刀熔 OUT 贴合面。
- 转接排同侧/异侧规则中已经考虑了刀熔在 `Front` 或 `Back` 的情况。

不足：

- 结构类型当前在 `BuildPlanFromScannedAssembly` 里写死为 `TypicalDesign`。
- 南网结构下的汇流排位置、路径引出方向、搭接面还没有配置入口。
- 分支排仍默认 SameSide，缺少完整拓扑规则表。

改进方案：

- 通过配置或 CLI 选择 `CabinetTopologyKind`。
- 把转接排和分支排都接入统一拓扑规则表。
- 把端口默认面、默认引出方向、搭接面做成规则配置。

### 17.2 支持不同汇流排位置

当前支持程度：弱到中等。

已有基础：

- `CollectorLayoutPlannerV2.CreateLayout` 集中计算汇流排中心。
- `CollectorTopClearanceY`、`CollectorOffsetFromLoubaoInZ`、`CollectorPhaseSpacing` 是参数。

不足：

- 当前位置公式仍假设汇流排在漏保上方并有固定 Z 偏移。
- 没有设备包络和避让信息。
- 没有目标函数判断哪个位置更优。

改进方案：

- 抽出 `ICollectorPositionStrategy`。
- V0 保留当前公式策略。
- V1 增加手动指定策略。
- V2 增加自动优化策略，输入设备包络和安全间隙。

### 17.3 支持不同铜排搭接方式

当前支持程度：中等。

已有基础：

- 有 `ContactFace`。
- 有 `MainFeedCollectorFace` 和 `BranchCollectorFace`。
- 有 `SameSide/DifferentSide` 概念。
- 有厚度补偿函数。

不足：

- `ResolveMainFeedTopology` 只覆盖转接排主要场景。
- 分支排默认 SameSide。
- 补偿方向目前主要靠连接面法向，不够完整表达局部几何。

改进方案：

- 建立 `TopologyRuleTable`。
- 每条规则至少包含：结构类型、铜排类型、起点面、终点面、搭接面、拓扑结果、默认补偿端。
- 厚度转换方向升级为由路径切向和接触面共同推导。

### 17.4 自动优化汇流排长度

当前支持程度：已初步支持。

已有基础：

- `BusbarLengthControllerV2` 已经根据连接范围动态计算长度。
- 当前已考虑最外侧铜排半宽，避免孔半露。
- X- 侧外伸 50mm 已参数化为 `CollectorNegativeXExtendMm`。

不足：

- 只考虑 X 向范围。
- 不考虑安装孔边距、端部倒角、标准长度、制造余量。
- 没有按设计规范切换外伸策略。

改进方案：

- 输入从 `CollectorConnectionExtentV2` 扩展为 `CollectorConnectionEnvelope`。
- 输出包含 `StartX/EndX/Length/Reason`。
- 规则支持：无外伸、单侧外伸、双侧外伸、标准长度取整。

### 17.5 自动优化汇流排位置

当前支持程度：暂不支持，仅保留参数化位置公式。

原因：

- 当前没有设备外形包络。
- 没有障碍物模型。
- 没有安全距离和最小折弯距离约束。
- 没有评价函数。

改进方案：

```text
输入：设备端口、设备包络、柜体边界、铜排规格、间隙规则
候选：生成若干汇流排位置
评估：路径长度、折弯次数、是否碰撞、安装空间、制造余量
输出：最优 CollectorLayoutV2
```

建议新增：

```text
DeviceEnvelope
CabinetBoundary
ClearanceRule
CollectorPositionCandidate
CollectorPositionOptimizer
```

## 18. 渐进式重构路线

### 阶段 1：命名和边界清理

目标：不改变行为，只让主线更清楚。

建议操作：

- `InsertDemoPartIntoAssembly` 重命名为 `InsertPartIntoAssembly`。
- `BusbarPlanningDemoV2` 重命名为 `BusbarPlanBuilderV2`。
- 给 V1 入口和函数加 `Legacy` 命名或区域标记。
- 把 `--sheetmetal-v2-all` 标为当前推荐主流程。

风险：低。

### 阶段 2：抽出基础模型

目标：让业务层不再依赖 `Program.cs` 的模型。

建议操作：

- `Point3` -> `Models/Point3.cs`
- `BusbarProfile` -> `Models/BusbarProfile.cs`
- `BusbarKind`、`AxisDirection` 等枚举 -> `Models/Enums.cs`
- `BusbarSettings` -> `Configuration/BusbarSettings.cs`

风险：中低。需要处理命名空间和编译引用。

### 阶段 3：拆 V2 业务框架

目标：把 `BusbarFramework.cs` 拆成稳定模块。

建议操作：

- `ManualBusbarRuleSet`、`ManualPortRuleProvider` -> `Rules/`
- `CollectorLayoutPlannerV2`、`BusbarLengthControllerV2`、`BusbarRoutePlannerV2` -> `Planning/`
- `ContactTopologyResolver` 拆成 `Topology/` 和 `Compensation/`
- `BusbarPlanBuilderV2` -> `Planning/BusbarPlanBuilderV2.cs`

风险：中。需要保证 V2 输出点位完全一致。

### 阶段 4：抽出 SolidWorks Builder

目标：让 `Program.cs` 不再直接堆 SW API 细节。

建议操作：

- `SolidWorksSession`
- `ReferencePointScanner`
- `SheetMetalBuilder`
- `HoleBuilder`
- `PartFileSaver`
- `AssemblyInserter`

风险：中高。SW API 对选择状态敏感，迁移时必须保持调用顺序。

### 阶段 5：配置化规则

目标：支持 TypicalDesign 和 SouthernGrid 切换。

建议操作：

- 增加 JSON 或 Excel 配置导入。
- `ManualBusbarRuleSet.CreateDefault` 只作为默认模板。
- CLI 支持 `--topology typical` / `--topology southern-grid`。

风险：中。

### 阶段 6：自动优化能力

目标：支持不同汇流排位置、自动避让、自动优化。

建议操作：

- 增加设备包络模型。
- 增加汇流排位置候选和评分。
- 增加路径避障规划器。

风险：高，应在现有主线稳定后再做。

## 19. 推荐测试策略

当前项目依赖 SolidWorks COM，不适合所有逻辑都靠 SW 实测。建议分两类测试。

### 19.1 纯业务测试

不启动 SolidWorks，直接测试：

- 端口规则是否正确。
- 汇流排长度是否覆盖所有连接范围。
- 转接排路径是否符合预期。
- 分支排路径是否符合预期。
- 同侧/异侧判断是否符合规则表。
- 异侧补偿是否移动正确端点。

这些测试应覆盖 TypicalDesign 和 SouthernGrid。

### 19.2 SolidWorks 集成测试

启动 SolidWorks 后测试：

- 能否扫描到参考点。
- 2D 开放轮廓草图是否正确生成。
- Sheet Metal Base Flange 是否使用 MidPlane。
- R=5mm、K=0.47 是否写入。
- 孔是否能切除。
- 零件是否能保存并插入装配体。

## 20. 最小可行重构目标

如果只做一轮较小重构，建议目标是：

```text
Program.cs 只负责：
1. 解析参数
2. 调用 PlanningService 得到 BusbarPlanV2
3. 调用 SolidWorksGenerationService 生成装配
```

理想主流程会变成：

```csharp
CommandLineOptions options = CommandLineOptions.Parse(args);
SolidWorksSession session = SolidWorksSession.ConnectOrStart();
AssemblyContext assembly = session.GetActiveAssembly();

List<FoundPoint> points = scanner.Scan(assembly);
BusbarPlanV2 plan = planBuilder.Build(points, options.RuleSet);

sheetMetalGenerationService.Generate(assembly, plan.Busbars);
```

这样以后增加南网结构时，主要改规则和规划层；增加新的 SolidWorks API 技巧时，主要改生成层。两边不会互相污染。

## 21. 优先级建议

| 优先级 | 建议 | 原因 |
| --- | --- | --- |
| P0 | 保护当前 V2 主流程，不先大改 SW API 顺序。 | 当前已经跑通，SW API 对选择状态敏感。 |
| P1 | 抽出基础模型和规则/规划层。 | 这是未来支持多结构的前提。 |
| P1 | 拆 `ContactTopologyResolver`。 | 同侧/异侧和补偿是后续扩展核心。 |
| P2 | 把孔生成独立为 `HoleBuilder`。 | 孔逻辑复杂且容易继续扩展。 |
| P2 | 把保存/插入独立出来并修正 Demo 命名。 | 降低误解，提高主流程可读性。 |
| P3 | 配置化 `ManualBusbarRuleSet`。 | 支持南网结构和更多项目参数。 |
| P3 | 自动汇流排位置优化。 | 需要更多几何信息，暂时不宜抢跑。 |

## 22. 最终目标架构

```text
扫描层只知道 SolidWorks 里有什么点。
端口层把点变成工程连接语义。
规划层决定铜排怎么连、汇流排在哪里、路径怎么走。
拓扑层判断同侧/异侧。
补偿层把业务路径转换为可建模草图线。
建模层只负责把草图线变成钣金和孔。
装配层只负责保存和插入。
```

这套架构的关键价值是：

- 典设箱、南网箱只是规则输入不同。
- 上搭、下搭、同侧、异侧只是拓扑和补偿策略不同。
- 汇流排位置固定或自动优化只是 Collector Planning 策略不同。
- SolidWorks API 技巧不会污染业务规则。
