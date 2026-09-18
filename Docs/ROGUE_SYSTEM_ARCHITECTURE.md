# 《肉鸽卡牌自走棋系统架构与边界解耦设计规范》

> **版本**：v1.1 (Roguelike Card Auto-Battler Architecture)  
> **关联文档**：[DESIGN_DECISIONS.md](DESIGN_DECISIONS.md)（决策总账）、[WORLD_SETTING.md](WORLD_SETTING.md)、[BATTLE_CORE.md](BATTLE_CORE.md)、[HERO_VESSEL_SYSTEM_SPEC.md](HERO_VESSEL_SYSTEM_SPEC.md)、[ROGUE_WORLD_AND_AFFIX_DRAFT.md](ROGUE_WORLD_AND_AFFIX_DRAFT.md)、[ROGUE_MAP_AND_RECRUITMENT.md](ROGUE_MAP_AND_RECRUITMENT.md)  
> **核心定位**：**多元宇宙无限流** × **策略前置的肉鸽卡牌自动战斗**。
>
> **v1.1 变更**（依据 [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md)）：
> - D-01 题材由三国改为多元宇宙无限流，全部经济与角色命名更换；
> - D-04 战斗确定为**纯全自动零输入**，手操相关表述全部删除；
> - D-08 **职业羁绊取消**，羁绊下沉至插槽；`SynergyEngine` 由 L1 内核域**移出至 L2 Rogue 层**。

---

## 目录
1. [设计背景与定位升级](#一设计背景与定位升级)
2. [总体系统架构与边界全景](#二总体系统架构与边界全景)
3. [五大核心系统域职责矩阵](#三五大核心系统域职责矩阵)
4. [系统解耦与数据流转契约](#四系统解耦与数据流转契约)
5. [战斗机制自走棋化升级路径](#五战斗机制自走棋化升级路径)

---

## 一、设计背景与定位升级

项目最初规划为《单机三国志2 重制版》，战斗采用高频手操拖拽出招。深入分析后发现：**全队平均每秒产生约 2.6 次出手决策，而玩家手势操作带宽上限约 1~1.5 次/秒**——缺口意味着玩家实战中大部分出手只能被动交由 AI 托管，"手操"成为噪声而非策略。

由此发生两次连续转向：

| 转向 | 内容 | 依据 |
|:---:|---|:---:|
| ① | 手操 ATB 战斗 → **肉鸽卡牌自走棋**（战斗自动化，策略前置） | 上述带宽分析 |
| ② | 三国题材 → **多元宇宙无限流**（跨世界词条构筑） | [D-01](DESIGN_DECISIONS.md) |

**当前定位的战略价值**：
1. **彻底释放操作带宽**：战斗为**纯全自动零输入**（[D-04](DESIGN_DECISIONS.md)），微操完全交由 AI、ATB 与自动化技能管线执行；
2. **策略心智前置**：玩家的全部核心决策转移至**局内路线规划（Pathing）**、**素体招募（Drafting）**、**插槽词条装配与羁绊构筑（Building）**、**阵型站位与元素克制对位（Positioning）** 以及 **遗物/世界法则组合（Modifier Combos）**；
3. **跨世界构筑爽点**：不同世界的词条可通过**超武融合**产生质变组合，这是无限流题材相对单一题材的核心增量；
4. **架构彻底解耦**：战斗内核退化为纯函数式的无状态黑盒模拟器，极大降低子系统间的耦合复杂度。

> ⚠ **全自动的代价**：零输入意味着战斗表现必须独自承担全部参与感。三项要求由"可选优化"升格为"必答题"——构筑深度必须足够、战斗过程必须可读、**AI 质量直接等于游戏质量**。详见 [D-04c](DESIGN_DECISIONS.md)（战斗时长与倍速）与 [D-04d](DESIGN_DECISIONS.md)（AI 决策质量）。

---

## 二、总体系统架构与边界全景

系统采用严格单向依赖的**整洁架构（Clean / Hexagonal Architecture）**，明确划分为五个边界域：

```mermaid
flowchart TD
    subgraph L5_Presentation [表现与交互层 (Godot 4.x Presentation)]
        UI_HUD[战斗与HUD视图]
        UI_Map[节点地图与路线选择]
        UI_Shop[酒馆/商店与招募抽卡]
        VFX_Audio[视听反馈/打击特效/音效池]
    end

    subgraph L4_LLM [LLM 智能与叙事驱动层 (GameAndLLM 扩展)]
        LLM_Narrator[奇遇事件动态推演]
        LLM_Bark[战前素体喊话/局内解说]
        LLM_Tactic[Boss 策略动态意图生成]
    end

    subgraph L3_Meta [局外元养成域 (Meta Progression)]
        SaveSystem[存档/序列化系统]
        TalentTree[局外君主天赋/局外解锁]
        Compendium[素体/词条/敌人图鉴]
    end

    subgraph L2_Rogue [局内肉鸽状态机 (Rogue Run Loop)]
        RunState[单局状态机 RunState]
        MapGraph[地图拓扑与节点流转]
        TeamDeck[战队构筑 (素体招募/插槽装配)]
        SynergyEngine[插槽羁绊结算器 → 产出 Modifier]
        RelicManager[遗物/世界法则修正器管道]
        Economy[局内资源 (授权点/信用点)]
    end

    subgraph L1_BattleCore [纯 C# 确定性战斗内核 (BattleSim)]
        BattleSim[BattleSim 模拟器]
        UnitModel[单位属性与状态模型]
        CombatPipeline[出手管线与伤害减免管线]
        ElementSystem[元素克制环]
        ModifierPipeline[修正器注入管道]
        EventStream[单向事件流 Events]
    end

    %% 依赖与通信关系
    L5_Presentation -->|监听渲染| L1_BattleCore
    L5_Presentation -->|下达决策指令| L2_Rogue
    L5_Presentation -->|读取配置与存档| L3_Meta

    L2_Rogue -->|装配并启动单场战斗| L1_BattleCore
    L1_BattleCore -->|上报战斗结果 BattleResult| L2_Rogue

    L2_Rogue -->|结算并上报战报| L3_Meta

    L4_LLM -.->|异步提供文本/选项/候选池索引| L2_Rogue
    L4_LLM -.->|订阅事件驱动台词| L5_Presentation
```

---

## 三、五大核心系统域职责矩阵

| 系统域 | 核心职责 (What it DOES) | 严禁越界行为 (What it NEVER does) | 核心输入 / 输出 |
|---|---|---|---|
| **1. 战斗内核域<br>(`SanguoCore.Battle`)** | • 纯 C# 确定性物理与数值模拟<br>• ATB 充能、自动索敌、出手管线、受击退火<br>• 元素克制环<br>• 单场战斗从 Marching 到 Victory/Defeat<br>• 产生事件流供表现层消费 | • **严禁引用 Godot 引擎**任何 API<br>• **严禁感知肉鸽外围状态**（不知道什么是"下一层"、不知道货币）<br>• **严禁直接调用 LLM**<br>• **严禁认识"职业"与"羁绊"概念**——只认 `Tags` 与传入的 `Modifiers`<br>• **严禁接受任何玩家局内输入**（D-04 全自动） | **Input**: `BattleContext`（我方阵容快照、敌方波次快照、**羁绊/遗物/世界法则已折算成的修正器列表**、随机种子）<br>**Output**: `BattleResult`（胜负、各单位剩余血量/法力/怒气、伤害统计、事件流日志） |
| **2. 肉鸽状态机域<br>(`SanguoCore.Rogue`)** | • 管理单次单局（Run）的完整生命周期<br>• 节点地图（Map Graph）生成与推进（战斗/突袭/商店/事件/休整）<br>• 战队构筑（素体招募、插槽装配、进阶）<br>• **羁绊结算**：统计全队插槽标签件数 → 匹配档位 → 产出 `Modifier`<br>• 局内遗物与世界法则的修正器管理<br>• 局内经济（授权点/信用点）流转 | • **不负责渲染具体的 UI 动画**<br>• **不干涉单次战斗内的微观每一帧 Tick**<br>• 不负责跨单局的永久数据（除结算时上报 Meta） | **Input**: 玩家交互指令（如：选择节点、招募素体、装配词条）<br>**Output**: `RunStateSnapshot`（当前层数、全队健康度、插槽装配、已激活羁绊、可用交互） |
| **3. 局外元养成域<br>(`SanguoCore.Meta`)** | • 跨单局存档持久化（JSON / 二进制）<br>• 轮回点数、主神科技树（解锁初始授权点、素体池偏好等）<br>• 难度阶梯（Ascension Level）管理<br>• **传承封印**（跨局带入 1 枚词条）<br>• 成就与图鉴系统 | • **不参与局内实时状态运算**<br>• 不直接依赖表现层图形节点 | **Input**: 单局结束后的 `RunSummary`<br>**Output**: 玩家 Profile、解锁的素体池与词条池掩码 |
| **4. 表现与交互层<br>(`GodotPresentation`)** | • 纯展现与用户输入采集<br>• 阵型站位拖拽、卡牌信息预览<br>• 序列帧打击特效、飘字 Tween、音频混音池<br>• 响应式的 UI 状态绑定（MVVM / Reactive） | • **严禁在表现层中编写战斗结算或数值扣减公式**（表现层永远只是“播放器”）<br>• 严禁直接篡改底层模型属性 | **Input**: 引擎渲染帧 `_Process`、用户触控/鼠标输入、内核事件流<br>**Output**: 调用 Rogue/Battle 的指令接口 |
| **5. LLM / AI 扩展层<br>(`GameAndLLM.Agent`)** | • **异步外挂服务**：为肉鸽奇遇事件生成动态剧情与多选分支<br>• 根据战况日志（BattleResult）生成素体动态嘲讽、战斗评述（主神点评）<br>• 动态生成具名精英敌人的个性化外号与战斗意图提示 | • **绝对不能出现在战斗内的高频 Tick 循环中**<br>• 网络延迟或 API 失败时必须有 **Fallback 离线静态配置**，不可导致核心玩法阻断<br>• **严禁自由指定掉落物 ID**——只能从种子预生成的候选池中按索引选择（⬜ [D-20](DESIGN_DECISIONS.md)），否则破坏种子可复现性 | **Input**: 局内上下文 JSON（当前阵容、已激活羁绊、遭遇事件 ID、候选池条目、上局战报）<br>**Output**: 结构化数据/文本（动态事件选项、对话台词、候选池索引） |

---

## 四、系统解耦与数据流转契约

### 4.1 快照装配模式（Snapshot Injection）
战斗内核被封装为纯函数式的确定性黑盒，与肉鸽状态机通过快照解耦：

```csharp
public class BattleContext {
    public int Seed { get; init; }
    public List<UnitSnapshot> PlayerTeam { get; init; }  // 从 Rogue 战队派生的 5 人只读快照
    public List<WaveSnapshot> EnemyWaves { get; init; }  // 从关卡/怪物表提取的波次快照

    // 羁绊、遗物、世界法则统一折算为修正器后注入。
    // 内核不知道它们的来源，只按挂钩点执行。
    public List<IBattleModifier> Modifiers { get; init; }
}

// 单位快照：注意没有 Class 字段。
// 职业是插槽被动，对内核而言只是 Tags 里的一个字符串。
public record UnitSnapshot {
    public int      UnitId   { get; init; }
    public int      MaxHp    { get; init; }
    public int      Atk      { get; init; }
    public int      Armor    { get; init; }
    public int      Speed    { get; init; }
    public Element  Element  { get; init; }   // 元素克制环，内核唯一认识的"属性"
    public IReadOnlySet<string> Tags { get; init; }  // 由插槽装配派生
    public SkillDef ActiveSkill { get; init; }       // 战技（法力驱动）
    public SkillDef UltimateSkill { get; init; }     // 大招（怒气驱动）⬜ 见 D-04b
}

// 模拟器执行接口
public interface IBattleSim {
    BattleResult Simulate(BattleContext context);
}
```

战斗结束产出 `BattleResult`：
```csharp
public class BattleResult {
    public BattleOutcome Outcome { get; init; } // Victory / Defeat / Timeout
    public IReadOnlyList<UnitBattleEndState> FinalStates { get; init; } // 各单位终态(剩余HP/法力/怒气/是否阵亡)
    public BattleStatistics Stats { get; init; } // 承伤、输出、治疗统计
    public IReadOnlyList<BattleEvent> EventLog { get; init; } // 完整事件流
}
```
Rogue 层接收此结果后，负责更新素体健康状态（含**重伤标记**，⬜ [D-09](DESIGN_DECISIONS.md)）、结算奖励并推进节点。

### 4.2 统一修正器管道（Modifier Pipeline）

避免在战斗内核硬编码 `if (hasRelic_X)`。**羁绊、遗物、世界法则三者共用同一条管道**——内核不区分它们的来源：

| 接口 | 挂钩点 | 示例 |
|---|---|---|
| **`IStatModifier`** | 单位属性初始化时 | 「携带 `守护` 标签的单位生命上限 +20%」 |
| **`IDamageModifier`** | 伤害计算管线中 | 「对已中 DOT 的目标伤害 +25%」 |
| **`IActionModifier`** | 行动条与资源充能时 | 「开战时全员初始法力 +30」 |
| **`IHealModifier`** | 治疗结算时 | 「本世界治疗效果 -20%」（世界法则 W-01）⬜ 见 D-12 |

**命名约定**（[D-21](DESIGN_DECISIONS.md) 待决，暂用此口径）：

| 中文 | 英文 | 定义 |
|---|---|---|
| 词条 | `Affix` | 可镶嵌进素体插槽的构筑单位 |
| 修正器 | `Modifier` | 注入内核的战斗修改器（羁绊/遗物/世界法则折算而来） |
| 世界法则 | `WorldLaw` | 该世界常驻的环境规则 |

> ⛔ **禁止使用"词缀"一词**——它与"词条"一字之差、指代不同，在中文文档与代码注释中极易混淆。

> ⚠ 示例中不得出现"暴击"——内核**当前没有暴击系统**（[D-10](DESIGN_DECISIONS.md) 待决，推荐不引入）。本节前一版的示例已据此更换。

---

## 五、战斗机制自走棋化升级路径

1. **纯全自动与策略前置**（[D-04](DESIGN_DECISIONS.md) ✅ 已决）：
   - 战斗期间**零玩家输入**。拖拽下令、蓄力施法、手动大招等机制**全部废除**，不保留任何"手动可选"开关；
   - 单位行动条充满后按 AI 优先级自动索敌并释放战技/大招；
   - 核心玩法转化为**战前构筑与布阵**：素体招募、插槽装配、站位对位、元素克制对位。

2. **羁绊下沉至插槽，职业羁绊取消**（[D-08](DESIGN_DECISIONS.md) ✅ 已决）：
   - ⛔ **不存在"按上阵职业人数触发"的羁绊**——本文档前一版对此的描述已作废；
   - 职业本身是一类**可自由装卸的插槽被动**。素体无先天职业，仅提供属性底盘、本命特性与权能等级；
   - 插槽被动分三类：**血脉 / 职业 / 技能**；
   - 羁绊按**插槽件数**计数，阶梯为 `(2) / (4) / (6)`。`5 人 × 2~4 槽 = 10~20 个插槽`，计数基数充足；
   - 原六职业个体天赋（25% 免伤、30% 再动等）**已删除**，效果全部改由羁绊档位提供。

3. **元素克制环**（[D-02](DESIGN_DECISIONS.md) ✅ 已决）：
   - 五元素克制环与 `1.25 / 1.00 / 0.75` 系数**保留在内核**（它是伤害公式的组成部分）；
   - 各世界仅更换**显示命名与美术表皮**，映射表不得进入任何逻辑判断。

4. **续航与战损机制（Health Economy）**：
   - 战损跨节点沉淀，强化营地与商店节点的战略价值；
   - ⬜ 具体方案见 [D-09](DESIGN_DECISIONS.md)（待决）。

---

### 5.1 ⚠ 架构要点：羁绊不属于战斗内核

羁绊由 **L2 Rogue 层**在装配 `BattleContext` 时结算完毕，转换为 `IBattleModifier` 列表注入内核。

```
[插槽装配] → [Rogue 层统计标签件数] → [匹配羁绊档位] → [产出 Modifier 列表] → [注入 BattleContext]
```

**内核不认识"职业"，也不认识"羁绊"**——它只认识单位身上的 `Tags` 集合与传入的 `Modifiers`。这样做的收益：

- 内核保持纯粹与确定性，羁绊规则调整不需要动内核；
- 羁绊可以在战斗外被完整预览（玩家在布阵界面就能看到激活了什么）；
- 羁绊与遗物、世界法则共用同一条 `Modifier` 管道，无需三套机制。

**唯一留在内核的"系统性机制"是元素克制环**，因为它直接参与伤害公式的乘区计算，外移会导致每次伤害都要回调 Rogue 层。
