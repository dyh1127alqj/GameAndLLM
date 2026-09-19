# GameAndLLM 生产级落地总案：待实施清单 · 工程计划 · 数字资产清单

> **文档状态**：✅ **正式定案** ｜ **当前版本**：v1.0  
> **基准决议**：全面对齐 [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md)（D-01 ~ D-27 全部决议）、[BATTLE_CORE.md](BATTLE_CORE.md) v2.0、[AFFIX_AND_SYNERGY.md](AFFIX_AND_SYNERGY.md)、[TACTICS.md](TACTICS.md)、[WORLD_SETTING.md](WORLD_SETTING.md)  
> **核心定位**：本项目名为 `GameAndLLM`，意为**「尝试使用 LLM 作为研发辅助工具与工程 Agent 来开发游戏」**；游戏本体为 **100% 纯单机、离线、确定性**的自走棋肉鸽卡牌游戏，技术栈为 **纯 .NET 8 / C# 确定性无头内核 + Godot 4.x (.NET) 表现层**。

---

## 目录
1. [工程解决方案拓扑 (Solution Architecture)](#一工程解决方案拓扑-solution-architecture)
2. [完整待实施工程清单 (WBS & Implementation Checklist)](#二完整待实施工程清单-wbs--implementation-checklist)
   - [2.1 确定性无头战斗内核 (GameCore.Battle)](#21-确定性无头战斗内核-gamecorebattle)
   - [2.2 单局肉鸽状态机与构筑服务 (GameCore.Rogue)](#22-单局肉鸽状态机与构筑服务-gamecorerogue)
   - [2.3 Godot 4.x 表现层驱动 (GodotApp)](#23-godot-4x-表现层驱动-godotapp)
   - [2.4 自动化单测与蒙特卡洛验证 (GameCore.Tests)](#24-自动化单测与蒙特卡洛验证-gamecoretests)
3. [工程排期计划与阶段门禁 (5-Week Roadmap & Quality Gates)](#三工程排期计划与阶段门禁-5-week-roadmap--quality-gates)
   - [3.1 Sprint 1 (Week 1)：纯 C# 战斗内核闭环 (门禁 G1)](#31-sprint-1-week-1纯-c-战斗内核闭环-门禁-g1)
   - [3.2 Sprint 2 (Week 2)：肉鸽状态机、插槽与超武 (门禁 G2)](#32-sprint-2-week-2肉鸽状态机插槽与超武-门禁-g2)
   - [3.3 Sprint 3 (Week 3)：Godot 4.x 表现层与 MVP 闭环 (门禁 G3)](#33-sprint-3-week-3godot-4x-表现层与-mvp-闭环-门禁-g3)
   - [3.4 Sprint 4 (Week 4)：双世界内容实装与数值平配 (门禁 G4)](#34-sprint-4-week-4双世界内容实装与数值平配-门禁-g4)
   - [3.5 Sprint 5 (Week 5)：美术音效注入、存档与封包发布 (门禁 G5)](#35-sprint-5-week-5美术音效注入存档与封包发布-门禁-g5)
4. [数字资产完整清单 (Digital Asset Ledger & Schemas)](#四数字资产完整清单-digital-asset-ledger--schemas)
   - [4.1 角色素体资产 (首发 24 款)](#41-角色素体资产-首发-24-款)
   - [4.2 词条卡牌、徽章与图标资产 (62 枚)](#42-词条卡牌徽章与图标资产-62-枚)
   - [4.3 场景背景与视觉特效资产 (VFX & Shader)](#43-场景背景与视觉特效资产-vfx--shader)
   - [4.4 音频与音效资产 (Audio & Sound FX)](#44-音频与音效资产-audio--sound-fx)
   - [4.5 结构化配置数据表清单 (Data Schemas)](#45-结构化配置数据表清单-data-schemas)

---

## 一、工程解决方案拓扑 (Solution Architecture)

```
GameAndLLM.sln
├── 📂 src/GameCore/                         [纯 .NET 8 类库，零 Godot 依赖]
│   ├── 📂 Battle/                          [L1 确定性战斗内核]
│   │   ├── Model/                          UnitSnapshot, SkillDef, StatusEffect, TacticsTuple
│   │   ├── Time/                           FixedTickManager (60Hz, 90s 限时)
│   │   ├── Targeting/                      TargetResolver (7种模式, 隐匿/嘲讽过滤)
│   │   ├── Pipeline/                       DamagePipeline, HealPipeline, InterceptorSorter
│   │   ├── Math/                           CombatMath (分段软钳制, 护甲线性K, 混合DOT)
│   │   ├── Extensions/                     ShieldModule, ExecutionCheck, StatusEngine (第21节10大机制)
│   │   ├── Interceptors/                   IPipelineInterceptor (四阶权能拦截器)
│   │   ├── Tactics/                        TacticsEvaluator (四元组数据驱动求值器)
│   │   └── Events/                         IBattleEventListener, BattleResult, DamageEvent
│   ├── 📂 Rogue/                           [L2 单局肉鸽状态机]
│   │   ├── Map/                            RogueMapGenerator (三轨拓扑, 跨界裂隙, 纯种子驱动)
│   │   ├── Inventory/                      SocketManager (D-25 核心槽恒为2/总槽3~6), BackpackService (15格)
│   │   ├── Synergy/                        SynergyEvaluator (六职业2/4/6 + 血脉2/4), FusionEngine (12超武)
│   │   ├── State/                          RogueRunState, InjuryService (D-09 跨节点继承与重伤经济)
│   │   └── Events/                         RogueEventManager, EventDef (静态确定性异象事件)
│   └── 📂 Meta/                            [L3 局外元养成]
│       └── VesselRoster, LegacySeal, EchoPointEconomy
├── 📂 tests/GameCore.Tests/                 [xUnit 自动化测试工程]
│   ├── BattleCoreTests/                    管线单测、软钳制验收、控制递减(5s)单测
│   ├── MonteCarloSimulator/                10,000 场无头蒙特卡洛（验证 B-7 释放次数 & B-12 速度平衡）
│   └── DeterminismReplayTests/             1x/2x/4x 多倍速事件哈希完全一致性回归
└── 📂 client/GodotApp/                      [L4 Godot 4.x .NET C# 表现层工程]
    ├── project.godot                       Godot 工程配置
    ├── Scenes/                             Battlefield2x5.tscn, RogueMapView.tscn, GambitSetup.tscn
    ├── Scripts/                            BattleViewController (IBattleEventListener 消费者)
    ├── Shaders/                            CRTScanline.gdshader, HitFlash.gdshader, InkFlow.gdshader
    └── Assets/                             Textures, Icons, Audio
```

---

## 二、完整待实施工程清单 (WBS & Implementation Checklist)

### 2.1 确定性无头战斗内核 (GameCore.Battle)

| 编号 | 模块 | 实现类 / 文件 | 核心功能与职责边界 | 关联决议 / 章节 |
|:---:|---|---|---|:---:|
| **B-01** | 实体模型 | `Model/UnitSnapshot.cs` | 不可变结构体；动态属性：`Hp`, `MaxHp`, `Mana`(0~100), `Rage`(0~100), `Gauge`(0~1.0), `Speed`, `Atk`, `Armor`, `Pen`；`Rank` 标记（Normal/Elite/Boss）。 | BATTLE_CORE 3.1 |
| **B-02** | 技能契约 | `Model/SkillDefinition.cs` | `ActiveSkill`（法力驱动）与 `InnateUltimate`（怒气驱动本命大招）；技能耗费、冷却与目标选择模式。 | D-04b, D-08c |
| **B-03** | 60Hz 时钟 | `Time/FixedTickManager.cs` | 严格每步 `FixedDeltaTime = 1/60s`；单位 ATB 推进：`Gauge += Speed × Delta`；90s 限时硬终止；倍速通过步频实现，严禁改变步长。 | D-04c, BATTLE_CORE 5.3 |
| **B-04** | 自动出手 | `BattleSim.StepActions.cs` | 当 `Gauge >= 1.0` 触发行动判定：大招 (怒气&ge;100) &gt; 战技 (法力&ge;100) &gt; 普攻；普攻回复怒气+10、战技回复怒气+20、受击回复怒气+5。 | D-04b, BATTLE_CORE 8.2 |
| **B-05** | 索敌解析 | `Targeting/TargetResolver.cs` | 实现 7 种 `TargetMode`；前排优先对位；阻断隐匿单位被 `Single` 命中；优先锁定被嘲讽目标；敌全隐匿时保底。 | BATTLE_CORE 10.1, 21.9 |
| **B-06** | 伤害管线 | `Pipeline/DamagePipeline.cs` | 8 步管线：基础威力 &rarr; 穿甲与护甲衰减 `K = 800 + 20 × Level` &rarr; 穿透率 `Pen ∈ [0,1]` &rarr; 元素 1.67 摆幅 &rarr; 伤害加深 &rarr; 伤害减免。 | D-24, BATTLE_CORE 11.2 |
| **B-07** | 治疗管线 | `Pipeline/HealPipeline.cs` | 施法方 `HealingDone` × 受击方 `HealingReceived` 独立双乘区，各自默认 1.0。 | D-12, BATTLE_CORE 11.3 |
| **B-08** | 软钳制器 | `Math/CombatMath.cs` | 分段双曲软钳制：`raw <= soft` 恒等；`raw > soft` 双曲平滑渐近 `Cap`；DOT 混合公式：`MaxHp × pct + Atk × 0.20`。 | D-13, BATTLE_CORE 6.2 |
| **B-09** | 扩展机制 | `Extensions/ShieldModule.cs` 等 | 实现第 21 节 10 大扩展：护盾扣减、吸血防循环(`IsReflected`)、按枚举序驱散、无敌拦截、12% 直接伤害斩杀、隐匿与嘲讽。 | D-27, BATTLE_CORE 21 |
| **B-10** | 拦截器管线 | `Interceptors/PipelineInterceptor.cs` | 四阶权能挂钩；按 `AuthorityLevel` 降序 &rarr; `UnitId` 升序执行；首个 true 短路退出。 | D-19, HERO_VESSEL 5.1 |
| **B-11** | Gambit 求值 | `Tactics/TacticsEvaluator.cs` | 纯函数求值 `(ConditionType, P0, ActionType, A0)` 四元组；支持首发 16 条指令库；非精英战动态调节 `RageCap 100↔150`。 | D-04d, D-27, TACTICS |
| **B-12** | 事件流发布 | `Events/BattleEventDispatcher.cs` | 实现 `IBattleEventListener` 事件抛出：`DamageEvent`, `HealEvent`, `CastEvent`, `DeathEvent`，提供纯数据快照。 | BATTLE_CORE 19 |

---

### 2.2 单局肉鸽状态机与构筑服务 (GameCore.Rogue)

| 编号 | 模块 | 实现类 / 文件 | 核心功能与职责边界 | 关联决议 / 章节 |
|:---:|---|---|---|:---:|
| **R-01** | 插槽管理 | `Inventory/SocketManager.cs` | 落实 **D-25**：核心槽恒为 2（职业与血脉共用）；总槽位 3~6 格依权能与进阶解锁；仅在安全屋/行商安全节点允许插拔。 | D-25, AFFIX 1.1 |
| **R-02** | 背包服务 | `Inventory/BackpackService.cs` | 15 格备用背包；同名同阶词条 2 合 1 精炼升阶；多余词条分解为信用点。 | D-16, D-25, ROGUE_MAP |
| **R-03** | 羁绊换算 | `Synergy/SynergyEvaluator.cs` | 严格统计核心槽 `tags`（隔离 `affinity`）；计算六职业 `(2)/(4)/(6)` 档位与双血脉 `(2)/(4)` 档位；生成运行时 `Modifier` 列表。 | D-08a, D-26, AFFIX 2 |
| **R-04** | 超武融合 | `Synergy/FusionEngine.cs` | 检测 2 枚满阶跨界词条配对；合成概念级超武并腾出 1 个核心槽；管理配方图鉴（已解锁与未解锁剪影）。 | D-15, AFFIX 4 |
| **R-05** | 三轨拓扑 | `Map/RogueMapGenerator.cs` | 生成单局 1 世界 3 层、每层 6 步的三轨网络；生成普通战斗、险恶遭遇、首领、行商、安全屋、异象与 25% 跨界裂隙节点。 | D-22, ROGUE_MAP 1 |
| **R-06** | 战损流转 | `State/InjuryService.cs` | 落实 **D-09**：血量跨战斗继承；阵亡转入【重伤】（词条不锁定，可换给替补）；安全屋花费信用点阶梯救治（50&rarr;100&rarr;180...）。 | D-09, D-09a, ROGUE_MAP |
| **R-07** | 异象事件 | `Events/RogueEventManager.cs` | 解析静态 JSON 事件表；依据 `Rng(seed, nodeId)` 确定性派发选项；执行确定性增益/扣血/掉落。 | WORLD_SETTING 8 |
| **R-08** | 单局裁判 | `State/RogueRunState.cs` | 维护单局全局状态（信用点、授权点、当前层数步数）；当可出战健康素体 &lt; 5 人且无力救治时，宣告肉鸽任务失败。 | D-09b, D-18 |

---

### 2.3 Godot 4.x 表现层驱动 (GodotApp)

| 编号 | 模块 | 场景 / 脚本 | 核心功能与职责边界 | 关联决议 / 章节 |
|:---:|---|---|---|:---:|
| **G-01** | 2×5 布阵台 | `Scenes/Battlefield2x5.tscn`<br>`UI/BattlefieldGrid.cs` | 提供 10 格自由站位拖拽放置；后排单位统一自动按 0.85 缩放；按 Y 轴实现动态 Z-Index 深度排序。 | D-07, BATTLE_CORE 4 |
| **G-02** | 事件消费器 | `Controllers/BattleViewController.cs` | 实现 `IBattleEventListener`：读取内核事件流；Tween 驱动平滑血条缓冲、伤害跳字池、受击震屏与顿帧。 | BATTLE_SYSTEM 6 |
| **G-03** | 战斗 Shader | `Shaders/HitFlash.gdshader`<br>`Shaders/CRTScanline.gdshader` | 实现受击闪白特效、赛博 CRT 扫描线全屏 Shader、蜀山水墨剑气拖尾着色器。 | WORLD_SETTING 4 |
| **G-04** | 战术配置台 | `Scenes/GambitSetup.tscn`<br>`UI/GambitSelector.cs` | 每素体 2 槽战术配置 UI；条件与动作下拉选择器；实时校验前置技能合法性。 | TACTICS 5 |
| **G-05** | 插槽与背包 | `Scenes/VesselDetailView.tscn`<br>`UI/SocketInventoryView.cs` | 核心槽/作战槽/通用槽拖拽镶嵌；15 格背包网格；羁绊徽章点亮动效；超武融合触发引导。 | AFFIX 1, 5 |
| **G-06** | 三轨大地图 | `Scenes/RogueMapView.tscn`<br>`UI/MapPathGraph.cs` | 渲染三轨 18 步节点拓扑连线；高亮当前可选步进分支；平滑镜头跟随行军推进。 | ROGUE_MAP 1 |
| **G-07** | 倍速与控制 | `UI/TopBarController.cs` | 1x / 2x / 4x 按钮组与状态持久化记忆；战斗暂停；单局种子复制到剪贴板。 | D-04c, BATTLE_CORE 5.3 |

---

### 2.4 自动化单测与蒙特卡洛验证 (GameCore.Tests)

| 编号 | 模块 | 测试套件 / 类名 | 验收指标与测试目标 | 对应风险项 |
|:---:|---|---|---|:---:|
| **T-01** | 管线数学 | `BattleCoreTests/DamagePipelineTests.cs` | 验证护甲线性公式、连续穿透率 Pen、元素 1.67 摆幅及分段软钳制计算精度（误差 &lt; 0.0001）。 | B-6, D-13 |
| **T-02** | 控制递减 | `BattleCoreTests/CrowdControlTests.cs` | 验证 5s 窗口内第 1/2/3/4 次眩晕时长严格为 100%/50%/25%/0% 免疫。 | B-9, BATTLE_CORE 14.5 |
| **T-03** | 扩展机制 | `BattleCoreTests/ExtensionMechanicsTests.cs` | 验证护盾吸收、直接伤害 &lt; 12% 斩杀、吸血防死循环标记及按枚举序驱散。 | D-27, BATTLE_CORE 21 |
| **T-04** | 无头蒙特卡洛 | `MonteCarloSimulator/BattleBalanceSimulator.cs` | 运行 10,000 场无头对战：**统计人均战技+大招释放次数 &ge; 2 次/场**；常规战时长 20~40s。 | B-7, 附录 C-3, C-4 |
| **T-05** | 速度边际 | `MonteCarloSimulator/SpeedSensitivityTests.cs` | 模拟速度属性从 100 增至 300 的边际 TTK 贡献，确认其未产生指数级统治地位。 | B-12, 附录 C-5 |
| **T-06** | 确定性重放 | `DeterminismReplayTests/SeedReplayTests.cs` | 验证相同 Seed 在 1x / 2x / 4x 倍速下产出事件哈希 100% 完全一致。 | 附录 C-1, C-2 |

---

## 三、工程排期计划与阶段门禁 (5-Week Roadmap & Quality Gates)

```
Week 1 (Sprint 1) ──▶ Week 2 (Sprint 2) ──▶ Week 3 (Sprint 3) ──▶ Week 4 (Sprint 4) ──▶ Week 5 (Sprint 5)
[纯 C# 战斗内核]       [肉鸽状态与插槽]       [Godot 表现与MVP]      [双世界数据实装]       [视听资产与发布]
      │                     │                     │                     │                     │
   门禁 G1               门禁 G2               门禁 G3               门禁 G4               门禁 G5
(单测 & 1万次无头)     (单局三轨全跑通)       (首个可玩原型)       (10万次平衡验证)       (发布候选版本)
```

### 3.1 Sprint 1 (Week 1)：纯 C# 战斗内核闭环 (门禁 G1)
- **目标**：彻底落地纯 .NET 8 战斗内核，实现数据模型、主循环、伤害管线、扩展机制与战术求值器。
- **关键路径**：B-01 &rarr; B-03 &rarr; B-06 &rarr; B-08 &rarr; B-09 &rarr; B-11 &rarr; T-01 ~ T-04。
- **阶段门禁 G1**：
  1. `GameCore.Tests` 数学单测通过率 100%；
  2. 10,000 场无头战斗蒙特卡洛模拟顺利结算，无死锁、无异常退出；
  3. 人均战技+大招释放次数 &ge; 2.0 次/场（消除 B-7 风险）。

### 3.2 Sprint 2 (Week 2)：肉鸽状态机、插槽与超武 (门禁 G2)
- **目标**：落地单局肉鸽流转服务，实现 D-25 核心槽恒为 2 拓扑、羁绊引擎、超武融合与 D-09 重伤经济。
- **关键路径**：R-01 &rarr; R-02 &rarr; R-03 &rarr; R-04 &rarr; R-05 &rarr; R-06 &rarr; R-08。
- **阶段门禁 G2**：
  1. 无头模式完整跑通单局 1 世界 3 层 18 步流转；
  2. 验证职业(6)+血脉(4) 恰好用满 10 个核心槽且正确触发 Modifier；
  3. 阵亡素体进入【重伤】、词条自由卸下、安全屋信用点救治费用递增逻辑 100% 正确。

### 3.3 Sprint 3 (Week 3)：Godot 4.x 表现层与 MVP 闭环 (门禁 G3)
- **目标**：在 Godot 4.x 中接入 `GameCore.dll`，实现 2×5 布阵、事件消费平滑动画、战术配置面板与大地图。
- **关键路径**：G-01 &rarr; G-02 &rarr; G-03 &rarr; G-04 &rarr; G-06 &rarr; G-07。
- **阶段门禁 G3**：
  1. 玩家可在 Godot 界面中拖拽 5 名素体自由放置在 2×5 棋盘（后排 0.85 缩放与遮挡正确）；
  2. 完整观战一场自动对局，平滑血条扣减、伤害跳字、打击闪白与胜利结算流畅呈现；
  3. 1x / 2x / 4x 倍速切换无卡顿且战斗结果与内核完全一致。

### 3.4 Sprint 4 (Week 4)：双世界内容实装与数值平配 (门禁 G4)
- **目标**：全量录入蜀山仙界与赛博蜂巢的 24 款素体、62 枚词条、12 款超武与 30 篇异象事件配置表。
- **关键路径**：配置表编写 &rarr; 波次组装 &rarr; 异象配置 &rarr; 100,000 场无头蒙特卡洛数值微调。
- **阶段门禁 G4**：
  1. 62 枚词条与 12 款超武的全部机制与数值在游戏中生效无阻；
  2. 100,000 场对局统计下，六大职业流派胜率收敛在 35% ~ 65% 区间，无废弃流派；
  3. 30 篇异象事件全部可触发，无数值破坏性 Bug。

### 3.5 Sprint 5 (Week 5)：美术音效注入、存档与封包发布 (门禁 G5)
- **目标**：注入 24 款素体切片立绘、战场视差背景、全套打击与系统音效，实现本地存档并封包。
- **关键路径**：美术切片配置 &rarr; 音频接入 &rarr; 本地确定性存档 &rarr; 多平台导出 (Windows x64 / Android)。
- **阶段门禁 G5**：
  1. 导出独立运行包，在断网脱机状态下秒级秒开，对局全程稳定 60 FPS；
  2. 种子复制与黏贴对局可重现完全一致的随机地图与战斗结果；
  3. 产出首个对外公开试玩版 Demo 包体。

---

## 四、数字资产完整清单 (Digital Asset Ledger & Schemas)

### 4.1 角色素体资产 (首发 24 款)

- **美术交付规格**：
  - 全身立绘：`512 × 768` 像素，WebP/PNG，透明通道背景；
  - 战场战斗卡面：`256 × 384` 像素，带职业边框与先天权能星级标识；
  - 头像图标：`128 × 128` 像素；
  - 必要状态切片：待机 (Idle)、普攻 (Attack)、受击闪红 (Hit)、大招释出 (Cast)、重伤倒地 (Down)。

#### 1. 蜀山断裂仙界 (12 款素体)
| 素体代码 | 素体中文名 | 标准职业 | 先天权能 | 本命大招 (InnateUltimate) | 初始保底词条 |
|---|---|:---:|:---:|---|---|
| `vessel_ss_guard_01` | **苍岩剑壁** | 守卫 Guardian | Level 1 | 【不动如山】全队获得 20% 最大生命护盾，持续 4 秒 | `affix_iron_guardian_lv1` |
| `vessel_ss_guard_02` | **玄龟剑傀** | 守卫 Guardian | Level 2 | 【玄冥剑罡】对自身周围造成伤害，嘲讽命中者 3 秒 | `affix_heavy_bastion_lv1` |
| `vessel_ss_striker_01` | **裂风剑客** | 强袭 Striker | Level 1 | 【疾风骤雨】对目标造成 3 次 120% 物理穿透攻击 | `affix_slash_blade_lv1` |
| `vessel_ss_striker_02` | **破军剑圣** | 强袭 Striker | Level 3 | 【天地同寿】消耗自身 15% 当前生命，造成全场 240% 绝强穿透伤害 | `affix_slash_blade_lv2` |
| `vessel_ss_mystic_01` | **紫霄引雷真君** | 秘术 Mystic | Level 2 | 【紫霄九天雷】引雷轰击敌方最高攻击者，造成 260% 元素伤害并眩晕 1.5s | `affix_element_gather_lv1` |
| `vessel_ss_mystic_02` | **焚天道人** | 秘术 Mystic | Level 3 | 【真火焚野】对敌方全排附加 10 秒真火灼烧（每跳 5% 最大生命） | `affix_element_gather_lv2` |
| `vessel_ss_ranger_01` | **穿云飞剑客** | 游猎 Ranger | Level 1 | 【穿云一击】射出一柄飞剑贯穿同列敌方，造成 180% 穿透物理伤害 | `affix_rend_barb_lv1` |
| `vessel_ss_ranger_02` | **千机散人** | 游猎 Ranger | Level 2 | 【漫天花雨】向敌方后排随机倾泻 6 柄飞剑，每柄造成 60% 伤害 | `affix_rend_barb_lv1` |
| `vessel_ss_shadow_01` | **无相剑影** | 暗影 Shadow | Level 2 | 【影杀术】突刺至最弱敌人背后，造成 220% 伤害，若斩杀则回满怒气 | `affix_shadow_stab_lv1` |
| `vessel_ss_shadow_02` | **绝情弃剑者** | 暗影 Shadow | Level 4 | 【概念级·断因果】使目标进入 3 秒孤立状态，受直接伤害提升 50% | `affix_shadow_stab_lv2` |
| `vessel_ss_support_01` | **灵台拂尘仙姑** | 支援 Support | Level 1 | 【甘霖普降】治疗全体友军 120% 攻击力生命值，驱散 1 个控制减益 | `affix_life_aid_lv1` |
| `vessel_ss_support_02` | **太清度厄真人** | 支援 Support | Level 3 | 【太极玄黄界】展开法阵，全队受直接伤害降低 30%，持续 5 秒 | `affix_life_aid_lv2` |

#### 2. 赛博霓虹蜂巢 (12 款素体)
| 素体代码 | 素体中文名 | 标准职业 | 先天权能 | 本命大招 (InnateUltimate) | 初始保底词条 |
|---|---|:---:|:---:|---|---|
| `vessel_cb_guard_01` | **钛金防暴机兵** | 守卫 Guardian | Level 1 | 【防暴矩阵】架起合金巨盾，自身减伤 50% 并反射 20% 伤害，持续 4 秒 | `affix_iron_guardian_lv1` |
| `vessel_cb_guard_02` | **力场重装守卫** | 守卫 Guardian | Level 2 | 【动能偏转屏障】为前排生成吸收 400 点伤害的电磁偏转力场 | `affix_heavy_bastion_lv1` |
| `vessel_cb_striker_01` | **纳米武士·斩牙** | 强袭 Striker | Level 2 | 【纳米分子裂解斩】直劈敌方前排，附带 10 秒分子流血（每跳 4% 最大生命） | `affix_slash_blade_lv1` |
| `vessel_cb_striker_02` | **过载狂暴机甲** | 强袭 Striker | Level 3 | 【反应堆过载】攻速暴增 100%，每次普攻附带 30% 真实伤害，持续 5 秒 | `affix_slash_blade_lv2` |
| `vessel_cb_mystic_01` | **神经电弧过载者** | 秘术 Mystic | Level 1 | 【超导电弧链】释放连环电弧弹跳 4 次，每段造成 110% 元素伤害 | `affix_element_gather_lv1` |
| `vessel_cb_mystic_02` | **量子黑客·虚空** | 秘术 Mystic | Level 4 | 【逻辑死锁崩溃】入侵敌方全体神经芯片，沉默全场 2.5 秒并清空 30 点法力 | `affix_element_gather_lv2` |
| `vessel_cb_ranger_01` | **磁轨重炮猎手** | 游猎 Ranger | Level 2 | 【超重型电磁炮】蓄力发射磁轨炮，对目标造成 280% 贯通伤害（忽视 50% 护甲） | `affix_rend_barb_lv1` |
| `vessel_cb_ranger_02` | **微型巡飞弹巢** | 游猎 Ranger | Level 3 | 【饱和蜂群轰炸】发射 8 枚微型巡飞弹随机轰击敌方全阵地 | `affix_rend_barb_lv2` |
| `vessel_cb_shadow_01` | **光学隐形人** | 暗影 Shadow | Level 3 | 【光学拟态伏杀】进入 2 秒隐匿状态，下一次攻击必定造成 250% 破甲伤害 | `affix_shadow_stab_lv1` |
| `vessel_cb_shadow_02` | **数据死神** | 暗影 Shadow | Level 4 | 【防火墙穿透背刺】瞬移至敌方后排施法者身后，造成 300% 物理暴击直接伤害 | `affix_shadow_stab_lv2` |
| `vessel_cb_support_01` | **战地急救无人机** | 支援 Support | Level 1 | 【纳米修复喷雾】为生命最低友军注射纳米针，3 秒内恢复 30% 最大生命 | `affix_life_aid_lv1` |
| `vessel_cb_support_02` | **神经共鸣枢纽** | 支援 Support | Level 2 | 【超频神经同步】立即为全体友军注入 25 点法力与 15 点怒气 | `affix_life_aid_lv1` |

---

### 4.2 词条卡牌、徽章与图标资产 (62 枚)

- **美术交付规格**：
  - 图标：`128 × 128` 像素 PNG，透明通道；
  - 边框装饰：Lv1 铸铁/青铜色；Lv2 银白合金/玄银色；Lv3 炫金/流光金色；超武专属概念级紫金炫彩呼吸光。

#### 1. 六大职业核心词条 (32 枚，涵盖 Lv1~Lv3 进阶与回归机制)
| 词条 ID | 中文名 | 归属职业 | 核心机制描述 (Lv1 / Lv2 / Lv3) | 挂钩点 |
|---|---|:---:|---|---|
| `affix_iron_guardian` | 铁甲守卫 | 守卫 | 受直接物理伤害减免 10% / 18% / 25% | `OnDamageTaken` |
| `affix_heavy_bastion` | 重装壁垒 | 守卫 | 战斗开局获得 15% / 25% / 35% 最大生命护盾 | `OnBattleStart` |
| `affix_thorn_carapace` | 荆棘反甲 | 守卫 | 反弹所受直接伤害的 15% / 25% / 35% 给攻击者 | `IsReflected` |
| `affix_slash_blade` | 斩裂重刃 | 强袭 | 普攻有 20% 概率造成 140% / 180% / 220% 破甲伤害 | `OnHit` |
| `affix_blood_craze` | 嗜血狂袭 | 强袭 | 造成物理伤害的 12% / 20% / 30% 转化为自身生命 | `OnDamageDealt` |
| `affix_berserk_surge` | **狂化涌流** | 强袭 | **残血增伤回归**：伤害根据已损生命提升 `K = 0.30 / 0.45 / 0.60` | `GetAtkMultiplier` |
| `affix_element_gather` | 元素聚能 | 秘术 | 战技造成的法术伤害提升 15% / 25% / 35% | `OnSkillCast` |
| `affix_arcane_echo` | 秘术余波 | 秘术 | 施放战技后，下次普攻附带 40% / 70% / 100% 溅射法伤 | `OnAfterAction` |
| `affix_rend_barb` | **裂伤箭镞** | 游猎 | **经典流血回归**：普攻有 15% / 20% / 25% 附加流血状态 | `OnHit` |
| `affix_swift_cascade` | **疾风连动** | 游猎 | **经典再动回归**：行动后有 15% / 22% / 30% 立即再次行动（不回能） | `RollExtraAction` |
| `affix_eagle_eye` | 鹰眼急所 | 游猎 | 攻击距离加深，对后排目标造成伤害提高 15% / 25% / 35% | `GetDamageMultiplier` |
| `affix_shadow_stab` | 孤立背刺 | 暗影 | 对战场最边缘的敌方单体造成伤害提升 20% / 35% / 50% | `GetDamageMultiplier` |
| `affix_ghost_blade` | 幽影斩杀 | 暗影 | 直接伤害命中生命值低于 12% 的目标时直接处决 | `GetExecuteThreshold` |
| `affix_life_aid` | 生命急救 | 支援 | 自身治疗效果提升 15% / 25% / 35% | `HealingDone` |
| `affix_mana_fountain`| 灵能涌泉 | 支援 | 每秒自然回复 2 / 4 / 6 点法力 (`ManaRegenPerSec`) | `ManaRegen` |

#### 2. 双世界血脉词条 (18 枚)
| 词条 ID | 中文名 | 归属世界 | 核心机制描述 (Lv1 / Lv2 / Lv3) |
|---|---|:---:|---|
| `affix_blood_ss_sword` | 万剑诀残卷 | 蜀山仙界 | 每次普攻凝聚 1 柄悬空剑气，攒满 3 柄时自动呼啸发射贯穿敌阵 |
| `affix_blood_ss_talisman`| 太清护身符 | 蜀山仙界 | 受到致死伤害时免死并无敌 1.5 秒（全场仅 1 次，战后正常结算重伤） |
| `affix_blood_ss_heart` | 养剑蕴心法 | 蜀山仙界 | 自身每持有 10 点怒气，全属性伤害提升 2% / 3.5% / 5% |
| `affix_blood_cb_implant`| 超维神经义体 | 赛博蜂巢 | 攻击充能速度提升 15% / 25% / 35%，受电磁干扰免疫 |
| `affix_blood_cb_overload`| 动力微型聚变堆 | 赛博蜂巢 | 生命低于 40% 时进入过载，攻速与攻击力暴增 40%，受伤害加深 15% |
| `affix_blood_cb_shield` | 纳米偏转力场 | 赛博蜂巢 | 每隔 8 秒自动刷新一个吸收 15% 最大生命的偏转力场护盾 |

#### 3. 概念级跨界超武 (12 款，需由满阶跨界词条合成)
| 超武 ID | 概念超武名称 | 合成素材配方 | 质变机制与概念级效果 |
|---|---|---|---|
| `weapon_super_01` | **高频纳米飞剑** | 斩裂重刃 (强袭) + 万剑诀残卷 (蜀山) | 普攻变为 3 柄纳米剑气全屏飞射，每段攻击削减受击者 10% 护甲（上限 60%）。 |
| `weapon_super_02` | **赛博金丹热能炉** | 狂化涌流 (强袭) + 动力微型聚变堆 (赛博) | 受到伤害按 30% 转化为怒气与热能，满热能时普攻产生全屏真实伤害爆炸。 |
| `weapon_super_03` | **因果律反器材狙击** | 鹰眼急所 (游猎) + 超维神经义体 (赛博) | 普攻直接跨过前排锁定敌方最大攻击力单位，造成 250% 绝对穿甲物理伤害。 |
| `weapon_super_04` | **等离子诛仙剑阵** | 元素聚能 (秘术) + 万剑诀残卷 (蜀山) | 战场中央降下等离子剑阵，敌方每秒受到 6% 混合伤害，持续 12 秒。 |
| `weapon_super_05` | **义体渡劫金身** | 重装壁垒 (守卫) + 太清护身符 (蜀山) | 致命伤害时清除负面并回复 30% 生命存活本场；战后转重伤但不计入救治涨价累计。 |
| `weapon_super_06` | **量子黑客夺舍针** | 幽影斩杀 (暗影) + 超维神经义体 (赛博) | 命中敌方时使其沉默 3 秒，且此期间其受到的所有伤害 50% 同步反弹给其队友。 |
| `weapon_super_07` | **万象剑傀蜂群** | 疾风连动 (游猎) + 动力微型聚变堆 (赛博) | 普攻裂解为三段机械飞刃连击，每段均可独立触发攻击特效与索敌。 |
| `weapon_super_08` | **无间相位折跃刃** | 孤立背刺 (暗影) + 纳米偏转力场 (赛博) | 受攻击时有 50% 概率折跃遁入虚空（完全规避伤害并眩晕攻击者 1 秒）。 |
| `weapon_super_09` | **太清微波护道钟** | 铁甲守卫 (守卫) + 太清护身符 (蜀山) | 为全队展开太清护道钟，受到的一切控制效果持续时间削减 60%。 |
| `weapon_super_10` | **真火引力坍缩弹** | 秘术余波 (秘术) + 动力微型聚变堆 (赛博) | 战技施放后生成引力黑洞，将敌方后排拉扯至中排并造成 200% 灼烧法伤。 |
| `weapon_super_11` | **灵能纳米医疗枢纽** | 生命急救 (支援) + 纳米偏转力场 (赛博) | 队伍任意成员濒死时立即为其注入 40% 生命超频护盾（全队共享冷却 10 秒）。 |
| `weapon_super_12` | **绝对反射矩阵** | 荆棘反甲 (守卫) + 纳米偏转力场 (赛博) | 受到敌方后排的一切攻击伤害削减 50%，并将吸收伤害的 25% 以能量束反弹。 |

---

### 4.3 场景背景与视觉特效资产 (VFX & Shader)

- **场景大图规格**：`1920 × 1080` 像素，分双层贴图（近景地面层 + 远景视差漂浮层），PNG/WebP。
  1. `env_shushan_battlefield`：蜀山倒悬剑冢峰（远景漂浮孤峰、云海、古老插剑残壁）；
  2. `env_cyber_battlefield`：赛博深渊雨夜霓虹街区（地面雨水积水倒影、霓虹广告牌、全息投影）；
  3. `env_void_rift`：虚空跨界裂隙竞技场（空间破裂断层、量子粒子光晕）。
- **着色器 (Godot CanvasItemShader)**：
  1. `CRTScanline.gdshader`：模拟老旧赛博监视器、行扫描线、轻微色散与暗角；
  2. `HitFlash.gdshader`：受击 0.05 秒纯白闪烁材质，配合顿帧提供拳拳到肉的打击感；
  3. `InkFlow.gdshader`：仙侠水墨挥毫扩散拖尾 Shader。
- **粒子系统 (GPUParticles2D)**：
  1. `vfx_sword_trail`：青色与金色剑气拖尾；
  2. `vfx_laser_beam`：高能蓝色电磁激光束；
  3. `vfx_shield_bubble`：半透明六边形蜂窝力场球；
  4. `vfx_slash_burst`：物理斩击红色爆裂粒子；
  5. `vfx_execute_skull`：暗影 12% 斩杀瞬发紫黑死神徽标；
  6. `vfx_level_up`：超武合成冲天光柱。

---

### 4.4 音频与音效资产 (Audio & Sound FX)

- **音频标准**：44.1kHz / 16bit，BGM 为 OGG 格式（统一 -14 LUFS，完美 Seamless Loop）；音效为无损 WAV 格式（统一 -18 LUFS）。
- **背景音乐 (BGM)**：
  1. `bgm_shushan_explore`：《断剑悲鸣》仙侠空灵古筝与低沉大提琴混合氛围音乐；
  2. `bgm_cyber_explore`：《霓虹过载》80s 复古合成器波（Synthwave）配重低音节拍；
  3. `bgm_boss_battle`：《天道仲裁》激昂交响打击乐与失真电吉他融合战歌。
- **战斗音效 (SFX)**：
  - `sfx_sword_swing_01~03`：轻重飞剑划空破风声；
  - `sfx_laser_fire_01~02`：电磁充能射击声；
  - `sfx_shield_impact`：重盾承受撞击金属沉闷声；
  - `sfx_shield_break`：力场护盾爆碎玻璃脆响；
  - `sfx_execute_hit`：断头台般的沉重处决重击音；
  - `sfx_ultimate_ready`：怒气攒满时的清脆战术高频提示音。
- **UI 与系统反馈音**：
  - `sfx_ui_click`：轻脆微动开关点击声；
  - `sfx_ui_equip`：插槽金属卡扣咔哒锁死声；
  - `sfx_ui_fuse`：超武融合能量激荡轰鸣音；
  - `sfx_ui_injury_alarm`：素体阵亡进入重伤的警报短鸣；
  - `sfx_ui_victory`：战斗胜利三颗星结算音。

---

### 4.5 结构化配置数据表清单 (Data Schemas)

所有数据配置表存放于 `GodotApp/Data/` 目录，采用纯 JSON 格式存储，与纯 C# 内核反序列化契约完全一致：

```
GodotApp/Data/
├── vessels.json         [24 款角色素体基础数值、先天权能、怒气本命大招定义]
├── affixes.json         [62 枚核心/作战/通用词条详细参数、tags 与 affinity 标签]
├── fusion_recipes.json  [12 款跨界超武合成素材配方与概念质变挂钩点]
├── tactics.json         [首发 16 条战术指令四元组枚举映射表]
├── events.json          [30 篇离线确定性位面异象剧情与决策分支表]
└── encounters.json      [双世界 45 组怪物波次、等级与阵型数据]
```

---

## 五、总结与交接说明

本规划书是 **GameAndLLM** 项目进入编码实施阶段的**最终最高指导纲领**：
1. **架构坚固**：彻底阻断“游戏运行时调用 LLM”与“逻辑强行绑死游戏引擎”两大致命设计隐患；
2. **职责分明**：纯 C# 无头内核专注 100% 确定性、数学防爆与极限速度验证；Godot 4.x 专注 2×5 棋盘交互、视差渲染与绚丽打击反馈；
3. **资产清晰**：从 24 款素体到 62 枚词条、12 款超武，所有数据字段、挂钩点与艺术标准均有据可查、零悬空。

项目随时可以按 **Sprint 1 (Week 1)** 正式启动第一行 C# 代码的编写！
