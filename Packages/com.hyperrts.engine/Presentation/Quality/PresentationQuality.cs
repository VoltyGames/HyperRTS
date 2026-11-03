using System.Collections.Generic;
using HyperRTS.Core;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Quality
{
    /// <summary>
    /// Every presentation quality knob the game sets from its graphics options, applied together. Systems read the
    /// applied knobs from <see cref="Current"/>.
    /// </summary>
    public partial struct PresentationQuality
    {
        /// <summary>Particle share, flash lights and loop reach of effects.</summary>
        public DetailLevel Effects;

        /// <summary>Terrain shader detail (hex tiling, far sample, micro detail, triplanar slopes, parallax).</summary>
        public TerrainQuality Ground;

        /// <summary>Ground mark (tracks, trampling) resolution, or Off.</summary>
        public TrampleQuality Trampling;

        /// <summary>Share of grass and small plants grown, 0 (none) to 1.</summary>
        public float GrassDensity;

        /// <summary>Metres grass is drawn to.</summary>
        public float GrassDistance;

        /// <summary>Metres grass casts shadows to, 0 for none.</summary>
        public float GrassShadowDistance;

        /// <summary>Share of row crop plants grown, 0 to 1.</summary>
        public float CropDensity;

        /// <summary>Metres row crops are drawn to.</summary>
        public float CropDistance;

        /// <summary>Metres row crop plants cast shadows to, 0 for none. A plant band casts only when it ends within it.</summary>
        public float CropShadowDistance;

        /// <summary>How the row crop canopy past the plants is drawn.</summary>
        public CanopyQuality CropCanopy;

        /// <summary>
        /// Share of close-up detail grown, 0 to 1: close-only plants draw above 0, and clutter plants are thinned to
        /// this share.
        /// </summary>
        public float CloseDetail;

        /// <summary>Whether shallow water lights its bed with caustics.</summary>
        public bool WaterCaustics;

        /// <summary>Share of the screen resolution the cameras render at, above 0 and at most 1.</summary>
        public float RenderScale;

        /// <summary>Whether the smoke of the fighting thickens the volumetric fog.</summary>
        public bool BattleHaze;

        /// <summary>The full-detail knobs a session starts with, before the game applies its options.</summary>
        public static readonly PresentationQuality Default = new()
        {
            Effects = DetailLevel.High,
            Ground = TerrainQuality.Ultra,
            Trampling = TrampleQuality.Full,
            GrassDensity = 1f,
            GrassDistance = 200f,
            GrassShadowDistance = 40f,
            CropDensity = 1f,
            CropDistance = 300f,
            CropShadowDistance = 60f,
            CropCanopy = CanopyQuality.SelfShadowed,
            CloseDetail = 1f,
            WaterCaustics = true,
            RenderScale = 1f,
            BattleHaze = true,
        };

        /// <summary>The quality last applied.</summary>
        [AutoStaticsCleanup]
        public static PresentationQuality Current { get; private set; } = Default;

        // Each pipeline asset's own render scale, put back on quit so the editor never saves a player's choice.
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<UniversalRenderPipelineAsset, float> AuthoredScales = new();

        // No keyword means caustics on, so edit mode and map captures, which run without the game, show them.
        private static readonly GlobalKeyword NoCaustics = GlobalKeyword.Create("_WATER_NO_CAUSTICS");

        private static readonly GlobalKeyword CanopyParallax = GlobalKeyword.Create("_CANOPY_PARALLAX");
        private static readonly GlobalKeyword CanopySelfShadow = GlobalKeyword.Create("_CANOPY_SELF_SHADOW");

        /// <summary>Whether the canopy casts shadows at this quality.</summary>
        public bool CanopyCastsShadows => CropCanopy >= CanopyQuality.Shadowed;

        private void Check()
        {
            Require(GrassDensity is >= 0f and <= 1f, nameof(GrassDensity), GrassDensity, "0 to 1");
            Require(GrassDistance > 0f, nameof(GrassDistance), GrassDistance, "above 0");
            Require(GrassShadowDistance >= 0f, nameof(GrassShadowDistance), GrassShadowDistance, "not negative");
            Require(CropDensity is >= 0f and <= 1f, nameof(CropDensity), CropDensity, "0 to 1");
            Require(CropDistance > 0f, nameof(CropDistance), CropDistance, "above 0");
            Require(CropShadowDistance >= 0f, nameof(CropShadowDistance), CropShadowDistance, "not negative");
            Require(CloseDetail is >= 0f and <= 1f, nameof(CloseDetail), CloseDetail, "0 to 1");
            Require(System.Enum.IsDefined(typeof(CanopyQuality), CropCanopy), nameof(CropCanopy), (float)CropCanopy, "a CanopyQuality");
        }

        /// <summary>
        /// Throws naming the first bad knob before anything is written, then sets the terrain and canopy keywords, the render
        /// scale and <see cref="Current"/>. Shadows and the rest of the pipeline come from the quality level's URP
        /// asset, so set the quality level first.
        /// </summary>
        public static void Apply(in PresentationQuality quality)
        {
            quality.Check();
            var pipeline = ActivePipeline();
            TerrainDetail.SetKeywords(quality.Ground);
            Shader.SetKeyword(NoCaustics, !quality.WaterCaustics);
            SetCanopyKeywords(quality.CropCanopy);
            if (AuthoredScales.Count == 0)
            {
                Application.quitting += RestoreScales;
            }

            AuthoredScales.TryAdd(pipeline, pipeline.renderScale);
            pipeline.renderScale = quality.RenderScale;
            Current = quality;
        }

        // Global keywords outlive Play mode in the editor.
        [OnEnteringPlayMode]
        private static void ResetKeywords()
        {
            Shader.SetKeyword(NoCaustics, !Default.WaterCaustics);
            SetCanopyKeywords(Default.CropCanopy);
        }

        private static void SetCanopyKeywords(CanopyQuality canopy)
        {
            Shader.SetKeyword(CanopyParallax, canopy >= CanopyQuality.Parallax);
            Shader.SetKeyword(CanopySelfShadow, canopy == CanopyQuality.SelfShadowed);
        }

        private static void RestoreScales()
        {
            Application.quitting -= RestoreScales;
            foreach (var (pipeline, scale) in AuthoredScales)
            {
                if (pipeline != null)
                {
                    pipeline.renderScale = scale;
                }
            }

            AuthoredScales.Clear();
        }

        // Render scale is the one pipeline value URP reads live from the asset; no camera or volume carries it.
        private static UniversalRenderPipelineAsset ActivePipeline()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset asset)
            {
                throw new System.InvalidOperationException("HyperRTS: the active render pipeline isn't URP.");
            }

            return asset;
        }

        private static void Require(bool valid, string field, float value, string rule)
        {
            if (!valid)
            {
                throw new System.ArgumentOutOfRangeException(field, value, $"HyperRTS: {field} must be {rule}.");
            }
        }
    }
}
