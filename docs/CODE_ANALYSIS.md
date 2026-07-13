# 二次开发代码梳理

当前主工程 `TopToDown` 已经收敛为单一铜排自动生成主线，并完成了第一轮“业务层 / CAD 适配层”拆分。

如果需要按“脚本文件 -> 函数/类 -> 作用”的方式查细节，优先看 [SCRIPT_FUNCTION_GUIDE.md](SCRIPT_FUNCTION_GUIDE.md)。

## 当前主任务

```text
连接 SolidWorks
-> 获取当前装配体
-> 扫描命名参考点
-> 构建铜排规划
-> 生成钣金铜排
-> 打孔
-> 保存并插回装配体
```

## 核心代码模块

### `Program.cs`

负责应用入口：

- `Main`：统一异常边界。
- `ConfigureFromArgs`：支持 `--verbose`、`--keep-existing`、`--preview`。
- 默认 `BusbarSettings`：当前仍在这里初始化，后续适合迁移到 `GenerationOptions` 或 UI/数据层。

### `SolidWorks`

负责 SolidWorks API 适配：

- `SolidWorksGenerationRunner`：当前 SW 后端主流程。
- `SolidWorksSession`：连接或启动 SW，获取当前装配体。
- `AssemblyScanner`：扫描装配体和组件参考点。
- `PreviewBuilder`：生成预览骨架。
- `BusbarBatchBuilder`：选择和批量生成铜排。
- `SheetMetalFeatureBuilder`：调用 Sheet Metal Base Flange。
- `SheetMetalSketchBuilder`：创建开放轮廓草图。
- `MountingHoleBuilder`：创建孔草图和 Cut Feature。
- `CadGeometryUtilities`：平面、坐标、草图辅助函数。
- `PartPersistenceService`：保存零件并插回装配体。

### `Domain`

负责纯业务对象：

- `Point3`
- `FoundPoint`
- `BusbarProfile`
- `ConnectionPort`
- `Busbar`
- `CollectorLayout`
- `BusbarPlan`
- 各类枚举和钣金辅助模型

### `Rules`

负责规则入口：

- `ManualBusbarRuleSet`：当前默认规则和参数。
- `ManualPortRuleProvider`：把参考点转换为端口。

后续适合新增：

```text
BendRadiusRules
ElectricalClearanceRules
BusbarLapJointRules
FastenerRules
HoleLayoutPlanner
```

### `Planning`

负责业务规划：

- `CollectorLayoutPlanner`：计算汇流排位置。
- `BusbarLengthController`：根据搭接范围计算汇流排长度。
- `BusbarRoutePlanner`：生成孔中心意义上的逻辑路径。
- `ContactTopologyResolver`：处理同侧/异侧、端部裕度和厚度补偿。
- `BusbarPlanBuilder`：从扫描点生成完整 `BusbarPlan`。

### `CadAbstractions`

当前是后端解耦雏形：

- `SheetMetalPartSpec`
- `HoleSpec`
- `ICadSheetMetalBuilder`

后续目标是让业务层输出 CAD 中立规格，由 SolidWorks、UG/NXOpen 或其他 CAD 后端分别实现。

## 运行参数

```text
--verbose        输出详细扫描日志
--keep-existing  不删除已有 Busbar_* 组件
--preview        只生成预览骨架线
```

## 当前注意事项

- `SWtopToDown/Busbar_*.SLDPRT` 是运行生成物，不作为源码维护。
- `SWtopToDown/APITest.SLDASM` 可能因 SolidWorks 运行被修改，提交前要确认是否需要纳入版本。
- 当前 N 相只生成 N 汇流排和 N 分支排。
- 当前孔位仍以端口孔中心为主，后续应抽成孔位规划模块。
- 当前折弯半径仍为固定默认值，后续应由规则或数据层计算。

## 2026-07 当前实测经验

- 分支排拓扑已按相别拆分：ABC 默认 `DoubleClamp`，N 默认 `Single`；ABC 每个漏保生成 `_Lower`、`_Upper`，N 只生成一根分支排。
- `BusbarOverlapHolePlanner` 已将搭接中心 Tap 展开为单孔、直双孔或斜双孔；`MountingHoleBuilder` 只负责将孔草图放到真实铜排实体表面并按厚度贯穿切除。
- ABC Z-方向外侧 `_Upper` 使用专用的 Y+ 首段、Y+/Z- 斜段、Y+ 上升、Z+ 回接路径。它改变中间避让形状，但不改变上下排与汇流排的最终贴合高度。
- 当前后端中汇流排路径对应实体上表面，分支排搭接端路径对应实体下表面；路径参考坐标、实体接触面和孔草图面不可混用。

局部调试建议：不带 `--keep-existing` 运行 `--only=Busbar_A_Collector,Busbar_A_Branch_1_Lower,Busbar_A_Branch_1_Upper`，获得干净的三件局部装配。详细公式和实测日志结论见 [DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md](DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md)。
