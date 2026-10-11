using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Scenery
{
    /// <summary>
    /// Client: shows the server's scenery changes on the client's own copy of the map (<see cref="SceneryWriter"/>),
    /// so damage stages, collapses, remains and the nav grid follow. Changes for scenery that hasn't streamed in yet
    /// wait, and then show without their effects.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct SceneryReceiveSystem : ISystem
    {
        private EntityQuery _rpcs;
        private EntityQuery _scenery;
        private SceneryWriter _writer;
        private NativeHashMap<ulong, Entity> _index;
        private NativeHashMap<ulong, byte> _waiting;

        /// <summary>Scenery that should exist now: indexed pieces minus those destroyed. More means some streamed in.</summary>
        private int _indexed;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _rpcs = SystemAPI.QueryBuilder().WithAll<SceneryRpc, ReceiveRpcCommandRequest>().Build();
            _scenery = SystemAPI.QueryBuilder().WithAll<SceneryId>().WithNone<GhostInstance>().Build();
            _writer = new SceneryWriter(ref state);
            _index = new NativeHashMap<ulong, Entity>(1024, Allocator.Persistent);
            _waiting = new NativeHashMap<ulong, byte>(16, Allocator.Persistent);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnDestroy(ref SystemState state)
        {
            _index.Dispose();
            _waiting.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_rpcs.IsEmpty && _waiting.IsEmpty)
            {
                return;
            }

            var loaded = Index(ref state);
            state.CompleteDependency();
            _writer.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            if (loaded)
            {
                ApplyWaiting(ecb);
            }

            foreach (var rpc in SystemAPI.Query<RefRO<SceneryRpc>>().WithAll<ReceiveRpcCommandRequest>())
            {
                var changes = rpc.ValueRO.Changes;
                for (var i = 0; i < changes.Length; i++)
                {
                    Apply(changes[i], rpc.ValueRO.Witnessed, ecb);
                }
            }

            state.EntityManager.DestroyEntity(_rpcs);
        }

        /// <summary>Indexes scenery that streamed in since the last update; true when some did.</summary>
        private bool Index(ref SystemState state)
        {
            var count = _scenery.CalculateEntityCount();
            var grew = count > _indexed;
            _indexed = count;
            if (!grew)
            {
                return false;
            }

            foreach (var (id, entity) in SystemAPI.Query<RefRO<SceneryId>>().WithNone<GhostInstance>().WithEntityAccess())
            {
                _index[id.ValueRO.Value] = entity;
            }

            return true;
        }

        private void ApplyWaiting(EntityCommandBuffer ecb)
        {
            var ready = new NativeList<SceneryChange>(Allocator.Temp);
            foreach (var waiting in _waiting)
            {
                if (_index.ContainsKey(waiting.Key))
                {
                    ready.Add(new SceneryChange { Id = waiting.Key, State = waiting.Value });
                }
            }

            foreach (var change in ready)
            {
                _waiting.Remove(change.Id);
                Apply(change, false, ecb);
            }
        }

        private void Apply(in SceneryChange change, bool witnessed, EntityCommandBuffer ecb)
        {
            if (!_index.TryGetValue(change.Id, out var scenery) || !_writer.Exists(scenery))
            {
                _waiting[change.Id] = change.State;
                return;
            }

            if (change.State != SceneryChange.Destroyed)
            {
                _writer.SetHealth(scenery, change.Fraction, witnessed);
                return;
            }

            _writer.Destroy(scenery, witnessed, ecb);
            _index.Remove(change.Id);
            _indexed--;
        }
    }
}
