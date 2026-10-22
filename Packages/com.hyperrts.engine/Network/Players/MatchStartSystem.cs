using System;
using HyperRTS.Core;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Players
{
    /// <summary>
    /// Holds a networked match until every human slot has a connection, so nobody (AI included) plays while others
    /// still load; after <see cref="MatchRules.JoinTimeout"/> it starts anyway and late players join a running match.
    /// Sets <see cref="MatchState.Started"/>, which loading screens wait for and command receiving requires.
    /// Not Burst: it switches managed system groups.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(PlayerGhostSystem))]
    public partial struct MatchStartSystem : ISystem
    {
        private EntityQuery _players;
        private EntityQuery _humans;
        private double _waitingSince;
        private bool _holding;
        private ulong _resume;

        public void OnCreate(ref SystemState state)
        {
            _players = SystemAPI.QueryBuilder().WithAll<Player, PlayerConnection>().Build();
            _humans = SystemAPI.QueryBuilder().WithAll<Player, PlayerConnection>().WithNone<AIPlayer>().Build();
            state.RequireForUpdate<MatchState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            ref var match = ref SystemAPI.GetSingletonRW<MatchState>().ValueRW;
            if (match.Started)
            {
                return;
            }

            var now = SystemAPI.Time.ElapsedTime;
            if (!_holding)
            {
                _holding = true;
                _waitingSince = now;
                _resume = GameplayPhaseGroup.PauseAllButOrders(state.World);
            }

            var humans = _humans.ToComponentDataArray<PlayerConnection>(Allocator.Temp);
            if (!AllJoined(_players.CalculateEntityCount(), humans) && now - _waitingSince < JoinTimeout(ref state))
            {
                return;
            }

            match.Started = true;
            _holding = false;
            GameplayPhaseGroup.Restore(state.World, _resume);
        }

        private double JoinTimeout(ref SystemState state)
        {
            var timeout = SystemAPI.GetSingleton<MatchRules>().JoinTimeout;
            if (timeout <= 0f)
            {
                throw new InvalidOperationException($"MatchRules.JoinTimeout must be positive, got {timeout}.");
            }

            return timeout;
        }

        /// <summary>Every spawned human has a connection; a match of only AI has once its players spawn.</summary>
        public static bool AllJoined(int players, NativeArray<PlayerConnection> humans)
        {
            // Player ghosts spawn a frame after the map loads, so no players is "not yet", not "nobody".
            if (players == 0)
            {
                return false;
            }

            foreach (var connection in humans)
            {
                if (connection.NetworkId == 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
