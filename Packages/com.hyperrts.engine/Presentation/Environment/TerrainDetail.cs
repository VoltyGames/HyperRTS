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
        private static TerrainDetailLevel _level = TerrainDetailLevel.High;

        /// <summary>Set by the game from its ground quality.</summary>
        public static TerrainDetailLevel Level
        {
            get => _level;
            set
            {
                _level = value;
                Shader.SetKeyword(Simple, value == TerrainDetailLevel.Low);
                Shader.SetKeyword(NoFar, value == TerrainDetailLevel.Medium);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Level = TerrainDetailLevel.High;
    }
}
