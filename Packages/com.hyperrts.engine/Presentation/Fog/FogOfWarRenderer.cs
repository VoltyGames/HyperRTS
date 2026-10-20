using HyperRTS.Client.Common;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>
    /// Draws the fog-of-war grid over everything in view (terrain, trees, buildings) as a box around the map that
    /// the shader projects onto the depth buffer, re-uploading the grid only when it changes.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Rig + "Fog of War Renderer")]
    [Icon(HyperRTSIcons.Vision)]
    [HelpURL(HyperRTSDocs.Vision)]
    [DisallowMultipleComponent]
    public class FogOfWarRenderer : PresentationBehaviour
    {
        private static readonly int FogTexId = Shader.PropertyToID("_FogTex");
        private static readonly int FogRectId = Shader.PropertyToID("_FogRect");

        [SerializeField]
        [Tooltip("Material using the HyperRTS/Fog Of War shader.")]
        private Material material;

        [SerializeField]
        [Tooltip("Lowest and highest ground the fog box covers (world Y).")]
        private Vector2 heightRange = new(-50f, 250f);

        [SerializeField]
        [Tooltip("How far past the map the box reaches, dimming scenery beyond the playable edge (metres).")]
        private float borderReach = 300f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity over areas seen before but not in sight now.")]
        private float exploredOpacity = 0.5f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity over areas never seen.")]
        private float unexploredOpacity = 0.85f;

        private readonly LiveQuery _fogQuery = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<FogOfWar>()));
        private readonly LiveQuery _localView = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalFogView>()));
        private MaterialPropertyBlock _properties;
        private Texture2D _texture;
        private Mesh _box;
        private int _version = -1;

        private void LateUpdate()
        {
            if (material == null || !MatchView.TryGetDefault(out var match))
            {
                return;
            }

            var entityManager = match.EntityManager;
            var hasView = _localView.In(entityManager).TryGetSingleton(out LocalFogView view);
            if (!hasView || !view.Active)
            {
                return;
            }

            var query = _fogQuery.In(entityManager);
            if (!query.TryGetSingleton(out FogOfWar fog))
            {
                return;
            }

            if (view.Version != _version || _texture == null)
            {
                query.CompleteDependency();
                Upload(fog, view);
            }

            Draw(fog);
        }

        private void Upload(in FogOfWar fog, in LocalFogView view)
        {
            if (_texture == null || _texture.width != fog.Size.x || _texture.height != fog.Size.y)
            {
                DestroyTexture();
                _texture = new Texture2D(fog.Size.x, fog.Size.y, TextureFormat.R8, false, true)
                {
                    name = "Fog Of War",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
            }

            FogTexels.Fill(fog.Visible, fog.Explored, view.Team, ToByte(exploredOpacity), ToByte(unexploredOpacity),
                _texture.GetPixelData<byte>(0));
            _texture.Apply(false);
            _version = view.Version;
        }

        private void Draw(in FogOfWar fog)
        {
            // Unity null check, not ??=: a recompile in Play mode leaves a fake-null mesh that ??= keeps.
            if (_box == null)
            {
                _box = OverlayMeshes.Box();
            }

            var area = new Vector2(fog.Size.x * fog.CellSize, fog.Size.y * fog.CellSize);
            _properties ??= new MaterialPropertyBlock();
            _properties.SetTexture(FogTexId, _texture);
            _properties.SetVector(FogRectId, new Vector4(fog.Min.x, fog.Min.y, area.x, area.y));

            var size = new Vector3(area.x + borderReach * 2f, heightRange.y - heightRange.x, area.y + borderReach * 2f);
            var bottom = new Vector3(fog.Min.x + area.x * 0.5f, heightRange.x, fog.Min.y + area.y * 0.5f);
            var parameters = new RenderParams(material)
            {
                matProps = _properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
            };
            Graphics.RenderMesh(parameters, _box, 0, Matrix4x4.TRS(bottom, Quaternion.identity, size));
        }

        private static byte ToByte(float opacity) => (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * 255f);

        protected override void OnDestroyed()
        {
            DestroyTexture();
            if (_box != null)
            {
                Destroy(_box);
            }
        }

        private void DestroyTexture()
        {
            if (_texture != null)
            {
                Destroy(_texture);
            }
        }
    }
}
