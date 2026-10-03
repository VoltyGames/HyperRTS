namespace HyperRTS.Presentation.Common
{
    /// <summary>How an entity's owner relates to the local player, for colour-coding.</summary>
    public enum Relation : byte
    {
        Neutral = 0,
        Own = 1,
        Ally = 2,
        Enemy = 3,
    }
}
