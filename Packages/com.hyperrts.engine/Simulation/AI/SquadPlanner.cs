using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Squads;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Sends the AI's squads to retreat and reinforce points, and says which squads are still recovering.</summary>
    internal struct SquadPlanner
    {
        private EntityQuery _squadQuery;
        private EntityQuery _retreatQuery;
        private EntityQuery _reinforceQuery;
        private EntityQuery _rulesQuery;
        private AIGroup _squads;
        private SquadPoints _retreatPoints;
        private SquadPoints _reinforcePoints;

        public static SquadPlanner Create(ref SystemState state) => new()
        {
            _squadQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SquadMember, Suppression, Health, Owner, LocalTransform>().WithNone<Dead>().Build(ref state),
            _retreatQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<RetreatPoint, Owner, LocalTransform>()
                .WithNone<Dead, Offline>().Build(ref state),
            _reinforceQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<ReinforcePoint, Owner, LocalTransform>()
                .WithNone<Dead, Offline>().Build(ref state),
            _rulesQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<SquadRules>().Build(ref state),
        };

        public void Update()
        {
            _squads = AIGroup.From(_squadQuery);
            _retreatPoints = SquadPoints.From<RetreatPoint>(_retreatQuery);
            _reinforcePoints = SquadPoints.From<ReinforcePoint>(_reinforceQuery);
        }

        /// <summary>
        /// Sends the bot's squads that are pinned or worn down to a retreat point (see <see cref="ShouldRetreat"/>), and
        /// idle ones that stand within reach of a reinforce point and can pay for a member to reinforce.
        /// </summary>
        public readonly void Plan(ref SystemState state, in AITurn turn, in AISnapshot snapshot)
        {
            if (_squads.Length == 0)
            {
                return;
            }

            var entities = state.EntityManager;
            var ai = entities.GetComponentData<AIPlayer>(turn.Player);
            for (var i = 0; i < _squads.Length; i++)
            {
                var squad = _squads.Entities[i];
                if (!_squads.IsOwnedBy(i, turn.Owner) || turn.Busy.Contains(squad))
                {
                    continue;
                }

                if (ai.RetreatsSquads && ShouldRetreat(ref state, turn, snapshot, squad, _squads.Position(i)))
                {
                    turn.Commands.Add(new PlayerCommand { Type = CommandType.Retreat, Unit = squad });
                    turn.Busy.Add(squad);
                }
                else if (!entities.IsComponentEnabled<ActiveOrder>(squad) && WantsReinforcing(ref state, turn, snapshot, squad))
                {
                    turn.Commands.Add(new PlayerCommand { Type = CommandType.Reinforce, Unit = squad });
                    turn.Busy.Add(squad);
                }
            }
        }

        /// <summary>
        /// A squad that is pinned, or down to the tuned share of living members or of health, and is not already
        /// running or safe inside a friendly retreat point's radius; false when no point is left.
        /// </summary>
        private readonly bool ShouldRetreat(ref SystemState state, in AITurn turn, in AISnapshot snapshot, Entity squad,
            float3 position)
        {
            var entities = state.EntityManager;
            if (IsRetreating(entities, squad) || IsRetreatOrdered(entities, squad))
            {
                return false;
            }

            var hasPoint = _retreatPoints.HasAllied(turn.Owner, snapshot.Relations);
            var refused = SquadOrderRule.FindRetreatRefusal(hasPoint) != SquadRefusal.None;
            if (refused || _retreatPoints.Covers(position, turn.Owner, snapshot.Relations))
            {
                return false;
            }

            var ai = entities.GetComponentData<AIPlayer>(turn.Player);
            var rules = _rulesQuery.GetSingleton<SquadRules>();
            var members = entities.GetBuffer<SquadMember>(squad);
            var roster = new SquadRoster(members);
            var pinned = rules.Level(entities.GetComponentData<Suppression>(squad).Value) == SuppressionLevel.Pinned;
            var fewLiving = roster.CountLiving() <= members.Length * ai.RetreatLivingShare;
            var hurt = roster.Total() <= roster.TotalMax() * ai.RetreatHealthShare;
            return pinned || fewLiving || hurt;
        }

        private static bool IsRetreatOrdered(EntityManager entities, Entity squad) =>
            entities.IsComponentEnabled<ActiveOrder>(squad)
            && entities.GetComponentData<ActiveOrder>(squad).Value.Type == OrderType.Retreat;

        private static bool IsRetreating(EntityManager entities, Entity squad) =>
            entities.HasComponent<Retreating>(squad) && entities.IsComponentEnabled<Retreating>(squad);

        /// <summary>A squad with dead members, in range of a friendly reinforce point, that the player can afford a member for.</summary>
        private readonly bool WantsReinforcing(ref SystemState state, in AITurn turn, in AISnapshot snapshot, Entity squad)
        {
            var entities = state.EntityManager;
            if (IsReinforcing(entities, squad))
            {
                return false;
            }

            var position = entities.GetComponentData<LocalTransform>(squad).Position;
            var inRange = _reinforcePoints.Covers(position, turn.Owner, snapshot.Relations);
            var cost = entities.HasBuffer<ResourceCost>(squad)
                ? entities.GetBuffer<ResourceCost>(squad)
                : default;
            var refusal = SquadOrderRule.FindReinforceRefusal(entities.GetBuffer<SquadMember>(squad), inRange,
                _rulesQuery.GetSingleton<SquadRules>(), cost, entities.GetBuffer<ResourceStock>(turn.Player));
            return refusal == SquadRefusal.None;
        }

        private static bool IsReinforcing(EntityManager entities, Entity squad) =>
            entities.HasComponent<Reinforcing>(squad) && entities.IsComponentEnabled<Reinforcing>(squad);

        /// <summary>
        /// Whether a unit is a squad that is running for a retreat point or being reinforced, or waits to be: it stays
        /// out of attack waves and field work until it is whole again, or the money runs out.
        /// </summary>
        public readonly bool IsRecovering(ref SystemState state, in AITurn turn, in AISnapshot snapshot, Entity unit)
        {
            var entities = state.EntityManager;
            if (!entities.HasBuffer<SquadMember>(unit))
            {
                return false;
            }

            if (IsRetreating(entities, unit) || IsReinforcing(entities, unit))
            {
                return true;
            }

            return _squads.Length > 0 && WantsReinforcing(ref state, turn, snapshot, unit);
        }
    }
}
