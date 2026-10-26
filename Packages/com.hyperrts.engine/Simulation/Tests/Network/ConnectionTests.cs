using HyperRTS.Core;
using HyperRTS.Network.Common;
using HyperRTS.Network.Cues;
using HyperRTS.Network.Players;
using HyperRTS.Network.Replication;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Tests.Network
{
    /// <summary>
    /// Joining and staying connected: which slot a join gets, when the match stops waiting, per-faction RPCs, prefab
    /// references from type ids and prediction only while in game.
    /// </summary>
    public class ConnectionTests
    {
        private const ulong Mine = 0xA1;
        private const ulong Stranger = 0xB2;

        [Test]
        public void PrefabReferences_ResolveFromTypeIds()
        {
            using var world = new TestWorld();
            world.CreateMatch(1);
            var prefab = world.MakePrefab(world.SpawnUnit(0, new float3(50f, 0f, 50f), name: "Scout"));
            world.Tick();
            var holder = world.EntityManager.CreateEntity();
            world.EntityManager.AddBuffer<ProductionQueueItem>(holder)
                .Add(new ProductionQueueItem { TypeId = EntityInfo.TypeIdFromName("Scout") });

            var registry = world.EntityManager.CreateEntityQuery(typeof(PrefabRegistry)).GetSingleton<PrefabRegistry>();
            var holders = world.EntityManager.CreateEntityQuery(typeof(ProductionQueueItem));
            PrefabReferences.Resolve<ProductionQueueItem>(world.EntityManager, holders, registry);

            Assert.AreEqual(prefab, world.EntityManager.GetBuffer<ProductionQueueItem>(holder)[0].Prefab);
        }

        [Test]
        public void Prediction_RunsOnlyWhileInGame()
        {
            using var world = new World("In-Game Prediction Test", WorldFlags.GameClient);
            var prediction = world.CreateSystemManaged<PredictedSimulationSystemGroup>();
            var inGamePrediction = world.CreateSystem<InGamePredictionSystem>();

            inGamePrediction.Update(world.Unmanaged);
            Assert.IsFalse(prediction.Enabled, "No connection in game yet.");

            var connection = world.EntityManager.CreateEntity(typeof(NetworkStreamInGame));
            inGamePrediction.Update(world.Unmanaged);
            Assert.IsTrue(prediction.Enabled, "In game.");

            world.EntityManager.DestroyEntity(connection);
            inGamePrediction.Update(world.Unmanaged);
            Assert.IsFalse(prediction.Enabled, "The connection dropped.");
        }

        [Test]
        public void Factions_AreListedOnce_AndRpcsReachEveryConnectionOfTheFaction()
        {
            using var world = new TestWorld();
            var entities = world.EntityManager;
            var allies = new[] { entities.CreateEntity(), entities.CreateEntity() };
            var observer = entities.CreateEntity();
            var viewers = FactionConnections.Create(Allocator.Temp);
            viewers.Add(1, allies[0]);
            viewers.Add(0, observer);
            viewers.Add(1, allies[1]);
            using var rpcs = new NativeList<CueRpc>(Allocator.Temp) { new CueRpc(), new CueRpc() };
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            viewers.Send(ecb, 1, rpcs);
            ecb.Playback(entities);
            ecb.Dispose();

            CollectionAssert.AreEqual(new byte[] { 1, 0 }, viewers.Factions.AsArray().ToArray());
            using var query = entities.CreateEntityQuery(typeof(CueRpc), typeof(SendRpcCommandRequest));
            var requests = query.ToComponentDataArray<SendRpcCommandRequest>(Allocator.Temp);
            Assert.AreEqual(4, requests.Length, "two RPCs to each of the two allies");
            foreach (var request in requests)
            {
                Assert.AreNotEqual(observer, request.TargetConnection, "the observer views as another faction");
            }

            viewers.Dispose();
        }

        [TestCase(0, new int[0], false, TestName = "NoPlayersYet_IsNotJoined")]
        [TestCase(2, new int[0], true, TestName = "AllAIMatch_IsJoinedOncePlayersSpawn")]
        [TestCase(3, new[] { 4, 0 }, false, TestName = "HumanWithoutConnection_IsNotJoined")]
        [TestCase(3, new[] { 4, 5 }, true, TestName = "EveryHumanConnected_IsJoined")]
        public void AllJoined(int players, int[] humanNetworkIds, bool joined) =>
            Assert.AreEqual(joined, MatchStartSystem.AllJoined(players, Humans(humanNetworkIds)));

        [Test]
        public void JoinHold_PausesEveryPhaseButOrders_AndEveryAI()
        {
            using var world = new TestWorld();
            var orders = world.World.GetExistingSystemManaged<OrderSystemGroup>();
            var ai = world.World.GetExistingSystemManaged<AISystemGroup>();
            var combat = world.World.GetExistingSystemManaged<CombatSystemGroup>();
            CollectionAssert.Contains(ai.GetAllSystems().AsArray().ToArray(),
                world.World.Unmanaged.GetExistingUnmanagedSystem<SkirmishAISystem>(), "the skirmish AI runs in the AI group");

            var resume = GameplayPhaseGroup.PauseAllButOrders(world.World);
            Assert.IsTrue(orders.Enabled, "players still spawn and join");
            Assert.IsFalse(ai.Enabled, "no AI plays");
            Assert.IsFalse(combat.Enabled);

            GameplayPhaseGroup.Restore(world.World, resume);
            Assert.IsTrue(ai.Enabled);
            Assert.IsTrue(combat.Enabled);
        }

        private static NativeArray<PlayerConnection> Humans(params int[] networkIds)
        {
            var humans = new NativeArray<PlayerConnection>(networkIds.Length, Allocator.Temp);
            for (var i = 0; i < networkIds.Length; i++)
            {
                humans[i] = new PlayerConnection { NetworkId = networkIds[i] };
            }

            return humans;
        }

        [Test]
        public void DroppedOwner_GetsItsSlotBack()
        {
            var slots = new[] { Held(1, 3, 0xC3), Dropped(2, Mine) };
            Assert.AreEqual(1, SlotClaim.Choose(slots, 1, Mine));
        }

        [Test]
        public void Stranger_CannotTakeADroppedSlot()
        {
            var slots = new[] { Held(1, 3, 0xC3), Dropped(2, Mine) };
            Assert.AreEqual(-1, SlotClaim.Choose(slots, 2, Stranger));
        }

        [Test]
        public void Owner_ReplacesAStaleConnection()
        {
            var slots = new[] { Held(1, 3, 0xC3), Held(2, 4, Mine) };
            var index = SlotClaim.Choose(slots, 0, Mine);
            Assert.AreEqual(1, index);
            Assert.AreNotEqual(0, slots[index].NetworkId, "the stale connection still holds it, so it is disconnected");
        }

        [Test]
        public void NewJoin_TakesTheWantedNeverClaimedSlot_ThenTheFirst()
        {
            var slots = new[] { Dropped(1, 0xC3), Open(2), Open(3) };
            Assert.AreEqual(2, SlotClaim.Choose(slots, 3, Stranger));
            Assert.AreEqual(1, SlotClaim.Choose(slots, 1, Stranger));
        }

        [Test]
        public void AnonymousSlot_IsFreeAgainOnceDropped()
        {
            var slots = new[] { Dropped(1, 0) };
            Assert.AreEqual(0, SlotClaim.Choose(slots, 0, Stranger));
        }

        [TestCase((byte)0, true, TestName = "ObserverRejoinsAsObserver")]
        [TestCase((byte)2, false, TestName = "PlayerRejoinsForItsSlot")]
        public void Rejoining_KeepsTheGrantedSeat(byte granted, bool observe)
        {
            var rejoin = JoinPreference.Rejoining(new LocalFaction { Value = granted });
            Assert.AreEqual(granted, rejoin.Faction);
            Assert.AreEqual(observe, rejoin.Observe);
        }

        private static SlotClaim.Slot Open(byte faction) => new() { Faction = faction };

        private static SlotClaim.Slot Dropped(byte faction, ulong token) => new() { Faction = faction, Token = token };

        private static SlotClaim.Slot Held(byte faction, int networkId, ulong token) =>
            new() { Faction = faction, NetworkId = networkId, Token = token };
    }
}
