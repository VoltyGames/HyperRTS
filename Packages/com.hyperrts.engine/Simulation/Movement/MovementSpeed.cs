using HyperRTS.Simulation.Stats;
using Unity.Entities;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>Top speed in world units per second.</summary>
    public struct MovementSpeed : IComponentData
    {
        [StatField(Stat.MoveSpeed)] public float Value;
    }
}
