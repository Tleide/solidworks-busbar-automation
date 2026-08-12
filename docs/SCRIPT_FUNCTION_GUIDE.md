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

职责：保存当前默认工程参数，并提供进程级异常边界。

适合修改的场景：

- 临时调整默认铜排规格、相间距、折弯半径等。
- 以后接 UI 前，把这些默认参数迁到配置文件或 UI 输入模型。

主要成员：

| 名称 | 作用 |
| --- | --- |
| `Settings` | 当前默认生成参数，包括主排/汇流排/分支排规格、汇流排位置、折弯半径、K 因子等。 |
| `GenerationOptions` | 一次运行的不可共享开关集合，由解析器创建后传给 Runner 和建模器，不再使用全局可变标志。 |
| `Main(args)` | 解析命令行，创建 `SolidWorksGenerationRunner`，统一捕获未处理异常并返回进程退出码。 |

### `App/GenerationOptionsParser.cs`

职责：严格解析命令行参数，拒绝未知参数和互斥模式组合。新增命令行参数时改这里，并同步补充单元测试。

| 函数 | 作用 |
| --- | --- |
| `Parse(args)` | 支持 `--verbose`、`--keep-existing`、`--preview`、`--validate`、`--verify-geometry`、`--export-report`、`--only=...`。 |
| `IsOption(actual, expected)` | 忽略大小写比较无值开关。 |

### `SolidWorks/BusbarBuilderUtilities.cs`

职责：`SolidWorksBusbarPartBuilder` 各个 partial 文件共用的字符串比较和单位换算。

| 函数 | 作用 |
| --- | --- |
| `SameText(left, right)` | 忽略大小写比较字符串，常用于点名、组件名、特征类型判断。 |
| `Mm(value)` | 毫米转米。SolidWorks API 长度单位是米。 |
| `ToMm(value)` | 米转毫米，主要用于日志显示。 |

## 2. 领域模型层

这一层只表达业务对象，不应该出现 SolidWorks API。

### `Domain/BusbarGenerationSettings.cs`

职责：当前默认生成设置。

| 成员/函数 | 作用 |
| --- | --- |
| `MainFeedWidthMm / MainFeedThicknessMm` | 转接排规格。 |
| `CollectorWidthMm / CollectorThicknessMm` | ABC 汇流排规格。 |
| `PhaseBranchRules` | ABC 漏保额定电流到分支排规格和默认单双排的代码内规则表；当前覆盖 `250A/4x20/单排`、`400A/4x30/双排`、`630A/4x40/双排`。 |
| `PhaseBranchArrangementOverride` | 项目级 ABC 拓扑覆盖。为 `null` 时使用每台漏保规则默认值；设置为单排或双排时统一覆盖拓扑。 |
| `NeutralBranchRules` | N 排独立的额定电流到分支排规格和默认单双排规则表；当前为 `250A/4x20/单排`、`400A/4x30/单排`、`630A/4x40/单排`。 |
| `NeutralBranchArrangementOverride` | 项目级 N 排拓扑覆盖。为 `null` 时使用每台漏保的 N 排规则。 |
| `NeutralCollectorWidthMm / NeutralCollectorThicknessMm` | N 汇流排规格。 |
| `NeutralBranchWidthMm / NeutralBranchThicknessMm` | N 分支排规格。 |
| `CollectorPhaseSpacingMm` | 汇流排相间距。 |
| `CollectorTopClearanceYMm` | 汇流排相对漏保上方净距。 |
| `CollectorOffsetFromLoubaoInZMm` | 汇流排相对漏保的 Z 偏移。 |
| `CollectorANegativeXExtendMm / CollectorBNegativeXExtendMm / CollectorCNegativeXExtendMm` | A、B、C 汇流排各自的 X- 侧外伸；只延长对应汇流排的 X- 端。 |
| `NeutralCollectorNegativeXExtendMm` | N 汇流排独立的 X- 侧外伸。 |
| `MainLeadOutYMm` | 转接排从刀熔端初始 Y 方向引出距离。 |
| `DoubleClampOuterInitialRiseMm` | 双排夹接 Z-方向外侧上排从漏保端先沿 Y+ 引出的距离，默认 `50mm`。 |
| `DoubleClampOuterDiagonalMinimumLengthMm` | 外侧上排斜向避让段的最小实际长度，默认 `50mm`。 |
| `SheetMetalBendRadiusMm` | 当前默认折弯半径。后续应迁到 `BendRadiusRules`。 |
| `SheetMetalKFactor` | 钣金 K 因子。 |
| `MainCollectorFrontClearanceMm` | 转接排靠近汇流排前的 Z 向避让距离。 |
| `MainFeedProfile / CollectorProfile / BranchProfile` | 把宽厚参数转成 `BusbarProfile`。 |
| `GetSheetMetalWidthSide(kind)` | 根据铜排种类返回钣金宽度方向模式。 |

### `Domain/BusbarModels.cs`

职责：铜排业务主对象。

| 类/函数 | 作用 |
| --- | --- |
| `SheetMetalOptions` | 单根铜排的钣金制造快照：折弯半径、K 因子、宽度模式、宽度生成侧和加厚方向。 |
| `BusbarRoutingOptions` | 路径规则选项：轴顺序、厚度过渡策略和分支排路径模式。 |
| `ConnectionPort` | 可连接铜排的工程端口，包含孔中心、贴合面、引出方向、端部裕度、孔径。 |
| `ConnectionPort.ToString()` | 调试日志用，输出端口详细信息。 |
| `Busbar` | 一根铜排，包括起终端口、规格、分支腿角色、逻辑中心线、钣金草图线、打孔端口。 |
| `CollectorLayout` | 某相汇流排的位置、长度、Tap 端口集合。 |
| `BusbarDesignPlan` | 设备选型、汇流排布局、逻辑路径和搭接孔结果。 |
| `BusbarManufacturingPlan` | 钣金参数、钣金草图线和螺栓选型均已补齐的制造结果。 |

### `Domain/BusbarHoleModels.cs`

职责：描述铜排搭接孔型规则，不包含具体 CAD API。

| 类/枚举 | 作用 |
| --- | --- |
| `BusbarOverlapHolePattern` | 搭接孔型枚举：单孔、直双孔、斜双孔。 |
| `BusbarOverlapHoleRule` | 单个宽度组合对应的孔位规则：孔型、孔径、偏移量、来源编码。 |
| `BusbarOverlapHoleRule.Clone()` | 克隆规则对象，避免调用侧修改静态矩阵中的原始规则。 |

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
| `RatedCurrentA` | 从组件名称按已配置规则解析出的漏保最大电流。 |
| `BranchProfile` | 该漏保专属的 ABC 分支排宽度与厚度。 |
| `BranchArrangement` | 该漏保规则给出的默认单排或双排拓扑。 |
| `NeutralBranchProfile` | 该漏保专属的 N 分支排宽度与厚度，来自独立 N 排规则表。 |
| `NeutralBranchArrangement` | 该漏保 N 排规则给出的默认单排或双排拓扑。 |

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
| `CreateDefault(topologyKind, settings)` | 创建当前默认规则，并从项目配置读取折弯半径和 K 因子。 |
| `DefaultEndMarginMm` | 默认端部裕度。 |
| `BendRadiusMm / KFactor` | 已解析到本轮计划的钣金参数，随后被复制到每根铜排快照。 |
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

### `Rules/BusbarOverlapRuleMatrix.cs`

职责：固化当前 `铜排搭接逻辑.xlsx` 中 30/40/50/60 宽度组合的 4x4 搭接孔矩阵。

| 函数 | 作用 |
| --- | --- |
| `TryResolve(firstWidthMm, secondWidthMm, out rule)` | 按两根铜排宽度查找孔型规则；当前只覆盖 30、40、50、60。 |
| `CreateRules()` | 创建静态规则矩阵。 |
| `Single(...) / StraightDouble(...) / DiagonalDouble(...)` | 创建三类孔型规则。 |
| `NormalizeWidth(widthMm)` | 将宽度四舍五入为整数规格，用于矩阵索引。 |

## 5. 规划层

### `Planning/BusbarPlanBuilder.cs`

职责：从标准化的 `AssemblySnapshot` 生成 `BusbarDesignPlan`，不生成钣金草图线和螺栓选型。

最适合修改的场景：

- 改变生成哪些铜排。
- 改变 A/B/C/N 的组织关系。
- 接入新的孔位规划、搭接规则、标准件规则。

| 函数 | 作用 |
| --- | --- |
| `BuildDesignPlan(...)` | 主入口：读取标准化设备输入和配置快照，为 ABC/N 生成布局、逻辑路径和搭接孔。 |
| `AddNeutralCollectorAndBranches(...)` | 处理 N 相汇流排和 N 分支排。 |
| `CreateNeutralLoubaoInputs(...)` | 收集每个漏保的 `N_IN`，并检查是否部分缺失。 |
| `CreateCollectorBusbar(...)` | 根据 `CollectorLayout` 创建一根汇流排对象。 |
| `CreateCollectorEndPort(...)` | 创建汇流排起止端口，不参与打孔。 |
| `ApplyMainFeedCollectorTapRules(...)` | 给转接排与汇流排搭接 Tap 设置裕度和孔径。 |
| `ApplyBranchDevicePortRules(...)` | 给分支排设备侧端口设置孔径。 |
| `ApplyBranchCollectorTapRules(...)` | 给分支排汇流排侧 Tap 设置孔径。 |
| `ApplyCollectorTapHoleRules(...)` | 给汇流排 Tap 设置孔径。 |
| `ApplyCollectorOverlapHoleRules(...)` | 在生成中心 Tap 后，根据搭接孔矩阵把中心点展开成实际单孔/双孔孔位，并同步写入连接铜排和汇流排。 |
| `ReplaceBusbarMountingPort(...)` | 将连接铜排上的中心 Tap 孔替换为搭接规则计算出的实际孔位。 |
| `ReplaceCollectorTapPort(...)` | 将汇流排布局中的中心 Tap 替换为搭接规则计算出的实际孔位，供汇流排本体打孔。 |
| `CreateBusbar(...)` | 创建转接排或分支排，生成逻辑路径和打孔端口。 |
| `AddMountingPortIfNeeded(...)` | 如果端口孔径有效，就加入铜排打孔列表。 |
| `CloneConnectionPort(...)` | 克隆端口，避免后续修改原始端口影响打孔数据。 |
| `CreateLoubaoGroups(...)` | 根据标准化漏保输入和额定电流规则创建 ABC/N 规格与单双排结果。 |
| `ResolveBranchRule(...)` | 根据额定电流取得分支排规格和默认单双排规则。 |

### `Planning/BusbarPlans.cs` 与 `BusbarManufacturingPlanner.cs`

职责：明确设计结果和制造结果的边界。制造规划器是 `SheetMetalOptions`、`SheetMetalSketchLine` 和紧固件选型的唯一生成步骤。

| 类/函数 | 作用 |
| --- | --- |
| `BusbarDesignPlan` | 保存设备选型、汇流排布局、铜排逻辑路径和搭接孔。 |
| `BusbarManufacturingPlan` | 包装设计计划，并保存制造阶段生成的紧固件连接计划。 |
| `BusbarManufacturingPlanner.Build(...)` | 为每根铜排建立钣金参数快照、生成钣金草图线，再调用 `FastenerPlanBuilder`。 |

### `Planning/CollectorLayoutPlanner.cs`

职责：计算汇流排的位置和 Tap 点。

| 函数/类 | 作用 |
| --- | --- |
| `CreateLayout(...)` | 根据相序、刀熔端口、漏保端口计算汇流排中心、Y/Z 位置和 X 范围。 |
| `CreateTap(...)` | 在汇流排指定 X 位置创建 Tap 端口。 |
| `CreateConnectionExtents(...)` | 根据每台漏保对应的连接铜排宽度计算汇流排长度覆盖范围。 |
| `CollectorLengthRange` | 汇流排 X 起止范围。 |
| `CollectorConnectionExtent` | 一个连接点在 X 方向的占用范围。 |
| `BusbarLengthController.Calculate(...)` | 根据所有连接范围计算汇流排 StartX/EndX。 |

### `Planning/BusbarPlanBuilder.cs` 中的双排夹接

| 函数 | 作用 |
| --- | --- |
| `AddPlannedBranchBusbars(...)` | 根据 `BranchArrangement` 生成传统单分支，或生成下搭接 `_Lower` 与上搭接 `_Upper` 两根分支排。 |
| 下搭接分支 | 下排目标为汇流排下表面；当前在 `BusbarPlanBuilder` 直接使用已推导的终点高度公式，并保留 `BranchLegRole.Single`，避免 `ContactTopologyResolver` 再次加入同一份厚度补偿。 |
| `BranchLegRole.Upper` | 上排目标为汇流排上表面，路径在漏保端先做 Z 向错层，并使用 Z-方向外侧避让路线。 |
### `Planning/BusbarRoutePlanner.cs`

职责：生成孔中心意义上的逻辑路径。

| 函数/类 | 作用 |
| --- | --- |
| `CreateRoute(...)` | 根据铜排类型和路径模式选择转接排、简单分支或双排外侧避让路径。 |
| `CreateMainFeedRoute(...)` | 生成刀熔到汇流排的转接排折线路径。 |
| `CreateSimpleRoute(...)` | 生成单排或双排下搭接的简单折线路径。 |
| `CreateDoubleClampOuterAvoidanceRoute(...)` | 生成双排 Z-方向外侧上排路径：Y+ 首段、Y+/Z- 斜向避让、Y+ 上升、Z+ 回到原搭接点；并校验最小斜段长度和可用高度。 |
| `CalculateMainFeedRouteDecision(...)` | 计算转接排 Y 引出和 Z 避让策略。 |
| `CalculateMainFeedLeadOutY(...)` | 根据端口引出方向计算初始 Y 引出距离。 |
| `CalculateMainFeedApproachZ(...)` | 计算转接排进入汇流排前的 Z 方向避让位置。 |
| `CalculateMainFeedApproachOffsetZ(...)` | 当前使用汇流排宽度、转接排宽度、前方净距计算 Z 避让量。 |
| `MainFeedRouteDecision` | 记录转接排路径决策结果和说明文字。 |

### `Planning/BusbarDirectionResolver.cs`

职责：从铜排中心线和贴合面中推导局部长度方向，供直双孔沿窄排方向偏移。

| 函数 | 作用 |
| --- | --- |
| `ResolveLengthDirectionAtPort(busbar, port, fallbackAxis)` | 根据端口所在位置，从铜排中心线推导端口处长度方向，并投影到贴合面内。 |
| `ResolveAxisDirection(axis, face)` | 将指定坐标轴投影到贴合面内，用作兜底方向。 |
| `GetPlaneFirstAxis(face)` | 返回贴合面内第一个局部轴，斜双孔使用。 |
| `GetPlaneSecondAxis(face)` | 返回贴合面内第二个局部轴，斜双孔使用。 |
| `FindNearestSegmentDirection(...)` | 当端口不在起终点时，找到离端口最近的中心线段方向。 |

### `Planning/BusbarOverlapHolePlanner.cs`

职责：在“搭接中心点”和“实际打孔”之间计算搭接孔位。

| 函数 | 作用 |
| --- | --- |
| `CreateCollectorOverlapPorts(...)` | 主入口：根据连接铜排宽度、汇流排宽度、搭接面和方向，返回实际孔位列表。 |
| `CreateSingle(...)` | 生成单孔，中心点不偏移，仅按规则设置孔径。 |
| `CreateStraightDouble(...)` | 生成直双孔，沿窄排长度方向正负偏移，偏移量为宽排宽度的四分之一。 |
| `CreateDiagonalDouble(...)` | 生成斜双孔，按矩阵规则在贴合面内两个局部轴上同时正负偏移。 |

`CreateCollectorOverlapPorts(...)` 在矩阵未覆盖时直接抛出错误，不生成中心孔兜底。当前 `20mm` 分支排组合尚未获批，因此包含 250A 漏保的规划会被阻止。

### `Planning/BusbarPreflightValidator.cs`

职责：在进入 SolidWorks 建模前，验证配置和已经生成的 `BusbarManufacturingPlan` 是否满足独立的工程契约。它只读取规划结果，不创建零件、不删除装配组件，也不重新调用路径规划器。

| 类/函数 | 作用 |
| --- | --- |
| `BusbarPreflightReport` | 保存 `INFO`、`WARNING`、`ERROR` 消息，统计结果并输出 PowerShell 控制台报告。未来 UI 直接消费该对象。 |
| `ValidateConfiguration(settings)` | 验证规格、ABC/N 电流选型表、单双排枚举和汇流排基础参数。 |
| `ValidatePlan(plan, settings, phaseNames)` | 验证实际选型、汇流排范围、分支数量、孔位对应关系和双排几何契约。 |
| `ValidateDoubleClampRoutes(...)` | 验证上下排终点高度、上排 Z 错层和外侧避让路径的首段、斜段、回接关系。 |
| `CreatePlanningFailure(exception)` | 将扫描或规划阶段的异常转换为统一的预检错误报告。 |

### `Domain/FastenerModels.cs` 与 `Planning/FastenerPlanBuilder.cs`

职责：为已知的铜排-汇流排搭接孔生成标准件选型结果，并将结果交给预检报告。当前标准件数值在 `Program.cs` 中与数据层工作簿保持同步，后续可替换为 Excel Repository。

| 类/函数 | 作用 |
| --- | --- |
| `FastenerSpec` | 描述 M 规格、通孔直径、标准公称长度、弹垫压平厚度、平垫尺寸、螺母高度和螺栓头尺寸。 |
| `FastenerJointPlan` | 保存一个实际搭接孔对应的螺栓选择、夹紧厚度、所需长度和实际露出量。 |
| `BuildCollectorJoints(...)` | 将主排/分支排的汇流排搭接孔按相位和 X/Z 孔位分组；双排上下排共享同一组贯穿螺栓。 |
| `BuildSelection(...)` | 按孔径匹配 M 规格，计算最小长度并选择最短可用标准长度。 |

### `Reporting/ProductionReportExporter.cs`

职责：把完整的 `BusbarManufacturingPlan` 转换成生产和加工所需的 Excel 报表，不依赖 SolidWorks 实体。它生成漏保选型、铜排汇总、逐根铜排下料明细、钻孔清单、标准件汇总与螺栓明细。

| 类/函数 | 作用 |
| --- | --- |
| `ProductionReportExporter.Export(...)` | 从规划对象计算各表数据，并输出 `.xlsx` 文件。 |
| `CalculateRouteMetrics(...)` | 以 `SheetMetalSketchLine` 计算含端部余量的路径长度、折弯数和按 R/K/t 估算的展开长度。 |
| `BuildCopperSummarySheet(...)` | 按铜排规格汇总数量、总长度、折弯数与孔数。 |
| `BuildLoubaoSelectionSheet(...)` | 输出每台漏保的额定电流、ABC/N 铜排规格和单/双排拓扑。 |
| `BuildHoleSheet(...)` | 输出每根实体铜排上的孔径、用途和装配体坐标。 |
| `BuildFastenerSummarySheet(...)` | 从搭接螺栓计划推导螺栓、两片平垫、弹垫和螺母的数量。 |
| `ProductionReportWorkbookWriter` | 不依赖 Excel 或第三方 NuGet 包，直接写出包含六个工作表的 `.xlsx` 文件。 |

命令行参数 `--export-report` 会在预检通过后只导出报表，不修改装配体；完整建模和实体校验成功后也会自动导出报表。

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
| `SolidWorksGenerationRunner(...)` | 接收本轮 `BusbarSettings` 和 `GenerationOptions`，创建专属零件建模器。 |
| `Run()` | 校验配置，连接 SW，调用扫描器，建立计划并预检，再分派预览、报表、既有实体校验或完整生成。正常生成不会预先删除旧件。 |
| `VerifyExistingGeometry(...)` | 调用只读实体校验器，并在失败时设置非零进程退出码。 |
| `ExportProductionReport(...)` | 将当前完整计划导出到装配体旁的 `Reports` 目录。 |

### `SolidWorks/BusbarGeometryVerifier.cs`

职责：读取已经插入装配的 `Busbar_*` 零件，验证实际实体而非规划数据。此模块只读，不会修改 SolidWorks 模型。

| 函数/类 | 作用 |
| --- | --- |
| `Verify(...)` | 主入口：将计划铜排与装配组件匹配，输出实体校验报告。 |
| `FindGeneratedComponents(...)` | 查找装配中的 `Busbar_*` 组件，并兼容 SolidWorks 实例后缀与保存文件名后缀。 |
| `GetAssemblyBounds(...)` | 用 `Component2.GetBox(false, false)` 获取装配坐标系下的真实实体边界框。 |
| `ValidateProfileEnvelope(...)` | 检查主/分支排宽度、汇流排宽度和厚度是否符合计划规格。 |
| `ValidateHoleFeatures(...)` | 检查每个预期 `HoleCut_P*` 特征、声明切除深度和实际圆柱孔面。 |
| `ValidatePhysicalThroughHole(...)` | 按计划孔中心、半径和轴向匹配实体圆柱面，并验证物理跨度覆盖完整铜排厚度。 |
| `ValidateDoubleClampSurfaceContacts(...)` | 根据实体边界框验证上下分支排分别贴合汇流排下、上表面。 |

### `SolidWorks/SolidWorksSession.cs`

职责：SolidWorks 会话和文档管理。

| 函数 | 作用 |
| --- | --- |
| `GetActiveOrOpenAssembly(swApp)` | 获取当前激活装配体；如果当前不是装配体，尝试切换到已打开装配体。 |
| `GetOrStartSolidWorks()` | 连接正在运行的 SolidWorks；没有则启动一个新实例。 |
| `ActivateDocument(swApp, model)` | 激活指定文档。 |

### `SolidWorks/AssemblyScanner.cs`

职责：只扫描装配体与组件中的命名参考点，并将坐标转换到装配体坐标系。删除组件不属于扫描职责。

| 函数 | 作用 |
| --- | --- |
| `AssemblyReferencePointScanner(verbose)` | 创建一次扫描操作所需的日志配置。 |
| `Scan(...)` | 扫描装配体和组件参考点；每次扫描只创建一个 `MathUtility`，并输出模型扫描数、缓存命中数和耗时。 |
| `ScanComponentReferencePoints(...)` | 跳过程序生成的 `Busbar_*` 组件，并按“零件文件路径 + 引用配置”缓存设备局部 `RefPoint`；同一零件的多个实例只扫描一次 Feature 树。 |
| `ReadReferencePointTemplates(...)` | 只读取 `TypeName2 = RefPoint` 的 Feature，提取局部坐标模板并隔离单点读取异常。 |
| `AddFoundPoints(...)` | 对每个组件实例套用装配变换，写入装配体坐标下的 `FoundPoint`。 |
| `TransformPoint(...)` | 将组件局部点转换到装配体坐标，并释放本次转换创建的临时数学对象。 |
| `SolidWorksCom.Release(...)` | 释放扫描过程中创建且不再保留的 COM 临时对象；不释放仍由后续流程使用的装配体对象。 |

### `SolidWorks/BusbarBatchBuilder.cs`

职责：选择生成顺序并批量创建铜排零件。

| 函数 | 作用 |
| --- | --- |
| `SelectBusbarsForSheetMetalBatch(plan)` | 选择实体生成顺序：主排、汇流排、分支排、N 排。 |
| `FindRequiredBusbar(...)` | 查找必须存在的铜排，不存在时报错。 |
| `FindBusbars(...)` | 按相位和类型查找铜排。 |
| `FindOptionalBusbars(...)` | 查找可选铜排，例如 N 相。 |
| `CreateBusbarSheetMetalParts(...)` | 逐根生成并暂存插入，全部完成后执行实体校验；失败时清理本轮暂存项，通过后才交给组件管理器替换旧件。 |
| `CreateBusbarSheetMetalPart(...)` | 生成单根铜排：建新零件、建钣金、打孔、保存，在零件文档仍打开时立即插入装配体，再在 `finally` 中关闭临时文档。这个顺序避免 `AddComponent5` 对刚关闭文件返回 `null`。 |
| `CreateBusbarSheetMetalFeature(...)` | 创建单根铜排的钣金主体 Feature。 |

### `SolidWorks/GeneratedComponentManager.cs`

职责：集中管理生成组件的删除操作，使扫描器和建模器不再各自维护选择/删除逻辑。

| 函数 | 作用 |
| --- | --- |
| `DeleteExistingBusbars(...)` | 完整生成时删除旧 `Busbar_*` 组件；局部 `--only` 生成时只删除同名旧组件，同时排除已经通过暂存校验的新组件，并验证是否仍有旧件残留。 |
| `DeleteSelected(...)` | 在本轮生成失败时删除已插入的暂存组件。 |

### `SolidWorks/CadGeometryUtilities.cs`

职责：SolidWorks 草图和几何辅助。

| 函数 | 作用 |
| --- | --- |
| `CreateLineOrThrow(...)` | 创建草图线，失败时报错。 |
| `NewPartDocument(swApp)` | 新建零件文档。 |
| `GetOpenProfilePlane(...)` | 根据铜排中心线判断开放轮廓草图应在哪个基准平面。 |
| `AllSameCoordinate(...)` | 判断一组点是否在某个坐标轴上保持常量。 |
| `GetCoordinate(...)` | 按轴读取点坐标。 |
| `CreateOffsetPlane(...)` | 基于指定默认基准面创建偏移平面；创建或选择失败时直接报错，不回退到任意已有参考面。 |
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
- 多孔逻辑不在这里判断；这里只消费 `Planning` 层已经展开好的 `MountingPorts`。

| 函数 | 作用 |
| --- | --- |
| `CreateBusbarMountingHoles(...)` | 遍历 `MountingPorts` 打孔；如果为空则回退起终端口。 |
| `CreateBusbarMountingHole(...)` | 创建单个孔草图并切除。 |
| `GetCollectorHoleSketchPlane(...)` | 让汇流排所有孔型共用实际上表面作为草图面。 |
| `GetBranchCollectorHoleSketchPlane(...)` | 从分支排实际钣金路径推导汇流排搭接端的实体表面。 |
| `GetHoleSketchPlane(port)` | 对其他孔根据端口贴合面选择草图平面。 |
| `CreateDirectedBlindCutFromActiveSketch(...)` | 优先从当前激活草图执行唯一方向、深度等于材料厚度的盲切。 |
| `CreateDirectedBlindCutFromSketch(...)` | active sketch 入口失败时，改用已创建草图 Feature 执行相同参数的切除。 |
| `ShouldReverseHoleCutDirection(port)` | 根据贴合面判断切除方向是否反向。 |
| `SelectSketchForCut(...)` | 选择切除草图。 |

两种入口只解决 SolidWorks 对 active sketch/feature selection 的差异，不允许改变切除方向、normal-cut 或 feature scope 来试错。

### `SolidWorks/PartPersistenceService.cs`

职责：保存零件、插入装配、关闭临时文档。

| 函数 | 作用 |
| --- | --- |
| `SaveBusbarSheetMetalPart(...)` | 按铜排名称、规格和唯一时间戳保存钣金零件。 |
| `SaveGeneratedPart(...)` | 以唯一文件名保存通用生成零件，预览等流程可用。 |
| `BuildUniquePartPath(...)` | 使用毫秒时间戳和递增后缀避免同名文件被覆盖。 |
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
| 搭接孔型、孔数、孔径、偏移 | `Rules/BusbarOverlapRuleMatrix.cs`、`Planning/BusbarOverlapHolePlanner.cs` |
| 直双孔方向判断 | `Planning/BusbarDirectionResolver.cs` |
| 刀熔/漏保识别规则 | `App/AssemblySnapshotFactory.cs` |
| 汇流排位置和长度 | `Planning/CollectorLayoutPlanner.cs` |
| 生成前规则预检与控制台报告 | `Planning/BusbarPreflightValidator.cs` |
| 生成后实体、孔深度、双排表面贴合校验 | `SolidWorks/BusbarGeometryVerifier.cs` |
| 搭接螺栓规格与长度选型 | `Domain/FastenerModels.cs`、`Planning/FastenerPlanBuilder.cs` |
| 转接排/分支排路径 | `Planning/BusbarRoutePlanner.cs` |
| 同侧/异侧、厚度补偿 | `Planning/ContactTopologyResolver.cs` |
| 打孔方向和孔草图 | `SolidWorks/MountingHoleBuilder.cs` |
| 钣金 MidPlane 参数 | `SolidWorks/SheetMetalFeatureBuilder.cs` |
| 读取 SolidWorks 参考点 | `SolidWorks/AssemblyScanner.cs` |
| 保存和插回装配体 | `SolidWorks/PartPersistenceService.cs` |
| 接 Excel 数据层 | 后续新增 `Data/ExcelDataRepository.cs` |
| 接 UI | 后续新增 `GenerationRequest` / `GenerationOptions`，UI 填请求对象 |
| 支持 UG/NXOpen | 保持 `Domain/Rules/Planning` 不依赖 CAD；等第二后端真实接入时，从 `BusbarManufacturingPlan` 提取两个后端共同需要的最小生成契约 |
