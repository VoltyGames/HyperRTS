using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// The terrain shader's detail level, as global keywords. No keyword means Ultra, so edit mode and map view
    /// captures, which run without the game, render the full ground.
    /// </summary>
    public static class TerrainDetail
    {
        /// <summary>Low: one sample per layer texture, no far sample.</summary>
        public const string SimpleKeyword = "_TERRAIN_SIMPLE";

        /// <summary>Medium: hex tiling without the far sample.</summary>
        public const string NoFarKeyword = "_TERRAIN_NO_FAR";

        /// <summary>High: hex tiling, far sample, micro detail and triplanar slopes, without parallax.</summary>
        public const string NoParallaxKeyword = "_TERRAIN_NO_PARALLAX";

        private static readonly GlobalKeyword Simple = GlobalKeyword.Create(SimpleKeyword);
        private static readonly GlobalKeyword NoFar = GlobalKeyword.Create(NoFarKeyword);
        private static readonly GlobalKeyword NoParallax = GlobalKeyword.Create(NoParallaxKeyword);
        private static GroundLevel _level = PresentationQuality.Default.Ground;

        /// <summary>Written by <see cref="PresentationQuality.Apply"/>.</summary>
        public static GroundLevel Level
        {
            get => _level;
            internal set
            {
                _level = value;
                var keyword = KeywordFor(value);
                Shader.SetKeyword(Simple, keyword == SimpleKeyword);
                Shader.SetKeyword(NoFar, keyword == NoFarKeyword);
                Shader.SetKeyword(NoParallax, keyword == NoParallaxKeyword);
            }
        }

        /// <summary>The one keyword a level sets, or null for Ultra, which is the shader's full path.</summary>
        public static string KeywordFor(GroundLevel level) => level switch
        {
            GroundLevel.Low => SimpleKeyword,
            GroundLevel.Medium => NoFarKeyword,
            GroundLevel.High => NoParallaxKeyword,
            GroundLevel.Ultra => null,
            _ => throw new System.ArgumentOutOfRangeException(nameof(level), level, "HyperRTS: unknown ground level."),
        };
    }
}
