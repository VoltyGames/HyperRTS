using HyperRTS.Core;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace HyperRTS.Network.Scenery
{
    /// <summary>
    /// Server: replicates map scenery, which isn't made of ghosts, by <see cref="SceneryId"/>. Each team's clients
    /// hear of a change once the team sees the spot (<see cref="SceneryLedger"/>), and a joining client first gets
    /// what its team knows. Runs last in the lifecycle phase: health is final and the dead are still there.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct ScenerySendSystem : ISystem
    {
        private EntityQuery _scenery;
        private SceneryLedger _ledger;

        /// <summary>Scenery that should exist now: tracked pieces minus those destroyed. More means some streamed in.</summary>
        private int _expected;

        private uint _frame;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _scenery = SystemAPI.QueryBuilder().WithAll<SceneryId, Health, LocalTransform>().WithNone<GhostInstance>()
                .Build();
            _ledger = new SceneryLedger(Allocator.Persistent);
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnDestroy(ref SystemState state) => _ledger.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _frame++;
            Register(ref state);
            if (_ledger.Count == 0)
            {
                return;
            }

            Track(ref state, Teams(ref state));
            SystemAPI.TryGetSingleton(out FogOfWar fog);
            var news = new NativeList<SceneryNews>(Allocator.Temp);
            _ledger.Reveal(fog, SystemAPI.GetSingleton<MapSettings>().FogOfWar, _frame, news);
            Send(ref state, news);
        }

        /// <summary>Tracks scenery that streamed in; scenery only leaves by dying, which <see cref="Track"/> counts.</summary>
        private void Register(ref SystemState state)
        {
            var count = _scenery.CalculateEntityCount();
            if (count > _expected)
            {
                var placed = new NativeHashSet<ulong>(count, Allocator.Temp);
                foreach (var (id, transform) in SystemAPI.Query<RefRO<SceneryId>, RefRO<LocalTransform>>()
                             .WithAll<Health>().WithNone<GhostInstance>())
                {
                    var position = transform.ValueRO.Position;
                    if (!placed.Add(id.ValueRO.Value))
                    {
                        ReportShared(position);
                    }

                    _ledger.Register(id.ValueRO.Value, position);
                }
            }

            _expected = count;
        }

        private static void ReportShared(float3 position) => UnityEngine.Debug.LogError(
            $"Two pieces of map scenery share the spot ({position.x}, {position.z}): clients can't tell them apart.");

        private void Track(ref SystemState state, ushort teams)
        {
            foreach (var (health, id) in SystemAPI.Query<RefRO<Health>, RefRO<SceneryId>>()
                         .WithAll<LocalTransform>().WithNone<GhostInstance>().WithChangeFilter<Health>())
            {
                var now = SceneryChange.Encode(health.ValueRO);
                if (_ledger.Set(id.ValueRO.Value, now, teams, _frame) && now == SceneryChange.Destroyed)
                {
                    _expected--;
                }
            }
        }

        /// <summary>The teams in the match (a bit each), with team 0, which observers view as.</summary>
        private ushort Teams(ref SystemState state)
        {
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            var teams = 1;
            foreach (var player in SystemAPI.Query<RefRO<Player>>())
            {
                teams |= 1 << relations.GetTeam(player.ValueRO.Faction);
            }

            return (ushort)teams;
        }

        private void Send(ref SystemState state, NativeList<SceneryNews> news)
        {
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var changes = new NativeList<SceneryChange>(Allocator.Temp);
            foreach (var (_, connection) in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>()
                         .WithEntityAccess())
            {
                var team = relations.GetTeam(Viewer(ref state, connection));
                if (SystemAPI.HasComponent<ScenerySynced>(connection))
                {
                    PostNews(ecb, connection, news, team, true, changes);
                    PostNews(ecb, connection, news, team, false, changes);
                    continue;
                }

                // A joining client starts from what its team knows, this frame's news included.
                changes.Clear();
                _ledger.Known(team, changes);
                Post(ecb, connection, false, changes);
                ecb.AddComponent<ScenerySynced>(connection);
            }
        }

        /// <summary>The connection's faction; observers (no slot) view as neutral, whose team 0 sees everything.</summary>
        private byte Viewer(ref SystemState state, Entity connection)
        {
            if (!SystemAPI.HasComponent<ConnectionPlayer>(connection))
            {
                return Faction.Neutral;
            }

            return SystemAPI.GetComponent<Player>(SystemAPI.GetComponent<ConnectionPlayer>(connection).Player).Faction;
        }

        private static void PostNews(EntityCommandBuffer ecb, Entity connection, NativeList<SceneryNews> news,
            byte team, bool witnessed, NativeList<SceneryChange> changes)
        {
            changes.Clear();
            foreach (var item in news)
            {
                if (item.Team == team && item.Witnessed == witnessed)
                {
                    changes.Add(item.Change);
                }
            }

            Post(ecb, connection, witnessed, changes);
        }

        /// <summary>Sends <paramref name="changes"/> to one connection in as many messages as they need.</summary>
        private static void Post(EntityCommandBuffer ecb, Entity connection, bool witnessed,
            NativeList<SceneryChange> changes)
        {
            for (var start = 0; start < changes.Length; start += SceneryRpc.Capacity)
            {
                var rpc = new SceneryRpc { Witnessed = witnessed };
                var end = math.min(start + SceneryRpc.Capacity, changes.Length);
                for (var i = start; i < end; i++)
                {
                    rpc.Changes.Add(changes[i]);
                }

                var message = ecb.CreateEntity();
                ecb.AddComponent(message, rpc);
                ecb.AddComponent(message, new SendRpcCommandRequest { TargetConnection = connection });
            }
        }
    }
}
