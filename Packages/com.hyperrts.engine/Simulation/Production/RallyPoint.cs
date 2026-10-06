using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Where new units walk after spawning; disabled means stay at the spawn point.</summary>
    public struct RallyPoint : IComponentData, IEnableableComponent
    {
        public float3 Position;
    }
}
