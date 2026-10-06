using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>A player's stockpile, one element per resource type held.</summary>
    [InternalBufferCapacity(4)]
    public struct ResourceStock : IBufferElementData
    {
        /// <summary>Not replicated; clients resolve it from <see cref="TypeId"/>.</summary>
        [GhostField(SendData = false)] public UnityObjectRef<ResourceType> Type;

        /// <summary><see cref="ResourceType.Id"/>, filled in on the server.</summary>
        [GhostField] public int TypeId;

        [GhostField] public int Amount;
    }
}
