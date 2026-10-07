using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Common
{
    /// <summary>The game's army or nation id for a player (0 = none); the engine never interprets it.</summary>
    public struct PlayerSide : IComponentData
    {
        [GhostField] public byte Value;
    }
}
