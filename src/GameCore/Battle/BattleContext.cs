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
}

public sealed class BattleResult
{
    public required BattleOutcome Outcome { get; init; }
    public required int ElapsedTicks { get; init; }
    public required float ElapsedSeconds => ElapsedTicks / 60.0f;
    public required IReadOnlyList<UnitSnapshot> FinalPlayerUnits { get; init; }
    public required IReadOnlyList<UnitSnapshot> FinalEnemyUnits { get; init; }
    public required int TotalDamageDealtByPlayer { get; init; }
    public required int TotalDamageDealtByEnemy { get; init; }
    public required int TotalSkillsCast { get; init; }
    public required int TotalUltimatesCast { get; init; }
}
