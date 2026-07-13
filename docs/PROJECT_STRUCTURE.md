# 项目结构说明

本仓库围绕 SolidWorks API 的配电箱铜排自动建模展开。当前主工程已经完成第一轮结构拆分，业务层和 SolidWorks 适配层分开维护。

## `C#/TopToDown`

主工程。当前默认运行完整铜排生成流程。

```text
C#/TopToDown/TopToDown
├─ Program.cs
├─ App
├─ CadAbstractions
├─ Domain
├─ Planning
├─ Rules
├─ SolidWorks
├─ TopToDown.csproj
└─ Properties
```

核心职责：

- `Program.cs`：应用入口、默认参数、命令行参数。
- `App`：入口辅助方法。
- `Domain`：铜排业务模型、端口、点、规格、孔型、枚举。
- `Rules`：当前默认规则、端口规则和搭接孔矩阵。
- `Planning`：从扫描点生成 `BusbarPlan`，包含布局、长度、路径、拓扑补偿和搭接孔位规划。
- `SolidWorks`：连接 SW、扫描装配体、生成草图/钣金/孔、保存并插回装配体。
- `CadAbstractions`：CAD 中立零件规格和后端接口雏形，后续用于 SW/UG 等后端解耦。

## `C#/FeatureExtract`

诊断工具。用于查看 SolidWorks API 能读取到的特征、参考点和坐标。

## `docs`

文档目录：

- `BUSBAR_ARCHITECTURE.md`：铜排建模规则和经验。
- `CODE_ANALYSIS.md`：代码结构梳理。
- `SCRIPT_FUNCTION_GUIDE.md`：各脚本、类、函数作用导览。
- `GIT_WORKFLOW.md`：Git 工作流说明。
- `PROJECT_STRUCTURE.md`：项目结构说明。

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
