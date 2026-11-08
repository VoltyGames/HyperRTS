using HyperRTS.Simulation.Destruction;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Turns every section model but the intact one, authored at scale 0 so editor views show the standing building,
    /// into a full-size entity prefab that <see cref="SectionViewSystem"/> copies in while it shows. The building's
    /// <see cref="LinkedEntityGroup"/> lists the rest of it, so it is destroyed whole with its copies.
    /// </summary>
    // After the groups bake, so a prefab building's group can drop the section prefabs.
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial struct SectionsBakingSystem : ISystem
    {
        private EntityQuery _buildings;
        private EntityQuery _parented;

        public void OnCreate(ref SystemState state)
        {
            const EntityQueryOptions options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities |
                                               EntityQueryOptions.IgnoreComponentEnabledState;
            _buildings = new EntityQueryBuilder(Allocator.Temp).WithAll<SectionModel>().WithOptions(options).Build(ref state);
            _parented = new EntityQueryBuilder(Allocator.Temp).WithAll<Parent>().WithOptions(options).Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            using var buildings = _buildings.ToEntityArray(Allocator.Temp);
            if (buildings.Length == 0)
            {
                return;
            }

            var owners = new NativeHashSet<Entity>(64, Allocator.Temp);
            var models = new NativeHashSet<Entity>(64, Allocator.Temp);
            foreach (var building in buildings)
            {
                owners.Add(building);
                foreach (var model in state.EntityManager.GetBuffer<SectionModel>(building, true))
                {
                    model.ForEachPrefab(prefab =>
                    {
                        owners.Add(prefab);
                        models.Add(prefab);
                    });
                }
            }

            var members = Members(ref state, owners);
            var prefabParts = new NativeHashSet<Entity>(members.Count(), Allocator.Temp);
            foreach (var model in models)
            {
                prefabParts.Add(model);
                foreach (var member in members.GetValuesForKey(model))
                {
                    prefabParts.Add(member);
                }

                MakePrefab(ref state, model, members);
            }

            foreach (var building in buildings)
            {
                Group(ref state, building, members, prefabParts);
            }
        }

        /// <summary>Every entity under a building or section model, keyed by the nearest of them above it.</summary>
        private NativeParallelMultiHashMap<Entity, Entity> Members(ref SystemState state, NativeHashSet<Entity> owners)
        {
            var parents = SystemAPI.GetComponentLookup<Parent>(true);
            using var parented = _parented.ToEntityArray(Allocator.Temp);
            var members = new NativeParallelMultiHashMap<Entity, Entity>(parented.Length, Allocator.Temp);
            foreach (var entity in parented)
            {
                var owner = parents[entity].Value;
                while (!owners.Contains(owner) && parents.TryGetComponent(owner, out var parent))
                {
                    owner = parent.Value;
                }

                if (owners.Contains(entity))
                {
                    continue;
                }

                if (owners.Contains(owner))
                {
                    members.Add(owner, entity);
                }
            }

            return members;
        }

        private static void MakePrefab(ref SystemState state, Entity model, NativeParallelMultiHashMap<Entity, Entity> members)
        {
            var em = state.EntityManager;
            var transform = em.GetComponentData<LocalTransform>(model);
            transform.Scale = 1f;
            em.SetComponentData(model, transform);

            var group = new NativeList<Entity>(Allocator.Temp) { model };
            foreach (var member in members.GetValuesForKey(model))
            {
                group.Add(member);
            }

            em.AddComponent<Prefab>(group.AsArray());
            var linked = em.HasBuffer<LinkedEntityGroup>(model)
                ? em.GetBuffer<LinkedEntityGroup>(model)
                : em.AddBuffer<LinkedEntityGroup>(model);
            linked.Clear();
            linked.AddRange(group.AsArray().Reinterpret<LinkedEntityGroup>());
        }

        // A building baked as a prefab already lists everything; a scene building gets a group of its own parts.
        private static void Group(ref SystemState state, Entity building, NativeParallelMultiHashMap<Entity, Entity> members,
            NativeHashSet<Entity> prefabParts)
        {
            var em = state.EntityManager;
            if (!em.HasBuffer<LinkedEntityGroup>(building))
            {
                var group = new NativeList<Entity>(Allocator.Temp) { building };
                foreach (var member in members.GetValuesForKey(building))
                {
                    group.Add(member);
                }

                em.AddBuffer<LinkedEntityGroup>(building).AddRange(group.AsArray().Reinterpret<LinkedEntityGroup>());
                return;
            }

            var linked = em.GetBuffer<LinkedEntityGroup>(building);
            for (var i = linked.Length - 1; i > 0; i--)
            {
                if (prefabParts.Contains(linked[i].Value))
                {
                    linked.RemoveAt(i);
                }
            }
        }
    }
}
