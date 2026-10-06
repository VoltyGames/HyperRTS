using HyperRTS.Core;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Upgrades;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Skirmish AI for <see cref="AIPlayer"/>s: follows its <see cref="AIBuildStep"/> build order, then keeps producers
    /// training; keeps harvesters gathering, fires ready abilities and sends idle armies at the nearest enemy base.
    /// It only issues <see cref="PlayerCommand"/>s, exactly like a human player. Split by concern over partial files.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct SkirmishAISystem : ISystem
    {
        /// <summary>Query results shared by every AI that thinks this frame.</summary>
        private struct Snapshot
        {
            public AIGroup Harvesters;
            public AIGroup Nodes;
            public NativeArray<ResourceNode> NodeData;
            public AIGroup Army;
            public AIGroup Targets;
            public AIGroup Builders;
            public AIGroup Buildings;
            public AIGroup Sites;
            public AIGroup Casters;
            public AIGroup Owned;
            public NativeArray<EntityInfo> OwnedInfo;
            public NativeArray<Entity> Producers;
            public CompletedBuildings Completed;
            public FactionRelations Relations;
        }

        /// <summary>One AI's think: who it is, where its base is, and the units already given a job this think.</summary>
        private struct Turn
        {
            public Entity Player;
            public byte Faction;
            public float3 Home;
            public DynamicBuffer<PlayerCommand> Commands;
            public NativeList<Entity> Busy;
        }

        private EntityQuery _idleHarvesters;
        private EntityQuery _idleArmy;
        private EntityQuery _nodes;
        private EntityQuery _producers;
        private EntityQuery _targets;
        private EntityQuery _builders;
        private EntityQuery _buildings;
        private EntityQuery _sites;
        private EntityQuery _casters;
        private EntityQuery _owned;
        private EntityQuery _completed;
        private EntityQuery _queues;
        private TargetLookup _targetLookup;
        private ComponentLookup<Faction> _factions;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _idleHarvesters = SystemAPI.QueryBuilder().WithAll<Harvester, Faction, LocalTransform>()
                .WithDisabled<ActiveOrder>().WithNone<Dead>().Build();
            _idleArmy = SystemAPI.QueryBuilder().WithAll<Weapon, Faction, LocalTransform>()
                .WithDisabled<ActiveOrder>().WithNone<Harvester, Builder, Dead, AttackTarget>().Build();
            _nodes = SystemAPI.QueryBuilder().WithAll<ResourceNode, Faction, LocalTransform>().Build();
            _producers = SystemAPI.QueryBuilder().WithAll<Producer, ProductionOption, ProductionQueueItem, Faction>()
                .WithNone<ConstructionProgress, Dead>().Build();
            _targets = SystemAPI.QueryBuilder().WithAll<Health, Faction, LocalTransform>().WithNone<Dead>().Build();
            _builders = SystemAPI.QueryBuilder().WithAll<Builder, BuildOption, Faction, LocalTransform>()
                .WithPresent<ActiveOrder>().WithNone<Dead>().Build();
            _buildings = SystemAPI.QueryBuilder().WithAll<BuildingTag, Faction, LocalTransform>().WithNone<Dead>().Build();
            _sites = SystemAPI.QueryBuilder().WithAll<BuildingTag, ConstructionProgress, Faction, LocalTransform>()
                .WithNone<Dead>().Build();
            _casters = SystemAPI.QueryBuilder().WithAll<Ability, Faction, LocalTransform>().WithNone<Dead>().Build();
            _owned = SystemAPI.QueryBuilder().WithAll<EntityInfo, Faction, LocalTransform>().WithNone<Dead>().Build();
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _queues = UpgradeRules.QueueQuery(Allocator.Temp).Build(ref state);
            _targetLookup = new TargetLookup(ref state);
            _factions = state.GetComponentLookup<Faction>(true);
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var due = DuePlayers(ref state);
            if (due.Length == 0)
            {
                return;
            }

            state.CompleteDependency();
            _targetLookup.Update(ref state);
            _factions.Update(ref state);
            var snapshot = TakeSnapshot();
            foreach (var player in due)
            {
                Think(ref state, player, snapshot);
            }
        }

        private NativeList<Entity> DuePlayers(ref SystemState state)
        {
            var due = new NativeList<Entity>(Allocator.Temp);
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (ai, entity) in SystemAPI.Query<RefRW<AIPlayer>>().WithNone<Defeated>().WithEntityAccess())
            {
                ai.ValueRW.TimeUntilThink -= deltaTime;
                if (ai.ValueRO.TimeUntilThink <= 0f)
                {
                    ai.ValueRW.TimeUntilThink = ai.ValueRO.ThinkInterval;
                    due.Add(entity);
                }
            }

            return due;
        }

        private Snapshot TakeSnapshot() => new()
        {
            Harvesters = AIGroup.From(_idleHarvesters),
            Nodes = AIGroup.From(_nodes),
            NodeData = _nodes.ToComponentDataArray<ResourceNode>(Allocator.Temp),
            Army = AIGroup.From(_idleArmy),
            Targets = AIGroup.From(_targets),
            Builders = AIGroup.From(_builders),
            Buildings = AIGroup.From(_buildings),
            Sites = AIGroup.From(_sites),
            Casters = AIGroup.From(_casters),
            Owned = AIGroup.From(_owned),
            OwnedInfo = _owned.ToComponentDataArray<EntityInfo>(Allocator.Temp),
            Producers = _producers.ToEntityArray(Allocator.Temp),
            Completed = new CompletedBuildings(_completed, Allocator.Temp),
            Relations = SystemAPI.GetSingleton<FactionRelations>(),
        };

        private void Think(ref SystemState state, Entity player, in Snapshot snapshot)
        {
            var faction = SystemAPI.GetComponent<Player>(player).Faction;
            var turn = new Turn
            {
                Player = player,
                Faction = faction,
                Home = Home(faction, snapshot),
                Commands = SystemAPI.GetBuffer<PlayerCommand>(player),
                Busy = new NativeList<Entity>(Allocator.Temp),
            };

            var buildOrderDone = FollowBuildOrder(ref state, turn, snapshot);
            ResumeSites(ref state, turn, snapshot);
            SendHarvesters(turn, snapshot);
            if (buildOrderDone)
            {
                Train(ref state, turn, snapshot);
            }

            Attack(ref state, turn, snapshot);
            if (SystemAPI.GetComponent<AIPlayer>(player).UseAbilities)
            {
                UseAbilities(ref state, turn, snapshot);
            }
        }

        /// <summary>Where the AI builds around: its building with the lowest entity index, else its first unit.</summary>
        private static float3 Home(byte faction, in Snapshot snapshot)
        {
            var buildings = snapshot.Buildings;
            var home = FirstOwned(buildings, faction);
            if (home >= 0)
            {
                return buildings.Position(home);
            }

            var owned = FirstOwned(snapshot.Owned, faction);
            return owned >= 0 ? snapshot.Owned.Position(owned) : float3.zero;
        }

        private static int FirstOwned(in AIGroup group, byte faction)
        {
            var best = -1;
            for (var i = 0; i < group.Length; i++)
            {
                if (!group.IsOwnedBy(i, faction))
                {
                    continue;
                }

                if (best < 0 || group.Entities[i].Index < group.Entities[best].Index)
                {
                    best = i;
                }
            }

            return best;
        }
    }
}
