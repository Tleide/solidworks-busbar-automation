# SolidWorks 母排自动化二次开发

这是一个 C# SolidWorks API 学习项目，目标是在配电箱装配体中自动识别端子参考点，并生成铜排路径与钣金母排零件。

当前代码聚焦一条可运行主线：连接 SolidWorks，扫描装配体参考点，生成母排路线，创建钣金扫掠母排，保存零件并插回装配体。

## 当前状态

项目仍处于学习和原型验证阶段，不是生产级电气设计工具。

当前重点：

- SolidWorks COM API 连接与环境验证
- 装配体零部件遍历
- 命名参考点读取与坐标转换
- 熔断器到漏保的三相母排路径生成
- 主进线母排、汇流母排、分支母排的参数化生成
- 生成零件保存并回插到当前装配体

## 目录结构

```text
.
├── C#/
│   ├── TopToDown/        # 主程序：母排路径和钣金母排生成
│   └── FeatureExtract/   # 辅助工具：扫描特征、参考点和坐标
├── SWtopToDown/          # SolidWorks 示例装配体和零件
├── docs/                 # 项目结构和 Git 工作流说明
├── 配电箱二次开发_数据层模板.xlsx
└── 配电箱二次开发路线图.md
```

更详细的结构说明见 [docs/PROJECT_STRUCTURE.md](docs/PROJECT_STRUCTURE.md)。

## 主程序功能

`C#/TopToDown/TopToDown/Program.cs` 负责：

- 获取当前 SolidWorks 实例，必要时启动 SolidWorks。
- 要求当前活动文档是装配体。
- 扫描装配体和子零件中的 `RefPoint` 特征。
- 识别 `A_OUT`、`B_OUT`、`C_OUT`、`A_IN`、`B_IN`、`C_IN` 等命名端子点。
- 将零部件局部坐标转换为装配体全局坐标。
- 根据零部件名称提示识别熔断器和漏保。
- 生成主进线、汇流和分支母排路径。
- 创建 3D 路径草图和矩形截面草图。
- 通过钣金扫掠生成母排零件。
- 将生成零件保存为 `Busbar_*.SLDPRT` 并插入装配体。

## 运行方式

1. 打开 SolidWorks。
2. 打开 `SWtopToDown/` 中的示例装配体。
3. 打开 `C#/TopToDown/TopToDown.slnx`。
4. 构建并运行 `TopToDown` 项目。
5. 确认控制台输出中的参考点、路线和生成零件信息。

命令行参数：

```text
--verbose        输出更详细的特征扫描信息
--keep-existing  保留已有 Busbar_* 组件
--branch         生成分支母排
--no-branch      不生成分支母排
--no-main        不生成主进线母排
--no-collector   不生成汇流母排
```

默认会删除旧的 `Busbar_*` 组件，并重新生成主进线、汇流和分支母排；如果只想先看主排和汇流排，可以用 `--no-branch` 关闭分支母排。

当前搭接方向参数：

- `MainCollectorLapSide`：主进线母排搭接在汇流排上侧还是下侧
- `BranchCollectorLapSide`：分支母排搭接在汇流排上侧还是下侧

当前钣金参数：

- `SheetMetalBendRadiusMm = 5.0`
- `SheetMetalKFactor = 0.47`

## 参考点命名约定

当前路线逻辑依赖以下参考点命名：

```text
A_OUT
B_OUT
C_OUT
A_IN
B_IN
C_IN
```

典型含义：

- `*_OUT`：熔断器或上游器件出线端
- `*_IN`：漏保或下游器件进线端

## 辅助工具

`C#/FeatureExtract` 是独立诊断工具，用来确认 SolidWorks API 能看到哪些特征、参考点和坐标。

当主程序没有生成预期路线时，建议先运行这个工具，确认模型里的参考点名称和坐标是否符合约定。

## 注意事项

- `.SLDPRT` 和 `.SLDASM` 是二进制文件，Git 不能展示精细文本差异。
- 生成的 `SWtopToDown/Busbar_*.SLDPRT` 默认被 Git 忽略。
- `bin/`、`obj/`、`.vs/` 等构建和 IDE 缓存目录不应提交。
- 大型生产 CAD 文件建议使用 SolidWorks PDM 或其他 CAD 数据管理流程。

## 开发文档

- [项目结构说明](docs/PROJECT_STRUCTURE.md)
- [Git 工作流说明](docs/GIT_WORKFLOW.md)
