using HyperRTS.Presentation.Animation;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>One unit's own components the overlays read, taken from its chunk once per frame.</summary>
    public readonly struct OverlayUnit
    {
        public readonly NavAgent? Agent;
        public readonly NavObstacle? Obstacle;
        public readonly int Rank;

        /// <summary>Squad figures, or an uncreated buffer for everything that isn't a squad.</summary>
        public readonly DynamicBuffer<SquadFigure> Figures;

        public OverlayUnit(NavAgent? agent, NavObstacle? obstacle, int rank, DynamicBuffer<SquadFigure> figures)
        {
            Agent = agent;
            Obstacle = obstacle;
            Rank = rank;
            Figures = figures;
        }

        public bool IsSquad => Figures.IsCreated;
        public int Members => IsSquad ? Figures.Length : 0;
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

        /// <summary>Marker for a vehicle, infantry or building; squads take a circle round their figures instead.</summary>
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
