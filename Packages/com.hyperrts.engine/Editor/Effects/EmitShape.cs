namespace HyperRTS.Editor.Effects
{
    /// <summary>Where a layer's particles start and which way they fly, in rig space with +Y up.</summary>
    public enum EmitShape
    {
        /// <summary>From a disc of the radius, tilted out from +Y by up to the spread toward its rim.</summary>
        Cone,

        /// <summary>From inside a half ball of the radius, flying straight out from its centre.</summary>
        Hemisphere,

        /// <summary>From a disc of the radius, flying outward along the ground.</summary>
        Ring,
    }
}
