using HyperRTS.Network.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Commands
{
    /// <summary>
    /// Sends the local player's commands to the server, which runs them; clients never execute them. A command for
    /// a group sends the group as a <see cref="SelectionRpc"/> first, only when it differs from the last one sent.
    /// </summary>
    [BurstCompile]
    [RemoteClientOnly]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderLast = true)]
    public partial struct CommandSendSystem : ISystem
    {
        private EntityQuery _selected;

        /// <summary>The selection the server holds, sorted.</summary>
        private NativeList<Entity> _sent;

        // Netcode serializes each RPC type in its own system, so a command waits a tick behind its new selection.
        private NativeList<CommandRpc> _held;

        private ushort _version;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _selected = SystemAPI.QueryBuilder().WithAll<Selected, Faction, GhostInstance>().Build();
            _sent = new NativeList<Entity>(64, Allocator.Persistent);
            _held = new NativeList<CommandRpc>(4, Allocator.Persistent);
            state.RequireForUpdate<LocalPlayer>();
        }

        public void OnDestroy(ref SystemState state)
        {
            _sent.Dispose();
            _held.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            Rpc.Send(ecb, Entity.Null, _held);
            _held.Clear();

            var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
            var commands = SystemAPI.GetBuffer<PlayerCommand>(player);
            if (commands.IsEmpty)
            {
                return;
            }

            var listed = SystemAPI.GetBuffer<PlayerCommandSubject>(player);
            var faction = SystemAPI.GetComponent<Player>(player).Faction;
            var holding = false;
            foreach (var command in commands)
            {
                var rpc = ToRpc(ref state, command);
                if (rpc.Unit == Entity.Null)
                {
                    holding |= Select(ref state, ecb, command, listed, faction);
                }

                rpc.Selection = _version;
                if (holding)
                {
                    _held.Add(rpc);
                }
                else
                {
                    Rpc.Send(ecb, Entity.Null, rpc);
                }
            }

            commands.Clear();
            listed.Clear();
        }

        private CommandRpc ToRpc(ref SystemState state, in PlayerCommand command)
        {
            var rpc = CommandRpc.From(command);
            rpc.Target = command.Target;
            rpc.TargetScenery = SceneryTarget(ref state, command.Target);
            rpc.PrefabTypeId = EntityInfo.GetTypeId(state.EntityManager, command.Prefab);

            // A listed group overrides the unit, so it travels as a selection.
            rpc.Unit = command.SubjectCount > 0 ? Entity.Null : command.Unit;
            return rpc;
        }

        /// <summary>Sends this player's units among the command's group if they changed; true when it sent them.</summary>
        private bool Select(ref SystemState state, EntityCommandBuffer ecb, in PlayerCommand command,
            DynamicBuffer<PlayerCommandSubject> listed, byte faction)
        {
            var units = new NativeList<Entity>(Allocator.Temp);
            foreach (var subject in PlayerCommands.Collect(command, listed, _selected))
            {
                if (IsOwned(ref state, subject, faction))
                {
                    units.Add(subject);
                }
            }

            units.Sort();
            if (units.AsArray().ArraysEqual(_sent.AsArray()))
            {
                return false;
            }

            _version++;
            var rpcs = new NativeList<SelectionRpc>(Allocator.Temp);
            SelectionRpc.Fill(units.AsArray(), _version, rpcs);
            Rpc.Send(ecb, Entity.Null, rpcs);
            _sent.CopyFrom(units);
            return true;
        }

        private bool IsOwned(ref SystemState state, Entity entity, byte faction) =>
            SystemAPI.HasComponent<Faction>(entity) && SystemAPI.GetComponent<Faction>(entity).Value == faction;

        /// <summary>The target's <see cref="SceneryId"/> when it is map scenery rather than a ghost, else 0.</summary>
        private ulong SceneryTarget(ref SystemState state, Entity target)
        {
            if (!SystemAPI.HasComponent<SceneryId>(target) || SystemAPI.HasComponent<GhostInstance>(target))
            {
                return 0;
            }

            return SystemAPI.GetComponent<SceneryId>(target).Value;
        }
    }
}
