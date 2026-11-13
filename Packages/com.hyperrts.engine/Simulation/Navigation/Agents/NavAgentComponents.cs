using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Agents
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
        /// <see cref="NavAccess.AllScenery"/> walks between everything.
        /// </summary>
        public byte SceneryCrushLimit;

        /// <summary>
        /// Distance from walls inside which its paths cost more, so they keep room to turn and pass (see
        /// <see cref="HullWallMargin"/>); 0 lets them hug walls.
        /// </summary>
        public float WallMargin;

        /// <summary>How the hull turns and reverses; default for units without handling.</summary>
        public Chassis Chassis;

        /// <summary>Tonnes: the heavier of two units by half again has the right of way.</summary>
        public float Mass;

        /// <summary>Has locomotion handling, whatever its chassis; foot units never do.</summary>
        public bool IsVehicle;

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
        /// The wall margin its size needs: the radius, 2 m of room and a quarter of the hull's half segment, so long
        /// hulls keep further off.
        /// </summary>
        public readonly float HullWallMargin => Radius + 2f + 0.25f * HalfSegment;

        /// <summary>A vehicle that backs up: it has a reverse run and a turn radius or a hull longer than it is wide.</summary>
        public readonly bool CanReverse => Chassis.ReverseRun > 0f && (Chassis.TurnRadius > 0f || HalfSegment > 0f);

        /// <summary>The core segment's half on the XZ plane, along the unit's facing.</summary>
        public readonly float2 SegmentAxis(quaternion rotation) => SegmentAxis(rotation, Radius, HullLength);

        /// <summary>The same axis for any capsule footprint, such as a <see cref="Blocker"/>.</summary>
        public static float2 SegmentAxis(quaternion rotation, float radius, float hullLength) =>
            math.normalizesafe(math.mul(rotation, math.forward()).xz) * HalfSegmentFor(radius, hullLength);

        /// <summary>How far <paramref name="point"/> lies outside the footprint centred at <paramref name="center"/>.</summary>
        public readonly float FootprintGap(float2 center, quaternion rotation, float2 point)
        {
            var axis = SegmentAxis(rotation);
            return Planar.PointSegmentDistance(point, center - axis, center + axis) - Radius;
        }

        public static float HalfSegmentFor(float radius, float hullLength) => math.max(0f, hullLength * 0.5f - radius);

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

        /// <summary>
        /// Metres the formation's slots span. Touching an arrived groupmate counts only this close to the unit's own
        /// slot, so a groupmate parked on the way doesn't end the move short.
        /// </summary>
        public float Extent;

        /// <summary>
        /// The goal follows a moving target, as combat and walk-ups set: its moves re-aim the path's end by sight and
        /// search again at most every <see cref="PathState.ChaseSearchSeconds"/>.
        /// </summary>
        public bool Chase;

        /// <summary>
        /// Drive the whole way in reverse, keeping the front toward where it came from. Only a vehicle that reverses
        /// does; any other unit moves as usual.
        /// </summary>
        public bool Backward;

        /// <summary>Whether locomotion already heads within <paramref name="tolerance"/> of the goal, so a rewrite would only restart pathing.</summary>
        public readonly bool IsHeadingTo(EnabledRefRW<MoveDestination> moving, float3 goal, float tolerance) =>
            moving.ValueRO && math.distancesq(Value.xz, goal.xz) < tolerance * tolerance;

        /// <summary>Points locomotion at a chase goal, rewriting it only when it moved more than <paramref name="tolerance"/>.</summary>
        public void MoveTo(EnabledRefRW<MoveDestination> moving, float3 goal, float tolerance = 0.1f)
        {
            if (IsHeadingTo(moving, goal, tolerance))
            {
                return;
            }

            this = new MoveDestination { Value = goal, Chase = true };
            moving.ValueRW = true;
        }
    }

    /// <summary>Enabled when locomotion ended the move by itself, with why. Disabled while a move runs.</summary>
    public struct Arrival : IComponentData, IEnableableComponent
    {
        public ArrivalReason Reason;
    }

    /// <summary>Why a move ended by itself.</summary>
    public enum ArrivalReason : byte
    {
        Reached,

        /// <summary>Gave up after making no progress.</summary>
        Stalled,

        /// <summary>Stopped against an arrived groupmate short of its goal, so no groupmate stops against it.</summary>
        AtGroupmate,
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

        /// <summary>The leg to this waypoint is driven backwards; a gear change is a waypoint where it flips.</summary>
        public bool Reverse;
    }
}
