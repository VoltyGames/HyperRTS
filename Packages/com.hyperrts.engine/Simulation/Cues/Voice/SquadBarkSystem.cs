using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Cues.Voice
{
    /// <summary>
    /// Squad barks: when one of the local player's squads takes cover, is suppressed or pinned, retreats, digs in or
    /// gets a member back, it speaks the matching cue. Client-side only (never sent), at most one bark per squad every
    /// <see cref="SquadInterval"/> and one per player every <see cref="PlayerInterval"/>.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct SquadBarkSystem : ISystem
    {
        /// <summary>Seconds between two barks of one squad.</summary>
        public const float SquadInterval = 6f;

        /// <summary>Seconds between two barks of the local player's squads.</summary>
        public const float PlayerInterval = 1.5f;

        private CueWriter _cues;
        private EntityQuery _unseen;
        private double _next;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cues = new CueWriter(ref state);
            _unseen = SystemAPI.QueryBuilder()
                .WithAll<SquadMember, Suppression, Faction, LocalTransform, EntitySound>()
                .WithNone<SquadBarkState>().Build();
            state.RequireForUpdate<LocalPlayer>();
            state.RequireForUpdate<SquadRules>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            state.EntityManager.AddComponent<SquadBarkState>(_unseen);
            _cues.Update(ref state);
            var now = SystemAPI.Time.ElapsedTime;
            var rules = SystemAPI.GetSingleton<SquadRules>();
            var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
            var local = SystemAPI.GetComponent<Player>(player).Faction;
            foreach (var (bark, suppression, retreating, faction, transform, members, squad) in SystemAPI
                         .Query<RefRW<SquadBarkState>, RefRO<Suppression>, EnabledRefRO<Retreating>, RefRO<Faction>,
                             RefRO<LocalTransform>, DynamicBuffer<SquadMember>>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                if (faction.ValueRO.Value != local)
                {
                    continue;
                }

                var slot = bark.ValueRW.Observe(rules, suppression.ValueRO, retreating.ValueRO, members);
                var allowed = now >= _next && now >= bark.ValueRO.QuietUntil;
                if (slot == CueSlot.None || !allowed)
                {
                    continue;
                }

                var place = transform.ValueRO;
                if (_cues.Play(squad, slot, place, place.Forward(), local))
                {
                    bark.ValueRW.QuietUntil = now + SquadInterval;
                    _next = now + PlayerInterval;
                }
            }
        }
    }
}
