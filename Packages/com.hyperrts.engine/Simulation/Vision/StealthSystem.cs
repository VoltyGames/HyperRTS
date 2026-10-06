using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Turns <see cref="Stealth"/> into <see cref="Stealthed"/>: hidden while stealth is enabled, unless the entity
    /// fired within its reveal time or, for still-only stealth, is moving. Firing restarts the timer in
    /// <c>WeaponFireSystem</c>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct StealthSystem : ISystem
    {
        private ComponentLookup<MoveDestination> _moves;

        [BurstCompile]
        public void OnCreate(ref SystemState state) => _moves = state.GetComponentLookup<MoveDestination>(true);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _moves.Update(ref state);
            new StealthJob { DeltaTime = SystemAPI.Time.DeltaTime, Moves = _moves }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(Stealth), typeof(Stealthed))]
        private partial struct StealthJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<MoveDestination> Moves;

            private void Execute(Entity entity, ref Stealth stealth, EnabledRefRW<Stealth> enabled,
                EnabledRefRW<Stealthed> stealthed)
            {
                stealth.RevealTimer = math.max(0f, stealth.RevealTimer - DeltaTime);
                var hidden = enabled.ValueRO && stealth.RevealTimer <= 0f;
                if (stealth.OnlyWhenStill && IsMoving(entity))
                {
                    hidden = false;
                }

                if (stealthed.ValueRO != hidden)
                {
                    stealthed.ValueRW = hidden;
                }
            }

            private bool IsMoving(Entity entity) => Moves.HasEnabled(entity);
        }
    }
}
