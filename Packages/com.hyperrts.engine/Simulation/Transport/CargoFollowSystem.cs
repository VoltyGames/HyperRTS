using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Units;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>
    /// Keeps passengers at their container's position so fog, range checks and exits start from there. Passengers
    /// whose container vanished without dying (sold, removed by game code) get out where they are.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct CargoFollowSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<CombatStance> _stances;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>();
            _stances = state.GetComponentLookup<CombatStance>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transforms.Update(ref state);
            _stances.Update(ref state);
            new FollowJob { Transforms = _transforms, Stances = _stances }.Schedule();
        }

        // Single-threaded: reads the container's transform and writes the passenger's through one lookup.
        [BurstCompile]
        private partial struct FollowJob : IJobEntity
        {
            public ComponentLookup<LocalTransform> Transforms;
            public ComponentLookup<CombatStance> Stances;

            private void Execute(Entity entity, ref Inside inside, EnabledRefRW<Inside> aboard)
            {
                if (!Transforms.TryGetComponent(inside.Container, out var container))
                {
                    aboard.ValueRW = false;
                    if (Stances.TryGetComponent(entity, out var stance))
                    {
                        Stances[entity] = new CombatStance { Value = inside.Stance, Anchor = stance.Anchor };
                    }

                    return;
                }

                var transform = Transforms[entity];
                transform.Position = container.Position;
                Transforms[entity] = transform;
            }
        }
    }
}
