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
            u.CurrentGauge += (u.BaseSpeed * 1000) / (60 * 100);
        }
    }

    private void ExecuteTurn(UnitSnapshot u)
    {
        var enemies = u.Faction == Faction.Player ? Context.EnemyTeam : Context.PlayerTeam;
        var target = TargetResolver.ResolveTarget(u, TargetMode.Nearest, enemies, _lockTracker);
        if (target == null) return;

        // 优先级 1：满怒释放大招
        if (u.CurrentRage >= 100 && u.UltimateSkill != null)
        {
            CastSkill(u, target, u.UltimateSkill, isUltimate: true);
            u.CurrentRage = 0;
            return;
        }

        // 优先级 2：满蓝释放战技
        if (u.CurrentMana >= 100 && u.ActiveSkill != null)
        {
            CastSkill(u, target, u.ActiveSkill, isUltimate: false);
            u.CurrentMana = 0;
            u.CurrentRage = global::System.Math.Min(100, u.CurrentRage + 20);
            return;
        }

        // 优先级 3：普攻
        BasicAttack(u, target);
        u.CurrentMana = global::System.Math.Min(100, u.CurrentMana + 10);
        u.CurrentRage = global::System.Math.Min(100, u.CurrentRage + 10);
    }

    private void BasicAttack(UnitSnapshot u, UnitSnapshot target)
    {
        var res = DamagePipeline.CalculateAndApply(u, target, 1000, DamageType.Physical, DamageFlags.Direct);
        EventQueue.PushDamage(new DamageEvent(CurrentTick, u.UnitId, target.UnitId, res.FinalDamage, res.Flags, DamageType.Physical));
        
        if (u.Faction == Faction.Player) TotalDamageDealtPlayer += res.FinalDamage;
        else TotalDamageDealtEnemy += res.FinalDamage;

        if (target.IsDead)
        {
            EventQueue.PushUnitDied(new UnitDiedEvent(CurrentTick, target.UnitId, u.UnitId));
            u.CurrentRage = global::System.Math.Min(100, u.CurrentRage + 20); // 击杀回怒
        }
    }

    private void CastSkill(UnitSnapshot u, UnitSnapshot target, SkillDefinition skill, bool isUltimate)
    {
        if (isUltimate) TotalUltimatesCast++;
        else TotalSkillsCast++;

        EventQueue.PushSkillCast(new SkillCastEvent(CurrentTick, u.UnitId, skill.SkillId, target.UnitId, isUltimate));
        var res = DamagePipeline.CalculateAndApply(u, target, skill.DamageRatioPermille, skill.Type, DamageFlags.Direct);
        EventQueue.PushDamage(new DamageEvent(CurrentTick, u.UnitId, target.UnitId, res.FinalDamage, res.Flags, skill.Type));

        if (u.Faction == Faction.Player) TotalDamageDealtPlayer += res.FinalDamage;
        else TotalDamageDealtEnemy += res.FinalDamage;

        if (target.IsDead)
        {
            EventQueue.PushUnitDied(new UnitDiedEvent(CurrentTick, target.UnitId, u.UnitId));
            u.CurrentRage = global::System.Math.Min(100, u.CurrentRage + 20);
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