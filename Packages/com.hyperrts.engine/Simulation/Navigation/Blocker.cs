using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// A capsule ground units can't drive through, such as a wreck, closing the land <see cref="NavGrid"/> cells it
    /// covers while enabled.
    /// </summary>
    public struct Blocker : IComponentData, IEnableableComponent
    {
        public float Radius;

        /// <summary>Full length along the forward; up to the diameter keeps it round.</summary>
        public float HullLength;

        /// <summary>Half the capsule's core segment on XZ, along the entity's facing.</summary>
        public readonly float2 SegmentAxis(quaternion rotation) => NavAgent.SegmentAxis(rotation, Radius, HullLength);

        public readonly ulong Hash(in LocalTransform transform)
        {
            var hash = xxHash3.Hash64(transform) ^ xxHash3.Hash64(this).yx;
            return ((ulong)hash.x << 32) | hash.y;
        }

        /// <summary>Closes the land under the capsule but keeps water and decks, so ships still pass under a wreck on a bridge.</summary>
        public readonly void Stamp(ref NavGrid grid, in LocalTransform transform)
        {
            var center = transform.Position.xz;
            var axis = SegmentAxis(transform.Rotation);
            var reach = math.abs(axis) + Radius;
            grid.GetClampedArea(transform.Position, reach * 2f, out var min, out var max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    if (grid.CellGap(new int2(x, y), center - axis, center + axis) < Radius)
                    {
                        var index = grid.Index(new int2(x, y));
                        grid.Cells[index] = (byte)((NavSurface)grid.Cells[index] & ~NavSurface.Land);
                    }
                }
            }
        }
    }
}
