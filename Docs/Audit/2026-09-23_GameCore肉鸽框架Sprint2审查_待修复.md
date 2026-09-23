# GameCore 肉鸽框架（Sprint 2）代码评审

- **评审日期**：2026-09-23
- **评审对象**：`60cfb58 feat(rogue): 完成 Sprint 2 构筑、背包与肉鸽状态机实现，落实 D-25 插槽与超武融合`（`src/GameCore/Rogue/*` 全新模块 + `UnitSnapshot.IsInjured` 字段）
- **修改状态**：🔴 待修复
- **验证方式**：`dotnet build` + `dotnet test` 实测 + 对照 [DESIGN_DECISIONS.md](../DESIGN_DECISIONS.md) D-25/D-09/D-16、[ROGUE_MAP_AND_RECRUITMENT.md](../ROGUE_MAP_AND_RECRUITMENT.md) 逐条核对

## 一、结论摘要

好消息先说：这是这个仓库第一次一次性交付就**编译通过、全部新增测试通过**的提交（`dotnet build` 0 错误，`dotnet test` 21/21 通过，含 4 条新的 `RogueFrameworkTests`）。同步率/背包容量/核心槽恒为 2/职业与血脉羁绊按 2·4·6 与 2·4 阶梯分别计数且只统计核心槽这几处关键决策，也都和 [D-25](../DESIGN_DECISIONS.md#d-25-插槽扩容--解决羁绊-6-档不可达)/[D-16](../DESIGN_DECISIONS.md#d-16-备用词条背包容量) 的落地口径对得上。

但对照设计文档逐条核查后，发现**插槽解锁的类型与顺序和 D-25 矩阵不一致**（奇点槽完全没有落地），**跨界裂隙节点的生成是纯概率、不保证"每层至少 1 个"**，以及**超武融合的解锁状态是进程级静态字段，不属于单局存档**，这三项是本轮比较关键的问题，建议在被上层系统（地图推进、招募）依赖之前先处理掉。

## 二、🔴 与设计明确冲突

### 2.1 插槽解锁的槽位类型与顺序不符合 D-25 矩阵，奇点槽完全没有落地

[D-25 新插槽矩阵](../DESIGN_DECISIONS.md#d-25-插槽扩容--解决羁绊-6-档不可达)（进阶阶）：

| 权能等级 | 核心槽 | 作战槽 | 通用槽 | 奇点槽 | 总数 |
|:---:|:---:|:---:|:---:|:---:|:---:|
| Level 1 | 2 | 1 | 0 | 0 | 3 |
| Level 2 | 2 | 1 | **1** | 0 | 4 |
| Level 3 | 2 | **2** | 1 | 0 | 5 |
| Level 4 | 2 | 2 | 1 | **1** | 6 |

即：第 4 个槽应该是**通用槽**，第 5 个槽应该是**第二个作战槽**，第 6 个槽应该是**奇点槽**。

实现（[SocketManager.cs:51-60](../../src/GameCore/Rogue/SocketManager.cs#L51-L60)）：

```csharp
return slotIndex switch
{
    0 => TrySetCore(affix, a => s.CoreSlot1 = a),
    1 => TrySetCore(affix, a => s.CoreSlot2 = a),
    2 => TrySetCombat(affix, a => s.CombatSlot1 = a),
    3 when s.UnlockedSlotCount >= 4 => TrySetCombat(affix, a => s.CombatSlot2 = a),   // 应该是通用槽
    4 when s.UnlockedSlotCount >= 5 => TrySetGeneral(affix, a => s.GeneralSlot1 = a), // 应该是第二个作战槽
    5 when s.UnlockedSlotCount >= 6 => TrySetGeneral(affix, a => s.GeneralSlot2 = a), // 应该是奇点槽，但压根没有奇点槽
    _ => false
};
```

`VesselSockets`（[SocketManager.cs:7-30](../../src/GameCore/Rogue/SocketManager.cs#L7-L30)）只有 `CoreSlot1/2`、`CombatSlot1/2`、`GeneralSlot1/2` 六个字段——**没有任何字段对应 `SlotType.Singularity`**，即便 `SlotType.cs` 自己的注释都写着"奇点槽（预留超武独占）"。第 4/5 级解锁的槽位类型也和矩阵反了（第 4 级本该开通用槽，代码开的是第二个作战槽；第 5 级本该开第二个作战槽，代码开的是通用槽）。

**实际影响**：`FusionEngine` 产出的超武词条被打上 `SlotType.Core | SlotType.Combat` 标签（[FusionEngine.cs:32](../../src/GameCore/Rogue/FusionEngine.cs#L32) 等三处），而不是 `SlotType.Singularity`——这与 `SlotType.cs` 自己"奇点槽预留超武独占"的注释矛盾。目前的结果是：奇点槽这个类型在整个 Rogue 模块里从设计到实现都没有被真正用起来，Level 4 权能"专属拦截/转译权能"这条设计线（[D-17 附近](../DESIGN_DECISIONS.md#d-25-插槽扩容--解决羁绊-6-档不可达)）目前没有落地点。

**建议**：`VesselSockets` 补一个 `SingularitySlot` 字段；`EquipAffix` 的 slotIndex→槽位类型映射按矩阵改为"3→General、4→Combat2、5→Singularity"；超武词条的 `AllowedSlots` 改为包含 `SlotType.Singularity`（是否还要同时允许普通 Core/Combat 槽，需要和设计确认——如果超武明确要求"独占奇点槽"，就不应该再允许挤占普通核心/作战槽）。

### 2.2 跨界裂隙节点是纯概率生成，不保证"每层至少 1 个"

[ROGUE_MAP_AND_RECRUITMENT.md](../ROGUE_MAP_AND_RECRUITMENT.md) 明确要求（第 1.5 节 ✅ D-22）：

> 跨界裂隙节点在单层中的出现：**每层至少 1 个**，可与位面异象共用泳道位

实现（[RogueMap.cs:94-109](../../src/GameCore/Rogue/RogueMap.cs#L94-L109)）：

```csharp
private static RogueNodeType RollNodeType(int step, Random rng)
{
    if (step == 0) return RogueNodeType.NormalBattle;
    if (step == 3) return RogueNodeType.Safehouse;

    int roll = rng.Next(100);
    return roll switch
    {
        < 40 => RogueNodeType.NormalBattle,
        < 65 => RogueNodeType.EliteBattle,
        < 80 => RogueNodeType.Event,
        < 90 => RogueNodeType.Merchant,
        < 95 => RogueNodeType.Safehouse,
        _ => RogueNodeType.Rift   // 仅 5% 概率
    };
}
```

每层可参与随机的节点只有 `step ∈ {1,2,4}` × 3 条轨道 = 12 个格子，每个格子只有 5% 概率生成 `Rift`。期望值约 `12 × 0.05 = 0.6` 个/层——**多数楼层生成出来根本不会有跨界裂隙节点**，直接违反设计"每层至少 1 个"的硬性要求。`RogueMap_SeedDeterminism_ShouldMatch` 测试目前只验证了同 seed 两次生成结果一致，没有验证过"每层至少 1 个 Rift"这条规则，所以没被测试捕捉到。

**建议**：生成完一层后做一次后处理——如果该层 `Rift` 节点数为 0，用确定性规则（如按 `UnitId`/坐标排序后选第一个非固定步数格子）强制把其中一个格子改写为 `Rift`，保证下限，同时不破坏"同 seed 同结果"的确定性要求。

### 2.3 超武融合的"配方解锁状态"是进程级静态字段，不是单局存档的一部分

`FusionEngine`（[FusionEngine.cs:13-17](../../src/GameCore/Rogue/FusionEngine.cs#L13-L17)）：

```csharp
public static class FusionEngine
{
    private static readonly List<FusionRecipe> _recipes = new();
    private static readonly HashSet<string> _unlockedRecipeIds = new();
    ...
```

`_unlockedRecipeIds` 是**静态**字段，属于整个进程的生命周期，不挂在 `RogueRunState` 上。这意味着：

- 两个不同的肉鸽存档/对局会共享同一份"配方是否已解锁"状态——A 存档合成过的配方，会让 B 存档也显示为"已解锁"；
- 更直接的隐患：`ENGINEERING_PLAN_AND_ASSETS.md` 反复强调的"无头模式下跑上万场模拟"场景下，第 N 场跑出来的解锁状态会泄漏进第 N+1 场，破坏"相同 seed 必须产出相同结果"的确定性前提（这条原则目前写在 `BATTLE_CORE.md` 18.1，但同样的精神理应适用于 Rogue 层的状态）；
- 已经在单元测试里体现出来：`RogueFrameworkTests.Fusion_Tier3CrossWorldItems_ShouldProduceSuperWeapon` 跑过一次之后，`FusionEngine.IsUnlocked("recipe_super_01")` 会一直返回 `true`，如果测试框架换成随机顺序执行或者并行跑多个进程内测试，这条全局状态会让断言产生依赖测试执行历史的隐性耦合。

**建议**：把 `_unlockedRecipeIds`（以及理想情况下 `_recipes` 如果未来要支持"每局解锁不同配方池"）搬到 `RogueRunState` 里作为实例字段，`FusionEngine` 保留为无状态的纯函数工具类（`_recipes` 静态只读表可以留着，它是配置数据不是运行时状态；但"哪些配方本局已解锁过"属于运行时状态，必须跟着存档走）。

## 三、🟡 与设计存在偏差，建议确认

### 3.1 重伤救治费用公式与设计文档给出的示例数列不一致

`InjuryService.GetTreatmentCost`（[RogueRunState.cs:46-49](../../src/GameCore/Rogue/RogueRunState.cs#L46-L49)）：

```csharp
public static int GetTreatmentCost(int currentTreatments)
{
    return 50 + currentTreatments * 50 + currentTreatments * 30;
}
```

产出数列：`50 → 130 → 210 → 290 ...`

[DESIGN_DECISIONS.md D-09 结论](../DESIGN_DECISIONS.md#d-09-战损与死亡经济)给出的示例数列是：`50 → 100 → 180 ... 信用点`，而该节正文里另给了一个"如"字开头的示例公式 `基础费 × (1 + 0.5 × 累计重伤次数)`（代入得 `50 → 75 → 100`）——**这两处设计文档自己给出的数字就互不一致**，实现选择的公式（`50 → 130 → 210`）和这两者都不一样。这更像是设计文档本身存在两处未收敛的示例数字，而不是单纯的实现 bug；建议找设计确认最终采纳哪一条公式，再让实现对齐，并把设计文档里自相矛盾的那一处改掉。

### 3.2 大名单人数上限（10 人）未在代码层做任何约束

[D-09 配套参数](../DESIGN_DECISIONS.md#d-09-战损与死亡经济)："大名单上限定为 **10 人**。此数字与战损惩罚强度直接挂钩……"。`RogueRunState.ActiveTeam`/`BenchTeam` 目前是普通 `List<UnitSnapshot>`，没有任何地方检查总人数上限。推测这是因为招募系统（把新素体加进大名单的入口）还没有实现，本轮 Sprint 2 只交付了背包/插槽/融合/战损/地图这几个子系统，尚未整合到一起。**先记录，不算本轮缺陷**——等招募/新增素体的入口接进来时，记得把这条上限一起加上，否则战损模型的"渐进经济压力"会失效（大名单可以无限扩，重伤惩罚就形同虚设）。

## 四、✅ 已核对、符合设计的部分

- 核心槽恒为 2、不随权能等级变化（`VesselSockets.CoreSlot1/2` 始终可用，无 `UnlockedSlotCount` 门槛）——符合 [D-25](../DESIGN_DECISIONS.md#d-25-插槽扩容--解决羁绊-6-档不可达) 决定 1。
- `SynergyEvaluator` 只统计 `CoreAffixes`（核心槽），不统计作战/通用槽——符合 D-26 "职业羁绊件数 = 仅统计【核心槽】内职业词条的 tags" 的强约束（文档原话强调"若允许技能词条的职业标签计入羁绊……D-25 的全部推导将同时失效"，这条没有被违反）。
- 职业标签（`Guardian/Striker/Mystic/Ranger/Shadow/Support`）阶梯为 `2/4/6`，世界标签（`Shushan/Cyber`）阶梯为 `2/4` 且**没有**补 `(6)` 档——精确对应 [D-25 "✅ 血脉不补 (6) 档"](../DESIGN_DECISIONS.md#-血脉不补-6-档--已确认) 的定案。
- 背包容量 `MaxCapacity = 15`——符合 [D-16](../DESIGN_DECISIONS.md#d-16-备用词条背包容量)。
- 地图结构：每层 5 个可随机步 + 1 个 Boss 步 = 6 步、3 条轨道——符合 [D-22](../DESIGN_DECISIONS.md) "1 世界 × 3 层 × 6 步，三轨推进"。
- `RogueMap_SeedDeterminism_ShouldMatch` 测试证明了同 seed 生成结果一致，这一点做得对，只是覆盖面还没到"每层至少 1 个 Rift"这条规则（见 2.2）。

## 五、建议优先级

1. 先确认 §2.1 的插槽类型顺序——这是后续 Level 3/4 装备互动、奇点槽相关内容能否开工的前提，越早对齐越省事。
2. §2.3 的静态状态搬到 `RogueRunState` 实例上，改动量不大，但越晚改牵扯的调用点越多。
3. §2.2 的 Rift 保底生成规则。
4. §3.1 找设计对齐救治费用公式（同时建议顺手把 `DESIGN_DECISIONS.md` 里两处不一致的示例数字改成一致）。
5. §3.2 留给招募系统接入时一并处理，不阻塞当前 Sprint。
