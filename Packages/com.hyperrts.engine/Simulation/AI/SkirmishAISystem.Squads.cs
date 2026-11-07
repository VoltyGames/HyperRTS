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
    public partial struct SkirmishAISystem
    {
        /// <summary>
        /// Sends the bot's squads that are pinned or worn down to a retreat point (see <see cref="ShouldRetreat"/>), and
        /// idle ones that stand within reach of a reinforce point and can pay for a member to reinforce.
        /// </summary>
        private void LookAfterSquads(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            var squads = snapshot.Squads;
            if (squads.Length == 0)
            {
                return;
            }

            var ai = SystemAPI.GetComponent<AIPlayer>(turn.Player);
            var rules = SystemAPI.GetSingleton<SquadRules>();
            for (var i = 0; i < squads.Length; i++)
            {
                var squad = squads.Entities[i];
                if (!squads.IsOwnedBy(i, turn.Owner) || turn.Busy.Contains(squad))
                {
                    continue;
                }

                if (ai.RetreatsSquads && ShouldRetreat(ref state, squad, squads.Position(i), ai, rules, turn, snapshot))
                {
                    turn.Commands.Add(new PlayerCommand { Type = CommandType.Retreat, Unit = squad });
                    turn.Busy.Add(squad);
                }
                else if (!SystemAPI.IsComponentEnabled<ActiveOrder>(squad) && WantsReinforcing(ref state, squad, rules, turn, snapshot))
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
        private bool ShouldRetreat(ref SystemState state, Entity squad, float3 position, in AIPlayer ai, in SquadRules rules, in Turn turn,
            in Snapshot snapshot)
        {
            if (IsRetreating(ref state, squad) || IsRetreatOrdered(ref state, squad))
            {
                return false;
            }

            var points = snapshot.RetreatPoints;
            var hasPoint = points.HasAllied(turn.Owner, snapshot.Relations);
            var refused = SquadOrderRule.FindRetreatRefusal(hasPoint) != SquadRefusal.None;
            if (refused || points.Covers(position, turn.Owner, snapshot.Relations))
            {
                return false;
            }

            var members = SystemAPI.GetBuffer<SquadMember>(squad);
            var pinned = rules.Level(SystemAPI.GetComponent<Suppression>(squad).Value) == SuppressionLevel.Pinned;
            var fewLiving = SquadMember.CountLiving(members) <= members.Length * ai.RetreatLivingShare;
            var hurt = SquadMember.Total(members) <= SquadMember.TotalMax(members) * ai.RetreatHealthShare;
            return pinned || fewLiving || hurt;
        }

        private bool IsRetreatOrdered(ref SystemState state, Entity squad) =>
            SystemAPI.IsComponentEnabled<ActiveOrder>(squad)
            && SystemAPI.GetComponent<ActiveOrder>(squad).Value.Type == OrderType.Retreat;

        private bool IsRetreating(ref SystemState state, Entity squad) =>
            SystemAPI.HasComponent<Retreating>(squad) && SystemAPI.IsComponentEnabled<Retreating>(squad);

        /// <summary>A squad with dead members, in range of a friendly reinforce point, that the player can afford a member for.</summary>
        private bool WantsReinforcing(ref SystemState state, Entity squad, in SquadRules rules, in Turn turn, in Snapshot snapshot)
        {
            if (IsReinforcing(ref state, squad))
            {
                return false;
            }

            var position = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            var inRange = snapshot.ReinforcePoints.Covers(position, turn.Owner, snapshot.Relations);
            var cost = SystemAPI.HasBuffer<ResourceCost>(squad)
                ? SystemAPI.GetBuffer<ResourceCost>(squad)
                : default;
            var refusal = SquadOrderRule.FindReinforceRefusal(SystemAPI.GetBuffer<SquadMember>(squad), inRange, rules,
                cost, SystemAPI.GetBuffer<ResourceStock>(turn.Player));
            return refusal == SquadRefusal.None;
        }

        private bool IsReinforcing(ref SystemState state, Entity squad) =>
            SystemAPI.HasComponent<Reinforcing>(squad) && SystemAPI.IsComponentEnabled<Reinforcing>(squad);

        /// <summary>
        /// Whether a unit is a squad that is running for a retreat point or being reinforced, or waits to be: it stays
        /// out of attack waves and field work until it is whole again, or the money runs out.
        /// </summary>
        private bool IsRecovering(ref SystemState state, Entity unit, in Turn turn, in Snapshot snapshot)
        {
            if (!SystemAPI.HasBuffer<SquadMember>(unit))
            {
                return false;
            }

            if (IsRetreating(ref state, unit) || IsReinforcing(ref state, unit))
            {
                return true;
            }

            return snapshot.Squads.Length > 0
                   && WantsReinforcing(ref state, unit, SystemAPI.GetSingleton<SquadRules>(), turn, snapshot);
        }
    }
}
