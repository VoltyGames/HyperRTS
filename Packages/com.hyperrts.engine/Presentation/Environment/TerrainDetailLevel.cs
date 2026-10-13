namespace HyperRTS.Presentation.Environment
{
    /// <summary>How much work the terrain shader spends breaking up texture repetition.</summary>
    public enum TerrainDetailLevel
    {
        /// <summary>One sample per layer texture.</summary>
        Low,

        /// <summary>Hex tiling: three rotated, offset samples per layer texture.</summary>
        Medium,

        /// <summary>Hex tiling plus a coarse far sample that breaks up repetition at overview zoom.</summary>
        High,
    }
}
