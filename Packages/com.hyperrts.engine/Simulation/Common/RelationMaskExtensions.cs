namespace HyperRTS.Simulation.Common
{
    public static class RelationMaskExtensions
    {
        public static bool Includes(this RelationMask mask, Relation relation) =>
            ((int)mask & (1 << (int)relation)) != 0;
    }
}
