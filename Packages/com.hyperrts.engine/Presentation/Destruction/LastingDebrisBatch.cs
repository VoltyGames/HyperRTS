using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// The lasting debris pieces sharing one mesh and material: its registered IDs, the mesh's bounds and how many
    /// pieces it draws.
    /// </summary>
    public struct LastingDebrisBatch
    {
        public BatchMeshID MeshID;
        public BatchMaterialID MaterialID;
        public Bounds MeshBounds;
        public int Count;

        /// <summary>The world bounding sphere, radius in w, of its mesh drawn at <paramref name="matrix"/>.</summary>
        public readonly float4 Sphere(float4x4 matrix)
        {
            var scale = new float3(math.length(matrix.c0.xyz), math.length(matrix.c1.xyz), math.length(matrix.c2.xyz));
            var radius = math.length((float3)MeshBounds.extents * scale);
            return new float4(math.transform(matrix, MeshBounds.center), radius);
        }
    }
}
