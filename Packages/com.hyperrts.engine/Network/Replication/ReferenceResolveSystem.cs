using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Upgrades;
using Unity.Entities;

namespace HyperRTS.Network.Replication
{
    /// <summary>
    /// Client: entity and asset references can't cross the network, so replicated buffers carry type ids and this
    /// fills the references back in from the client's own prefabs and assets whenever a snapshot changes them.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial class ReferenceResolveSystem : SystemBase
    {
        private readonly Dictionary<int, ResourceType> _resources = new();

        protected override void OnCreate() => RequireForUpdate<PrefabRegistry>();

        protected override void OnUpdate()
        {
            var prefabs = SystemAPI.GetSingleton<PrefabRegistry>();
            foreach (var queue in SystemAPI.Query<DynamicBuffer<ProductionQueueItem>>()
                         .WithChangeFilter<ProductionQueueItem>())
            {
                for (var i = 0; i < queue.Length; i++)
                {
                    queue.ElementAt(i).Prefab = prefabs.Find(queue[i].TypeId);
                }
            }

            foreach (var researched in SystemAPI.Query<DynamicBuffer<ResearchedUpgrade>>()
                         .WithChangeFilter<ResearchedUpgrade>())
            {
                for (var i = 0; i < researched.Length; i++)
                {
                    researched.ElementAt(i).Upgrade = prefabs.Find(researched[i].TypeId);
                }
            }

            foreach (var stock in SystemAPI.Query<DynamicBuffer<ResourceStock>>().WithChangeFilter<ResourceStock>())
            {
                for (var i = 0; i < stock.Length; i++)
                {
                    stock.ElementAt(i).Type = ResourceOf(stock[i].TypeId);
                }
            }
        }

        // Assets referenced by the SubScene are loaded, so they can be found by id.
        private ResourceType ResourceOf(int typeId)
        {
            if (typeId == 0)
            {
                return null;
            }

            if (!_resources.TryGetValue(typeId, out var type))
            {
                foreach (var loaded in UnityEngine.Resources.FindObjectsOfTypeAll<ResourceType>())
                {
                    _resources[loaded.Id] = loaded;
                }

                _resources.TryGetValue(typeId, out type);
            }

            return type;
        }
    }
}
