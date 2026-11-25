using HyperRTS.Editor.Maps.Meshes;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests.Meshes
{
    /// <summary>The ground mesh light transport bounces off: it covers the terrain, follows its height and faces up.</summary>
    public class MapGroundBounceMeshTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void Build_CoversTheTerrain_FollowsItsSlope_AndFacesUp()
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(200f, 50f, 120f) };
            var heights = new float[33, 33];
            for (var z = 0; z < 33; z++)
            {
                for (var x = 0; x < 33; x++)
                {
                    heights[z, x] = x / 32f * 0.5f;
                }
            }

            data.SetHeights(0, 0, heights);
            var mesh = MapGroundBounceMesh.Build(data);
            try
            {
                Assert.AreEqual(data.size.x, mesh.bounds.size.x, Tolerance);
                Assert.AreEqual(data.size.z, mesh.bounds.size.z, Tolerance);
                var (vertices, uvs, triangles) = (mesh.vertices, mesh.uv, mesh.triangles);
                Assert.AreEqual(Vector2.zero, uvs[0]);
                Assert.AreEqual(Vector2.one, uvs[^1]);
                for (var i = 0; i < vertices.Length; i++)
                {
                    var uv = uvs[i];
                    Assert.AreEqual(data.GetInterpolatedHeight(uv.x, uv.y), vertices[i].y, Tolerance);
                }

                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var (a, b, c) = (vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
                    Assert.Greater(Vector3.Cross(b - a, c - a).y, 0f);
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(data);
            }
        }
    }
}
