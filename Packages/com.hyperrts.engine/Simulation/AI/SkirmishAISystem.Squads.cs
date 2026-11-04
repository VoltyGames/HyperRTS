using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
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
                if (!squads.IsOwnedBy(i, turn.Faction) || turn.Busy.Contains(squad))
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
            if (SystemAPI.GetComponent<Suppression>(squad).Retreating || IsRetreatOrdered(ref state, squad))
            {
                return false;
            }

            if (!TryFindRetreatPoint(position, turn.Faction, snapshot, out var isSafe) || isSafe)
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

        /// <summary>Whether a friendly working retreat point exists, and whether <paramref name="position"/> is inside one's radius.</summary>
        private bool TryFindRetreatPoint(float3 position, byte faction, in Snapshot snapshot, out bool isSafe)
        {
            var points = snapshot.RetreatPoints;
            var found = false;
            isSafe = false;
            for (var i = 0; i < points.Length; i++)
            {
                if (!snapshot.Relations.IsAllied(faction, points.Owners[i].Value))
                {
                    continue;
                }

                found = true;
                var radius = snapshot.RetreatData[i].Radius;
                isSafe |= points.DistanceSq(i, position) <= radius * radius;
            }

            return found;
        }

        /// <summary>A squad with dead members, in range of a friendly reinforce point, that the player can afford a member for.</summary>
        private bool WantsReinforcing(ref SystemState state, Entity squad, in SquadRules rules, in Turn turn, in Snapshot snapshot)
        {
            var members = SystemAPI.GetBuffer<SquadMember>(squad);
            if (SquadMember.CountLiving(members) == members.Length || IsReinforcing(ref state, squad))
            {
                return false;
            }

            var position = SystemAPI.GetComponent<LocalTransform>(squad).Position;
            if (!IsInReinforceRange(position, turn.Faction, snapshot))
            {
                return false;
            }

            return !SystemAPI.HasBuffer<ResourceCost>(squad)
                   || ResourceMath.CanAfford(SystemAPI.GetBuffer<ResourceStock>(turn.Player),
                       SystemAPI.GetBuffer<ResourceCost>(squad), rules.ReinforceCost / members.Length);
        }

        private bool IsReinforcing(ref SystemState state, Entity squad) =>
            SystemAPI.HasComponent<Reinforcing>(squad) && SystemAPI.IsComponentEnabled<Reinforcing>(squad);

        private bool IsInReinforceRange(float3 position, byte faction, in Snapshot snapshot)
        {
            var points = snapshot.ReinforcePoints;
            for (var i = 0; i < points.Length; i++)
            {
                var range = snapshot.ReinforceData[i].Range;
                var allied = snapshot.Relations.IsAllied(faction, points.Owners[i].Value);
                if (allied && points.DistanceSq(i, position) <= range * range)
                {
                    return true;
                }
            }

            return false;
        }

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

            if (SystemAPI.GetComponent<Suppression>(unit).Retreating || IsReinforcing(ref state, unit))
            {
                return true;
            }

            return snapshot.Squads.Length > 0
                   && WantsReinforcing(ref state, unit, SystemAPI.GetSingleton<SquadRules>(), turn, snapshot);
        }
    }
}
