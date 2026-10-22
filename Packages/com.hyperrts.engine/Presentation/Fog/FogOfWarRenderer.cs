using HyperRTS.Client.Common;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using Unity.Scripting.LifecycleManagement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>
    /// Publishes the fog-of-war grid and its look as shader globals for <see cref="FogOfWarFeature"/> and the
    /// transparents that fog themselves, re-uploading the grid only when it changes.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Rig + "Fog of War Renderer")]
    [Icon(HyperRTSIcons.Vision)]
    [HelpURL(HyperRTSDocs.Vision)]
    [DisallowMultipleComponent]
    public partial class FogOfWarRenderer : PresentationBehaviour
    {
        private static readonly int TextureId = Shader.PropertyToID("_HyperRTSFogOfWar");
        private static readonly int RectId = Shader.PropertyToID("_HyperRTSFogOfWarRect");
        private static readonly int ColorId = Shader.PropertyToID("_HyperRTSFogOfWarColor");
        private static readonly int BorderId = Shader.PropertyToID("_HyperRTSFogOfWarBorder");

        [SerializeField]
        [Tooltip("Fog colour; alpha scales every opacity below.")]
        private Color color = new(0.02f, 0.03f, 0.05f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity over scenery beyond the playable edge.")]
        private float borderOpacity = 0.65f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Metres past the edge the border opacity fades in over.")]
        private float borderFade = 6f;

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
        private Texture2D _texture;
        private int _version = -1;

        /// <summary>Whether a fog of war is published, so the full-screen pass has something to draw.</summary>
        public static bool Shown => Shader.GetGlobalVector(ColorId).w > 0f;

        private void LateUpdate()
        {
            if (!TryReadFog(out var entityManager, out var fog, out var view))
            {
                Hide();
                return;
            }

            if (view.Version != _version || _texture == null)
            {
                _fogQuery.In(entityManager).CompleteDependency();
                Upload(fog, view);
            }

            Publish(fog);
        }

        private bool TryReadFog(out EntityManager entityManager, out FogOfWar fog, out LocalFogView view)
        {
            entityManager = default;
            fog = default;
            view = default;
            if (!MatchView.TryGetDefault(out var match))
            {
                return false;
            }

            entityManager = match.EntityManager;
            var hasView = _localView.In(entityManager).TryGetSingleton(out view);
            if (!hasView || !view.Active)
            {
                return false;
            }

            return _fogQuery.In(entityManager).TryGetSingleton(out fog);
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

        private void Publish(in FogOfWar fog)
        {
            var area = new Vector2(fog.Size.x * fog.CellSize, fog.Size.y * fog.CellSize);
            Shader.SetGlobalTexture(TextureId, _texture);
            Shader.SetGlobalVector(RectId, new Vector4(fog.Min.x, fog.Min.y, area.x, area.y));
            Shader.SetGlobalColor(ColorId, color);
            Shader.SetGlobalVector(BorderId, new Vector4(borderOpacity, borderFade, 0f, 0f));
        }

        // Zero alpha turns the fog off in the pass and in every transparent that applies it.
        [OnEnteringPlayMode]
        private static void Hide() => Shader.SetGlobalColor(ColorId, Color.clear);

        private static byte ToByte(float opacity) => (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * 255f);

        protected override void OnDestroyed()
        {
            Hide();
            DestroyTexture();
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
