using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Research a player has finished, in completion order; on the player entity.</summary>
    [InternalBufferCapacity(0)]
    public struct CompletedResearch : IPrefabReference
    {
        /// <summary>The research prefab (an upgrade holds its effects there). Clients resolve it from TypeId.</summary>
        [GhostField(SendData = false)] public Entity Prefab;

        [GhostField] public int TypeId;

        int IPrefabReference.TypeId => TypeId;

        Entity IPrefabReference.Prefab
        {
            get => Prefab;
            set => Prefab = value;
        }
    }
}
