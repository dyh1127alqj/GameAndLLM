using GameCore.Model;

namespace GameCore.Battle;

public enum BattleOutcome
{
    InProgress,
    PlayerVictory,
    PlayerDefeat,
    TimeOutDraw
}

public sealed class BattleContext
{
    public IReadOnlyList<UnitSnapshot> PlayerTeam { get; init; } = Array.Empty<UnitSnapshot>();
    public IReadOnlyList<UnitSnapshot> EnemyTeam { get; init; } = Array.Empty<UnitSnapshot>();
    public int RandomSeed { get; init; } = 42;
    public int TimeLimitSeconds { get; init; } = 90;
    public int MaxTicks => TimeLimitSeconds * 60;

    public BattleContext() { }

    public BattleContext(
        IReadOnlyList<UnitSnapshot> playerTeam,
        IReadOnlyList<UnitSnapshot> enemyTeam,
        int seed = 42,
        int timeLimitSeconds = 90)
    {
        PlayerTeam = playerTeam;
        EnemyTeam = enemyTeam;
        RandomSeed = seed;
        TimeLimitSeconds = timeLimitSeconds;
    }
}

public sealed class BattleResult
{
    public BattleOutcome Outcome { get; init; } = BattleOutcome.InProgress;
    public int ElapsedTicks { get; init; }
    public float ElapsedSeconds => ElapsedTicks / 60.0f;
    public float TotalSeconds => ElapsedSeconds;
    public int DurationTicks => ElapsedTicks;

    public IReadOnlyList<UnitSnapshot> FinalPlayerUnits { get; init; } = Array.Empty<UnitSnapshot>();
    public IReadOnlyList<UnitSnapshot> FinalEnemyUnits { get; init; } = Array.Empty<UnitSnapshot>();
    
    public int TotalDamageDealtByPlayer { get; init; }
    public int TotalDamageDealtByEnemy { get; init; }
    public int TotalDamageDealt => TotalDamageDealtByPlayer + TotalDamageDealtByEnemy;

    public int TotalSkillsCast { get; init; }
    public int TotalUltimatesCast { get; init; }
    public float SkillsCastPerUnit => (TotalSkillsCast + TotalUltimatesCast) / 10.0f;

    public int SurvivorsCount => FinalPlayerUnits.Count(u => !u.IsDead);
    public int StarRating
    {
        get
        {
            if (Outcome != BattleOutcome.PlayerVictory) return 0;
            int total = FinalPlayerUnits.Count;
            if (total == 0) return 0;
            int alive = FinalPlayerUnits.Count(u => !u.IsDead);
            float ratio = (float)alive / total;
            if (ratio >= 1.0f) return 3;
            if (ratio >= 0.5f) return 2;
            if (alive > 0) return 1;
            return 0;
        }
    }

    public Faction? Winner => Outcome switch
    {
        BattleOutcome.PlayerVictory => Faction.Player,
        BattleOutcome.PlayerDefeat => Faction.Enemy,
        _ => null
    };

    public BattleResult() { }

    public BattleResult(
        BattleOutcome outcome,
        int elapsedTicks,
        IReadOnlyList<UnitSnapshot> finalPlayerUnits,
        IReadOnlyList<UnitSnapshot> finalEnemyUnits,
        int totalSkillsCast = 0,
        int totalUltimatesCast = 0,
        int totalDamageDealtPlayer = 0,
        int totalDamageDealtEnemy = 0)
    {
        Outcome = outcome;
        ElapsedTicks = elapsedTicks;
        FinalPlayerUnits = finalPlayerUnits;
        FinalEnemyUnits = finalEnemyUnits;
        TotalSkillsCast = totalSkillsCast;
        TotalUltimatesCast = totalUltimatesCast;
        TotalDamageDealtByPlayer = totalDamageDealtPlayer;
        TotalDamageDealtByEnemy = totalDamageDealtEnemy;
    }
}
