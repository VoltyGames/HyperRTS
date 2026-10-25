using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// A dragged line of building segments in any direction, each one footprint long and yawed along the line. The
    /// ghost and the authoritative command build the same line from the same start, end and footprint.
    /// </summary>
    public readonly struct PlacementLine
    {
        /// <summary>Most segments one line places, so a drag across the map stays cheap.</summary>
        public const int MaxSegments = 64;

        /// <summary>The segment at the drag start, the only one snapped, so the rest follow gap-free.</summary>
        public readonly BuildingSpot First;

        /// <summary>Offset from one segment centre to the next.</summary>
        public readonly float3 Step;

        public readonly int Count;

        /// <summary>Footprint X is a segment's length along the line.</summary>
        public PlacementLine(in MapSettings map, float3 start, float3 end, float2 footprint, PlacementSurface surface)
        {
            // A click without a drag lays one segment along X.
            var direction = math.normalizesafe(end.xz - start.xz, new float2(1f, 0f));
            First = new BuildingSpot(start, Yaw(direction), footprint, surface).Snap(map);
            var right = First.Box.Right;
            var along = math.dot(right, direction) < 0f ? -right : right;
            Step = new float3(along.x, 0f, along.y) * footprint.x;
            var reach = math.max(0f, math.dot(end.xz - First.Center.xz, along));
            Count = math.min(MaxSegments, (int)math.round(reach / footprint.x) + 1);
        }

        public BuildingSpot Segment(int index) =>
            new(First.Center + Step * index, First.Yaw, First.Footprint, First.Surface);

        /// <summary>The same segments as <paramref name="other"/>, heights aside since placement grounds each one.</summary>
        public bool Matches(in PlacementLine other)
        {
            var sameStart = First.Center.xz.Equals(other.First.Center.xz) && First.Yaw.Equals(other.First.Yaw);
            return sameStart && Step.Equals(other.Step) && Count == other.Count
                   && First.Footprint.Equals(other.First.Footprint);
        }

        /// <summary>Right (+X) along the line, with the front turned toward the camera as a placed building faces.</summary>
        private static float Yaw(float2 direction)
        {
            var yaw = math.atan2(-direction.y, direction.x);
            return direction.x > 0f ? yaw + math.PI : yaw;
        }
    }
}
