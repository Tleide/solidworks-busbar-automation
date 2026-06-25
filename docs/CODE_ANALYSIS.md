# 二次开发代码梳理

本文档记录当前仓库的核心任务、代码结构和后续开发方向，方便继续迭代 SolidWorks 配电箱母排自动化功能。

## 当前主要任务

当前项目的主任务是跑通一条 SolidWorks API 自动建模闭环：

1. 打开或连接 SolidWorks。
2. 获取当前活动装配体。
3. 扫描装配体和子零件里的命名参考点。
4. 把零件局部坐标转换成装配体全局坐标。
5. 根据 `A_OUT/B_OUT/C_OUT` 和 `A_IN/B_IN/C_IN` 建立三相母排拓扑。
6. 生成主进线母排和汇流母排路径。
7. 生成漏保上端到对应相汇流母排的分支母排。
8. 新建零件，创建 3D 路径草图和钣金截面草图。
9. 使用钣金扫掠特征生成母排。
10. 保存生成的 `Busbar_*.SLDPRT`，并插入回当前装配体。

## 代码模块

### `C#/TopToDown/TopToDown/Program.cs`

主程序，承担母排生成完整流程。

核心流程：

```text
Main
  -> ConfigureFromArgs
  -> GetOrStartSolidWorks
  -> DeleteExistingBusbarComponents
  -> ScanReferencePoints
  -> BuildComplexRoutes
  -> CreateBusbarSolidPart
       -> NewPartDocument
       -> CreatePart3DPathSketch
       -> CreatePartProfileSketch
       -> CreateSweptFlangeFeature
       -> SaveBusbarPart
       -> InsertBusbarPartIntoAssembly
       -> CloseBusbarPartDocument
```

主要数据对象：

- `FoundPoint`：保存扫描到的参考点，坐标统一为装配体坐标。
- `BusbarRoute`：保存一根母排的名称、类型、截面规格和中心线路径点。
- `BusbarProfile`：保存母排宽度和厚度，输入单位为 mm，内部按 SolidWorks API 使用的 m 计算。
- `BusbarSettings`：集中保存母排规格、相间距、偏移距离、钣金参数和搭接参数。
- `CollectorLayout`：保存某一相汇流母排的几何位置。
- `MainCollectorLapLayout`：保存主进线母排与汇流母排搭接位置。

当前搭接控制：

- `MainCollectorLapSide` 控制主进线母排与汇流母排的上搭或下搭。
- `BranchCollectorLapSide` 控制分支母排与汇流母排的上搭或下搭。

当前钣金参数：

- `SheetMetalBendRadiusMm`：折弯半径，当前为 `5mm`。
- `SheetMetalKFactor`：K 因子，当前为 `0.47`。

当前支持参数：

```text
--verbose        输出详细特征扫描信息
--keep-existing  保留已有 Busbar_* 组件
--branch         生成分支母排
--no-branch      不生成分支母排
--no-main        不生成主进线母排
--no-collector   不生成汇流母排
```

默认行为：

- 自动删除当前装配体中旧的 `Busbar_*` 组件。
- 生成主进线母排。
- 生成汇流母排。
- 生成分支母排；如需关闭可传入 `--no-branch`。

分支母排路径规则：

```text
漏保 A/B/C_IN 端子点
  -> 沿 Y 正方向到搭接层高度
  -> 沿 Z 正方向到对应相汇流排位置
```

### `C#/FeatureExtract/FeatureExtract/Program.cs`

辅助诊断工具，不直接生成母排。

用途：

- 查看当前 SolidWorks 文档中 API 能读取到的特征。
- 打印参考点名称和坐标。
- 检查模型里参考点是否命名正确。
- 排查装配体或零件坐标转换问题。

当主程序找不到点、识别错组件或路线异常时，应优先运行这个工具。

## 当前清理和优化

本轮清理后，主程序已经从历史测试代码中收敛到当前可用主线：

- 修复了 `Program.cs` 中重复入口、破碎注释和字符串导致的大量编译错误。
- 删除了未启用的旧 demo/测试路径和临时恢复文件。
- 删除了主程序中不再读取的历史字段，减少误导。
- 恢复为钣金扫掠母排生成，而不是普通实体扫掠。
- 恢复关键钣金参数：折弯半径 `5mm`、K 因子 `0.47`。
- 保留 `FeatureExtract` 作为有效诊断工具。
- 删除 `demo/` 演示媒体目录，减轻仓库负担。
- 删除 `.vs` IDE 缓存目录。
- 更新 `.gitignore`，继续忽略构建产物、IDE 缓存和生成母排零件。
- 更新 README 和项目结构说明，修复旧文档中的乱码和过期目录说明。

## 运行前检查

运行主程序前建议确认：

- SolidWorks 已打开。
- 当前活动文档是装配体。
- 目标零件不是轻化或未加载状态。
- 熔断器类组件包含 `A_OUT`、`B_OUT`、`C_OUT`。
- 漏保类组件包含 `A_IN`、`B_IN`、`C_IN`。
- 示例模型已保存到磁盘，否则生成零件会默认保存到桌面。

## 后续开发方向

建议下一步按这个顺序推进：

1. 在 SolidWorks 中实测当前主流程，确认钣金扫掠是否稳定生成、能否正常展开。
2. 微调主进线母排搭接汇流排时的 `FrontZ / EndZ / Depth` 几何关系，消除主排穿模。
3. 继续微调分支母排搭接层高度和净空，避免贴死或轻微咬模。
4. 将根目录 Excel 数据层模板接入主程序，让母排规格、组件类型和拓扑配置可由表格驱动。
5. 把组件识别从名称提示升级为配置文件或属性读取，减少对文件名的依赖。
6. 增加运行日志导出，方便记录每次生成的点位、路径和保存文件。

