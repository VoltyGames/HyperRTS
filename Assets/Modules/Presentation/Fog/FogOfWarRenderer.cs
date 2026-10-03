using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>Draws the fog-of-war grid as a translucent plane over the map, re-uploading only when it changes.</summary>
    [AddComponentMenu(HyperRTSMenu.Vision + "Fog Of War Renderer")]
    [Icon(HyperRTSIcons.Vision)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class FogOfWarRenderer : MonoBehaviour
    {
        private static readonly int FogTexId = Shader.PropertyToID("_FogTex");

        [SerializeField]
        [Tooltip("Material using the HyperRTS/Fog Of War shader.")]
        private Material material;

        [SerializeField]
        [Tooltip("World height of the fog plane; keep it just above the ground.")]
        private float height = 0.05f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity over areas seen before but not in sight now.")]
        private float exploredOpacity = 0.5f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity over areas never seen.")]
        private float unexploredOpacity = 0.85f;

        private readonly MatchView _view = new();
        private readonly LiveQuery _fogQuery = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<FogOfWar>()));
        private MaterialPropertyBlock _properties;
        private Texture2D _texture;
        private Mesh _quad;
        private int _version = -1;

        private void LateUpdate()
        {
            if (material == null || !_view.Refresh() || !_view.HasMap || !_view.Map.FogOfWar)
            {
                return;
            }

            var query = _fogQuery.In(_view.EntityManager);
            if (!query.TryGetSingleton(out FogOfWar fog) || !fog.IsCreated)
            {
                return;
            }

            if (fog.Version != _version || _texture == null)
            {
                query.CompleteDependency();
                Upload(fog);
            }

            Draw(fog);
        }

        private void Upload(in FogOfWar fog)
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

            var team = _view.Relations.TeamOf(_view.Local.Faction);
            FogTexels.Fill(fog.Visible, fog.Explored, team, ToByte(exploredOpacity), ToByte(unexploredOpacity),
                _texture.GetPixelData<byte>(0));
            _texture.Apply(false);
            _version = fog.Version;
        }

        private void Draw(in FogOfWar fog)
        {
            _quad ??= OverlayMeshes.FlatQuad();
            _properties ??= new MaterialPropertyBlock();
            _properties.SetTexture(FogTexId, _texture);

            var size = new Vector3(fog.Size.x * fog.CellSize, 1f, fog.Size.y * fog.CellSize);
            var center = new Vector3(fog.Min.x + size.x * 0.5f, height, fog.Min.y + size.z * 0.5f);
            var parameters = new RenderParams(material)
            {
                matProps = _properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
            };
            Graphics.RenderMesh(parameters, _quad, 0, Matrix4x4.TRS(center, Quaternion.identity, size));
        }

        private static byte ToByte(float opacity) => (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * 255f);

        private void OnDestroy()
        {
            DestroyTexture();
            if (_quad != null)
            {
                Destroy(_quad);
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
