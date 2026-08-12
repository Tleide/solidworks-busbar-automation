# 架构迁移 Phase 0 基线

本文档定义后续渐进式架构迁移必须保持的功能基线。Phase 0 原则上只增加特征测试和验收标准；如果基线执行暴露现有核心功能阻断，则先做独立的最小修复，再冻结修复后的行为。

## 0.1 基线建立期间发现的阻断问题

基线首次执行完整 SolidWorks 生成时，在 `Busbar_B_MainFeed` 的 P1 孔失败。原因是该孔转换到草图坐标后圆心落在草图轴 `X=0`，SolidWorks 草图自动推理使 `CreateCircleByRadius` 返回空对象。

已在 [MountingHoleBuilder.cs](../C%23/TopToDown/TopToDown/SolidWorks/MountingHoleBuilder.cs) 中做最小修复：创建圆期间临时启用 `SketchManager.AddToDB`，创建结束后恢复原状态。孔坐标、孔径、基准面、切除深度和路径计算没有改变。

验证结果：

- `Busbar_B_MainFeed` 定向生成：3 个孔创建成功，物理贯穿校验通过；
- 完整 28 件生成：暂存校验和最终校验均为 `errors=0, warnings=0`；
- 生成后独立 `--verify-geometry`：`errors=0, warnings=0`。

该修复是建立可用基线所必需的边界修复，不属于架构拆分。后续架构迁移仍应从该修复后的版本开始。

## 1. 自动化规划基线

代表性装配输入采用当前实际组合：

- 1 台 `HR6-630` 刀熔；
- 1 台 `PGM8LZ-630` 漏保；
- 2 台 `PGM8LZ-400` 漏保；
- ABC 分支排使用双排夹接；
- N 分支排使用单排搭接。

批准的快照位于：

```text
C#/TopToDown/TopToDown.Tests/Baselines/RepresentativeBusbarPlan.txt
```

快照固定以下结果：

- 3 台漏保及其额定电流、ABC/N 铜排规格和单双排方式；
- A、B、C、N 四根汇流排的位置、长度和搭接孔；
- 28 根铜排的名称、规格、逻辑中心线和钣金草图线；
- 每根铜排的安装孔位置、孔径和接触面；
- 30 个汇流排连接点的螺栓规格、夹紧厚度和露扣长度。

普通测试命令：

```powershell
dotnet test .\C#\TopToDown\TopToDown.Tests\TopToDown.Tests.csproj
```

只有业务结果被有意修改并完成 SolidWorks 验证后，才允许批准新快照：

```powershell
$env:BUSBAR_APPROVE_BASELINE = '1'
dotnet test .\C#\TopToDown\TopToDown.Tests\TopToDown.Tests.csproj --filter "FullyQualifiedName~BusbarPlanBaselineTests"
Remove-Item Env:BUSBAR_APPROVE_BASELINE
dotnet test .\C#\TopToDown\TopToDown.Tests\TopToDown.Tests.csproj
```

批准操作会重写快照，因此不能把“更新快照”当成修复测试的方法。必须先审查文本差异并确认变化符合工程规则。

## 2. SolidWorks 验收装配体

不要直接把日常使用中的 `APITest.SLDASM` 当成可反复覆盖的测试文件。开始架构迁移前，应复制一套专用验收装配体及其引用零件，并确保测试生成目录与生产模型目录隔离。

验收装配体需要保留两种状态：

1. `Empty`：设备和参考点完整，但不包含程序生成的铜排；
2. `Generated`：已经通过当前版本完整生成和几何校验。

SolidWorks 二进制文件包含内部标识和保存信息，不能通过文件哈希判断建模是否一致。

## 3. 每阶段执行顺序

### 3.1 纯规划验证

```powershell
dotnet test .\C#\TopToDown\TopToDown.Tests\TopToDown.Tests.csproj
```

要求：全部测试通过，`RepresentativeAssemblyPlanMatchesApprovedBaseline` 不能被跳过。

### 3.2 生成前预检

在 SolidWorks 中打开专用验收装配体，然后运行：

```powershell
.\C#\TopToDown\TopToDown\bin\Debug\TopToDown.exe --validate
```

要求：`errors=0, warnings=0`，装配体不被修改。

### 3.3 定向建模冒烟测试

至少分别生成并检查：

- 一根主排：斜双孔、钣金路径和汇流排搭接；
- 一根 ABC 汇流排：主排斜双孔和分支直双孔全部贯穿；
- 一组 ABC 双排分支排：上下两根贴合汇流排且互不穿模；
- 一根 N 单排分支排：规格和搭接逻辑不受 ABC 双排规则影响。

可使用 `--only=铜排名称` 限定生成范围。定向建模通过后才能进行完整生成。

### 3.4 完整生成

打开 `Empty` 验收装配体，不带参数运行 `TopToDown.exe`。

预期组成：

```text
ABC：3 x（1 主排 + 1 汇流排 + 6 双排分支排）= 24
N：  1 汇流排 + 3 单排分支排                 = 4
总计：                                               28
```

要求：

- 28 个铜排零件全部生成、保存并装入装配体；
- 实体几何校验 `errors=0, warnings=0`；
- 旧铜排只在新零件全部成功后替换；
- 生产报表成功输出。

### 3.5 已生成模型只读校验

```powershell
.\C#\TopToDown\TopToDown\bin\Debug\TopToDown.exe --verify-geometry
```

要求：`errors=0, warnings=0`，装配体和零件不被修改。

## 4. 人工几何检查点

- 主排、汇流排、分支排的厚度和宽度正确；
- ABC 汇流排间距和 N 汇流排位置与基线模型一致；
- 双排夹接的上、下分支排分别贴住汇流排两侧；
- 外侧上排的 Z 向避让斜段存在，起点和终点没有漂移；
- 直双孔和斜双孔均完全贯穿，孔中心与搭接排一致；
- N 排仍为单排，不被 ABC 双排逻辑影响；
- 装配体中没有同名旧铜排残留或重复组件。

## 5. 阶段通过标准

一个迁移阶段只有同时满足以下条件才算完成：

1. Debug/Release 编译通过；
2. 自动化测试和批准快照通过；
3. 生成前预检通过；
4. 定向建模通过；
5. 完整 28 件生成、装配和几何校验通过；
6. 人工几何检查完成；
7. 生产报表内容正确；
8. 没有改动或覆盖非测试用途的装配体。

只编译成功不能作为架构迁移完成的依据。
