using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// Farmland round the map out to <see cref="width"/>, so a camera tilted toward the horizon sees fields and belts
    /// fading into haze instead of the world's edge. Built when enabled, in edit mode too, and kept in memory only: a
    /// ground ring painted with the map terrain's layers in large fields, real belt trees near the terrain and a tree
    /// line past them. Nothing in it collides, navigates or casts a shadow but the near trees.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu(HyperRTSMenu.Environment + "Horizon")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class Horizon : PresentationBehaviour
    {
        private static readonly int AlbedoArrayId = Shader.PropertyToID("_AlbedoArray");
        private static readonly int LayerParamsId = Shader.PropertyToID("_LayerParams");

        [Tooltip("The map's terrain: the ring starts under its edge, and its material lends the ring its ground layers.")]
        public Terrain terrain;

        [Tooltip("Metres the horizon reaches past the terrain's edge.")]
        [Min(0f)]
        public float width = 2500f;

        [Tooltip("Seeds the fields, belts and swells.")]
        [Range(0, HorizonFields.MaxSeed)]
        public int seed;

        public HorizonRelief relief = new();

        public HorizonFields fields = new();

        public HorizonBelts belts = new();

        [Tooltip("HyperRTS/HorizonGround, which paints the fields.")]
        public Shader groundShader;

        [Tooltip("HyperRTS/HorizonTreeLine, which shades the far belts.")]
        public Shader treeLineShader;

        private Mesh _ground;
        private Material _groundMaterial;
        private HorizonTrees _trees;
        private bool _ready;
#if UNITY_EDITOR
        private bool _dirty;
#endif

        /// <summary>The terrain's world rectangle.</summary>
        public Rect TerrainRect
        {
            get
            {
                var (origin, size) = (terrain.transform.position, terrain.terrainData.size);
                return new Rect(origin.x, origin.z, size.x, size.z);
            }
        }

        /// <summary>The horizon's world rectangle, the terrain's grown by the width.</summary>
        public Rect Area
        {
            get
            {
                var rect = TerrainRect;
                return Rect.MinMaxRect(rect.xMin - width, rect.yMin - width, rect.xMax + width, rect.yMax + width);
            }
        }

        protected void OnEnable()
        {
            Build();
            RenderPipelineManager.beginCameraRendering += Render;
        }

        protected override void OnDisabled()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            Release();
        }

#if UNITY_EDITOR
        // Rebuilt on the next camera, not here: OnValidate runs mid-import.
        private void OnValidate() => _dirty = true;
#endif

        /// <summary>
        /// The land's world height at a point past the terrain: the terrain's edge height giving way to open ground
        /// over the relief's blend.
        /// </summary>
        public float Height(Vector2 point)
        {
            var rect = TerrainRect;
            var open = relief.Open(point, seed);
            var edge = EdgeHeight(point);
            return Mathf.Lerp(edge, open, Mathf.SmoothStep(0f, 1f, HorizonRing.Distance(rect, point) / relief.blend));
        }

        // The terrain's height at the point nearest inside it, a metre in from its edge.
        private float EdgeHeight(Vector2 point)
        {
            var rect = TerrainRect;
            var inside = Vector2.Max(rect.min + Vector2.one, Vector2.Min(rect.max - Vector2.one, point));
            return terrain.SampleHeight(new Vector3(inside.x, 0f, inside.y)) + terrain.transform.position.y;
        }

        private void Build()
        {
#if UNITY_EDITOR
            _dirty = false;
#endif
            var problems = Problems();
            if (problems.Count > 0)
            {
                Debug.LogError($"HyperRTS: Horizon can't draw: {string.Join("; ", problems)}.", this);
                return;
            }

            _ground = HorizonRing.Build(TerrainRect, width, EdgeHeight, Height);
            _groundMaterial = new Material(groundShader) { name = "Horizon Ground", hideFlags = HideFlags.DontSave };
            var template = terrain.materialTemplate;
            _groundMaterial.SetTexture(AlbedoArrayId, template.GetTexture(AlbedoArrayId));
            _groundMaterial.SetTexture(LayerParamsId, template.GetTexture(LayerParamsId));
            fields.Apply(_groundMaterial, seed);
            _trees = new HorizonTrees(belts, fields, seed, TerrainRect, Area, Height, treeLineShader);
            _ready = true;
        }

        private void Release()
        {
            _ready = false;
            _trees?.Dispose();
            _trees = null;
            CoreUtils.Destroy(_ground);
            CoreUtils.Destroy(_groundMaterial);
        }

        private List<string> Problems()
        {
            var problems = new List<string>();
            if (terrain == null || terrain.terrainData == null)
            {
                problems.Add("no terrain");
            }
            else if (terrain.materialTemplate == null || !terrain.materialTemplate.HasTexture(AlbedoArrayId))
            {
                problems.Add("the terrain's material has no layer arrays");
            }

            if (groundShader == null || treeLineShader == null)
            {
                problems.Add("a shader is missing");
            }

            if (fields.crops.Count is 0 or > HorizonFields.MaxCrops)
            {
                problems.Add($"{fields.crops.Count} crops, not 1 to {HorizonFields.MaxCrops}");
            }

            if (belts.trees.Exists(tree => tree.prefab == null))
            {
                problems.Add("a belt tree has no prefab");
            }

            if (belts.share > 0f && belts.trees.Count == 0)
            {
                problems.Add("belts without trees");
            }

            return problems;
        }

        private void Render(ScriptableRenderContext context, Camera camera)
        {
#if UNITY_EDITOR
            if (_dirty)
            {
                Release();
                Build();
            }
#endif

            if (!_ready || !SceneCameras.Draws(camera, gameObject))
            {
                return;
            }

            var parameters = new RenderParams(_groundMaterial)
            {
                camera = camera,
                layer = gameObject.layer,
                worldBounds = _ground.bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
            };
            Graphics.RenderMesh(parameters, _ground, 0, Matrix4x4.identity);
            _trees.Draw(camera, gameObject.layer);
        }
    }
}
