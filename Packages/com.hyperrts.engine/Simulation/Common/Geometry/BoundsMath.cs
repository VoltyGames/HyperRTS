using UnityEngine;

namespace HyperRTS.Simulation.Common.Geometry
{
    /// <summary>Box math for measuring meshes at authoring time.</summary>
    public static class BoundsMath
    {
        /// <summary>The box round <paramref name="box"/>'s corners moved by <paramref name="matrix"/>.</summary>
        public static Bounds Moved(Bounds box, Matrix4x4 matrix)
        {
            var moved = new Bounds(matrix.MultiplyPoint3x4(box.min), Vector3.zero);
            for (var corner = 1; corner < 8; corner++)
            {
                var sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                moved.Encapsulate(matrix.MultiplyPoint3x4(box.center + Vector3.Scale(box.extents, sign)));
            }

            return moved;
        }

        /// <summary>The farthest the box reaches from its origin on the ground plane, along x or z.</summary>
        public static float FlatReach(Bounds box) =>
            Mathf.Max(Mathf.Max(-box.min.x, box.max.x), Mathf.Max(-box.min.z, box.max.z));
    }
}
