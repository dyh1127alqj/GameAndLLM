# 《轮回者素体系统规范：差异性正交轴与机制权能阶梯架构》

> **版本**：v1.1 (Hero Vessel & Authority Hierarchy Specification)  
> **文档定位**：项目规划阶段核心系统规范，从计算模型、状态机拓扑、信号转换与管线权限等底层维度，抽象定义素体差异性与品级梯度。  
> **关联文档**：[DESIGN_DECISIONS.md](DESIGN_DECISIONS.md)（决策总账）、[ROGUE_SYSTEM_ARCHITECTURE.md](ROGUE_SYSTEM_ARCHITECTURE.md)、[BATTLE_CORE.md](BATTLE_CORE.md)、[ROGUE_WORLD_AND_AFFIX_DRAFT.md](ROGUE_WORLD_AND_AFFIX_DRAFT.md)
>
> **v1.1 变更**（依据 [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md)）：
> - D-08 **素体无先天职业**，`BaseRole` 字段删除；职业改为可自由装卸的插槽被动；
> - D-08 级联 8：素体个体差异现 **100% 由本命特性承担**，本命特性范例需大幅扩充（见第七节）；
> - D-04 战斗纯全自动，本命大招改为满怒自动释放；
> - D-19 新增多拦截器执行顺序约束（见 5.1）。

---

## 目录
1. [设计哲学与系统抽象定位](#一设计哲学与系统抽象定位)
2. [素体差异性的三大底层正交轴](#二素体差异性的三大底层正交轴)
3. [品级梯度的本质：四大机制权能阶梯](#三品级梯度的本质四大机制权能阶梯)
4. [宏观构筑经济与系统映射矩阵](#四宏观构筑经济与系统映射矩阵)
5. [C# 数据模型与底层接口契约](#五c-数据模型与底层接口契约)
6. [与战斗内核及肉鸽状态机的协同契约](#六与战斗内核及肉鸽状态机的协同契约)

---

## 一、设计哲学与系统抽象定位

在“词条自由刷取装配”的无限流架构中，为彻底避免角色沦为“同质化白板插槽”，英雄素体（Hero Vessel）的定位必须在数学模型与架构上确立以下共识：

1. **素体不是容器，而是信号转换函数（Transfer Function）**：
   $$Effect_{output} = f_{Vessel}(Affix_{input})$$
   词条提供原始效果与世界标签，而素体决定了该效果的**放大系数、触发时机、承载极性与形态映射**。
2. **品级梯度是“权能层级（Authority Level）”的跃迁，而非数值线性放大**：
   低星与高星的区别不是“攻击力多 20%”，而是其在战斗状态机与底层结算管线中所拥有的**规则干预深度**。

---

## 二、素体差异性的三大底层正交轴

在数据模型中，任意两个素体的不可替代性由以下三个彼此独立的底层正交轴唯一定位：

```
                              ▲ [轴 1: 行为状态机模型 (Behavioral Model)]
                              │ (线性周期 / 双模态切换 / 异步并发代理)
                              │
                              │
                              │
     ─────────────────────────┼─────────────────────────► [轴 2: 词条承载与转换拓扑 (Topology & Transpiler)]
     [轴 3: 构筑战略生态位]    │                          (透传 / 极性过滤 / 跨域转译)
     (零成本垫脚石 / 流派放大器 / 胜负手引力源)
```

### 2.1 轴一：行为状态机模型（Behavioral State Model）
定义素体在战斗主循环中的驱动方式：
* **单循环状态机（Linear ATB）**：严格遵循 `累加充能 -> 阈值释放 -> 清零重置` 的通用闭环；
* **双模态切换状态机（Modal Switching）**：具备状态标志位（State Flag）。当满足特定全局阈值（时间、存活人数、濒死等）时，主循环完全切换为另一套行为分支与索敌优先级；
* **异步并发代理状态机（Asynchronous Proxy）**：素体在主 ATB 线程运行的同时，在战场生成具备独立生命周期与逻辑 Tick 的伴生实体（如召唤物、陷阱、阵地光环）。

### 2.2 轴二：词条承载与转换拓扑（Affix Channel Topology）
定义素体如何处理挂载在其身上的词条数据流：
* **透传型（Pass-through）**：$f(x) = x$，词条挂载后按其原始数值与挂钩点原生执行；
* **极性过滤型（Polarized Filter）**：插槽带有标签约束掩码（Mask），对符合本命标签的词条赋予增益，对不相容标签产生抑制或附加代价；
* **跨域转译型（Domain Transpiler）**：重写输入词条的触发源（Trigger）或输出域（Payload），例如将“受击触发”强行转译为“普攻触发”，将“物理伤害”转译为“真实穿透伤害”。

### 2.3 轴三：构筑战略生态位（Strategic Economic Niche）
定义素体在肉鸽单局资源流转中的经济角色：
* **高流动性垫脚石（High Liquidity）**：招募零成本，拥有高保底的单卡基础战力，生命周期短，中后期允许无损或低损置换；
* **流派枢纽放大器（Synergy Enabler）**：自身的有效产出与队伍中特定标签的密度呈超线性挂钩，充当构筑润滑剂；
* **构筑绝对引力源（Build Gravity Well）**：自身具备强烈的构筑排他性，一旦入队，全队后续的词条调配与站位决策均需向其绝对倾斜。

---

## 三、品级梯度的本质：四大机制权能阶梯

品级梯度严格对应素体在战斗管线中所拥有的**机制介入权能等级（Rule Authority Level）**：

```mermaid
graph TD
    L1[Level 1: 标量输入输出层 (Scalar I/O)] --> L2[Level 2: 条件逻辑判定层 (Conditional Logic)]
    L2 --> L3[Level 3: 乘区与转换枢纽层 (Multiplier & Transpiler)]
    L3 --> L4[Level 4: 管线与规则重构层 (Pipeline & Rule Alteration)]
```

### Level 1：标量输入输出层（Scalar I/O）—— 低阶素体
* **权能边界**：只能与系统开放的基础标量属性交互（$\pm \Delta HP, \pm \Delta Shield, \pm \Delta Atk$）；
* **规则受控度**：战斗行为严格受制于全局默认规则（默认索敌、固定 ATB 充能、无法拦截异常状态）；
* **插槽表现**：固定 $N$ 个通用基础槽，无乘区加权（放大倍率恒为 $1.0\times$）。

### Level 2：条件逻辑判定层（Conditional Logic）—— 中阶素体
* **权能边界**：能够监听特定单体战斗事件（Event Listener），在状态机中插入**布尔逻辑分支**（`If Target Has Condition -> Execute Extra Action`）；
* **赋能范围**：具备局域赋能能力（如为同排、相邻或特定职能的队友提供被动修正）；
* **插槽表现**：插槽出现极性偏好，对特定世界标签提供阶梯式基础数值补正。

### Level 3：乘区与转换枢纽层（Transformation Hub）—— 高阶素体
* **权能边界**：介入数值公式内部的**独立乘区计算**（不再是加减法，而是指数/乘法联动）；
* **机制特权**：具备**词条转译能力**（改变装配词条的触发时机、将一种资源循环强制映射为另一种资源）；
* **插槽表现**：拥有专属的【黄金放大插槽（Prismatic Socket）】，放入该槽位的词条效果获得倍率加成（如 $1.5\times \sim 2.0\times$）。

### Level 4：管线与规则重构层（Rule Alteration）—— 顶阶素体
* **权能边界**：拥有底层战斗管线的**全局拦截与改写特权（Pipeline Interceptor）**：
  - **时间流改写**：强制重塑行动队列（全局时间冻结、偷取敌方行动进度）；
  - **拓扑结构扭曲**：改写前后排空间定义，重构寻敌管线（如强制将全场单位判定为处于自身攻击半径内）；
  - **因果终审裁决**：拦截致命致死事件，将致死判定逆转为增益或反向伤害；
* **插槽表现**：拥有【全域奇点插槽】，彻底豁免世界法则的压制与词条冲突。

---

## 四、宏观构筑经济与系统映射矩阵

| 权能等级 (Tier) | 招募成本 (授权点) | 插槽拓扑架构 | 词条信号转译能力 | 管线插桩权限 | 构筑生态位定位 |
|:---:|:---:|---|---|---|:---:|
| **Level 1** | **0 消耗**<br>(初始白送/流动) | 2 个通用标量槽 | 无转译能力<br>($f(x) = x$) | 无拦截权，纯响应回调 | 前期过渡垫脚石 |
| **Level 2** | **2 ~ 3 消耗**<br>(适度沉没) | 2~3 个专精槽位 | 单标签条件触发加成 | 单事件条件监听分支 | 流派基石与枢纽 |
| **Level 3** | **5 ~ 6 消耗**<br>(重大投资) | 3~4 槽<br>(含1黄金放大槽) | 触发时机/结算域转译 | 独立计算乘区注入 | 战术核心建队支点 |
| **Level 4** | **8+ 或 特殊奇遇**<br>(极端昂贵) | 4 槽<br>(含全域奇点槽) | 跨世界形态强制同化 | 时间/空间/生死法则拦截 | 终局决胜引力源 |

> **插槽经济提示**：职业本身要占 1 个核心槽（[D-08](DESIGN_DECISIONS.md)）。因此 Level 1 素体的 2 槽 = `1 职业 + 1 自由`，构筑空间极窄；Level 4 的 4 槽才谈得上"双职业"或"职业 + 血脉 + 双技能"的复合构筑。**这正是权能等级的经济意义所在**——高星买的不只是权能深度，还有构筑自由度。
>
> ✅ 权能等级与「基础阶/进阶阶」的关系已按 [D-17](DESIGN_DECISIONS.md) 确立为正交模型（先天权能决定机制上限，局内进阶解锁插槽与本命效果）。

---

## 五、C# 数据模型与底层接口契约

该规范可直接无缝映射为纯 C# 数据结构，保持对引擎的零依赖：

```csharp
namespace SanguoCore.Domain.Vessel;

// 1. 机制权能等级
public enum AuthorityLevel
{
    Level1_ScalarIO = 1,
    Level2_ConditionalLogic = 2,
    Level3_TransformationHub = 3,
    Level4_RuleAlteration = 4
}

// 2. 素体核心定义
public record HeroVesselDefinition
{
    public string VesselId { get; init; }
    public AuthorityLevel Tier { get; init; }

    // ⛔ D-08：BaseRole 字段已删除。素体无先天职业。
    //    职业是可自由装卸的插槽被动，任何素体都能被改造成任何角色。
    //    如需在 UI 上给新手一个方向提示，用下面这个字段——它不参与任何逻辑判断。
    public HeroClass? SuggestedRole { get; init; }   // 仅 UI 提示，禁止进入战斗计算

    // 基础标量数据底盘
    public int BaseSpeed { get; init; }
    public int BaseHp { get; init; }
    public int BaseAtk { get; init; }
    public int BaseArmor { get; init; }

    // 本命大招：素体身份的第二根支柱，不可更换。
    // 由怒气驱动，满怒自动释放（D-04 全自动）。✅ 最终形态已按 D-04b / D-08c 定案保留
    public SkillDefinition InnateUltimate { get; init; }

    // 轴 1: 行为状态机模型驱动器
    public IBehaviorStateMachine BehaviorModel { get; init; }

    // 轴 2: 插槽拓扑定义与转译算子
    public IReadOnlyList<SocketDescriptor> Sockets { get; init; }
    public IAffixTranspiler AffixTranspiler { get; init; }

    // 轴 3 & 权能: 底层管线拦截器 (Level 3~4 专享)
    public IPipelineInterceptor PipelineInterceptor { get; init; }
}

// 3. 插槽拓扑描述
public record SocketDescriptor(
    int SlotIndex,
    SocketType AllowedType,         // Universal, PolarizedTag, PrismaticMultiplier, Singularity
    float EffectMultiplier = 1.0f,  // 放大倍率 (黄金槽为 1.5 ~ 2.0)
    string RequiredTagMask = null   // 约束标签 (极性槽专享)
);

// 4. 词条转译算子接口
public interface IAffixTranspiler
{
    // 输入词条原生效果，输出经过素体本命调制后的效果
    TranspiledEffect Transpile(AffixInstance inputAffix, VesselCombatContext ctx);
}

// 5. 管线拦截器接口
public interface IPipelineInterceptor
{
    // 拦截伤害计算 (Level 3/4)
    bool InterceptDamage(ref DamagePayload payload, BattleSimContext simCtx);
    
    // 拦截行动条与时间流 (Level 4)
    bool InterceptTimeline(BattleUnit unit, ref float deltaGauge);
    
    // 拦截致死裁决 (Level 4)
    bool InterceptDeath(BattleUnit target, UnitKillerContext killerCtx);
}
```

### 5.1 拦截器执行顺序约束（确定性硬要求）

✅ 依据 [D-19](DESIGN_DECISIONS.md) 确立权威执行契约。

多个拦截器同时触发时若无确定顺序，会产生**不可复现的战斗结果**——这是本系统最容易埋下的确定性隐患。

| # | 约束 | 说明 |
|:---:|---|---|
| 1 | **排序** | `AuthorityLevel` 降序 → `UnitId` 升序 |
| 2 | **短路** | 同一拦截点，第一个返回 `true` 的拦截器生效后**立即中止**，后续不执行 |
| 3 | **纯函数** | 拦截器不得读取任何未排序集合，不得调用 `BattleRng` 以外的随机源 |
| 4 | **时间流拦截的边界** | `InterceptTimeline` **只允许修改传入单位自身的 `deltaGauge`** |

> ⚠ 第 4 条尤其关键。第三节 Level 4 描述的"全局时间冻结"若直接在拦截器里遍历全场修改 `Gauge`，会破坏 [BATTLE_CORE 6.2](BATTLE_CORE.md) 的"就绪队列一次性构造"前提。
>
> **正确做法**：任何全场效果必须实现为一个**状态效果（StatusEffect）**，走既有的状态系统，由主循环统一结算。拦截器只处理单点介入。

---

## 六、与战斗内核及肉鸽状态机的协同契约

1. **与战斗内核（`BattleSim`）解耦**：
   - 战斗内核不知道什么是"Level 3"或"Level 4"，也**不知道什么是"职业"或"羁绊"**（[D-08](DESIGN_DECISIONS.md)）；
   - 战斗内核在各个管线阶段（Tick、UpdateGauge、CalculateDamage、OnUnitDeath）仅检查当前单位是否挂载了 `IPipelineInterceptor`；
   - 如果挂载了，则执行拦截逻辑。这保证了核心管线的纯粹性与确定性。
2. **与肉鸽状态机（`RogueEngine`）解耦**：
   - `RosterService` 在招募单位时，通过 `AuthorityLevel` 查询全局配置表，决定扣除的**授权点**额度；
   - `InventoryService` 在镶嵌词条时，严格通过 `SocketDescriptor` 进行合法性校验（标签掩码、槽位容量判定）；
   - `SynergyService` 在战前统计全队插槽标签件数、匹配羁绊档位，产出 `IBattleModifier` 列表注入 `BattleContext`——**羁绊结算全程不进内核**。

---

## 七、⚠ 遗留工作：本命特性内容量

[D-08](DESIGN_DECISIONS.md) 剥离职业后，素体的个体差异**只剩两根支柱**：

| 支柱 | 当前状态 |
|---|---|
| 本命特性（`InnateTrait`） | ⚠ [ROGUE_WORLD 3.2](ROGUE_WORLD_AND_AFFIX_DRAFT.md) 仅有 **3 个范例** |
| 本命大招（`InnateUltimate`） | ✅ 已按 [D-04b](DESIGN_DECISIONS.md) / [D-08c](DESIGN_DECISIONS.md) 定案保留，由怒气驱动 |

若本命特性内容量不足，素体之间将**只剩属性数值差异**，"防止词条白板化"的原始设计目标会落空。

**建议产能**：首发 20~24 个素体（[D-03](DESIGN_DECISIONS.md) 估算），需配套 **20~24 条互不重复的本命特性** + 同等数量的本命大招。这是 D-08 带来的最大新增设计工作量，建议尽早排期。

**设计约束**：本命特性必须沿第二节的三条正交轴分布，避免全部挤在"某属性 +X%"这一类。参考配比：

| 正交轴 | 建议占比 |
|---|:---:|
| 轴一 行为状态机（双模态切换 / 异步并发代理） | ~30% |
| 轴二 词条转译（极性过滤 / 跨域转译） | ~40% |
| 轴三 构筑生态位（流派放大器 / 构筑引力源） | ~30% |
