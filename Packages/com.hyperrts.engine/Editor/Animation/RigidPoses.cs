using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Animation
{
    /// <summary>
    /// Every piece's position and rotation in the model's space at every row of a rigid motion: the texels of the
    /// position and rotation textures. A vertex moves as <c>pos[row] + rotate(rot[row], p - pos[0])</c>.
    /// </summary>
    internal sealed class RigidPoses
    {
        private readonly Vector3[,] _positions;
        private readonly Quaternion[,] _rotations;
        private readonly int _rows;
        private readonly int _width;

        private RigidPoses(int rows, int width)
        {
            _rows = rows;
            _width = width;
            _positions = new Vector3[rows, width];
            _rotations = new Quaternion[rows, width];
        }

        /// <summary>
        /// Reads the take's curves at each row's time. A piece's channel without a curve keeps its node's pose in the
        /// model. Each rotation keeps the sign nearest the row before, so rows blend the short way.
        /// </summary>
        public static RigidPoses Sample(GameObject motion, AnimationClip take, RigidMotion pieces, int rows)
        {
            var poses = new RigidPoses(rows, pieces.pieces.Count);
            var curves = Curves(take);
            foreach (var piece in pieces.pieces)
            {
                var node = motion.transform.Find(piece.node)
                    ?? throw new InvalidOperationException($"HyperRTS: {motion.name} has no node '{piece.node}' for column {piece.column}.");
                curves.TryGetValue(piece.node, out var channels);
                var before = Quaternion.identity;
                for (var row = 0; row < rows; row++)
                {
                    var time = row / take.frameRate;
                    poses._positions[row, piece.column] = new Vector3(
                        Read(channels, "m_LocalPosition.x", time, node.localPosition.x),
                        Read(channels, "m_LocalPosition.y", time, node.localPosition.y),
                        Read(channels, "m_LocalPosition.z", time, node.localPosition.z));
                    var rotation = Quaternion.Normalize(new Quaternion(
                        Read(channels, "m_LocalRotation.x", time, node.localRotation.x),
                        Read(channels, "m_LocalRotation.y", time, node.localRotation.y),
                        Read(channels, "m_LocalRotation.z", time, node.localRotation.z),
                        Read(channels, "m_LocalRotation.w", time, node.localRotation.w)));
                    if (Quaternion.Dot(rotation, before) < 0f)
                    {
                        rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    }

                    poses._rotations[row, piece.column] = rotation;
                    before = rotation;
                }
            }

            return poses;
        }

        /// <summary>Row per y, column per x: (position, 1).</summary>
        public Texture2D Positions() => Texture((row, column) =>
        {
            var p = _positions[row, column];
            return new Color(p.x, p.y, p.z, 1f);
        });

        /// <summary>Row per y, column per x: the rotation's (x, y, z, w).</summary>
        public Texture2D Rotations() => Texture((row, column) =>
        {
            var q = _rotations[row, column];
            return new Color(q.x, q.y, q.z, q.w);
        });

        /// <summary>
        /// Where the pieces reach over every row: each piece's position padded by its farthest vertex from where it
        /// rests, measured on the meshes of <paramref name="model"/> that carry a UV1 column.
        /// </summary>
        public Bounds Bounds(GameObject model)
        {
            var reach = Reach(model);
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            for (var row = 0; row < _rows; row++)
            {
                for (var column = 0; column < _width; column++)
                {
                    var pad = Vector3.one * reach[column];
                    min = Vector3.Min(min, _positions[row, column] - pad);
                    max = Vector3.Max(max, _positions[row, column] + pad);
                }
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private float[] Reach(GameObject model)
        {
            var reach = new float[_width];
            var vertices = new List<Vector3>();
            var columns = new List<Vector2>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.HasVertexAttribute(VertexAttribute.TexCoord1))
                {
                    continue;
                }

                var toModel = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                mesh.GetVertices(vertices);
                mesh.GetUVs(1, columns);
                for (var v = 0; v < vertices.Count; v++)
                {
                    var column = Mathf.Clamp((int)(columns[v].x * _width), 0, _width - 1);
                    var distance = Vector3.Distance(toModel.MultiplyPoint3x4(vertices[v]), _positions[0, column]);
                    reach[column] = Mathf.Max(reach[column], distance);
                }
            }

            return reach;
        }

        private Texture2D Texture(Func<int, int, Color> texel)
        {
            var texture = new Texture2D(_width, _rows, TextureFormat.RGBAFloat, false, true);
            var pixels = new Color[_width * _rows];
            for (var row = 0; row < _rows; row++)
            {
                for (var column = 0; column < _width; column++)
                {
                    pixels[row * _width + column] = texel(row, column);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Dictionary<string, Dictionary<string, AnimationCurve>> Curves(AnimationClip take)
        {
            var found = new Dictionary<string, Dictionary<string, AnimationCurve>>();
            foreach (var binding in AnimationUtility.GetCurveBindings(take))
            {
                if (binding.type != typeof(Transform))
                {
                    continue;
                }

                if (!found.TryGetValue(binding.path, out var channels))
                {
                    channels = new Dictionary<string, AnimationCurve>();
                    found[binding.path] = channels;
                }

                channels[binding.propertyName] = AnimationUtility.GetEditorCurve(take, binding);
            }

            return found;
        }

        private static float Read(Dictionary<string, AnimationCurve> channels, string property, float time, float rest) =>
            channels != null && channels.TryGetValue(property, out var curve) ? curve.Evaluate(time) : rest;
    }
}
