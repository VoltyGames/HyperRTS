using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using Unity.Collections;
using Unity.Scripting.LifecycleManagement;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>
    /// Publishes the fog-of-war grid and its look as shader globals for <see cref="FogOfWarFeature"/> and the
    /// transparents that fog themselves. Each restamp sets a target opacity per cell, which the shown grid eases toward
    /// over the fade time, so the fog's edge moves smoothly.
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
        private static readonly int LookId = Shader.PropertyToID("_HyperRTSFogOfWarLook");

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

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How much colour the fog drains from what lies under it, full at the explored opacity and beyond.")]
        private float desaturation = 0.7f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Seconds a cell takes to fade fully in or out of the fog.")]
        private float fadeTime = 0.3f;

        [SerializeField]
        [Tooltip("Outline round the ground the selected units see, while its key is held; alpha is its strength.")]
        private Color sightShapeColor = new(0.95f, 0.92f, 0.75f, 0.8f);

        private readonly LiveQuery _fogQuery = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<FogOfWar>()));
        private readonly LiveQuery _localView = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalFogView>()));
        private readonly SightShape _sightShape = new();
        private Texture2D _texture;
        private NativeArray<byte> _target;
        private bool _settled;
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
                Retarget(fog, view);
            }

            if (!_settled)
            {
                _settled = FogEase.Step(_texture.GetPixelData<byte>(0), _target, Time.deltaTime, fadeTime);
                _texture.Apply(false);
            }

            Publish(fog);
            _sightShape.Update(entityManager, fog, view, sightShapeColor);
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

        /// <summary>Fills the target from the latest stamp; a new grid shows it at once rather than fading in.</summary>
        private void Retarget(in FogOfWar fog, in LocalFogView view)
        {
            var created = _texture == null || _texture.width != fog.Size.x || _texture.height != fog.Size.y;
            if (created)
            {
                DestroyTexture();
                _texture = new Texture2D(fog.Size.x, fog.Size.y, TextureFormat.R8, false, true)
                {
                    name = "Fog Of War",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                _target = new NativeArray<byte>(fog.Size.x * fog.Size.y, Allocator.Persistent);
            }

            FogTexels.Fill(fog, view.Team, ToByte(exploredOpacity), ToByte(unexploredOpacity), _target);
            if (created)
            {
                _texture.GetPixelData<byte>(0).CopyFrom(_target);
                _texture.Apply(false);
            }

            _settled = created;
            _version = view.Version;
        }

        private void Publish(in FogOfWar fog)
        {
            var area = new Vector2(fog.Size.x * fog.CellSize, fog.Size.y * fog.CellSize);
            Shader.SetGlobalTexture(TextureId, _texture);
            Shader.SetGlobalVector(RectId, new Vector4(fog.Min.x, fog.Min.y, area.x, area.y));
            Shader.SetGlobalColor(ColorId, color);
            Shader.SetGlobalVector(BorderId, new Vector4(borderOpacity, borderFade, 1f / fog.Size.x, 1f / fog.Size.y));
            Shader.SetGlobalVector(LookId, new Vector4(desaturation, 0f, 0f, 0f));
        }

        // Zero alpha turns the fog off in the pass and in every transparent that applies it.
        [OnEnteringPlayMode]
        private static void Hide()
        {
            Shader.SetGlobalColor(ColorId, Color.clear);
            SightShape.Hide();
        }

        private static byte ToByte(float opacity) => (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * 255f);

        protected override void OnDestroyed()
        {
            Hide();
            _sightShape.Dispose();
            DestroyTexture();
        }

        private void DestroyTexture()
        {
            if (_texture != null)
            {
                Destroy(_texture);
            }

            if (_target.IsCreated)
            {
                _target.Dispose();
            }
        }
    }
}
