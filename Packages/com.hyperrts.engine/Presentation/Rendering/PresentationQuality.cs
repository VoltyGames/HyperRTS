using HyperRTS.Presentation.Effects;
using HyperRTS.Presentation.Environment;
using HyperRTS.Presentation.Environment.RowCrops;
using HyperRTS.Simulation.Common;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Every presentation quality knob the game sets from its graphics options, applied together. The knobs it
    /// writes (<see cref="EffectDetail"/>, <see cref="TerrainDetail"/>, <see cref="InteractionMap"/>,
    /// <see cref="RowCropRenderer"/>, <see cref="CameraAntialiasingMode"/>) are read-only outside the engine.
    /// </summary>
    public struct PresentationQuality
    {
        /// <summary>Particle share, flash lights and loop reach of effects.</summary>
        public DetailLevel Effects;

        /// <summary>Terrain shader detail (hex tiling, far sample).</summary>
        public DetailLevel Ground;

        /// <summary>Ground mark (tracks, trampling) resolution, or Off.</summary>
        public InteractionQuality Interactions;

        /// <summary>Terrain grass density, 0 to 1, on every active terrain.</summary>
        public float DetailDensity;

        /// <summary>Metres terrain grass is drawn to, published to <see cref="DetailFade"/>.</summary>
        public float DetailDistance;

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
            Ground = DetailLevel.High,
            Interactions = InteractionQuality.Full,
            DetailDensity = 1f,
            DetailDistance = 150f,
            CropDensity = 1f,
            CropDistance = 300f,
            Antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing,
            ShadowResolution = 2048,
        };

        /// <summary>The quality last applied.</summary>
        public static PresentationQuality Current { get; private set; } = Default;

        /// <summary>
        /// Writes every knob, scales every active terrain's grass and sets the shadow map size on the active URP
        /// asset. Call it again after a scene loads, since map terrains are authored at full detail.
        /// </summary>
        public static void Apply(in PresentationQuality quality)
        {
            WriteKnobs(quality);
            foreach (var terrain in Terrain.activeTerrains)
            {
                terrain.detailObjectDensity = quality.DetailDensity;
                terrain.detailObjectDistance = quality.DetailDistance;
            }

            MainLightShadows.SetResolution(quality.ShadowResolution);
            Current = quality;
        }

        private static void WriteKnobs(in PresentationQuality quality)
        {
            EffectDetail.Level = quality.Effects;
            TerrainDetail.Level = quality.Ground;
            InteractionMap.Quality = quality.Interactions;
            DetailFade.Publish(quality.DetailDistance);
            RowCropRenderer.Density = quality.CropDensity;
            RowCropRenderer.Distance = quality.CropDistance;
            CameraAntialiasingMode.Mode = quality.Antialiasing;
        }

        // Not shadows or terrains: the URP asset gets its authored size back on quit, and no scene is loaded yet.
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
