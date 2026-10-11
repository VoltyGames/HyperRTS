using System;

namespace HyperRTS.Simulation.Common
{
    /// <summary>A set of <see cref="Relation"/>s, for rules that apply to some of them.</summary>
    [Flags]
    public enum RelationMask : byte
    {
        None = 0,
        Neutral = 1 << (int)Relation.Neutral,
        Own = 1 << (int)Relation.Own,
        Ally = 1 << (int)Relation.Ally,
        Enemy = 1 << (int)Relation.Enemy,
        Friendly = Own | Ally,
        Any = Neutral | Own | Ally | Enemy,
    }
}
