using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>One unit's own components the overlays read, taken from its chunk once per frame.</summary>
    public readonly struct OverlayUnit
    {
        public readonly NavAgent? Agent;
        public readonly NavObstacle? Obstacle;
        public readonly int Rank;

        /// <summary>Room one standing soldier takes round its own origin.</summary>
        private const float MemberRadius = 0.8f;

        /// <summary>Squad members, or an uncreated buffer for everything that isn't a squad.</summary>
        public readonly DynamicBuffer<SquadMember> Members;

        public readonly Suppression? Suppression;

        public OverlayUnit(NavAgent? agent, NavObstacle? obstacle, int rank, DynamicBuffer<SquadMember> members,
            Suppression? suppression)
        {
            Suppression = suppression;
            Agent = agent;
            Obstacle = obstacle;
            Rank = rank;
            Members = members;
        }

        public bool IsSquad => Members.IsCreated;
        public int MemberCount => IsSquad ? Members.Length : 0;
        public int Alive => IsSquad ? SquadMember.CountLiving(Members) : 0;

        /// <summary>Radius of the circle that takes in the squad's living members, which stand past its nav radius.</summary>
        public float Spread
        {
            get
            {
                var spread = Radius;
                if (!IsSquad)
                {
                    return spread;
                }

                foreach (var member in Members)
                {
                    spread = member.Alive ? math.max(spread, math.length(member.Offset) + MemberRadius) : spread;
                }

                return spread;
            }
        }
        public NavLayer Layer => Agent?.Layer ?? NavLayer.Ground;

        public float Radius
        {
            get
            {
                if (Agent.HasValue)
                {
                    return Agent.Value.Radius;
                }

                return Obstacle.HasValue ? EntityRadius.Get(Obstacle.Value) : EntityRadius.Default;
            }
        }

        /// <summary>Marker for a vehicle, infantry or building; squads take a circle round their members instead.</summary>
        public MarkerFootprint Footprint(float margin)
        {
            if (Agent.HasValue)
            {
                return MarkerFootprint.Agent(Agent.Value, margin);
            }

            return Obstacle.HasValue
                ? MarkerFootprint.Building(Obstacle.Value, margin)
                : MarkerFootprint.Round(EntityRadius.Default, margin);
        }
    }
}
