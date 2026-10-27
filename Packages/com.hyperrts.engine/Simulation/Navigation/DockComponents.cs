using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
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

        public Entity Occupant;

        /// <summary>The occupant claimed it since <see cref="DockSystem"/> last ran; a bay nobody claims frees.</summary>
        public bool Claimed;

        /// <summary>Places in line taken last frame, bit n for place n + 1.</summary>
        public uint Line;

        /// <summary>Places taken so far this frame, rolled into <see cref="Line"/> by <see cref="DockSystem"/>.</summary>
        public uint NextLine;

        public readonly bool IsFree => Occupant == Entity.Null;

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

        public Entity Occupant(Entity dock, int bay) => _bays[dock][bay].Occupant;

        /// <summary>
        /// Claims the free bay nearest the unit that nobody waits for ahead of it: one with no line, or the bay it
        /// is first in line for (<paramref name="firstInLineAt"/>, -1 for none).
        /// </summary>
        public bool TryClaim(Entity dock, Entity unit, float2 position, int firstInLineAt, out int claimed)
        {
            var bays = _bays[dock];
            var transform = _transforms[dock];
            var closest = Closest.None;
            for (var i = 0; i < bays.Length; i++)
            {
                var bay = bays[i];
                var isFirst = bay.Line == 0 || i == firstInLineAt;
                if (bay.IsFree && isFirst)
                {
                    closest.Consider(i, dock, math.distancesq(position, bay.WorldPose(transform).Stop.xz));
                }
            }

            claimed = closest.Index;
            if (closest.Found)
            {
                Hold(dock, claimed, unit);
            }

            return closest.Found;
        }

        /// <summary>Keeps the bay the unit's for another frame.</summary>
        public void Hold(Entity dock, int bay, Entity unit)
        {
            ref var held = ref _bays[dock].ElementAt(bay);
            held.Occupant = unit;
            held.Claimed = true;
        }

        public void Free(Entity dock, int bay, Entity unit)
        {
            ref var held = ref _bays[dock].ElementAt(bay);
            if (held.Occupant == unit)
            {
                held.Occupant = Entity.Null;
                held.Claimed = false;
            }
        }

        /// <summary>
        /// The unit's place in line this frame: its old one moved up past places left empty, or for a newcomer
        /// (<paramref name="place"/> 0) the end of the shortest line, whose bay it sets.
        /// </summary>
        public int Queue(Entity dock, ref int bay, int place)
        {
            var bays = _bays[dock];
            if (place == 0)
            {
                bay = ShortestLine(bays);
            }

            ref var line = ref bays.ElementAt(bay);
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
