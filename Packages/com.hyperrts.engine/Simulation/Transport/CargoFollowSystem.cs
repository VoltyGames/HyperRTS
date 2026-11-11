using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using Unity.Burst;
using Unity.Collections;
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
    public partial struct CargoFollowSystem : ISystem
    {

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new FollowJob { Transforms = SystemAPI.GetComponentLookup<LocalTransform>(false), Stances = SystemAPI.GetComponentLookup<CombatStance>(false), Saved = SystemAPI.GetComponentLookup<PassengerStance>(true) }.Schedule();
        }

        // Single-threaded: reads the container's transform and writes the passenger's through one lookup.
        [BurstCompile]
        private partial struct FollowJob : IJobEntity
        {
            public ComponentLookup<LocalTransform> Transforms;
            public ComponentLookup<CombatStance> Stances;
            [ReadOnly] public ComponentLookup<PassengerStance> Saved;

            private void Execute(Entity entity, ref Inside inside, EnabledRefRW<Inside> aboard)
            {
                if (!Transforms.TryGetComponent(inside.Container, out var container))
                {
                    aboard.ValueRW = false;
                    if (Stances.TryGetComponent(entity, out var stance))
                    {
                        var restored = Saved.TryGetComponent(entity, out var saved) ? saved.Value : default;
                        Stances[entity] = stance.WithPost(restored, stance.ReturnPoint);
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
