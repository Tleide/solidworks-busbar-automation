# 脚本与函数导览

这份文档按“文件 -> 类/函数 -> 作用”的方式说明当前主工程。它适合在你准备修改某个功能前快速定位：应该改哪个目录、哪个脚本、哪个函数。

阅读建议：

```text
先看 Program 和 SolidWorksGenerationRunner 理解主线
再看 Planning 理解业务生成
再看 Rules 理解默认规则
最后看 SolidWorks 理解具体建模 API
```

## 1. 入口与应用层

### `Program.cs`

职责：应用入口、默认参数、命令行参数解析。

适合修改的场景：

- 增加新的命令行参数。
- 临时调整默认铜排规格、相间距、折弯半径等。
- 以后接 UI 前，把这些默认参数迁到 `GenerationOptions`。

主要成员：

| 名称 | 作用 |
| --- | --- |
| `Settings` | 当前默认生成参数，包括主排/汇流排/分支排规格、汇流排位置、折弯半径、K 因子等。 |
| `PhaseNames` | 当前三相顺序，默认为 `A/B/C`。 |
| `_replaceExistingBusbar` | 是否删除旧的 `Busbar_*` 组件。 |
| `_verboseFeatureScan` | 是否输出详细特征扫描日志。 |
| `_previewOnly` | 是否只生成预览骨架。 |
| `Main(args)` | 程序入口，负责异常处理并调用 `RunSolidWorksGeneration()`。 |
| `ConfigureFromArgs(args)` | 解析 `--verbose`、`--keep-existing`、`--preview`。 |

### `App/ProgramUtilities.cs`

职责：入口层通用小工具。

| 函数 | 作用 |
| --- | --- |
| `SameText(left, right)` | 忽略大小写比较字符串，常用于点名、组件名、特征类型判断。 |
| `Mm(value)` | 毫米转米。SolidWorks API 长度单位是米。 |
| `ToMm(value)` | 米转毫米，主要用于日志显示。 |

## 2. CAD 抽象层

### `CadAbstractions/CadPartSpecs.cs`

职责：定义 CAD 中立的钣金零件描述。当前只是接口雏形，主流程尚未使用。

| 类 | 作用 |
| --- | --- |
| `SheetMetalPartSpec` | 描述一根待生成钣金铜排：零件名、种类、截面、折弯半径、K 因子、中心线、孔。 |
| `HoleSpec` | 描述一个孔：名称、中心点、孔径、所在贴合面。 |

适合后续修改的场景：

- 想让 SolidWorks 和 UG 共用同一份“生成意图”。
- 想从 `Busbar` 转换出 CAD 中立数据。
- 想把 SolidWorks 层改成只消费 `SheetMetalPartSpec`。

### `CadAbstractions/ICadSheetMetalBuilder.cs`

职责：CAD 后端接口雏形。

| 接口/函数 | 作用 |
| --- | --- |
| `ICadSheetMetalBuilder.CreateSheetMetalPart(spec)` | 未来由 SolidWorks、UG/NXOpen 等后端分别实现。 |

## 3. 领域模型层

这一层只表达业务对象，不应该出现 SolidWorks API。

### `Domain/BusbarGenerationSettings.cs`

职责：当前默认生成设置。

| 成员/函数 | 作用 |
| --- | --- |
| `MainFeedWidthMm / MainFeedThicknessMm` | 转接排规格。 |
| `CollectorWidthMm / CollectorThicknessMm` | ABC 汇流排规格。 |
| `BranchWidthMm / BranchThicknessMm` | ABC 分支排规格。 |
| `NeutralCollectorWidthMm / NeutralCollectorThicknessMm` | N 汇流排规格。 |
| `NeutralBranchWidthMm / NeutralBranchThicknessMm` | N 分支排规格。 |
| `CollectorPhaseSpacingMm` | 汇流排相间距。 |
| `CollectorTopClearanceYMm` | 汇流排相对漏保上方净距。 |
| `CollectorOffsetFromLoubaoInZMm` | 汇流排相对漏保的 Z 偏移。 |
| `CollectorNegativeXExtendMm` | 汇流排 X- 侧外伸。 |
| `MainLeadOutYMm` | 转接排从刀熔端初始 Y 方向引出距离。 |
| `SheetMetalBendRadiusMm` | 当前默认折弯半径。后续应迁到 `BendRadiusRules`。 |
| `SheetMetalKFactor` | 钣金 K 因子。 |
| `MainCollectorFrontClearanceMm` | 转接排靠近汇流排前的 Z 向避让距离。 |
| `MainFeedProfile / CollectorProfile / BranchProfile` | 把宽厚参数转成 `BusbarProfile`。 |
| `GetSheetMetalWidthSide(kind)` | 根据铜排种类返回钣金宽度方向模式。 |

### `Domain/BusbarModels.cs`

职责：铜排业务主对象。

| 类/函数 | 作用 |
| --- | --- |
| `SheetMetalOptions` | 钣金规则结果：折弯半径、K 因子、宽度模式。 |
| `SheetMetalOptions.FromRules(rules)` | 从当前规则集生成钣金选项。 |
| `BusbarRoutingOptions` | 路径规则选项：轴顺序、厚度过渡策略。 |
| `ConnectionPort` | 可连接铜排的工程端口，包含孔中心、贴合面、引出方向、端部裕度、孔径。 |
| `ConnectionPort.ToString()` | 调试日志用，输出端口详细信息。 |
| `Busbar` | 一根铜排，包括起终端口、规格、逻辑中心线、钣金草图线、打孔端口。 |
| `CollectorLayout` | 某相汇流排的位置、长度、Tap 端口集合。 |
| `BusbarPlan` | 当前装配体完整铜排规划结果。 |

### `Domain/BusbarProfile.cs`

职责：铜排截面规格。

| 成员/函数 | 作用 |
| --- | --- |
| `WidthMm / ThicknessMm` | 宽度、厚度，单位 mm。 |
| `Width / Thickness` | 宽度、厚度，单位 m，供 SolidWorks API 使用。 |
| `TerminalFaceOffset` | 端面偏移，当前等于厚度一半。 |
| `Label` | 文件命名用标签，例如 `6x60`。 |

### `Domain/Enums.cs`

职责：集中定义业务枚举。

| 枚举 | 作用 |
| --- | --- |
| `CabinetTopologyKind` | 柜体/典设拓扑类型，当前有 `TypicalDesign`、`SouthernGrid`。 |
| `ContactFace` | 端口或搭接面：`Front/Back/Upper/Lower/Left/Right`。 |
| `BusbarWidthMode` | 铜排宽度生成模式，当前为 `MidPlane`。 |
| `ThicknessTransitionPolicy` | 厚度补偿策略。 |
| `ContactTopologyKind` | 同侧/异侧拓扑。 |
| `PortKind` | 端口类型：刀熔出线、漏保进线、汇流排 Tap。 |
| `RouteAxisOrder` | 简单路径的轴移动顺序。 |
| `BusbarKind` | 铜排类型：转接排、汇流排、分支排。 |
| `SheetMetalWidthSide` | 开放轮廓钣金宽度向哪侧生成。 |
| `AxisDirection` | 坐标轴方向。 |

### `Domain/FoundPoint.cs`

职责：装配体扫描得到的参考点。

| 成员/函数 | 作用 |
| --- | --- |
| `ComponentName` | 参考点所属组件。 |
| `PointName` | 参考点名称，例如 `A_OUT`。 |
| `Position` | 已转换到装配体坐标的点。 |
| `ToString()` | 调试输出。 |

### `Domain/LoubaoGroup.cs`

职责：识别到的漏保组件简要信息。

| 成员 | 作用 |
| --- | --- |
| `ComponentName` | 漏保组件名。 |
| `CenterX` | 该漏保所有进线点的平均 X，用于排序。 |

### `Domain/Point3.cs`

职责：三维点。

| 函数 | 作用 |
| --- | --- |
| `DistanceTo(other)` | 计算两点距离，常用于判断重复点。 |
| `ToMillimeterText()` | 按毫米格式输出坐标。 |

### `Domain/SheetMetalBaseFlangeExtent.cs`

职责：描述开放轮廓钣金宽度方向参数。

| 成员 | 作用 |
| --- | --- |
| `Dist1 / Dist2` | Base Flange 宽度参数。 |
| `EndCondition1 / EndCondition2` | SolidWorks 端条件。 |
| `DirToUse` | SolidWorks 方向参数。 |
| `ModeLabel` | 日志显示当前模式。 |

### `Domain/SheetMetalOpenProfilePlane.cs`

职责：描述开放轮廓草图或孔草图所在平面。

| 成员 | 作用 |
| --- | --- |
| `BasePlaneRole` | 基准平面角色：`Top/Front/Right`。 |
| `Offset` | 相对基准面的偏移，单位 m。 |
| `SketchAxis1 / SketchAxis2` | 草图平面内的两个坐标轴。 |

## 4. 规则层

### `Rules/ManualBusbarRuleSet.cs`

职责：当前默认规则集合。未来可以逐步被 Excel 数据层、UI 参数和更细规则类替换。

| 成员/函数 | 作用 |
| --- | --- |
| `CreateDefault(topologyKind)` | 创建当前默认规则。 |
| `DefaultEndMarginMm` | 默认端部裕度。 |
| `BendRadiusMm / KFactor` | 默认钣金参数。 |
| `MainFeedCollectorFace / BranchCollectorFace` | 转接排、分支排与汇流排的搭接面。 |
| `MainFeedStartHoleDiameterMm` 等孔径字段 | 当前各类端口默认孔径。 |
| `GetFuseOutFace()` | 根据拓扑返回刀熔出线端贴合面。 |
| `GetLoubaoInFace()` | 返回漏保进线端贴合面。 |

### `Rules/ManualPortRuleProvider.cs`

职责：把扫描点转换为带规则属性的 `ConnectionPort`。

| 函数 | 作用 |
| --- | --- |
| `CreateFuseOutPort(phase, point)` | 为刀熔出线点创建端口，并设置起点裕度、孔径。 |
| `CreateLoubaoInPort(phase, index, point)` | 为漏保进线点创建端口。 |
| `CreateCollectorTapPort(phase, name, point, face)` | 在汇流排上创建搭接 Tap 端口。 |
| `CreateDevicePort(...)` | 通用设备端口创建函数。 |

## 5. 规划层

### `Planning/BusbarPlanBuilder.cs`

职责：业务规划总入口，从扫描点生成完整 `BusbarPlan`。

最适合修改的场景：

- 改变生成哪些铜排。
- 改变 A/B/C/N 的组织关系。
- 接入新的孔位规划、搭接规则、标准件规则。

| 函数 | 作用 |
| --- | --- |
| `BuildPlanFromScannedAssembly(...)` | 主入口：创建规则、识别设备、为 ABC/N 生成铜排计划。 |
| `AddNeutralCollectorAndBranches(...)` | 处理 N 相汇流排和 N 分支排。 |
| `CreateNeutralLoubaoInputs(...)` | 收集每个漏保的 `N_IN`，并检查是否部分缺失。 |
| `CreateCollectorBusbar(...)` | 根据 `CollectorLayout` 创建一根汇流排对象。 |
| `CreateCollectorEndPort(...)` | 创建汇流排起止端口，不参与打孔。 |
| `ApplyMainFeedCollectorTapRules(...)` | 给转接排与汇流排搭接 Tap 设置裕度和孔径。 |
| `ApplyBranchDevicePortRules(...)` | 给分支排设备侧端口设置孔径。 |
| `ApplyBranchCollectorTapRules(...)` | 给分支排汇流排侧 Tap 设置孔径。 |
| `ApplyCollectorTapHoleRules(...)` | 给汇流排 Tap 设置孔径。 |
| `CreateBusbar(...)` | 创建转接排或分支排，生成逻辑路径、草图线和打孔端口。 |
| `AddMountingPortIfNeeded(...)` | 如果端口孔径有效，就加入铜排打孔列表。 |
| `CloneConnectionPort(...)` | 克隆端口，避免后续修改原始端口影响打孔数据。 |
| `FindFuseComponent(...)` | 从扫描点里识别刀熔组件。 |
| `FindLoubaoGroups(...)` | 从扫描点里识别漏保组件并按 X 排序。 |
| `FindRequiredPoint(...)` | 查找必需参考点，缺失时报错。 |
| `ScoreNameHint(...)` | 根据组件名关键词给刀熔/漏保识别打分。 |

### `Planning/CollectorLayoutPlanner.cs`

职责：计算汇流排的位置和 Tap 点。

| 函数/类 | 作用 |
| --- | --- |
| `CreateLayout(...)` | 根据相序、刀熔端口、漏保端口计算汇流排中心、Y/Z 位置和 X 范围。 |
| `CreateTap(...)` | 在汇流排指定 X 位置创建 Tap 端口。 |
| `CreateConnectionExtents(...)` | 根据连接铜排宽度计算汇流排长度覆盖范围。 |
| `CollectorLengthRange` | 汇流排 X 起止范围。 |
| `CollectorConnectionExtent` | 一个连接点在 X 方向的占用范围。 |
| `BusbarLengthController.Calculate(...)` | 根据所有连接范围计算汇流排 StartX/EndX。 |

### `Planning/BusbarRoutePlanner.cs`

职责：生成孔中心意义上的逻辑路径。

| 函数/类 | 作用 |
| --- | --- |
| `CreateRoute(...)` | 根据铜排类型选择转接排路径或简单路径。 |
| `CreateMainFeedRoute(...)` | 生成刀熔到汇流排的转接排折线路径。 |
| `CreateSimpleRoute(...)` | 生成分支排简单折线路径。 |
| `CalculateMainFeedRouteDecision(...)` | 计算转接排 Y 引出和 Z 避让策略。 |
| `CalculateMainFeedLeadOutY(...)` | 根据端口引出方向计算初始 Y 引出距离。 |
| `CalculateMainFeedApproachZ(...)` | 计算转接排进入汇流排前的 Z 方向避让位置。 |
| `CalculateMainFeedApproachOffsetZ(...)` | 当前使用汇流排宽度、转接排宽度、前方净距计算 Z 避让量。 |
| `MainFeedRouteDecision` | 记录转接排路径决策结果和说明文字。 |

### `Planning/ContactTopologyResolver.cs`

职责：从逻辑路径生成实际钣金草图线，处理端部裕度和厚度补偿。

最适合修改的场景：

- 改同侧/异侧搭接判断。
- 改厚度补偿方向。
- 改端部裕度如何外伸。

| 函数 | 作用 |
| --- | --- |
| `CreateSheetMetalSketchLine(busbar)` | 主入口：添加端部裕度，再应用厚度过渡补偿。 |
| `IsSameSide(start, end)` | 简单判断两个端口贴合面是否相同。 |
| `Resolve(busbar)` | 解析铜排同侧/异侧拓扑。 |
| `ResolveMainFeedTopology(busbar)` | 当前转接排专用同侧/异侧判断。 |
| `ApplyThicknessTransition(...)` | 根据拓扑对草图线端部做厚度方向补偿。 |
| `ResolveForTransition(...)` | 决定某根铜排是否需要厚度过渡。 |
| `ShouldCompensateStart(...)` | 根据策略决定补偿起点还是终点。 |
| `GetEndpointTangent(...)` | 计算端点处路径切向。 |
| `ChooseThicknessNormal(...)` | 根据贴合面选择厚度补偿法向。 |
| `InferPlanarNormal(...)` | 无明确贴合面时推断法向。 |
| `MoveEndpointRun(...)` | 移动端点附近一段共线点，避免只移动单点造成折线异常。 |
| `SharesTwoCoordinates(...)` | 判断两点是否同属一段轴向直线。 |
| `FindPointIndex(...)` | 在草图点集合里找到逻辑端点。 |
| `AddEndMargins(...)` | 按端部裕度把草图线从孔中心向外延伸。 |

## 6. SolidWorks 适配层

这一层集中处理 SolidWorks API。后续如果换 UG/NXOpen，大部分需要重写的是这一层，而不是 `Domain/Rules/Planning`。

### `SolidWorks/SolidWorksGenerationRunner.cs`

职责：当前 SolidWorks 后端主流程。

| 函数 | 作用 |
| --- | --- |
| `RunSolidWorksGeneration()` | 连接 SW、扫描、生成计划、预览或实体生成。 |

### `SolidWorks/SolidWorksSession.cs`

职责：SolidWorks 会话和文档管理。

| 函数 | 作用 |
| --- | --- |
| `GetActiveOrOpenAssembly(swApp)` | 获取当前激活装配体；如果当前不是装配体，尝试切换到已打开装配体。 |
| `GetOrStartSolidWorks()` | 连接正在运行的 SolidWorks；没有则启动一个新实例。 |
| `ActivateDocument(swApp, model)` | 激活指定文档。 |

### `SolidWorks/AssemblyScanner.cs`

职责：扫描参考点，删除旧铜排组件。

| 函数 | 作用 |
| --- | --- |
| `ScanReferencePoints(...)` | 扫描装配体自身和各组件内部参考点。 |
| `DeleteExistingBusbarComponents(...)` | 删除装配体中旧的 `Busbar_*` 组件。 |
| `DumpModelFeatures(...)` | 遍历一个模型的 Feature 树。 |
| `TryReadReferencePoint(...)` | 尝试把 Feature 当作 `RefPoint` 读取，并转换成 `FoundPoint`。 |
| `TransformPoint(...)` | 将组件局部点转换到装配体坐标。 |

### `SolidWorks/BusbarBatchBuilder.cs`

职责：选择生成顺序并批量创建铜排零件。

| 函数 | 作用 |
| --- | --- |
| `SelectBusbarsForSheetMetalBatch(plan)` | 选择实体生成顺序：主排、汇流排、分支排、N 排。 |
| `FindRequiredBusbar(...)` | 查找必须存在的铜排，不存在时报错。 |
| `FindBusbars(...)` | 按相位和类型查找铜排。 |
| `FindOptionalBusbars(...)` | 查找可选铜排，例如 N 相。 |
| `CreateBusbarSheetMetalParts(...)` | 批量生成铜排。 |
| `CreateBusbarSheetMetalPart(...)` | 生成单根铜排：建新零件、建钣金、打孔、保存、插回装配体。 |
| `CreateBusbarSheetMetalFeature(...)` | 创建单根铜排的钣金主体 Feature。 |

### `SolidWorks/CadGeometryUtilities.cs`

职责：SolidWorks 草图和几何辅助。

| 函数 | 作用 |
| --- | --- |
| `CreateLineOrThrow(...)` | 创建草图线，失败时报错。 |
| `NewPartDocument(swApp)` | 新建零件文档。 |
| `GetOpenProfilePlane(...)` | 根据铜排中心线判断开放轮廓草图应在哪个基准平面。 |
| `AllSameCoordinate(...)` | 判断一组点是否在某个坐标轴上保持常量。 |
| `GetCoordinate(...)` | 按轴读取点坐标。 |
| `CreateOffsetPlane(...)` | 基于默认基准面创建偏移平面。 |
| `FindLastFeatureByType(...)` | 查找最后一个指定类型 Feature。 |
| `FindDefaultPlane(...)` | 查找默认 Top/Front/Right 平面。 |
| `ModelPointToSketchPoint(...)` | 将模型坐标转换成草图坐标。 |
| `FlattenSketchPoint(...)` | 把草图点压到 Z=0 的二维草图平面。 |
| `FindFirstFeatureByType(...)` | 查找第一个指定类型 Feature。 |

### `SolidWorks/SheetMetalSketchBuilder.cs`

职责：创建开放轮廓钣金草图。

| 函数 | 作用 |
| --- | --- |
| `CreateSheetMetalOpenProfileSketch(...)` | 创建偏移平面，打开草图，把 `SheetMetalSketchLine` 画成开放折线。 |

### `SolidWorks/SheetMetalFeatureBuilder.cs`

职责：从开放轮廓草图生成钣金。

| 函数 | 作用 |
| --- | --- |
| `CreateSheetMetalBaseFlangeFromSelectedSketch(...)` | 调用 SolidWorks `InsertSheetMetalBaseFlange2` 生成 MidPlane 钣金。 |
| `GetSheetMetalBaseFlangeExtent(...)` | 根据宽度方向模式计算 `Dist1/Dist2/EndCondition`。 |
| `CreateKFactorBendAllowance(...)` | 创建 K 因子折弯扣除对象。 |
| `ApplySheetMetalParametersToCreatedFeature(...)` | 对新建钣金 Feature 再尝试应用厚度等参数。 |

### `SolidWorks/MountingHoleBuilder.cs`

职责：按端口孔中心创建孔。

最适合修改的场景：

- 改打孔方向。
- 改孔草图平面。
- 后续把单孔换成多孔时，这里会消费 `HoleLayoutPlanner` 的结果。

| 函数 | 作用 |
| --- | --- |
| `CreateBusbarMountingHoles(...)` | 遍历 `MountingPorts` 打孔；如果为空则回退起终端口。 |
| `CreateBusbarMountingHole(...)` | 创建单个孔草图并切除。 |
| `GetHoleSketchPlane(port)` | 根据端口贴合面选择孔草图平面。 |
| `CreateDirectedBlindCutFromSketch(...)` | 从已存在草图创建定向盲切。 |
| `ShouldReverseHoleCutDirection(port)` | 根据贴合面判断切除方向是否反向。 |
| `TryCreateBlindCut(...)` | 标准盲切尝试。 |
| `TryCreateBlindCutWithScope(...)` | 带 FeatureScope 参数的盲切尝试。 |
| `CreateDirectedBlindCutFromActiveSketch(...)` | 优先从当前激活草图直接切除。 |
| `TryCreateBlindCutFromCurrentSelection(...)` | 从当前选择集创建切除。 |
| `SelectSketchForCut(...)` | 选择切除草图。 |

### `SolidWorks/PartPersistenceService.cs`

职责：保存零件、插入装配、关闭临时文档。

| 函数 | 作用 |
| --- | --- |
| `SaveBusbarSheetMetalPart(...)` | 按铜排名称、规格、时间戳保存钣金零件。 |
| `SaveGeneratedPart(...)` | 保存通用生成零件，预览等流程可用。 |
| `InsertPartIntoAssembly(...)` | 将保存的零件插回装配体，并设置单位变换。 |
| `CloseBusbarPartDocument(...)` | 保存并插入后关闭生成的零件文档。 |

### `SolidWorks/PreviewBuilder.cs`

职责：生成预览骨架，不生成实体铜排。

| 函数 | 作用 |
| --- | --- |
| `CreateBusbarPreviewPart(...)` | 新建预览零件并插回装配。 |
| `CreateBusbarPreviewSketches(...)` | 为每根铜排生成逻辑路径和钣金草图线预览。 |
| `CreatePreview3DPolylineSketch(...)` | 创建 3D 折线草图。 |
| `ToSafeFeatureName(name)` | 把名称转换为适合 Feature 的安全名称。 |

## 7. FeatureExtract 诊断工具

位置：`C#/FeatureExtract/FeatureExtract/Program.cs`

职责：独立诊断工具，用于看 SolidWorks 能读到哪些 Feature、RefPoint、坐标。

常用场景：

- 你不确定模型里的参考点是否命名正确。
- 你想确认零件局部坐标和装配体坐标转换是否正确。
- 你想检查某个零件是否存在 `RefPoint` 或坐标系。

关键函数：

| 函数 | 作用 |
| --- | --- |
| `Main()` | 连接已运行 SolidWorks，读取当前活动文档。 |
| `GetRunningSolidWorks()` | 只连接已有 SW，不主动启动。 |
| `PrintDocumentHeader(model)` | 打印当前文档标题、路径、类型。 |
| `ScanAssembly(swApp, assembly)` | 遍历装配体组件并扫描各组件特征。 |
| `ScanModelFeatures(...)` | 遍历 Feature 树，打印重要特征。 |
| `TryPrintReferencePoint(...)` | 读取并打印参考点局部/装配坐标。 |
| `TryPrintCoordinateSystem(...)` | 打印坐标系 Feature 信息。 |
| `TransformPoint(...)` | 将组件局部点转换到装配体坐标。 |

## 8. 修改方向速查

| 你想改什么 | 优先看哪里 |
| --- | --- |
| 默认铜排规格、相间距、折弯半径 | `Program.cs`、后续迁到 `BusbarGenerationSettings` / `BendRadiusRules` |
| 端口孔径、端部裕度 | `Rules/ManualBusbarRuleSet.cs`、`Planning/BusbarPlanBuilder.cs` |
| 刀熔/漏保识别规则 | `Planning/BusbarPlanBuilder.cs` 的 `FindFuseComponent`、`FindLoubaoGroups` |
| 汇流排位置和长度 | `Planning/CollectorLayoutPlanner.cs` |
| 转接排/分支排路径 | `Planning/BusbarRoutePlanner.cs` |
| 同侧/异侧、厚度补偿 | `Planning/ContactTopologyResolver.cs` |
| 打孔方向和孔草图 | `SolidWorks/MountingHoleBuilder.cs` |
| 钣金 MidPlane 参数 | `SolidWorks/SheetMetalFeatureBuilder.cs` |
| 读取 SolidWorks 参考点 | `SolidWorks/AssemblyScanner.cs` |
| 保存和插回装配体 | `SolidWorks/PartPersistenceService.cs` |
| 接 Excel 数据层 | 后续新增 `Data/ExcelDataRepository.cs` |
| 接 UI | 后续新增 `GenerationRequest` / `GenerationOptions`，UI 填请求对象 |
| 支持 UG/NXOpen | 新增 CAD 后端，实现 `ICadSheetMetalBuilder` |
