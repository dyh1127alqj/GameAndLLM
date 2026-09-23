using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Model;
using GameCore.Battle;

namespace GameCore.Rogue;

public sealed class RogueRunState
{
    public int Seed { get; }
    public int CurrentFloor { get; set; } = 1;
    public int CurrentStep { get; set; } = 0;
    public int Credits { get; set; } = 200;
    public int SyncRatePermille { get; set; } = 1000;
    public int AuthPoints { get; set; } = 10;
    public bool IsInSafehouse { get; set; } = false;

    public List<UnitSnapshot> ActiveTeam { get; } = new();
    public List<UnitSnapshot> BenchTeam { get; } = new();
    public const int MaxRosterCapacity = 10; // 审查 3.2: 大名单上限 10 人软约束
    public bool CanRecruit => (ActiveTeam.Count + BenchTeam.Count) < MaxRosterCapacity;
    public List<UnitSnapshot> Roster => ActiveTeam.Concat(BenchTeam).ToList();
    public List<int> ActiveTeamUnitIds => ActiveTeam.Select(u => u.UnitId).ToList();

    public BackpackService Backpack { get; } = new();
    public SocketManager Sockets { get; } = new();
    public int TotalHealTreatments { get; set; } = 0;
    public HashSet<string> UnlockedRecipeIds { get; } = new(); // 审查 2.3: 配方解锁归属单局实例

    public RogueRunState(int seed = 42)
    {
        Seed = seed;
    }

    public bool IsRunFailed()
    {
        int healthyCount = ActiveTeam.Count(u => !u.IsInjured && !u.IsDead) +
                           BenchTeam.Count(u => !u.IsInjured && !u.IsDead);
        if (healthyCount >= 5) return false;

        int cost = InjuryService.GetTreatmentCost(TotalHealTreatments);
        return Credits < cost;
    }
}

public static class InjuryService
{
    public static int GetTreatmentCost(int currentTreatments)
    {
        // 审查 3.1 对齐设计文档 D-09 数列: 50 -> 100 -> 180 ...
        return currentTreatments switch
        {
            0 => 50,
            1 => 100,
            2 => 180,
            _ => 180 + (currentTreatments - 2) * 100
        };
    }

    public static void ApplyPostBattleLosses(RogueRunState state, IEnumerable<UnitSnapshot> finalUnits)
    {
        foreach (var unit in finalUnits)
        {
            var match = state.ActiveTeam.FirstOrDefault(u => u.UnitId == unit.UnitId);
            if (match == null) continue;

            if (unit.IsDead)
            {
                match.IsInjured = true;
                match.CurrentHp = 0;
            }
            else
            {
                match.CurrentHp = unit.CurrentHp;
            }
        }

        var injuredOnActive = state.ActiveTeam.Where(u => u.IsInjured).ToList();
        foreach (var injured in injuredOnActive)
        {
            state.ActiveTeam.Remove(injured);
            state.BenchTeam.Add(injured);
        }
    }

    public static bool TryTreatInjury(RogueRunState state, int unitId, bool isSafeNode)
    {
        if (!isSafeNode) return false;
        var target = state.BenchTeam.FirstOrDefault(u => u.UnitId == unitId);
        if (target == null || !target.IsInjured) return false;

        int cost = GetTreatmentCost(state.TotalHealTreatments);
        if (state.Credits < cost) return false;

        state.Credits -= cost;
        state.TotalHealTreatments++;

        target.IsInjured = false;
        target.CurrentHp = target.MaxHp;

        return true;
    }

    public static void ProcessBattleCasualties(RogueRunState state, BattleResult result) => ApplyPostBattleLosses(state, result.FinalPlayerUnits);
    public static void ProcessBattleCasualties(RogueRunState state) => ApplyPostBattleLosses(state, state.ActiveTeam);
    public static int GetNextHealFee(RogueRunState state) => GetTreatmentCost(state.TotalHealTreatments);
    public static bool TryTreatInjury(RogueRunState state, int unitId) => TryTreatInjury(state, unitId, state.IsInSafehouse);
}
