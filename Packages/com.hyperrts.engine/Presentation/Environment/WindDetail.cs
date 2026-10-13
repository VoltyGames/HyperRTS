namespace HyperRTS.Presentation.Environment
{
    /// <summary>How much wind motion the vegetation shaders draw; each level adds to the one before.</summary>
    public enum WindDetail
    {
        /// <summary>Vegetation doesn't move in the wind; only trampling and falling move it. Flags still fly.</summary>
        None = 0,

        /// <summary>Whole trees and plants bend from the root.</summary>
        Sway = 1,
        Branches = 2,
        Flutter = 3,
    }
}
