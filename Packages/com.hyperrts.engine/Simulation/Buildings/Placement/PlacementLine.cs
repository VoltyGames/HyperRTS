using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings.Placement
{
    /// <summary>
    /// A dragged line of building segments in any direction, each one footprint long and yawed along the line. The
    /// ghost and the authoritative command build the same line from the same start, end and footprint. A wall drawn
    /// with corners is a run of lines, each joined to the end of the one before.
    /// </summary>
    public readonly struct PlacementLine
    {
        /// <summary>Most segments one line places, so a drag across the map stays cheap.</summary>
        public const int MaxSegments = 64;

        /// <summary>The segment at the start: snapped on a new line, exact on a joined one.</summary>
        public readonly BuildingSpot First;

        /// <summary>Offset from one segment centre to the next.</summary>
        public readonly float3 Step;

        public readonly int Count;

        /// <summary>Footprint X is a segment's length along the line.</summary>
        public PlacementLine(in MapSettings map, float3 start, float3 end, float2 footprint, PlacementSurface surface,
            LineStart join)
        {
            // A click without a drag lays one segment along X.
            var direction = math.normalizesafe(end.xz - start.xz, new float2(1f, 0f));
            var first = new BuildingSpot(start, Yaw(direction), footprint, surface);
            First = join == LineStart.Joined ? first : first.Snap(map);
            var right = First.Box.Right;
            var along = math.dot(right, direction) < 0f ? -right : right;
            Step = new float3(along.x, 0f, along.y) * footprint.x;
            var reach = math.max(0f, math.dot(end.xz - First.Center.xz, along));
            Count = math.min(MaxSegments, (int)math.round(reach / footprint.x) + 1);
        }

        public BuildingSpot Segment(int index) =>
            new(First.Center + Step * index, First.Yaw, First.Footprint, First.Surface);

        /// <summary>The centre of the end face past the last segment, where a joined line turns.</summary>
        public float3 Joint => First.Center + Step * (Count - 0.5f);

        /// <summary>
        /// Where the first segment of a line joined on along <paramref name="direction"/> sits: on the line through the
        /// <see cref="Joint"/>, as near as it can be without overlapping the last segment, so it touches it and the
        /// corner stays closed.
        /// </summary>
        public float3 JoinedStart(float2 direction)
        {
            direction = math.normalizesafe(direction, math.normalize(Step.xz));
            var last = Segment(Count - 1).Box;
            var next = new BuildingSpot(Joint, Yaw(direction), First.Footprint, First.Surface).Box;
            var offset = Joint.xz - last.Center;
            // Slides the next box out along the direction until one side of either box separates them.
            var clear = math.min(math.min(Exit(last, next, offset, direction, last.Right),
                    Exit(last, next, offset, direction, last.Forward)),
                math.min(Exit(last, next, offset, direction, next.Right),
                    Exit(last, next, offset, direction, next.Forward)));
            var distance = math.max(First.Footprint.x * 0.5f, clear);
            var start = Joint.xz + direction * distance;
            return new float3(start.x, Joint.y, start.y);
        }

        /// <summary>The same segments as <paramref name="other"/>, heights aside since placement grounds each one.</summary>
        public bool Matches(in PlacementLine other)
        {
            var sameStart = First.Center.xz.Equals(other.First.Center.xz) && First.Yaw.Equals(other.First.Yaw);
            return sameStart && Step.Equals(other.Step) && Count == other.Count
                   && First.Footprint.Equals(other.First.Footprint);
        }

        /// <summary>The ground-plane side the segments of a line along <paramref name="direction"/> face.</summary>
        public static float2 Front(float2 direction)
        {
            var yaw = Yaw(math.normalizesafe(direction, new float2(1f, 0f)));
            return new float2(math.sin(yaw), math.cos(yaw));
        }

        /// <summary>Right (+X) along the line, with the front turned toward the camera as a placed building faces.</summary>
        private static float Yaw(float2 direction)
        {
            var yaw = math.atan2(-direction.y, direction.x);
            return direction.x > 0f ? yaw + math.PI : yaw;
        }

        /// <summary>
        /// How far along <paramref name="direction"/> <paramref name="moving"/>, starting <paramref name="offset"/>
        /// from <paramref name="still"/>, must slide before <paramref name="axis"/> separates them; infinite if never.
        /// </summary>
        private static float Exit(in NavBox still, in NavBox moving, float2 offset, float2 direction, float2 axis)
        {
            var reach = still.Reach(axis) + moving.Reach(axis);
            var rate = math.dot(direction, axis);
            var apart = math.dot(offset, axis);
            if (math.abs(rate) < 1e-6f)
            {
                return math.abs(apart) >= reach ? float.NegativeInfinity : float.PositiveInfinity;
            }

            return rate > 0f ? (reach - apart) / rate : (reach + apart) / -rate;
        }
    }
}
