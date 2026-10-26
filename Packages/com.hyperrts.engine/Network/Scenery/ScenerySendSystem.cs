using HyperRTS.Core;
using HyperRTS.Network.Common;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;
using Unity.Transforms;

namespace HyperRTS.Network.Scenery
{
    /// <summary>Server: replicates map scenery by <see cref="SceneryId"/> to teams that see it. Runs last in the lifecycle phase, while the dead still exist.</summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(FactionConnectionsSystem))]
    public partial struct ScenerySendSystem : ISystem
    {
        private EntityQuery _scenery;
        private SceneryTracker _tracker;

        /// <summary>Tracked pieces minus destroyed ones. A higher count means more scenery streamed in.</summary>
        private int _expected;

        private uint _frame;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _scenery = SystemAPI.QueryBuilder().WithAll<SceneryId, Health, LocalTransform>().WithNone<GhostInstance>()
                .Build();
            _tracker = new SceneryTracker(Allocator.Persistent);
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<FactionConnections>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnDestroy(ref SystemState state) => _tracker.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _frame++;
            Register(ref state);
            if (_tracker.Count == 0)
            {
                return;
            }

            Track(ref state, Teams(ref state));
            SystemAPI.TryGetSingleton(out FogOfWar fog);
            var sightings = new NativeList<ScenerySighting>(Allocator.Temp);
            _tracker.Reveal(fog, SystemAPI.GetSingleton<MapSettings>().FogOfWar, _frame, sightings);
            Send(ref state, sightings);
        }

        /// <summary>Scenery only leaves by dying or being cleared, which <see cref="Track"/> counts.</summary>
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

                    _tracker.Register(id.ValueRO.Value, position);
                }
            }

            _expected = count;
        }

        private static void ReportShared(float3 position) => UnityEngine.Debug.LogError(
            $"Two pieces of map scenery share the spot ({position.x}, {position.z}): clients can't tell them apart.");

        private void Track(ref SystemState state, ushort teams)
        {
            var fallen = SystemAPI.GetComponentLookup<ToppleHeading>(true);
            foreach (var (health, id, entity) in SystemAPI.Query<RefRO<Health>, RefRO<SceneryId>>()
                         .WithAll<LocalTransform>().WithNone<GhostInstance>().WithChangeFilter<Health>().WithEntityAccess())
            {
                var now = SceneryChange.Encode(health.ValueRO);

                // Scenery that doesn't topple sends heading 0, which clients ignore.
                fallen.TryGetComponent(entity, out var fell);
                if (_tracker.Set(id.ValueRO.Value, now, fell.Heading, teams, _frame) && now == SceneryChange.Destroyed)
                {
                    _expected--;
                }
            }

            // Cleared remains have no health, so only standing pieces leave the count.
            var standing = SystemAPI.GetComponentLookup<Health>(true);
            foreach (var (id, entity) in SystemAPI.Query<RefRO<SceneryId>>().WithAll<SceneryCleared, LocalTransform>()
                         .WithNone<GhostInstance>().WithEntityAccess())
            {
                if (_tracker.Clear(id.ValueRO.Value, teams, _frame) && standing.HasComponent(entity))
                {
                    _expected--;
                }
            }
        }

        /// <summary>Match teams as bits, with team 0 for observers.</summary>
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

        private void Send(ref SystemState state, NativeList<ScenerySighting> sightings)
        {
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            var viewers = SystemAPI.GetSingleton<FactionConnections>();
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var changes = new NativeList<SceneryChange>(Allocator.Temp);
            var seen = new NativeList<SceneryRpc>(Allocator.Temp);
            var known = new NativeList<SceneryRpc>(Allocator.Temp);
            foreach (var faction in viewers.Factions)
            {
                var team = relations.GetTeam(faction);
                seen.Clear();
                FillSightings(sightings, team, true, changes, seen);
                FillSightings(sightings, team, false, changes, seen);
                foreach (var connection in viewers.Connections.GetValuesForKey(faction))
                {
                    if (SystemAPI.HasComponent<ScenerySynced>(connection))
                    {
                        FactionConnections.Send(ecb, connection, seen);
                        continue;
                    }

                    // A joining client starts from what its team knows, this frame's sightings included.
                    changes.Clear();
                    known.Clear();
                    _tracker.Known(team, changes);
                    SceneryRpc.Fill(changes, false, known);
                    FactionConnections.Send(ecb, connection, known);
                    ecb.AddComponent<ScenerySynced>(connection);
                }
            }
        }

        private static void FillSightings(NativeList<ScenerySighting> sightings, byte team, bool witnessed,
            NativeList<SceneryChange> changes, NativeList<SceneryRpc> rpcs)
        {
            changes.Clear();
            foreach (var item in sightings)
            {
                if (item.Team == team && item.Witnessed == witnessed)
                {
                    changes.Add(item.Change);
                }
            }

            SceneryRpc.Fill(changes, witnessed, rpcs);
        }
    }
}
