# GameCore 肉鸽框架（Sprint 2）代码复审

- **复审日期**：2026-09-23
- **复审对象**：`4df30a6 fix(rogue-audit): 根据肉鸽框架Sprint2审查报告完成全量修复，落实D-25奇点槽、Rift保底与实例级超武解锁`
- **修改状态**：🟡 大部分已修复，1 项关键问题实为"新增了正确路径，但没有下线原有的错误路径"
- **验证方式**：`dotnet build` + 针对性 `dotnet test` 实测 + 逐条对照上一轮评审 [2026-09-23_GameCore肉鸽框架Sprint2审查_待修复.md](./2026-09-23_GameCore肉鸽框架Sprint2审查_待修复.md)

## 一、结论摘要

`dotnet build` 0 错误（仅 1 条 xUnit 分析器风格警告，无关紧要）。上一轮报告的 §2.1（插槽类型顺序/奇点槽缺失）、§2.2（跨界裂隙无保底）、§3.1（救治费用数列）三项都**确认修复到位**，并且都补了针对性回归测试（`SocketManager_SingularitySlot_ShouldRequireSlotTypeAndUnlockedCount`、`RogueMap_EveryFloor_ShouldContainAtLeastOneRift`），实测全部通过。§3.2（大名单上限）也顺手加了 `MaxRosterCapacity`/`CanRecruit`。

但 §2.3（超武配方解锁状态是进程级静态字段）**没有真正修复**——新增了一套挂在 `RogueRunState` 上的实例级跟踪（方向正确），但**原来那套有问题的静态字段和方法一个都没有删**，而且现有测试和最直观的调用方式（`FusionEngine.TryFuse(a, b, out result)` + `FusionEngine.IsUnlocked(recipeId)`）走的还是旧的静态路径。相当于"给正确答案开了一条新路，但错误答案那条路完全没堵上，而且路牌还是指向错误答案"。

## 二、✅ 确认修复

### 2.1 插槽类型顺序 + 奇点槽 —— 已按 D-25 矩阵修正

`VesselSockets` 新增 `SingularitySlot` 字段（[SocketManager.cs:15](../../src/GameCore/Rogue/SocketManager.cs#L15)），`EquipAffix` 的映射改为：

```csharp
3 when s.UnlockedSlotCount >= 4 => TrySetGeneral(...)     // 第 4 级：通用槽 ✅
4 when s.UnlockedSlotCount >= 5 => TrySetCombat(...)      // 第 5 级：第二作战槽 ✅
5 when s.UnlockedSlotCount >= 6 => TrySetSingularity(...) // 第 6 级：奇点槽 ✅
```

与 D-25 矩阵（Level2 开通用槽、Level3 开第二作战槽、Level4 开奇点槽）完全对齐。新增测试 `SocketManager_SingularitySlot_ShouldRequireSlotTypeAndUnlockedCount` 验证了"未解锁到 6 槽时装不进奇点槽，解锁后能装"，实测通过。

**一个可以讨论但不算 bug 的点**：三把超武的 `AllowedSlots` 改成了 `SlotType.Core | SlotType.Combat | SlotType.Singularity`（[FusionEngine.cs:32](../../src/GameCore/Rogue/FusionEngine.cs#L32) 等），也就是说超武除了能装进奇点槽，也仍然能装进普通核心槽或作战槽。`SlotType.Singularity` 的注释写的是"预留超武独占"，如果"独占"的本意是"这个槽位只留给超武，同时超武也应当优先/只能进这个槽"，那么现在的实现只满足了前半句。是否要收紧为"超武只能进奇点槽"建议找设计确认一下，不算这轮修复的缺陷。

### 2.2 跨界裂隙保底 —— 已实现，测试覆盖 5 个种子

[RogueMap.cs:70-83](../../src/GameCore/Rogue/RogueMap.cs#L70-L83) 在常规随机生成完之后检查本层是否存在 `Rift` 节点，没有则从 `step 1~3`（排除固定为安全屋的 step 3）里用同一个种子化的 `rng` 确定性地选一个节点强制改写为 `Rift`。新增测试 `RogueMap_EveryFloor_ShouldContainAtLeastOneRift` 对 seed 1~5 分别验证，实测全部通过。确定性没有被破坏（改写仍然消费同一个 `rng` 实例，同 seed 必然产出同样的选择）。

### 2.3-b 救治费用数列 —— 已对齐设计文档

`InjuryService.GetTreatmentCost` 改为直接对应设计文档给出的字面数列 `50 → 100 → 180 → (180 + 100×n)`（[RogueRunState.cs:51-58](../../src/GameCore/Rogue/RogueRunState.cs#L51-L58)），测试断言也同步改成了 `100`（原来错误地断言了 `130`）。这是干净的修复。

### 3.2 大名单上限 —— 已加软约束

`RogueRunState.MaxRosterCapacity = 10` + `CanRecruit` 属性（[RogueRunState.cs:21-22](../../src/GameCore/Rogue/RogueRunState.cs#L21-L22)）。目前还没有任何招募代码路径去调用 `CanRecruit`，但这本来就是上一轮报告里标注为"等招募系统接入时一并处理"的事项，现在至少把约束值预先放好了，方向正确。

## 三、🔴 未真正修复：超武配方解锁状态仍然是静态字段

`FusionEngine`（[FusionEngine.cs:15-16](../../src/GameCore/Rogue/FusionEngine.cs#L15-L16)）：

```csharp
private static readonly List<FusionRecipe> _recipes = new();
private static readonly HashSet<string> _unlockedRecipeIds = new();   // ← 原问题字段，还在
```

这次新增的是一套**并行**的实例级方法（[FusionEngine.cs:91-110](../../src/GameCore/Rogue/FusionEngine.cs#L91-L110)）：

```csharp
public static AffixInstance? TryFuse(RogueRunState state, AffixInstance a, AffixInstance b)
{
    var result = TryFuse(a, b);              // ← 内部仍然调用旧的 2 参数版本
    if (result != null)
    {
        ...
        state.UnlockedRecipeIds.Add(recipe.RecipeId);   // 新增：写入实例状态
    }
    return result;
}
```

注意 `TryFuse(RogueRunState state, ...)` 内部调用的仍然是旧的 `TryFuse(a, b)`——而**旧方法本身还会继续往静态的 `_unlockedRecipeIds` 里写**（[FusionEngine.cs:81](../../src/GameCore/Rogue/FusionEngine.cs#L81)）。也就是说现在每合成一次，状态会**同时**写进静态字典和（如果调用的是带 `state` 参数的重载）实例字典——旧的泄漏路径完全没有被堵住，只是多了一条并行的正确路径。

**更能说明问题的是现有测试**：`RogueFrameworkTests.Fusion_Tier3CrossWorldItems_ShouldProduceSuperWeapon`（[RogueFrameworkTests.cs:52-65](../../tests/GameCore.Tests/RogueFrameworkTests.cs#L52-L65)）走的还是旧的两参数 `TryFuse(a, b, out result)` + 静态查询 `FusionEngine.IsUnlocked("recipe_super_01")`，完全没有触碰这次新加的 `RogueRunState` 重载。换句话说，**这次改动新加的"正确"代码路径，连测试都没有覆盖到**，而"错误"的静态路径依然是被测试验证、被文档示例调用方式默认指向的那一条。

**实际后果没有变化**：只要任何调用方（包括现有测试自己）继续用两参数的 `TryFuse`/`IsUnlocked(string)`，跨存档、跨无头模拟场次的状态泄漏问题原样存在。

**建议**：

1. 把 `_unlockedRecipeIds` 这个静态字段整个删掉，`TryFuse(a, b)`（无 `state` 参数的版本）要么废弃、要么改成必须传 `RogueRunState`（哪怕只是为了拿到要写入的目标集合）；
2. 现有的 `Fusion_Tier3CrossWorldItems_ShouldProduceSuperWeapon` 测试改用带 `RogueRunState` 的重载，并断言 `state.UnlockedRecipeIds` 而不是静态的 `IsUnlocked`；
3. 建议再补一条测试，直接证明"两个不同的 `RogueRunState` 实例互不影响彼此的解锁记录"——这是当初这条问题最核心的关注点，目前还没有任何测试断言过这一点。

## 四、次要事项

- 本轮跑测试时发现：并发跑多个 `dotnet test` 进程会因为共享 `obj`/`bin` 输出目录而互相等待，其中一次全量套件（未排除性能门禁测试）被后台调度卡住超过 120 秒没有返回。单独跑各子集（Rogue 7 项 / 单条 BattleSim 用例）都能在 1 秒内完成，判断是本地并发调用互相占锁导致的假象，不是代码问题，仅记录以免下次误判为回归。

## 五、修复优先级

1. 收口 §三——删掉静态解锁状态，统一到 `RogueRunState` 实例上，并把测试断言切过去、补一条"多实例互不干扰"的测试。这是本轮唯一真正需要继续跟进的问题。
2. §2.1 里"超武是否应独占奇点槽"这个开放问题，建议找设计过一遍，不影响当前功能正确性，只影响后续超武互斥逻辑的实现方式。
