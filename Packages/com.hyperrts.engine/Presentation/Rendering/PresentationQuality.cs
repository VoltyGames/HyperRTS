using System.Collections.Generic;
using HyperRTS.Presentation.Effects;
using HyperRTS.Presentation.Environment;
using HyperRTS.Presentation.Environment.Grass;
using HyperRTS.Presentation.Environment.RowCrops;
using HyperRTS.Simulation.Common;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Every presentation quality knob the game sets from its graphics options, applied together. The knobs it
    /// writes (<see cref="EffectDetail"/>, <see cref="TerrainDetail"/>, <see cref="InteractionMap"/>,
    /// <see cref="GrassRenderer"/>, <see cref="RowCropRenderer"/>, <see cref="BattleHazeSystem"/>) are read-only outside
    /// the engine.
    /// </summary>
    public partial struct PresentationQuality
    {
        /// <summary>Particle share, flash lights and loop reach of effects.</summary>
        public DetailLevel Effects;

        /// <summary>Terrain shader detail (hex tiling, far sample, micro detail, triplanar slopes, parallax).</summary>
        public GroundLevel Ground;

        /// <summary>Ground mark (tracks, trampling) resolution, or Off.</summary>
        public InteractionQuality Interactions;

        /// <summary>Share of grass and small plants grown, above 0 and at most 1.</summary>
        public float GrassDensity;

        /// <summary>Metres grass is drawn to.</summary>
        public float GrassDistance;

        /// <summary>Metres grass casts shadows to, 0 for none.</summary>
        public float GrassShadowDistance;

        /// <summary>Share of row crop plants grown, 0 to 1.</summary>
        public float CropDensity;

        /// <summary>Metres row crops are drawn to.</summary>
        public float CropDistance;

        /// <summary>Share of the screen resolution the cameras render at, above 0 and at most 1.</summary>
        public float RenderScale;

        /// <summary>Whether the smoke of the fighting thickens the volumetric fog (<see cref="BattleHazeSystem"/>).</summary>
        public bool BattleHaze;

        /// <summary>The full-detail knobs a session starts with, before the game applies its options.</summary>
        public static readonly PresentationQuality Default = new()
        {
            Effects = DetailLevel.High,
            Ground = GroundLevel.Ultra,
            Interactions = InteractionQuality.Full,
            GrassDensity = 1f,
            GrassDistance = 200f,
            GrassShadowDistance = 40f,
            CropDensity = 1f,
            CropDistance = 300f,
            RenderScale = 1f,
            BattleHaze = true,
        };

        /// <summary>The quality last applied.</summary>
        [AutoStaticsCleanup]
        public static PresentationQuality Current { get; private set; } = Default;

        // Each pipeline asset's own render scale, put back on quit so the editor never saves a player's choice.
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<UniversalRenderPipelineAsset, float> AuthoredScales = new();

        /// <summary>
        /// Writes every knob and the render scale. Shadows and the rest of the pipeline come from the quality level's
        /// URP asset, so set the quality level first.
        /// </summary>
        public static void Apply(in PresentationQuality quality)
        {
            WriteKnobs(quality);
            var pipeline = ActivePipeline();
            if (AuthoredScales.Count == 0)
            {
                Application.quitting += RestoreScales;
            }

            AuthoredScales.TryAdd(pipeline, pipeline.renderScale);
            pipeline.renderScale = quality.RenderScale;
            Current = quality;
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

        private static void WriteKnobs(in PresentationQuality quality)
        {
            EffectDetail.Level = quality.Effects;
            TerrainDetail.Level = quality.Ground;
            InteractionMap.Quality = quality.Interactions;
            GrassRenderer.Density = quality.GrassDensity;
            GrassRenderer.Distance = quality.GrassDistance;
            GrassRenderer.ShadowDistance = quality.GrassShadowDistance;
            RowCropRenderer.Density = quality.CropDensity;
            RowCropRenderer.Distance = quality.CropDistance;
            BattleHazeSystem.Shown = quality.BattleHaze;
        }
    }
}
