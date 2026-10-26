using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// A building under construction marks <see cref="SceneryCleared"/> the open <see cref="Destructible"/> scenery its
    /// footprint overlaps (trees, fences, pipes crossing the edge) and the scenery remains on it, so they go with no
    /// remains, effects or sounds. A tree goes when its crown reaches over the site. Scenery with its own obstacle
    /// blocks placement instead. Scenery and sites stand still, so only a new site or new scenery is checked again;
    /// remains appear any time and are checked every frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct SiteClearingSystem : ISystem
    {
        private ComponentLookup<Destructible> _destructibles;
        private ComponentLookup<NavObstacle> _obstacles;
        private ComponentLookup<NavAgent> _agents;
        private ComponentLookup<Health> _health;
        private ComponentLookup<SceneryCleared> _cleared;
        private int _siteOrder;
        private int _sceneryOrder;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BuildingTag, ConstructionProgress>().Build());
            _destructibles = state.GetComponentLookup<Destructible>(true);
            _obstacles = state.GetComponentLookup<NavObstacle>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _health = state.GetComponentLookup<Health>(true);
            _cleared = state.GetComponentLookup<SceneryCleared>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _destructibles.Update(ref state);
            _obstacles.Update(ref state);
            _agents.Update(ref state);
            _health.Update(ref state);
            _cleared.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var siteOrder = state.EntityManager.GetComponentOrderVersion<ConstructionProgress>();
            var sceneryOrder = state.EntityManager.GetComponentOrderVersion<Destructible>();
            var changed = siteOrder != _siteOrder || sceneryOrder != _sceneryOrder;
            _siteOrder = siteOrder;
            _sceneryOrder = sceneryOrder;
            var sites = new NativeList<NavBox>(8, state.WorldUpdateAllocator);
            new SceneryJob
            {
                Search = changed,
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Ecb = ecb,
                Sites = sites,
                Destructibles = _destructibles,
                Obstacles = _obstacles,
                Agents = _agents,
                Health = _health,
                Cleared = _cleared,
            }.Schedule();
            new RemainsJob { Ecb = ecb, Sites = sites }.Schedule();
            if (changed)
            {
                new CrownJob { Ecb = ecb, Sites = sites }.Schedule();
            }
        }

        /// <summary>A piece of scenery or remains counts as reaching the site when its circle overlaps the box.</summary>
        private static bool Reaches(in NavBox site, float2 point, float radius) => site.Gap(point) < radius;

        /// <summary>Single-threaded: it lists the sites, searches them on a change and writes the one buffer.</summary>
        [BurstCompile]
        [WithAll(typeof(BuildingTag), typeof(ConstructionProgress))]
        [WithNone(typeof(Dead))]
        private partial struct SceneryJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            public EntityCommandBuffer Ecb;
            public NativeList<NavBox> Sites;
            public bool Search;
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            [ReadOnly] public ComponentLookup<NavObstacle> Obstacles;
            [ReadOnly] public ComponentLookup<NavAgent> Agents;
            [ReadOnly] public ComponentLookup<Health> Health;
            [ReadOnly] public ComponentLookup<SceneryCleared> Cleared;

            private void Execute(in NavObstacle obstacle, in LocalTransform transform)
            {
                var footprint = new Footprint
                {
                    Ecb = Ecb,
                    Destructibles = Destructibles,
                    Obstacles = Obstacles,
                    Agents = Agents,
                    Health = Health,
                    Cleared = Cleared,
                    Box = new NavBox(transform.Position, transform.Rotation, obstacle.Size),
                };
                Sites.Add(footprint.Box);
                if (!Search)
                {
                    return;
                }

                Index.Query(transform.Position, math.length(obstacle.Size) * 0.5f, ref footprint);
            }
        }

        /// <summary>Remains of map scenery carry its id and no health; solid ones block placement like the scenery.</summary>
        [BurstCompile]
        [WithAll(typeof(SceneryId))]
        [WithNone(typeof(Health), typeof(NavObstacle), typeof(SceneryCleared))]
        private partial struct RemainsJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;
            [ReadOnly] public NativeList<NavBox> Sites;

            private void Execute(Entity entity, in LocalTransform transform)
            {
                foreach (var site in Sites)
                {
                    if (Reaches(site, transform.Position.xz, EntityRadius.Default))
                    {
                        Ecb.AddComponent<SceneryCleared>(entity);
                        return;
                    }
                }
            }
        }

        /// <summary>A tree's trunk may stand clear while its crown hangs over the site, so its crown decides.</summary>
        [BurstCompile]
        [WithAll(typeof(Destructible))]
        [WithNone(typeof(NavObstacle), typeof(SceneryCleared))]
        private partial struct CrownJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;
            [ReadOnly] public NativeList<NavBox> Sites;

            private void Execute(Entity entity, in Topple topple, in LocalTransform transform, in Health health)
            {
                // A tree felled this frame leaves remains through the death path; the remains pass clears them.
                if (health.Current <= 0f)
                {
                    return;
                }

                foreach (var site in Sites)
                {
                    if (Reaches(site, transform.Position.xz, topple.Crown * transform.Scale))
                    {
                        Ecb.AddComponent<SceneryCleared>(entity);
                        return;
                    }
                }
            }
        }

        private struct Footprint : ISpatialVisitor
        {
            public EntityCommandBuffer Ecb;
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            [ReadOnly] public ComponentLookup<NavObstacle> Obstacles;
            [ReadOnly] public ComponentLookup<NavAgent> Agents;
            [ReadOnly] public ComponentLookup<Health> Health;
            [ReadOnly] public ComponentLookup<SceneryCleared> Cleared;
            public NavBox Box;

            public void Visit(in SpatialEntry entry)
            {
                var victim = entry.Entity;
                if (!IsOpenScenery(victim) || !Reaches(Box, entry.Position.xz, entry.Radius))
                {
                    return;
                }

                // Scenery dying this frame leaves remains through the death path; the next frame clears them.
                if (Common.Health.IsAlive(Health, victim) && !Cleared.HasComponent(victim))
                {
                    Ecb.AddComponent<SceneryCleared>(victim);
                }
            }

            // Units and buildings carry Destructible too, and solid scenery has an obstacle of its own.
            private readonly bool IsOpenScenery(Entity entity)
            {
                if (!Destructibles.HasComponent(entity))
                {
                    return false;
                }

                return !Obstacles.HasComponent(entity) && !Agents.HasComponent(entity);
            }
        }
    }
}
