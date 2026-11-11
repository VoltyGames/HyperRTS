using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    /// <summary>One AI's think: who it is, where its base is, what it can buy, and the units already given a job this think.</summary>
    internal struct AITurn
    {
        public Entity Player;
        public byte Owner;
        public float3 Home;
        public DynamicBuffer<PlayerCommand> Commands;
        public DynamicBuffer<PlayerCommandSubject> Subjects;
        public NativeList<Entity> Busy;
        public Buyer Buyer;

        public static AITurn Begin(ref SystemState state, in AIQueries queries, Entity player, in AISnapshot snapshot)
        {
            var entities = state.EntityManager;
            var owner = entities.GetComponentData<Player>(player).Owner;
            return new AITurn
            {
                Player = player,
                Owner = owner,
                Home = snapshot.FindHome(owner),
                Commands = entities.GetBuffer<PlayerCommand>(player),
                Subjects = entities.GetBuffer<PlayerCommandSubject>(player),
                Busy = new NativeList<Entity>(Allocator.Temp),
                Buyer = Buyer.Snapshot(entities, queries.Queues, queries.Limited, owner, snapshot.Completed,
                    entities.GetBuffer<CompletedResearch>(player)),
            };
        }
    }
}
