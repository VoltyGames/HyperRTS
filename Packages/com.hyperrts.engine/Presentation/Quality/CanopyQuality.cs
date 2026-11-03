namespace HyperRTS.Presentation.Quality
{
    /// <summary>How the row crop canopy, the raised field surface past the plants, is drawn.</summary>
    public enum CanopyQuality
    {
        /// <summary>A normal-mapped surface at the crop's top that casts no shadow.</summary>
        Flat,

        /// <summary>Flat, casting shadows.</summary>
        Shadowed,

        /// <summary>Shadowed, with parallax occlusion down through the crop and its traced depth written.</summary>
        Parallax,

        /// <summary>Parallax, with the crop shading itself toward the sun.</summary>
        SelfShadowed,
    }
}
