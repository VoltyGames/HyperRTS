using HyperRTS.Simulation.Destruction;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering.Parts
{
    /// <summary>
    /// Turns the models a root shows only now and then into entity prefabs that <see cref="ModelCopies"/> copies in
    /// while they show: every section model but the intact one, authored at scale 0 so editor views show the standing
    /// building, and every damage stage but the first. The root's <see cref="LinkedEntityGroup"/> lists the rest of
    /// it, so it is destroyed whole with its copies.
    /// </summary>
    // After the groups bake, so a prefab root's group can drop the model prefabs.
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial struct ModelCopyBakingSystem : ISystem
    {
        private EntityQuery _roots;
        private EntityQuery _parented;

        public void OnCreate(ref SystemState state)
        {
            const EntityQueryOptions options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities |
                                               EntityQueryOptions.IgnoreComponentEnabledState;
            _roots = new EntityQueryBuilder(Allocator.Temp).WithAny<SectionModel, DamageStage>().WithOptions(options)
                .Build(ref state);
            _parented = new EntityQueryBuilder(Allocator.Temp).WithAll<Parent>().WithOptions(options).Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            using var roots = _roots.ToEntityArray(Allocator.Temp);
            if (roots.Length == 0)
            {
                return;
            }

            var owners = new NativeHashSet<Entity>(64, Allocator.Temp);
            var models = new NativeHashSet<Entity>(64, Allocator.Temp);
            foreach (var root in roots)
            {
                owners.Add(root);
                Models(ref state, root, models);
            }

            foreach (var model in models)
            {
                owners.Add(model);
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

            foreach (var root in roots)
            {
                Group(ref state, root, members, prefabParts);
                if (!state.EntityManager.HasBuffer<ModelCopy>(root))
                {
                    state.EntityManager.AddBuffer<ModelCopy>(root);
                }
            }
        }

        private static void Models(ref SystemState state, Entity root, NativeHashSet<Entity> models)
        {
            var em = state.EntityManager;
            if (em.HasBuffer<SectionModel>(root))
            {
                foreach (var model in em.GetBuffer<SectionModel>(root, true))
                {
                    model.ForEachPrefab(prefab => models.Add(prefab));
                }
            }

            if (em.HasBuffer<DamageStage>(root))
            {
                var stages = em.GetBuffer<DamageStage>(root, true);
                for (var i = 1; i < stages.Length; i++)
                {
                    var visual = stages[i].Visual;
                    if (visual != Entity.Null && visual != stages[0].Visual && visual != root)
                    {
                        models.Add(visual);
                    }
                }
            }
        }

        /// <summary>Every entity under a root or model, keyed by the nearest of them above it.</summary>
        private NativeParallelMultiHashMap<Entity, Entity> Members(ref SystemState state, NativeHashSet<Entity> owners)
        {
            var parents = SystemAPI.GetComponentLookup<Parent>(true);
            using var parented = _parented.ToEntityArray(Allocator.Temp);
            var members = new NativeParallelMultiHashMap<Entity, Entity>(parented.Length, Allocator.Temp);
            foreach (var entity in parented)
            {
                if (owners.Contains(entity))
                {
                    continue;
                }

                var owner = parents[entity].Value;
                while (!owners.Contains(owner) && parents.TryGetComponent(owner, out var parent))
                {
                    owner = parent.Value;
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
            if (transform.Scale == 0f)
            {
                transform.Scale = 1f;
                em.SetComponentData(model, transform);
            }

            var group = OwnGroup(model, members);
            em.AddComponent<Prefab>(group.AsArray());
            var linked = em.HasBuffer<LinkedEntityGroup>(model)
                ? em.GetBuffer<LinkedEntityGroup>(model)
                : em.AddBuffer<LinkedEntityGroup>(model);
            linked.Clear();
            linked.AddRange(group.AsArray().Reinterpret<LinkedEntityGroup>());
        }

        private static NativeList<Entity> OwnGroup(Entity owner, NativeParallelMultiHashMap<Entity, Entity> members)
        {
            var group = new NativeList<Entity>(Allocator.Temp) { owner };
            foreach (var member in members.GetValuesForKey(owner))
            {
                group.Add(member);
            }

            return group;
        }

        // A root baked as a prefab already lists everything; a SubScene root gets a group of its own parts.
        private static void Group(ref SystemState state, Entity root, NativeParallelMultiHashMap<Entity, Entity> members,
            NativeHashSet<Entity> prefabParts)
        {
            var em = state.EntityManager;
            if (!em.HasBuffer<LinkedEntityGroup>(root))
            {
                var group = OwnGroup(root, members);
                em.AddBuffer<LinkedEntityGroup>(root).AddRange(group.AsArray().Reinterpret<LinkedEntityGroup>());
                return;
            }

            var linked = em.GetBuffer<LinkedEntityGroup>(root);
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
