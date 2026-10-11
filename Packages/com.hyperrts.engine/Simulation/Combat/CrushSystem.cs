using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Crushers flatten the <see cref="Destructible"/> scenery they drive over (fences, hay, small trees) with a
    /// lethal hit, so remains and sounds follow the normal death path. Crushing credits no one.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(DamageSystem))]
    public partial struct CrushSystem : ISystem
    {
        private const float Reach = 0.5f;
        private const float Lethal = 1e6f;

        private DamageWriter _damage;
        private ComponentLookup<Destructible> _destructibles;
        private ComponentLookup<Health> _health;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<Crusher>();
            _damage = new DamageWriter(ref state);
            _destructibles = state.GetComponentLookup<Destructible>(true);
            _health = state.GetComponentLookup<Health>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());
            _destructibles.Update(ref state);
            _health.Update(ref state);
            new CrushJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Damage = _damage,
                Destructibles = _destructibles,
                Health = _health,
            }.Schedule();
        }

        /// <summary>Single-threaded: hits go into the one damage queue.</summary>
        [BurstCompile]
        [WithNone(typeof(Inside), typeof(Dead))]
        private partial struct CrushJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            public DamageWriter Damage;
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            [ReadOnly] public ComponentLookup<Health> Health;

            private void Execute(Entity entity, in Crusher crusher, in NavAgent agent, in LocalTransform transform)
            {
                if (agent.Layer == NavLayer.Air || agent.Layer == NavLayer.Naval)
                {
                    return;
                }

                var underfoot = new Underfoot { Victims = new NativeList<Entity>(8, Allocator.Temp) };
                Index.Query(transform.Position, agent.Radius + Reach, ref underfoot);
                foreach (var victim in underfoot.Victims)
                {
                    var crushable = Destructibles.TryGetComponent(victim, out var destructible) && destructible.Crush > 0;
                    if (!crushable || destructible.Crush > crusher.Size || !Common.Health.IsAlive(Health, victim))
                    {
                        continue;
                    }

                    Damage.Add(new DamageEvent
                    {
                        Target = victim,
                        Position = transform.Position,
                        Origin = transform.Position,
                        Source = entity,
                        Amount = Lethal,
                    });
                }
            }
        }

        private struct Underfoot : ISpatialVisitor
        {
            public NativeList<Entity> Victims;

            public void Visit(in SpatialEntry entry) => Victims.Add(entry.Entity);
        }
    }
}
