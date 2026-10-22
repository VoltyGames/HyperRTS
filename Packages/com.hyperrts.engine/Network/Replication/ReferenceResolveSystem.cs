using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Network.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Entities;

namespace HyperRTS.Network.Replication
{
    /// <summary>
    /// Client: entity and asset references can't cross the network, so replicated buffers carry type ids and this
    /// fills the references back in from the client's own prefabs and assets whenever a snapshot changes them.
    /// Games resolve their own <see cref="IPrefabReference"/> buffers the same way with <see cref="PrefabReferences"/>.
    /// </summary>
    [RemoteClientOnly]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial class ReferenceResolveSystem : SystemBase
    {
        private readonly Dictionary<int, ResourceType> _resources = new();
        private EntityQuery _queues;
        private EntityQuery _research;

        protected override void OnCreate()
        {
            _queues = PrefabReferences.Changed<ProductionQueueItem>(ref CheckedStateRef);
            _research = PrefabReferences.Changed<CompletedResearch>(ref CheckedStateRef);
            RequireForUpdate<PrefabRegistry>();
        }

        protected override void OnUpdate()
        {
            var prefabs = SystemAPI.GetSingleton<PrefabRegistry>();
            PrefabReferences.Resolve<ProductionQueueItem>(EntityManager, _queues, prefabs);
            PrefabReferences.Resolve<CompletedResearch>(EntityManager, _research, prefabs);

            foreach (var stock in SystemAPI.Query<DynamicBuffer<ResourceStock>>().WithChangeFilter<ResourceStock>())
            {
                for (var i = 0; i < stock.Length; i++)
                {
                    stock.ElementAt(i).Type = FindResource(stock[i].TypeId);
                }
            }
        }

        // Assets referenced by the SubScene are loaded, so they can be found by id.
        private ResourceType FindResource(int typeId)
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
