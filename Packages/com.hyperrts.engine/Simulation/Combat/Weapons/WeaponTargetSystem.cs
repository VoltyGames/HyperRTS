using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// Gives each <see cref="WeaponMount"/> its target for the frame with <see cref="WeaponTargetJob"/>, after <see
    /// cref="EngagementSystem"/> and before the weapons turn and fire.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(EngagementSystem))]
    [UpdateBefore(typeof(WeaponPoseSystem))]
    public partial struct WeaponTargetSystem : ISystem
    {
        private TargetLookup _targets;
        private TargetQueries _targetQueries;
        private EntityQuery _projectiles;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _targetQueries = TargetQueries.Create(ref state);
            _projectiles = SystemAPI.QueryBuilder().WithAll<Projectile, LocalTransform>().Build();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<SimulationFrame>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _targetQueries);

            var grid = new ProjectileGrid(_projectiles.CalculateEntityCount(), state.WorldUpdateAllocator);
            new FileJob { Grid = grid }.Schedule();
            new WeaponTargetJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Targets = _targets,
                Scans = new ScanSchedule(SystemAPI.GetSingleton<SimulationFrame>()),
                Projectiles = grid,
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct FileJob : IJobEntity
        {
            public ProjectileGrid Grid;

            private void Execute(Entity entity, in Projectile projectile, in LocalTransform transform) =>
                Grid.Add(entity, projectile, transform.Position);
        }
    }
}
