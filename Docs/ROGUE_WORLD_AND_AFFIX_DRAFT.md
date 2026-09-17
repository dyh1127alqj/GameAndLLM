# 《无限流主题世界与词条构筑系统设计规范（草稿 RFC v0.1）》

> **文档定位**：针对“无限流跨世界单局 + 词条插槽构筑 + 主神空间元循环”的系统级设计草案。  
> **状态**：**[草稿 / RFC 深度讨论版]**（待推敲演进，非最终定稿）  
> **核心痛点聚焦**：如何让词条系统具备深度（不只是属性堆砌）、如何防止素体同质化、跨界融合如何产生质变。

---

## 目录
1. [系统总体哲学与全景架构](#一系统总体哲学与全景架构)
2. [世界主题与难度演化模型 (World & Ascension)](#二世界主题与难度演化模型-world--ascension)
3. [轮回者素体模型 (Hero Vessel & Innate Chassis)](#三轮回者素体模型-hero-vessel--innate-chassis)
4. [词条生态：分类、阶梯与超武融合 (Affix Ecosystem)](#四词条生态分类阶梯与超武融合-affix-ecosystem)
5. [套装共鸣与跨界共振矩阵 (Set & Cross-Resonance)](#五套装共鸣与跨界共振矩阵-set--cross-resonance)
6. [主神空间局外长线元循环 (Meta Hub & Legacy)](#六主神空间局外长线元循环-meta-hub--legacy)
7. [LLM 主神沙盒与确定性契约 (LLM Overseer Sandbox)](#七llm-主神沙盒与确定性契约-llm-overseer-sandbox)
8. [深水区待决策技术与玩法问题清单](#八深水区待决策技术与玩法问题清单)

---

## 一、系统总体哲学与全景架构

```mermaid
flowchart TD
    subgraph Meta_Hub [主神空间 (局外长线 Meta Hub)]
        WorldSelector[世界星图 / 难度选择]
        OperativePool[轮回者大名单招募]
        LegacyVault[传家宝词条 / 基因锁潜能]
    end

    subgraph World_Run [单次世界战役 (Single World Run)]
        MapEngine[3-Lane 拓扑推进 (该世界 1~5 层)]
        
        subgraph Team_Build [战队与词条构筑]
            HeroChassis[5名出战素体 (提供职能底盘)]
            AffixSockets[素体灵魂插槽 (2~4槽)]
            TeamResonance[全队共鸣池 (统计全队标签)]
        end

        subgraph Drops_Events [事件与掉落生态]
            ThemedAffixes[75% 本世界原生词条]
            CrossRift[25% 时空裂隙跨界奇遇]
            AffixForge[安全区: 词条精炼与超武融合]
        end
    end

    subgraph LLM_System [LLM 主神系统 (异步决策层)]
        Briefing[世界任务简报]
        DynamicEvent[不期而遇位面抉择]
        ScoreAndRoast[战后主神评定与吐槽]
    end

    Meta_Hub -->|配置世界与初始素体| World_Run
    World_Run -->|结算轮回点数与传家宝| Meta_Hub
    LLM_System -.->|注入情境文本与分支| World_Run
```

---

## 二、世界主题与难度演化模型 (World & Ascension)

### 2.1 主题世界目录设计（初期 4 大世界规划）

| 世界代号 | 世界名称 | 视觉与世界观基调 | 本世界主词条标签 | 世界常驻法则（环境词缀） |
|:---:|---|---|:---:|---|
| **W-01** | **末日生化都市** | 废土、丧尸狂潮、辐射废墟、变异体 | `[生化变异]`<br>`[辐射废土]` | **环境辐射**：所有单位每 10 秒流失 2% 生命，治疗效果衰减 20% |
| **W-02** | **赛博霓虹蜂巢** | 机械义体、数据流、巨企压迫、AI神明 | `[赛博义体]`<br>`[超频协议]` | **电磁过载**：暴击率提升 15%，但释放技能后强制眩晕 0.5 秒 |
| **W-03** | **蜀山断裂仙界** | 飞剑、锁妖塔、古灵力、残卷秘籍 | `[蜀山剑诀]`<br>`[五行玄门]` | **灵气复苏**：法力自然恢复速度 +100%，敌方精英具备灵力护盾 |
| **W-04** | **极地旧日深渊** | 暴风雪、星空异形、不可名状、理智侵蚀 | `[深渊血脉]`<br>`[疯狂低语]` | **理智崩解**：全员攻击力随战斗时长每秒 +1%，受到伤害同时 +1.5% |

### 2.2 难度阶梯体系（侵蚀度 / 熵值 Ascension 0~15）
每个世界单独记录通关最高难度。难度递增不是单纯数值乘算，而是**机制质变**：
* **Ascension 1**：敌人波次追加前排重装兵种；
* **Ascension 5（法则异化）**：世界常驻法则效果翻倍，精英怪必定携带一条该世界高级词条；
* **Ascension 10（主神制约）**：所有素体的灵魂插槽上限强制锁闭 1 格，需在营地高额付费解锁；
* **Ascension 15（真实终局）**：关底解锁**世界的隐藏真神 Boss**，通关解锁该世界的终极神话传家宝。

---

## 三、轮回者素体模型 (Hero Vessel & Innate Chassis)

> ⚠ **核心风险防范**：如果词条太强，所有英雄穿相同词条就会变成同一个人（角色个性丧失）。  
> **解决方案**：每个素体必须拥有**“本命素体被动（Chassis Synergy）”**，决定其最适合装配什么词条！

```csharp
public class HeroVessel {
    public string HeroId { get; init; }
    public string Name { get; init; }
    public HeroClass BaseClass { get; init; }      // 防、近、速、弓、术、补 (战术底盘)
    
    // 1. 战术底盘固有属性
    public int BaseSpeed { get; init; }
    public SkillDefinition InnateSkill { get; init; } // 核心大招
    
    // 2. 本命素体特性 (灵魂被动: 决定其与词条的相性)
    public IVesselInnateTrait InnateTrait { get; init; }
    
    // 3. 插槽系统 (Socket Constraints)
    public List<SocketSlot> Sockets { get; init; } // 具有类型倾向的插槽
}
```

### 3.1 插槽的类型化设计（防止乱装）
每个角色的插槽不应全都是“万能白板槽”，而是包含**倾向类型**：
* **核心槽（Core Socket）**：只能装【本职业】或【主神血统】词条；
* **作战槽（Combat Socket）**：只能装武器、伤害机制、触发机制词条；
* **通用槽（Universal Socket）**：任意词条皆可镶嵌（进阶完全体后解锁）。

### 3.2 素体特性范例（让英雄成为词条的放大器）
* **素体 A（代号：铁浮屠 · 防）**：
  * *本命特性【钢铁回响】*：身上每装备一个带有 `[装甲]` 或 `[机体]` 的词条，自身受击反弹伤害提升 100%。
* **素体 B（代号：青莲子 · 术）**：
  * *本命特性【万法归宗】*：装备的任何物理系词条，其伤害自动转换为最高克制倍率的五行法术伤害。
* **素体 C（代号：影刃 · 速）**：
  * *本命特性【过载神经】*：当再动触发时，装备的所有冷却型词条立即跳过 1 秒冷却。

---

## 四、词条生态：分类、阶梯与超武融合 (Affix Ecosystem)

### 4.1 词条结构模型
```csharp
public record AffixDefinition(
    string AffixId,
    string DisplayName,
    AffixCategory Category,        // Attack, Defense, Utility, Curse
    AffixRarity Rarity,            // Common(白), Fine(绿), Rare(蓝), Epic(紫), Mythic(金)
    string SetTag,                 // 归属的套装标签，如 "Cyber", "Shushan"
    int Tier,                      // 词条品阶 (Lv1 ~ Lv3)
    
    // 效果载荷
    IReadOnlyList<IStatModifier> StatModifiers,
    IReadOnlyList<IBattleHook> CombatHooks,
    
    // 超武合成配方引用 (可为空)
    string FusionRecipeTargetId
);
```

### 4.2 词条品质与强化机制（词条升级）
词条在局内并非一成不变，支持在营地进行**【精炼（Refine）】**：
* 相同词条合成：`2 个相同 Lv1 词条` $\to$ 融合成 `1 个 Lv2 词条`（数值提升 60%，并产生额外次级词缀）；
* 最高精炼至 Lv3（达到本词条的完全体形态）。

### 4.3 核心爽点：跨界超武融合机制（Super-Affix Fusion）
> 参考《吸血鬼幸存者》、《土豆兄弟》等顶级肉鸽的质变合成！

当一名角色身上同时装配了**两枚满足特定条件的互斥世界词条，并提升至满阶**时，在休整营地可融合成破格的**【概念级超武词条（Fusion Affix）】**，并腾出一个插槽！

```
┌─────────────────────────┐       ┌─────────────────────────┐
│ [赛博] 高频纳米振动刃(Lv3) │   +   │  [仙侠] 万剑归宗心诀(Lv3) │
└────────────┬────────────┘       └────────────┬────────────┘
             │                                 │
             └───────────────┬─────────────────┘
                             ▼
              ┌──────────────────────────────┐
              │  【超武】赛博飞剑·浮游歼灭阵列  │
              │  普攻射出 8 柄纳米高频飞剑，   │
              │  穿透所有前后排并附带真实流血   │
              └──────────────────────────────┘
```

#### 超武合成谱系预设：
1. **【赛博重型力场盾】+【古神不可名状之眼】=【虚空吞噬装甲】**（吸收伤害并将 50% 转化为不可名状触须反攻）；
2. **【生化辐射核芯】+【蜀山三昧真火】=【核爆三昧真火】**（灼烧 DOT 转化为全场指数级扩散的链式核裂变）；
3. **【基因狂暴针剂】+【圣光降临祈祷】=【狂暴天兵降世】**（濒死时变身为无敌的狂暴光之泰坦 8 秒）。

---

## 五、套装共鸣与跨界共振矩阵 (Set & Cross-Resonance)

### 5.1 全队标签统计规则
系统在战前将出战 5 人装备的所有词条的 `SetTag` 汇总到全队共鸣池：
$$\text{TagCount}(T) = \sum_{u \in \text{Team}} \text{CountTags}(u, T)$$

* **标准阶梯**：`(2)` 件激活微共鸣，`(4)` 件激活大共鸣，`(6)` 件激活超共鸣（需要跨人装配）。

### 5.2 跨界对冲法则（Dimensional Duality）
部分极端的套装如果同时存在于一队，会产生“时空排斥”或“奇点共振”：
* **科学 vs 修真（赛博 3 + 仙侠 3）**：激活【机械飞升·灵能同调】，法力充能和攻速互相按比例补正；
* **圣光 vs 深渊（西幻 3 + 旧日 3）**：激活【狂乱狂信】，阵亡队友化身暗影圣骑。

---

## 六、主神空间局外长线元循环 (Meta Hub & Legacy)

单局结束（无论通关还是暴毙）后，数据流转至主神空间：

```mermaid
graph LR
    RunEnd[单局结算] --> CalcPoints[根据推进层数/击杀换算「轮回点数」]
    RunEnd --> LegacySelect[通关奖励: 从本次构筑中挑选 1 枚词条作为「传家宝」]
    
    CalcPoints --> Hub_TechTree[主神光球科技树]
    LegacySelect --> LegacyVault[传家宝保险库 (下局开局可带入 1 枚)]
    
    Hub_TechTree --> UnlockNewHero[解锁全新素体干员]
    Hub_TechTree --> UnlockNewWorld[解锁更高难度与全新主题世界]
    Hub_TechTree --> SocketEnhance[开局素体自带插槽数扩充]
```

* **传家宝机制（Legacy Seal）**：通关最高难度后，玩家可封印当局最满意的 1 枚神级/超武词条存入主神保险库。新一局出发时，允许付出极高代价带入 1 枚作为开局信物，极大地提升了二周目挑战高难度的动力！

---

## 七、LLM 主神沙盒与确定性契约 (LLM Overseer Sandbox)

为保证底层战斗内核的严苛确定性，LLM **严禁直接改写战场变量**，必须通过**“主神仲裁沙盒（Arbitration Sandbox）”**交互：

```
[LLM 动态推演] ──产出──> [结构化 JSON Payload] ──校验──> [Rogue 状态机执行]
```

### 7.1 LLM 必须产出的规范 JSON 范式
```json
{
  "narrative_text": "你在废弃的巨企数据库里发现了一具被飞剑钉在墙上的仿生人机体...",
  "choices": [
    {
      "choice_id": "extract_chip",
      "text": "拔出残存的数据芯片",
      "system_effect": {
        "grant_affix_id": "cyber_neural_overclock_v1",
        "consume_hp_ratio": 0.10
      }
    },
    {
      "choice_id": "pull_sword",
      "text": "尝试拔出这柄古朴的飞剑",
      "system_effect": {
        "battle_encounter_modifier": "elite_ambush_trap",
        "potential_drop_tag": "Shushan"
      }
    }
  ]
}
```
* 如果 LLM 网络超时或解析失败，直接拉取该节点预置的**离线静态奇遇配置**，实现 100% 容灾。

---

## 八、深水区待决策技术与玩法问题清单

这正是目前方案中**最需要进一步推敲完善的硬核细节**：

1. **数值防爆控制（The Math Ceiling）**：
   * 词条如果允许叠属性（比如攻速、减伤、DOT），如何防止“100% 免伤”或“0.1秒满行动条”导致游戏崩溃？
   * *建议*：必须引入双曲递减或硬上限软上限机制（例如减伤上限 75%，行动条充能速度最高 300%）。
2. **插槽装卸自由度**：
   * 词条是“一旦装上就不可拆卸（类似消耗品打孔石）”，还是“可以在非战斗时随时自由拔插、调换给其他队友”？
3. **超武合成配方展示（Recipe Disclosure）**：
   * 配方是完全对玩家公开透明的合成树，还是故意保留神秘感，由玩家在游戏过程中自行尝试摸索（或由主神掉落配方卷轴）？
4. **背包装备容量（Inventory Capacity）**：
   * 未装备在 5 人身上的备用词条，玩家背包允许携带多少件？（带太多会导致选词焦虑，带太少会导致无法凑超武）。
