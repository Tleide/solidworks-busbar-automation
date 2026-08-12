# 项目结构说明

本仓库围绕 SolidWorks API 的配电箱铜排自动建模展开。当前主工程已经完成第一轮结构拆分，业务层和 SolidWorks 适配层分开维护。

## `C#/TopToDown`

主工程。当前默认运行完整铜排生成流程。

```text
C#/TopToDown/TopToDown
├─ Program.cs
├─ App
├─ Cli
├─ Domain
├─ Planning
├─ Rules
├─ Reporting
├─ SolidWorks
├─ TopToDown.csproj
└─ Properties

C#/TopToDown/TopToDown.Tests
└─ 纯规则、命令行与规划边界测试
```

核心职责：

- `Program.cs`：默认工程参数和进程级异常边界。
- `App`：可被 CLI/UI 共用的运行选项契约和不依赖 CAD 的规划工作流。
- `App/AssemblySnapshotFactory.cs`：将 SolidWorks 扫描点识别为标准化设备输入；组件名称识别和额定电流解析集中在此边界。
- `Cli`：严格命令行解析、预检控制台展示和当前 SolidWorks 组合根；UI 后续应复用 `App/BusbarPlanningWorkflow`，不复用控制台 presenter。
- `Domain`：铜排业务模型、端口、点、规格、孔型、枚举。
- `Rules`：当前默认规则、端口规则和搭接孔矩阵。
- `Planning`：从标准化装配快照生成两阶段计划。`BusbarDesignPlan` 保存布局、逻辑路径和搭接孔位；`BusbarManufacturingPlan` 再补齐钣金草图线、钣金参数和螺栓计划。
- `Domain/AssemblySnapshot.cs`：规划层使用的装配输入快照，包含设备、额定电流、端口和装配坐标。
- `Domain/EngineeringConfigurationSnapshot.cs`：单次规划使用的工程配置快照，隔离运行期间的配置变更。
- `Domain/BusbarOverlapRuleCatalog.cs`：单次规划与预检共用的搭接孔规则目录快照。
- `Reporting`：从制造计划导出生产与加工清单，不调用 SolidWorks API；`ProductionReportService` 处理输出目录，`ProductionReportExporter` 处理 workbook 内容。
- `SolidWorks`：连接 SW、扫描装配体、生成草图/钣金/孔、分阶段替换组件并校验真实实体。流程协调入口已移至 `Cli/SolidWorksGenerationRunner.cs`。
- `TopToDown.Tests`：不依赖 SolidWorks 的快速自动化测试。

当前仍是一个生产程序集 `TopToDown.exe`。目录现已映射到 `BusbarAutomation.*` 命名空间，用于显式表达依赖并为后续拆分程序集做准备；这不代表已经形成编译隔离。详细说明见 `ARCHITECTURE_MIGRATION_PHASE1.md`。

当前规划主链为：

```text
AssemblySnapshot
  -> BusbarPlanBuilder.BuildDesignPlan
  -> BusbarDesignPlan
  -> BusbarManufacturingPlanner.Build
  -> BusbarManufacturingPlan
  -> 预检 / 报表 / SolidWorks 建模
```

Phase 3 仍复用同一组 `Busbar` 对象，由制造规划器填入派生的钣金字段，避免复制路径和孔位对象造成两套结果漂移。详细边界、限制和验收记录见 `ARCHITECTURE_MIGRATION_PHASE3.md`。

当前只有 SolidWorks 一个 CAD 后端，未被使用的 `CadAbstractions` 已删除。未来接入 UG/NXOpen 时，应先复用 `Domain/Rules/Planning`，再根据真实的第二后端需求提取最小接口。

## `C#/FeatureExtract`

诊断工具。用于查看 SolidWorks API 能读取到的特征、参考点和坐标。

## `docs`

文档目录：

- `BUSBAR_ARCHITECTURE.md`：铜排建模规则和经验。
- `CODE_ANALYSIS.md`：代码结构梳理。
- `SCRIPT_FUNCTION_GUIDE.md`：各脚本、类、函数作用导览。
- `GIT_WORKFLOW.md`：Git 工作流说明。
- `PROJECT_STRUCTURE.md`：项目结构说明。
- `ARCHITECTURE_MIGRATION_PHASE3.md`：设计计划与制造计划的分离边界、限制和验证记录。
- `ARCHITECTURE_MIGRATION_PHASE4.md`：Application、CLI、Reporting 和 SolidWorks 调度职责的分离及验收记录。

## `SWtopToDown`

SolidWorks 示例装配和零件目录。

- `APITest.SLDASM`、`Top-Down.SLDASM`：示例装配体。
- `fuse24.SLDPRT`、`loubao.SLDPRT`：示例设备。
- `Busbar_*.SLDPRT`：程序运行生成的铜排零件。

## 数据层

根目录 `数据层.xlsx` 是后续规则和规格数据来源：

- `元件库`
- `标准件库`
- `铜排规格库`
- `电气规则`
- `相序相色`
- `枚举`

当前代码尚未读取该 Excel，后续会逐步接入。

根目录 `铜排搭接逻辑.xlsx` 是当前搭接孔矩阵的人工维护表。代码暂时没有运行时读取该 Excel，而是在 `Rules/BusbarOverlapRuleMatrix.cs` 中固化同等矩阵；这样可以先保持 .NET Framework 项目的依赖简单，后续再迁到数据层读取。

## 根目录文档

- `README.md`：项目入口说明。
- `Architecture.md`：当前架构分析。
- `BusinessFlow.md`：业务流程分析。
- `FunctionCallTree.md`：函数调用树。
- `RefactorProposal.md`：后续重构建议。

## 当前建模经验入口

`DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md` 是双排夹接的权威实测记录，包含：

- 汇流排、下排、上排的真实实体表面关系。
- 孔草图平面与单侧切除厚度的确定方式。
- ABC 双排 / N 单排的拓扑选择。
- Z-方向外侧 `_Upper` 的斜向避让路径和参数。
- 三件局部装配的生成、边界框验证与观察方法。

当调整钣金基体法兰、路径、孔基准面或切除方向时，先参照该记录做实体边界框验证，再更新规划公式；不要用移动整体汇流排或写死坐标来掩盖几何推导问题。
