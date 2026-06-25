# 项目结构说明

本仓库围绕 SolidWorks API 的配电箱铜排自动化二次开展开，当前重点是先跑通从装配体参考点到钣金母排生成的完整闭环。

## `C#/TopToDown`

主程序工程。

职责：

- 连接或启动 SolidWorks。
- 获取当前活动装配体。
- 遍历装配体和子零件。
- 提取命名参考点。
- 将零部件局部坐标转换为装配体坐标。
- 根据参考点生成母排中心线路径。
- 创建 3D 路径草图、截面草图和钣金扫掠特征。
- 保存生成的母排零件，并插回当前装配体。

核心文件：

```text
C#/TopToDown/TopToDown/Program.cs
```

## `C#/FeatureExtract`

独立诊断工具。

职责：

- 连接当前 SolidWorks 会话。
- 扫描当前零件或装配体。
- 打印特征名称、特征类型、参考点名称和坐标。
- 辅助排查模型参考点命名或坐标读取问题。

当主程序没有生成预期母排时，优先使用该工具检查 SolidWorks 模型暴露给 API 的真实数据。

## `C#/.../ReferenceDLL`

SolidWorks Interop 引用 DLL。

这些 DLL 暂时保留在仓库中，方便学习项目在同一 SolidWorks/API 版本族上直接打开和构建。

## `SWtopToDown`

SolidWorks 示例模型目录。

包含：

- 示例装配体
- 熔断器零件
- 漏保零件
- 安装板
- 骨架零件

生成的母排文件按以下规则忽略：

```text
Busbar_*.SLDPRT
```

保留的 `Busbar_Skeleton.SLDPRT` 是样例骨架文件，不属于运行时生成文件。

## `docs`

项目文档目录。

当前包含：

- `PROJECT_STRUCTURE.md`：项目结构说明
- `CODE_ANALYSIS.md`：代码梳理与任务说明
- `GIT_WORKFLOW.md`：基础 Git 工作流说明

## 根目录数据文件

```text
配电箱二次开发_数据层模板.xlsx
配电箱二次开发路线图.md
```

这两个文件用于记录后续数据层、配置层和整体路线图想法。当前主程序仍然以 SolidWorks 钣金母排生成闭环为第一优先级，暂未把 Excel 数据层接入主流程。

