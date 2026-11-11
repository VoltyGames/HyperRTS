using HyperRTS.Simulation.Common.Prefabs;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Research a player has finished, in completion order; on the player entity.</summary>
    [InternalBufferCapacity(0)]
    public struct CompletedResearch : IPrefabReference
    {
        /// <summary>The research prefab (an upgrade holds its effects there). Clients resolve it from TypeId.</summary>
        [GhostField(SendData = false)] public Entity Prefab;

        [GhostField] public int TypeId;

        public static bool Has(DynamicBuffer<CompletedResearch> completed, Entity prefab)
        {
            foreach (var item in completed)
            {
                if (item.Prefab == prefab)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Has(DynamicBuffer<CompletedResearch> completed, int typeId)
        {
            foreach (var item in completed)
            {
                if (item.TypeId == typeId)
                {
                    return true;
                }
            }

            return false;
        }

        int IPrefabReference.TypeId => TypeId;

        Entity IPrefabReference.Prefab
        {
            get => Prefab;
            set => Prefab = value;
        }
    }
}
