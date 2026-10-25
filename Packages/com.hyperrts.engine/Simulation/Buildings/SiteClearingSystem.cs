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
    /// A building under construction flattens the open <see cref="Destructible"/> scenery inside its footprint (trees,
    /// fences, small props) with a lethal hit, so remains and sync follow the normal death path. Scenery with its own
    /// obstacle blocks placement instead.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(DamageSystem))]
    public partial struct SiteClearingSystem : ISystem
    {
        private const float Lethal = 1e6f;

        private DamageWriter _damage;
        private ComponentLookup<Destructible> _destructibles;
        private ComponentLookup<NavObstacle> _obstacles;
        private ComponentLookup<NavAgent> _agents;
        private ComponentLookup<Health> _health;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<ConstructionProgress>();
            _damage = new DamageWriter(ref state);
            _destructibles = state.GetComponentLookup<Destructible>(true);
            _obstacles = state.GetComponentLookup<NavObstacle>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _health = state.GetComponentLookup<Health>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());
            _destructibles.Update(ref state);
            _obstacles.Update(ref state);
            _agents.Update(ref state);
            _health.Update(ref state);
            new ClearJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Damage = _damage,
                Destructibles = _destructibles,
                Obstacles = _obstacles,
                Agents = _agents,
                Health = _health,
            }.Schedule();
        }

        /// <summary>Single-threaded: hits go into the one damage queue.</summary>
        [BurstCompile]
        [WithAll(typeof(BuildingTag), typeof(ConstructionProgress))]
        [WithNone(typeof(Dead))]
        private partial struct ClearJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            public DamageWriter Damage;
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            [ReadOnly] public ComponentLookup<NavObstacle> Obstacles;
            [ReadOnly] public ComponentLookup<NavAgent> Agents;
            [ReadOnly] public ComponentLookup<Health> Health;

            private void Execute(Entity entity, in NavObstacle obstacle, in LocalTransform transform)
            {
                var footprint = new Footprint
                {
                    Damage = Damage,
                    Destructibles = Destructibles,
                    Obstacles = Obstacles,
                    Agents = Agents,
                    Health = Health,
                    Site = entity,
                    Box = new NavBox(transform.Position, transform.Rotation, obstacle.Size),
                    Origin = transform.Position,
                };
                Index.Query(transform.Position, math.length(obstacle.Size) * 0.5f, ref footprint);
            }
        }

        private struct Footprint : ISpatialVisitor
        {
            public DamageWriter Damage;
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            [ReadOnly] public ComponentLookup<NavObstacle> Obstacles;
            [ReadOnly] public ComponentLookup<NavAgent> Agents;
            [ReadOnly] public ComponentLookup<Health> Health;
            public Entity Site;
            public NavBox Box;
            public float3 Origin;

            public void Visit(in SpatialEntry entry)
            {
                var victim = entry.Entity;
                if (!IsOpenScenery(victim) || !Box.Contains(entry.Position.xz, 0f))
                {
                    return;
                }

                if (!Common.Health.IsAlive(Health, victim))
                {
                    return;
                }

                Damage.Add(new DamageEvent
                {
                    Target = victim,
                    Position = entry.Position,
                    Origin = Origin,
                    Source = Site,
                    Amount = Lethal,
                });
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
