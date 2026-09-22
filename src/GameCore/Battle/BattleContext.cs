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
    public required IReadOnlyList<UnitSnapshot> PlayerTeam { get; init; }
    public required IReadOnlyList<UnitSnapshot> EnemyTeam { get; init; }
    public int RandomSeed { get; init; } = 42;
    public int TimeLimitSeconds { get; init; } = 90;
    public int MaxTicks => TimeLimitSeconds * 60;
}

public sealed class BattleResult
{
    public required BattleOutcome Outcome { get; init; }
    public required int ElapsedTicks { get; init; }
    public float ElapsedSeconds => ElapsedTicks / 60.0f;
    public float TotalSeconds => ElapsedSeconds;
    public int DurationTicks => ElapsedTicks;

    public required IReadOnlyList<UnitSnapshot> FinalPlayerUnits { get; init; }
    public required IReadOnlyList<UnitSnapshot> FinalEnemyUnits { get; init; }
    
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
            int alive = FinalPlayerUnits.Count(u => !u.IsDead);
            int total = FinalPlayerUnits.Count;
            if (total == 0) return 0;
            if (alive == total) return 3;
            if (alive >= total / 2) return 2;
            return 1;
        }
    }

    public Faction? Winner => Outcome switch
    {
        BattleOutcome.PlayerVictory => Faction.Player,
        BattleOutcome.PlayerDefeat => Faction.Enemy,
        _ => null
    };
}
