using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision.Detection
{
    /// <summary>
    /// Turns <see cref="Stealth"/> into <see cref="Stealthed"/>, or <see cref="Concealed"/> for stealth that needs
    /// concealing cover: hidden while stealth is enabled, unless the entity fired within its reveal time, moves while
    /// still-only, or stands outside cover it needs. Firing restarts the timer in <c>WeaponFireSystem</c>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct StealthSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.TryGetSingleton<SightBlockers>(out var blockers);
            new StealthJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime, Moves = SystemAPI.GetComponentLookup<MoveDestination>(true),
                Blockers = blockers,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(Stealth), typeof(Stealthed), typeof(Concealed))]
        private partial struct StealthJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<MoveDestination> Moves;
            [ReadOnly] public SightBlockers Blockers;

            private void Execute(Entity entity, in LocalTransform transform, ref Stealth stealth,
                EnabledRefRW<Stealth> enabled, EnabledRefRW<Stealthed> stealthed, EnabledRefRW<Concealed> concealed)
            {
                stealth.RevealTimer = math.max(0f, stealth.RevealTimer - DeltaTime);
                var hidden = enabled.ValueRO && stealth.RevealTimer <= 0f;
                if (stealth.OnlyWhenStill && Moves.HasEnabled(entity))
                {
                    hidden = false;
                }

                if (stealth.InConcealment)
                {
                    Set(concealed, hidden && Blockers.IsCreated && Blockers.IsConcealing(transform.Position));
                    Set(stealthed, false);
                    return;
                }

                Set(stealthed, hidden);
            }

            private static void Set<T>(EnabledRefRW<T> flag, bool value) where T : unmanaged, IComponentData, IEnableableComponent
            {
                if (flag.ValueRO != value)
                {
                    flag.ValueRW = value;
                }
            }
        }
    }
}
