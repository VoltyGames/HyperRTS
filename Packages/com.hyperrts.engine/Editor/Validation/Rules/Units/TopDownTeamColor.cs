using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Squads;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>Measures how much of a model, seen from above, takes team colour through its _TeamMask.</summary>
    public sealed class TopDownTeamColor
    {
        private const string TeamColor = "_TeamColor";
        private const string TeamMask = "_TeamMask";
        private const int MaxMaskSize = 512;
        private const float TexelsPerSample = 8f;
        private const int MaxSplit = 16;

        private readonly Dictionary<Texture, Color32[]> _masks = new();
        private float _masked;
        private float _total;
        private bool _anyMask;

        /// <summary>Vehicles and aircraft: a unit that isn't a squad. Buildings and props have no UnitAuthoring.</summary>
        public static bool IsMobileVehicle(GameEntityAuthoring entity) =>
            entity is UnitAuthoring && !entity.TryGetComponent<SquadAuthoring>(out _);

        /// <summary>Every unit prefab in the project that is a mobile vehicle, with its top-down share.</summary>
        public static (string Name, float Share)[] UnitShares()
        {
            var measure = new TopDownTeamColor();
            return AssetDatabase.FindAssets("t:Prefab")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(prefab => prefab != null && prefab.TryGetComponent<UnitAuthoring>(out var unit) && IsMobileVehicle(unit))
                .Select(prefab => (prefab.name, Measured: measure.TryGetShare(prefab, out var share), share))
                .Where(row => row.Measured)
                .Select(row => (row.name, row.share))
                .OrderBy(pair => pair.name)
                .ToArray();
        }

        /// <summary>
        /// Masked share of the upward-facing area, projected on the ground, of the intact model's highest-detail
        /// meshes; false when no part carries a _TeamMask, as on a base prefab or a model still to be made.
        /// </summary>
        public bool TryGetShare(GameObject root, out float share)
        {
            _masked = 0f;
            _total = 0f;
            _anyMask = false;
            var hidden = LowerDetail(root);
            hidden.UnionWith(DamagedStages(root));
            foreach (var renderer in root.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !hidden.Contains(r)))
            {
                AddRenderer(renderer);
            }

            share = _total > 0f ? _masked / _total : 0f;
            return _anyMask;
        }

        private static HashSet<Renderer> LowerDetail(GameObject root)
        {
            var lower = new HashSet<Renderer>();
            foreach (var group in root.GetComponentsInChildren<LODGroup>())
            {
                var levels = group.GetLODs();
                for (var i = 1; i < levels.Length; i++)
                {
                    lower.UnionWith(levels[i].renderers.Where(r => r != null));
                }
            }

            return lower;
        }

        // A damage stage's model stands in for the intact one only once the unit is hurt.
        private static IEnumerable<Renderer> DamagedStages(GameObject root) =>
            root.GetComponentsInChildren<DamageStagesAuthoring>()
                .SelectMany(stages => stages.stages)
                .Where(stage => stage.below < 1f && stage.visual != null)
                .SelectMany(stage => stage.visual.GetComponentsInChildren<Renderer>(true));

        private static Mesh MeshOf(Renderer renderer) => renderer switch
        {
            SkinnedMeshRenderer skinned => skinned.sharedMesh,
            MeshRenderer => renderer.GetComponent<MeshFilter>()?.sharedMesh,
            _ => null,
        };

        private void AddRenderer(Renderer renderer)
        {
            var mesh = MeshOf(renderer);
            if (mesh == null)
            {
                return;
            }

            // Mesh data reads a mesh that isn't Read/Write enabled, which the importer leaves off.
            var array = Mesh.AcquireReadOnlyMeshData(mesh);
            try
            {
                var data = array[0];
                var materials = renderer.sharedMaterials;
                var toWorld = renderer.transform.localToWorldMatrix;
                using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
                using var uvs = new NativeArray<Vector2>(data.vertexCount, Allocator.Temp);
                data.GetVertices(vertices);
                var hasUvs = data.HasVertexAttribute(VertexAttribute.TexCoord0);
                if (hasUvs)
                {
                    data.GetUVs(0, uvs);
                }

                for (var sub = 0; sub < data.subMeshCount; sub++)
                {
                    var mask = sub < materials.Length && hasUvs ? MaskOf(materials[sub]) : null;
                    AddSubMesh(data, sub, vertices, uvs, toWorld, mask);
                }
            }
            finally
            {
                array.Dispose();
            }
        }

        private void AddSubMesh(Mesh.MeshData data, int sub, NativeArray<Vector3> vertices, NativeArray<Vector2> uvs,
            Matrix4x4 toWorld, Color32[] mask)
        {
            var descriptor = data.GetSubMesh(sub);
            if (descriptor.topology != MeshTopology.Triangles)
            {
                return;
            }

            using var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
            data.GetIndices(indices, sub);
            for (var i = 0; i + 2 < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                AddTriangle(
                    toWorld.MultiplyPoint3x4(vertices[a]), toWorld.MultiplyPoint3x4(vertices[b]), toWorld.MultiplyPoint3x4(vertices[c]),
                    (uvs[a], uvs[b], uvs[c]), mask);
            }
        }

        private Color32[] MaskOf(Material material)
        {
            if (material == null || !material.HasProperty(TeamColor) || !material.HasProperty(TeamMask))
            {
                return null;
            }

            var texture = material.GetTexture(TeamMask);
            if (texture == null)
            {
                return null;
            }

            if (!_masks.TryGetValue(texture, out var pixels))
            {
                var size = Mathf.Min(Mathf.Max(texture.width, texture.height), MaxMaskSize);
                var read = EditorTextures.Resample(texture, size, RenderTextureFormat.ARGB32, TextureFormat.RGBA32);
                pixels = read.GetPixels32();
                Object.DestroyImmediate(read);
                _masks[texture] = pixels;
            }

            return pixels;
        }

        private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, (Vector2 A, Vector2 B, Vector2 C) uv, Color32[] mask)
        {
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.y <= 0f)
            {
                return;
            }

            var area = 0.5f * normal.y;
            _total += area;
            if (mask == null)
            {
                return;
            }

            _anyMask = true;
            var size = Mathf.RoundToInt(Mathf.Sqrt(mask.Length));
            var texels = Mathf.Max((uv.B - uv.A).magnitude, (uv.C - uv.A).magnitude, (uv.C - uv.B).magnitude) * size;
            var split = Mathf.Clamp(Mathf.CeilToInt(texels / TexelsPerSample), 1, MaxSplit);
            var sum = 0f;
            var count = 0;
            for (var i = 0; i < split; i++)
            {
                for (var j = 0; i + j < split; j++)
                {
                    sum += Sample(mask, size, uv, (i + 1f / 3f) / split, (j + 1f / 3f) / split);
                    count++;
                    if (i + j < split - 1)
                    {
                        sum += Sample(mask, size, uv, (i + 2f / 3f) / split, (j + 2f / 3f) / split);
                        count++;
                    }
                }
            }

            _masked += area * sum / count;
        }

        private static float Sample(Color32[] mask, int size, (Vector2 A, Vector2 B, Vector2 C) uv, float u, float v)
        {
            var point = uv.A + u * (uv.B - uv.A) + v * (uv.C - uv.A);
            var x = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(point.x, 1f) * size), 0, size - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(point.y, 1f) * size), 0, size - 1);
            return mask[y * size + x].r / 255f;
        }
    }
}
