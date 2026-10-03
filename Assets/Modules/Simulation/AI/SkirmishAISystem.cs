using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
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
            public NativeArray<EntityInfo> Infos;
            public NativeArray<Faction> Owners;
        }

        private EntityQuery _idleHarvesters;
        private EntityQuery _idleArmy;
        private EntityQuery _nodes;
        private EntityQuery _producers;
        private EntityQuery _targets;
        private EntityQuery _completed;

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
            _completed = ProductionRules.CompletedBuildings(Allocator.Temp).Build(ref state);
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

            foreach (var player in due)
            {
                var faction = SystemAPI.GetComponent<Player>(player).Faction;
                var commands = SystemAPI.GetBuffer<PlayerCommand>(player);
                SendHarvesters(ref state, faction, commands);
                Train(ref state, player, faction, commands);
                Attack(ref state, player, faction, commands);
            }
        }

        private void SendHarvesters(ref SystemState state, byte faction, DynamicBuffer<PlayerCommand> commands)
        {
            var harvesters = AIGroup.From(_idleHarvesters);
            var nodes = AIGroup.From(_nodes);
            for (var i = 0; i < harvesters.Length; i++)
            {
                if (!harvesters.IsOwnedBy(i, faction))
                {
                    continue;
                }

                var node = NearestNode(ref state, nodes, harvesters.Position(i));
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

        private int NearestNode(ref SystemState state, in AIGroup nodes, float3 from)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < nodes.Length; i++)
            {
                var distance = math.distancesq(nodes.Position(i).xz, from.xz);
                if (distance < bestDistance && SystemAPI.GetComponent<ResourceNode>(nodes.Entities[i]).Amount > 0)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void Train(ref SystemState state, Entity player, byte faction, DynamicBuffer<PlayerCommand> commands)
        {
            ref var ai = ref SystemAPI.GetComponentRW<AIPlayer>(player).ValueRW;
            var budget = new Budget
            {
                Faction = faction,
                Room = SystemAPI.GetComponent<Population>(player),
                Stock = SystemAPI.GetBuffer<ResourceStock>(player),
                Infos = _completed.ToComponentDataArray<EntityInfo>(Allocator.Temp),
                Owners = _completed.ToComponentDataArray<Faction>(Allocator.Temp),
            };

            foreach (var producer in _producers.ToEntityArray(Allocator.Temp))
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

        /// <summary>Next option after the last one picked that fits population, stockpile and tech.</summary>
        private Entity PickOption(ref SystemState state, ref AIPlayer ai, DynamicBuffer<ProductionOption> options,
            in Budget budget)
        {
            for (var k = 0; k < options.Length; k++)
            {
                var index = (math.max(ai.NextOption, 0) + k) % options.Length;
                var prefab = options[index].Prefab;
                if (budget.Room.HasRoomFor(SystemAPI.GetComponent<Producible>(prefab).Population) &&
                    ResourceMath.CanAfford(budget.Stock, SystemAPI.GetBuffer<ResourceCost>(prefab)) &&
                    ProductionRules.PrerequisitesMet(SystemAPI.GetBuffer<Prerequisite>(prefab), budget.Faction,
                        budget.Infos, budget.Owners))
                {
                    ai.NextOption = index + 1;
                    return prefab;
                }
            }

            return Entity.Null;
        }

        private void Attack(ref SystemState state, Entity player, byte faction, DynamicBuffer<PlayerCommand> commands)
        {
            var army = AIGroup.From(_idleArmy);
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
                !TryFindTarget(ref state, faction, center / wave.Length, out var target))
            {
                return;
            }

            foreach (var i in wave)
            {
                commands.Add(new PlayerCommand { Type = CommandType.AttackMove, Unit = army.Entities[i], Position = target });
            }
        }

        /// <summary>Nearest hostile critical building, else the nearest hostile unit.</summary>
        private bool TryFindTarget(ref SystemState state, byte faction, float3 from, out float3 target)
        {
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            var targets = AIGroup.From(_targets);
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
    }
}
