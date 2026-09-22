# GameAndLLM 生产级落地总案 (v2.0 终局版)

> **版本**：v2.0 (全量审计与工业级防御版)  
> **生效时间**：2026-09-18  
> **基准决策**：D-01 ~ D-27 全量闭环  
> **对齐规格**：`BATTLE_CORE v2.0`、`AFFIX_AND_SYNERGY`、`TACTICS`、`WORLD_SETTING`  
> **技术栈路线**：纯 .NET 8 / C# 12 确定性无头内核 + Godot 4.x (.NET) 渲染表现层

---

## 目录
1. [系统拓扑与工程规范](#一系统拓扑与工程规范)
2. [第一部分：底层工业级防线与待实施清单 (WBS)](#二第一部分底层工业级防线与待实施清单-wbs)
   - [2.1 七大工程防线深度落地规格](#21-七大工程防线深度落地规格)
   - [2.2 WBS 具体任务实施分解矩阵 (共 36 项)](#22-wbs-具体任务实施分解矩阵-共-36-项)
3. [第二部分：工程排期计划 (5 周 Sprint 与门禁)](#三第二部分工程排期计划-5-周-sprint-与门禁)
   - [3.1 各冲刺周期目标与严格门禁准则](#31-各冲刺周期目标与严格门禁准则)
4. [第三部分：全量数字资产明细与量产 SOP](#四第三部分全量数字资产明细与量产-sop)
   - [4.1 角色素体资产表 (首发 24 款)](#41-角色素体资产表-首发-24-款)
   - [4.2 图标、徽章与场景资产明细账](#42-图标徽章与场景资产明细账)
   - [4.3 AI 辅助数字资产工业化量产 SOP](#43-ai-辅助数字资产工业化量产-sop)

---

## 一、系统拓扑与工程规范

### 1.1 解决方案目录树

```
GameAndLLM.sln
├── 📂 src/
│   └── 📂 GameCore/                        # 纯 .NET 8 类库 (零 Godot 依赖，可独立无头运行)
│       ├── Battle/                         # 确定性战斗内核
│       │   ├── Model/                      # UnitSnapshot, SkillDef, TacticsTuple (只读不可变)
│       │   ├── Time/                       # FixedTickManager, StepClock (60Hz 严格定频)
│       │   ├── Targeting/                  # TargetResolver (含目标锁定黏性，消除震荡)
│       │   ├── Pipeline/                   # 8步伤害管线, 独立双治疗乘区
│       │   ├── Math/                       # 整数千分比/定点数, 分段双曲软钳制
│       │   ├── Extensions/                 # 第21节 10 大扩展机制 (护盾/斩杀/无敌等)
│       │   ├── Interceptors/               # 四阶权能拦截器按序短路调度
│       │   ├── Tactics/                    # Gambit 数据驱动四元组求值器
│       │   └── Events/                     # 零堆分配 (Zero-GC) 事件结构体与双缓冲环形队列
│       ├── Rogue/                          # 单局肉鸽状态机
│       │   ├── Map/                        # 三轨地图拓扑生成器 (基于种子 Rng 绝对可复现)
│       │   ├── Inventory/                  # SocketManager (核心槽恒2/总槽3~6), 15格背包, 精炼
│       │   ├── Synergy/                    # SynergyEvaluator (6职业2/4/6+血脉2/4), FusionEngine(12超武)
│       │   ├── State/                      # RogueRunState, D-09 重伤战损流转与阶梯救治
│       │   └── EventData/                  # EventDef 静态异象事件解析器
│       └── Meta/                           # 局外元养成域
│           └── State/                      # VesselRoster, EchoPoints, LegacySeal
├── 📂 tools/
│   └── 📂 GameCore.Validator/              # 离线数据校验 CLI (JSON Schema 静态扫描网关)
│       ├── Program.cs
│       └── Rules/                          # 词条ID闭环、超武配方存在性、数值越界检测
├── 📂 tests/
│   └── 📂 GameCore.Tests/                  # xUnit 高性能并发测试套件
│       ├── BattleCoreTests/                # 伤害管线数学断言、护甲K线性、软钳制、控制递减
│       ├── MonteCarloSimulator/            # 多核并行压测 (10,000 / 100,000 场无头对局)
│       ├── DataIntegrityTests/             # 配置表合规性自动化拦截
│       └── DeterminismTests/               # 跨平台 (x86/ARM) 逐帧状态哈希强一致性回归
└── 📂 client/
    └── 📂 GodotApp/                        # Godot 4.x (.NET 8) 客户端工程 (事件消费者)
        ├── project.godot
        ├── Scenes/                         # 场景树 (Battlefield2x5, RogueMapView, GambitSetup)
        ├── Controllers/                    # 驱动控制器 (BattleViewController, 插值累加器)
        └── Shaders/                        # CanvasItemShader (CRT扫描线, 水墨剑气, 受击闪白)
```

### 1.2 核心开发军规 (Hard Architectural Rules)
1. **纯 C# 物理隔离**：`GameCore` 项目严禁添加任何对 `Godot` 命名空间的引用，违者视为架构违规。
2. **Zero-GC（零堆内存分配）军规**：在每秒 60 次的战斗循环中，禁止在热路径（Hot-Path）中执行任何 `new` 操作，所有高频数据必须使用 `readonly record struct` 或栈上内存。
3. **确定性数学契约**：禁止在核心战斗计算中依赖平台相关的浮点数计算，一律遵循定点数/整数千分比（Integer Permille）规范，保证跨端重放哈希一致。

---

## 二、第一部分：底层工业级防线与待实施清单 (WBS)

### 2.1 七大工程防线深度落地规格

#### 防线 1：Zero-GC（零堆内存分配）分发管线
- **值类型事件结构体**：
  ```csharp
  // 严禁声明为 class，全部采用栈分配的 readonly record struct
  public readonly record struct DamageEvent(
      int SourceUnitId,
      int TargetUnitId,
      int FinalDamage,
      DamageType Type,
      DamageFlags Flags, // IsCrit, IsExecute, IsReflected
      int ShieldAbsorbed,
      int OverkillDamage
  );
  ```
- **双缓冲环形队列（Double-Buffered Ring Buffer）**：
  ```csharp
  public sealed class BattleEventQueue
  {
      private readonly DamageEvent[] _bufferA = new DamageEvent[2048];
      private readonly DamageEvent[] _bufferB = new DamageEvent[2048];
      private int _countA = 0;
      private bool _usingA = true;

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void Publish(in DamageEvent evt)
      {
          // 环形覆盖或定长写入，无堆内存分配
          if (_usingA && _countA < _bufferA.Length) _bufferA[_countA++] = evt;
      }

      // Godot 每帧调用：零拷贝切片导出
      public ReadOnlySpan<DamageEvent> ConsumeFrameEvents()
      {
          _usingA = !_usingA;
          var span = new ReadOnlySpan<DamageEvent>(_usingA ? _bufferB : _bufferA, 0, _countA);
          _countA = 0;
          return span;
      }
  }
  ```

#### 防线 2：整数千分比确定性数学模型 (Integer Permille)
- **基准常数**：`public const int FP_ONE = 1000;`（即 100% = 1000）。
- **乘除规范**：
  - `Mul(int a, int b) => (int)(((long)a * b + 500) / FP_ONE);`
  - `Div(int a, int b) => (int)(((long)a * FP_ONE) / b);`
- **护甲减免定点化**：
  $$K = 800 + 20 \times \text{Level}$$
  $$\text{DamageFactor} = \frac{K \times 1000}{K + \text{Armor}}$$
- **分段双曲软钳制 (Soft-Cap)**：
  ```csharp
  public static int SoftCapPermille(int raw, int soft, int cap)
  {
      if (raw <= soft) return raw;
      long num = (long)(raw - soft) * (cap - soft);
      long den = (raw - soft) + (cap - soft);
      return soft + (int)(num / den);
  }
  ```

#### 防线 3：配置数据离线校验网关 (GameCore.Validator)
- **Schema 强约束**：在 `res://Data/` 下建立 `.schema.json` 文件（`vessels.schema.json`, `affixes.schema.json` 等）。
- **静态扫描规则集（编译前与 CI 自动化执行）**：
  1. `CheckBrokenForeignKeys()`：超武配方引用的 `affix_a` 和 `affix_b` 必须存在于词条库中；
  2. `CheckEnumConsistency()`：素体职业必须严格匹配 6 大职业枚举，严禁历史残留词；
  3. `CheckNumericBounds()`：所有免伤、暴击、攻速词条必须符合系统软钳制上限阈值。

#### 防线 4：Gambit 战术索敌震荡消除（目标锁定黏性）
- **索敌黏性规则**：
  ```csharp
  public sealed class TargetLockTracker
  {
      public int CurrentTargetId { get; private set; } = -1;
      public int LockedTicksRemaining { get; private set; } = 0;
      private const int MIN_LOCK_TICKS = 60; // 强制保持 1.0 秒 (60 帧)

      public bool ShouldRetarget(UnitSnapshot self, BattleContext ctx)
      {
          if (CurrentTargetId == -1) return true;
          var target = ctx.FindUnit(CurrentTargetId);
          if (target == null || target.Hp <= 0) return true; // 目标死亡，立即重选
          if (target.HasStatus(StatusType.Stealth)) return true; // 目标进入隐匿，强制丢失
          if (self.HasStatus(StatusType.Taunted)) return true; // 自身被嘲讽，强制转向
          if (--LockedTicksRemaining <= 0) return true; // 锁定时间耗尽，允许评估更优解
          return false; // 处于黏性锁定中，禁止跳跃索敌
      }

      public void LockTarget(int targetId)
      {
          CurrentTargetId = targetId;
          LockedTicksRemaining = MIN_LOCK_TICKS;
      }
  }
  ```

#### 防线 5：多核并行蒙特卡洛平衡模拟器
- **并发分片调度**：
  ```csharp
  public static BenchmarkReport RunMonteCarlo(int totalMatches = 100000)
  {
      var results = new ConcurrentBag<BattleResult>();
      Parallel.For(0, totalMatches, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
      {
          var sim = new BattleSim(RngSeed: (uint)(1000000 + i));
          results.Add(sim.RunToCompletion());
      });
      return BenchmarkReport.Aggregate(results);
  }
  ```
- **质量警报门禁**：
  - 超时平局率 `DrawRate > 1.0%` &rarr; 触发【肉盾输出不足/拖回血】严重警报；
  - 战斗期望时长 P90 &gt; 65 秒 &rarr; 触发战斗拖沓警报；
  - 六大职业大羁绊胜率落在 `[35%, 65%]` 之外 &rarr; 触发流派失衡警报。

#### 防线 6：表现层定频累加器与快照平滑插值
- **Godot 表现层驱动模板**：
  ```csharp
  public partial class BattleViewController : Node2D
  {
      private const double FIXED_DT = 1.0 / 60.0;
      private double _accumulator = 0.0;
      private BattleSim _sim;

      public override void _Process(double delta)
      {
          // 支持倍速控制 (1x / 2x / 4x)
          double timeScale = GameSettings.BattleSpeed;
          _accumulator += delta * timeScale;

          while (_accumulator >= FIXED_DT)
          {
              _sim.Step();
              _accumulator -= FIXED_DT;
          }

          // 核心平滑：利用剩余未整除的时间 alpha，对精灵位置与血条缓冲做插值渲染
          float alpha = (float)(_accumulator / FIXED_DT);
          RenderInterpolatedState(alpha);
      }
  }
  ```

---

### 2.2 WBS 具体任务实施分解矩阵 (共 36 项)

| 编号 | 模块分类 | 实施任务名称 | 核心类 / 文件路径 | 验收标准 / 交付物 |
|---|---|---|---|---|
| **L1-01** | 内核核心 | 实体数据与不可变模型 | `GameCore/Battle/Model/UnitSnapshot.cs` | 包含动态资源、属性字段、不可变构造 |
| **L1-02** | 内核核心 | 60Hz 严格定频主循环 | `GameCore/Battle/BattleSim.cs` | 90s 限时、胜负判定、纯内存极速执行 |
| **L1-03** | 内核数学 | 整数千分比数学工具库 | `GameCore/Battle/Math/CombatMath.cs` | 乘除定点化、分段双曲软钳制、DOT计算 |
| **L1-04** | 内核索敌 | 目标解析与黏性跟踪器 | `GameCore/Battle/Targeting/TargetResolver.cs` | 7 种索敌模式、隐匿/嘲讽优先、1秒锁定 |
| **L1-05** | 内核管线 | 8 步伤害计算流水线 | `GameCore/Battle/Pipeline/DamagePipeline.cs` | 护甲K(Lv)、穿透率Pen、元素1.67倍率 |
| **L1-06** | 内核管线 | 独立双乘区治疗流水线 | `GameCore/Battle/Pipeline/HealPipeline.cs` | 施法侧 Done × 目标侧 Received 独立计算 |
| **L1-07** | 内核机制 | 第21节 10 大扩展机制 | `GameCore/Battle/Extensions/` | 护盾吸收、直接伤害12%斩杀、5s控制递减 |
| **L1-08** | 内核权能 | 四阶拦截器排序调度器 | `GameCore/Battle/Interceptors/` | Level降序→UnitId升序，首个true短路 |
| **L1-09** | 内核策略 | Gambit 数据驱动求值器 | `GameCore/Battle/Tactics/TacticsEvaluator.cs` | 16 条基础指令库匹配、动态RageCap(100↔150) |
| **L1-10** | 内核事件 | 零堆分配双缓冲事件队列 | `GameCore/Battle/Events/BattleEventQueue.cs` | readonly record struct 栈分发，0 GC 分配 |
| **L2-01** | 单局肉鸽 | 三轨地图拓扑生成器 | `GameCore/Rogue/Map/RogueMapGenerator.cs` | 单局1世界3层18步，25%裂隙，种子可复现 |
| **L2-02** | 单局构筑 | D-25 插槽服务与背包 | `GameCore/Rogue/Inventory/SocketManager.cs` | 核心槽恒为2/总槽3~6，背包15格，精炼逻辑 |
| **L2-03** | 单局构筑 | 羁绊换算引擎 | `GameCore/Rogue/Synergy/SynergyEvaluator.cs` | 6 职业(2/4/6) + 双血脉(2/4) Modifier生成 |
| **L2-04** | 单局构筑 | 12 款跨界超武融合机 | `GameCore/Rogue/Synergy/FusionEngine.cs` | 满阶配对检测、超武生成、退还1核心槽 |
| **L2-05** | 单局战损 | D-09 重伤战损流转状态机 | `GameCore/Rogue/State/InjuryService.cs` | 血量跨节点继承，阵亡转重伤，阶梯救治费 |
| **L2-06** | 单局事件 | 确定性静态异象事件流 | `GameCore/Rogue/EventData/EventManager.cs` | 静态 JSON 解析，根据种子派发选项与奖励 |
| **L2-07** | 局外元养成 | 轮回点数与传家宝封印 | `GameCore/Meta/State/MetaPersistence.cs` | 轮回结算、传承封印、征召许可逻辑 |
| **L3-01** | 表现布阵 | 2×5 自由站位棋盘场景 | `GodotApp/Scenes/Battlefield2x5.tscn` | 10 格自由放置，后排 0.85 缩放与 Z-Index |
| **L3-02** | 表现事件 | 战局事件消费与飘字池 | `GodotApp/Controllers/BattleViewController.cs` | 读取 RingBuffer 驱动 Tween 飘字与闪白 |
| **L3-03** | 表现插值 | 定频累加器与运动插值 | `GodotApp/Controllers/FrameAccumulator.cs` | 消除变频显示器微抖动，保证丝滑观感 |
| **L3-04** | 表现战术 | Gambit 战前配置面板 | `GodotApp/UI/GambitSetupView.tscn` | 2 槽战术下拉选择器与规则可视化提示 |
| **L3-05** | 表现构筑 | 插槽镶嵌与超武动效 UI | `GodotApp/UI/SocketInventoryView.tscn` | 拖拽词条卡扣、羁绊点亮徽章、超武合成粒子 |
| **L3-06** | 表现地图 | 三轨地图视差连线视图 | `GodotApp/Scenes/RogueMapView.tscn` | 渲染 18 步节点、分支选择高亮、路径推进 |
| **L3-07** | 表现控制 | 多倍速与系统顶栏控制器 | `GodotApp/UI/TopBarController.cs` | 1x/2x/4x 步频切换、种子复制与暂停放弃 |
| **T-01** | 工具链 | JSON Schema 规则集 | `tools/GameCore.Validator/Schemas/` | 5 份标准 schema 文件及字段断言 |
| **T-02** | 工具链 | 数据完整性扫描 CLI | `tools/GameCore.Validator/Program.cs` | 外键引用、枚举拼写、数值越界自动化扫描 |
| **TEST-01**| 测试回归 | 伤害与治疗管线数学单测 | `tests/GameCore.Tests/PipelineMathTests.cs` | 100% 覆盖护甲、软钳制、混合DOT断言 |
| **TEST-02**| 测试回归 | 10,000 场蒙特卡洛压测 | `tests/GameCore.Tests/MonteCarloTests.cs` | 验证附录 C-3 人均技能释放 &ge; 2 次/场 |
| **TEST-03**| 测试回归 | 跨平台逐帧状态哈希回归 | `tests/GameCore.Tests/DeterminismTests.cs` | 校验多倍速下事件哈希逐帧完全一致 |
| **TEST-04**| 测试回归 | 配置表完整性自动化门禁 | `tests/GameCore.Tests/DataIntegrityTests.cs` | 编译期执行静态数据扫描，阻断脏数据 |

---

## 三、第二部分：工程排期计划 (5 周 Sprint 与门禁)

```
[Sprint 1: 内核防线与数学闭环] ──── 门禁 G1 (万场无头测试 & Zero-GC 验证)
               │
[Sprint 2: 肉鸽拓扑与构筑超武] ──── 门禁 G2 (单局 18 步全流转与重伤经济验证)
               │
[Sprint 3: Godot 表现层与 MVP] ──── 门禁 G3 (首场图形自走棋自由布阵实机闭环)
               │
[Sprint 4: 双世界实装与平衡调校] ── 门禁 G4 (10 万场对局蒙特卡洛平衡收敛)
               │
[Sprint 5: 美术音效与封包发布] ──── 门禁 G5 (双端 60FPS 秒开独立 Demo)
```

### 3.1 各冲刺周期目标与严格门禁准则

#### Sprint 1 (第 1 周)：纯 C# 确定性无头内核 (L1)
- **重点目标**：完成 L1-01 至 L1-10，建立 Zero-GC 事件环形队列与定点数数学工具。
- **质量门禁 G1**：
  1. `dotnet test` 数学断言 100% 通过；
  2. 10,000 场无头蒙特卡洛模拟运行耗时 &le; 3 秒（8核并行）；
  3. 内存检测工具验证：单场战斗运行期间 **0 GC Gen 0/1/2 堆内存分配**；
  4. 人均技能释放（战技+大招）&ge; 2.0 次/场。

#### Sprint 2 (第 2 周)：单局肉鸽状态机、插槽与超武 (L2)
- **重点目标**：完成 L2-01 至 L2-07，闭环 D-25 插槽拓扑与 D-09 战损流转状态机。
- **质量门禁 G2**：
  1. 无头自动跑通单局 1 世界 3 层 18 步全流程，不发生死循环或未捕获异常；
  2. 验证核心槽恒为 2，职业(6)+血脉(4) 恰好用满 10 个核心槽；
  3. 12 款超武配方检测合成率达标，合成后稳定腾退插槽。

#### Sprint 3 (第 3 周)：Godot 4.x 表现层打通与 MVP 原型 (L3)
- **重点目标**：完成 L3-01 至 L3-07，建立 2×5 自由棋盘与定频累加器插值。
- **质量门禁 G3**：
  1. 在 Godot 客户端完成首场可视化自走棋实机对战；
  2. 后排角色自动以 0.85 缩放对齐，无严重视觉穿模与遮挡错乱；
  3. 1x / 2x / 4x 倍速切换平滑，飞剑与伤害飘字无锯齿微抖动。

#### Sprint 4 (第 4 周)：双世界内容实装与数值平配
- **重点目标**：录入 24 款素体、62 枚词条、12 款超武与 30 篇异象 JSON。
- **质量门禁 G4**：
  1. 离线校验 CLI 扫描数据 0 警告 0 报错；
  2. 运行 100,000 场无头蒙特卡洛模拟，6 大职业构筑胜率严格收敛在 `[35%, 65%]`；
  3. 超时平局率 &le; 0.8%。

#### Sprint 5 (第 5 周)：美术音效注入、系统打磨与封包
- **重点目标**：注入全套 24 款素体立绘、Shader 特效、背景与打击音效；导出跨平台包体。
- **终局发布门禁 G5**：
  1. PC (Windows x64) 与 Mobile (Android) 脱机离线秒开启动；
  2. 复杂战局（满屏超武飞弹）实测帧率稳定 &ge; 60 FPS；
  3. 种子复制重放哈希一致率 100%。

---

## 四、第三部分：全量数字资产明细与量产 SOP

### 4.1 角色素体资产表 (首发 24 款：蜀山 12 + 赛博 12)

```
[蜀山仙界 12 素体]                 [赛博蜂巢 12 素体]
├── 守卫: 苍岩剑壁 (L1)            ├── 守卫: 钛金防暴机兵 (L1)
├── 守卫: 镇岳道尊 (L4)            ├── 守卫: 磁暴力场机神 (L4)
├── 强袭: 裂风剑客 (L2)            ├── 强袭: 纳米武士·斩牙 (L2)
├── 强袭: 惊鸿剑狂 (L3)            ├── 强袭: 热能链锯屠夫 (L3)
├── 秘术: 紫霄真君 (L3)            ├── 秘术: 神经电弧过载者 (L3)
├── 秘术: 丹青画仙 (L1)            ├── 秘术: 量子网络黑客 (L1)
├── 游猎: 穿云剑侠 (L2)            ├── 游猎: 磁轨重炮赏金手 (L2)
├── 游猎: 逐日神弩 (L3)            ├── 游猎: 光子无人机哨兵 (L3)
├── 暗影: 无相剑影 (L3)            ├── 暗影: 光学隐形特工 (L4)
├── 暗影: 谪仙绝影 (L4)            ├── 暗影: 单分子线切裂者 (L2)
├── 支援: 灵台仙姑 (L4)            ├── 支援: 生化急救无人机 (L1)
└── 支援: 妙手丹童 (L1)            └── 支援: 战术中继纳米站 (L3)
```

- **美术规格**：512×768 WebP 格式（RGBA 8-bit），背景全透明。
- **必要切片与状态**：
  1. 卡面半身像（256×384 战前与上阵槽）
  2. 待机轻微呼吸浮动（Idle）
  3. 技能释放瞬态高亮（Cast）
  4. 受击红晕闪烁（Hit）
  5. 阵亡灰度重伤态（Down）

### 4.2 图标、徽章与场景资产明细账
- **徽章与流派图标 (8 枚)**：
  - 守卫/强袭/秘术/游猎/暗影/支援（6 大职业，128×128 矢量发光）
  - 蜀山断裂仙界纹章、赛博霓虹蜂巢芯片纹章（2 大血脉，128×128 矢量发光）
- **词条卡牌图标 (50 枚)**：
  - 32 枚基础职业词条（96×96 方形图标，支持 1/2/3 阶铜银金镶边）
  - 18 枚双世界血脉词条（96×96 方形图标）
- **概念级跨界超武 (12 款)**：
  - 12 款带流光彩色边框的超武大图标（128×128）
  - 12 款半透明剪影问号态图标（未解锁状态）
- **战场背景图 (3 幅大画)**：
  - `bg_shushan_summit.webp`：1920×1080 倒悬峰与青白剑冢（双层视差）
  - `bg_cyber_neon_slum.webp`：1920×1080 雨夜积水霓虹与摩天巨屏（双层视差）
  - `bg_void_rift_arena.webp`：1920×1080 虚空破碎悬石与数据乱流
- **着色器 (3 套 Shader)**：
  - `cyber_scanline_chromatic.gdshader`（赛博屏幕扫描线与色散）
  - `shushan_ink_stroke.gdshader`（水墨拖尾与剑气轮廓流光）
  - `combat_flash_hit.gdshader`（全屏受击闪白与极低开销顿红）

---

### 4.3 AI 辅助数字资产工业化量产 SOP

为了彻底规避独立开发团队在美术与音频上的“产能断崖”，制定标准化工业级生成管线：

#### 阶段 A：角色立绘量产流程 (ComfyUI / Midjourney)
1. **基座模型与 LoRA 固化**：
   - 蜀山世界：选用 SDXL + 国风修真古画/仙侠水墨特定 LoRA，权重锁定 0.75。
   - 赛博世界：选用 SDXL + Cyberpunk Concept Art LoRA，权重锁定 0.80。
2. **Prompt 模板规范（严格保持光影与画风统一）**：
   - 基础 Prompt 结构：`[Subject], [Class Role], standing front-view pose, official concept art, clean background, sharp focus, masterpiece, cinematic lighting, transparent background compatible`。
   - 统一 Seed 分段分配：蜀山系列 `Seed: 10001 ~ 10012`；赛博系列 `Seed: 20001 ~ 20012`。
3. **自动化后处理批处理脚本 (Python)**：
   - 编写 `tools/AssetPipeline/process_sprites.py`：
     - 利用 `rembg` 库自动抠除纯色背景；
     - 自动裁剪居中并强制缩放至标准 512×768；
     - 导出为高质量 WebP（Quality 90），单张体积压缩至 120KB 以内。

#### 阶段 B：词条与超武图标量产流程
1. **提示词公式**：
   - `[Affix Name/Object], vector game icon, RPG inventory style, clean dark slate background, stylized illustration, isometric 3d icon, vibrant glow --no realistic photo, complex background`。
2. **自动化切片加框**：
   - 脚本自动将生成的中心物品贴合进 `border_tier_1.png` ~ `border_tier_3.png` 的 96×96 边框槽位中，一键输出 62 枚成品词条切片。

#### 阶段 C：音频音效量产流程
1. **BGM 生成**：利用 Suno 设定纯器乐（Instrumental）：
   - 蜀山：《Ethereal Guqin, Taoist flute, ambient wind chime, tragic war drum, meditative fight》
   - 赛博：《Aggressive Dark Synthwave, Cyberpunk combat, heavy distorted bass, fast paced BPM 130》
   - 经由 Audacity 裁剪无缝循环点（Loop Points），导出 44.1kHz OGG。
2. **音效批量获取与调教**：
   - 从开源商用库（Freesound CC0 / Kenney Audio）批量拉取短促打击音；
   - 运行批处理脚本统一定位响度为 `-14 LUFS`，斩杀/暴击提示音设置在 `-10 LUFS`，彻底消除忽大忽小的听觉灾难。
