using HyperRTS.Simulation.Common.Players;

namespace HyperRTS.Simulation.Commands;

/// <summary>Whether a RelationMask includes a Relation.</summary>
public static class RelationMaskExtensions
{
    extension(RelationMask mask)
    {
        public bool Includes(Relation relation) =>
            ((int)mask & (1 << (int)relation)) != 0;
    }
}
