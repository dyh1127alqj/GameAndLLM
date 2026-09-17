# 《肉鸽卡牌自走棋系统架构与边界解耦设计规范》

> **版本**：v1.0 (Roguelike Card Auto-Battler Architecture)  
> **关联文档**：[BATTLE_SYSTEM.md](BATTLE_SYSTEM.md)、[BATTLE_CORE.md](BATTLE_CORE.md)、[ROGUE_MAP_AND_RECRUITMENT.md](ROGUE_MAP_AND_RECRUITMENT.md)  
> **核心定位**：从传统手操战斗升级为**策略前置的“肉鸽like（Roguelike）卡牌自动战斗”**游戏。

---

## 目录
1. [设计背景与定位升级](#一设计背景与定位升级)
2. [总体系统架构与边界全景](#二总体系统架构与边界全景)
3. [五大核心系统域职责矩阵](#三五大核心系统域职责矩阵)
4. [系统解耦与数据流转契约](#四系统解耦与数据流转契约)
5. [战斗机制自走棋化升级路径](#五战斗机制自走棋化升级路径)

---

## 一、设计背景与定位升级

在原《单机三国志2 重制版》核心战斗规划中，原设计包含高频的手操拖拽出招机制。但在深入分析后发现，全队平均每秒产生约 2.6 次出手决策，远超玩家手势操作带宽，导致玩家实战中绝大多数时间只能被动交由 AI 托管。

**定位升级为“肉鸽卡牌自走棋”的战略价值**：
1. **释放操作带宽**：战斗中将微操彻底交由武将 AI、ATB 行动条与自动化技能管线执行；
2. **策略心智前置**：玩家的核心决策转移至**局内路线规划（Pathing）**、**职业招募与进阶（Drafting）**、**阵型站位与五行克制对位（Positioning）**以及**遗物/宝物修改器组合（Relic Combos）**；
3. **架构彻底解耦**：战斗内核退化为纯函数式的无状态黑盒模拟器，极大降低各子系统之间的耦合复杂度。

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
        LLM_Bark[战前武将喊话/局内解说]
        LLM_Tactic[Boss 策略动态意图生成]
    end

    subgraph L3_Meta [局外元养成域 (Meta Progression)]
        SaveSystem[存档/序列化系统]
        TalentTree[局外君主天赋/局外解锁]
        Compendium[武将/遗物/怪物图鉴]
    end

    subgraph L2_Rogue [局内肉鸽状态机 (Rogue Run Loop)]
        RunState[单局状态机 RunState]
        MapGraph[地图拓扑与节点流转]
        TeamDeck[战队卡组管理 (招募/进阶/装备)]
        RelicManager[遗物/宝物修改器管道]
        Economy[局内资源 (军令/军粮/铜币)]
    end

    subgraph L1_BattleCore [纯 C# 确定性战斗内核 (BattleSim)]
        BattleSim[BattleSim 模拟器]
        UnitModel[武将属性与状态模型]
        CombatPipeline[7步出手与伤害减免管线]
        SynergyEngine[六职业羁绊 / 五行相克]
        EventStream[单向事件流 Events]
    end

    %% 依赖与通信关系
    L5_Presentation -->|监听渲染| L1_BattleCore
    L5_Presentation -->|下达决策指令| L2_Rogue
    L5_Presentation -->|读取配置与存档| L3_Meta

    L2_Rogue -->|装配并启动单场战斗| L1_BattleCore
    L1_BattleCore -->|上报战斗结果 BattleResult| L2_Rogue

    L2_Rogue -->|结算并上报战报| L3_Meta

    L4_LLM -.->|异步提供文本/选项/词缀| L2_Rogue
    L4_LLM -.->|订阅事件驱动台词| L5_Presentation
```

---

## 三、五大核心系统域职责矩阵

| 系统域 | 核心职责 (What it DOES) | 严禁越界行为 (What it NEVER does) | 核心输入 / 输出 |
|---|---|---|---|
| **1. 战斗内核域<br>(`SanguoCore.Battle`)** | • 纯 C# 确定性物理与数值模拟<br>• ATB 充能、自动索敌、7步出手管线、受击退火<br>• 单场战斗从 Marching 到 Victory/Defeat<br>• 产生每帧事件流供表现层消费 | • **严禁引用 Godot 引擎**任何 API<br>• **严禁感知肉鸽外围状态**（不知道什么是“下一层”、不知道金币）<br>• **严禁直接调用 LLM** | **Input**: `BattleContext`（我方阵容快照、敌方波次快照、修改器列表、随机种子）<br>**Output**: `BattleResult`（胜负、各武将剩余血量/法力、伤害统计、事件流日志） |
| **2. 肉鸽状态机域<br>(`SanguoCore.Rogue`)** | • 管理单次单局（Run）的完整生命周期<br>• 节点地图（Map Graph）生成与推进（战斗/突袭/商店/事件/休整）<br>• 战队阵容构筑（武将招募、进阶、装备槽）<br>• 局内遗物（Relics）全域生效管理<br>• 局内经济（军令/军粮/铜币）流转 | • **不负责渲染具体的 UI 动画**<br>• **不干涉单次战斗内的微观每一帧 Tick**<br>• 不负责跨单局的永久数据（除结算时上报 Meta） | **Input**: 玩家交互指令（如：选择节点、购买武将、装备符石）<br>**Output**: `RunStateSnapshot`（当前层数、全队健康度、遗物栏、可用交互） |
| **3. 局外元养成域<br>(`SanguoCore.Meta`)** | • 跨单局存档持久化（JSON / 二进制）<br>• 君主等级、局外天赋树（解锁初始军令、卡池偏好武将等）<br>• 游戏难度/灾厄（Ascension Level）词缀管理<br>• 成就与图鉴系统 | • **不参与局内实时状态运算**<br>• 不直接依赖表现层图形节点 | **Input**: 单局结束后的 `RunSummary`<br>**Output**: 玩家 Profile、解锁的卡池和遗物池掩码 |
| **4. 表现与交互层<br>(`GodotPresentation`)** | • 纯展现与用户输入采集<br>• 阵型站位拖拽、卡牌信息预览<br>• 序列帧打击特效、飘字 Tween、音频混音池<br>• 响应式的 UI 状态绑定（MVVM / Reactive） | • **严禁在表现层中编写战斗结算或数值扣减公式**（表现层永远只是“播放器”）<br>• 严禁直接篡改底层模型属性 | **Input**: 引擎渲染帧 `_Process`、用户触控/鼠标输入、内核事件流<br>**Output**: 调用 Rogue/Battle 的指令接口 |
| **5. LLM / AI 扩展层<br>(`GameAndLLM.Agent`)** | • **异步外挂服务**：为肉鸽奇遇事件生成动态剧情与多选分支<br>• 根据战况日志（BattleResult）生成武将动态嘲讽、战斗评述（解说席）<br>• 动态生成具名精英怪的个性化外号与战斗意图提示 | • **绝对不能出现在战斗内的高频 Tick 循环中**<br>• 网络延迟或 API 失败时必须有 **Fallback 离线静态配置**，不可导致核心玩法阻断 | **Input**: 局内上下文 JSON（当前阵容、遭遇事件 ID、上局战报）<br>**Output**: 结构化数据/文本（动态事件选项、对话台词、战术建议） |

---

## 四、系统解耦与数据流转契约

### 4.1 快照装配模式（Snapshot Injection）
战斗内核被封装为纯函数式的确定性黑盒，与肉鸽状态机通过快照解耦：

```csharp
public class BattleContext {
    public int Seed { get; init; }
    public List<UnitSnapshot> PlayerTeam { get; init; }  // 从 Rogue 战队派生的 5 人只读快照
    public List<WaveSnapshot> EnemyWaves { get; init; }  // 从关卡/怪物表提取的波次快照
    public List<IBattleModifier> Modifiers { get; init; } // 遗物与词缀转化成的修改器
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
    public IReadOnlyList<UnitBattleEndState> FinalStates { get; init; } // 各武将终态(剩余HP/法力)
    public BattleStatistics Stats { get; init; } // 承伤、输出、治疗统计
    public IReadOnlyList<BattleEvent> EventLog { get; init; } // 完整事件流
}
```
Rogue 层接收此结果后，负责更新单局武将健康状态、结算军粮并推进节点。

### 4.2 遗物系统的“修改器管道模式（Modifier Pipeline）”
避免在战斗内核硬编码 `if (hasRelic_X)`，通过在 7 步结算管线中注入统一的修改器接口：

1. **`IStatModifier`**：在单位属性初始化时介入（例如：“近战武将生命上限 +20%”）；
2. **`IDamageModifier`**：在伤害计算管线中介入（例如：“暴击时对目标施加 2 秒流血”）；
3. **`IActionModifier`**：在行动条与能量充能时介入（例如：“开战时全员初始法力 +30”）。

---

## 五、战斗机制自走棋化升级路径

1. **自动出招与策略前置**：
   - 战斗彻底实现全自动：武将行动条充满后按既定 AI 优先级索敌并开大；
   - 核心玩法转化为**战前布阵**：我方 5 人在 2排×5列 中的站位对位、前排承伤护卫、五行对位克制。
2. **职业与五行升级为“羁绊（Synergies）”**：
   - 防、近、速、弓、术、补 6 大职业转为上阵羁绊人数阶梯（例如：2/4 弓激活全员破甲与流血）；
   - 五行（金木水火土）转为元素矩阵羁绊，强化克制反馈。
3. **续航与战损机制（Health Economy）**：
   - 原作中“负伤”与“包子/医馆”天然契合肉鸽长线生存博弈，战损跨场次沉淀，强化营地与药铺节点的战略价值。
