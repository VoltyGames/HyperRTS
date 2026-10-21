using HyperRTS.Presentation.Effects;
using HyperRTS.Presentation.Environment;
using HyperRTS.Presentation.Environment.Grass;
using HyperRTS.Presentation.Environment.RowCrops;
using HyperRTS.Simulation.Common;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Every presentation quality knob the game sets from its graphics options, applied together. The knobs it
    /// writes (<see cref="EffectDetail"/>, <see cref="TerrainDetail"/>, <see cref="InteractionMap"/>,
    /// <see cref="GrassRenderer"/>, <see cref="RowCropRenderer"/>, <see cref="CameraAntialiasingMode"/>) are read-only
    /// outside the engine.
    /// </summary>
    public struct PresentationQuality
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

        public AntialiasingMode Antialiasing;

        /// <summary>Main light shadow map size, a power of two from 256 to 8192.</summary>
        public int ShadowResolution;

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
            Antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing,
            ShadowResolution = 2048,
        };

        /// <summary>The quality last applied.</summary>
        public static PresentationQuality Current { get; private set; } = Default;

        /// <summary>Writes every knob and sets the shadow map size on the active URP asset.</summary>
        public static void Apply(in PresentationQuality quality)
        {
            WriteKnobs(quality);
            MainLightShadows.SetResolution(quality.ShadowResolution);
            Current = quality;
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
            CameraAntialiasingMode.Mode = quality.Antialiasing;
        }

        // Not shadows: the URP asset gets its authored size back on quit.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            MainLightShadows.Reset();
            CameraAntialiasingMode.Reset(Default.Antialiasing);
            WriteKnobs(Default);
            Current = Default;
        }
    }
}
