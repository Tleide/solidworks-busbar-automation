# 架构重构建议

当前项目已经完成第一轮结构拆分：旧的 `Program.cs + BusbarFramework.cs` 两个大文件已经拆成 `Domain`、`Rules`、`Planning`、`SolidWorks`、`CadAbstractions` 等目录。默认运行仍然走当前钣金主线。

## 1. 已完成的拆分

### 入口层

`Program.cs` 当前只负责：

- 默认参数初始化。
- 命令行参数解析。
- 异常边界。
- 调用 `RunSolidWorksGeneration()`。

### 业务层

`Domain`、`Rules`、`Planning` 已经从 SolidWorks API 中分离：

```text
Domain      点、端口、铜排、规格、计划对象、枚举
Rules       当前默认规则、端口规则
Planning    汇流排布局、长度、路径、拓扑、计划构建
```

这些目录不引用 `SolidWorks.Interop`。

### SolidWorks 适配层

SolidWorks 相关代码已经集中到 `SolidWorks` 目录：

```text
SolidWorksSession
AssemblyScanner
PreviewBuilder
BusbarBatchBuilder
SheetMetalFeatureBuilder
SheetMetalSketchBuilder
MountingHoleBuilder
CadGeometryUtilities
PartPersistenceService
SolidWorksGenerationRunner
```

### CAD 抽象雏形

`CadAbstractions` 中已有：

```text
SheetMetalPartSpec
HoleSpec
ICadSheetMetalBuilder
```

这部分暂未接入主流程，主要用于后续把 SolidWorks 生成流程收敛成可替换后端。

## 2. 当前仍然耦合较高的位置

### `partial Program`

SolidWorks 目录下的方法目前仍通过 `partial Program` 共享私有方法和全局设置。这是低风险拆分后的过渡形态，下一步可以逐步改成独立服务类：

```text
SolidWorksGenerationRunner
SolidWorksSession
AssemblyScanner
SheetMetalPartBuilder
MountingHoleBuilder
PartPersistenceService
```

### 固定参数

`BusbarSettings` 仍在 `Program.cs` 里直接创建。后续 UI 和数据层接入后，应改为：

```text
GenerationRequest
GenerationOptions
ExcelDataRepository
```

### 规则仍偏硬编码

当前孔径、折弯半径、端部裕度仍来自默认规则。后续应拆出：

```text
BendRadiusRules
ElectricalClearanceRules
BusbarLapJointRules
FastenerRules
HoleLayoutPlanner
```

## 3. 推荐下一阶段

1. 抽 `GenerationRequest` 和 `GenerationOptions`  
   让命令行、未来 UI、默认配置都生成同一个请求对象。

2. 抽 `BendRadiusRules`  
   先把固定 `SheetMetalBendRadiusMm = 5.0` 变成规则计算结果，支持 UI 覆盖和数据层覆盖。

3. 抽 `HoleLayoutPlanner`  
   当前是一端口一孔，后续搭接规则会涉及孔数、孔径、孔距、边距、螺栓规格。

4. 接入 `数据层.xlsx`  
   增加 `ExcelDataRepository`、`BusbarCatalog`、`StandardPartCatalog`，先只读铜排规格和标准件。

5. 让 SolidWorks 后端消费 `SheetMetalPartSpec`  
   将 `Busbar` 转成 CAD 中立描述，再由 SolidWorks 层建模。

## 4. 未来扩展性

| 扩展方向 | 当前支持情况 | 改进点 |
| --- | --- | --- |
| UI 参数输入 | 默认参数仍在代码里 | 增加 `GenerationRequest`，UI 只负责填请求对象 |
| 折弯半径规则 | 当前固定值 | 增加 `BendRadiusRules`，支持厚度/材料/工艺/覆盖值 |
| 电气间隙 | 数据层已有规则表，未接入 | 增加 `ElectricalClearanceRules` 和校核报告 |
| 铜排搭接 | 同侧/异侧已有拓扑入口 | 增加 `BusbarLapJointRules` |
| 螺栓规格 | `数据层.xlsx` 已有标准件库 | 增加 `FastenerRules` 和标准件查询 |
| 自动孔位 | 当前按端口孔中心直接打孔 | 增加 `HoleLayoutPlanner` |
| 自动汇流排位置 | 当前固定偏移 | 增加布局优化函数 |
| UG/NXOpen 后端 | 仅有 CAD 抽象雏形 | 新增 `CadNx` 实现，不改业务层 |

## 5. 设计原则

后续继续沿用这个边界：

```text
业务层表达“要生成什么”
CAD 层负责“在具体软件里怎么生成”
数据层提供“规则和规格从哪里来”
UI 层只负责“用户如何输入和覆盖参数”
```

不要把 SolidWorks API、Excel 读取、UI 控件状态混到规则计算里。这样后面接电气知识、搭接规则、标准件库和其他 CAD 后端时，改动会比较稳。
