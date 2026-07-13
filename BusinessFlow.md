# 业务流程分析

本文档从业务逻辑角度说明当前铜排自动建模流程。当前程序只保留一条主流程：生成 ABC 三相转接排、ABC/N 汇流排、ABC/N 分支排，并按孔中心打孔装配。

## 1. 总体流程

```text
Program 读取命令行参数
↓
SolidWorksGenerationRunner 进入 SW 生成主线
↓
连接 / 激活 SolidWorks 装配体
↓
扫描装配体和组件参考点
↓
提取连接点 FoundPoint
↓
识别刀熔和漏保
↓
ManualPortRuleProvider 创建 ConnectionPort
↓
CollectorLayoutPlanner 确定汇流排位置和长度
↓
BusbarPlanBuilder 生成连接关系
↓
ContactTopologyResolver 做同侧/异侧拓扑和厚度补偿
↓
BusbarRoutePlanner 路径规划
↓
SolidWorks 层生成 2D 开放轮廓草图
↓
Sheet Metal Base Flange MidPlane 生成钣金
↓
MountingHoleBuilder 孔切除
↓
保存零件并插回装配体
```

## 2. 阶段输入输出

| 阶段 | 输入 | 输出 | 当前位置 |
| --- | --- | --- | --- |
| 命令行入口 | `args` | 运行开关 | `Program.ConfigureFromArgs` |
| SolidWorks 主线 | 运行开关、默认设置 | 完整生成流程 | `SolidWorks/SolidWorksGenerationRunner.cs` |
| 装配体读取 | SolidWorks 会话 | `ModelDoc2`、`AssemblyDoc` | `SolidWorks/SolidWorksSession.cs` |
| 扫描阶段 | 装配体、组件 Transform | `FoundPoint` | `SolidWorks/AssemblyScanner.cs` |
| 点位提取 | `RefPoint` | 装配体坐标 `Point3` | `AssemblyScanner.TransformPoint` |
| 设备识别 | 所有 `FoundPoint` | 刀熔组件、漏保组 | `Planning/BusbarPlanBuilder.cs` |
| 端口生成 | 命名点、手动规则 | `ConnectionPort` | `Rules/ManualPortRuleProvider.cs` |
| 汇流排布局 | 端口、相序、设置 | `CollectorLayout` | `Planning/CollectorLayoutPlanner.cs` |
| 长度控制 | 所有搭接端口范围 | 汇流排 StartX/EndX | `BusbarLengthController.Calculate` |
| 连接关系 | 设备端口、汇流排 Tap | `Busbar` | `BusbarPlanBuilder.CreateBusbar` |
| 拓扑判断 | 起终端口连接面 | 同侧/异侧 | `Planning/ContactTopologyResolver.cs` |
| 补偿计算 | 拓扑、厚度、端部裕度 | 钣金草图线 | `ContactTopologyResolver.CreateSheetMetalSketchLine` |
| 路径规划 | 起终点和规则 | 逻辑中心线 | `Planning/BusbarRoutePlanner.cs` |
| 草图生成 | 钣金草图线 | 2D Sketch | `SolidWorks/SheetMetalSketchBuilder.cs` |
| 钣金生成 | Sketch、宽度、厚度、R、K | Sheet Metal Feature | `SolidWorks/SheetMetalFeatureBuilder.cs` |
| 打孔 | `MountingPorts` | Cut Feature | `SolidWorks/MountingHoleBuilder.cs` |
| 保存装配 | 零件文档 | `SLDPRT`、装配组件 | `SolidWorks/PartPersistenceService.cs` |

## 3. Mermaid 流程图

```mermaid
flowchart TD
    A["Program.Main"] --> B["SolidWorksGenerationRunner"]
    B --> C["SolidWorks 装配体"]
    C --> D["AssemblyScanner: ScanReferencePoints"]
    D --> E["FoundPoint"]
    E --> F["BusbarPlanBuilder: 设备识别"]
    F --> G["ManualPortRuleProvider: ConnectionPort"]
    G --> H["CollectorLayoutPlanner"]
    H --> I["BusbarLengthController"]
    I --> J["BusbarPlan: MainFeed / Collector / Branch / N"]
    J --> K["BusbarRoutePlanner"]
    J --> L["ContactTopologyResolver"]
    K --> M["LogicalCenterline"]
    L --> N["SheetMetalSketchLine"]
    N --> O["SheetMetalSketchBuilder: 2D Open Profile"]
    O --> P["SheetMetalFeatureBuilder: Base Flange MidPlane"]
    P --> Q["MountingHoleBuilder: Hole Cuts"]
    Q --> R["PartPersistenceService: Save + Insert"]
```

## 4. 业务规则摘要

- 铜排连接点按孔中心理解，不按实体边缘理解。
- 铜排端部裕度从孔中心向外延伸。
- 钣金宽度统一使用 SolidWorks 真正的 `MidPlane`，草图线就是宽度中心线。
- 同侧/异侧是拓扑关系，补偿只是当前建模阶段的应用结果。
- N 相当前只做汇流排和分支排，不做刀熔转接排。
- 折弯半径当前仍为默认固定值，后续应迁入 `BendRadiusRules` 或数据层。
- 螺栓大小、孔数、孔径、搭接距离后续应集中到 `HoleLayoutPlanner`、`BusbarLapJointRules`、`FastenerRules`。

## 5. 数据层接入点

当前 `数据层.xlsx` 还没有进入运行流程，但未来可以按下面方式接入：

```text
ExcelDataRepository
-> ComponentCatalog / BusbarCatalog / StandardPartCatalog
-> Rules
-> Planning
-> SolidWorks 渲染
```

这样 UI、Excel、SolidWorks、未来 UG 后端都围绕同一套业务模型和规则工作。

## 6. 2026-07 当前实测流程

默认生成拓扑已经明确为 ABC 双排夹接、N 排单排搭接。ABC 每个漏保生成 `_Lower`、`_Upper` 两根分支排；搭接孔先由宽度矩阵规划为单孔、直双孔或斜双孔，再由 SolidWorks 层在真实实体表面完成贯穿切除。

Z-方向外侧 `_Upper` 从已错层起点出发，经过 Y+ 首段、Y+/Z- 斜向避让、Y+ 上升和 Z+ 回接；起终点和最终搭接高度保持不变。

双排夹接可只生成一个汇流排和一对分支排验证：

```powershell
TopToDown.exe --only=Busbar_A_Collector,Busbar_A_Branch_1_Lower,Busbar_A_Branch_1_Upper
```

不带 `--keep-existing` 时会先清除旧 `Busbar_*` 组件，便于观察实体贴合、孔位和外侧避让。当前实测下排上表面贴合汇流排下表面，上排下表面贴合汇流排上表面；详细记录见 [docs/DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md](docs/DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md)。
