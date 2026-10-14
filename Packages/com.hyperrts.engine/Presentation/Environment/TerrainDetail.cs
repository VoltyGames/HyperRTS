using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// The terrain shader's detail level, as global keywords. No keyword means High, so edit mode and map view
    /// captures, which run without the game, render the full ground.
    /// </summary>
    public static class TerrainDetail
    {
        /// <summary>Low: one sample per layer texture, no far sample.</summary>
        public const string SimpleKeyword = "_TERRAIN_SIMPLE";

        /// <summary>Medium: hex tiling without the far sample.</summary>
        public const string NoFarKeyword = "_TERRAIN_NO_FAR";

        private static readonly GlobalKeyword Simple = GlobalKeyword.Create(SimpleKeyword);
        private static readonly GlobalKeyword NoFar = GlobalKeyword.Create(NoFarKeyword);
        private static DetailLevel _level = DetailLevel.High;

        /// <summary>Written by <see cref="PresentationQuality.Apply"/>.</summary>
        public static DetailLevel Level
        {
            get => _level;
            internal set
            {
                _level = value;
                Shader.SetKeyword(Simple, value == DetailLevel.Low);
                Shader.SetKeyword(NoFar, value == DetailLevel.Medium);
            }
        }
    }
}
