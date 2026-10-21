using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// The ground marker's outline round one footprint: a rounded rectangle for vehicle hulls, a circle for round
    /// ones and corner brackets round a building's box. Half sizes are across (x) and along (y) the facing.
    /// </summary>
    public readonly struct MarkerFootprint
    {
        // Building brackets keep a hint of rounding so their corners don't alias.
        private const float BracketCorner = 0.25f;

        // Vehicle hulls read as rounded boxes rather than pills.
        private const float HullCorner = 0.45f;

        public readonly float2 HalfSize;
        public readonly float CornerRadius;
        public readonly MarkerShape Shape;

        public MarkerFootprint(float2 halfSize, float cornerRadius, MarkerShape shape)
        {
            HalfSize = halfSize;
            CornerRadius = math.min(cornerRadius, math.cmin(halfSize));
            Shape = shape;
        }

        /// <summary>A circle for round agents (infantry), a rounded rectangle as long as the hull for capsule ones.</summary>
        public static MarkerFootprint Agent(in NavAgent agent, float margin)
        {
            var radius = agent.Radius + margin;
            var corner = agent.HalfSegment > 0f ? radius * HullCorner : radius;
            return new MarkerFootprint(new float2(radius, agent.HalfLength + margin), corner, MarkerShape.Outline);
        }

        public static MarkerFootprint Round(float radius, float margin) =>
            new(new float2(radius + margin), radius + margin, MarkerShape.Outline);

        public static MarkerFootprint Building(in NavObstacle obstacle, float margin) =>
            new(obstacle.Size * 0.5f + margin, BracketCorner, MarkerShape.Brackets);

        public MarkerFootprint Scaled(float scale) => new(HalfSize * scale, CornerRadius * scale, Shape);
    }
}
