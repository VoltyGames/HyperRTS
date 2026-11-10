using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Reach;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Docks
{
    /// <summary>One place a vehicle stands to load or unload at a dock, in the dock's space, and who holds it.</summary>
    [InternalBufferCapacity(4)]
    public struct DockBay : IBufferElementData
    {
        public float3 Stop;

        /// <summary>Which way the vehicle faces at the stop, on XZ.</summary>
        public float2 Facing;

        /// <summary>Metres of straight drive into the stop.</summary>
        public float RunIn;

        /// <summary>Where the straight run out of the bay ends.</summary>
        public float3 Exit;

        /// <summary>The occupant renews it every frame; <see cref="DockSystem"/> frees a bay nobody renewed.</summary>
        public Reservation Reservation;

        /// <summary>Places in line taken last frame, bit n for place n + 1.</summary>
        public uint Line;

        /// <summary>Places taken so far this frame, rolled into <see cref="Line"/> by <see cref="DockSystem"/>.</summary>
        public uint NextLine;

        /// <summary>Metres from the stop to the nearest vehicle in line last frame, which the bay goes to next.</summary>
        public float FrontDistance;

        /// <summary>The same so far this frame, rolled into <see cref="FrontDistance"/> by the dock system.</summary>
        public float NextFrontDistance;

        public readonly bool IsFree => Reservation.IsFree;

        public readonly BayPose WorldPose(in LocalTransform dock)
        {
            var facing = math.normalize(math.mul(dock.Rotation, new float3(Facing.x, 0f, Facing.y)).xz);
            return new BayPose(dock.TransformPoint(Stop), facing, RunIn, dock.TransformPoint(Exit));
        }
    }

    /// <summary>A dock bay in world space: where a vehicle stops, which way it faces, its run-in and exit.</summary>
    public readonly struct BayPose
    {
        /// <summary>How near the stop's centre and facing a vehicle must stand to work there.</summary>
        public const float StopReach = 0.75f;

        private const float LineGap = 2f;

        // A vehicle parked this near its place in line stays put rather than creeping onto it.
        private const float LineSlack = 1.5f;
        private static readonly float StopAngleCos = math.cos(math.radians(15f));

        public readonly float3 Stop;
        public readonly float2 Facing;
        public readonly float RunIn;
        public readonly float3 Exit;

        public BayPose(float3 stop, float2 facing, float runIn, float3 exit)
        {
            Stop = stop;
            Facing = facing;
            RunIn = runIn;
            Exit = exit;
        }

        /// <summary>Where a vehicle waits its turn: on the run-in line, a hull plus 2 m back per place.</summary>
        public float3 WaitSpot(int place, float hullLength)
        {
            var back = RunIn + (hullLength + LineGap) * place;
            return Stop - new float3(Facing.x, 0f, Facing.y) * back;
        }

        public bool IsAtStop(float2 center, float2 facing) =>
            math.distance(center, Stop.xz) <= StopReach && math.dot(facing, Facing) >= StopAngleCos;

        /// <summary>
        /// Drives a vehicle along the run-in until it stands at the stop. A drive that gave up short of the stop is
        /// stuck; one that ended off the stop's pose plans the rest from where it is.
        /// </summary>
        public ApproachState Approach(MoveControl move, float2 unit, float2 facing)
        {
            if (IsAtStop(unit, facing))
            {
                move.Stop();
                return ApproachState.Reached;
            }

            var isGoalStop = math.distancesq(move.Destination.ValueRO.Value.xz, Stop.xz) < 0.01f;
            if (isGoalStop && move.GaveUp)
            {
                return ApproachState.Stuck;
            }

            move.ToPose(Stop, Facing, RunIn);
            move.Arrived.ValueRW = false;
            return ApproachState.Walking;
        }

        /// <summary>Parks a vehicle at its place in the line, facing the bay, and keeps it there.</summary>
        public void WaitInLine(MoveControl move, int place, float hullLength, float2 unit)
        {
            var spot = WaitSpot(place, hullLength);
            move.Arrived.ValueRW = false;
            if (!move.Moving.ValueRO && math.distance(unit, spot.xz) <= LineSlack)
            {
                return;
            }

            move.ToPose(spot, Facing, 0f);
        }

        /// <summary>The hull has driven clear of the stop, so the bay can take the next vehicle.</summary>
        public bool IsClear(float2 center, float hullLength) => math.distance(center, Stop.xz) >= hullLength;

        /// <summary>Metres of the straight run out still ahead of a vehicle at <paramref name="center"/>.</summary>
        public float ExitRun(float2 center)
        {
            var way = math.normalizesafe(Exit.xz - Stop.xz);
            return math.max(0f, math.dot(Exit.xz - center, way));
        }
    }

    /// <summary>
    /// Claims and frees dock bays and keeps their lines, for single-threaded jobs such as gathering. A holder holds
    /// its bay every frame; <see cref="DockSystem"/> frees bays nobody held.
    /// </summary>
    public struct DockBays
    {
        // One bit a place in a line's mask.
        private const int MostPlaces = 32;

        // Metres within which a vehicle counts as the nearest in line, for float noise between frames.
        private const float FrontSlack = 0.05f;

        private BufferLookup<DockBay> _bays;
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;

        public DockBays(ref SystemState state)
        {
            _bays = state.GetBufferLookup<DockBay>();
            _transforms = state.GetComponentLookup<LocalTransform>(true);
        }

        public void Update(ref SystemState state)
        {
            _bays.Update(ref state);
            _transforms.Update(ref state);
        }

        public bool Has(Entity dock) => _bays.TryGetBuffer(dock, out var bays) && bays.Length > 0;

        public BayPose Pose(Entity dock, int bay) => _bays[dock][bay].WorldPose(_transforms[dock]);

        public Entity Occupant(Entity dock, int bay) => _bays[dock][bay].Reservation.Holder;

        /// <summary>
        /// Claims the free bay nearest the unit that nobody waits for ahead of it: one with no line, or the bay it
        /// waits for (<paramref name="waitsAt"/>, -1 for none) when it stands nearest that bay's stop.
        /// </summary>
        public bool TryClaim(Entity dock, Entity unit, float2 position, int waitsAt, out int claimed)
        {
            var bays = _bays[dock];
            var transform = _transforms[dock];
            var closest = Closest.None;
            for (var i = 0; i < bays.Length; i++)
            {
                var bay = bays[i];
                var distance = math.distance(position, bay.WorldPose(transform).Stop.xz);
                if (bay.IsFree && IsNext(bay, i == waitsAt, distance))
                {
                    closest.Consider(i, dock, distance * distance);
                }
            }

            claimed = closest.Index;
            if (closest.Found)
            {
                Reserve(dock, claimed, unit);
            }

            return closest.Found;
        }

        /// <summary>Keeps the bay the unit's for another frame.</summary>
        public void Reserve(Entity dock, int bay, Entity unit)
        {
            _bays[dock].ElementAt(bay).Reservation.Reserve(unit);
        }

        public void Free(Entity dock, int bay, Entity unit) => _bays[dock].ElementAt(bay).Reservation.Release(unit);

        // The bay goes to the vehicle in line standing nearest its stop, so none drives through another waiting.
        private static bool IsNext(in DockBay bay, bool waits, float distance)
        {
            if (bay.Line == 0)
            {
                return true;
            }

            return waits && distance <= bay.FrontDistance + FrontSlack;
        }

        /// <summary>
        /// The unit's place in line this frame: its old one moved up past places left empty, or for a newcomer
        /// (<paramref name="place"/> 0) the end of the shortest line, whose bay it sets. It also counts towards who
        /// stands nearest the stop.
        /// </summary>
        public int Queue(Entity dock, ref int bay, int place, float2 position)
        {
            var bays = _bays[dock];
            if (place == 0)
            {
                bay = ShortestLine(bays);
            }

            ref var line = ref bays.ElementAt(bay);
            var distance = math.distance(position, line.WorldPose(_transforms[dock]).Stop.xz);
            line.NextFrontDistance = line.NextLine == 0u ? distance : math.min(line.NextFrontDistance, distance);
            var lastTaken = MostPlaces - math.lzcnt(line.Line | line.NextLine);
            var ahead = math.countbits(line.Line & ((1u << (place - 1)) - 1u));
            var next = math.min(place == 0 ? lastTaken + 1 : ahead + 1, MostPlaces);
            line.NextLine |= 1u << (next - 1);
            return next;
        }

        private static int ShortestLine(DynamicBuffer<DockBay> bays)
        {
            var shortest = 0;
            var fewest = int.MaxValue;
            for (var i = 0; i < bays.Length; i++)
            {
                var queued = math.countbits(bays[i].Line | bays[i].NextLine);
                if (queued < fewest)
                {
                    shortest = i;
                    fewest = queued;
                }
            }

            return shortest;
        }
    }
}
