using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>
    /// Resolves the frame's <see cref="DamageEvent"/>s in order: heals skip every multiplier, and squad hits land on
    /// members by seed, splash and cover.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(SectionCollapseSystem))]
    public partial struct DamageSystem : ISystem
    {
        private DamageFactors _factors;
        private EventWriter<EntityDamaged> _damaged;
        private SectionDamage _buildings;
        private SquadDamage _squads;

        public void OnCreate(ref SystemState state)
        {
            _factors = new DamageFactors(ref state);
            _damaged = new EventWriter<EntityDamaged>(ref state);
            _buildings = new SectionDamage(ref state);
            _squads = new SquadDamage(ref state);

            EventQueues.Create<DamageQueue>(ref state, typeof(DamageEvent));
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<SimulationFrame>();
            state.RequireForUpdate<CoverRules>();
            state.RequireForUpdate<SquadRules>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _factors.Update(ref state);
            _damaged.Update(ref state);
            _buildings.Update(ref state);
            _squads.Update(ref state, SystemAPI.GetSingleton<CoverRules>());

            new DamageResolveJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                Health = SystemAPI.GetComponentLookup<Health>(false),
                Attackers = SystemAPI.GetComponentLookup<LastAttacker>(false),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Factors = _factors,
                Destructibles = SystemAPI.GetComponentLookup<Destructible>(true),
                LethalHits = SystemAPI.GetComponentLookup<LethalHit>(false),
                Owners = SystemAPI.GetComponentLookup<Owner>(true),
                Damaged = _damaged,
                Shelters = SystemAPI.GetComponentLookup<Shelter>(true),
                ShelterHits = SystemAPI.GetBufferLookup<ShelterHit>(false),
                Buildings = _buildings,
                Squads = _squads,
                Frame = SystemAPI.GetSingleton<SimulationFrame>().Value,
                Squad = SystemAPI.GetSingleton<SquadRules>(),
            }.Schedule();
        }
    }
}
