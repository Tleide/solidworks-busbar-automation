# SolidWorks 铜排自动建模

本仓库是一个 C# SolidWorks API 二次开发项目，当前目标是从配电箱装配体中的命名参考点自动生成铜排零件，并插回原装配体。

当前主线已经收敛为一套流程：

```text
扫描装配体参考点
-> 构建连接端口
-> 规划 ABC + N 铜排
-> 生成 2D 开放轮廓草图
-> Sheet Metal Base Flange MidPlane 生成钣金铜排
-> 按端口孔中心打孔
-> 保存零件并插回装配体
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
├─ Program.cs                    # 应用入口、默认参数、命令行参数
├─ App                           # 入口辅助方法
├─ CadAbstractions               # CAD 中立规格描述和后端接口雏形
├─ Domain                        # 铜排、端口、点、规格、枚举等纯业务模型
├─ Planning                      # 汇流排布局、路径规划、拓扑补偿、计划构建
├─ Rules                         # 当前手动规则、端口规则
├─ SolidWorks                    # SolidWorks 会话、扫描、钣金、孔、保存、装配插入
└─ TopToDown.csproj
```

分层原则：

- `Domain`、`Rules`、`Planning`、`CadAbstractions` 不引用 SolidWorks API。
- SolidWorks 相关类型集中在 `SolidWorks` 目录。
- `CadAbstractions` 先保留 `SheetMetalPartSpec`、`HoleSpec`、`ICadSheetMetalBuilder`，为后续 UG/NXOpen 或其他 CAD 后端预留接口方向。

## 运行方式

1. 打开 SolidWorks。
2. 打开并激活目标装配体。
3. 构建并运行 `C#/TopToDown/TopToDown/TopToDown.csproj`。
4. 默认会删除装配体中旧的 `Busbar_*` 组件，然后生成新的铜排。

可用参数：

```text
--verbose        输出更详细的特征扫描日志
--keep-existing  保留装配体中已有 Busbar_* 组件
--preview        只生成铜排骨架预览线，不生成实体铜排
```

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
