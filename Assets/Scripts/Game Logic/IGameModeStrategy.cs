public interface IGameModeStrategy
{
    GameModeType Type { get; }
    bool IsEX { get; }
    bool IsHard { get; }

    // merge 가능한 최대 레벨 제한 (Normal: 10, EX: 12)
    int MergeMaxLevelExclusive { get; }

    // spawn_level clamp 규칙 (기존 로직 반영)
    int MaxSpawnLevel { get; }
}
