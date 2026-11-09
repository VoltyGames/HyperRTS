using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Scripting.LifecycleManagement;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>
    /// Publishes the fog-of-war grid and its look as shader globals, easing each cell toward its target so the fog's edge
    /// moves smoothly.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Scene + "Rig/Fog of War Renderer")]
    [Icon(HyperRTSIcons.FogOfWarRenderer)]
    [HelpURL(HyperRTSDocs.Vision)]
    [DisallowMultipleComponent]
    public partial class FogOfWarRenderer : PresentationBehaviour
    {
        private static readonly MapTextureGlobal Global = new("_HyperRTSFogOfWar", "_HyperRTSFogOfWarRect");
        private static readonly int ColorId = Shader.PropertyToID("_HyperRTSFogOfWarColor");
        private static readonly int BorderId = Shader.PropertyToID("_HyperRTSFogOfWarBorder");
        private static readonly int LookId = Shader.PropertyToID("_HyperRTSFogOfWarLook");

        [SerializeField]
        [Tooltip("Fog colour; alpha scales every opacity below.")]
        private Color color = new(0.035f, 0.033f, 0.03f, 1f);

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

        private readonly LiveSingleton<FogOfWar> _fogGrid = new();
        private readonly LiveSingleton<LocalFogView> _localView = new();
        private readonly SightShape _sightShape = new();
        private readonly FogTexture _fogTexture = new("Fog Of War", TextureFormat.R8, true);

        /// <summary>Whether a fog of war is published, so the full-screen pass has something to draw.</summary>
        public static bool Shown => Shader.GetGlobalVector(ColorId).w > 0f;

        private void LateUpdate()
        {
            if (!TryReadFog(out var entityManager, out var fog, out var view))
            {
                Hide();
                return;
            }

            if (_fogTexture.Stale(view))
            {
                _fogGrid.Complete(entityManager);
                _fogTexture.Refill(fog, view, ToByte(exploredOpacity), ToByte(unexploredOpacity));
            }

            _fogTexture.Ease(Time.deltaTime, fadeTime);

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
            var hasView = _localView.TryGet(entityManager, out view);
            if (!hasView || !view.Active)
            {
                return false;
            }

            return _fogGrid.TryGet(entityManager, out fog);
        }

        private void Publish(in FogOfWar fog)
        {
            // Unlike the ground maps, the fog rect holds the size, not its inverse.
            Global.Publish(_fogTexture.Texture, new float4(fog.Grid.Min, (float2)fog.Grid.Size * fog.Grid.CellSize));
            Shader.SetGlobalColor(ColorId, color);
            Shader.SetGlobalVector(BorderId, new Vector4(borderOpacity, borderFade, 1f / fog.Grid.Size.x, 1f / fog.Grid.Size.y));
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
            Global.Clear();
            _sightShape.Dispose();
            _fogTexture.Dispose();
        }
    }
}
