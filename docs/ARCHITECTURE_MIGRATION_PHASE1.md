# 架构迁移 Phase 1：低风险边界整理

Phase 1 在不改变业务规则、建模公式和 SolidWorks API 参数的前提下，建立后续拆分程序集所需的命名空间边界。

## 1. 当前代码状态

当前生产代码仍全部编译进一个 `TopToDown.exe`，目标框架仍是 `.NET Framework 4.8`，平台仍是 `x64`。

Phase 1 没有创建新的生产程序集。命名空间是过渡期的依赖地图，真正的编译隔离将在后续阶段完成。

```text
TopToDown.exe
├─ BusbarAutomation.Cli
├─ BusbarAutomation.Application
├─ BusbarAutomation.Core.Domain
├─ BusbarAutomation.Core.Rules
├─ BusbarAutomation.Core.Planning
├─ BusbarAutomation.Reporting
└─ BusbarAutomation.Cad.SolidWorks
```

测试代码使用：

```text
BusbarAutomation.Tests
```

命名空间根使用 `BusbarAutomation`，而不是 `Busbar`。原因是领域模型中已经存在 `Busbar` 类型；两者同名会使 C# 将 `Busbar` 解析成命名空间并产生大量歧义。未来程序集仍可使用 `Busbar.Core` 等名称，程序集名不要求与 C# 命名空间完全相同。

## 2. 本阶段修改

### 2.1 入口配置

`GenerationOptions` 已从 `Program.cs` 移入：

```text
App/GenerationOptions.cs
```

其字段、默认值和运行行为没有改变。

### 2.2 CLI 参数解析

`GenerationOptionsParser` 已移动到：

```text
Cli/GenerationOptionsParser.cs
```

命令行参数、互斥规则和错误行为没有改变。

### 2.3 命名空间边界

| 目录 | 命名空间 | 当前职责 |
|---|---|---|
| `Domain` | `BusbarAutomation.Core.Domain` | 铜排、端口、路径、孔、紧固件和配置模型 |
| `Rules` | `BusbarAutomation.Core.Rules` | 搭接矩阵和人工工程规则 |
| `Planning` | `BusbarAutomation.Core.Planning` | 识别、布局、路径、孔位、紧固件和生成前预检 |
| `Reporting` | `BusbarAutomation.Reporting` | 生产报表构造与 XLSX 输出 |
| `SolidWorks` | `BusbarAutomation.Cad.SolidWorks` | SW 扫描、建模、装配和实体校验 |
| `App` | `BusbarAutomation.Application` | 可被 CLI/UI 共用的运行选项契约 |
| `Cli` + `Program.cs` | `BusbarAutomation.Cli` | 参数解析、默认配置和进程入口 |

## 3. 当前依赖方向

```text
Cli
├─ Application
├─ Core.Domain
└─ Cad.SolidWorks

Cad.SolidWorks
├─ Application
├─ Core.Domain
├─ Core.Planning
└─ Reporting

Core.Planning
├─ Core.Domain
└─ Core.Rules

Core.Rules
└─ Core.Domain

Reporting
└─ Core.Domain
```

这里仍有两个已知的过渡性问题：

1. `SolidWorksGenerationRunner` 同时承担应用流程和 CAD 调用，导致 `Cad.SolidWorks` 依赖 `Reporting`；
2. `BusbarModels.cs` 中的 `SheetMetalOptions.FromRules` 使 Domain 反向依赖 Rules。

Phase 1 不处理这两个问题，因为它们需要移动职责或改变模型边界，已经超出低风险机械整理范围。后续阶段会在黄金快照保护下逐步消除。

## 4. 明确没有修改的内容

- 铜排规格和单双排选择规则；
- 汇流排位置、长度和补偿参数；
- 主排、分支排和双排夹接路径公式；
- 孔型、孔径、孔位和切除方向；
- 钣金宽度、厚度、折弯半径和 K 因子；
- 螺栓选型和报表统计规则；
- SolidWorks COM 会话、批处理顺序和旧件替换策略；
- `TopToDown.exe` 文件名和现有命令行使用方式。

## 5. 回退方式

Phase 1 应作为一个独立提交保存。若后续发现运行行为差异，可以整体回退该提交，不会影响 Phase 0 黄金快照和 `X=0` 孔圆创建修复。

回退后仍应执行 `ARCHITECTURE_MIGRATION_BASELINE.md` 中的完整门禁，不能只依赖编译结果。

## 6. Phase 1 验证记录

执行日期：2026-08-12。

自动化验证结果：

- Debug 测试：16/16 通过，0 跳过；
- Release 测试：16/16 通过，0 跳过；
- 黄金规划快照保持：3 台漏保、4 根汇流排、28 根铜排、30 个紧固件连接点；
- 真实入口 `--validate`：`errors=0, warnings=0`；
- `Busbar_B_MainFeed` 定向建模：3 个孔全部通过物理贯穿校验；
- 完整批处理：28/28 零件生成和装配完成；
- 暂存几何校验：`errors=0, warnings=0`；
- 最终装配几何校验：`errors=0, warnings=0`；
- 独立 `--verify-geometry`：`errors=0, warnings=0`；
- 生产报表成功输出；
- 验证在系统临时目录的装配体副本上执行，仓库源装配体未被本次验证覆盖。

自动几何校验覆盖了实体包围盒、铜排宽度、孔特征、孔的物理贯穿跨度和双排夹接上下表面接触。SolidWorks 中的人工视觉抽查仍应由使用者在合并前完成，重点查看斜双孔、直双孔、ABC 双排夹接和 N 单排搭接。
