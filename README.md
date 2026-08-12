# SolidWorks 铜排自动建模

本仓库是一个 C# SolidWorks API 二次开发项目，当前目标是从配电箱装配体中的命名参考点自动生成铜排零件，并插回原装配体。

当前主线已经收敛为一套流程：

```text
扫描装配体参考点
-> 构建连接端口
-> 规划 ABC + N 铜排
-> 生成前规则预检
-> 生成 2D 开放轮廓草图
-> Sheet Metal Base Flange MidPlane 生成钣金铜排
-> 按端口孔中心打孔
-> 暂存插入并校验实体
-> 校验通过后替换旧铜排
-> 导出生产报表
```

## 当前能力

- 自动识别刀熔 `A_OUT/B_OUT/C_OUT`。
- 自动识别漏保 `A_IN/B_IN/C_IN`，如果存在完整 `N_IN`，追加 N 相汇流排和 N 相分支排。
- 生成 ABC 三相转接排、ABC/N 汇流排、ABC/N 分支排。
- 铜排统一使用 2D 开放轮廓中心线 + 钣金 MidPlane。
- 默认钣金参数：折弯半径 `5mm`，K 因子 `0.47`。
- 按端口孔中心生成安装孔，当前默认孔径按规则配置。
- 汇流排长度根据实际搭接位置计算，X- 侧保留扩展长度。

## 当前代码结构

主工程已经从原来的 `Program.cs + BusbarFramework.cs` 两个大文件拆成分层目录：

```text
C#/TopToDown/TopToDown
├─ Program.cs                    # 默认工程参数和进程级异常边界
├─ App                           # 命令行解析和入口辅助方法
├─ Domain                        # 铜排、端口、点、规格、枚举等纯业务模型
├─ Planning                      # 汇流排布局、路径规划、拓扑补偿、计划构建
├─ Rules                         # 当前手动规则、端口规则
├─ Reporting                     # 生产与加工报表
├─ SolidWorks                    # SolidWorks 会话、扫描、钣金、孔、保存、装配插入
└─ TopToDown.csproj
```

分层原则：

- `Domain`、`Rules`、`Planning`、`Reporting` 不引用 SolidWorks API。
- SolidWorks 相关类型集中在 `SolidWorks` 目录。
- 当前只有一个 SolidWorks 后端，因此不保留未被使用的 CAD 接口。后续真正接入 UG/NXOpen 时，再从已经稳定的 `BusbarManufacturingPlan` 提取两个后端共同需要的最小生成契约。
- 主调用方向是 `Program -> SolidWorksGenerationRunner -> AssemblyReferencePointScanner -> AssemblySnapshotFactory -> BusbarPlanBuilder.BuildDesignPlan -> BusbarManufacturingPlanner.Build -> 预检/报表/SolidWorks 建模`。

## 运行方式

1. 打开 SolidWorks。
2. 打开并激活目标装配体。
3. 构建并运行 `C#/TopToDown/TopToDown/TopToDown.csproj`。
4. 默认先生成并插入临时命名的新铜排，实体校验通过后才删除旧的 `Busbar_*` 组件。生成或暂存校验失败时会删除本轮临时组件和零件文件。

可用参数：

```text
--verbose        输出更详细的特征扫描日志
--keep-existing  生成模式下保留装配体中已有 Busbar_* 组件
--preview        只生成铜排骨架预览线，不生成实体铜排
--validate       只执行扫描、规划和生成前预检
--verify-geometry  只校验装配体中已有铜排实体
--export-report  只规划并导出生产报表
--only=名称1,名称2  只生成或校验指定铜排
```

执行模式参数 `--preview`、`--validate`、`--verify-geometry`、`--export-report` 互斥。未知参数、冲突参数、预检失败和实体校验失败都会返回非零进程退出码。

## 数据层

根目录的 `数据层.xlsx` 是当前数据层入口，包含：

- `元件库`：型号、端子名称、相位、局部坐标、端子宽度、螺栓规格、进线法向。
- `标准件库`：螺栓规格、孔径、螺栓长度、弹垫、平垫、螺母、螺栓头等。
- `铜排规格库`：铜排宽度、厚度、截面积、载流量、下料模数、折弯半径等。
- `电气规则`、`相序相色`、`枚举`：后续电气校核和 UI 选项的数据来源。

当前代码还没有读取 Excel，规格、孔径、折弯半径等仍由代码中的默认规则提供。后续会逐步接入数据层。

## 参考点约定

```text
A_OUT / B_OUT / C_OUT    刀熔出线端孔中心
A_IN  / B_IN  / C_IN     漏保三相进线端孔中心
N_IN                       漏保 N 相进线端孔中心，可选；如果出现则必须每个漏保都完整出现
```

## 开发文档

- [Architecture.md](Architecture.md)
- [BusinessFlow.md](BusinessFlow.md)
- [FunctionCallTree.md](FunctionCallTree.md)
- [RefactorProposal.md](RefactorProposal.md)
- [docs/SCRIPT_FUNCTION_GUIDE.md](docs/SCRIPT_FUNCTION_GUIDE.md)
- [docs/BUSBAR_ARCHITECTURE.md](docs/BUSBAR_ARCHITECTURE.md)
- [docs/DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md](docs/DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md)

## 2026-07 当前能力补充

- ABC 分支排默认采用双排夹接：`_Lower` 贴合汇流排下表面，Z-方向外侧 `_Upper` 贴合汇流排上表面；N 排默认采用传统单排。
- 搭接孔已按宽度矩阵规划为单孔、直双孔或斜双孔，并在真实铜排实体表面完成贯穿切除。
- 搭接孔规则严格覆盖 `30/40/50/60mm` 宽度组合；未批准的组合直接阻止规划，不再生成中心孔兜底。当前 `250A -> 4x20mm` 分支排因此不能生成，必须先补充并确认 `20mm` 对应的搭接规则。
- 外侧上排采用 Y+ 首段、Y+/Z- 斜向避让、Y+ 上升、Z+ 回接的路径；起终点和汇流排搭接高度不变。
- 可用 `--only=Busbar_A_Collector,Busbar_A_Branch_1_Lower,Busbar_A_Branch_1_Upper` 做三件局部装配验证；默认只替换这些选中的旧组件，其他 `Busbar_*` 组件保持不动。调试生成时可组合 `--keep-existing`，让暂存的新组件与旧组件并存。

## 测试

纯规则、命令行和规划边界测试位于 `C#/TopToDown/TopToDown.Tests`，不需要启动 SolidWorks：

```powershell
dotnet test C#/TopToDown/TopToDown.Tests/TopToDown.Tests.csproj -c Release
```

SolidWorks COM 建模仍需在打开目标装配体的真实环境中验证；单元测试不能替代孔贯穿、实体尺寸和双排贴合校验。

双排高度、孔草图面、路径参数及已排查的错误见 `docs/DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md`。
