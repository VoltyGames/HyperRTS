using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Upgrades;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Skirmish AI for <see cref="AIPlayer"/>s: keeps harvesters gathering and producers training, and sends idle
    /// armies at the nearest enemy base. It only issues <see cref="PlayerCommand"/>s, exactly like a human player.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct SkirmishAISystem : ISystem
    {
        private struct Budget
        {
            public byte Faction;
            public Population Room;
            public DynamicBuffer<ResourceStock> Stock;
            public DynamicBuffer<ResearchedUpgrade> Researched;
            public CompletedBuildings Completed;
        }

        /// <summary>Query results shared by every AI that thinks this frame.</summary>
        private struct Snapshot
        {
            public AIGroup Harvesters;
            public AIGroup Nodes;
            public NativeArray<ResourceNode> NodeData;
            public AIGroup Army;
            public AIGroup Targets;
            public NativeArray<Entity> Producers;
            public CompletedBuildings Completed;
        }

        private EntityQuery _idleHarvesters;
        private EntityQuery _idleArmy;
        private EntityQuery _nodes;
        private EntityQuery _producers;
        private EntityQuery _targets;
        private EntityQuery _completed;
        private EntityQuery _queues;

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
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _queues = UpgradeRules.QueueQuery(Allocator.Temp).Build(ref state);
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
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

            if (due.Length == 0)
            {
                return;
            }

            var snapshot = TakeSnapshot();
            foreach (var player in due)
            {
                var faction = SystemAPI.GetComponent<Player>(player).Faction;
                var commands = SystemAPI.GetBuffer<PlayerCommand>(player);
                SendHarvesters(faction, commands, snapshot);
                Train(ref state, player, faction, commands, snapshot);
                Attack(ref state, player, faction, commands, snapshot);
            }
        }

        private Snapshot TakeSnapshot() => new()
        {
            Harvesters = AIGroup.From(_idleHarvesters),
            Nodes = AIGroup.From(_nodes),
            NodeData = _nodes.ToComponentDataArray<ResourceNode>(Allocator.Temp),
            Army = AIGroup.From(_idleArmy),
            Targets = AIGroup.From(_targets),
            Producers = _producers.ToEntityArray(Allocator.Temp),
            Completed = new CompletedBuildings(_completed, Allocator.Temp),
        };

        private static void SendHarvesters(byte faction, DynamicBuffer<PlayerCommand> commands, in Snapshot snapshot)
        {
            var harvesters = snapshot.Harvesters;
            var nodes = snapshot.Nodes;
            for (var i = 0; i < harvesters.Length; i++)
            {
                if (!harvesters.IsOwnedBy(i, faction))
                {
                    continue;
                }

                var node = NearestNode(nodes, snapshot.NodeData, harvesters.Position(i));
                if (node < 0)
                {
                    return;
                }

                commands.Add(new PlayerCommand
                {
                    Type = CommandType.Gather,
                    Unit = harvesters.Entities[i],
                    Target = nodes.Entities[node],
                    Position = nodes.Position(node),
                });
            }
        }

        private static int NearestNode(in AIGroup nodes, NativeArray<ResourceNode> data, float3 from)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < nodes.Length; i++)
            {
                var distance = math.distancesq(nodes.Position(i).xz, from.xz);
                if (distance < bestDistance && data[i].Amount > 0)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void Train(ref SystemState state, Entity player, byte faction, DynamicBuffer<PlayerCommand> commands,
            in Snapshot snapshot)
        {
            ref var ai = ref SystemAPI.GetComponentRW<AIPlayer>(player).ValueRW;
            var budget = new Budget
            {
                Faction = faction,
                Room = SystemAPI.GetComponent<Population>(player),
                Stock = SystemAPI.GetBuffer<ResourceStock>(player),
                Researched = SystemAPI.GetBuffer<ResearchedUpgrade>(player),
                Completed = snapshot.Completed,
            };

            foreach (var producer in snapshot.Producers)
            {
                if (SystemAPI.GetComponent<Faction>(producer).Value != faction ||
                    !SystemAPI.GetBuffer<ProductionQueueItem>(producer).IsEmpty)
                {
                    continue;
                }

                var prefab = PickOption(ref state, ref ai, SystemAPI.GetBuffer<ProductionOption>(producer), budget);
                if (prefab != Entity.Null)
                {
                    budget.Room.Used += SystemAPI.GetComponent<Producible>(prefab).Population;
                    commands.Add(new PlayerCommand { Type = CommandType.Produce, Unit = producer, Prefab = prefab });
                }
            }
        }

        /// <summary>Next option after the last one picked that fits population, stockpile and tech, skipping done or queued research.</summary>
        private Entity PickOption(ref SystemState state, ref AIPlayer ai, DynamicBuffer<ProductionOption> options,
            in Budget budget)
        {
            for (var k = 0; k < options.Length; k++)
            {
                var index = (math.max(ai.NextOption, 0) + k) % options.Length;
                var prefab = options[index].Prefab;
                if (UpgradeRules.CanQueue(state.EntityManager, _queues, budget.Researched, budget.Faction, prefab) &&
                    budget.Room.HasRoomFor(SystemAPI.GetComponent<Producible>(prefab).Population) &&
                    ResourceMath.CanAfford(budget.Stock, SystemAPI.GetBuffer<ResourceCost>(prefab)) &&
                    budget.Completed.MeetsPrerequisites(SystemAPI.GetBuffer<Prerequisite>(prefab), budget.Faction))
                {
                    ai.NextOption = index + 1;
                    return prefab;
                }
            }

            return Entity.Null;
        }

        private void Attack(ref SystemState state, Entity player, byte faction, DynamicBuffer<PlayerCommand> commands,
            in Snapshot snapshot)
        {
            var army = snapshot.Army;
            var wave = new NativeList<int>(Allocator.Temp);
            var center = float3.zero;
            for (var i = 0; i < army.Length; i++)
            {
                if (army.IsOwnedBy(i, faction))
                {
                    wave.Add(i);
                    center += army.Position(i);
                }
            }

            if (wave.Length == 0 || wave.Length < SystemAPI.GetComponent<AIPlayer>(player).AttackWaveSize ||
                !TryFindTarget(ref state, faction, center / wave.Length, snapshot.Targets, out var target))
            {
                return;
            }

            foreach (var i in wave)
            {
                commands.Add(new PlayerCommand { Type = CommandType.AttackMove, Unit = army.Entities[i], Position = target });
            }
        }

        /// <summary>Nearest hostile critical building, else the nearest hostile unit; undetected stealth is skipped.</summary>
        private bool TryFindTarget(ref SystemState state, byte faction, float3 from, in AIGroup targets,
            out float3 target)
        {
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            SystemAPI.TryGetSingleton(out FogOfWar fog);
            var bestBase = float.MaxValue;
            var bestUnit = float.MaxValue;
            float3 basePosition = default, unitPosition = default;

            for (var i = 0; i < targets.Length; i++)
            {
                var entity = targets.Entities[i];
                var distance = math.distancesq(targets.Position(i).xz, from.xz);
                if (!relations.IsHostile(faction, targets.Owners[i].Value))
                {
                    continue;
                }

                var stealthed = IsStealthed(ref state, entity);
                if (fog.IsCloakedFrom(relations.TeamOf(faction), targets.Position(i), stealthed))
                {
                    continue;
                }

                if (SystemAPI.HasComponent<BuildingTag>(entity) && SystemAPI.HasComponent<VictoryCritical>(entity) &&
                    distance < bestBase)
                {
                    bestBase = distance;
                    basePosition = targets.Position(i);
                }
                else if (SystemAPI.HasComponent<UnitTag>(entity) && distance < bestUnit)
                {
                    bestUnit = distance;
                    unitPosition = targets.Position(i);
                }
            }

            target = bestBase < float.MaxValue ? basePosition : unitPosition;
            return bestBase < float.MaxValue || bestUnit < float.MaxValue;
        }

        private bool IsStealthed(ref SystemState state, Entity entity) =>
            SystemAPI.HasComponent<Stealthed>(entity) && SystemAPI.IsComponentEnabled<Stealthed>(entity);
    }
}
