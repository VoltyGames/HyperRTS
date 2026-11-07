using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources.Gathering;
using HyperRTS.Simulation.Squads;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Skirmish AI for <see cref="AIPlayer"/>s: builds, produces, harvests, uses abilities, manages squads and attacks
    /// along the map's lanes (see <see cref="AIMapHints"/>). It only issues <see cref="PlayerCommand"/>s, like a human
    /// player.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
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
            public AIGroup Squads;
            public SquadPoints RetreatPoints;
            public SquadPoints ReinforcePoints;
            public AIGroup FieldWorks;
            public NativeArray<AIFieldWork> FieldWorkData;
            public NativeArray<EntityInfo> OwnedInfo;
            public NativeArray<Entity> Producers;
            public CompletedBuildings Completed;
            public OwnerRelations Relations;
            public AIMapHintsView Hints;

            /// <summary>Default (not created) in a match without fog, where everything counts as seen.</summary>
            public FogOfWar Fog;
        }

        /// <summary>One AI's think: who it is, where its base is, what it can buy, and the units already given a job this think.</summary>
        private struct Turn
        {
            public Entity Player;
            public byte Owner;
            public float3 Home;
            public DynamicBuffer<PlayerCommand> Commands;
            public DynamicBuffer<PlayerCommandSubject> Subjects;
            public NativeList<Entity> Busy;
            public Buyer Buyer;
        }

        private EntityQuery _idleHarvesters;
        private EntityQuery _idleArmy;
        private EntityQuery _nodes;
        private EntityQuery _producers;
        private EntityQuery _targets;
        private EntityQuery _builders;
        private EntityQuery _buildings;
        private EntityQuery _sites;
        private EntityQuery _obstacles;
        private EntityQuery _casters;
        private EntityQuery _owned;
        private EntityQuery _squads;
        private EntityQuery _retreatPoints;
        private EntityQuery _reinforcePoints;
        private EntityQuery _fieldWorks;
        private EntityQuery _completed;
        private EntityQuery _queues;
        private EntityQuery _limited;
        private TargetLookup _targetLookup;
        private TargetQueries _targetQueries;
        private ComponentLookup<Owner> _owners;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _idleHarvesters = SystemAPI.QueryBuilder().WithAll<Harvester, Owner, LocalTransform>()
                .WithDisabled<ActiveOrder>().WithNone<Dead>().Build();
            _idleArmy = SystemAPI.QueryBuilder().WithAll<WeaponMount, CombatStance, Owner, LocalTransform>()
                .WithDisabled<ActiveOrder>().WithNone<Harvester, Dead, AttackTarget>().Build();
            _nodes = SystemAPI.QueryBuilder().WithAll<ResourceNode, Owner, LocalTransform>().Build();
            _producers = SystemAPI.QueryBuilder().WithAll<Producer, ProductionOption, ProductionQueueItem, Owner>()
                .WithNone<ConstructionProgress, Dead>().Build();
            _targets = SystemAPI.QueryBuilder().WithAll<Health, Owner, LocalTransform>().WithNone<Dead>().Build();
            _builders = SystemAPI.QueryBuilder().WithAll<Builder, BuildOption, Owner, LocalTransform>()
                .WithPresent<ActiveOrder>().WithNone<Dead>().Build();
            _buildings = SystemAPI.QueryBuilder().WithAll<BuildingTag, Owner, LocalTransform>().WithNone<Dead>().Build();
            _sites = SystemAPI.QueryBuilder().WithAll<BuildingTag, ConstructionProgress, Owner, LocalTransform>()
                .WithNone<Dead>().Build();
            _obstacles = SystemAPI.QueryBuilder().WithAll<NavObstacle, LocalTransform>().WithNone<Dead>().Build();
            _casters = SystemAPI.QueryBuilder().WithAll<Ability, AbilityCooldown, Owner, LocalTransform>().WithNone<Dead>().Build();
            _owned = SystemAPI.QueryBuilder().WithAll<EntityInfo, Owner, LocalTransform>().WithNone<Dead>().Build();
            _squads = SystemAPI.QueryBuilder().WithAll<SquadMember, Suppression, Health, Owner, LocalTransform>()
                .WithNone<Dead>().Build();
            _retreatPoints = SystemAPI.QueryBuilder().WithAll<RetreatPoint, Owner, LocalTransform>()
                .WithNone<Dead, Offline>().Build();
            _reinforcePoints = SystemAPI.QueryBuilder().WithAll<ReinforcePoint, Owner, LocalTransform>()
                .WithNone<Dead, Offline>().Build();
            _fieldWorks = SystemAPI.QueryBuilder().WithAll<AIFieldWork, Owner, LocalTransform>().WithNone<Dead>().Build();
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _queues = ResearchRules.QueueQuery(Allocator.Temp).Build(ref state);
            _limited = BuildCounts.Query(Allocator.Temp).Build(ref state);
            _targetLookup = new TargetLookup(ref state);
            _targetQueries = TargetQueries.Create(ref state);
            _owners = state.GetComponentLookup<Owner>(true);
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<NavGrid>();

            // Without the map's hints there is no AI; AIMapHintsCheckSystem says so.
            state.RequireForUpdate<AIMapHints>();
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
            _targetLookup.Update(ref state, _targetQueries);
            _owners.Update(ref state);
            var snapshot = TakeSnapshot(ref state);
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

        private Snapshot TakeSnapshot(ref SystemState state) => new()
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
            Squads = AIGroup.From(_squads),
            RetreatPoints = SquadPoints.From<RetreatPoint>(_retreatPoints),
            ReinforcePoints = SquadPoints.From<ReinforcePoint>(_reinforcePoints),
            FieldWorks = AIGroup.From(_fieldWorks),
            FieldWorkData = _fieldWorks.ToComponentDataArray<AIFieldWork>(Allocator.Temp),
            Producers = _producers.ToEntityArray(Allocator.Temp),
            Completed = new CompletedBuildings(_completed, Allocator.Temp),
            Relations = SystemAPI.GetSingleton<OwnerRelations>(),
            Hints = TakeHints(ref state),
            Fog = _targetQueries.Fog.TryGetSingleton(out FogOfWar fog) ? fog : default,
        };

        private AIMapHintsView TakeHints(ref SystemState state)
        {
            var hints = SystemAPI.GetSingletonEntity<AIMapHints>();
            return new AIMapHintsView(SystemAPI.GetBuffer<AISlotHint>(hints),
                SystemAPI.GetBuffer<AILaneHint>(hints), SystemAPI.GetBuffer<AIHintPoint>(hints));
        }

        private void Think(ref SystemState state, Entity player, in Snapshot snapshot)
        {
            var owner = SystemAPI.GetComponent<Player>(player).Owner;
            var turn = new Turn
            {
                Player = player,
                Owner = owner,
                Home = Home(owner, snapshot),
                Commands = SystemAPI.GetBuffer<PlayerCommand>(player),
                Subjects = SystemAPI.GetBuffer<PlayerCommandSubject>(player),
                Busy = new NativeList<Entity>(Allocator.Temp),
                Buyer = Buyer.Snapshot(state.EntityManager, _queues, _limited, owner, snapshot.Completed,
                    SystemAPI.GetBuffer<CompletedResearch>(player)),
            };

            var buildOrderDone = FollowBuildOrder(ref state, turn, snapshot);
            ResumeSites(ref state, turn, snapshot);
            LookAfterSquads(ref state, turn, snapshot);
            SendHarvesters(turn, snapshot);
            if (buildOrderDone)
            {
                Train(ref state, turn, snapshot);
                BuildFieldWorks(ref state, turn, snapshot);
            }

            Deploy(ref state, turn);
            Attack(ref state, turn, snapshot);
            if (SystemAPI.GetComponent<AIPlayer>(player).UseAbilities)
            {
                UseAbilities(ref state, turn, snapshot);
            }
        }

        /// <summary>Where the AI builds around: its building with the lowest entity index, else its first unit.</summary>
        private static float3 Home(byte owner, in Snapshot snapshot)
        {
            var buildings = snapshot.Buildings;
            var home = FirstOwned(buildings, owner);
            if (home >= 0)
            {
                return buildings.Position(home);
            }

            var owned = FirstOwned(snapshot.Owned, owner);
            return owned >= 0 ? snapshot.Owned.Position(owned) : float3.zero;
        }

        private static int FirstOwned(in AIGroup group, byte owner)
        {
            var best = -1;
            for (var i = 0; i < group.Length; i++)
            {
                if (!group.IsOwnedBy(i, owner))
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
