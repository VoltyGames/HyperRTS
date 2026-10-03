using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>Locomotion goal on the XZ plane: set and enable it to move, disabled again on arrival.</summary>
    public struct MoveDestination : IComponentData, IEnableableComponent
    {
        public float3 Value;
    }
}
