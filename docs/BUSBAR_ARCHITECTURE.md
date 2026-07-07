# 铜排自动建模架构设计

本文档记录当前铜排自动建模主线的规则。历史 demo、旧扫掠路线和版本化命名已经移除，当前代码已经拆成 `Domain`、`Rules`、`Planning`、`SolidWorks`、`CadAbstractions` 等目录，后续开发应在当前主线中扩展规则和模块。

## 1. 建模原则

- 所有铜排统一使用 2D 开放轮廓中心线 + Sheet Metal Base Flange。
- 铜排宽度方向统一使用 SolidWorks 的真实 `MidPlane`。
- 草图线代表铜排宽度中心线，不再通过人工宽度偏移修正装配位置。
- 默认折弯半径 `5mm`，默认 K 因子 `0.47`。
- 连接点代表孔中心，端部裕度由孔中心向铜排端部外伸。
- 同侧/异侧先作为拓扑关系判断，再由拓扑关系决定是否做厚度补偿。

## 2. 坐标和面定义

| 方向 | 含义 |
| --- | --- |
| `X+` | 正视柜门时向左 |
| `Y+` | 向上 |
| `Z+` | 向柜体内部 |

| 面 | 含义 |
| --- | --- |
| `Front` | `Z-` 侧 |
| `Back` | `Z+` 侧 |
| `Upper` | `Y+` 侧 |
| `Lower` | `Y-` 侧 |
| `Left` | `X+` 侧 |
| `Right` | `X-` 侧 |

## 3. 当前规格参数

| 参数 | 默认值 |
| --- | --- |
| 转接排 | `6 x 60mm` |
| ABC 汇流排 | `6 x 80mm` |
| ABC 分支排 | `4 x 40mm` |
| N 汇流排 | `6 x 60mm` |
| N 分支排 | `4 x 40mm` |
| 汇流排相间距 | `60mm` |
| 汇流排上方净距 | `240mm` |
| 汇流排相对漏保 Z 偏移 | `120mm` |
| 汇流排 X- 侧外伸 | `50mm` |
| 转接排初始 Y 引出 | `40mm` |
| 折弯半径 | `5mm` |
| K 因子 | `0.47` |

## 4. SolidWorks API 经验

开放轮廓钣金两侧对称不能用 `Dist1=Width/2`、`Dist2=Width/2` 模拟。当前正确约定是：

```text
Dist1 = Width
Dist2 = 0
EndCondition1 = swEndCondMidPlane
EndCondition2 = swEndCondBlind
DirToUse = 1
```

孔切除当前最可靠流程：

```text
创建孔草图平面
-> 打开草图并画圆
-> 优先从 active sketch 直接 FeatureCut4
-> 失败后再选择 sketch feature 做 fallback cut
```

## 5. 当前主对象

| 对象 | 含义 |
| --- | --- |
| `FoundPoint` | 从 SW 扫描得到的命名参考点，坐标已转换到装配体坐标。 |
| `ConnectionPort` | 可连接铜排的工程端口，包含孔中心、贴合面、引出方向、裕度、孔径。 |
| `CollectorLayout` | 某相汇流排的中心位置、方向、长度和 Tap 端口。 |
| `Busbar` | 一根待生成铜排，包含端口、规格、逻辑中心线、钣金草图线和孔位。 |
| `BusbarPlan` | 当前装配体的完整铜排规划结果。 |

## 6. N 相规则

- N 相当前只生成汇流排和分支排。
- 若没有任何 `N_IN`，跳过 N 相生成。
- 若只存在部分 `N_IN`，直接报错，避免生成不完整 N 排。
- N 排规格独立于 ABC，可后续接入标准校核和自动选型。

## 7. 后续重点

- 把孔位计算抽成独立 `HoleLayoutPlanner`。
- 把折弯半径从固定值抽成 `BendRadiusRules`，支持铜排厚度、工艺规则和 UI/数据层覆盖。
- 把螺栓规格、两孔/四孔规则做成可配置标准表。
- 汇流排间距从固定值升级为基于螺栓长度和电气间隙的计算。
- 汇流排位置从固定偏移升级为布局优化函数。
- 让 SolidWorks 生成层逐步消费 `SheetMetalPartSpec`，为未来 UG/NXOpen 后端保留边界。
