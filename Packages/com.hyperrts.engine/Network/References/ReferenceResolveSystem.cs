using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Upgrades;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Network.References
{
    /// <summary>
    /// Client: entity and asset references can't cross the network, so replicated buffers carry type ids and this
    /// fills the references back in from the client's own prefabs and assets.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial class ReferenceResolveSystem : SystemBase
    {
        private readonly Dictionary<int, ResourceType> _resources = new();
        private NativeHashMap<int, Entity> _prefabs;
        private EntityQuery _prefabQuery;

        protected override void OnCreate()
        {
            _prefabs = new NativeHashMap<int, Entity>(64, Allocator.Persistent);
            _prefabQuery = SystemAPI.QueryBuilder().WithAll<EntityInfo, Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build();
        }

        protected override void OnDestroy() => _prefabs.Dispose();

        protected override void OnUpdate()
        {
            foreach (var queue in SystemAPI.Query<DynamicBuffer<ProductionQueueItem>>())
            {
                for (var i = 0; i < queue.Length; i++)
                {
                    queue.ElementAt(i).Prefab = PrefabOf(queue[i].TypeId);
                }
            }

            foreach (var researched in SystemAPI.Query<DynamicBuffer<ResearchedUpgrade>>())
            {
                for (var i = 0; i < researched.Length; i++)
                {
                    researched.ElementAt(i).Upgrade = PrefabOf(researched[i].TypeId);
                }
            }

            foreach (var stock in SystemAPI.Query<DynamicBuffer<ResourceStock>>())
            {
                for (var i = 0; i < stock.Length; i++)
                {
                    stock.ElementAt(i).Type = ResourceOf(stock[i].TypeId);
                }
            }
        }

        private Entity PrefabOf(int typeId)
        {
            if (!_prefabs.TryGetValue(typeId, out var prefab) && _prefabs.Count != _prefabQuery.CalculateEntityCount())
            {
                _prefabs.Clear();
                foreach (var entity in _prefabQuery.ToEntityArray(Allocator.Temp))
                {
                    _prefabs[EntityManager.GetComponentData<EntityInfo>(entity).TypeId] = entity;
                }

                _prefabs.TryGetValue(typeId, out prefab);
            }

            return prefab;
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
