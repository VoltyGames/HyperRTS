using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// A dragged line of building segments along the nearer grid axis, X or Z, each one footprint long. The ghost and
    /// the authoritative command build the same line from the same start, end and footprint.
    /// </summary>
    public readonly struct PlacementLine
    {
        /// <summary>Most segments one line places, so a drag across the map stays cheap.</summary>
        public const int MaxSegments = 64;

        /// <summary>Snapped centre of the segment at the drag start.</summary>
        public readonly float3 First;

        /// <summary>Offset from one segment centre to the next.</summary>
        public readonly float3 Step;

        public readonly int Count;

        /// <summary>Each segment's footprint, X and Z swapped for a line along Z.</summary>
        public readonly float2 Footprint;

        public readonly bool AlongZ;

        /// <summary>Footprint X is a segment's length along the line.</summary>
        public PlacementLine(in MapSettings map, float3 start, float3 end, float2 footprint)
        {
            var delta = end - start;
            AlongZ = math.abs(delta.z) > math.abs(delta.x);
            Footprint = AlongZ ? footprint.yx : footprint;
            // Whole cells, so every segment centre stays on the grid.
            var length = math.max(1f, math.round(footprint.x / map.NavCellSize)) * map.NavCellSize;
            First = PlacementMath.Snap(map, start, Footprint);
            var along = AlongZ ? end.z - First.z : end.x - First.x;
            var stride = along < 0f ? -length : length;
            Step = AlongZ ? new float3(0f, 0f, stride) : new float3(stride, 0f, 0f);
            Count = math.min(MaxSegments, (int)math.round(math.abs(along) / length) + 1);
        }

        /// <summary>A quarter turn for a line along Z, so the prefab's length runs with the line.</summary>
        public quaternion Turn => AlongZ ? quaternion.RotateY(math.PI * 0.5f) : quaternion.identity;

        public float3 Segment(int index) => First + Step * index;
    }
}
