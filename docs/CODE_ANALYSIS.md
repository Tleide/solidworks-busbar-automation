# 二次开发代码梳理

当前主工程已收敛为 `Program -> SolidWorksGenerationRunner -> 扫描/规划/建模/校验/报表`。详细函数索引见 [SCRIPT_FUNCTION_GUIDE.md](SCRIPT_FUNCTION_GUIDE.md)。

## 主流程

```text
解析命令行与校验配置
-> 扫描 SolidWorks 命名参考点
-> 建立并预检 BusbarPlan
-> 生成全部铜排文件
-> 暂存插入并验证实体
-> 通过后替换旧组件
-> 最终校验与报表
```

## 模块说明

### `Program.cs` 与 `App`

- `Program.Main`：唯一进程级异常边界，返回可供脚本判断的退出码。
- `Program.Settings`：当前人工配置入口，包含铜排规格、布局、单双排、折弯和标准件参数。
- `GenerationOptionsParser`：严格解析命令行，未知参数和冲突模式直接失败。

### `Domain`

纯业务对象，不引用 SolidWorks。核心对象是 `Point3`、`ConnectionPort`、`Busbar`、`BusbarPlan`、`BusbarProfile`、`FastenerSpec`。`Point3` 使用米，与 SolidWorks 内部单位一致；工程配置字段明确使用 `Mm` 后缀。

### `Rules`

- `ManualBusbarRuleSet` / `ManualPortRuleProvider`：把命名参考点转换成工程端口。
- `BusbarOverlapRuleMatrix`：批准的 30/40/50/60mm 搭接孔矩阵。未覆盖规格失败关闭，不生成中心孔兜底。

### `Planning`

- `BusbarPlanBuilder`：识别设备、电流规格和拓扑，汇总完整计划。
- `CollectorLayoutPlanner`：计算汇流排位置、长度和各相独立 X- 外伸。
- `BusbarRoutePlanner`：生成单排、双排下搭接和外侧上排避让路径。
- `ContactTopologyResolver`：应用端部裕度和明确的厚度过渡策略。
- `BusbarOverlapHolePlanner`：将搭接中心展开为单孔、直双孔或斜双孔。
- `FastenerPlanBuilder`：按容差分组搭接孔并选择标准螺栓长度。
- `BusbarPreflightValidator`：对计划结果做独立契约检查，不调用 SolidWorks。

### `Reporting`

`ProductionReportExporter` 从 `BusbarPlan` 导出漏保选型、铜排汇总/明细、钻孔清单、标准件汇总和螺栓明细。它不依赖已生成的 SolidWorks 实体。

### `SolidWorks`

- `SolidWorksGenerationRunner`：流程协调，不包含具体草图算法。
- `SolidWorksSession`：连接/启动 SW，获取和激活装配体。
- `AssemblyReferencePointScanner`：只负责参考点扫描与坐标转换。
- `SolidWorksBusbarPartBuilder`：由多个同名 `partial` 文件组成，集中实现一根铜排零件的草图、钣金、孔、保存和批处理。这是一个有内聚性的 CAD 建模器，不再是 `partial Program`。
- `GeneratedComponentManager`：处理暂存回滚和旧组件删除；完整生成清理全部旧件，`--only` 只按选中的铜排基名替换。
- `BusbarGeometryVerifier`：读取实际包络、切除特征和圆柱面，验证尺寸、孔贯穿及双排贴合；每个零件只扫描一次特征树和实体面，再在内存快照中匹配多个孔。
- `SolidWorksCom`：只释放生命周期明确的临时 COM 对象。

## 当前风险边界

- `250A -> 4 x 20mm` 选型已配置，但 20mm 搭接孔规则未批准，相关计划会被阻止。
- Excel 数据层尚未运行时接入，代码配置与工作簿需要人工保持一致。
- 分阶段组件替换降低了预验证失败时丢失旧件的风险，但 SolidWorks 不提供数据库级原子事务。
- 单元测试不能验证 COM API、实际钣金方向和装配干涉，关键几何修改后必须运行真实三件小样。

## 运行参数

```text
--verbose          详细扫描日志
--keep-existing    生成模式下保留旧 Busbar_* 组件，可与 --only 组合
--preview          只生成骨架预览
--validate         只执行规划与预检
--verify-geometry  只校验已有实体
--export-report    只导出报表
--only=...         只生成或校验指定铜排
```

局部双排验证建议使用：

```powershell
TopToDown.exe --only=Busbar_A_Collector,Busbar_A_Branch_1_Lower,Busbar_A_Branch_1_Upper
```

双排公式和孔基准面历史问题见 [DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md](DOUBLE_CLAMP_IMPLEMENTATION_NOTES.md)。
