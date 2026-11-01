using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>Sends, halts and checks units' <see cref="MoveDestination"/>s from a combat job.</summary>
    public struct MoveLookup
    {
        // Each entity writes only its own MoveDestination.
        [NativeDisableParallelForRestriction] private ComponentLookup<MoveDestination> _moves;
        [ReadOnly] private ComponentLookup<Deployable> _deployables;

        public MoveLookup(ref SystemState state)
        {
            _moves = state.GetComponentLookup<MoveDestination>();
            _deployables = state.GetComponentLookup<Deployable>(true);
        }

        public void Update(ref SystemState state)
        {
            _moves.Update(ref state);
            _deployables.Update(ref state);
        }

        public bool IsMoving(Entity entity) => _moves.HasEnabled(entity);

        /// <summary>Has a destination to move to and isn't rooted by deploying.</summary>
        public bool IsMobile(Entity entity) =>
            _moves.HasComponent(entity) && !Deployable.IsImmobile(_deployables, entity);

        public void MoveTo(Entity entity, float3 goal) =>
            _moves.GetRefRW(entity).ValueRW.MoveTo(_moves.GetEnabledRefRW<MoveDestination>(entity), goal);

        /// <summary>Moves on to <paramref name="goal"/> unless already heading within <paramref name="tolerance"/> of it.</summary>
        public void MoveTo(Entity entity, float3 goal, float tolerance) =>
            _moves.GetRefRW(entity).ValueRW.MoveTo(_moves.GetEnabledRefRW<MoveDestination>(entity), goal, tolerance);

        public void Halt(Entity entity) => _moves.DisableIfPresent(entity);
    }
}
