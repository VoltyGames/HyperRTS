using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// ORCA local avoidance: gathers a mover's nearest neighbours and picks the velocity closest to its planned one
    /// that keeps clear of them for <see cref="TimeHorizon"/>. Two movers share the avoiding. An idle unit of the
    /// mover's side shares it too, stepping aside through <see cref="SeparationVisitor"/>; a unit holding its ground
    /// (<see cref="GivingWay"/>) leaves it all to the mover. Units on layers that can't block each other are ignored.
    /// </summary>
    internal struct AvoidanceVisitor : ISpatialVisitor
    {
        public const int MaxNeighbours = 8;
        public const float TimeHorizon = 1.5f;

        public Entity Self;
        public float2 Position;
        public float Radius;
        public float2 SegmentAxis;
        public float Spacing;
        public NavLayer Layer;
        public byte Faction;
        public float2 Goal;
        public int Group;
        public GivingWay Rule;
        private FixedList512Bytes<AvoidanceNeighbour> _neighbours;

        public AvoidanceVisitor(Entity self, float2 position, float2 axis, in NavAgent agent, byte faction,
            in MoveDestination destination, in GivingWay rule)
        {
            Self = self;
            Position = position;
            Radius = agent.Radius;
            SegmentAxis = axis;
            Spacing = agent.Spacing;
            Layer = agent.Layer;
            Faction = faction;
            Goal = destination.Value.xz;
            Group = destination.Group;
            Rule = rule;
            _neighbours = default;
        }

        private readonly float3 Center => new(Position.x, 0f, Position.y);

        /// <summary>The velocity nearest <paramref name="planned"/> that keeps clear of the neighbours round the unit.</summary>
        public float2 Avoid(in SpatialIndex index, float2 velocity, float2 planned, float maxSpeed, float deltaTime)
        {
            index.QueryHulls(Center, Reach(maxSpeed), ref this);
            var planes = new VelocityPlanes();
            foreach (var neighbour in _neighbours)
            {
                planes.Add(neighbour.Plane(velocity, TimeHorizon, deltaTime));
            }

            return planes.Solve(planned, maxSpeed);
        }

        /// <summary>As far as two units closing at top speed cover within the time horizon.</summary>
        private readonly float Reach(float maxSpeed) =>
            Radius + math.length(SegmentAxis) + Spacing + maxSpeed * TimeHorizon * 2f;

        public void Visit(in SpatialEntry entry)
        {
            if (!entry.IsUnit || entry.Entity == Self)
            {
                return;
            }

            if (!NavLayers.CanBlock(Layer, entry.Layer) || IsWelcome(entry))
            {
                return;
            }

            var offset = -entry.ClosestOffset(Position, SegmentAxis);
            var distanceSq = math.lengthsq(offset);
            // Stacked units have no line between them; the separation push parts them first.
            if (distanceSq < 1e-8f)
            {
                return;
            }

            Keep(new AvoidanceNeighbour
            {
                Offset = offset,
                Velocity = entry.Velocity,
                Reach = Radius + entry.Radius + math.max(Spacing, entry.Spacing),
                Share = Share(entry),
                DistanceSq = distanceSq,
            });
        }

        // A mover arrives by touching an arrived groupmate and waits against a unit on its goal, so it avoids neither.
        private readonly bool IsWelcome(in SpatialEntry entry)
        {
            if (entry.IsMoving)
            {
                return false;
            }

            var groupmate = Group != 0 && entry.Group == Group && entry.IsArrived;
            return groupmate || math.distance(Goal, entry.Position.xz) < entry.HullReach + Radius;
        }

        private readonly float Share(in SpatialEntry entry)
        {
            if (entry.IsMoving || Rule.Yields(entry.Entity, entry.Faction, Faction))
            {
                return 0.5f;
            }

            return 1f;
        }

        private void Keep(in AvoidanceNeighbour neighbour)
        {
            if (_neighbours.Length < MaxNeighbours)
            {
                _neighbours.Add(neighbour);
                return;
            }

            var farthest = 0;
            for (var i = 1; i < _neighbours.Length; i++)
            {
                farthest = _neighbours[i].DistanceSq > _neighbours[farthest].DistanceSq ? i : farthest;
            }

            if (neighbour.DistanceSq < _neighbours[farthest].DistanceSq)
            {
                _neighbours[farthest] = neighbour;
            }
        }
    }
}
