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
- `Domain`：铜排业务模型、端口、点、规格、枚举。
- `Rules`：当前默认规则和端口规则。
- `Planning`：从扫描点生成 `BusbarPlan`，包含布局、长度、路径、拓扑补偿。
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

## 根目录文档

- `README.md`：项目入口说明。
- `Architecture.md`：当前架构分析。
- `BusinessFlow.md`：业务流程分析。
- `FunctionCallTree.md`：函数调用树。
- `RefactorProposal.md`：后续重构建议。
