using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>
    /// A grid over the whole terrain in its local space, for light transport to bounce off in place of the terrain.
    /// UV0 spans the terrain, which is how the terrain shader's meta pass finds the ground colour.
    /// </summary>
    public static class MapGroundBounceMesh
    {
        private const float VertexSpacing = 2f;
        private const int MinResolution = 65;
        private const int MaxResolution = 513;

        public static Mesh Build(TerrainData data)
        {
            var size = data.size;
            var resolution = math.clamp((int)(math.max(size.x, size.z) / VertexSpacing) + 1, MinResolution, MaxResolution);
            var vertices = new Vector3[resolution * resolution];
            var uvs = new Vector2[vertices.Length];
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var (u, v) = (x / (float)(resolution - 1), z / (float)(resolution - 1));
                    vertices[z * resolution + x] = new Vector3(u * size.x, data.GetInterpolatedHeight(u, v), v * size.z);
                    uvs[z * resolution + x] = new Vector2(u, v);
                }
            }

            var mesh = new Mesh { name = "Ground Bounce", indexFormat = IndexFormat.UInt32, vertices = vertices, uv = uvs };
            mesh.triangles = Triangles(resolution);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int[] Triangles(int resolution)
        {
            var cells = resolution - 1;
            var triangles = new int[cells * cells * 6];
            var next = 0;
            for (var z = 0; z < cells; z++)
            {
                for (var x = 0; x < cells; x++)
                {
                    var i = z * resolution + x;
                    triangles[next++] = i;
                    triangles[next++] = i + resolution;
                    triangles[next++] = i + 1;
                    triangles[next++] = i + 1;
                    triangles[next++] = i + resolution;
                    triangles[next++] = i + resolution + 1;
                }
            }

            return triangles;
        }
    }
}
