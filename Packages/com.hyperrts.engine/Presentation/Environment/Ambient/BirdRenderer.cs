using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>
    /// Draws birds as camera-facing quads, heading up the screen: one instanced call per flipbook frame, each picking
    /// its cell of the 2×2 sheet through <c>_BaseMap_ST</c>.
    /// </summary>
    public sealed class BirdRenderer : IDisposable
    {
        private const int Frames = 4;
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        private readonly Mesh _quad = CreateQuad();
        private readonly Matrix4x4[][] _matrices = new Matrix4x4[Frames][];
        private readonly int[] _counts = new int[Frames];
        private readonly MaterialPropertyBlock[] _blocks = new MaterialPropertyBlock[Frames];

        public BirdRenderer()
        {
            for (var frame = 0; frame < Frames; frame++)
            {
                _matrices[frame] = new Matrix4x4[16];
                _blocks[frame] = new MaterialPropertyBlock();
                _blocks[frame].SetVector(BaseMapST, FrameST(frame));
            }
        }

        /// <summary>Tiling and offset of a frame's cell: frames run left to right, top to bottom.</summary>
        public static Vector4 FrameST(int frame)
        {
            var column = frame & 1;
            var row = frame >> 1;
            return new Vector4(0.5f, 0.5f, column * 0.5f, 0.5f - row * 0.5f);
        }

        public void Draw(IReadOnlyList<BirdFlock> flocks, Material material, float size, Vector3 eye)
        {
            Array.Clear(_counts, 0, Frames);
            var bounds = new Bounds(flocks[0].Position(0), Vector3.zero);
            foreach (var flock in flocks)
            {
                for (var bird = 0; bird < flock.Count; bird++)
                {
                    Vector3 position = flock.Position(bird);
                    bounds.Encapsulate(position);
                    var facing = Quaternion.LookRotation(position - eye, flock.Velocity(bird));
                    Add(flock.Frame(bird), Matrix4x4.TRS(position, facing, Vector3.one * size));
                }
            }

            bounds.Expand(size * 2f);
            for (var frame = 0; frame < Frames; frame++)
            {
                DrawFrame(frame, material, bounds);
            }
        }

        public void Dispose() => CoreUtils.Destroy(_quad);

        private void Add(int frame, Matrix4x4 matrix)
        {
            ref var matrices = ref _matrices[frame];
            if (_counts[frame] == matrices.Length)
            {
                Array.Resize(ref matrices, matrices.Length * 2);
            }

            matrices[_counts[frame]++] = matrix;
        }

        private void DrawFrame(int frame, Material material, Bounds bounds)
        {
            if (_counts[frame] == 0)
            {
                return;
            }

            var parameters = new RenderParams(material)
            {
                matProps = _blocks[frame],
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMeshInstanced(parameters, _quad, 0, _matrices[frame], _counts[frame]);
        }

        /// <summary>A unit quad in XY facing -Z, so +Z (the look direction) points away from the camera.</summary>
        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "Bird", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 3, 1, 3, 0, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
