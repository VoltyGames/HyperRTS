using System.Collections.Generic;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.PlayMode.DebugLayers
{
    /// <summary>
    /// What one debug layer draws: lines and one grid of cells draped on the ground, both shown over everything so
    /// crops, trees and units never hide them. Hidden mesh objects draw them, so they show in the Game view as well as
    /// the Scene view.
    /// </summary>
    public sealed class DebugShapes
    {
        private const float DashLength = 1f;
        private const int CircleSegments = 24;
        private const float CellLift = 0.2f;

        // Caps the draped mesh near 256 by 256 vertices on any grid.
        private const int MaxDrapeQuads = 255;

        private readonly List<Vector3> _lineVertices = new();
        private readonly List<Color32> _lineColors = new();
        private readonly List<int> _lineIndices = new();

        private GameObject _root;
        private GameObject _cells;
        private Mesh _lineMesh;
        private Mesh _cellMesh;
        private Material _cellMaterial;
        private Texture2D _cellTexture;
        private CellGrid _cellGrid;
        private bool _hasCells;
        private CellGrid _drapedGrid;
        private BlobAssetReference<HeightfieldBlob> _drapedGround;

        /// <summary>Whether the last refresh drew anything.</summary>
        public bool IsEmpty => _lineVertices.Count == 0 && !_hasCells;

        /// <summary>Blue to red as <paramref name="t"/> runs from 0 to 1, see-through.</summary>
        public static Color32 Heat(float t)
        {
            t = math.saturate(t);
            return new Color32((byte)(255f * t), (byte)(255f * (1f - math.abs(2f * t - 1f))), (byte)(255f * (1f - t)), 110);
        }

        /// <summary>A stable colour for an id, with 0 left clear.</summary>
        public static Color32 Hashed(int id)
        {
            if (id == 0)
            {
                return default;
            }

            var bits = math.hash(new int2(id, 7));
            return new Color32((byte)(bits & 255), (byte)((bits >> 8) & 255), (byte)((bits >> 16) & 255), 110);
        }

        public void Line(float3 from, float3 to, Color32 color)
        {
            _lineIndices.Add(_lineVertices.Count);
            _lineVertices.Add(from);
            _lineIndices.Add(_lineVertices.Count);
            _lineVertices.Add(to);
            _lineColors.Add(color);
            _lineColors.Add(color);
        }

        public void DashedLine(float3 from, float3 to, Color32 color)
        {
            var length = math.distance(from, to);
            var dashes = (int)(length / (2f * DashLength));
            if (dashes == 0)
            {
                Line(from, to, color);
                return;
            }

            for (var i = 0; i < dashes; i++)
            {
                var start = (2f * i) / (2f * dashes);
                var end = (2f * i + 1f) / (2f * dashes);
                Line(math.lerp(from, to, start), math.lerp(from, to, end), color);
            }
        }

        /// <summary>A flat ring round <paramref name="center"/>.</summary>
        public void Circle(float3 center, float radius, Color32 color)
        {
            var previous = center + new float3(radius, 0f, 0f);
            for (var i = 1; i <= CircleSegments; i++)
            {
                math.sincos(2f * math.PI * i / CircleSegments, out var sin, out var cos);
                var next = center + new float3(cos * radius, 0f, sin * radius);
                Line(previous, next, color);
                previous = next;
            }
        }

        /// <summary>Colours every cell of <paramref name="grid"/> from <paramref name="colors"/> with a Burst job; one grid per layer.</summary>
        public void Cells<T>(in CellGrid grid, in T colors) where T : struct, ICellColors
        {
            PrepareTexture(grid.Size);
            var job = new PaintJob<T> { Colors = colors, Pixels = _cellTexture.GetRawTextureData<Color32>() };
            job.Run(grid.Count);
            _cellTexture.Apply(false);
            _cellGrid = grid;
            _hasCells = true;
        }

        internal void Clear()
        {
            _lineVertices.Clear();
            _lineColors.Clear();
            _lineIndices.Clear();
            _hasCells = false;
        }

        /// <summary>Uploads this refresh's shapes, draping the cells on <paramref name="ground"/>.</summary>
        internal void Show(string name, in TerrainHeight ground)
        {
            Prepare(name);
            _lineMesh.Clear();
            _lineMesh.SetVertices(_lineVertices);
            _lineMesh.SetColors(_lineColors);
            _lineMesh.SetIndices(_lineIndices, MeshTopology.Lines, 0);

            if (_hasCells && (!SameGrid(_cellGrid, _drapedGrid) || !_drapedGround.Equals(ground.Blob)))
            {
                Drape(_cellGrid, ground);
            }

            _cells.SetActive(_hasCells);
            _root.SetActive(true);
        }

        internal void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        internal void Dispose()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_lineMesh);
            Object.DestroyImmediate(_cellMesh);
            Object.DestroyImmediate(_cellMaterial);
            Object.DestroyImmediate(_cellTexture);
            _drapedGrid = default;
            _drapedGround = default;
        }

        private void Prepare(string name)
        {
            if (_root != null)
            {
                return;
            }

            var shader = Shader.Find("Hidden/HyperRTS/DebugShapes");
            _root = new GameObject($"Debug layer: {name}") { hideFlags = HideFlags.HideAndDontSave };
            _lineMesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            var lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            AddRenderer("Lines", _lineMesh, lineMaterial);

            _cellMesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            // Lines draw after cells, so paths stay readable over a grid.
            _cellMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = (int)RenderQueue.Overlay - 1 };
            if (_cellTexture != null)
            {
                _cellMaterial.mainTexture = _cellTexture;
            }

            _cells = AddRenderer("Cells", _cellMesh, _cellMaterial);
        }

        private static bool SameGrid(in CellGrid a, in CellGrid b) =>
            math.all(a.Min == b.Min) && a.CellSize == b.CellSize && math.all(a.Size == b.Size);

        private GameObject AddRenderer(string name, Mesh mesh, Material material)
        {
            var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            child.transform.SetParent(_root.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return child;
        }

        private void PrepareTexture(int2 size)
        {
            if (_cellTexture != null && _cellTexture.width == size.x && _cellTexture.height == size.y)
            {
                return;
            }

            Object.DestroyImmediate(_cellTexture);
            _cellTexture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (_cellMaterial != null)
            {
                _cellMaterial.mainTexture = _cellTexture;
            }
        }

        // A coarse grid of vertices on the ground heights; the texture keeps every cell sharp between them.
        private void Drape(in CellGrid grid, in TerrainHeight ground)
        {
            var step = math.max(1, (int)math.ceil(math.cmax(grid.Size) / (float)MaxDrapeQuads));
            var quads = (grid.Size + step - 1) / step;
            var columns = quads.x + 1;
            var vertices = new Vector3[columns * (quads.y + 1)];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            for (var y = 0; y <= quads.y; y++)
            {
                for (var x = 0; x <= quads.x; x++)
                {
                    var cell = math.min(new int2(x, y) * step, grid.Size);
                    var position = grid.Corner(cell);
                    vertices[y * columns + x] = new Vector3(position.x, ground.Height(position) + CellLift, position.y);
                    uvs[y * columns + x] = (float2)cell / grid.Size;
                    colors[y * columns + x] = new Color32(255, 255, 255, 255);
                }
            }

            var indices = TriangleIndices(quads, columns);

            _cellMesh.Clear();
            _cellMesh.vertices = vertices;
            _cellMesh.uv = uvs;
            _cellMesh.colors32 = colors;
            _cellMesh.triangles = indices;
            _cellMesh.RecalculateBounds();
            _drapedGrid = grid;
            _drapedGround = ground.Blob;
        }

        private static int[] TriangleIndices(int2 quads, int columns)
        {
            var indices = new int[quads.x * quads.y * 6];
            var next = 0;
            for (var y = 0; y < quads.y; y++)
            {
                for (var x = 0; x < quads.x; x++)
                {
                    var corner = y * columns + x;
                    indices[next++] = corner;
                    indices[next++] = corner + columns;
                    indices[next++] = corner + columns + 1;
                    indices[next++] = corner;
                    indices[next++] = corner + columns + 1;
                    indices[next++] = corner + 1;
                }
            }

            return indices;
        }

        // Run on the main thread: the colours read whole arrays, which a parallel job's index range would forbid.
        [BurstCompile]
        private struct PaintJob<T> : IJobFor where T : struct, ICellColors
        {
            public T Colors;
            public NativeArray<Color32> Pixels;

            public void Execute(int index) => Pixels[index] = Colors.At(index);
        }
    }
}
