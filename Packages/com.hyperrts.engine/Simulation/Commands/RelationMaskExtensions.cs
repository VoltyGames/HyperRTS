using HyperRTS.Simulation.Common.Players;


namespace HyperRTS.Simulation.Commands
{
    /// <summary>Whether a RelationMask includes a Relation.</summary>
    public static class RelationMaskExtensions
    {
        public static bool Includes(this RelationMask mask, Relation relation) =>
            ((int)mask & (1 << (int)relation)) != 0;
    }
}
