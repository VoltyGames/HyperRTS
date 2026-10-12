using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Most of this type one player may have at once, counting standing, unfinished and queued ones.</summary>
    public struct BuildLimit : IComponentData
    {
        public int Max;
    }
}
