using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>
    /// Defeats players who lost every <see cref="VictoryCritical"/> entity they once had, and ends the match when the
    /// survivors are all on one team. Players never defeat at startup, while SubScene entities are still streaming in.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(DeathSystem))]
    public partial struct VictorySystem : ISystem
    {
        private EntityQuery _critical;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _critical = SystemAPI.QueryBuilder().WithAll<VictoryCritical, Faction>().WithNone<Dead>().Build();
            state.RequireForUpdate<MatchState>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var match = ref SystemAPI.GetSingletonRW<MatchState>().ValueRW;
            if (match.Phase == MatchPhase.Ended)
            {
                return;
            }

            var alive = 0u;
            foreach (var owner in _critical.ToComponentDataArray<Faction>(Allocator.Temp))
            {
                alive |= Bit(owner.Value);
            }

            match.Contenders |= alive;
            foreach (var (player, entity) in SystemAPI.Query<RefRO<Player>>().WithNone<Defeated>().WithEntityAccess())
            {
                var bit = Bit(player.ValueRO.Faction);
                if ((match.Contenders & bit) != 0 && (alive & bit) == 0)
                {
                    SystemAPI.SetComponentEnabled<Defeated>(entity, true);
                }
            }

            Resolve(ref state, ref match);
        }

        /// <summary>Ends the match once someone has lost and no two survivors are hostile; no survivors is a draw.</summary>
        private void Resolve(ref SystemState state, ref MatchState match)
        {
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            var anyDefeated = false;
            byte winner = 0;
            foreach (var (player, entity) in SystemAPI.Query<RefRO<Player>>().WithPresent<Defeated>().WithEntityAccess())
            {
                if (SystemAPI.IsComponentEnabled<Defeated>(entity))
                {
                    anyDefeated = true;
                }
                else if (winner == 0)
                {
                    winner = relations.TeamOf(player.ValueRO.Faction);
                }
                else if (winner != relations.TeamOf(player.ValueRO.Faction))
                {
                    return;
                }
            }

            if (anyDefeated)
            {
                match.Phase = MatchPhase.Ended;
                match.WinningTeam = winner;
            }
        }

        private static uint Bit(byte faction) => faction < 32 ? 1u << faction : 0u;
    }
}
