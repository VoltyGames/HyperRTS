using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    /// <summary>One AI's think: who it is, where its base is, what it can buy, and the units already given a job this think.</summary>
    internal struct AITurn
    {
        public Entity Player;

        /// <summary>The player's settings as they stood when the think began.</summary>
        public AIPlayer Settings;
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
                Settings = entities.GetComponentData<AIPlayer>(player),
                Home = snapshot.FindHome(owner),
                Commands = entities.GetBuffer<PlayerCommand>(player),
                Subjects = entities.GetBuffer<PlayerCommandSubject>(player),
                Busy = new NativeList<Entity>(Allocator.Temp),
                Buyer = Buyer.Snapshot(entities, queries.Queues, queries.Limited, owner, snapshot.Completed,
                    entities.GetBuffer<CompletedResearch>(player)),
            };
        }

        public readonly bool CanBuy(EntityManager entities, Entity prefab) =>
            Buyer.CanBuy(entities, prefab, new Stockpile(entities.GetBuffer<ResourceStock>(Player)));

        /// <summary>Whether the unit at <paramref name="i"/> is the owner's and not yet given a job this think.</summary>
        public readonly bool IsFree(in AIGroup group, int i) =>
            group.IsOwnedBy(i, Owner) && !Busy.Contains(group.Entities[i]);
    }
}
