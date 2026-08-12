# 项目结构说明

本仓库围绕 SolidWorks API 的配电箱铜排自动建模展开。当前主工程已经完成第一轮结构拆分，业务层和 SolidWorks 适配层分开维护。

## `C#/TopToDown`

主工程。当前默认运行完整铜排生成流程。

```text
C#/TopToDown
├─ BusbarAutomation.Core
│  ├─ Domain
│  ├─ Planning
│  ├─ Rules
│  └─ BusbarAutomation.Core.csproj
├─ BusbarAutomation.Application
│  ├─ App
│  └─ BusbarAutomation.Application.csproj
├─ BusbarAutomation.Reporting
│  ├─ Reporting
│  └─ BusbarAutomation.Reporting.csproj
├─ TopToDown
│  ├─ Program.cs
│  ├─ Cli
│  ├─ SolidWorks
│  ├─ TopToDown.csproj
│  └─ Properties

C#/TopToDown/TopToDown.Tests
└─ 纯规则、命令行与规划边界测试
```

核心职责：

- `Program.cs`：默认工程参数和进程级异常边界。
- `BusbarAutomation.Application/App`：可被 CLI/未来 UI 共用的运行选项契约和不依赖 CAD 的规划工作流。
- `BusbarAutomation.Application/App/AssemblySnapshotFactory.cs`：将扫描点识别为标准化设备输入；组件名称识别和额定电流解析集中在此边界。
- `Cli`：严格命令行解析、预检控制台展示和当前 SolidWorks 组合根；未来 UI 应复用 Application 的规划工作流，不复用控制台 presenter。
- `BusbarAutomation.Core/Domain`：铜排业务模型、端口、点、规格、孔型、枚举。
- `BusbarAutomation.Core/Rules`：当前默认规则、端口规则和搭接孔矩阵。
- `BusbarAutomation.Core/Planning`：从标准化装配快照生成两阶段计划。`BusbarDesignPlan` 保存布局、逻辑路径和搭接孔位；`BusbarManufacturingPlan` 再补齐钣金草图线、钣金参数和螺栓计划。
- `BusbarAutomation.Core/Domain/AssemblySnapshot.cs`：规划层使用的装配输入快照，包含设备、额定电流、端口和装配坐标。
- `BusbarAutomation.Core/Domain/EngineeringConfigurationSnapshot.cs`：单次规划使用的工程配置快照，隔离运行期间的配置变更。
- `BusbarAutomation.Core/Domain/BusbarOverlapRuleCatalog.cs`：单次规划与预检共用的搭接孔规则目录快照。
- `BusbarAutomation.Reporting/Reporting`：从制造计划导出生产与加工清单，不调用 SolidWorks API；`ProductionReportService` 处理输出目录，`ProductionReportExporter` 处理 workbook 内容。
- `SolidWorks`：连接 SW、扫描装配体、批量生成和校验真实实体。`SolidWorksBusbarBatchGenerator` 负责批次顺序、暂存校验和替换，`SolidWorksBusbarPartBuilder` 只负责单根零件建模。流程协调入口位于 `Cli/SolidWorksGenerationRunner.cs`。
- `TopToDown.Tests`：不依赖 SolidWorks 的快速自动化测试。

当前有四个生产程序集：`BusbarAutomation.Core.dll`、`BusbarAutomation.Application.dll`、`BusbarAutomation.Reporting.dll` 和 SolidWorks 自动化入口 `TopToDown.exe`。三个类库均不引用 SolidWorks；SolidWorks 和 CLI 继续留在主程序中。详细说明见 `ARCHITECTURE_MIGRATION_PHASE8.md`。

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

当前只有 SolidWorks 一个 CAD 后端，未被使用的 `CadAbstractions` 已删除。未来接入 UG/NXOpen 时，应先复用 `BusbarAutomation.Core`，再根据真实的第二后端需求提取最小接口。

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
- `ARCHITECTURE_MIGRATION_PHASE5.md`：SolidWorks 批次生成与单根零件建模职责的分离及验收记录。
- `ARCHITECTURE_MIGRATION_PHASE6.md`：Domain、Rules、Planning 的物理 Core 程序集边界及验收记录。
- `ARCHITECTURE_MIGRATION_PHASE7.md`：输入标准化和规划工作流的物理 Application 程序集边界及验收记录。
- `ARCHITECTURE_MIGRATION_PHASE8.md`：生产报表的物理 Reporting 程序集边界及最终迁移验收记录。

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

根目录 `铜排搭接逻辑.xlsx` 是当前搭接孔矩阵的人工维护表。代码暂时没有运行时读取该 Excel，而是在 `BusbarAutomation.Core/Rules/BusbarOverlapRuleMatrix.cs` 中固化同等矩阵；这样可以先保持 .NET Framework 项目的依赖简单，后续再迁到数据层读取。

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
