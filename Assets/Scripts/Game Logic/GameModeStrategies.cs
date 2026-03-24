public sealed class NormalStrategy : IGameModeStrategy
{
    public GameModeType Type => GameModeType.Normal;
    public bool IsEX => false;
    public bool IsHard => false;
    public int MergeMaxLevelExclusive => 10;
    public int MaxSpawnLevel => 5;
}

public sealed class ExStrategy : IGameModeStrategy
{
    public GameModeType Type => GameModeType.Ex;
    public bool IsEX => true;
    public bool IsHard => false;
    public int MergeMaxLevelExclusive => 12;
    public int MaxSpawnLevel => 7;
}

public sealed class NormalHardStrategy : IGameModeStrategy
{
    public GameModeType Type => GameModeType.NormalHard;
    public bool IsEX => false;
    public bool IsHard => true;
    public int MergeMaxLevelExclusive => 10;
    public int MaxSpawnLevel => 5;
}

public sealed class ExHardStrategy : IGameModeStrategy
{
    public GameModeType Type => GameModeType.ExHard;
    public bool IsEX => true;
    public bool IsHard => true;
    public int MergeMaxLevelExclusive => 12;
    public int MaxSpawnLevel => 5; // hard면 5로 clamp (기존 로직)
}

public static class GameModeStrategyFactory
{
    public static IGameModeStrategy Create(GameModeType t) => t switch
    {
        GameModeType.Normal => new NormalStrategy(),
        GameModeType.Ex => new ExStrategy(),
        GameModeType.NormalHard => new NormalHardStrategy(),
        GameModeType.ExHard => new ExHardStrategy(),
        _ => new NormalStrategy()
    };
}
