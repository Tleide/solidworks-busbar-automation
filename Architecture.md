# 项目架构分析

本文档描述 `SWApiDesign` 当前真实架构。详细到每个脚本和函数的说明见 [docs/SCRIPT_FUNCTION_GUIDE.md](docs/SCRIPT_FUNCTION_GUIDE.md)。

## 1. 主工程结构

```text
C#/TopToDown
├─ TopToDown.slnx
├─ TopToDown
│  ├─ Program.cs
│  ├─ App
│  │  ├─ AssemblySnapshotFactory.cs
│  │  ├─ BusbarPlanningWorkflow.cs
│  │  └─ GenerationOptions.cs
│  ├─ Cli
│  │  ├─ GenerationOptionsParser.cs
│  │  ├─ PreflightConsolePresenter.cs
│  │  └─ SolidWorksGenerationRunner.cs
│  ├─ Domain
│  ├─ Rules
│  ├─ Planning
│  ├─ Reporting
│  ├─ SolidWorks
│  │  ├─ SolidWorksSession.cs
│  │  ├─ AssemblyScanner.cs
│  │  ├─ BusbarBatchBuilder.cs
│  │  ├─ GeneratedComponentManager.cs
│  │  ├─ BusbarGeometryVerifier.cs
│  │  └─ 草图、钣金、孔、保存等实现文件
│  └─ TopToDown.csproj
└─ TopToDown.Tests
   └─ 纯规则与边界测试
```

未使用的 `CadAbstractions` 已删除。当前只有 SolidWorks 一个 CAD 后端，提前保留接口不能降低复杂度；以后真正接入 UG/NXOpen 时，再从稳定的 `BusbarManufacturingPlan` 中提取两个后端共同需要的最小生成契约。

## 2. 分层职责

| 层 | 职责 | 依赖 SolidWorks |
| --- | --- | --- |
| `Program` / `App` | 默认参数、命令行解析、进程退出码、异常边界 | 否 |
| `Domain` | 点、端口、铜排、规格、孔和标准件等业务对象 | 否 |
| `Rules` | 端口规则、搭接孔矩阵、当前手动规则 | 否 |
| `Planning` | 设计计划、制造计划、布局、路径、拓扑、孔位、预检和螺栓计划 | 否 |
| `Reporting` | 从 `BusbarManufacturingPlan` 导出生产与加工报表 | 否 |
| `SolidWorks` | 扫描装配、生成钣金、打孔、保存、插入、实体校验 | 是 |

依赖方向：

```text
Program
  -> Cli.SolidWorksGenerationRunner
      -> Application.BusbarPlanningWorkflow
          -> AssemblySnapshotFactory
          -> BusbarPlanBuilder.BuildDesignPlan -> Rules / Domain
          -> BusbarManufacturingPlanner.Build
          -> BusbarPreflightValidator
      -> SolidWorksSession / AssemblyReferencePointScanner
      -> SolidWorksBusbarPartBuilder
      -> BusbarGeometryVerifier
      -> Reporting.ProductionReportService -> ProductionReportExporter
```

`Domain`、`Rules`、`Planning`、`Reporting` 不允许引用 `ModelDoc2`、`Feature`、`Component2` 等 SolidWorks 类型。

## 3. 主流程

```text
严格解析命令行
-> 校验配置
-> 连接并扫描当前 SolidWorks 装配体
-> 建立 BusbarDesignPlan
-> 补齐 BusbarManufacturingPlan
-> 生成前预检
-> 生成本轮全部零件文件
-> 临时插入新组件
-> 校验实际尺寸、孔贯穿和双排贴合
-> 校验通过后删除旧 Busbar_* 组件
-> 最终实体校验
-> 完整生成时导出生产报表
```

旧组件删除由 `GeneratedComponentManager` 负责，不属于扫描器。新组件暂存校验失败时，本轮组件和文件会清理，旧组件保持不动。SolidWorks 不提供通用数据库事务，因此旧件删除开始后的异常不能承诺原子回滚，最终校验和装配体版本管理仍然必要。

## 4. 关键设计决定

- `Program` 不再承载 CAD 方法，只保存默认设置并启动 Runner。
- `Cli.SolidWorksGenerationRunner` 是当前组合根，负责连接 CLI 选项、Application 规划流程、SolidWorks 适配器和报表服务；它不是 CAD API 实现类。
- `Application.BusbarPlanningWorkflow` 统一配置快照、设计计划、制造计划和生成前预检，不引用 SolidWorks API。
- `Cli.PreflightConsolePresenter` 负责预检文本展示；`BusbarPreflightReport` 只保存结构化结果，未来 UI 可直接消费。
- `Reporting.ProductionReportService` 负责根据装配路径选择报表输出目录；`ProductionReportExporter` 只负责 workbook 内容。
- `GenerationOptions` 按运行实例传递，不使用全局可变命令行标志。
- 搭接矩阵未覆盖时失败关闭，不再用中心孔掩盖缺失规则。
- 孔切除使用唯一方向和材料厚度，不通过多组参数反复试切。
- 实体校验不仅读取切除特征深度，还匹配实际圆柱面并检查孔沿厚度方向的物理跨度。
- 双排下排通过 `BranchLegRole.Lower` 表达，不再依赖文件名推断业务语义。
- `Point3` 明确使用 SolidWorks 的米制内部单位；面向工程师的配置继续使用 `Mm` 后缀。
- 逻辑布局、路径和搭接孔先进入 `BusbarDesignPlan`；钣金参数、钣金草图线和螺栓选型由 `BusbarManufacturingPlanner` 统一补齐。

## 5. 当前边界

- `250A -> 4 x 20mm` 已配置，但 `20mm` 搭接孔规则尚未批准，因此相关装配会停止规划。
- Excel 数据层尚未成为运行时数据源，当前规则仍由代码配置。
- 自动测试覆盖纯规则与命令行边界；SolidWorks COM、真实实体方向和装配干涉仍需实机验证。
- 折弯半径按公司现有刀具固定为 `5mm`。

双排几何公式与历史故障见 [docs/DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md](docs/DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md)，当前规则与命令见 [docs/BUSBAR_ARCHITECTURE.md](docs/BUSBAR_ARCHITECTURE.md)。
