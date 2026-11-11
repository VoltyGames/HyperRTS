using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>
    /// Validates and leashes <see cref="AttackTarget"/>s, chases targets out of range, backs off inside minimum range
    /// and halts once in range. A rooted <see cref="Deployable"/> fights like a building.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(TargetAcquisitionSystem))]
    public partial struct EngagementSystem : ISystem
    {
        /// <summary>Chasers re-target their move only once the target has drifted this far from it.</summary>
        public const float RepathDistance = 1f;

        private TargetLookup _targets;
        private TargetQueries _targetQueries;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _targetQueries = TargetQueries.Create(ref state);
            state.RequireForUpdate<OwnerRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _targetQueries);

            var traits = SystemAPI.GetSingleton<OrderTraitTable>();

            var engager = new Engager
            {
                Targets = _targets,
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                Orders = SystemAPI.GetComponentLookup<ActiveOrder>(true),
                Traits = traits,
                Deployables = SystemAPI.GetComponentLookup<Deployable>(true),
            };
            new EngageJob { Engager = engager }.ScheduleParallel();
            new EngageFixedJob { Engager = engager }.ScheduleParallel();
        }
        [BurstCompile]
        [WithPresent(typeof(AttackTarget), typeof(MoveDestination))]
        private partial struct EngageJob : IJobEntity
        {
            public Engager Engager;

            private void Execute(Entity entity, in LocalTransform transform, in DynamicBuffer<WeaponMount> mounts,
                in VisionRange vision, in Owner owner, ref CombatStance stance, ref AttackTarget attack,
                EnabledRefRW<AttackTarget> attacking, RefRW<MoveDestination> destination,
                EnabledRefRW<MoveDestination> moving)
            {
                var rooted = Deployable.IsImmobile(Engager.Deployables, entity);
                Engager.Engage(entity, transform, mounts, vision, owner, ref stance, ref attack, attacking,
                    new UnitMove(destination, moving, rooted));
            }
        }

        /// <summary>Towers and other units with no <see cref="MoveDestination"/> engage in place.</summary>
        [BurstCompile]
        [WithPresent(typeof(AttackTarget))]
        [WithAbsent(typeof(MoveDestination))]
        private partial struct EngageFixedJob : IJobEntity
        {
            public Engager Engager;

            private void Execute(Entity entity, in LocalTransform transform, in DynamicBuffer<WeaponMount> mounts,
                in VisionRange vision, in Owner owner, ref CombatStance stance, ref AttackTarget attack,
                EnabledRefRW<AttackTarget> attacking) =>
                Engager.Engage(entity, transform, mounts, vision, owner, ref stance, ref attack, attacking, default);
        }
    }
}
