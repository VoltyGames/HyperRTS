using HyperRTS.Core;
using HyperRTS.Network.Common;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Commands
{
    /// <summary>
    /// Turns received <see cref="CommandRpc"/>s into <see cref="PlayerCommand"/>s on the sender's player, in arrival
    /// order. Each <see cref="SelectionRpc"/> updates the sender's <see cref="PlayerSelection"/>, keeping only units
    /// it owns, and a group command lists that selection in <see cref="PlayerCommandSubject"/>. Observers' commands,
    /// and every command before <see cref="MatchState.Started"/>, are dropped.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct CommandReceiveSystem : ISystem
    {
        private RpcInbox<CommandRpc> _commands;
        private RpcInbox<SelectionRpc> _selections;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _commands = new RpcInbox<CommandRpc>(ref state);
            _selections = new RpcInbox<SelectionRpc>(ref state);
            state.RequireForUpdate<PrefabRegistry>();
            state.RequireForUpdate<MatchState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_commands.IsEmpty && _selections.IsEmpty)
            {
                return;
            }

            // Selections apply before a match starts too, since the client counts them as sent.
            var started = SystemAPI.GetSingleton<MatchState>().Started;
            var prefabs = SystemAPI.GetSingleton<PrefabRegistry>();
            var selections = _selections.Take(state.EntityManager, out var selectionSenders);
            var arrived = new ArrivedSelections(selections, selectionSenders);
            var commands = _commands.Take(state.EntityManager, out var senders);
            for (var i = 0; i < commands.Length; i++)
            {
                if (!SystemAPI.HasComponent<ConnectionPlayer>(senders[i]))
                {
                    continue;
                }

                var player = SystemAPI.GetComponent<ConnectionPlayer>(senders[i]).Player;
                Select(ref state, ref arrived, senders[i], player, commands[i].Selection);
                if (started)
                {
                    Apply(ref state, player, commands[i], prefabs);
                }
            }

            SelectRest(ref state, ref arrived);
        }

        /// <summary>Applies the sender's selections that came before a command stamped <paramref name="version"/>.</summary>
        private void Select(ref SystemState state, ref ArrivedSelections arrived, Entity sender, Entity player,
            ushort version)
        {
            for (var i = 0; i < arrived.Rpcs.Length; i++)
            {
                if (arrived.IsWaitingFrom(i, sender) && arrived.Rpcs[i].Precedes(version))
                {
                    Select(ref state, player, arrived.Take(i));
                }
            }
        }

        /// <summary>Applies the selections no command this tick came after.</summary>
        private void SelectRest(ref SystemState state, ref ArrivedSelections arrived)
        {
            for (var i = 0; i < arrived.Rpcs.Length; i++)
            {
                var sender = arrived.Senders[i];
                if (arrived.IsWaitingFrom(i, sender) && SystemAPI.HasComponent<ConnectionPlayer>(sender))
                {
                    Select(ref state, SystemAPI.GetComponent<ConnectionPlayer>(sender).Player, arrived.Take(i));
                }
            }
        }

        private void Select(ref SystemState state, Entity player, in SelectionRpc rpc)
        {
            var faction = SystemAPI.GetComponent<Player>(player).Faction;
            var selection = SystemAPI.GetBuffer<PlayerSelection>(player);
            if (!rpc.Append)
            {
                selection.Clear();
            }

            foreach (var unit in rpc.Units)
            {
                if (IsOwned(ref state, unit, faction))
                {
                    selection.Add(new PlayerSelection { Value = unit });
                }
            }
        }

        private void Apply(ref SystemState state, Entity player, in CommandRpc rpc, in PrefabRegistry prefabs)
        {
            var faction = SystemAPI.GetComponent<Player>(player).Faction;
            var command = rpc.ToCommand();
            command.Target = rpc.TargetScenery != 0 ? FindScenery(ref state, rpc.TargetScenery) : rpc.Target;
            command.Prefab = prefabs.Find(rpc.PrefabTypeId);
            if (rpc.Unit == Entity.Null)
            {
                ListSelection(ref state, player, faction, ref command);
            }
            else if (IsOwned(ref state, rpc.Unit, faction))
            {
                command.Unit = rpc.Unit;
            }
            else
            {
                return;
            }

            SystemAPI.GetBuffer<PlayerCommand>(player).Add(command);
        }

        /// <summary>Lists the selection's units still owned as the command's group.</summary>
        private void ListSelection(ref SystemState state, Entity player, byte faction, ref PlayerCommand command)
        {
            var selection = SystemAPI.GetBuffer<PlayerSelection>(player);
            var listed = SystemAPI.GetBuffer<PlayerCommandSubject>(player);
            var start = listed.Length;
            foreach (var unit in selection)
            {
                if (IsOwned(ref state, unit.Value, faction))
                {
                    listed.Add(new PlayerCommandSubject { Value = unit.Value });
                }
            }

            command.SubjectStart = (ushort)start;
            command.SubjectCount = (ushort)(listed.Length - start);
        }

        private bool IsOwned(ref SystemState state, Entity entity, byte faction) =>
            SystemAPI.HasComponent<Faction>(entity) && SystemAPI.GetComponent<Faction>(entity).Value == faction;

        /// <summary>The map scenery with this id; scenery targets are rare enough to look up by scanning.</summary>
        private Entity FindScenery(ref SystemState state, ulong id)
        {
            foreach (var (scenery, entity) in SystemAPI.Query<RefRO<SceneryId>>().WithNone<GhostInstance>()
                         .WithEntityAccess())
            {
                if (scenery.ValueRO.Value == id)
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        /// <summary>This tick's selections with their senders, each applied once.</summary>
        private struct ArrivedSelections
        {
            public NativeArray<SelectionRpc> Rpcs;
            public NativeArray<Entity> Senders;
            private NativeArray<bool> _applied;

            public ArrivedSelections(NativeArray<SelectionRpc> rpcs, NativeArray<Entity> senders)
            {
                Rpcs = rpcs;
                Senders = senders;
                _applied = new NativeArray<bool>(rpcs.Length, Allocator.Temp);
            }

            public readonly bool IsWaitingFrom(int index, Entity sender) => !_applied[index] && Senders[index] == sender;

            public SelectionRpc Take(int index)
            {
                _applied[index] = true;
                return Rpcs[index];
            }
        }
    }
}
