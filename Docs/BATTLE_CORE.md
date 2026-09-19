# 战斗系统核心机制文档

> **文档定位**：**纯战斗规则集**，不含世界观、构筑系统与养成内容。目标是"照此文档可独立实现一个行为一致的战斗内核"。
> **适用范围**：战斗内核实现、数值调优、AI 行为、无头自动化测试。
> **关联文档**：[DESIGN_DECISIONS.md](DESIGN_DECISIONS.md)（决策总账）、[WORLD_SETTING.md](WORLD_SETTING.md)（命名真值源）、[ROGUE_SYSTEM_ARCHITECTURE.md](ROGUE_SYSTEM_ARCHITECTURE.md)

### v2.0 变更摘要

依据 [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md)，本版相对 v1.0 有大幅删改：

| 决策 | 影响 |
|:---:|---|
| **D-04** 纯全自动 | 删除拖拽下令、托管窗口、蓄力机制、拖拽目标规则。**战斗期间零玩家输入** |
| **D-08** 职业插槽化 | 删除整个「职业天赋体系」章节。`Class` 字段 → `Tags` 集合。**内核不再认识"职业"与"羁绊"** |
| **D-02** 元素环 | 五行改为中性枚举 `Element`，各世界仅换显示表皮 |
| **D-09** 战损 | 复活机制整节重写 ✅ 已闭环（重伤与跨节点继承） |
| **D-20** 叙事 | 彻底废除运行时 LLM 机制，全面确立纯离线确定性事件契约 |

### 标注约定

| 标记 | 含义 |
|:---:|---|
| *（无标记）* | 既定规则 |
| **[补全]** | 实现时必须有明确答案的行为，本版给出默认口径 |
| **[推导]** | 由既有规则计算得出的参考数据 |
| ✅ | **已决策定案**，规格闭环，见 [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md) |

---

## 目录

**A. 基础模型**
1. [机制总览](#一机制总览)
2. [术语与符号](#二术语与符号)
3. [单位属性模型](#三单位属性模型)
4. [战场空间与阵型规则](#四战场空间与阵型规则)

**B. 时间与流程**

5. [战斗生命周期](#五战斗生命周期)
6. [主循环与固定步长](#六主循环与固定步长)
7. [行动条（ATB）](#七行动条atb)

**C. 出手与结算**

8. [资源系统：法力与怒气](#八资源系统法力与怒气)
9. [出手形态：普攻 / 战技 / 大招](#九出手形态普攻--战技--大招)
10. [目标选取规则](#十目标选取规则)
11. [单次出手完整结算管线](#十一单次出手完整结算管线)
12. [数值与公式](#十二数值与公式)
13. [标签与修正器](#十三标签与修正器)
14. [状态效果体系](#十四状态效果体系)

**D. 系统层**

15. [AI 行为](#十五ai-行为)
16. [波次推进与战损](#十六波次推进与战损)
17. [胜负判定](#十七胜负判定)
18. [确定性与随机数](#十八确定性与随机数)
19. [事件流接口](#十九事件流接口)
20. [常量速查表](#二十常量速查表)
21. [扩展机制](#二十一扩展机制)

**附录**

- [附录 A：本版补全的规格决策清单](#附录-a本版补全的规格决策清单)
- [附录 B：已知设计风险](#附录-b已知设计风险)
- [附录 C：实现验证清单](#附录-c实现验证清单)

---

# A. 基础模型

## 一、机制总览

战斗为**实时推进、纯全自动**。战斗期间**没有任何玩家输入**——玩家的全部决策前置到战前构筑与布阵。

核心循环由三条规则咬合：

1. **行动条驱动出手顺序**——每个单位按自身速度实时充能行动条，充满即出手；
2. **资源决定出手形态**——怒气满放大招，否则法力满放战技，否则普攻；
3. **AI 决定目标**——玩家无法在战斗中干预，AI 行为质量直接决定体验。

在此之上叠加两层深度：**元素相克**、**多波次连战**。构筑层的羁绊与遗物**不在内核内**，它们在战前被折算为 `Modifier` 注入（见[第十三节](#十三标签与修正器)）。

所有战斗计算为**确定性纯逻辑**，不依赖渲染帧率与表现层，支持无头模式下的极速模拟与单元测试。确定性的保障条件见[第十八节](#十八确定性与随机数)。

---

## 二、术语与符号

| 符号 / 术语 | 含义 |
|---|---|
| `Gauge` | 行动条，`[0, 1]`，达到 `1.0` 即就绪 |
| `Mana` | 法力，`[0, 100]`，满槽触发**战技** |
| `Rage` | 怒气，`[0, 100]`，满槽触发**大招** ✅ D-04b |
| `Atk` / `Armor` / `Speed` | 攻击力 / 护甲 / 速度 |
| `HpRatio` | 当前生命 ÷ 最大生命 |
| `Elem` | 元素克制系数，取 `1.25 / 1.00 / 0.75` |
| `Tags` | 单位身上的标签集合，由插槽装配派生。**内核只读不判义** |
| `SkillMult` | 技能配置表中的伤害/治疗倍率 |
| `SlowFactor` | 减速系数，钳制于 `[0.2, 1.0]` |
| **就绪（Ready）** | `Gauge >= 1.0` 且未被眩晕/冰冻 |
| **出手（Action）** | 一次完整的普攻、战技或大招，正常消耗满行动条 |
| **额外再动（ExtraAction）** | 由 `Modifier` 触发的追加出手，**不产生资源** |
| **直接攻击伤害** | 由普攻、战技或大招造成的伤害。**不含** DOT 跳伤 |

---

## 三、单位属性模型

### 3.1 静态属性（战斗开始时装载，战中不变）

| 字段 | 类型 | 说明 |
|---|---|---|
| `UnitId` | `int` | 战场内唯一自增 ID，**一切排序的最终 tie-break 键** |
| `Side` | `enum{Player, Enemy}` | 阵营 |
| `Tags` | `IReadOnlySet<string>` | **标签集合**，由插槽装配派生。内核只用于 `Modifier` 匹配，不解释其含义 |
| `Element` | `enum{None, Metal, Wood, Water, Fire, Earth}` | 元素属性 |
| `MaxHp` / `Atk` / `Armor` / `Speed` | `int` | 基础属性。`Atk` 同时作为治疗力 |
| `MoveSpeed` | `float` | 行军移速（我方 `900`，敌方 `800`） |
| `ActiveSkill` | `SkillDef?` | **战技**（法力驱动），来自插槽技能类词条，可为空 |
| `UltimateSkill` | `SkillDef?` | **大招**（怒气驱动），素体本命，不可更换，可为空 ✅ D-04b |

> ⛔ **v1.0 的 `Class` 字段已删除**（D-08）。职业是可装卸的插槽被动，对内核而言只是 `Tags` 里的一个字符串。内核中**不得出现任何 `if (Class == X)` 形式的判断**。

### 3.2 动态状态

| 字段 | 初值 | 说明 |
|---|---|---|
| `Hp` | `MaxHp` | `<= 0` 判定阵亡 |
| `Mana` | `0` **[补全]** | 当前法力 |
| `Rage` | `0` **[补全]** | 当前怒气 |
| `Gauge` | `0` | 当前行动条 |
| `IsExtraAction` | `false` | 本次出手是否为额外再动 |
| `Row` / `Column` | 布阵值 | 排（0=前排, 1=后排）/ 列（0–4） |
| `Position` | `StartLine` | 战场坐标，行军阶段推进 |
| `Alive` | `true` | 存活标记 |
| `Statuses` | `[]` | 状态效果列表 |

> ⛔ **v1.0 的 `ReadyElapsed` 与 `PendingCommand` 字段已删除**（D-04）。无玩家指令，就绪即出手，不存在等待窗口。

### 3.3 派生量（实时计算，不缓存）

```
HpRatio      = Hp / MaxHp
SlowFactor   = clamp(Π(1 - Slow_i.Magnitude), 0.2, 1.0)
IsReady      = Gauge >= 1.0 && !HasStatus(Stun) && !HasStatus(Freeze)
CanCastUlt   = Rage >= 100 && UltimateSkill != null && !HasStatus(Silence)
CanCastSkill = Mana >= 100 && ActiveSkill  != null && !HasStatus(Silence)
```

所有攻防乘区（原 `TalentAtk` / `TalentDR`）现由 `Modifier` 提供，见[第十三节](#十三标签与修正器)。

---

## 四、战场空间与阵型规则

```
           【敌方后排 (Slot 5-9)】  y = +760
           【敌方前排 (Slot 0-4)】  y = +280   (EnemyFrontLine)
===================== 接敌对峙线 Gap = 640 =====================
           【我方前排 (Slot 0-4)】  y = -360   (PlayerFrontLine)
           【我方后排 (Slot 5-9)】  y = -840
```

### 4.1 站位参数

| 参数项 | 标识 | 数值 |
|---|---|---|
| 我方前排基准线 | `PlayerFrontLine` | `-360f` |
| 敌方前排基准线 | `EnemyFrontLine` | `+280f` |
| 接敌安全空隙 | `ContactGap` | `640f` |
| 前后排间距 | `RowSpacing` | `480f` |
| 列间距（X 轴） | `ColumnSpacing` | `160f`（5 槽：`-320, -160, 0, +160, +320`） |
| 起始行军线 | `StartLine` | `±1250f` |

### 4.2 布阵约束 ✅ [D-07](DESIGN_DECISIONS.md)

**已定案**：彻底解除布阵约束，释放 2×5 全部 10 格自由站位。
表现层通过将后排卡牌等比缩放至 0.85，并配合 Y 轴与深度层级排序（Z-Index），彻底消除视觉遮挡，将站位博弈（如 4 前排抗压防刺客切后）完整交还给玩家。

### 4.3 队伍规模 ✅ [D-06](DESIGN_DECISIONS.md)

| 概念 | 数值 |
|---|:---:|
| 阵位容器 | 10 格 |
| **我方出战小队** | **固定 5 人** |
| 结算星级评定 | 按存活比例判定（100% / 50%~80% / <50%） |
| 敌方波次单边人数 | 1 ~ 6 人 |

---

# B. 时间与流程

## 五、战斗生命周期

```mermaid
stateDiagram-v2
    [*] --> Marching: 关卡载入
    Marching --> InContact: 行军至 Gap <= 640

    state InContact {
        [*] --> GaugeAccumulation: 激活 ATB 充能
        GaugeAccumulation --> UnitReady: Gauge >= 1.0
        UnitReady --> AutoAction: AI 自动决策并出手
        AutoAction --> GaugeAccumulation: 出手完毕清空行动条
    }

    InContact --> WaveCleared: 当前波次敌人全灭
    WaveCleared --> NextWaveMarch: 仍有后续波次 (延迟 1.5s)
    NextWaveMarch --> InContact: 切入下一波

    WaveCleared --> Victory: 最后一波清空
    InContact --> Defeat: 我方全灭
    InContact --> Defeat: 战斗限时耗尽
```

> ⛔ v1.0 的 `WipedWindow`（5 秒抢救窗口）状态已删除，见[第十六节](#十六波次推进与战损)。

### 5.1 行军阶段（Marching）

- 双方开局位于 `y = ±1250f` 的屏幕外；
- 先锋单位以 `MoveSpeed` 对向推进（我方 `900px/s`，敌方 `800px/s`）；
- **行军期间行动条锁定为 0**，不得出手；
- **[补全]** 行军期间**不推进状态计时、不结算 DOT**，战斗计时器**照常累加**；
- **[补全]** 先到位的一方原地等待，不获得先手收益。

### 5.2 接敌阶段（Contact）

- 双方前排间距缩小至 `ContactGap <= 640f` 时判定接敌；
- 移速归零，锁定于阵线坐标；
- 全体单位解锁行动条累加。

**[推导]** 行军耗时：我方 `(1250-360)/900 ≈ 0.99s`，敌方 `(1250-280)/800 ≈ 1.21s`，接敌时点约 `1.21s`。

### 5.3 战斗限时 ✅ [D-04c](DESIGN_DECISIONS.md)

**已定案 `90s`**（v1.0 为 `180s`）。零输入观战对时长极度敏感，目标单场时长：

| 战斗类型 | 目标时长 |
|---|---|
| 普通战斗 | **20 ~ 40 秒** |
| Boss 战 | **45 ~ 75 秒** |
| 硬性上限（超时判负） | **90 秒** |

---

## 六、主循环与固定步长

### 6.1 固定步长要求

`BattleSim` **必须以固定步长推进**，不得直接消费渲染层的可变 `delta`。

```
FixedDeltaTime = 1/60f   // 16.667ms

accumulator += frameDelta * speedMultiplier    // 倍速在此处生效
while (accumulator >= FixedDeltaTime) {
    sim.Step(FixedDeltaTime)
    accumulator -= FixedDeltaTime
}
// 单帧内最多连续 Step 20 次，防止卡顿或高倍速下的追帧雪崩
```

> ⚠ **倍速实现铁律**（✅ [D-04c](DESIGN_DECISIONS.md)）：倍速必须通过**加快 `Step` 调用频次**实现，**严禁放大 `FixedDeltaTime`**。后者会改变浮点累加序列，导致 2× 与 1× 打出不同结果——玩家侧表现为"开了倍速就输了"，极难排查。

### 6.2 每步执行顺序

**顺序不可调换**——它决定了同一 tick 内"DOT 致死"与"该单位出手"的优先级等边界行为。

```
Step(dt):
    ── Phase == Marching ────────────────────────────────
    1. 推进双方队列位置
    2. if 前排间距 <= ContactGap: Phase = InContact; Emit(Contact)
    3. BattleTimer += dt
    return

    ── Phase == InContact ───────────────────────────────
    1. 状态计时推进（全体存活单位，UnitId 升序）
       for s in u.Statuses:
           s.Duration -= dt
           if s.IsDot:
               s.NextTickAt -= dt
               while s.NextTickAt <= 0:
                   结算一跳 DOT（见 12.5）
                   s.NextTickAt += s.TickInterval
           if s.Duration <= 0: 移除并 Emit(StatusExpire)
       // DOT 可致死；本步死亡的单位不再参与后续所有阶段

    2. 行动条推进（全体存活单位，UnitId 升序）
       if HasStatus(Stun) || HasStatus(Freeze): continue    // 冻结
       prev = Gauge
       Gauge = min(1.0, Gauge + Speed * SlowFactor * 0.005 * dt)
       if prev < 1.0 && Gauge >= 1.0: Emit(GaugeFull)

    3. 出手服务（构造就绪队列后遍历）
       ready = 存活 && IsReady 的单位
              .OrderByDescending(Gauge)
              .ThenBy(UnitId)
       for u in ready:
           if !u.Alive: continue          // 可能已在本轮被击杀
           ExecuteAction(u)               // 见第十一节。无等待，就绪即出手

    4. 波次 / 胜负判定
       if 敌方全灭: Phase = WaveCleared; WaveTimer = 1.5f
       if 我方全灭: Phase = Defeat
       BattleTimer += dt
       if BattleTimer > BattleTimeLimit: Phase = Defeat
```

> ⛔ v1.0 阶段 3 中"等待玩家指令"的分支已删除（D-04）。`AutoCommandDelay` 常量一并废除——**这使全局战斗节奏加快约 31%**。

**关键边界行为 [补全]**：

- 同一 tick 内**状态计时先于行动条推进**——DOT 造成的死亡会阻止该单位本 tick 出手；
- 减速在本 tick 内**先衰减、后影响充能**；
- 就绪队列在阶段 3 开始时**一次性构造**，遍历中新就绪的单位等到下一 tick 处理。

---

## 七、行动条（ATB）

### 7.1 充能公式

$$\Delta \text{Gauge} = \text{Speed} \times \text{SlowFactor} \times 0.005 \times \Delta t$$

- 速度 200 的单位 1 秒充满一条；
- `SlowFactor` 多层减速**乘法叠加**后钳制于 `[0.2, 1.0]` **[补全]**；
- **眩晕 / 冰冻期间行动条完全静止，已积累值不清空**——解控后立即就绪 **[补全]**；
- 行动条**封顶于 1.0，溢出丢弃** **[补全]**。

### 7.2 速度—节奏对照表 **[推导]**

$$\text{出手周期} = \frac{200}{\text{Speed}}\ \text{秒}$$

| Speed | 出手周期 | 30s 内出手次数 |
|---:|---:|---:|
| 80 | 2.50s | 12 |
| 100 | 2.00s | 15 |
| 150 | 1.33s | 22.5 |
| 200 | 1.00s | 30 |
| 250 | 0.80s | 37.5 |
| 300 | 0.67s | 45 |

> ⚠ v1.0 的周期含 `+0.6s` 托管延迟，本版已移除。

### 7.3 出手顺序展示（纯观战信息）

表现层可展示接下来若干个即将出手的单位，排序规则同 6.2 阶段 3。

> ⛔ **此展示无任何交互功能**（D-04）。v1.0 的"拖拽卡片下令"整套机制已废除。

---

# C. 出手与结算

## 八、资源系统：法力与怒气

### 8.1 法力（Mana）—— 驱动战技

| 规则 | 值 |
|---|---|
| 取值范围 | `[0, 100]`，溢出丢弃 **[补全]** |
| 战斗开始初值 | `0` **[补全]** |
| 普攻回复 | `+30` |
| 战技消耗 | 归零 |
| 受击退火 | `-10`（见 8.3） |
| 额外再动 | **不回复** |
| 跨波次 | **保留** **[补全]** |

**[推导]** `0 → 30 → 60 → 90 → 100(封顶)`，即**每 4 次普攻后第 5 次出手放战技**。速度 200 时首次战技在第 **4 秒**。

> ⚠ **窗口把控**：✅ [D-04c](DESIGN_DECISIONS.md) 将战斗压到 20~40 秒后，攒满法力的窗口更紧张。若杂兵波次在 4 秒内清场，将有大量单位全程放不出战技。**列为首要验证项**，见[附录 B-7](#b-7-技能系统可能形同虚设)。

### 8.2 怒气（Rage）—— 驱动大招 ✅ [D-04b](DESIGN_DECISIONS.md)

以下为推荐口径。若 D-04b 选择删除怒气系统，本节整节移除。

| 规则 | 值 |
|---|---|
| 取值范围 | `[0, 100]`，溢出丢弃 |
| 战斗开始初值 | `0` |
| 普攻回复 | `+10` |
| 战技回复 | `+20` |
| **受击回复** | **`+5`** ✅ D-04b 定案 |
| 大招消耗 | 归零 |
| 额外再动 | **不回复** |
| 跨波次 | **保留** |

**"受击回怒"的设计意图**：v1.0 中怒气只在出手时产出，承伤单位与输出单位的大招频率完全相同。加入受击回怒后，前排形成"挨打 → 开大"的独立节奏，守护流派获得天然正反馈。

**[推导]** 满怒所需（不含受击回怒）：`4 × 10 + 20 = 60` 每 5 次出手 → `100/60 ≈ 1.67` 循环 ≈ **8.3 次出手**。速度 150 时约 **11 秒**一次大招。

### 8.3 受击法力退火

单位受到**直接攻击伤害**时，法力强制回退 10 点：`Mana = max(0, Mana - 10)`。

**[补全]** 触发边界：

| 伤害来源 | 触发退火 |
|---|:---:|
| 普攻 / 战技 / 大招的直接伤害 | ✅ |
| AOE 命中（**每个被命中目标各触发一次**） | ✅ |
| DOT 跳伤 | ❌ |
| 伤害被减免至保底 1 点 | ✅ |
| 该次伤害导致目标阵亡 | ❌ |

> ⛔ v1.0 的"补系免疫退火"硬判断已删除（D-08）。退火免疫现由 `Modifier` 提供——例如支援羁绊档位可提供"退火减半 / 免疫 / 全队免疫"。

**[推导]** 断蓝阈值——单位速度 `S`，被 `n` 个速度 `Se` 的敌人持续攻击：

$$\text{法力净收入} \ge 0 \iff n \le \frac{3S}{Se}$$

**同速下，4 个及以上敌人攻击同一单位时，该单位法力永远停在 0。** 这对前排影响最大，见[附录 B-3](#b-3-前排单位断蓝)。

---

## 九、出手形态：普攻 / 战技 / 大招

### 9.1 形态判定与优先级

```
优先级：大招 > 战技 > 普攻
```

| 形态 | 条件 | 效果 |
|---|---|---|
| **大招** ✅ D-04b | `Rage >= 100` 且有 `UltimateSkill` 且未沉默 | 按配置结算；`Rage → 0` |
| **战技** | `Mana >= 100` 且有 `ActiveSkill` 且未沉默 | 按配置结算；`Mana → 0`；`Rage +20` |
| **普攻** | 其余全部情况 | `1.0 × Atk` 物理伤害；`Mana +30`；`Rage +10` |

**[补全]** 沉默同时封锁战技与大招，但**不阻止普攻，也不阻止回蓝回怒**。法力/怒气封顶后继续普攻，沉默结束的下一次出手立即释放。因此沉默是"延迟"而非"剥夺"。

### 9.2 ⛔ 已废除的机制（D-04）

| 废除项 | v1.0 出处 |
|---|---|
| 拖拽下令 | 9.1 三种指令来源 |
| 托管等待窗口 `AutoCommandDelay` | 9.2 |
| **蓄力机制**（按住 0.8s → ×1.5） | 9.4 |
| 拖拽目标合法性规则 | 10.3 |
| 手动大招与自动/手动开关 | — |

战斗期间**不存在任何玩家输入接口**。内核不得暴露任何接受指令的公开方法。

---

## 十、目标选取规则

| TargetMode | 阵营 | 选取规则 |
|---|:---:|---|
| `Single` | 敌 | 按 [10.1](#101-默认单体目标规则) 规则 |
| `FrontRow` | 敌 | 敌方前排全部存活单位 |
| `BackRow` | 敌 | 敌方后排全部存活单位 |
| `MiddleColumn` | 敌 | 位于中间列（`Column == 2`）的全部存活敌人 |
| `AllEnemies` | 敌 | 敌方全体存活单位 |
| `RandomN` | 敌 | 从存活敌人中随机抽取 N 个 |
| `LowestHpAlly` | 友 | 我方 `HpRatio` 最低的存活单位 |
| `AllAllies` | 友 | 我方全体存活单位 |

### 10.1 默认单体目标规则 **[补全]**

**距离最近优先**：

```
1. 优先前排（Row == 0）存活单位；前排全灭则取后排
2. 同排内按与攻击者的 |Column 差| 升序
3. 完全并列时按 UnitId 升序
```

该规则使攻击者天然优先打击**正对面的同列单位**，避免全体敌人无脑集火同一个前排单位。

### 10.2 通用选取约束 **[补全]**

- **所有模式一律过滤已阵亡单位**；
- 筛选结果为空时**放弃本次出手，但仍清空行动条并结算资源**（避免空转死循环）；
- `RandomN`：**不重复选取**；存活数 `< N` 时取全部；使用 Fisher–Yates 部分洗牌保证确定性；
- `LowestHpAlly`：按 `HpRatio` 升序，并列取 `UnitId` 最小；**不能选中已阵亡单位**。

> ⛔ v1.0 的 10.3「玩家拖拽的目标合法性」整节已删除（D-04）。

---

## 十一、单次出手完整结算管线

### 11.1 出手主流程

```
ExecuteAction(u):
    isExtra = u.IsExtraAction;  u.IsExtraAction = false

    ── 1. 形态判定（优先级：大招 > 战技 > 普攻）──
    if u.CanCastUlt:        mode = Ultimate; skill = u.UltimateSkill
    elif u.CanCastSkill:    mode = Active;   skill = u.ActiveSkill
    else:                   mode = Basic;    skill = null

    ── 2. 目标解析 ─────────────────────────────
    targetMode = (skill != null) ? skill.TargetMode : Single
    targets = ResolveTargets(u, targetMode)
    if targets.IsEmpty: goto 5           // 空目标：空过但仍结算资源

    ── 3. 逐目标结算（targets 按 UnitId 升序）──
    for t in targets:
        if !t.Alive: continue            // 前序目标的连锁效果可能已击杀
        if 效果为治疗: ApplyHeal(u, t, ...)
        else:          ApplyDamage(u, t, ...)

    ── 4. Modifier 挂钩：出手后附加效果 ─────────
    // 例如「射手」羁绊的普攻附加流血，由 Modifier 在此挂钩实现
    Modifiers.OnAfterAction(u, mode, targets)

    ── 5. 资源结算 ─────────────────────────────
    switch mode:
        Ultimate: u.Rage = 0
        Active:   u.Mana = 0
                  if !isExtra: u.Rage = min(100, u.Rage + 20)
        Basic:    if !isExtra:
                      u.Mana = min(100, u.Mana + 30)
                      u.Rage = min(100, u.Rage + 10)

    ── 6. Modifier 挂钩：额外再动判定 ───────────
    // 内核只提供挂钩点，概率由 Modifier 提供，默认 0
    if u.Alive && Modifiers.RollExtraAction(u, Rng):
        u.Gauge = 1.0
        u.IsExtraAction = true           // 可连锁
        Emit(ExtraAction, u)
    else:
        u.Gauge = 0
```

> ⛔ **v1.0 的第 2 步「蓄力判定」与第 5 步「施法者职业天赋」已删除**（D-04 / D-08）。原先硬编码的"弓系 25% 流血"与"速系 30% 再动"现由 `Modifier` 在第 4、6 步挂钩实现，内核不再知道这些效果的来源。

**[补全]** 关键裁定：

| 情形 | 裁定 |
|---|---|
| 额外再动能否释放战技/大招 | **能**。`isExtra` 只屏蔽资源**产出** |
| 额外再动能否再次触发再动 | **能，可连锁**。概率 `p` 时期望出手 `1/(1-p)` 次 |
| 大招是否消耗法力 | **否**，两条资源轨道互相独立 |
| 目标在遍历中途死亡 | 跳过，不结算 |
| 无合法目标 | 空过，但照常清空行动条并结算资源 |

### 11.2 单目标伤害结算

```
ApplyDamage(src, tgt, skillMult, skillPen, statusesToApply):

    ── 1. 攻击侧乘区 ───────────────────────────
    elem   = ElementCoef(src.Element, tgt.Element)        // 1.25 / 1.00 / 0.75
    atkMod = SoftClamp(Modifiers.GetAtkMultiplier(src, tgt), 3.0, 5.0)   // ✅ D-13
    raw    = src.Atk * atkMod * skillMult * elem

    ── 2. 防御减免（连续穿透）────────────────── ✅ D-24/B-13
    pen      = min(1.0, skillPen + Modifiers.GetArmorPenetration(src, tgt))
    armor    = Modifiers.GetEffectiveArmor(tgt) * (1 - pen)
    K        = 800 + 20 * tgt.Level                      // ✅ D-24/B-6
    mitigated = raw * (1 - armor / (armor + K))

    ── 3. 受伤乘区 ─────────────────────────────
    dr    = Modifiers.GetDamageTakenMultiplier(tgt, src)
    dr    = 1 - SoftClamp(1 - dr, 0.50, 0.75)            // ✅ D-13 减伤上限 75%
    final = mitigated * dr

    ── 4. 取整与保底 ───────────────────────────[补全]
    final = max(1, (int)floor(final))                    // 仅在此处取整一次

    ── 5. 扣血 ─────────────────────────────────
    tgt.Hp -= final
    Emit(Damage{src, tgt, final, elem})

    ── 6. 受击退火与回怒 ───────────────────────
    if tgt.Hp > 0:
        burn = Modifiers.GetManaBurnAmount(tgt)           // 默认 10，可被减免至 0
        tgt.Mana = max(0, tgt.Mana - burn)
        tgt.Rage = min(100, tgt.Rage + 5)                 // ✅ D-04b 受击回怒

    ── 7. 附加状态 ─────────────────────────────
    if tgt.Hp > 0:
        for s in statusesToApply: ApplyStatus(tgt, s, from: src)

    ── 8. 死亡判定 ─────────────────────────────
    if tgt.Hp <= 0:
        tgt.Hp = 0; tgt.Alive = false
        清空 tgt.Statuses（含全部 DOT）                    // [补全]
        Emit(UnitDeath{tgt})
```

**[补全]** 取整必须在步骤 4 **一次性完成**，中间量保持 `float` 全精度——这是跨平台确定性的必要条件。

### 11.3 治疗结算 **[补全]**

```
ApplyHeal(src, tgt, healMult):
    amount = src.Atk * healMult
           * Modifiers.GetHealingDone(src)        // ✅ D-12
           * Modifiers.GetHealingReceived(tgt)    // ✅ D-12
    amount = max(1, (int)floor(amount))
    actual = min(amount, tgt.MaxHp - tgt.Hp)      // 溢出丢弃
    tgt.Hp += actual
    Emit(Heal{src, tgt, actual, overheal: amount - actual})
```

- 治疗**不受**元素、护甲、受伤乘区影响；
- 治疗**不能**复活已阵亡单位；
- 治疗**不触发**目标的受击退火与回怒。

---

## 十二、数值与公式

### 12.1 元素相克

✅ [D-02](DESIGN_DECISIONS.md)。内核枚举为唯一真值，各世界仅换显示表皮（映射表见 [WORLD_SETTING.md](WORLD_SETTING.md) 第四节）。

```
Fire → Metal → Wood → Earth → Water → Fire
```

| 关系 | `Elem` |
|---|:---:|
| 攻击方克制受击方 | `1.25` |
| 受击方克制攻击方 | `0.75` |
| 无克制 / 任一方 `None` | `1.00` |

**[补全]** 元素系数**仅作用于直接攻击伤害**，不作用于 DOT 与治疗。

**[推导]** 同一对单位互打的伤害比 `1.25 / 0.75 ≈ 1.67`——元素摆幅大于绝大多数单条羁绊效果。

### 12.2 伤害公式 ✅ [D-24/B-13](DESIGN_DECISIONS.md)

$$\text{Raw} = \text{Atk} \times \text{AtkMod} \times \text{SkillMult} \times \text{Elem}$$

**护甲穿透为连续量**（不再是"绝对伤害"二元开关）：

$$\text{EffArmor} = \text{Armor} \times (1 - \text{Pen})$$

$$\text{Final} = \text{Raw} \times \left(1 - \dfrac{\text{EffArmor}}{\text{EffArmor} + K}\right) \times \text{DmgTakenMod}$$

- $\text{Pen} \in [0, 1]$ —— 护甲穿透率，由技能配置与 `Modifier` 累加后钳制；
- $\text{Pen} = 0$ 为常规攻击，$\text{Pen} = 1$ 等价于旧的"绝对伤害"；
- **[补全]** 多来源穿透**加法叠加**后钳制于 `[0, 1]`：$\text{Pen} = \min(1, \sum \text{Pen}_i)$。

> ⛔ v1.0 的 `Charge`（蓄力）乘区已删除（D-04）。
> ⛔ v2.0 的"绝对伤害"二元分支已删除（D-24/B-13）。旧配置表中的 `isAbsolute = true` 迁移为 `Pen = 1.0`。

**[推导]** 穿透收益参考（目标护甲 3000，`K` 按 12.3 取 3000）：

| `Pen` | 有效护甲 | 免伤率 | 相对 `Pen=0` 的伤害倍率 |
|---:|---:|---:|---:|
| 0% | 3000 | 50.0% | ×1.00 |
| 30% | 2100 | 41.2% | ×1.18 |
| 50% | 1500 | 33.3% | ×1.33 |
| 70% | 900 | 23.1% | ×1.54 |
| 100% | 0 | 0.0% | ×2.00 |

对比旧二元开关的 ×3 跳变，连续量把"必带/废品"变成了可调旋钮。

### 12.3 护甲收益曲线 ✅ [D-24/B-6](DESIGN_DECISIONS.md)

**`K` 随等级线性缩放**（不再是固定 1500）：

$$K = 800 + 20 \times \text{Level}$$

| 等级 | `K` | 达成 50% 免伤所需护甲 |
|:---:|---:|---:|
| 1 | 820 | 820 |
| 15 | 1100 | 1100 |
| 35 | 1500 | 1500 |
| 60 | 2000 | 2000 |
| 100 | 2800 | 2800 |

**免伤率由「护甲 ÷ K」的比值决定**，而非护甲绝对值：

| 护甲 / K | 免伤率 | 等效生命倍率 |
|---:|---:|---:|
| 0 | 0.0% | ×1.00 |
| 0.5 | 33.3% | ×1.50 |
| **1.0** | **50.0%** | ×2.00 |
| 2.0 | 66.7% | ×3.00 |
| 3.0 | 75.0% | ×4.00 |

曲线永不触及 100%。

> **[补全]** `Level` 取**受击方**的等级。取攻击方会导致高等级单位打低等级时护甲凭空变强，反直觉。
> **[补全]** 敌方单位无等级概念时，取该波次配置的 `WaveLevel`。

### 12.4 DOT 跳伤公式 ✅ [D-24/B-8](DESIGN_DECISIONS.md)

**混合公式**（百分比最大生命 + 施法者攻击力）：

```
DotDamage = max(1, (int)floor(
    (tgt.MaxHp * pct + src.Atk * atkCoef) * DmgTakenMod
))
```

| 参数 | 默认值 | 说明 |
|---|:---:|---|
| `pct` | 见 14.3 各状态表 | 目标最大生命百分比项，提供抗数值膨胀的底线 |
| `atkCoef` | `0.20` | 施法者攻击力系数，提供成长反馈 |

**设计意图**：百分比项保证 DOT 对高血量目标始终有效（抗膨胀），攻击力项保证玩家升级装备后 DOT 会变强（成长可见）。v2.0 的纯百分比公式使满级与 1 级的流血完全等值，是成长反馈的硬伤。

**[补全]** `src` 已阵亡时，`atkCoef` 项按其**死亡瞬间**的 `Atk` 快照计算（DOT 施加时记入 `StatusEffect.SourceAtkSnapshot`），避免读取已销毁单位。

| 特性 | 裁定 |
|---|:---:|
| 受护甲 / 穿透影响 | ❌ |
| 受元素影响 | ❌ |
| 受受伤乘区影响 | ✅ |
| 触发退火 / 回怒 | ❌ |
| 可致死 | ✅ |
| **随施法者攻击力成长** | **✅**（`atkCoef` 项） |
| 受 DOT 总量上限约束 | ✅ 见 12.6 |

### 12.5 数值防爆软钳制 ✅ [D-13](DESIGN_DECISIONS.md)

内核在各乘区出口统一施加软钳制，杜绝构筑后期数值破表。

| 项 | 硬上限 `Cap` | 软阈值 `Soft` |
|---|:---:|:---:|
| 总减伤率 | `0.75` | `0.50` |
| 行动条充能倍率 | `3.00` | `2.00` |
| 攻击总乘区 | `5.00` | `3.00` |
| `HealingReceived` | `[0.20, 3.00]` | `2.00`（上行） |
| DOT 合计每秒扣血 | `0.15 × MaxHp` | `0.10 × MaxHp` |

**钳制算法**——软阈值以下**不惩罚**，仅衰减超出部分：

```
SoftClamp(raw, soft, cap):
    if raw <= soft:  return raw
    return soft + (cap - soft) * (1 - soft / raw)
```

**[推导]** 攻击乘区（`Soft = 3.0`, `Cap = 5.0`）验算：

| 原始乘区 | 生效乘区 |
|---:|---:|
| 1.0 | **1.00**（无加成不受惩罚） |
| 3.0 | **3.00**（阈值处连续，无跳变） |
| 4.0 | 3.50 |
| 6.0 | 4.00 |
| 12.0 | 4.50 |
| → ∞ | → 5.00（永不触及） |

> ⚠ **[补全]** 不得采用 `cap × (1 - 1/(1 + raw/cap))` 这类作用于整体的形式——该形式在 `raw = 1`（无任何加成）时返回 `0.833`，会凭空削弱基础值 17%，且 `raw = cap` 时只给一半。软钳制必须满足两个性质：**低值区恒等**（`raw ≤ soft` 时 `f(raw) = raw`）与**阈值处连续**。

### 12.6 DOT 总量上限

同一单位身上所有 DOT 的**每秒合计扣血**受 12.5 的上限约束：

```
totalDotPerSec = Σ (DotDamage_i / TickInterval_i)
若 totalDotPerSec > SoftClamp 上限，则按比例缩放各 DOT 的本次跳伤
```

**[补全]** 缩放按各 DOT 的 `UnitId → StatusType` 确定顺序遍历，保证确定性。

---

## 十三、标签与修正器

> 本节取代 v1.0 的「六职业天赋体系」（D-08 已整节删除）。

### 13.1 核心原则

**内核不认识"职业"，也不认识"羁绊"。**

单位身上只有一个 `Tags` 字符串集合（由插槽装配派生），以及一份从外部注入的 `Modifiers` 列表。内核**只做两件事**：

1. 在管线的固定挂钩点回调 `Modifiers`；
2. 把 `Tags` 原样提供给 `Modifiers` 用于匹配。

内核中**不得出现任何针对具体标签值的条件判断**。

### 13.2 挂钩点清单

| 挂钩 | 调用位置 | 返回/作用 |
|---|---|---|
| `GetAtkMultiplier(src, tgt)` | 11.2 步骤 1 | 攻击乘区（出口经 `SoftClamp`） |
| `GetArmorPenetration(src, tgt)` | 11.2 步骤 2 | **护甲穿透率**，加法叠加后钳制 `[0,1]` ✅ D-24/B-13 |
| `GetEffectiveArmor(tgt)` | 11.2 步骤 2 | 有效护甲（穿透前） |
| `GetDamageTakenMultiplier(tgt, src)` | 11.2 步骤 3 | 受伤乘区（出口经 `SoftClamp`，减伤上限 75%） |
| `GetManaBurnAmount(tgt)` | 11.2 步骤 6 | 退火量（默认 10） |
| `GetHealingDone / Received` | 11.3 | 治疗乘区 ✅ D-12 |
| `OnAfterAction(u, mode, targets)` | 11.1 步骤 4 | 出手后附加效果（如附加 DOT） |
| `RollExtraAction(u, rng)` | 11.1 步骤 6 | 额外再动概率（默认 0） |
| `GetManaRegenPerSec(u)` | 6.2 阶段 2 | 法力自然回复 ✅ D-11 |
| `GetGaugeRateMultiplier(u)` | 6.2 阶段 2 | 行动条充能倍率（上限 300%）✅ D-13 |
| `GetLifestealRatio(src, tgt)` | 11.2 步骤 5 后 | 生命吸取比例 ✅ [21.2](#212-生命吸取lifesteal) |
| `GetReflectRatio(tgt, src)` | 11.2 步骤 5 后 | 反射伤害比例 ✅ [21.3](#213-反射伤害reflect) |
| `GetExecuteThreshold(src, tgt)` | 11.2 步骤 8 前 | 阈值斩杀线 ✅ [21.6](#216-阈值斩杀execute) |
| `GrantShield(u, amount, cap)` | Tick / 事件 | 授予护盾 ✅ [21.1](#211-护盾shield) |
| `OnBattleStart(units)` | 战斗初始化 | 初始资源、属性修正 |

> **[补全]** 所有返回乘区的挂钩，其**求和/求积在 `Modifier` 侧完成，钳制在内核出口完成**。`Modifier` 不得自行钳制，否则多来源叠加时上限会被重复施加。

### 13.3 原六职业天赋的去向

v1.0 中硬编码在内核的六条天赋，现全部由羁绊档位经 `Modifier` 提供 ✅ [D-08a](DESIGN_DECISIONS.md)（详见 [AFFIX_AND_SYNERGY.md](AFFIX_AND_SYNERGY.md)）：

| v1.0 个体天赋 | 现挂钩 |
|---|---|
| 防 25% 免伤 | `GetDamageTakenMultiplier` |
| 近 残血增伤 | `GetAtkMultiplier` |
| 速 30% 再动 | `RollExtraAction` |
| 弓 25% 流血 | `OnAfterAction` |
| 术 蓄力 ×1.5 | **已废除**（D-04），改为战技法力阈值降低 |
| 补 免疫退火 | `GetManaBurnAmount` → 0 |

### 13.4 ⚠ 确定性约束

`Modifier` 的执行必须满足：

1. **排序确定**——多个 `Modifier` 作用于同一挂钩点时，按注入顺序执行，注入顺序由 Rogue 层按确定规则生成；
2. **纯函数**——不得读取未排序集合，不得使用 `BattleRng` 以外的随机源；
3. **乘区可交换**——所有乘法型 `Modifier` 应满足交换律，避免顺序影响结果。

---

## 十四、状态效果体系

### 14.1 状态实例结构

```
StatusEffect {
    Type         : enum{Stun, Freeze, Silence, Slow, Burn, Poison, Bleed}
    Duration     : float
    Magnitude    : float      // 仅 Slow 使用
    TickInterval : float      // DOT 跳伤间隔，非 DOT 为 0
    NextTickAt   : float
    SourceId     : int
}
```

### 14.2 控制类

| 状态 | 枚举 | 行为 | 行动条 |
|:---:|:---:|---|:---:|
| **眩晕** | `Stun` | 无法出手 | 冻结（保留已积累值） |
| **冰冻** | `Freeze` | 无法出手与施法 | 冻结（保留已积累值） |
| **沉默** | `Silence` | 允许普攻与回蓝回怒，禁止战技与大招 | 正常累加 |
| **减速** | `Slow` | 削减移速与行动条累加速率（`Magnitude ≈ 0.30`） | 按 `SlowFactor` 折减 |

**机制类状态** ✅ [第二十一节](#二十一扩展机制)——不可驱散、不受 DR 约束、不产生 DOT：

| 状态 | 枚举 | 行为 |
|:---:|:---:|---|
| **无敌** | `Invulnerable` | 伤害管线最前置拦截，完全免疫（含 DOT） |
| **隐匿** | `Stealth` | 无法被敌方 `Single` 模式选中；AOE 仍命中；主动出手后立即解除 |
| **嘲讽** | `Taunt` | 强制被嘲讽者的 `Single` 目标指向嘲讽者 |

### 14.3 持续伤害类（DOT）

| 状态 | 持续 | 间隔 | 每跳 | 总量 |
|:---:|:---:|:---:|:---:|:---:|
| **火烧** `Burn` | 6s | 2s | 5% MaxHp | 15% |
| **中毒** `Poison` | 10s | 2s | 4% MaxHp | 20% |
| **流血** `Bleed` | 6s | 2s | 5% MaxHp | 15% |

> ⚠ **已定案**（✅ D-04c / D-24）：中毒持续时间重定为 10 秒，每 2 秒一跳（共 5 跳），每跳造成 4% 最大生命伤害（总额 20%），与 20~40 秒快节奏战斗完全匹配。

### 14.4 叠加与刷新规则 **[补全]**

**同类型全局唯一**：

```
ApplyStatus(tgt, newStatus):
    existing = tgt.Statuses.Find(s => s.Type == newStatus.Type)
    if existing == null:
        tgt.Statuses.Add(newStatus)
        newStatus.NextTickAt = newStatus.TickInterval    // 不立即跳伤
        Emit(StatusApply)
    else:
        existing.Duration  = max(existing.Duration, newStatus.Duration)
        existing.Magnitude = max(existing.Magnitude, newStatus.Magnitude)
        existing.SourceId  = newStatus.SourceId
        // NextTickAt 不重置——刷新不打断跳伤节奏
```

- 同类型**不叠加层数**，后施加者刷新时长并取较高强度；
- **不同类型**可任意共存。

### 14.5 硬控递减（DR）✅ [D-24/B-9](DESIGN_DECISIONS.md)

> v2.0 无 DR，存在"多控制单位永久锁死玩家核心素体"的风险。全自动下玩家连"手动打断"这个最后手段都没有，只能眼睁睁看着，是最劣质的失败体验。本版引入强制 DR。

**适用范围**：仅**硬控**类状态（`Stun` / `Freeze`）。`Silence` 与 `Slow` 不受 DR 约束（它们不剥夺行动权）。

**递减阶梯**：

| 该单位本轮第 N 次受到硬控 | 生效时长系数 |
|:---:|:---:|
| 1 | `100%` |
| 2 | `50%` |
| 3 | `25%` |
| 4 及以后 | **`0%`（完全免疫）** |

**DR 计数器规则 [补全]**：

```
每个单位持有：DrStacks: int，DrResetAt: float

ApplyHardCC(tgt, duration):
    if tgt.DrStacks >= 3:
        return                              // 免疫，不施加，不刷新计时
    factor = [1.0, 0.5, 0.25][tgt.DrStacks]
    ApplyStatus(tgt, Stun/Freeze, duration * factor)
    tgt.DrStacks += 1
    tgt.DrResetAt = DrWindow                // 每次成功施加都重置窗口

// 主循环阶段 1 内递减
tgt.DrResetAt -= dt
if tgt.DrResetAt <= 0 && tgt.DrStacks > 0:
    tgt.DrStacks = 0
    Emit(DrReset, tgt)
```

| 常量 | 值 | 说明 |
|---|:---:|---|
| `DrWindow` | **`5.0s`** | 自最后一次成功施加硬控起计时，期间无新硬控则清零 |
| `DrMaxStacks` | `3` | 第 4 次起完全免疫 |

**[推导]** 最坏情况下单位的连续失控上限：假设初始硬控 2 秒，

```
2.0s (100%) + 1.0s (50%) + 0.5s (25%) = 3.5s，随后 5 秒内完全免疫
```

即**任何情况下失控不超过 3.5 秒**，且之后必有至少 5 秒的免疫窗口。这与 ✅ D-04c 的 20~40 秒战斗时长相容（最差失控占比约 17%）。

**[补全]** DR 计数器**跨波次清零**（与状态清空规则一致），但**不跨复活**——D-09 已废除局内复活，此条仅为将来预留。

### 14.6 其他裁定 **[补全]**

| 情形 | 裁定 |
|---|---|
| 施加时是否立即跳一次 DOT | **否**，首跳在 `TickInterval` 后 |
| 眩晕/冰冻期间状态计时是否流逝 | **是** |
| 眩晕/冰冻期间 DOT 是否继续跳 | **是** |
| 单位阵亡时状态处理 | **全部清空** |
| 状态是否跨波次保留 | **否，波次切换时清空** |
| **控制递减（DR）** | ✅ **已引入**，见 [14.5](#145-硬控递减dr--d-24b-9)。硬控 `100%/50%/25%/免疫`，`DrWindow = 5s` |
| DR 计数器是否跨波保留 | **否，跨波清零** |
| `Silence` / `Slow` 是否受 DR | **否**（不剥夺行动权） |

---

# D. 系统层

## 十五、AI 行为

✅ [D-04d](DESIGN_DECISIONS.md)。**全自动下 AI 质量直接等于游戏质量**——引入战前 Gambit 战术指令系统（每素体 2 槽，条件+动作），详细规则见 [TACTICS.md](TACTICS.md)。未配置战术或条件未满足时回退至以下基线流程：

### 15.1 基线决策流程（当前实现）

```
AiDecide(u):
    // 形态判定已在 ExecuteAction 中完成，AI 只负责目标偏好
    // 技能类：目标由 TargetMode 自动解析
    // 普攻：按 10.1 默认单体目标规则（最近优先）
```

### 15.2 ⚠ 基线 AI 的已知缺陷

| 缺陷 | 后果 |
|---|---|
| 满资源即放，无时机判断 | 大招砸在残血小怪身上 |
| `LowestHpAlly` 无阈值 | 满血时也会放治疗，浪费一次出手 |
| 无 AOE 目标数判断 | 群体技能打单个目标 |
| 无威胁评估 | 不会集火敌方治疗或高威胁单位 |

### 15.3 战前战术指令系统 (Gambit) ✅ [D-04d](DESIGN_DECISIONS.md)

已正式采纳**战前战术指令配置（Gambit 战术指令）**，完整规格见独立文档 [TACTICS.md](TACTICS.md)：
- 战前为每位素体开放 2 个战术指令槽；
- 首发 16 条原子战术指令（涵盖集火斩杀、越顶狙杀、打断吟唱、残血急救等）；
- 执行管线：按【指令槽 1 → 指令槽 2 → 内置默认 AI】自上而下求值，首个真值短路执行。这一机制在维持 100% 战斗全自动的前提下，彻底解决了全自动下 AI 愚蠢决策的痛点。

---

## 十六、波次推进与战损

### 16.1 波次转场

```
1. 击杀当波全部敌方单位 → Emit(WaveClear)
2. 挂起缓冲 1.5 秒
3. 若无后续波次 → Victory
4. 生成下一波次 SpawnWave()
5. 重新执行 行军 → 接敌 流程
```

**[补全]** 转场时我方单位的状态处理：

| 项 | 处理 |
|---|---|
| 生命值 / 法力 / 怒气 | **保留** |
| 行动条 | **清零** |
| 全部状态效果（含 DOT） | **清空** |
| 位置 | 重置回 `StartLine`，重新行军 |
| 已阵亡单位 | **保持阵亡** |

### 16.2 战损模型 ✅ [D-09](DESIGN_DECISIONS.md)

> ⛔ **v1.0 的「5 秒抢救窗口 + 行军包子复活」整套机制已废除**。该机制为单场手操战斗设计，在肉鸽长线结构中不成立（它使 180s 限时形同虚设，且让星级评定可被道具购买）。

**落地口径（方案 B）**：

| 项 | 规则 |
|---|---|
| 战斗内阵亡 | 单位进入**重伤**状态，本局内不可上阵 |
| 血量 | **跨节点继承**，不自动回满 |
| 救治 | 在营地/商店消耗**信用点**，费用随该素体累计重伤次数递增 |
| 词条 | ✅ [D-09a](DESIGN_DECISIONS.md) **不锁定**，可拔下转给替补 |
| 局内失败 | 可上阵素体不足 5 人且无力救治 |

**内核职责边界**：内核**只负责报告单场战斗的终态**（各单位剩余 HP、是否阵亡），重伤状态、救治、跨节点继承全部由 Rogue 层管理。

---

## 十七、胜负判定

### 17.1 胜利

清空最后一波敌人即胜利，按结算瞬间我方**存活比例**评定星级 ✅ [D-06](DESIGN_DECISIONS.md)：

| 星级 | 条件 |
|:---:|---|
| ★★★ | 全员存活（100%） |
| ★★ | 存活 50% ~ 80% |
| ★ | 存活 < 50% |

> 改用比例而非绝对人数，使将来调整队伍规模时无需再改评定规则。

### 17.2 失败

| 条件 |
|---|
| 我方全灭 |
| 战斗计时器超过 `BattleTimeLimit`（✅ `90s`，[D-04c](DESIGN_DECISIONS.md)） |

**[补全]** 同一 tick 内既满足"敌方全灭"又满足"我方全灭"（AOE 同归于尽）时，**判定为胜利**——玩家侧优先。

---

## 十八、确定性与随机数

### 18.1 随机数

```
class BattleRng {
    // 单一实例，由 BattleSim 持有
    // seed 由 BattleContext.Seed 提供
    uint  NextUInt()
    float NextFloat()            // [0, 1)
    int   Range(int min, int maxExclusive)
}
```

| 约束 | 说明 |
|---|---|
| **单一 RNG 实例** | 整场战斗只有一个随机源 |
| **禁用引擎随机** | 不得使用 `UnityEngine.Random` / `GD.Randf` / `Random.Shared` |
| **调用顺序即状态** | 遍历顺序必须完全确定 |
| **表现层不得消费** | 特效抖动、飘字偏移必须使用独立的表现层 RNG |
| **Modifier 亦受约束** | `Modifier` 内的随机必须走 `BattleRng` |

### 18.2 顺序确定性

| 环节 | 排序键 |
|---|---|
| 状态计时推进 | `UnitId` 升序 |
| 行动条推进 | `UnitId` 升序 |
| 就绪队列服务 | `Gauge` 降序 → `UnitId` 升序 |
| 多目标伤害结算 | `UnitId` 升序 |
| `RandomN` 抽样 | Fisher–Yates，输入集按 `UnitId` 升序 |
| `LowestHpAlly` 并列 | `UnitId` 升序 |
| **`Modifier` 执行** | 注入顺序（由 Rogue 层确定性生成） |
| **`PipelineInterceptor` 执行** | `AuthorityLevel` 降序 → `UnitId` 升序 ✅ D-19 |

### 18.3 浮点确定性

- 固定步长 `1/60f`，禁止消费可变 `delta`；
- **倍速通过加快 `Step` 调用频次实现，严禁放大 `FixedDeltaTime`**；
- 伤害计算中间量保持 `float` 全精度，**仅在最终扣血前取整一次**；
- 禁用 `Math.Pow` / 三角函数等平台实现可能不一致的函数。

### 18.4 可复现性检验

相同 `(seed, 关卡配置, 队伍配置, Modifier 列表)` 必须产出**逐事件完全一致**的事件流。建议作为 CI 回归测试基线。

**倍速一致性测试**：同一 seed 在 1× / 2× / 4× 下必须产出完全相同的事件流。

---

## 十九、事件流接口

内核单向产出事件，表现层单向消费，**表现层不得回写内核状态**。

| 事件 | 载荷 |
|---|---|
| `Contact` | — |
| `GaugeFull` | `unitId` |
| `ExtraAction` | `unitId` |
| `Damage` | `srcId, tgtId, amount, elemCoef, mode` |
| `Heal` | `srcId, tgtId, amount, overheal` |
| `DotTick` | `tgtId, statusType, amount, sourceId` |
| `ManaBurn` | `tgtId, amount` |
| `RageGain` | `unitId, amount, reason` ✅ D-04b |
| `UltimateCast` | `unitId, skillId` ✅ D-04b |
| `StatusApply` / `StatusExpire` | `tgtId, statusType[, duration]` |
| `UnitDeath` | `unitId` |
| `WaveClear` / `WaveStart` | `waveIndex[, isFinalWave]` |
| `BattleEnd` | `result, survivorRatio` |

> ⛔ v1.0 的 `WipedWindowOpen` 与 `UnitRevived` 事件已删除（D-09）。

**[补全]** 事件在内核状态**已变更后**立即入队，同一 `Step()` 内保持产生顺序，表现层在该 `Step()` 返回后统一消费。

---

## 二十、常量速查表

| 类别 | 常量 | 数值 | 状态 |
|---|---|---|:---:|
| **空间** | `PlayerFrontLine` / `EnemyFrontLine` | `-360f` / `+280f` | |
| | `ContactGap` | `640f` | |
| | `RowSpacing` / `ColumnSpacing` | `480f` / `160f` | |
| | `StartLine` | `±1250f` | |
| | 单排上阵上限 | 无限制（释放 2×5 全部 10 格） | ✅ D-07 |
| | 我方队伍人数 | 固定 5 人 | ✅ D-06 |
| **时间** | `FixedDeltaTime` | `1/60f` | |
| | `BattleTimeLimit` | **`90s`**（v1.0 为 180s） | ✅ D-04c |
| | 波次转场缓冲 | `1.5s` | |
| | ~~`AutoCommandDelay`~~ | **已废除** | D-04 |
| **行军** | 我方 / 敌方 `MoveSpeed` | `900` / `800` px/s | |
| **ATB** | `GaugePerSpeedUnit` | `0.005` | |
| | `Gauge` 范围 / 就绪阈值 | `[0, 1]` / `1.0` | |
| | `SlowFactor` 钳制区间 | `[0.2, 1.0]` | |
| **资源** | `Mana` / `Rage` 范围 | `[0, 100]` | |
| | 开局 `Mana` / `Rage` | `0` / `0` | |
| | 战技法力阈值 / 大招怒气阈值 | `100` / `100` | ✅ D-04b |
| | 普攻回法 / 回怒 | `+30` / `+10` | |
| | 战技回怒 | `+20` | |
| | 受击回怒 | `+5` | ✅ D-04b |
| | 受击法力退火 | `-10`（可被 Modifier 减免） | |
| | ~~蓄力时长 / 倍率~~ | **已废除** | D-04 |
| **伤害** | 护甲减免常数 `K` | `K = 800 + 20 × Level`（受击方等级；基线 1500@Lv35） | ✅ D-24/B-6 |
| | 护甲穿透 `Pen` | 连续量 `[0,1]`，多来源加法叠加后钳制 | ✅ D-24/B-13 |
| | ~~绝对伤害二元开关~~ | **已废除**，迁移为 `Pen = 1.0` | ✅ D-24/B-13 |
| | 伤害保底 / 取整 | `max(1, floor(x))` | |
| | 元素克制 / 被克 | `1.25` / `0.75` | |
| **软钳制** | 攻击总乘区 `Soft/Cap` | `3.0 / 5.0` | ✅ D-13 |
| | 总减伤率 `Soft/Cap` | `0.50 / 0.75` | ✅ D-13 |
| | 充能倍率 `Soft/Cap` | `2.0 / 3.0` | ✅ D-13 |
| | `HealingReceived` 区间 | `[0.20, 3.00]`，上行 `Soft = 2.0` | ✅ D-13 |
| | DOT 合计每秒扣血 `Soft/Cap` | `0.10 / 0.15 × MaxHp` | ✅ D-13 |
| | 钳制算法 | `raw ≤ soft → raw`；否则 `soft + (cap-soft)×(1 - soft/raw)` | ✅ D-13 |
| **DOT** | Tick 间隔 | `2s`（首跳延后一个间隔） | |
| | 跳伤公式 | `MaxHp × pct + Atk × atkCoef` | ✅ D-24/B-8 |
| | `atkCoef` | `0.20` | ✅ D-24/B-8 |
| | 火烧 / 流血 | 6s，每跳 5% MaxHp | |
| | 中毒 | **10s，每跳 4% MaxHp**（共 5 跳） | ✅ D-24/B-14 |
| | 叠加规则 | 同类型唯一，取长取强 | |
| **扩展机制** | `Shield` 初值 / 衰减 / 跨波 | `0` / 不衰减 / 清空 | ✅ D-27 |
| | 护盾完全吸收时是否退火回怒 | **仍触发** | ✅ D-27 |
| | 生命吸取：DOT / 反射是否触发 | **均不触发** | ✅ D-27 |
| | 反射伤害标记 | `IsReflected`，不再触发反射/吸血/退火 | ✅ D-27 |
| | 斩杀触发源 | **仅直接伤害**，DOT 不触发 | ✅ D-27 |
| | 隐匿：AOE 是否命中 | **命中**（仅过滤 `Single`） | ✅ D-27 |
| | 嘲讽实现 | 强制目标覆盖，**无威胁值系统** | ✅ D-27 |
| | `RageCap` | `100`（可被 Modifier 提升） | ✅ D-27 |
| **控制递减** | `DrWindow` | `5.0s` | ✅ D-24/B-9 |
| | `DrMaxStacks` | `3`（第 4 次起免疫） | ✅ D-24/B-9 |
| | 递减阶梯 | `100% / 50% / 25% / 免疫` | ✅ D-24/B-9 |
| | 适用范围 | 仅 `Stun` / `Freeze`；`Silence`/`Slow` 不受 DR | ✅ D-24/B-9 |

---

## 二十一、扩展机制

✅ [D-27](DESIGN_DECISIONS.md)。本节定义 [AFFIX_AND_SYNERGY](AFFIX_AND_SYNERGY.md) 与 [TACTICS](TACTICS.md) 所需、而 v2.0 内核尚未提供的机制。

**入选判据**：只需**新增字段或新增挂钩点**即可实现。任何需要改动内核基础语义（时间 / 空间 / 实体 / 阵营）的机制**不在本节**，已在 D-27 中裁剪并给出等效替代。

### 21.1 护盾（Shield）

本批唯一需要动伤害管线的机制。

**字段**：`BattleUnit.Shield : int`，初值 `0`。

**管线插入点**——[11.2](#112-单目标伤害结算) 步骤 4 取整后、步骤 5 扣血前：

```
── 4.5 护盾吸收 ───────────────────────────
absorbed = min(tgt.Shield, final)
tgt.Shield -= absorbed
final      -= absorbed
if absorbed > 0: Emit(ShieldAbsorb{tgt, absorbed})
```

**边界裁定 [补全]**：

| 情形 | 裁定 | 理由 |
|---|---|---|
| 护盾完全吸收（`final == 0`） | **仍触发受击退火与受击回怒** | 单位确实被命中了；且这关系到守卫流"挨打攒大"的资源循环 |
| 护盾是否吸收 DOT 跳伤 | **吸收** | 赛博血脉(2) 的"免疫 DOT"是一条独立免疫，与吸收无关 |
| 护盾是否自然衰减 | **不衰减** | |
| 跨波次 | **清空**（与状态效果一致） | |
| 多来源护盾 | **数值相加**；上限取各来源上限的**最大值** | |
| 护盾是否受受伤乘区影响 | **否** | 乘区已在步骤 3 作用于 `final`，护盾吸收的是已减免后的值 |

**挂钩**：`Modifiers.GrantShield(u, amount, cap)`，由 `IActionModifier` 在 Tick 或事件时调用。

### 21.2 生命吸取（Lifesteal）

**挂钩**：`GetLifestealRatio(src, tgt) → float`，默认 `0`。

```
// ApplyDamage 步骤 5 之后
ratio = Modifiers.GetLifestealRatio(src, tgt)
if ratio > 0 && !payload.IsReflected:
    heal = max(1, (int)floor(final * ratio))
    src.Hp = min(src.MaxHp, src.Hp + heal)
    Emit(Lifesteal{src, heal})
```

**边界 [补全]**：

| 情形 | 裁定 |
|---|---|
| DOT 跳伤是否触发吸血 | **否** —— 否则 DOT 流会获得无成本的永久续航 |
| 反射伤害是否触发吸血 | **否** |
| 治疗溢出 | 丢弃 |
| 吸血是否受 `HealingReceived` 影响 | **否** —— 它不是治疗，是伤害转化 |

### 21.3 反射伤害（Reflect）

**挂钩**：`GetReflectRatio(tgt, src) → float`，默认 `0`。

```
// ApplyDamage 步骤 5 之后
ratio = Modifiers.GetReflectRatio(tgt, src)
if ratio > 0 && !payload.IsReflected && src.Alive:
    ApplyDamage(tgt, src, ratio, pen: 0, statuses: [], isReflected: true)
```

> ⚠ **防循环铁律**：`DamagePayload` 新增 `IsReflected : bool` 标记。被标记的伤害**不得**再触发反射、吸血、退火与回怒。否则两个互带反射的单位会进入无限递归。

### 21.4 驱散（Dispel）

**字段**：`StatusEffect.IsDispellable : bool`。

| 状态 | 可驱散 |
|---|:---:|
| `Stun` / `Freeze` / `Silence` / `Slow` | ✅ |
| `Burn` / `Poison` / `Bleed` | ✅ |
| `Invulnerable` / `Stealth` / `Taunt` | ❌（机制性状态，非减益） |

**[补全]** 驱散 N 个时，按 `StatusType` **枚举声明顺序**取前 N 个可驱散项，保证确定性。不按剩余时长或强度排序。

### 21.5 无敌（Invulnerable）

**新状态** `Invulnerable`。

**管线插入点**——[11.2](#112-单目标伤害结算) **最前置**（步骤 1 之前）：

```
if tgt.HasStatus(Invulnerable):
    Emit(DamageNullified{src, tgt})
    return          // 不扣盾、不扣血、不退火、不回怒、不施加状态
```

| 边界 | 裁定 |
|---|---|
| 是否阻止 DOT 跳伤 | **是** |
| 是否受硬控 DR 约束 | **否** —— 它不是控制，不进入 DR 计数 |
| 是否可被驱散 | **否** |

### 21.6 阈值斩杀（Execute）

**挂钩**：`GetExecuteThreshold(src, tgt) → float`，默认 `0`。

**管线插入点**——[11.2](#112-单目标伤害结算) 步骤 8 死亡判定**之前**：

```
if !payload.IsDot && !payload.IsReflected:
    threshold = Modifiers.GetExecuteThreshold(src, tgt)
    if threshold > 0 && tgt.Hp > 0 && (tgt.Hp / tgt.MaxHp) < threshold:
        tgt.Hp = 0
        Emit(Execute{src, tgt})
```

> ⚠ **三条硬约束**（配平要求，见 [D-27](DESIGN_DECISIONS.md)）：
> 1. **仅直接伤害触发**——DOT 与反射伤害不触发。否则每 2 秒一跳的 DOT 会让斩杀线变成"敌人血量一到阈值立即死"，等效于全场敌人有效血量直接砍掉一个阈值且无视护甲；
> 2. **单次伤害结算时的一次判定**，不是常驻光环；
> 3. 斩杀**不受**护甲、元素、受伤乘区影响，但**必须发生在一次成功造成伤害之后**（不能对未被攻击的单位生效）。

### 21.7 资源授予（Grant Mana / Rage）

无需新机制——`Mana` / `Rage` 字段已存在。仅需技能效果类型：

```
GrantMana(target, amount)  →  target.Mana = min(100, target.Mana + amount)
GrantRage(target, amount)  →  target.Rage = min(RageCap, target.Rage + amount)
```

**[补全]** `RageCap` 默认 `100`，但可被 `Modifier` 提升（见 21.11 与 TACTICS 12 的底牌留置）。

### 21.8 单位等级标记（Rank）

**字段**：`UnitSnapshot.Rank : enum { Normal, Elite, Boss }`，默认 `Normal`。

> ⚠ 内核**不据此改变任何计算**。它只是一个供 `Modifier` 与战术指令条件读取的标记位，符合[第十三节](#十三标签与修正器)"内核不解释语义"的原则。

### 21.9 隐匿（Stealth）

**新状态** `Stealth`。

**作用点**——[第十节](#十目标选取规则) `ResolveTargets`：

| `TargetMode` | 隐匿单位是否可被选中 |
|---|:---:|
| `Single` | ❌ **过滤掉** |
| `FrontRow` / `BackRow` / `MiddleColumn` / `AllEnemies` / `RandomN` | ✅ **仍命中** |

语义与 AFFIX 暗影(2) 的原文"无法被选为**单一目标**"完全一致。

**边界 [补全]**：

| 情形 | 裁定 |
|---|---|
| 隐匿单位主动出手后 | **立即解除隐匿**（否则暗影(2) 的"首次攻击 200%"无法收束） |
| 敌方全部隐匿时 | `Single` **回退为不过滤**，避免无目标空过 |
| 隐匿单位是否可被治疗/增益选中 | **可以**（仅过滤敌对 `Single`） |

### 21.10 嘲讽（Taunt）

**新状态** `Taunt`，携带 `SourceId` = 嘲讽者的 `UnitId`。

**作用点**——`ResolveTargets` 的 `Single` 模式，在 [10.1](#101-默认单体目标规则) 最近优先规则**之前**：

```
t = tgt.Statuses.Find(Taunt)
if t != null && Unit(t.SourceId).Alive:
    return [ Unit(t.SourceId) ]      // 强制指向嘲讽者
```

> ⚠ **不需要威胁值系统**。嘲讽实现为"强制目标覆盖"，成本极低，且避免引入一整套仇恨累积/衰减逻辑。

**边界 [补全]**：

| 情形 | 裁定 |
|---|---|
| 嘲讽者阵亡 | 状态立即失效，回退到 10.1 默认规则 |
| 多重嘲讽 | 取**剩余时长最长**者；并列取 `SourceId` **最小**者 |
| 是否影响 AOE | **否**，只影响 `Single` |
| 是否受 DR 约束 | **否**（不剥夺行动权，只改目标） |

### 21.11 状态枚举与事件流增补

**状态枚举扩充**（[14.1](#141-状态实例结构)）：

```
StatusType = { Stun, Freeze, Silence, Slow,        // 原有控制类
               Burn, Poison, Bleed,                // 原有 DOT 类
               Invulnerable, Stealth, Taunt }      // 本节新增机制类
```

机制类状态的共同特征：**不可驱散、不受 DR 约束、不产生 DOT 跳伤**。

**事件流增补**（[第十九节](#十九事件流接口)）：

| 事件 | 载荷 |
|---|---|
| `ShieldAbsorb` | `tgtId, absorbed` |
| `Lifesteal` | `srcId, amount` |
| `Reflect` | `srcId, tgtId, amount` |
| `Dispel` | `tgtId, statusType` |
| `DamageNullified` | `srcId, tgtId` |
| `Execute` | `srcId, tgtId` |

### 21.12 ⛔ 明确不实现的 8 项机制

以下机制需改动内核基础语义，**首发不实现**。等效替代方案见 [D-27](DESIGN_DECISIONS.md)。

| 机制 | 为什么不做 |
|---|---|
| **射程 / 格距 / 前排阻挡** | 要给所有 `TargetMode` 重新定义阻挡语义，牵一发动全身 |
| **施法引导 / 可打断窗口** | 出手当前是**原子的**；加吟唱时间等于让出手可被中途取消，确定性复杂度大幅上升 |
| **召唤物** | 需独立生命周期、独立 ATB、`UnitId` 分配、死亡处理与确定性排序 |
| **位移 / 瞬移** | 接敌后移速归零（[5.2](#52-接敌阶段contact)），引入移动要改整个空间推进逻辑 |
| **伤害延后结算** | 需待结算伤害池，与确定性、DOT、死亡判定三方交互，风险最高 |
| **阵营转换（魅惑）** | 临时改 `Side` 会影响索敌、羁绊统计与胜负判定（被夺取的我方单位是否计入全灭？） |
| **弹道投射物** | 内核伤害是瞬时的，无飞行物概念 |
| **局内复活** | 与 ✅ [D-09](DESIGN_DECISIONS.md) 的"阵亡 → 重伤 → 救治"经济直接冲突 |

> 这些机制并非永久排除。`召唤物`已在 [HERO_VESSEL](HERO_VESSEL_SYSTEM_SPEC.md) 轴一「异步并发代理状态机」中预留了设计位，可在首发验证玩法后作为独立版本目标引入。

---

# 附录 A：本版补全的规格决策清单

原 v1.0 共 32 条。D-04 与 D-08 使其中 8 条作废，现存 24 条。

| # | 决策点 | 本版口径 | 影响面 |
|:--:|---|---|:---:|
| ~~1~~ | ~~拖拽期间是否暂停时间~~ | 🗑 D-04 废除 | — |
| ~~2~~ | ~~就绪后多久自动托管~~ | 🗑 D-04 废除 | — |
| ~~3~~ | ~~玩家按住期间是否出手~~ | 🗑 D-04 废除 | — |
| ~~4~~ | ~~蓄力期间行动条是否累积~~ | 🗑 D-04 废除 | — |
| 5 | DOT 叠加规则 | 同类型唯一，取长取强，不重置 tick | 🔴 |
| 6 | DOT 首跳时机 | 施加后 `TickInterval` 才首跳 | 🟡 |
| 7 | DOT 是否触发退火 | 否 | 🟡 |
| 8 | DOT 是否受护甲/元素影响 | 否 | 🟠 |
| 9 | DOT 是否受受伤乘区影响 | 是 | 🟠 |
| 10 | 法力/怒气上限与溢出 | `100`，溢出丢弃 | 🟠 |
| 11 | 开局法力/怒气 | `0` / `0` | 🟠 |
| 12 | 法力/怒气是否跨波保留 | **保留** | 🔴 |
| 13 | 状态是否跨波保留 | **清空** | 🟡 |
| 14 | 行动条是否跨波保留 | **清零** | 🟢 |
| 15 | 出手形态优先级 | 大招 > 战技 > 普攻 | 🔴 |
| 16 | 沉默是否封锁大招 | **是**（与战技一致） | 🟠 |
| 17 | 大招是否消耗法力 | **否**，双轨独立 | 🟠 |
| 18 | 额外再动能否放技能 | 能 | 🟠 |
| 19 | 额外再动能否连锁 | 能 | 🟠 |
| 20 | 减速叠加方式 | 乘法叠加后钳制 | 🟢 |
| 21 | `Single` 回退目标定义 | 最近优先（前排 → 列距 → UnitId） | 🔴 |
| 22 | `RandomN` 是否可重复 | 不重复；不足则取全部 | 🟡 |
| 23 | 无合法目标时的处理 | 空过，但仍清条并结算资源 | 🟡 |
| 24 | 治疗公式 | `Atk × HealMult × 治疗乘区` | 🔴 |
| ~~25~~ | ~~玩家能否拖拽指定治疗目标~~ | 🗑 D-04 废除 | — |
| ~~26~~ | ~~敌方 AI 是否蓄力~~ | 🗑 D-04 废除 | — |
| 27 | AI 是否留资源等时机 | 默认满即放（✅ D-04d 战术系统支持条件保留） | 🟡 |
| 28 | 伤害取整与保底 | `max(1, floor(x))`，仅取整一次 | 🟠 |
| 29 | 单位阵亡时状态处理 | 全部清空 | 🟢 |
| 30 | 胜负同 tick 判定 | 玩家侧优先判胜 | 🟢 |
| 31 | 倍速实现方式 | 加快 Step 频次，不改 `FixedDeltaTime` | 🔴 |
| 32 | `Modifier` 执行顺序 | 注入顺序，由 Rogue 层确定性生成 | 🔴 |

---

# 附录 B：已知设计风险

| 编号 | 风险 | 状态 |
|:---:|---|---|
| 编号 | 风险 | 状态 |
|:---:|---|---|
| **B-1** | ~~手操带宽超载~~ | ✅ D-04=A 彻底解决（零输入） |
| **B-2** | ~~蓄力 DPS 负收益~~ | ✅ 蓄力机制已废除 |
| **B-5** | ~~怒气悬空~~ | ✅ D-04b 重新定性为大招资源（怒气满自动释放本命大招） |
| **B-6** | ~~护甲常数不随等级缩放~~ | ✅ D-24 已落地 —— `K = 800 + 20 × Level`，见 [12.3](#123-护甲收益曲线--d-24b-6) |
| **B-8** | ~~DOT 不随成长缩放~~ | ✅ D-24 已落地 —— 混合公式 `MaxHp × pct + Atk × 0.2`，见 [12.4](#124-dot-跳伤公式--d-24b-8) |
| **B-9** | ~~控制无递减~~ | ✅ D-24 已落地 —— 硬控 DR `100/50/25/免疫`，见 [14.5](#145-硬控递减dr--d-24b-9) |
| **B-11** | ~~复活削弱难度信号~~ | ✅ D-09 废除复活机制 |
| **B-13** | ~~绝对伤害是二元开关~~ | ✅ D-24 已落地 —— 改为连续穿透率 `Pen`，见 [12.2](#122-伤害公式--d-24b-13) |
| **B-14** | ~~中毒跑不完一个周期~~ | ✅ D-24 已落地 —— 改为 10s / 每跳 4%，见 [14.3](#143-持续伤害类dot) |
| **B-15** | ~~AI 质量成为体验瓶颈~~ | ✅ D-04d 已落地 —— Gambit 战术指令，见 [TACTICS.md](TACTICS.md) |

以下为**仍然存在**的风险：

### B-3 前排单位断蓝

由 8.3 推导，同速下被 4 个及以上敌人攻击的单位法力恒为 0。前排承受攻击最多，结构性地放不出战技。

**现状**：D-08 后退火免疫由 `Modifier` 提供，✅ [D-08a](DESIGN_DECISIONS.md) 的支援羁绊档位（退火减半 → 免疫 → 全队免疫）已把它从"天生被动"变为"构筑决策"，**风险大幅降低但未消除**——不玩支援流派的队伍仍会遇到。

### B-4 阵型决策坍缩

三人排合法列组合唯一（`{0,2,4}`）。成因是渲染遮挡。✅ [D-07](DESIGN_DECISIONS.md) 彻底解除约束，释放全部 10 格，后排 0.85 缩放解决遮挡。

**D-08 后价值上升**：站位不再由职业隐含决定，布阵是玩家仅存的战场表达手段之一。

### B-7 技能系统可能形同虚设 ⚠ 风险上升

首次战技需 4 次普攻（速度 200 时 4 秒）。✅ [D-04c](DESIGN_DECISIONS.md) 把战斗压到 20~40 秒后，窗口更紧张。

**必须验证**：无头模拟统计**每场战斗人均战技/大招释放次数**。低于 2 则需调整（降阈值 / 提高回蓝 / 给开局初始资源）。**列为首要验证项。**

### B-10 治疗 AI 与残血增伤冲突

`LowestHpAlly` 无阈值释放，会持续优先治疗血量百分比最低的单位——而装备了**残血增伤类词条**（如「狂化涌流」，见 [AFFIX 2.2](AFFIX_AND_SYNERGY.md)）的单位正是靠低血量吃增伤。

**现状**：✅ [D-04d](DESIGN_DECISIONS.md) 的战术指令已提供缓解手段（玩家可为支援单位配置治疗阈值），且 ✅ [D-26](DESIGN_DECISIONS.md) 后残血增伤不再是职业天赋而是**可选词条**——玩家自己选择要不要走这条路，冲突从"系统强加"降级为"玩家自担"。默认 AI 仍建议对携带该类词条的单位施加治疗权重折扣。

### B-12 速度是复合超级属性

一点速度同时买到：出手频率、法力积累、怒气积累、对敌退火压制、DOT 施加频率——五种收益；一点攻击力只买到一种。

**必须验证**：无头模拟对比"每 1 点速度"与"每 1 点攻击"对 TTK 的边际贡献。

### B-16 扩展机制缺口 ✅ 已闭环

[AFFIX_AND_SYNERGY.md](AFFIX_AND_SYNERGY.md) 与 [TACTICS.md](TACTICS.md) 曾引用 19 项本文档未定义的机制。✅ [D-27](DESIGN_DECISIONS.md) 已按"是否需改动内核基础语义"分类处置：

| 处置 | 条数 | 位置 |
|---|:---:|---|
| ✅ **实现** | 10 | [第二十一节 扩展机制](#二十一扩展机制) |
| 🔄 **转化**（零新机制） | 1 | 怒气暂扣 → `RageCap` 阈值调整 |
| ✂ **裁剪 + 等效替代** | 8 | [21.12](#2112--明确不实现的-8-项机制) |

**两处决策冲突亦已解决**：

| 冲突 | 处置 |
|---|---|
| 超武 05 与守卫(6) 的**局内免死**绕过 D-09 战损经济 | ✅ D-27：保留免死观感，但**战后仍进入重伤**；守卫(6) 的免死由"每人 1 次"收为"**全队共享 1 次**" |
| TACTICS 12 要求**暂扣满怒大招**，与 D-04b 冲突 | ✅ D-27：改为**非精英战时怒气阈值提升至 150**，精英出现时回落至 100 —— 怒气跨波保留，阈值一落即倾泻，零新机制 |

---

# 附录 C：实现验证清单

内核完成后，用无头模式跑以下基线测试：

| # | 测试项 | 期望 / 关注点 |
|:--:|---|---|
| 1 | 确定性回归 | 相同 seed + 配置 → 逐事件一致 |
| 2 | **倍速一致性** | 1× / 2× / 4× 产出完全相同的事件流 |
| 3 | **人均战技+大招释放次数** | **≥ 2 次/场**，否则见 B-7。**首要验证项** |
| 4 | 单场战斗平均时长 | 目标 20~40 秒，校准 `BattleTimeLimit` |
| 5 | 速度 vs 攻击的边际 TTK 贡献 | 量级应相当，否则见 B-12 |
| 6 | 六羁绊同配对抗胜率矩阵 | 任一流派胜率不应长期 > 65% 或 < 35% |
| 7 | 前排单位场均战技释放次数 | 若接近 0，确认 B-3 |
| 8 | DOT 实际生效比例 | 中毒完整周期完成率，确认 B-14 |
| 9 | 控制链最长持续时间 | 若存在 > 8s 连续失控，确认 B-9 |
| 10 | 元素克制对胜率的影响幅度 | 量化 1.67 摆幅的实际权重 |
| 11 | **AI 决策质量抽查** | 大招是否被浪费在残血目标、AOE 是否只打到 1 人 |
