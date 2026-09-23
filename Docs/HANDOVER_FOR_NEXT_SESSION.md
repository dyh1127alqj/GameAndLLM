# GameAndLLM 跨会话研发交接备忘录 (Session Handover Document)

> **版本**：v2.0 终局版 ｜ **归档时间**：2026-09-23 ｜ **当前状态**：Sprint 1 & Sprint 2 研发与四轮审计闭环完毕，测试 25/25 全绿，工作区 Clean，与云端完全同步。

---

## 零、项目核心定调（新 Agent 必读）

1. **项目名称本义**：`GameAndLLM` 意为**「尝试使用 LLM 作为研发辅助工具与工程 Agent 来开发游戏」**。
2. **游戏本体属性**：**100% 纯单机、离线、确定性自走棋肉鸽卡牌游戏**。游戏运行时**完全不包含、不调用任何外部 LLM 服务**，无网络开销与延迟阻断。
3. **技术栈架构**：
   - **逻辑内核 (`GameCore`)**：纯 .NET 8 / C# 12 类库，**绝对禁止引用 Godot 命名空间**，保证无头高频压测、多核蒙特卡洛与种子完全可复现；
   - **表现层 (`GodotApp`)**：Godot 4.x (.NET) C# 客户端工程，作为事件消费者订阅内核事件，驱动画面渲染。

---

## 一、系统架构与核心设计规则（已拍板 27 项决策收口）

### 1. 战斗内核 (L1 - BattleCore)
- **战斗模式**：纯全自动自走棋（无手操、无拖拽、无蓄力）。
- **阵型规则**：2×5 共 10 格自由站位，彻底解除同列约束；表现层后排自动 0.85 缩放，按 Y 轴做 Z-Index 深度排序遮挡。
- **时钟与步长**：严格 60Hz 定频主时钟（$FixedDeltaTime = 1/60s$），单场限时 90 秒（5,400 Ticks），常规战期望 20~40 秒。
- **双轨技能驱动**：
  - 战技（ActiveSkill）：法力驱动，满 100 自动释放；
  - 本命大招（UltimateSkill）：素体专属固有，怒气驱动，满 100 自动释放；
  - 普攻回法 **+30**，普攻回怒 **+10**；
  - **受击回怒 +5**（守护肉盾挨打攒大招核心反馈）；
  - **受击法力退火 -10**（每次受击扣减 10 点法力，形成战术拉扯反制）。
- **ATB 充能速率**：$\Delta Gauge = (Speed \times 1000) / (60 \times 200) = Speed / 12$ 步进（$Gauge \ge 1000$ 获得行动回合）。
- **确定性定点数学与防爆护栏 (`CombatMath`)**：
  - 采用整数千分比（`FP_ONE = 1000`），彻底根除跨平台 IEEE 754 浮点尾数漂移；
  - 护甲动态等级线性衰减：$K = 800 + 20 \times TargetLevel$（受击方有效护甲 $Armor_{eff} = Armor \times (1000 - Pen) / 1000$）；
  - 护甲计算后加 1 点最低伤害保底；
  - 分段双曲软钳制算法（$raw \le soft$ 恒等保持，$raw > soft$ 双曲渐近逼近 Cap）；
  - 混合 DOT 公式：$Damage = TargetMaxHp \times pct + AttackerAtk \times 0.20$；
  - 控制递减规则：5 秒衰减窗口内受控时长按 100% → 50% → 25% → 免疫 衰减。
- **扩展机制 (第 21 节)**：
  - 护盾（ShieldHp）优先于本体血量扣除；
  - **12% 直接伤害斩杀**（仅限直接伤害，DOT 与反伤严禁触发）；
  - 伤害无敌拦截（Blocked 标记）；
  - 吸血与反伤闭环计算（`Reflected` 标记，严格防循环死锁）。
- **智能索敌与急救**：
  - 攻击索敌：60 帧（1.0 秒）目标锁定黏性追踪，支持对位最近、绝对/百分比残血、后排穿透等 7 种模式，支持隐匿豁免与受嘲讽转火；
  - 友方治疗索敌：`ResolveAllyTarget` 专属通道，不复用攻击锁定追踪器，优先急救同阵营血量百分比最低队友。
- **战术系统 (Gambit)**：
  - 纯数据驱动强类型四元组 `(ConditionType, P0, ActionType, A0)`，每素体开放 2 个战术槽。

### 2. 构筑与肉鸽状态机 (L2 - Rogue Framework)
- **D-25 矩阵插槽体系 (`SocketManager`)**：
  - Slot 0: `CoreSlot1`（核心槽 1，职业/血脉）
  - Slot 1: `CoreSlot2`（核心槽 2，职业/血脉，**核心槽全队恒为 2，不可膨胀**）
  - Slot 2: `CombatSlot1`（作战槽 1，武器/伤害机制）
  - Slot 3: `GeneralSlot1`（通用槽 1，门槛 $\ge 4$ 解锁）
  - Slot 4: `CombatSlot2`（作战槽 2，门槛 $\ge 5$ 解锁）
  - Slot 5: `SingularitySlot`（**奇点槽**，门槛 $\ge 6$ 解锁，超武独占）
  - 装卸规则：**仅在安全节点（安全屋/黑市）允许自由插拔装备**，行军中锁定 (D-14)。
- **15 格背包与 2 合 1 精炼 (`BackpackService`)**：
  - 背包容量恒定 15 格；
  - 支持 2 合 1 同名同阶精炼（Lv1+Lv1 → Lv2，Lv2+Lv2 → Lv3），合成后空出 1 格背包；
  - 支持分解换取信用点（Lv1:20, Lv2:50, Lv3:120）。
- **羁绊计算引擎 (`SynergyEvaluator`)**：
  - 严格统计出战队伍 5 人共 10 个核心槽的 `Tags`（**严格隔离技能倾向 `Affinity`，不计入件数**）；
  - 六大职业羁绊：守卫、强袭、秘术、游猎、暗影、支援，均设 `(2)/(4)/(6)` 档位，(2)/(4) 仅携带者生效，(6) 档全队质变；
  - 双世界血脉套装：蜀山断裂仙界 (2)/(4)、赛博霓虹蜂巢 (2)/(4)，刻意不对称，绝无第 6 档。
- **跨界超武融合机 (`FusionEngine`)**：
  - 注册 12 款跨界概念超武配方（如万剑诀+纳米刃 → 高频纳米飞剑）；
  - 消耗两枚满阶 Lv3 素材词条，合成 1 枚概念级超武并腾退 1 个插槽；
  - 纯无状态工具类，超武解锁状态记录在 `RogueRunState.UnlockedRecipeIds` 实例中，单局多实例物理隔离。
- **D-09 战损流转与重伤经济 (`RogueRunState`, `InjuryService`)**：
  - 血量跨战斗继承；
  - 战斗阵亡素体转【重伤】（`IsInjured = true`）移入替补席，**词条不随素体锁定**，可卸下给替补；
  - 安全屋消耗信用点救治，**救治费用严格按 50 → 100 → 180... 阶梯递增**；
  - 大名单上限软约束：出战+替补上限 10 人（`MaxRosterCapacity = 10`）；
  - 失败判定收敛：健康素体不足 5 人且无力救治判负。
- **三轨确定性地图 (`RogueMap`)**：
  - 1 世界 3 层，每层 6 步（Step 0~5），三轨网络向前推进拓扑；
  - 节点类型：普通战斗、险恶遭遇、流浪行商、安全屋、位面异象、跨界裂隙、位面首领；
  - **裂隙保底**：每层必有且至少 1 个跨界裂隙 Rift 节点；
  - 基于 `Rng(seed, floorIndex)` 绝对确定性伪随机。

---

## 二、当前工程资产与代码现状 (全量对齐)

### 1. 源码清单 (`src/GameCore/`)
```
src/GameCore/
├── Numerics/
│   └── CombatMath.cs                 // 定点数千分比、分段双曲软钳制、护甲K线性、混合DOT、控制递减
├── Model/
│   ├── Enums.cs                      // 六大职业、阵营、伤害类型、DamageFlags(含Blocked)、索敌模式
│   ├── Events.cs                     // 零 GC 栈值类型事件 (readonly record struct)
│   ├── TacticsInstruction.cs         // 纯数据驱动 Gambit 战术四元组
│   ├── SkillDefinition.cs            // 技能模型 (含 IsHeal, 别名属性与工厂)
│   └── UnitSnapshot.cs               // 单位快照 (含 CurrentGauge/ActionGauge, IsDead计算属性, IsInjured, Create工厂)
├── Targeting/
│   ├── TargetLockTracker.cs          // 60 帧 (1.0s) 目标锁定黏性追踪器
│   └── TargetResolver.cs             // 7 模式攻击索敌 + ResolveAllyTarget 友方急救索敌
├── Pipeline/
│   ├── DamagePipeline.cs             // 8 步伤害流 (无敌拦截/斩杀/护盾/受击回怒+5/退火-10/保底1点/吸血反伤防死循环)
│   └── HealPipeline.cs               // 治疗独立双乘区管线
├── Events/
│   └── BattleEventQueue.cs           // 零 GC 定长双缓冲环形事件队列
├── Battle/
│   ├── BattleContext.cs              // 战斗上下文与不可变战报 (含完整构造函数、StarRating浮点判定)
│   └── BattleSim.cs                  // 60Hz 严格定频主时钟、双轨技能调度、索敌分流与胜负仲裁
└── Rogue/
    ├── SlotType.cs                   // 插槽位掩码 (Core/Combat/General/Singularity)
    ├── AffixModel.cs                 // 词条定义与运行时实例
    ├── SocketManager.cs              // D-25 矩阵插槽管理 (0~5槽映射, 奇点槽, 安全节点校验)
    ├── BackpackService.cs            // 15 格背包与 2 合 1 精炼/分解
    ├── SynergyEvaluator.cs           // 核心槽 Tags 严格羁绊求值 (职业2/4/6 + 血脉2/4)
    ├── FusionEngine.cs               // 纯实例级超武融合机 (12款配方, 状态归属RunState)
    ├── RogueMap.cs                   // 三轨确定性地图生成 (每层Rift保底)
    └── RogueRunState.cs              // 单局状态机、重伤阶梯救治 (50->100->180) 与大名单软约束
```

### 2. 测试工程清单 (`tests/GameCore.Tests/`)
- `CombatMathTests.cs`（数学与钳制断言）
- `TargetResolverTests.cs`（攻击索敌黏性与嘲讽断言）
- `PipelineTests.cs`（伤害、斩杀、退火、治疗乘区断言）
- `BattleSimTests.cs`（单局胜负、友方急救索敌、**10,000 场无头蒙特卡洛多核并行压测**）
- `RogueFrameworkTests.cs`（D-25奇点槽、15格背包精炼、羁绊档位、超武融合、单局实例物理隔离、重伤阶梯救治、三轨地图确定性与裂隙保底）
- **当前测试结果**：**25 / 25 全部通过，0 失败，0 警告，单次全量运行仅 1 秒！**

### 3. 可执行验收程序 (`src/GameCore.Runner/`)
- 控制台实机验收对局器，支持蜀山 vs 赛博流式战况与性能看板输出。

### 4. Git 仓库与云端状态
- **远程仓库**：`https://github.com/dyh1127alqj/GameAndLLM.git`
- **本地分支**：`main`，与 `origin/main` 100% 同步。
- **Working Tree**：Clean（无任何未暂存改动或临时脏文件）。

---

## 三、新会话研发切入点：Sprint 3（Godot 4.x 表现层打通）

进入下一个 session 后，直接按照以下任务路线启动 **Sprint 3**：

### 任务目标：搭建 Godot 4.x (.NET) 工程，跑通首个带画面的 MVP 可玩对局

1. **工程骨架搭建**：
   - 在 `client/GodotApp/` 目录下初始化 Godot 4.x .NET C# 项目；
   - 将 `client/GodotApp/GodotApp.csproj` 添加到根目录 `GameAndLLM.sln`，并添加对 `src/GameCore/GameCore.csproj` 的项目引用。
2. **战前布阵与棋盘视图**：
   - 场景：`Scenes/Battlefield2x5.tscn`；
   - 落实 2×5 共 10 格自由放置手势与拖拽交互；
   - 落实后排（Row 1）单位自动按 0.85 缩放，并依据 Y 坐标动态计算 Z-Index 保证前后遮挡正确。
3. **战斗事件消费驱动器**：
   - 编写 `Scripts/BattleViewController.cs` 实现 `IBattleEventListener`；
   - 使用定频累加器（`FixedTimestepAccumulator`）：内核按 60Hz 步进，未满步长通过 Alpha 进行平滑插值（Lerp）；
   - 将 `DamageEvent`、`HealEvent` 转换为 Tween 平滑扣血、跳字飘字、受击闪白 Shader。
4. **战前战术与插槽配置 UI**：
   - 2 个 Gambit 战术四元组下拉选择框；
   - 词条拖拽装配界面（展示核心槽、作战槽、通用槽与奇点槽锁定态）；
   - 实时羁绊徽章点亮反馈面板。
5. **三轨地图节点拓扑渲染**：
   - 场景：`Scenes/RogueMapView.tscn`；
   - 读取 `RogueFloorMap`，使用 `Line2D` 绘制三轨节点网络连线，高亮当前可选节点并支持点击前进。
