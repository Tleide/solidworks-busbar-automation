# 后续重构建议

第一轮结构重构已经完成：入口、业务规划、SolidWorks 建模、实体校验和报表职责已分开；未使用的 CAD 接口和 `partial Program` 已删除。本文件只记录尚未完成且有明确收益的事项。

## 当前状态

```text
Program / GenerationOptionsParser
-> SolidWorksGenerationRunner
-> AssemblyReferencePointScanner
-> BusbarPlanBuilder + Rules + Preflight
-> SolidWorksBusbarBatchGenerator
-> SolidWorksBusbarPartBuilder
-> BusbarGeometryVerifier
-> ProductionReportExporter
```

已经完成：

- `Application` 已形成独立的 `BusbarAutomation.Application.dll`，只依赖 Core，不依赖 CAD。
- `Domain / Rules / Planning` 已形成独立的 `BusbarAutomation.Core.dll` 编译边界。
- 命令行严格解析与非零失败退出码。
- 搭接孔规则失败关闭。
- 双排上下腿使用明确领域语义。
- 新组件暂存校验后再替换旧件。
- 孔的实际圆柱面贯穿校验。
- 关键纯规则自动化测试。

## Phase 1：补齐业务规则

1. 先确认 `20mm` 分支排与 `50/60mm` 汇流排的孔数、孔径和孔距，再扩展 `BusbarOverlapRuleMatrix`。在规则获批前保持阻断生成。
2. 明确 N 排补偿回路预留孔的孔径、数量、位置和与平垫/相邻铜排的最小间距。
3. 把已经确认的规则以测试用例固化，测试数据应来自批准的标准表，不复制规划算法。

## Phase 2：接入配置与 UI

1. 将 `Program.Settings` 迁到一个可序列化的项目配置对象，UI 和命令行共用同一加载路径。
2. UI 先提供参数编辑、预检结果和生成进度，不在 UI 层复制规则。
3. 将 `数据层.xlsx` 的已批准工作表通过单一 Repository 读取为领域对象；读取失败时报告具体工作表、行和字段。

只在 UI 或第二种输入源真正接入时再做配置服务抽象，当前不要提前增加接口层。

## Phase 3：第二 CAD 后端

只有在 UG/NXOpen 原型真实启动后才提取 CAD 后端契约。步骤应是：

```text
比较 SolidWorks 与 NXOpen 都需要的输入
-> 从 BusbarPlan 提取最小不可变生成规格
-> 分别实现两个后端
-> 保持 Rules / Planning 不引用任一 CAD API
```

不要恢复此前未被使用的通用接口；第二后端的实际差异会决定正确边界。

## 暂不处理

- 折弯半径规则化：公司当前固定使用 `5mm` 刀具，暂时没有业务收益。
- 为可能出现的第三种 CAD、数据库或云端提前设计插件系统。
- 在没有第二个实现前为每个类增加 interface。

当前最有价值的下一步不是继续拆类，而是补齐 `20mm` 和 N 排预留孔的经批准工程规则，并用现有预检、测试和实体校验链路验证。
