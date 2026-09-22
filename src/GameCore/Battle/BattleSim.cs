using GameCore.Events;
using GameCore.Model;
using GameCore.Pipeline;
using GameCore.Targeting;

namespace GameCore.Battle;

public sealed class BattleSim
{
    public BattleContext Context { get; }
    public BattleEventQueue EventQueue { get; } = new();
    private readonly TargetLockTracker _lockTracker = new();
    
    public int CurrentTick { get; private set; }
    public bool IsFinished { get; private set; }
    public BattleOutcome Outcome { get; private set; } = BattleOutcome.InProgress;

    public int TotalDamageDealtPlayer { get; private set; }
    public int TotalDamageDealtEnemy { get; private set; }
    public int TotalSkillsCast { get; private set; }
    public int TotalUltimatesCast { get; private set; }

    public BattleSim(BattleContext context)
    {
        Context = context;
    }

    public void Step()
    {
        if (IsFinished) return;
        CurrentTick++;
        
        // 维护 1.0s 索敌锁定黏性计时
        _lockTracker.Tick();

        // 推进 ATB 充能
        AdvanceGauges();
        
        // 执行就绪单位的行动
        ProcessReadyUnits();

        // 检查胜负与超时
        CheckSettlement();
    }

    private void ProcessReadyUnits()
    {
        var readyUnits = Context.PlayerTeam.Concat(Context.EnemyTeam)
            .Where(u => !u.IsDead && u.CurrentGauge >= 1000)
            .OrderByDescending(u => u.CurrentGauge)
            .ToList();

        foreach (var u in readyUnits)
        {
            if (u.IsDead || IsFinished) continue;
            ExecuteTurn(u);
            u.CurrentGauge = 0;
        }
    }

    private void AdvanceGauges()
    {
        foreach (var u in Context.PlayerTeam.Concat(Context.EnemyTeam))
        {
            if (u.IsDead) continue;
            // 60Hz 充能推进: ΔGauge = Speed × 0.005 × (1/60) × 1000 = Speed / 12
            u.CurrentGauge += (u.BaseSpeed * 1000) / (60 * 200);
        }
    }

    private void ExecuteTurn(UnitSnapshot u)
    {
        // 决策本回合动作意图：大招 -> 战技 -> 普攻
        SkillDefinition? skillToCast = null;
        bool isUltimate = false;

        if (u.UltimateSkill != null && u.CurrentRage >= u.UltimateSkill.RageCost)
        {
            skillToCast = u.UltimateSkill;
            isUltimate = true;
        }
        else if (u.ActiveSkill != null && u.CurrentMana >= u.ActiveSkill.ManaCost)
        {
            skillToCast = u.ActiveSkill;
            isUltimate = false;
        }

        if (skillToCast != null)
        {
            CastSkill(u, skillToCast, isUltimate);
            return;
        }

        // 兜底执行普攻
        ExecuteBasicAttack(u);
    }

    private void ExecuteBasicAttack(UnitSnapshot u)
    {
        var enemies = u.Faction == Faction.Player ? Context.EnemyTeam : Context.PlayerTeam;
        var target = TargetResolver.ResolveTarget(u, TargetMode.Nearest, enemies, _lockTracker);
        if (target == null) return;

        var res = DamagePipeline.CalculateAndApply(u, target, 1000, DamageType.Physical, DamageFlags.Direct);
        EventQueue.PushDamage(new DamageEvent(CurrentTick, u.UnitId, target.UnitId, res.FinalDamage, res.Flags, DamageType.Physical));
        
        if (u.Faction == Faction.Player) TotalDamageDealtPlayer += res.FinalDamage;
        else TotalDamageDealtEnemy += res.FinalDamage;

        // 普攻回法 30 点 (4次普攻后满蓝释放战技)，回怒 10 点
        u.CurrentMana = global::System.Math.Min(100, u.CurrentMana + 30);
        u.CurrentRage = global::System.Math.Min(u.RageCap, u.CurrentRage + 10);

        if (target.IsDead)
        {
            EventQueue.PushUnitDied(new UnitDiedEvent(CurrentTick, target.UnitId, u.UnitId));
            u.CurrentRage = global::System.Math.Min(u.RageCap, u.CurrentRage + 20); // 击杀回怒
        }
    }

    private void CastSkill(UnitSnapshot u, SkillDefinition skill, bool isUltimate)
    {
        if (isUltimate)
        {
            TotalUltimatesCast++;
            u.CurrentRage = global::System.Math.Max(0, u.CurrentRage - skill.RageCost);
        }
        else
        {
            TotalSkillsCast++;
            u.CurrentMana = global::System.Math.Max(0, u.CurrentMana - skill.ManaCost);
            u.CurrentRage = global::System.Math.Min(u.RageCap, u.CurrentRage + 20);
        }

        // 治疗技能逻辑：索敌同阵营存活队友（友方解析不做阵营过滤、不走黏性锁）
        if (skill.IsHeal)
        {
            var allies = u.Faction == Faction.Player ? Context.PlayerTeam : Context.EnemyTeam;
            var healTarget = TargetResolver.ResolveAllyTarget(u, allies, skill.TargetMode) ?? u;
            
            EventQueue.PushSkillCast(new SkillCastEvent(CurrentTick, u.UnitId, skill.SkillId, healTarget.UnitId, isUltimate));
            var healRes = HealPipeline.CalculateAndApply(u, healTarget, skill.HealRatioPermille);
            EventQueue.PushHeal(new HealEvent(CurrentTick, u.UnitId, healTarget.UnitId, healRes.FinalHeal));
            return;
        }

        // 伤害技能逻辑：索敌敌对阵营
        var enemies = u.Faction == Faction.Player ? Context.EnemyTeam : Context.PlayerTeam;
        var target = TargetResolver.ResolveTarget(u, skill.TargetMode, enemies, _lockTracker);
        if (target == null) return;

        EventQueue.PushSkillCast(new SkillCastEvent(CurrentTick, u.UnitId, skill.SkillId, target.UnitId, isUltimate));
        var res = DamagePipeline.CalculateAndApply(u, target, skill.DamageRatioPermille, skill.Type, DamageFlags.Direct);
        EventQueue.PushDamage(new DamageEvent(CurrentTick, u.UnitId, target.UnitId, res.FinalDamage, res.Flags, skill.Type));

        if (u.Faction == Faction.Player) TotalDamageDealtPlayer += res.FinalDamage;
        else TotalDamageDealtEnemy += res.FinalDamage;

        if (target.IsDead)
        {
            EventQueue.PushUnitDied(new UnitDiedEvent(CurrentTick, target.UnitId, u.UnitId));
            u.CurrentRage = global::System.Math.Min(u.RageCap, u.CurrentRage + 20); // 击杀回怒
        }
    }

    private void CheckSettlement()
    {
        bool playerAllDead = Context.PlayerTeam.All(u => u.IsDead);
        bool enemyAllDead = Context.EnemyTeam.All(u => u.IsDead);

        if (enemyAllDead)
        {
            IsFinished = true;
            Outcome = BattleOutcome.PlayerVictory;
        }
        else if (playerAllDead)
        {
            IsFinished = true;
            Outcome = BattleOutcome.PlayerDefeat;
        }
        else if (CurrentTick >= Context.MaxTicks)
        {
            IsFinished = true;
            Outcome = BattleOutcome.TimeOutDraw;
        }
    }

    public BattleResult RunToCompletion()
    {
        while (!IsFinished)
        {
            Step();
        }

        return new BattleResult(
            Outcome,
            CurrentTick,
            Context.PlayerTeam.Select(u => u.Clone()).ToList(),
            Context.EnemyTeam.Select(u => u.Clone()).ToList(),
            TotalSkillsCast,
            TotalUltimatesCast,
            TotalDamageDealtPlayer,
            TotalDamageDealtEnemy
        );
    }
}