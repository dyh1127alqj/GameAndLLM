# 《战术指令系统设计规格书》(TACTICS_GAMBIT_SPEC)

> **版本**：v1.0  
> **生效依据**：[DESIGN_DECISIONS.md](DESIGN_DECISIONS.md) (D-04, D-04d)  
> **核心定位**：在纯全自动自走棋战斗模式下，作为玩家战前表达战术意图、防止 AI 产生愚蠢决策（如残血小兵吃斩杀大招、满血放治疗）的轻量级策略工具。借鉴《最终幻想 12》Gambit 系统与现代自走棋战术配置。

---

## 目录
1. [战术指令系统架构与管线](#一战术指令系统架构与管线)
2. [槽位规则与匹配流程](#二槽位规则与匹配流程)
3. [首发 16 条核心战术指令库](#三首发-16-条核心战术指令库)
4. [Godot UI 交互与体验设计](#四godot-ui-交互与体验设计)
5. [C# 内核执行协议与数据契约](#五c-内核执行协议与数据契约)

---

## 一、战术指令系统架构与管线

### 1.1 核心原则
- **战前配置，战中绝对自动**：玩家只能在战前布阵界面调整素体的战术指令；战斗开始后，指令作为确定性启发式规则注入内核，严禁任何手动微操；
- **自上而下，首个命中短路**：每个单位按照【指令槽 1 → 指令槽 2 → 默认内置 AI 兜底】的优先级从上到下评估，一旦某个指令的【条件 (Condition)】评估为真，立即执行该指令的【动作 (Action)】，不再评估后续槽位；
- **轻量克制，严禁编程化**：每个素体**固定 2 个战术槽**，指令全部由预设的原子规则库中选择，不提供多层逻辑嵌套与变量编程，控制玩家心智负担在 1~2 分钟内。

---

## 二、槽位规则与匹配流程

```
[ 单位行动条就绪 (Gauge >= 1.0) ]
              │
              ▼
    [ 评估战术指令槽 1 ]
       ├─ 条件命中 ──▶ [ 执行指令动作 1 ] ──▶ [ 出手并清空行动条 ]
       │
       ▼ 未命中 / 无法执行
    [ 评估战术指令槽 2 ]
       ├─ 条件命中 ──▶ [ 执行指令动作 2 ] ──▶ [ 出手并清空行动条 ]
       │
       ▼ 未命中 / 无法执行
    [ 内核默认 AI 兜底 ] ──▶ [ 按 BATTLE_CORE 10.1 最近优先索敌 ]
```

> ⚠ **两处口径对齐**（依据 [BATTLE_CORE](BATTLE_CORE.md)）：
> - 行动条 `Gauge` 取值范围是 `[0, 1]`，就绪阈值为 **`1.0`**，不是 100；
> - 兜底层是**内核默认 AI**，不是"素体默认职能 AI"——[D-08](DESIGN_DECISIONS.md) 后**素体没有职能**，职业是可装卸的插槽词条。若该单位未装职业词条，兜底行为与装了职业词条时完全相同。

---

## 三、首发 16 条核心战术指令库

所有指令划分为四大战术大类：**【集火集杀】、【控场打断】、【守护救援】、【生存自保】**。

| 编号 | 指令代码 | 指令名称 | 触发条件 (Condition) | 目标与行为 (Action) | 典型适用职能 |
|:---:|---|---|---|---|---|
| **01** | `T_FOCUS_LOWEST_HP` | **弱者退场** | 视野内存在敌方单位 | 优先锁定【当前生命百分比最低】的敌方单位施放战技/大招 | 强袭 / 暗影 |
| **02** | `T_SNIPE_BACKLINE` | **越顶狙杀** | 敌方后排存在存活单位 | 忽略前排阻挡，强制以【敌方后排核心输出/辅助】为目标 | 游猎 / 暗影 |
| **03** | `T_SUPPRESS_HIGH_RAGE` | **绝息截击** | 敌方存在怒气 ≥ 70 的单位 | 优先对**怒气最高**的敌方单位施放伤害或控制技能，在其大招成型前压制 | 守卫 / 秘术 |
| **04** | `T_TARGET_ELITE_BOSS` | **屠龙专精** | 战场中存在精英怪 (Elite) 或 首领 (Boss) | 全程优先将伤害型大招砸向【最高阶敌方单位】 | 强袭 / 秘术 |
| **05** | `T_CLUSTER_BURST` | **轰炸稠密区** | 存在 2×2 区域内聚集敌方单位 ≥ 2 人 | 以【敌方单位最密集的中心点】为目标施放范围 AOE 技能 | 秘术 / 游猎 |
| **06** | `T_HEAL_CRITICAL` | **悬壶续命** | 友方任意单位生命值 < 30% | 立即对其施放单体大额治疗或套盾战技 | 支援 |
| **07** | `T_CLEANSE_STUN` | **灵台清明** | 己方主力输出身上挂有控制/眩晕 Debuff | 立即对其使用净化/驱散类战技 | 支援 |
| **08** | `T_SHIELD_VANGUARD` | **掩护前锋** | 己方最前排单位正在承受集火 (承伤者) | 优先为其施加护盾、分摊伤害或防御强化 | 守卫 / 支援 |
| **09** | `T_TAUNT_THREAT` | **固若金汤** | 自身周围 1 格内存在敌方高攻击单位 | 立即施放嘲讽战技，强行拉住其普攻仇恨 | 守卫 |
| **10** | `T_EXECUTE_HIGH_DEF` | **破甲突刺** | 目标护甲值 > 400 | 优先使用带有【无视/削减护甲】特性的攻击技能 | 强袭 |
| **11** | `T_SELF_PRESERVATION` | **金蝉脱壳** | 自身当前生命值 < 25% | 优先施放自身**护盾 / 隐匿 / 无敌**类保命技能 | 暗影 / 游猎 |
| **12** | `T_HOLD_ULT_BOSS` | **底牌留置** | 战场无 Elite / Boss 单位 | **大招怒气阈值由 100 提升至 150**；一旦出现精英或首领，阈值回落至 100，已积攒的怒气立即倾泻 | 全职能通用 |
| **13** | `T_CHAIN_KILL_RUSH` | **趁胜追击** | 敌方刚刚阵亡 1 名单位（5 秒内） | 优先集火**当前生命值最低**的残余敌人，接续斩杀链 | 强袭 |
| **14** | `T_MANA_BATTERY` | **算力反哺** | 友方法师/秘术单位法力值 < 30% | 优先对其施放法力充能或加速战技 | 支援 |
| **15** | `T_FRONT_LINE_WALL` | **坚守阵线** | 自身所处排为前排（`Row == 0`） | **只索敌敌方前排**，绝不越顶打后排；前排全灭后才解除限制 | 守卫 |
| **16** | `T_SPREAD_BLEED` | **雨露均沾** | 存在尚未挂上 DOT（流血/中毒/真火）的敌人 | 优先切换目标攻击无 DOT 的健康敌人，扩散负面状态 | 秘术 / 游猎 |

### 3.1 ✅ D-27 对指令库的三处修正

| 指令 | 原设计 | 问题 | 修正 |
|:---:|---|---|---|
| **03** 绝息截击 | 条件为"敌方正在引导吟唱" | 内核出手是**原子的**，技能瞬发，**没有可打断的窗口**——该条件永远无法命中 | 改为"敌方怒气 ≥ 70"，以**预防性压制**取代真打断。依赖 [21.8 Rank 标记](BATTLE_CORE.md) 之外无新机制 |
| **11 / 15** 金蝉脱壳 / 坚守阵线 | 依赖"位移后撤""不主动位移" | 接敌后移速归零，内核**无移动机制**（见 [BATTLE_CORE 21.12](BATTLE_CORE.md)） | 11 改为护盾/隐匿/无敌；15 改为索敌范围限制 |
| **12** 底牌留置 | "怒气满时暂扣大招" | 与 ✅ [D-04b](DESIGN_DECISIONS.md)「满怒自动释放」直接冲突，且需新增暂扣状态与溢出规则 | 改为**动态 `RageCap`**：无精英时阈值 150，精英出现回落 100。怒气跨波保留，阈值一落即倾泻——**零新机制实现同样意图** |
| **13** 趁胜追击 | 附带"技能伤害提升 20%" | **越权**——战术指令的职责边界是**选择目标**，不是发放数值加成，否则会演变成第二套词条系统 | 删除数值加成，只保留目标偏好 |

### 3.2 ⚠ 指令的前置技能需求

多条指令要求单位**恰好装备了对应类型的技能**——而技能来自插槽词条（[AFFIX 1.1](AFFIX_AND_SYNERGY.md)）：

| 指令 | 前置需求 |
|:---:|---|
| 06 悬壶续命 | 治疗类战技 |
| 07 灵台清明 | 驱散类战技（依赖 [21.4 Dispel](BATTLE_CORE.md)） |
| 08 掩护前锋 | 护盾类战技（依赖 [21.1 Shield](BATTLE_CORE.md)） |
| 09 固若金汤 | 嘲讽类战技（依赖 [21.10 Taunt](BATTLE_CORE.md)） |
| 10 破甲突刺 | 带 `Pen` 的攻击技能 |
| 11 金蝉脱壳 | 护盾 / 隐匿 / 无敌类技能 |
| 14 算力反哺 | 资源授予类技能（依赖 [21.7](BATTLE_CORE.md)） |

若未装备，该指令**不命中并降级至下一槽**（流程见[第二节](#二槽位规则与匹配流程)），不会报错，但等同于空槽。

> **UI 要求**：指令列表必须标注前置技能需求，并对当前构筑无法执行的指令做**灰显**处理，否则玩家会配置一堆永不触发的指令而无从察觉。

---

## 四、Godot UI 交互与体验设计

### 4.1 战前界面的配置体验
1. 在战前布阵界面的【素体详情卡】下方，设计有两个醒目的战术插槽卡槽；
2. 点击插槽，弹出简洁的**战术指令轮盘/抽屉列表**，按大类带图标（集火、打断、急救、自保）展示；
3. 玩家只需拖拽或单选即可装配完毕；提供【一键推荐战术】按钮，自动根据当前装备词条填充最佳契合指令。

### 4.2 战斗内的视觉反馈 (观战满足感)
- 当单位因战术指令而改变行为时，头顶飘出微型的**战术浮标**（例如：打断施法时飘出 `[截击!]`，残血急救时飘出 `[急救!]`）；
- 极大地满足玩家“我的战前预设成功生效了”的操控感与策略自豪感。

---

## 五、C# 内核执行协议与数据契约

### 5.1 战术指令数据模型

> ⚠ **必须是数据驱动，不能用委托**。指令需要进配置表、进存档、并支持回放校验——委托（`Func<>`）无法序列化，会同时堵死这三条路。

```csharp
namespace GameCore.Battle.Tactics;

public enum TacticsCategory
{
    Focus,          // 集火集杀
    CrowdControl,   // 控场压制
    SupportHeal,    // 守护急救
    Survive         // 生存自保
}

// 条件类型：枚举 + 参数，可序列化
public enum TacticsConditionType
{
    Always,                 // 无条件
    EnemyExists,            // 敌方存在存活单位
    EnemyBackRowAlive,      // 敌方后排有存活单位
    EnemyRageAtLeast,       // 敌方存在怒气 >= P0 的单位
    EnemyRankPresent,       // 战场存在 Rank >= P0 的敌人 (Elite/Boss)
    EnemyClusterAtLeast,    // 存在 P0 人以上聚集的敌方区域
    AllyHpBelow,            // 友方存在 HpRatio < P0 的单位
    AllyHasControlDebuff,   // 友方存在身上挂控制类 Debuff 的单位
    AllyManaBelow,          // 友方存在 Mana < P0 的单位
    SelfHpBelow,            // 自身 HpRatio < P0
    SelfInFrontRow,         // 自身 Row == 0
    TargetArmorAtLeast,     // 目标 Armor >= P0
    EnemyLacksDot,          // 存在未挂 DOT 的敌人
    AllyDiedRecently,       // 最近 P0 秒内有敌方单位阵亡
}

// 动作类型：只决定目标偏好，不发放任何数值加成
public enum TacticsActionType
{
    FocusLowestHpRatio,     // 集火生命百分比最低
    FocusLowestHpAbsolute,  // 集火当前生命值最低
    FocusHighestRage,        // 集火怒气最高
    FocusHighestAtk,         // 集火攻击力最高
    FocusHighestRank,        // 集火 Rank 最高
    FocusBackRow,            // 强制索敌敌方后排
    FocusFrontRowOnly,       // 只索敌敌方前排
    FocusDensestCluster,     // 索敌最密集区域中心
    FocusEnemyWithoutDot,    // 索敌未挂 DOT 的敌人
    HealLowestAlly,          // 治疗最危险友军
    CleanseAlly,             // 驱散友军
    ShieldAlly,              // 为友军套盾
    GrantResourceToAlly,     // 为友军授予法力/怒气
    SelfProtect,             // 自身护盾/隐匿/无敌
    RaiseRageCap,            // 提升自身 RageCap (底牌留置)
}

public sealed record TacticsInstruction
{
    public required string              InstructionId { get; init; }
    public required TacticsCategory     Category      { get; init; }

    public required TacticsConditionType ConditionType { get; init; }
    public float                         P0            { get; init; }   // 条件参数

    public required TacticsActionType    ActionType    { get; init; }
    public float                         A0            { get; init; }   // 动作参数
}
```

**求值器**在内核侧实现为一个纯函数的 `switch`：

```csharp
bool Evaluate(TacticsConditionType type, float p0, BattleUnit self, BattleSnapshot snap);
TargetSelectionResult Resolve(TacticsActionType type, float a0, BattleUnit self, BattleSnapshot snap);
```

这样指令本身退化为**一行配置数据**（`(ConditionType, P0, ActionType, A0)`），可以：
- 进配置表，由策划直接调参；
- 进存档，玩家的战术配置可持久化；
- 进回放日志，用于确定性回归校验。

### 5.2 决策生命周期保障
- 战术指令求值严格限制为纯内存只读扫描，耗时小于 0.05ms，在 90 秒的战斗模拟中绝不造成性能抖动；
- 排序完全基于已排序的 `UnitId`，保证跨机型与多次运行的绝对确定性。
