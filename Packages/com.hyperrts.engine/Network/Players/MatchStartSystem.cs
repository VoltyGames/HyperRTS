using System;
using HyperRTS.Core;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

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
        private EntityQuery _humans;
        private double _waitingSince;
        private bool _holding;

        public void OnCreate(ref SystemState state)
        {
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
                SetGameplay(ref state, false);
            }

            if (!AllJoined() && now - _waitingSince < JoinTimeout(ref state))
            {
                return;
            }

            match.Started = true;
            _holding = false;
            SetGameplay(ref state, true);
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

        // Player ghosts spawn a frame after the map loads, so an empty query is "not yet", not "nobody".
        private bool AllJoined()
        {
            if (_humans.IsEmpty)
            {
                return false;
            }

            foreach (var connection in _humans.ToComponentDataArray<PlayerConnection>(Allocator.Temp))
            {
                if (connection.NetworkId == 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The order phase keeps running (it spawns player ghosts and binds joins); the rest and the AI wait.</summary>
        private static void SetGameplay(ref SystemState state, bool enabled)
        {
            var world = state.World;
            world.GetExistingSystemManaged<MovementSystemGroup>().Enabled = enabled;
            world.GetExistingSystemManaged<CombatSystemGroup>().Enabled = enabled;
            world.GetExistingSystemManaged<ProductionSystemGroup>().Enabled = enabled;
            world.GetExistingSystemManaged<LifecycleSystemGroup>().Enabled = enabled;
            var ai = world.Unmanaged.GetExistingUnmanagedSystem<SkirmishAISystem>();
            if (ai != SystemHandle.Null)
            {
                world.Unmanaged.ResolveSystemStateRef(ai).Enabled = enabled;
            }
        }
    }
}
