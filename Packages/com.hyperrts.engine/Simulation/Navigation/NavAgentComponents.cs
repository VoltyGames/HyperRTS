using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// A unit that paths over the <see cref="NavGrid"/> and keeps <see cref="Spacing"/> from its neighbours. Its
    /// footprint is a circle of <see cref="Radius"/>, or a capsule <see cref="HullLength"/> long along its forward.
    /// </summary>
    public struct NavAgent : IComponentData
    {
        public float Radius;

        /// <summary>Full footprint length along the forward; up to the diameter keeps it round.</summary>
        public float HullLength;

        /// <summary>Gap kept clear between this footprint and its neighbours'.</summary>
        public float Spacing;

        public NavLayer Layer;

        /// <summary>
        /// Highest <see cref="Common.Destructible.Crush"/> rating of scenery it drives through, such as its crusher size;
        /// <see cref="NavAccess.AllScenery"/> walks between everything. Scenery rated above it blocks it.
        /// </summary>
        public byte SceneryCrushLimit;

        /// <summary>
        /// Distance from walls inside which its paths cost more, so they keep room to turn and pass (see
        /// <see cref="WallMarginFor"/>); 0 lets them hug walls.
        /// </summary>
        public float WallMargin;

        /// <summary>Half the capsule's core segment: zero for round footprints.</summary>
        public readonly float HalfSegment => HalfSegmentFor(Radius, HullLength);

        /// <summary>Half the footprint's length; the radius for round footprints.</summary>
        public readonly float HalfLength => Radius + HalfSegment;

        /// <summary>
        /// Formation slot pitch across (x) and along (y) the travel direction: a quarter footprint of slack plus the
        /// spacing, so a column of long hulls doesn't pile up nose to tail.
        /// </summary>
        public readonly float2 SlotSpacing =>
            new float2(Radius * 2f, HalfLength * 2f) * 1.25f + Spacing;

        /// <summary>Who it is to the map: its layer, crush limit and radius.</summary>
        public readonly MovementClass Class => new(Layer, SceneryCrushLimit, Radius);

        /// <summary>What this agent may enter on the grid; its class must be registered (<see cref="NavFieldSystem"/>).</summary>
        public readonly NavAccess Access(in NavGrid grid) => new(grid, Class, WallMargin);

        /// <summary>
        /// <see cref="Access"/> once its class is registered, which a unit spawned this frame may not be until
        /// <see cref="NavFieldSystem"/> runs.
        /// </summary>
        public readonly bool TryAccess(in NavGrid grid, out NavAccess access)
        {
            var registered = grid.Classes.IsCreated && grid.Classes.Find(Class) >= 0;
            access = registered ? Access(grid) : default;
            return registered;
        }

        /// <summary>
        /// The wall margin its size and handling need: the radius, 2 m of room, and a quarter each of the hull's half
        /// segment and the turn radius, so long and wide-turning hulls keep further off.
        /// </summary>
        public readonly float WallMarginFor(float turnRadius) => Radius + 2f + 0.25f * HalfSegment + 0.25f * turnRadius;

        /// <summary>The core segment's half on the XZ plane, along the unit's facing.</summary>
        public readonly float2 SegmentAxis(quaternion rotation) => SegmentAxis(rotation, Radius, HullLength);

        /// <summary>The same axis for any capsule footprint, such as a <see cref="Blocker"/>.</summary>
        public static float2 SegmentAxis(quaternion rotation, float radius, float hullLength) =>
            math.normalizesafe(math.mul(rotation, math.forward()).xz) * HalfSegmentFor(radius, hullLength);

        /// <summary>How far <paramref name="point"/> lies outside the footprint centred at <paramref name="center"/>.</summary>
        public readonly float FootprintGap(float2 center, quaternion rotation, float2 point)
        {
            var axis = SegmentAxis(rotation);
            return NavCapsule.PointSegmentDistance(point, center - axis, center + axis) - Radius;
        }

        /// <summary>Whether the hull may turn to <paramref name="turned"/> without swinging an end into a closed cell.</summary>
        public readonly bool CanTurn(in NavGrid grid, float2 center, float2 axis, quaternion turned)
        {
            if (HalfSegment <= 0f)
            {
                return true;
            }

            return new NavCapsule(Access(grid), axis).CanTurn(center, SegmentAxis(turned));
        }

        private static float HalfSegmentFor(float radius, float hullLength) => math.max(0f, hullLength * 0.5f - radius);

        /// <summary>Entities without an agent (buildings, props) sit on the ground.</summary>
        public static NavLayer GetLayer(in ComponentLookup<NavAgent> agents, Entity entity) =>
            agents.TryGetComponent(entity, out var agent) ? agent.Layer : NavLayer.Ground;
    }

    /// <summary>Locomotion goal on the XZ plane: set and enable it to move, disabled again on arrival.</summary>
    public struct MoveDestination : IComponentData, IEnableableComponent
    {
        public float3 Value;

        /// <summary>Formation move it belongs to, 0 for none: touching an arrived groupmate is arrival.</summary>
        public int Group;
    }

    /// <summary>
    /// Enabled when locomotion ended the move by itself, at the goal or against an arrived groupmate, or
    /// <see cref="Stalled"/> when it stopped making progress. Disabled while a move runs.
    /// </summary>
    public struct Arrival : IComponentData, IEnableableComponent
    {
        public bool Stalled;
    }

    /// <summary>
    /// Set and enable to turn a parked unit toward <see cref="Direction"/> on XZ, as a hull-fixed weapon aims.
    /// Movement turns it through its locomotion and disables it each frame.
    /// </summary>
    public struct FacingGoal : IComponentData, IEnableableComponent
    {
        public float2 Direction;
    }

    /// <summary>How far a unit moved on XZ last frame, in metres per second, for neighbours avoiding it.</summary>
    public struct Velocity : IComponentData
    {
        public float2 Value;
    }

    /// <summary>Remaining corners of a unit's path, nearest first.</summary>
    [InternalBufferCapacity(8)]
    public struct PathWaypoint : IBufferElementData
    {
        public float3 Position;
    }
}
