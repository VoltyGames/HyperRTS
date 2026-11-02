using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// The horizon's belts on screen: within the belts' trees width each tree variant's last LOD, instanced in chunks
    /// so the camera culls what it can't see; past it one tree line mesh, a submesh per variant coloured by its leaves.
    /// </summary>
    public sealed class HorizonTrees : IDisposable
    {
        private const float ChunkSize = 500f;
        private const int MaxInstances = 1023;

        /// <summary>Texels across the copy of a leaf atlas its colour is read from.</summary>
        private const int AtlasSample = 128;

        /// <summary>Metres a tree or the tree line sinks, as the ring's facets lie a little off the land between loops.</summary>
        private const float Sink = 0.4f;

        /// <summary>Tree spacings between the tree line's columns.</summary>
        private const float ColumnSpacings = 2f;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly List<Variant> _variants = new();
        private readonly List<Chunk> _chunks = new();
        private readonly Dictionary<Material, Material> _instanced = new();
        private readonly List<Material> _lineMaterials = new();
        private readonly float[] _weights;
        private readonly Mesh _line;

        /// <summary>Builds the belts inside <paramref name="area"/>, standing on <paramref name="height"/>.</summary>
        public HorizonTrees(HorizonBelts belts, HorizonFields fields, int seed, Rect terrain, Rect area,
            Func<Vector2, float> height, Shader lineShader)
        {
            _weights = belts.trees.Select(tree => tree.weight).ToArray();
            var crowns = new Dictionary<Texture, Color>();
            foreach (var tree in belts.trees)
            {
                var variant = new Variant(tree, _instanced);
                _variants.Add(variant);
                var line = new Material(lineShader) { name = $"{tree.prefab.name} (Tree Line)", hideFlags = HideFlags.DontSave };
                line.SetColor(BaseColorId, CrownColor(variant.Leaves, crowns));
                _lineMaterials.Add(line);
            }

            var lines = new HorizonTreeLine(_variants.Count);
            var instances = new Dictionary<(int Variant, Vector2Int Chunk), List<Matrix4x4>>();
            foreach (var belt in belts.Belts(fields, seed, area))
            {
                Plant(belt, belts, terrain, height, instances);
                LineUp(belt, belts, terrain, area, height, lines);
            }

            foreach (var ((index, _), matrices) in instances)
            {
                for (var start = 0; start < matrices.Count; start += MaxInstances)
                {
                    var batch = matrices.GetRange(start, Mathf.Min(MaxInstances, matrices.Count - start)).ToArray();
                    _chunks.Add(new Chunk(_variants[index], batch));
                }
            }

            _line = lines.Build();
        }

        public void Draw(Camera camera, int layer)
        {
            foreach (var chunk in _chunks)
            {
                for (var submesh = 0; submesh < chunk.Variant.Mesh.subMeshCount; submesh++)
                {
                    var parameters = new RenderParams(chunk.Variant.Materials[submesh])
                    {
                        camera = camera,
                        layer = layer,
                        worldBounds = chunk.Bounds,
                        shadowCastingMode = ShadowCastingMode.On,
                        receiveShadows = true,
                    };
                    Graphics.RenderMeshInstanced(parameters, chunk.Variant.Mesh, submesh, chunk.Matrices);
                }
            }

            for (var submesh = 0; submesh < _line.subMeshCount; submesh++)
            {
                if (_line.GetSubMesh(submesh).indexCount == 0)
                {
                    continue;
                }

                var parameters = new RenderParams(_lineMaterials[submesh])
                {
                    camera = camera,
                    layer = layer,
                    worldBounds = _line.bounds,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = true,
                };
                Graphics.RenderMesh(parameters, _line, submesh, Matrix4x4.identity);
            }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_line);
            _lineMaterials.ForEach(CoreUtils.Destroy);
            foreach (var copy in _instanced.Values)
            {
                CoreUtils.Destroy(copy);
            }
        }

        // Rows of trees along the belt, odd rows staggered half a spacing, each tree jittered and picked by its hash.
        private void Plant(HorizonBelt belt, HorizonBelts belts, Rect terrain, Func<Vector2, float> height,
            Dictionary<(int Variant, Vector2Int Chunk), List<Matrix4x4>> instances)
        {
            var run = belt.To - belt.From;
            var (along, across) = (run.normalized, new Vector2(-run.normalized.y, run.normalized.x));
            var count = Mathf.FloorToInt(run.magnitude / belts.spacing);
            for (var row = 0; row < belts.rows; row++)
            {
                var offset = (row - (belts.rows - 1) * 0.5f) * belts.width / belts.rows;
                for (var i = 0; i < count; i++)
                {
                    var hash = ScatterHash.Hash(unchecked((int)belt.Hash), row, i);
                    var jitter = (Hashes.Fraction(ScatterHash.Channel(hash, 0)) - 0.5f) * 0.5f * belts.spacing;
                    var spot = belt.From + along * ((i + 0.5f + (row & 1) * 0.5f) * belts.spacing + jitter) + across * offset;
                    var distance = HorizonRing.Distance(terrain, spot);
                    if (distance <= 0f || distance >= belts.treesWidth)
                    {
                        continue;
                    }

                    var variant = Hashes.Pick(ScatterHash.Channel(hash, 1), _weights);
                    var yaw = Quaternion.Euler(0f, Hashes.Fraction(ScatterHash.Channel(hash, 2)) * 360f, 0f);
                    var scale = Mathf.Lerp(0.85f, 1.15f, Hashes.Fraction(ScatterHash.Channel(hash, 3)));
                    var position = new Vector3(spot.x, height(spot) - Sink, spot.y);
                    var key = (variant, Vector2Int.FloorToInt(spot / ChunkSize));
                    if (!instances.TryGetValue(key, out var matrices))
                    {
                        instances.Add(key, matrices = new List<Matrix4x4>());
                    }

                    matrices.Add(Matrix4x4.TRS(position, yaw, Vector3.one * scale) * _variants[variant].Local);
                }
            }
        }

        // Columns along the belt's middle every few tree spacings; each run of columns past the trees is one length of
        // tree line in the variant the belt's hash picks.
        private void LineUp(HorizonBelt belt, HorizonBelts belts, Rect terrain, Rect area, Func<Vector2, float> height,
            HorizonTreeLine lines)
        {
            var run = belt.To - belt.From;
            var across = new Vector2(-run.normalized.y, run.normalized.x);
            var steps = Mathf.Max(1, Mathf.CeilToInt(run.magnitude / (belts.spacing * ColumnSpacings)));
            var variant = Hashes.Pick(ScatterHash.Channel(belt.Hash, 7), _weights);
            for (var i = 0; i <= steps; i++)
            {
                var spot = Vector2.Lerp(belt.From, belt.To, i / (float)steps);
                var distance = HorizonRing.Distance(terrain, spot);
                if (distance < belts.treesWidth || !area.Contains(spot))
                {
                    lines.EndRun();
                    continue;
                }

                var hash = ScatterHash.Hash(unchecked((int)belt.Hash), 99, i);
                var tall = _variants[variant].Height * Mathf.Lerp(0.8f, 1.15f, Hashes.Fraction(hash));
                var shoulder = Mathf.Lerp(0.6f, 0.8f, Hashes.Fraction(ScatterHash.Channel(hash, 1)));
                lines.Column(variant, new Vector3(spot.x, height(spot) - Sink, spot.y), across, belts.width * 0.5f, tall,
                    shoulder);
            }

            lines.EndRun();
        }

        // The leaves' colour seen from afar: the atlas's texels weighted by their coverage, as its gaps carry no leaf
        // colour, times the material's tint. Read back once per atlas from a small copy.
        private static Color CrownColor(Material leaves, Dictionary<Texture, Color> crowns)
        {
            var atlas = leaves.GetTexture(BaseMapId);
            if (!crowns.TryGetValue(atlas, out var mean))
            {
                var small = RenderTexture.GetTemporary(AtlasSample, AtlasSample, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                Graphics.Blit(atlas, small);
                var previous = RenderTexture.active;
                RenderTexture.active = small;
                var copy = new Texture2D(AtlasSample, AtlasSample, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, AtlasSample, AtlasSample), 0, 0);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(small);
                var (sum, cover) = (Vector3.zero, 0f);
                foreach (var texel in copy.GetPixels())
                {
                    var linear = texel.linear;
                    sum += new Vector3(linear.r, linear.g, linear.b) * texel.a;
                    cover += texel.a;
                }

                CoreUtils.Destroy(copy);
                sum /= Mathf.Max(cover, 1e-4f);
                mean = new Color(sum.x, sum.y, sum.z);
                crowns.Add(atlas, mean);
            }

            var tint = leaves.GetColor(BaseColorId).linear;
            return new Color(mean.r * tint.r, mean.g * tint.g, mean.b * tint.b).gamma;
        }

        /// <summary>A tree variant's last LOD: its mesh, instancing copies of its materials, its leaves and height.</summary>
        private sealed class Variant
        {
            public Variant(HorizonTree tree, Dictionary<Material, Material> instanced)
            {
                var renderer = tree.LastLod();
                Mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Local = tree.prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                Materials = renderer.sharedMaterials.Select(material => Instanced(material, instanced)).ToArray();
                Leaves = tree.Leaves();
                Height = Top(Mesh.bounds, Local);
            }

            public Mesh Mesh { get; }

            public Matrix4x4 Local { get; }

            public Material[] Materials { get; }

            public Material Leaves { get; }

            public float Height { get; }

            // The highest corner of the mesh's bounds in the prefab, whatever turn the model was imported with.
            private static float Top(Bounds bounds, Matrix4x4 local)
            {
                var top = float.MinValue;
                for (var corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3((corner & 1) * 2 - 1, (corner >> 1 & 1) * 2 - 1, (corner >> 2) * 2 - 1);
                    top = Mathf.Max(top, local.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, sign)).y);
                }

                return top;
            }

            private static Material Instanced(Material material, Dictionary<Material, Material> instanced)
            {
                if (!instanced.TryGetValue(material, out var copy))
                {
                    copy = new Material(material) { enableInstancing = true, hideFlags = HideFlags.DontSave };
                    instanced.Add(material, copy);
                }

                return copy;
            }
        }

        private sealed class Chunk
        {
            public Chunk(Variant variant, Matrix4x4[] matrices)
            {
                Variant = variant;
                Matrices = matrices;
                var bounds = new Bounds(matrices[0].GetPosition(), Vector3.zero);
                foreach (var matrix in matrices)
                {
                    bounds.Encapsulate(matrix.GetPosition());
                }

                bounds.Expand(new Vector3(variant.Height, variant.Height * 2f, variant.Height));
                Bounds = bounds;
            }

            public Variant Variant { get; }

            public Matrix4x4[] Matrices { get; }

            public Bounds Bounds { get; }
        }
    }
}
