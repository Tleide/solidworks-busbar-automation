# 铜排自动建模架构设计

本文档记录新一版铜排自动建模框架的设计原则和 V0 手动规则。目标是先建立可扩展的建模逻辑，再逐步迁移旧代码，避免继续在旧 Demo 上叠加局部偏移补丁。

## 1. 设计目标

- 所有铜排统一使用 `2D 草图中心线 + Sheet Metal Base Flange` 建模。
- 铜排宽度方向统一采用两侧对称，也就是草图线位于铜排宽度中心。
- 转接排、汇流排、分支排共享同一套路线规划和钣金生成流程。
- 取消针对某一种配电箱结构的硬编码补偿。
- 通过端口规则描述设备连接面和汇流排搭接面，先手动配置，后续再自动识别。

## 2. 坐标系和面定义

坐标系按正视柜门方向定义。

| 方向 | 含义 |
| --- | --- |
| `X+` | 柜体向左 |
| `Y+` | 柜体向上 |
| `Z+` | 向柜体内部 |

面定义如下。

| 面 | 坐标方向 |
| --- | --- |
| `Front` | `Z-` |
| `Back` | `Z+` |
| `Upper` | `Y+` |
| `Lower` | `Y-` |
| `Left` | `X+` |
| `Right` | `X-` |

## 3. 全局默认参数

| 参数 | 默认值 |
| --- | --- |
| 端部预留 `EndMarginMm` | `15` |
| 折弯半径 `BendRadiusMm` | `5` |
| K 因子 `KFactor` | `0.47` |
| 宽度生成方式 | `MidPlane` |
| 转接排刀熔端预留 `MainFeedStartEndMarginMm` | `30` |
| 转接排汇流排端预留 `MainFeedCollectorEndMarginRatio` | `CollectorWidth * 0.5` |
| 转接排刀熔端孔径 `MainFeedStartHoleDiameterMm` | `13` |
| 转接排汇流排端孔径 `MainFeedCollectorHoleDiameterMm` | `13` |
| N 汇流排规格 `NeutralCollectorWidthMm x NeutralCollectorThicknessMm` | `60 x 6` |
| N 分支排规格 `NeutralBranchWidthMm x NeutralBranchThicknessMm` | `40 x 4` |

说明：

- 连接点按“连接面上的孔中心”理解。
- 端部预留从孔中心继续向铜排端部外伸。
- 宽度方向由钣金两侧对称保证，不再通过路径点偏移修正。
- 厚度方向由端口面、搭接面和钣金正反方向规则决定。
- N 排规格当前作为独立配置项放在 `Program.cs` 顶部 `Settings` 区，不并入 ABC 主排规格，后续可替换为标准校核或自动选型函数。

SolidWorks API 注意事项：

- 开放轮廓基体法兰的两侧对称必须使用 `swEndCondMidPlane`。
- 不能用 `Dist1=Width/2 + Dist2=Width/2 + swEndCondBlind` 模拟 UI 的 `Mid Plane`，否则 SolidWorks 会按给定深度处理，草图线不一定是宽度中心线。
- 当前正确调用约定为：`Dist1 = Width`，`EndCondition1 = swEndCondMidPlane`，`DirToUse = 1`。

## 4. 核心抽象

### 4.1 ConnectionPort

`ConnectionPort` 表示一个可以接铜排的端口。

建议字段：

```csharp
class ConnectionPort
{
    string Name;
    string ComponentName;
    Point3 HoleCenter;
    ContactFace RequiredFace;
    Vector3 PreferredLeadDirection;
    double EndMarginMm;
    HoleSpec Hole;
}
```

关键语义：

- `HoleCenter` 是连接面上的孔中心。
- `RequiredFace` 表示铜排需要贴合设备或汇流排的哪一面。
- `PreferredLeadDirection` 表示铜排离开端口时优先朝哪个方向走。
- `EndMarginMm` 表示孔中心到铜排端部的预留长度。

### 4.2 Busbar

`Busbar` 表示一根待生成的铜排。

建议字段：

```csharp
class Busbar
{
    string Name;
    BusbarKind Kind;
    BusbarProfile Profile;
    ConnectionPort StartPort;
    ConnectionPort EndPort;
    BusbarRoutingOptions Routing;
    SheetMetalOptions SheetMetal;
    List<Point3> LogicalCenterline;
    List<Point3> SheetMetalSketchLine;
}
```

关键语义：

- `LogicalCenterline` 是业务几何中心线，表示孔中心、搭接点和路径逻辑。
- `SheetMetalSketchLine` 是传给 SolidWorks 的 2D 草图线。
- 正常情况下二者一致；只有厚度面拓扑需要转换时，才由拓扑解析器生成差异。

### 4.3 Collector

汇流排本身也是一种 `Busbar`，但它还负责提供多个 `TapPort`。

建议字段：

```csharp
class CollectorLayout
{
    string Phase;
    AxisDirection Direction;
    Point3 Center;
    double Length;
    List<ConnectionPort> TapPorts;
}
```

V0 规则：

- 汇流排方向为 `X`。
- 汇流排位置可以先手动指定。
- 汇流排长度覆盖所有连接点，并在末端继续外伸。
- 当前长度控制由 `BusbarLengthController` 根据实际连接 Tap 动态计算：每个连接点先按对应铜排宽度的一半转换为 X 向占用范围；`X+` 侧截止到最外侧连接铜排的外边界，不额外外伸；`X-` 侧从最外侧连接铜排的外边界继续外伸 `50mm`，该值应保留为配置参数。
- 汇流排允许搭接面为 `Upper` 和 `Lower`。

## 5. 手动规则 V0

### 5.1 结构类型

V0 先支持手动选择结构类型。

```csharp
enum CabinetTopologyKind
{
    TypicalDesign,
    SouthernGrid
}
```

规则：

| 结构 | 刀熔和漏保相对汇流排位置 | 刀熔 OUT 默认贴合面 |
| --- | --- | --- |
| `TypicalDesign` | 刀熔、漏保在汇流排 `Z` 方向两侧 | `Back` (`Z+`) |
| `SouthernGrid` | 刀熔、漏保在汇流排同一侧 | `Front` (`Z-`) |

### 5.2 设备端口默认规则

| 端口 | 点名 | 默认贴合面 | 默认引出方向 | 端部预留 |
| --- | --- | --- | --- | --- |
| 刀熔 OUT | `A_OUT/B_OUT/C_OUT` | 由结构类型决定 | `Y-` 或手动指定 | `15mm` |
| 漏保 IN | `A_IN/B_IN/C_IN` | `Front` (`Z-`) | `Y+` 或手动指定 | `15mm` |

说明：

- 刀熔 OUT 在典设配电箱中默认贴 `Z+`，即 `Back`。
- 刀熔 OUT 在南网形结构中默认贴 `Z-`，即 `Front`。
- 漏保 IN 默认贴 `Z-`，即 `Front`。
- 连接点均按连接面孔中心处理。

### 5.3 汇流排搭接默认规则

| 连接类型 | 汇流排默认搭接面 |
| --- | --- |
| 转接排 `MainFeed -> Collector` | `Upper` |
| 分支排 `Branch -> Collector` | `Upper` |

后续可配置为 `Lower`。

## 6. 路径规划规则

所有设备到汇流排的铜排，都抽象为：

```text
StartPort -> RoutePlanner -> CollectorTapPort
```

V0 路径规则：

- 汇流排沿 `X` 方向。
- 每个设备端口按自身 `X` 值接到对应相汇流排的 Tap。
- 转接排和分支排路径在固定 `X` 截面内规划。
- 路径只允许两个坐标轴变化。
- 分支排默认优先顺序为先 `Y` 后 `Z`。
- 典设箱转接排从刀熔下端引出时，采用明确的出线折线：先 `Y-`，再 `Z-` 离开刀熔区域，再 `Y+` 到汇流排搭接高度，最后 `Z-` 到汇流排。

分支排示意：

```text
P0 = 设备端孔中心
P1 = (P0.X, Tap.Y, P0.Z)
P2 = (P0.X, Tap.Y, Tap.Z)
```

转接排示意：

```text
P0 = 刀熔 OUT 孔中心
P1 = (P0.X, P0.Y - LeadOut, P0.Z)
P2 = (P0.X, P0.Y - LeadOut, ApproachZ)
P3 = (P0.X, Tap.Y, ApproachZ)
P4 = 汇流排 Tap 孔中心
```

其中 `ApproachZ` 由汇流排中心、汇流排宽度、转接排宽度和前侧间隙计算，目标是先在汇流排外侧完成 `Y` 向长距离移动，最后再沿 `Z` 进入汇流排搭接位置。

当前实现中，转接排的两个关键中间位置由独立函数计算，先保留简单规则，后续可替换为自动优化规则。

| 决策点 | 当前简单规则 | 后续扩展方向 |
| --- | --- | --- |
| `LeadOutY`，即从刀熔 OUT 先向 `Y-` 走多远 | 使用 `Settings.MainLeadOutY`，方向由端口 `PreferredLeadDirection` 决定 | 根据刀熔外形包络、端子区高度、最小折弯距离和安全间隙自动计算 |
| `ApproachZ`，即先 `Z-` 到哪个过渡面 | 汇流排中心 `Z` 外侧偏移 `CollectorWidth/2 + MainFeedWidth/2 + FrontClearance` | 根据汇流排搭接面、铜排宽度、器件深度、避让空间和最短路径自动计算 |

后续可扩展：

- 自动选择先 `Y` 还是先 `Z`。
- 自动避让器件。
- 自动优化汇流排位置。

## 7. 同侧和异侧拓扑

Mid Plane 只解决铜排宽度方向对称，厚度方向仍需要拓扑判断。

本项目中，同侧/异侧是底层工程拓扑关系；补偿只是当前钣金建模阶段对该拓扑关系的一种处理结果。文档和代码都应保持以下分层：

```text
工程场景
-> 同侧/异侧拓扑判定
-> 补偿策略
-> 最终钣金建模
```

不要把逻辑写成：

```text
工程场景
-> 直接补偿
-> 最终钣金建模
```

这样后续如果出现新的铜排搭接方式，或者同侧/异侧从手动规则升级为自动几何判断时，只需要替换拓扑判定接口，不需要推翻路线规划和建模层。

### 7.1 拓扑关系定义

同侧/异侧描述的是铜排两端连接关系在厚度方向上的拓扑位置，而不是简单比较两个端口的 `RequiredFace` 名称。

V0 阶段先使用工程场景规则表进行手动判定；后续可升级为根据设备包络、连接面法向、汇流排搭接面、端点路径切向和钣金厚度方向自动判断。

建议接口：

```csharp
enum ContactTopologyKind
{
    SameSide,
    DifferentSide
}

interface IContactTopologyRule
{
    ContactTopologyKind Resolve(BusbarV2 busbar, CabinetTopologyKind cabinetTopology);
}
```

### 7.2 当前工程场景规则表

当前先按典型工程场景定义转接排与汇流排的同侧/异侧关系。

| 工程场景 | 汇流排搭接面 | 拓扑关系 | 当前建模处理 |
| --- | --- | --- | --- |
| 刀熔与漏保分别位于汇流排两侧 | `Upper` | 异侧 | 需要补偿 |
| 刀熔与漏保分别位于汇流排两侧 | `Lower` | 同侧 | 不需要补偿 |
| 刀熔与漏保位于汇流排同侧 | `Upper` | 同侧 | 不需要补偿 |
| 刀熔与漏保位于汇流排同侧 | `Lower` | 异侧 | 需要补偿 |

说明：

- `TypicalDesign` 对应“刀熔与漏保分别位于汇流排两侧”。
- `SouthernGrid` 对应“刀熔与漏保位于汇流排同侧”。
- 上表先用于转接排 `MainFeed -> Collector`。
- 分支排的拓扑关系也应通过同一个接口判断，不能因为当前生成效果正确就跳过判定。

### 7.3 拓扑关系到补偿策略

拓扑判定完成后，再决定是否补偿。

如果同侧：

```text
LogicalCenterline == SheetMetalSketchLine
不做厚度转换
```

如果异侧：

```text
需要把其中一个端口从接触面孔中心转换到钣金草图参考面
转换距离 = 当前铜排 Thickness
转换方向 = 被补偿端附近路径线段在当前 2D 草图平面内的法向方向
```

当前 V0 策略：

- 异侧时默认优先补偿汇流排端。
- 补偿量为当前铜排厚度 `Thickness`。
- 补偿方向不能写死为某个全局轴，应由被补偿端附近路径段推导。

### 7.4 补偿方向的统一解释

不要写死“刀熔端补 Z、汇流排端补 Y”。更通用的规则是：

```text
端点厚度转换方向 = 端点附近路径段方向在当前 2D 草图平面内的法向
```

在当前典设箱中：

- 设备端附近路径段通常沿 `Y` 走，所以厚度转换方向表现为 `Z`。
- 汇流排搭接段通常沿 `Z` 走，所以厚度转换方向表现为 `Y`。

这样路径规则变化后，厚度方向也能自动跟随。

### 7.5 异侧时优先补偿端

V0 使用手动策略。

```csharp
enum ThicknessTransitionPolicy
{
    PreferStartPort,
    PreferEndPort,
    Auto
}
```

建议默认：

- 如果设备端空间更敏感，优先补偿汇流排端。
- 如果汇流排端搭接关系更严格，优先补偿设备端。
- V0 可以先使用手动选择，后续根据避让和搭接约束自动选择。

## 8. SolidWorks 建模层

统一建模流程：

```text
创建 2D Sketch
-> 绘制开放中心线
-> InsertSheetMetalBaseFlange2
-> 设置 Thickness / BendRadius / KFactor
-> 宽度两侧对称
-> 创建孔特征
-> 保存零件并插入装配体
```

建议 API 参数语义：

- `Thickness = BusbarProfile.Thickness`
- `BendRadius = 5mm`
- `KFactor = 0.47`
- `Dist1 = Width`
- `Dist2 = 0`
- `EndCondition1 = swEndCondMidPlane`
- `EndCondition2 = swEndCondBlind`

注意：

- SolidWorks API 中的 `Mid Plane` 已通过转接排测试验证，必须使用 `swEndCondMidPlane`。
- 不要使用 `Dist1 = Width / 2` 和 `Dist2 = Width / 2` 加 `Blind` 模拟两侧对称；这不是 UI 中的 `Mid Plane`。
- 钣金厚度正反方向不要通过路径点魔法偏移解决，应由 `TopologyResolver` 和 `SheetMetalDirectionResolver` 处理。

## 9. 推荐模块划分

```text
ReferencePointScanner
    扫描 SolidWorks 命名参考点

ManualPortRuleProvider
    根据组件名、点名和结构类型生成 ConnectionPort

CollectorLayoutPlanner
    手动或自动布置汇流排

BusbarLengthController
    根据所有与汇流排连接的铜排 Tap 位置计算汇流排 X 向起止点

BusbarConnectionPlanner
    建立设备端口到汇流排 TapPort 的连接关系

BusbarRoutePlanner
    生成 LogicalCenterline

ContactTopologyResolver
    只负责判断同侧/异侧拓扑关系，不直接修改路径点

ThicknessTransitionResolver
    根据同侧/异侧拓扑关系和 ThicknessTransitionPolicy 生成 SheetMetalSketchLine

SheetMetalBuilder
    用 SolidWorks API 生成钣金实体

HoleBuilder
    基于 ConnectionPort.HoleCenter 创建安装孔
```

## 10. 阶段性成果记录

记录时间：2026-06-25。

本阶段已经确认并跑通的关键结论如下。

### 10.1 V2 线性规划

转接排 `MainFeed` 的路线规划已经从旧的补偿式逻辑切换为 V2 路由策略。

当前典设箱刀熔下端出线形状为：

```text
刀熔 OUT 孔中心
-> Y- 引出
-> Z- 到汇流排外侧过渡面
-> Y+ 到汇流排搭接高度
-> Z- 到汇流排 Tap 点
```

其中：

- `Y-` 引出距离由 `CalculateMainFeedLeadOutY` 计算，当前简单规则使用 `Settings.MainLeadOutY`。
- `Z` 向过渡面由 `CalculateMainFeedApproachZ` 计算，当前简单规则使用汇流排宽度、转接排宽度和前侧间隙。
- 这两个决策点已经拆成独立函数，后续可以替换为基于设备包络、避让空间和最小折弯距离的自动计算。

### 10.2 钣金 Mid Plane

开放轮廓钣金基体法兰必须使用 SolidWorks API 的真实 `Mid Plane` 条件。

当前确认可用的调用语义：

```text
Dist1 = 铜排宽度
Dist2 = 0
EndCondition1 = swEndCondMidPlane
EndCondition2 = swEndCondBlind
```

不要用以下方式模拟：

```text
Dist1 = 铜排宽度 / 2
Dist2 = 铜排宽度 / 2
EndCondition1 = swEndCondBlind
EndCondition2 = swEndCondBlind
```

该方式在 API 中不会等价于 UI 里的“两侧对称”，会导致草图线不再可靠地作为铜排宽度中心线。

### 10.3 连接孔

转接排两端孔参数已经分开定义：

```text
MainFeedStartEndMarginMm = 30
MainFeedCollectorEndMarginRatio = 0.5
MainFeedStartHoleDiameterMm = 13
MainFeedCollectorHoleDiameterMm = 13
```

当前语义：

- 刀熔端裕度为 `30mm`。
- 汇流排搭接端裕度为 `CollectorWidth * 0.5`，即当前汇流排宽度的一半。
- 刀熔端孔径和汇流排端孔径独立配置，当前均为 `13mm`。

孔中心绘制逻辑：

- 孔中心来自 `ConnectionPort.HoleCenter`，按连接面孔中心理解。
- 根据 `RequiredFace` 选择孔草图平面，例如 `Back/Front` 使用 `Front` 偏移平面，`Upper/Lower` 使用 `Top` 偏移平面。
- 使用 `ModelToSketchTransform` 将装配坐标孔中心转换到草图二维坐标后画圆，不依赖零件原点。

关键 API 经验：

- 退出孔草图后重新选中 Sketch Feature 再调用拉伸切除不稳定，容易返回空特征。
- 更稳定的方式是模拟手动操作流程：进入孔草图，画圆，保持当前活动草图，立刻调用 `FeatureCut4` 创建拉伸切除。
- 当前测试已确认：活动草图直接切除可以成功生成刀熔端孔和汇流排端孔。

### 10.4 调试入口

当前保留的阶段性调试入口：

```powershell
TopToDown.exe --preview-v2-assembly
TopToDown.exe --sheetmetal-v2-first-main
```

其中：

- `--preview-v2-assembly` 只生成 V2 路线预览草图，不生成实体。
- `--sheetmetal-v2-first-main` 只生成第一根 V2 转接排钣金，用于验证钣金、Mid Plane、孔和装配流程。

### 10.5 N 相汇流排和分支排

记录时间：2026-06-27。

本阶段已经确认 N 相汇流排与 N 相分支排的空间规划和实体生成均可用。

已验证结果：

- `--sheetmetal-v2-all` 成功生成并装配 `19` 根 V2 钣金铜排。
- 生成内容包括 `3` 根转接排、`4` 根汇流排和 `12` 根分支排。
- `4` 根汇流排为 `A/B/C/N`，其中 N 汇流排单独使用 `NeutralCollectorProfile`。
- `12` 根分支排为 `A/B/C/N` 各 `3` 根，其中 N 分支排单独使用 `NeutralBranchProfile`。
- N 汇流排和 N 分支排均正常生成孔，孔位跟随前面草图骨架计算出的端口位置。

当前 N 相建模策略：

- 不把 `N` 加入 `PhaseNames = { A, B, C }`。
- `A/B/C` 仍表示刀熔三相进出线主流程。
- `N` 作为中性线扩展，在 ABC 规划完成后由独立入口追加。
- 暂时只生成 `N Collector` 和 `N Branch`，不生成 N 相转接排或进线排。

N 点位规则：

- 每个漏保必须提供 `N_IN` 参考点，程序才生成 N 排。
- 如果没有任何 `N_IN`，则跳过 N 排生成。
- 如果只存在部分 `N_IN`，则抛出明确错误，避免生成不完整的 N 排。

N 排位置规则：

- N 汇流排沿用既有汇流排布局逻辑，不新增独立高度函数。
- 当前配置中 `CollectorTopClearanceYMm = 240`，`CollectorPhaseSpacingMm = 60`。
- A 汇流排高度由漏保 IN 点上方间隙确定，B/C/N 依次按相间距向下排列。
- 这种做法保持了代码简洁，也让 N 排自然纳入现有汇流排层距体系。

N 排规格配置：

```text
NeutralCollectorWidthMm = 60
NeutralCollectorThicknessMm = 6
NeutralBranchWidthMm = 40
NeutralBranchThicknessMm = 4
```

当前这些参数只是工程默认值，后续如果需要严格符合 N 排截面标准，应在规格选择层增加校核：

```text
主排截面
-> N 排标准规则
-> NeutralCollectorProfile / NeutralBranchProfile
```

目前暂不硬编码标准结论，避免在没有标准依据时把经验值写死。

## 11. 迁移步骤

1. 先保留旧代码，不继续在旧逻辑上补偏移。
2. 新增核心数据类：`Busbar`、`ConnectionPort`、`PortRule`、`CollectorLayout`。
3. 新增手动规则提供器，支持 `TypicalDesign` 和 `SouthernGrid`。
4. 新增纯几何路线规划器，只打印路径点，不调用 SolidWorks。
5. 验证路线点、端部预留和搭接端口。
6. 接入统一 `SheetMetalBuilder`。
7. 迁移旧的扫描点、保存零件、插入装配体流程。
8. 最后移除旧的转接排/分支排专用算法。

## 12. 当前待确认项

- 汇流排 A/B/C 三相手动中心位置如何配置。
- 汇流排末端外伸方向和外伸长度的默认值。
- 同侧/异侧 V0 规则表是否覆盖后续南网形结构；当前 N 分支排按同侧分支排逻辑处理，已通过当前典设箱验证。
- 异侧时 V0 默认补偿起点还是终点，当前建议优先补偿汇流排端。
- 连接孔规格、孔径和孔类型。
- N 排截面规格的标准依据，以及是否需要根据主排截面自动校核。
- N 相进线排或变压器中性线接入点的建模规则。
- 南网形结构下分支排和转接排的默认引出方向是否仍按 `Y` 优先。

