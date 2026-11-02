namespace HyperRTS.Presentation.Environment.Vegetation.Grass
{
    /// <summary>How a quality level's close detail scales a <see cref="GrassPlant"/>.</summary>
    public enum PlantKind
    {
        /// <summary>Grown at every level.</summary>
        Ground,

        /// <summary>Close-up cover that is dropped when close detail is 0.</summary>
        Close,

        /// <summary>Small scatter thinned to the close detail share.</summary>
        Clutter,
    }
}
